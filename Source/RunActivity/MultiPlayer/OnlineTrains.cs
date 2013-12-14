// COPYRIGHT 2012, 2013 by the Open Rails project.
// 
// This file is part of Open Rails.
// 
// Open Rails is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// Open Rails is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with Open Rails.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MSTS;
using System.IO;

namespace ORTS.MultiPlayer
{
	public class OnlineTrains
	{
		public Dictionary<string, OnlinePlayer> Players;
		public OnlineTrains()
		{
			Players = new Dictionary<string, OnlinePlayer>();
		}
		public static void Update()
		{

		}

		public Train findTrain(string name)
		{
			if (Players.ContainsKey(name))
				return Players[name].Train;
			else return null;
		}

		public bool findTrain(Train t)
		{

			foreach (OnlinePlayer o in Players.Values.ToList())
			{
				if (o.Train == t) return true;
			}
			return false;
		}

		public string MoveTrains(MSGMove move)
		{
			string tmp = "";
			if (move == null) move = new MSGMove();
			foreach (OnlinePlayer p in Players.Values)
			{
				if (p.Train != null && Program.Simulator.PlayerLocomotive != null && p.Train != Program.Simulator.PlayerLocomotive.Train)
				{
					if (Math.Abs(p.Train.SpeedMpS) > 0.001 || Math.Abs(p.Train.LastReportedSpeed) > 0)
					{
						move.AddNewItem(p.Username, p.Train);
					}
				}
			}
			foreach (Train t in Program.Simulator.Trains)
			{
				if (Program.Simulator.PlayerLocomotive != null && t == Program.Simulator.PlayerLocomotive.Train) continue;//player drived train
				if (t == null || findTrain(t)) continue;//is an online player controlled train
				if (Math.Abs(t.SpeedMpS) > 0.001 || Math.Abs(t.LastReportedSpeed) > 0)
				{
					move.AddNewItem("0xAI"+t.Number, t);
				}
			}
			tmp += move.ToString();
			return tmp;

		}
		public string MoveAllPlayerTrain(MSGMove move)
		{
			string tmp = "";
			if (move == null) move = new MSGMove();
			foreach (OnlinePlayer p in Players.Values)
			{
				if (p.Train == null) continue;
				if (Math.Abs(p.Train.SpeedMpS) > 0.001 || Math.Abs(p.Train.LastReportedSpeed) > 0)
				{
					move.AddNewItem(p.Username, p.Train);
				}
			}
			tmp += move.ToString();
			return tmp;
		}

		public static string MoveAllTrain(MSGMove move)
		{
			string tmp = "";
			if (move == null) move = new MSGMove();
			foreach (Train t in Program.Simulator.Trains)
			{
				if (t != null && (Math.Abs(t.SpeedMpS) > 0.001 || Math.Abs(t.LastReportedSpeed) > 0))
				{
					move.AddNewItem("AI"+t.Number, t);
				}
			}
			tmp += move.ToString();
			return tmp;
		}

		public string AddAllPlayerTrain() //WARNING, need to change
		{
			string tmp = "";
			foreach (OnlinePlayer p in Players.Values)
			{
				if (p.Train != null)
				{
					MSGPlayer player = new MSGPlayer(p.Username, "1234", p.con, p.path, p.Train, p.Train.Number, p.url);
					tmp += player.ToString();
				}
			}
			return tmp;
		}
		public void AddPlayers(MSGPlayer player, OnlinePlayer p)
		{
			if (Players.ContainsKey(player.user)) return;
			if (Program.Client != null && player.user == Program.Client.UserName) return; //do not add self//WARNING: may need to worry about train number here
			if (p == null)
			{
				p = new OnlinePlayer(null, null);
			}
			p.url = player.url; 
			p.LeadingLocomotiveID = player.leadingID;
			p.con = Program.Simulator.BasePath + "\\TRAINS\\CONSISTS\\" + player.con;
			p.path = Program.Simulator.RoutePath + "\\PATHS\\" + player.path;
			Train train = new Train(Program.Simulator);
            AIPath aiPath = null;
			train.TrainType = Train.TRAINTYPE.REMOTE;
			if (MPManager.IsServer()) //server needs to worry about correct train number
			{
                train.ControlMode = (Train.TRAIN_CONTROL)player.cmode;
                train.MpControlMode = (Train.MP_CONTROL)player.mpcmode;
			}
			else
			{
				train.Number = player.num;
			}
            if (player.con.Contains("tilted")) train.tilted = true;
			int direction = player.dir;
			train.travelled = player.Travelled;

			if (MPManager.IsServer() && train.MpControlMode == Train.MP_CONTROL.PATHED)
			{
                try
                {
                    PATFile patFile = new PATFile(p.path);
                    aiPath = new AIPath(patFile, Program.Simulator.TDB, Program.Simulator.TSectionDat, p.path);
                }
                catch (Exception)
                {
                    if (train.MpControlMode == Train.MP_CONTROL.PATHED)
                    {
                        train.MpControlMode = Train.MP_CONTROL.WAITDISPATCH;
                        MPManager.BroadCast((new MSGMessage(player.user, "Warning", "Path is not available at server - mode is set to WaitDispatch")).ToString());
                    }
                }
            }

			try
			{
				train.RearTDBTraveller = new Traveller(Program.Simulator.TSectionDat, Program.Simulator.TDB.TrackDB.TrackNodes, player.TileX, player.TileZ, player.X, player.Z, direction == 1 ? Traveller.TravellerDirection.Forward : Traveller.TravellerDirection.Backward);
			}
			catch (Exception e)
			{
				if (MPManager.IsServer())
				{
					MPManager.BroadCast((new MSGMessage(player.user, "Error", "MultiPlayer Error：" + e.Message)).ToString());
				}
				else throw new Exception();
			}
			for (var i = 0; i < player.cars.Length; i++)// cars.Length-1; i >= 0; i--) {
			{

				string wagonFilePath = Program.Simulator.BasePath + @"\trains\trainset\" + player.cars[i];
				TrainCar car = null;
				try
				{
                    car = RollingStock.Load(Program.Simulator, wagonFilePath);
					car.LengthM = player.lengths[i];
				}
				catch (Exception error)
				{
					System.Console.WriteLine(error.Message);
                    car = MPManager.Instance().SubCar(wagonFilePath, player.lengths[i]);
				}
				if (car == null) continue;
				bool flip = true;
				if (player.flipped[i] == 0) flip = false;
				car.Flipped = flip;
				car.CarID = player.ids[i];
				train.Cars.Add(car);
				car.Train = train;
				MSTSWagon w = (MSTSWagon)car;
				if (w != null)
				{
					w.Pan1Up = player.pantofirst == 1 ? true : false;
					w.Pan2Up = player.pantosecond == 1 ? true : false;
				}

			}// for each rail car

			if (train.Cars.Count == 0)
			{
				throw (new Exception("The train of player " + player.user + " is empty from "));
			}

			p.Username = player.user;
            train.CheckFreight();
            train.InitializeBrakes();
            bool canPlace = true;
            Train.TCSubpathRoute tempRoute = train.CalculateInitialTrainPosition(ref canPlace);
            if (tempRoute.Count == 0 || !canPlace)
            {
                MPManager.BroadCast((new MSGMessage(p.Username, "Error", "Cannot be placed into the game")).ToString());//server will broadcast this error
                throw new InvalidDataException("Remote train original position not clear");
            }

            train.SetInitialTrainRoute(tempRoute);
            train.CalculatePositionOfCars(0);
            train.ResetInitialTrainRoute(tempRoute);

            train.InitialTrainPlacement();
            train.CalculatePositionOfCars(0);
            train.AITrainBrakePercent = 100;

            if (train.MpControlMode == Train.MP_CONTROL.PATHED)
            {
                train.SetRoutePath(aiPath, Program.Simulator.Signals);
                train.BuildWaitingPointList(0.0f);
            }

			//if (MPManager.Instance().AllowedManualSwitch) train.InitializeSignals(false);
			foreach (var car in train.Cars) {
				if (car.CarID == p.LeadingLocomotiveID) train.LeadLocomotive = car;
			}
			if (train.LeadLocomotive == null)
			{
                train.LeadNextLocomotive();
				if (train.LeadLocomotive != null) p.LeadingLocomotiveID = train.LeadLocomotive.CarID;
				else p.LeadingLocomotiveID = "NA";
			}
			p.Train = train;
			
			Players.Add(player.user, p);
			MPManager.Instance().AddOrRemoveTrain(train, true);

		}
	}
}
