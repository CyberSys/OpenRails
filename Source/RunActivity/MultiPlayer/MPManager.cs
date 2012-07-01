﻿/* MPManager
 * 
 * Contains code to manager multiplayer sessions, especially to hide server/client mode from other code.
 * For example, the Notify method will check if it is server (then broadcast) or client (then send msg to server)
 * but the caller does not need to care.
 * 
 * 
/// COPYRIGHT 2009 by the Open Rails project.
/// This code is provided to enable you to contribute improvements to the open rails program.  
/// Use of the code for any other purpose or distribution of the code to anyone else
/// is prohibited without specific written permission from admin@openrails.org.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ORTS;
using ORTS.Debugging;
using System.Threading;

namespace ORTS.MultiPlayer
{
	//a singleton class handles communication, update and stop etc.
	class MPManager
	{
		double lastMoveTime = 0.0f;
		double lastSwitchTime = 0.0f;
		double lastSendTime = 0.0f;
		string metric = "";
		double metricbase = 1.0f;
		public static OnlineTrains OnlineTrains = new OnlineTrains();
		private static MPManager localUser = null;

		private List<Train> removedTrains;
		private List<Train> addedTrains;

		private List<Train> uncoupledTrains;

		public void AddUncoupledTrains(Train t)
		{
			lock (uncoupledTrains)
			{
				uncoupledTrains.Add(t);
			}
		}

		public void RemoveUncoupledTrains(Train t)
		{
			lock (uncoupledTrains)
			{
				uncoupledTrains.Remove(t);
			}
		}

		public void MoveUncoupledTrains(MSGMove move)
		{
			if (uncoupledTrains != null && uncoupledTrains.Count > 0)
			{
				foreach (Train t in uncoupledTrains)
				{
					if (t != null)
					{
						if (Math.Abs(t.SpeedMpS) > 0.001) move.AddNewItem("0xUC" + t.Number, t);
						else if (Math.Abs(t.LastReportedSpeed) > 0) move.AddNewItem("0xUC" + t.Number, t);
					}
				}
			}
		}
		//handles singleton
		private MPManager()
		{
			playersRemoved = new List<OnlinePlayer>();
			uncoupledTrains = new List<Train>();
			addedTrains = new List<Train>();
			removedTrains = new List<Train>();
		}
		public static MPManager Instance()
		{
			if (localUser == null) localUser = new MPManager();
			return localUser;
		}

		/// <summary>
		/// Update. Determines what messages to send every some seconds
		/// 1. every one second will send train location
		/// 2. every 10 seconds will send switch status
		/// 3. housekeeping (remove/add trains, remove players)
		/// 4. it will also capture key stroke of horn, panto, wiper, bell, headlight etc.
		/// </summary>

		public void RequestControl()
		{
			try
			{
				Train train = Program.Simulator.PlayerLocomotive.Train;
				var gainControl = true;
				foreach (var pair in MPManager.OnlineTrains.Players)
				{
					string check = pair.Key + " - 0";
					foreach (var car1 in train.Cars) if (car1.CarID.StartsWith(check)) { gainControl = false; break; }
				}
				if (gainControl == true) { 
					train.TrainType = Train.TRAINTYPE.PLAYER; train.LeadLocomotive = Program.Simulator.PlayerLocomotive;
					if (Program.Simulator.Confirmer != null)
						Program.Simulator.Confirmer.Message("Info:", "You gained back the control of your train");
					return; 
				}
				MSGControl msgctl;
				//I am the server, I have control
				if (IsServer())
				{
					train.TrainType = Train.TRAINTYPE.PLAYER; train.LeadLocomotive = Program.Simulator.PlayerLocomotive;
					if (Program.Simulator.Confirmer != null)
						Program.Simulator.Confirmer.Message("Info:", "You gained back the control of your train");
					msgctl = new MSGControl(GetUserName(), "Confirm", train);
					BroadCast(msgctl.ToString());
				}
				else //client, send request
				{
					msgctl = new MSGControl(GetUserName(), "Request", train);
					SendToServer(msgctl.ToString());
				}
			}
			catch (Exception)
			{ }
		}
		/// <summary>
		/// Update. Determines what messages to send every some seconds
		/// 1. every one second will send train location
		/// 2. every 10 seconds will send switch status
		/// 3. housekeeping (remove/add trains, remove players)
		/// 4. it will also capture key stroke of horn, panto, wiper, bell, headlight etc.
		/// </summary>

		public void Update(double newtime)
		{
			//get key strokes and determine if some messages should be sent
			handleUserInput();

			//server update train location of all
			if (Program.Server != null && newtime - lastMoveTime >= 1f)
			{
				MSGMove move = new MSGMove();
				move.AddNewItem(MultiPlayer.MPManager.GetUserName(), Program.Simulator.PlayerLocomotive.Train);
				Program.Server.BroadCast(OnlineTrains.MoveTrains(move));
				lastMoveTime = lastSendTime = newtime;
			}
			
			//server updates switch
			if (Program.Server != null && newtime - lastSwitchTime >= 10f)
			{
				lastSwitchTime = lastSendTime = newtime;
				MultiPlayer.MPManager.BroadCast((new MultiPlayer.MSGSwitchStatus()).ToString());
				MultiPlayer.MPManager.BroadCast((new MSGSignalStatus()).ToString());

			}
			
			//client updates itself
			if (Program.Client != null && Program.Server == null && newtime - lastMoveTime >= 1f)
			{
				Train t = Program.Simulator.PlayerLocomotive.Train;
				MSGMove move = new MSGMove();
				//if I am still conrolling the train
				if (t.TrainType != Train.TRAINTYPE.REMOTE)
				{
					if (Math.Abs(t.SpeedMpS) > 0.001) move.AddNewItem(MultiPlayer.MPManager.GetUserName(), t);
					else if (Math.Abs(t.LastReportedSpeed) > 0) move.AddNewItem(MultiPlayer.MPManager.GetUserName(), t);
				}
				MoveUncoupledTrains(move); //if there are uncoupled trains
				//if there are messages to send
				if (move.OKtoSend())
				{
					Program.Client.Send(move.ToString());
					lastMoveTime = lastSendTime = newtime;
				}
			}

			//need to send a keep-alive message if have not sent one to the server for the last 30 seconds
			if (Program.Client != null && Program.Server == null && newtime - lastSendTime >= 30f)
			{
				MPManager.Notify((new MSGAlive(GetUserName())).ToString());
				lastSendTime = newtime;
			}

			//some players are removed
			RemovePlayer();

			//some trains are added/removed
			HandleTrainList();
		}

		//check if it is in the server mode
		public static bool IsServer()
		{
			if (Program.Server != null) return true;
			else return false;
		}

		//user name
		static public string GetUserName()
		{
			if (Program.Server != null) return Program.Server.UserName;
			else if (Program.Client != null) return Program.Client.UserName;
			else return "";
		}

		//check if it is in the multiplayer session
		static public bool IsMultiPlayer()
		{
			if (Program.Server != null || Program.Client != null) return true;
			else return false;
		}

		static public void BroadCast(string m)
		{
			if (Program.Server != null) Program.Server.BroadCast(m);
		}

		//notify others (server will broadcast, client will send msg to server)
		static public void Notify(string m)
		{
			if (Program.Client != null && Program.Server == null) Program.Client.Send(m); //client notify server
			if (Program.Server != null) Program.Server.BroadCast(m); //server notify everybody else
		}

		static public void SendToServer(string m)
		{
			if (Program.Client != null) Program.Client.Send(m);
		}
		static public void BroadcastSignal()
		{
		}
		static public void BroadcastSignal(Signal s)
		{

		}

		//nicely shutdown listening threads, and notify the server/other player
		static public void Stop()
		{
			if (Program.Client != null && Program.Server == null)
			{
				Program.Client.Send((new MSGQuit(GetUserName())).ToString()); //client notify server
				Thread.Sleep(1000);
				Program.Client.Stop();
			}
			if (Program.Server != null)
			{
				Program.Server.BroadCast((new MSGQuit("ServerHasToQuit\t"+GetUserName())).ToString()); //server notify everybody else
				Thread.Sleep(1000);
				if (Program.Server.ServerComm != null) Program.Server.Stop();
				if (Program.Client != null) Program.Client.Stop();
			}
			
		}

		//this will be used in the server, in Simulator.cs
		public bool TrainOK2Couple(Train t1, Train t2)
		{

			if (t1.TrainType != Train.TRAINTYPE.REMOTE && t2.TrainType != Train.TRAINTYPE.REMOTE) return true;

			bool result = true;
			try
			{
				foreach (var p in OnlineTrains.Players)
				{
					if (p.Value.Train == t1 && Program.Simulator.GameTime  - p.Value.CreatedTime < 120) { result = false; break; }
					if (p.Value.Train == t2 && Program.Simulator.GameTime - p.Value.CreatedTime < 120) { result = false; break; }
				}
			}
			catch (Exception)
			{
			}
			return result;
		}
		/// <summary>
		/// Return a string of information of how many players online and those users who are close
		/// </summary>
		public string GetOnlineUsersInfo()
		{

			string info = "";
			if (Program.Simulator.PlayerLocomotive.Train.TrainType == Train.TRAINTYPE.REMOTE) info = "Your locomotive is a helper\t";
			info += ("" + (OnlineTrains.Players.Count + 1)+ (OnlineTrains.Players.Count <= 0 ? " player " : "  players "));
			info += ("" + Program.Simulator.Trains.Count + (Program.Simulator.Trains.Count <= 1 ? " train" : "  trains"));
			//foreach (var train in Program.Simulator.Trains) info += "\t" + train.Number + " " + train.Cars.Count;
			//info += "\t" + MPManager.OnlineTrains.Players.Count;
			//foreach (var p in MPManager.OnlineTrains.Players) info += "\t" + p.Value.Train.Number + " " + p.Key;
			TrainCar mine = Program.Simulator.PlayerLocomotive;
			SortedList<double, string> users = new SortedList<double,string>();
			try//the list of players may be changed during the following process
			{
				foreach (OnlinePlayer p in OnlineTrains.Players.Values)
				{
					if (p.Train == null) continue;
					if (p.Train.Cars.Count <= 0) continue;
					var d = WorldLocation.GetDistanceSquared(p.Train.RearTDBTraveller.WorldLocation, mine.Train.RearTDBTraveller.WorldLocation);
					users.Add(Math.Sqrt(d)+Program.Random.NextDouble(), p.Username);
				}
			}
			catch (Exception)
			{
			}
			if (metric == "")
			{
				metric = Program.Simulator.TRK.Tr_RouteFile.MilepostUnitsMetric == true ? " m" : " yd";
				metricbase = Program.Simulator.TRK.Tr_RouteFile.MilepostUnitsMetric == true ? 1.0f : 1.0936133f;
			}

			foreach(var pair in users)
			{
				info += "\t" + pair.Value + ": distance of " + (int)(pair.Key/metricbase) + metric;
			}
			return info;
		}

		private List<OnlinePlayer> playersRemoved;
		public void AddRemovedPlayer(OnlinePlayer p)
		{
			lock (playersRemoved)
			{
				playersRemoved.Add(p);
			}
		}

		//only can be called by Update
		private void RemovePlayer()
		{
			//if (Program.Server == null) return; //client will do it by decoding message
			if (playersRemoved.Count == 0) return;

			try //do it without lock, so may have exception
			{
				foreach (OnlinePlayer p in playersRemoved)
				{
					Program.Server.Players.Remove(p);
					MPManager.OnlineTrains.Players.Remove(p.Username);
					//player is not in this train
					if (p.Train != Program.Simulator.PlayerLocomotive.Train)
					{
						Program.Simulator.Trains.Remove(p.Train);
					}
				}
			}
			catch (Exception)
			{
				return;
			}
			playersRemoved.Clear();
		}

		public void AddOrRemoveTrain(Train t, bool add)
		{
			if (add)
			{
				lock (addedTrains)
				{
					addedTrains.Add(t); return;
				}
			}
			else
			{
				lock (removedTrains)
				{
					removedTrains.Add(t); return;
				}
			}
		}
		//only can be called by Update
		private void HandleTrainList()
		{
			if (addedTrains.Count != 0)
			{

				try //do it without lock, so may have exception
				{
					foreach (var t in addedTrains)
					{
						Program.Simulator.Trains.Add(t);
					}
					addedTrains.Clear();
				}
				catch (Exception) { }
			}
			if (removedTrains.Count != 0)
			{

				try //do it without lock, so may have exception
				{
					foreach (var t in removedTrains)
					{
						Program.Simulator.Trains.Remove(t);
					}
					removedTrains.Clear();
				}
				catch (Exception) { }
			}
		}

		public Train FindPlayerTrain(string user)
		{
			return OnlineTrains.findTrain(user);
		}

		public bool FindPlayerTrain(Train t)
		{
			return OnlineTrains.findTrain(t);
		}

		//count how many times a key has been stroked, thus know if the panto should be up or down, etc. for example, stroke 11 times means up, thus send event with id 1
		int PantoSecondCount = 0;
		int PantoFirstCount = 0;
		int BellCount = 0;
		int WiperCount = 0;
		int HeadLightCount = 0;

		public void handleUserInput()
		{
			TrainCar Locomotive = Program.Simulator.PlayerLocomotive;
			//In Multiplayer, I maybe the helper, but I can request to be the controller
			if (UserInput.IsPressed(UserCommands.GameRequestControl))
			{
				RequestControl();
			}

			if (UserInput.IsPressed(UserCommands.ControlHorn))	MPManager.Notify((new MSGEvent(MPManager.GetUserName(), "HORN", EventID.HornOn)).ToString());

			if (UserInput.IsReleased(UserCommands.ControlHorn)) MPManager.Notify((new MSGEvent(MPManager.GetUserName(), "HORN", EventID.HornOff)).ToString());
			
			if (UserInput.IsPressed(UserCommands.ControlPantographSecond)) MPManager.Notify((new MSGEvent(MPManager.GetUserName(), "PANTO2", (++PantoSecondCount)%2)).ToString());

			if (UserInput.IsPressed(UserCommands.ControlPantographFirst)) MPManager.Notify((new MSGEvent(MPManager.GetUserName(), "PANTO1", (++PantoFirstCount)%2)).ToString());

			if (UserInput.IsPressed(UserCommands.ControlBell)) MPManager.Notify((new MSGEvent(MPManager.GetUserName(), "BELL", (++BellCount)%2)).ToString());

			if (UserInput.IsPressed(UserCommands.ControlWiper)) MPManager.Notify((new MSGEvent(MPManager.GetUserName(), "WIPER", (++WiperCount) % 2)).ToString());

			if (UserInput.IsPressed(UserCommands.ControlHeadlightIncrease))
			{
				HeadLightCount++; if (HeadLightCount >= 3) HeadLightCount = 2;
				MPManager.Notify((new MSGEvent(MPManager.GetUserName(), "HEADLIGHT", HeadLightCount)).ToString());
			}

			if (UserInput.IsPressed(UserCommands.ControlHeadlightDecrease))
			{
				HeadLightCount--; if (HeadLightCount < 0) HeadLightCount = 0;
				MPManager.Notify((new MSGEvent(MPManager.GetUserName(), "HEADLIGHT", HeadLightCount)).ToString());
			}
		}

	}
}
