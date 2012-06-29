﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ORTS;
using MSTS;
namespace ORTS.MultiPlayer
{
	public class Message
	{
		public string msg;
		public static Message Decode(string m)
		{
			int index = m.IndexOf(' ');
			string key = m.Substring(0, index);
			if (key == "MOVE") return new MSGMove(m.Substring(index + 1));
			else if (key == "PLAYER") return new MSGPlayer(m.Substring(index + 1));
			else if (key == "SWITCHSTATES") return new MSGSwitchStatus(m.Substring(index + 1));
			else if (key == "SIGNALSTATES") return new MSGSignalStatus(m.Substring(index + 1));
			else if (key == "ALIVE") return new MSGAlive(m.Substring(index + 1));
			else if (key == "SWITCH") return new MSGSwitch(m.Substring(index + 1));
			else if (key == "TRAIN") return new MSGTrain(m.Substring(index + 1));
			else if (key == "REMOVETRAIN") return new MSGRemoveTrain(m.Substring(index + 1));
			else if (key == "SERVER") return new MSGServer(m.Substring(index + 1));
			else if (key == "MESSAGE") return new MSGMessage(m.Substring(index + 1));
			else if (key == "EVENT") return new MSGEvent(m.Substring(index + 1));
			else if (key == "UNCOUPLE") return new MSGUncouple(m.Substring(index + 1));
			else if (key == "COUPLE") return new MSGCouple(m.Substring(index + 1));
			else if (key == "GETTRAIN") return new MSGGetTrain(m.Substring(index + 1));
			else if (key == "UPDATETRAIN") return new MSGUpdateTrain(m.Substring(index + 1));
			else if (key == "QUIT") return new MSGQuit(m.Substring(index + 1));
			else throw new Exception("Unknown Keyword" + key);
		}

		public virtual void HandleMsg() { System.Console.WriteLine("test"); return; }
	}

	#region MSGMove
	public class MSGMove : Message
	{
		class MSGMoveItem
		{
			public string user;
			public float speed;
			public float travelled;
			public int num, count;
			public int TileX, TileZ, trackNodeIndex, direction;
			public float X, Z;
			public MSGMoveItem(string u, float s, float t, int n, int tX, int tZ, float x, float z, int tni, int cnt, int dir)
			{
				user = u; speed = s; travelled = t; num = n; TileX = tX; TileZ = tZ; X = x; Z = z; trackNodeIndex = tni; count = cnt; direction = dir;
			}
			public override string ToString()
			{
				return user + " " + speed + " " + travelled + " " + num + " " + TileX + " " + TileZ + " " + X + " " + Z + " " + trackNodeIndex + " " + count + " " + direction;
			}
		}
		List<MSGMoveItem> items;
		public MSGMove(string m)
		{
			m = m.Trim();
			string[] areas = m.Split(' ');
			if (areas.Length%11  != 0) //user speed travelled
			{
				throw new Exception("Parsing error " + m);
			}
			try
			{
				int i = 0;
				items = new List<MSGMoveItem>();
				for (i = 0; i < areas.Length / 11; i++)
					items.Add(new MSGMoveItem(areas[11 * i], float.Parse(areas[11 * i + 1]), float.Parse(areas[11 * i + 2]), int.Parse(areas[11 * i + 3]), int.Parse(areas[11 * i + 4]), int.Parse(areas[11 * i + 5]), float.Parse(areas[11 * i + 6]), float.Parse(areas[11 * i + 7]), int.Parse(areas[11 * i + 8]), int.Parse(areas[11 * i + 9]), int.Parse(areas[11 * i + 10])));
			}
			catch (Exception e)
			{
				throw e;
			}
		}

		static Dictionary<int, int> MissingTimes;

		//a train is missing, but will wait for 10 messages then ask
		static bool CheckMissingTimes(int TNumber)
		{
			if (MissingTimes == null) MissingTimes = new Dictionary<int, int>();
			try
			{
				if (MissingTimes[TNumber] < 10) { MissingTimes[TNumber]++; return false; }
				else { MissingTimes[TNumber] = 0; return true; }
			}
			catch (Exception)
			{
				MissingTimes.Add(TNumber, 1);
				return false;
			}

		}

		public MSGMove()
		{
		}

		public void AddNewItem(string u, Train t)
		{
			if (items == null) items = new List<MSGMoveItem>();
			items.Add(new MSGMoveItem(u, t.SpeedMpS, t.travelled, t.Number, t.RearTDBTraveller.TileX, t.RearTDBTraveller.TileZ, t.RearTDBTraveller.X, t.RearTDBTraveller.Z, t.RearTDBTraveller.TrackNodeIndex, t.Cars.Count, (int)t.MUDirection));
			t.LastReportedSpeed = t.SpeedMpS;
		}

		public bool OKtoSend()
		{
			if (items != null && items.Count > 0) return true;
			return false;
		}
		public override string ToString()
		{
			string tmp = "MOVE ";
			for (var i = 0; i < items.Count; i++) tmp += items[i].ToString() + " ";
			return "" + tmp.Length + ": " + tmp;
		}

		public override void HandleMsg()
		{
			foreach (MSGMoveItem m in items)
			{
				bool found = false; //a train may not be in my sim
				if (m.user == MPManager.GetUserName())//about itself, check if the number of car has changed, otherwise ignore
				{
					found = true;
					try
					{
						if (m.count != Program.Simulator.PlayerLocomotive.Train.Cars.Count)
						{
							if (!MPManager.IsServer() && CheckMissingTimes(Program.Simulator.PlayerLocomotive.Train.Number)) MPManager.SendToServer((new MSGGetTrain(MPManager.GetUserName(), Program.Simulator.PlayerLocomotive.Train.Number)).ToString());
						}
					}
					catch (Exception) { }
					continue; 
				}
				if (m.user.Contains("0xAI") || m.user.Contains("0xUC"))
				{
					foreach (Train t in Program.Simulator.Trains)
					{
						if (t.Number == m.num)
						{
							found = true;
							if (t.Cars.Count != m.count) //the number of cars are different, client will not update it, ask for new information
							{
								if (!MPManager.IsServer())
								{
									if (CheckMissingTimes(t.Number)) MPManager.SendToServer((new MSGGetTrain(MPManager.GetUserName(), t.Number)).ToString());
									continue;
								}
							}
							if (t.TrainType == Train.TRAINTYPE.REMOTE)
							{
								t.ToDoUpdate(m.trackNodeIndex, m.TileX, m.TileZ, m.X, m.Z, m.travelled, m.speed, m.direction);
								break;
							}
						}
					}
				}
				else
				{
					Train t = MPManager.OnlineTrains.findTrain(m.user);
					if (t != null)
					{
							found = true;
							t.ToDoUpdate(m.trackNodeIndex, m.TileX, m.TileZ, m.X, m.Z, m.travelled, m.speed, m.direction);
					}
				}
				if (found == false) //I do not have the train, tell server to send it to me
				{
					if (!MPManager.IsServer() && CheckMissingTimes(m.num)) MPManager.SendToServer((new MSGGetTrain(MPManager.GetUserName(), m.num)).ToString());
				}
			}
		}
	}
	#endregion MSGMove

	#region MSGPlayer
	public class MSGPlayer : Message
	{
		public string user = "";
		public string code = "";
		public int num; //train number
		public string con; //consist
		public string path; //path consist and path will always be double quoted
		public string route;
		public int dir; //direction
		public int TileX, TileZ;
		public float X, Z, Travelled;
		public double seconds;
		public int season, weather;
		public int pantofirst, pantosecond;

		public string[] cars;
		public string[] ids;
		public int[] flipped; //if a wagon is engine

		public MSGPlayer(string m)
		{
			string[] areas = m.Split('\r');
			if (areas.Length <= 5)
			{
				throw new Exception("Parsing error in MSGPlayer" + m);
			}
			try
			{
				var tmp = areas[0].Trim();
				string[] data = tmp.Split(' ');
				user = data[0];
				code = data[1];
				num = int.Parse(data[2]);
				TileX = int.Parse(data[3]);
				TileZ = int.Parse(data[4]);
				X = float.Parse(data[5]);
				Z = float.Parse(data[6]);
				Travelled = float.Parse(data[7]);
				seconds = double.Parse(data[8]);
				season = int.Parse(data[9]);
				weather = int.Parse(data[10]);
				pantofirst = int.Parse(data[11]);
				pantosecond = int.Parse(data[12]);
				//user = areas[0].Trim();
				con = areas[1].Trim();
				route = areas[2].Trim();
				path = areas[3].Trim();
				dir = int.Parse(areas[4].Trim());
				ParseTrainCars(areas[5].Trim());
				int index = path.LastIndexOf("\\PATHS\\", StringComparison.OrdinalIgnoreCase);
				if (index > 0)
				{
					path = path.Remove(0, index + 7);
				}
				index = con.LastIndexOf("\\CONSISTS\\", StringComparison.OrdinalIgnoreCase);
				if (index > 0)
				{
					con = con.Remove(0, index + 10);
				}

			}
			catch (Exception e)
			{
				throw e;
			}
		}


		private void ParseTrainCars(string m)
		{
			string[] areas = m.Split('\t');
			var numCars = areas.Length;
			if (MPManager.IsServer()) if (numCars <= 0) throw new MultiPlayerError();
				else if (numCars <= 0) throw new Exception();
			cars = new string[numCars];//with an empty "" at end
			ids = new string[numCars];
			flipped = new int[numCars];
			int index, last;
			for (var i = 0; i < numCars; i++)
			{
				index = areas[i].IndexOf('\"');
				last = areas[i].LastIndexOf('\"');
				cars[i] = areas[i].Substring(index + 1, last - index - 1);
				string tmp = areas[i].Remove(0, last + 1);
				tmp = tmp.Trim();
				string[] carinfo = tmp.Split('\n');
				ids[i] = carinfo[0];
				flipped[i] = int.Parse(carinfo[1]);
			}

		}
		public MSGPlayer(string n, string cd, string c, string p, Train t, int tn)
		{
			route = Program.Simulator.RouteName;
			int index = p.LastIndexOf("\\PATHS\\", StringComparison.OrdinalIgnoreCase);
			if (index > 0)
			{
				p = p.Remove(0, index + 7);
			}
			index = c.LastIndexOf("\\CONSISTS\\", StringComparison.OrdinalIgnoreCase);
			if (index > 0)
			{
				c = c.Remove(0, index + 10);
			}
			user = n; code = cd; con = c; path = p;
			if (t != null)
			{
				dir = (int)t.RearTDBTraveller.Direction; num = tn; TileX = t.RearTDBTraveller.TileX;
				TileZ = t.RearTDBTraveller.TileZ; X = t.RearTDBTraveller.X; Z = t.RearTDBTraveller.Z; Travelled = t.travelled;
			}
			seconds = Program.Simulator.ClockTime; season = (int)Program.Simulator.Season; weather = (int)Program.Simulator.Weather;
			pantofirst = pantosecond = 0;
			MSTSWagon w = (MSTSWagon)Program.Simulator.PlayerLocomotive;
			if (w != null)
			{
				pantofirst = w.AftPanUp == true ? 1 : 0;
				pantosecond = w.FrontPanUp == true ? 1 : 0;
			}

			cars = new string[t.Cars.Count];
			ids = new string[t.Cars.Count];
			flipped = new int[t.Cars.Count];
			for (var i = 0; i < t.Cars.Count; i++)
			{
				cars[i] = t.Cars[i].WagFilePath;
				ids[i] = t.Cars[i].CarID;
				if (t.Cars[i].Flipped == true) flipped[i] = 1;
				else flipped[i] = 0;
			}

		}
		public override string ToString()
		{
			string tmp = "PLAYER " + user + " " + code + " " + num + " " + TileX + " " + TileZ + " " + X + " " + Z + " " + Travelled + " " + seconds + " " + season + " " + weather + " " + pantofirst + " " + pantosecond + " \r" + con + "\r" + route + "\r" + path + "\r" + dir + "\r";
			for (var i = 0; i < cars.Length; i++)
			{
				var c = cars[i];
				var index = c.LastIndexOf("\\trains\\trainset\\", StringComparison.OrdinalIgnoreCase);
				if (index > 0)
				{
					c = c.Remove(0, index + 17);
				}//c: wagon path without folder name

				tmp += "\"" + c + "\"" + " " + ids[i] + "\n" + flipped[i] + "\t";
			}

			return "" + tmp.Length + ": " + tmp;
		}

		public override void HandleMsg()
		{
			//check if other players with the same name is online
			if (MPManager.IsServer())
			{
				//if someone with the same name is there, will throw a fatal error
				if (MPManager.OnlineTrains.findTrain(user) != null || MPManager.GetUserName() == user)
				{
					MPManager.BroadCast((new MSGMessage(user, "Error", "A user with the same name exists")).ToString());
					throw new MultiPlayerError();
				}
			}
			MPManager.OnlineTrains.AddPlayers(this, null);
			//System.Console.WriteLine(this.ToString());
			if (MPManager.IsServer())// && Program.Server.IsRemoteServer())
			{
				MSGSwitchStatus msg2 = new MSGSwitchStatus();
				MPManager.BroadCast(msg2.ToString());

				MSGPlayer host = new MSGPlayer(MPManager.GetUserName(), "1234", Program.Simulator.conFileName, Program.Simulator.patFileName, Program.Simulator.PlayerLocomotive.Train,
					Program.Simulator.PlayerLocomotive.Train.Number);
				MPManager.BroadCast(host.ToString() + MPManager.OnlineTrains.AddAllPlayerTrain());

				foreach (Train t in Program.Simulator.Trains)
				{
					if (Program.Simulator.PlayerLocomotive != null && t == Program.Simulator.PlayerLocomotive.Train) continue; //avoid broadcast player train
					if (MPManager.OnlineTrains.findTrain(t)) continue;
					MPManager.BroadCast((new MSGTrain(t, t.Number)).ToString());
				}
				//System.Console.WriteLine(host.ToString() + Program.Simulator.OnlineTrains.AddAllPlayerTrain());

			}
			else //client needs to handle environment
			{
				if (MPManager.GetUserName() == this.user) //a reply from the server, update my train number
				{
					if (Program.Simulator.PlayerLocomotive == null) Program.Simulator.Trains[0].Number = this.num;
					else Program.Simulator.PlayerLocomotive.Train.Number = this.num;
				}
				Program.Simulator.Weather = (WeatherType)this.weather;
				Program.Simulator.ClockTime = this.seconds;
				Program.Simulator.Season = (SeasonType)this.season;
			}

		}

		public void HandleMsg(OnlinePlayer p)
		{
			if (!MPManager.IsServer()) return; //only intended for the server, when it gets the player message in OnlinePlayer Receive
			//check if other players with the same name is online
				//if someone with the same name is there, will throw a fatal error
			if (MPManager.OnlineTrains.findTrain(user) != null || MPManager.GetUserName() == user)
			{
				MPManager.BroadCast((new MSGMessage(user, "Error", "A user with the same name exists")).ToString());
				throw new MultiPlayerError();
			}

			MPManager.OnlineTrains.AddPlayers(this, p);
			//System.Console.WriteLine(this.ToString());
			MSGSwitchStatus msg2 = new MSGSwitchStatus();
			MPManager.BroadCast(msg2.ToString());

			MSGPlayer host = new MSGPlayer(MPManager.GetUserName(), "1234", Program.Simulator.conFileName, Program.Simulator.patFileName, Program.Simulator.PlayerLocomotive.Train,
				Program.Simulator.PlayerLocomotive.Train.Number);

			MPManager.BroadCast(host.ToString() + MPManager.OnlineTrains.AddAllPlayerTrain());

			foreach (Train t in Program.Simulator.Trains)
			{
				if (Program.Simulator.PlayerLocomotive != null && t == Program.Simulator.PlayerLocomotive.Train) continue; //avoid broadcast player train
				if (MPManager.OnlineTrains.findTrain(t)) continue;
				MPManager.BroadCast((new MSGTrain(t, t.Number)).ToString());
			}

			//System.Console.WriteLine(host.ToString() + Program.Simulator.OnlineTrains.AddAllPlayerTrain());

		}
	}

	#endregion MSGPlayer

	#region MGSwitch

	public class MSGSwitch : Message
	{
		public string user;
		public int TileX, TileZ, WorldID, Selection;

		public MSGSwitch(string m)
		{
			string[] tmp = m.Split(' ');
			if (tmp.Length != 5) throw new Exception("Parsing error " + m);
			user = tmp[0];
			TileX = int.Parse(tmp[1]);
			TileZ = int.Parse(tmp[2]);
			WorldID = int.Parse(tmp[3]);
			Selection = int.Parse(tmp[4]);
		}

		public MSGSwitch(string n, int tX, int tZ, int u, int s)
		{
			user = n;
			WorldID = u;
			TileX = tX;
			TileZ = tZ;
			Selection = s;
		}

		public override string ToString()
		{
			string tmp = "SWITCH " + user + " " + TileX + " " + TileZ + " " + WorldID + " " + Selection;
			//System.Console.WriteLine(tmp);
			return "" + tmp.Length + ": " + tmp;
		}

		public override void HandleMsg()
		{
			//System.Console.WriteLine(this.ToString());

			if (user == MPManager.GetUserName()) return;//ignore myself
			TrJunctionNode trj = Program.Simulator.TDB.GetTrJunctionNode(TileX, TileZ, WorldID);
			trj.SelectedRoute = Selection;
			MPManager.BroadCast(this.ToString()); //server will tell others
		}
	}

	#endregion MGSwitch

	#region MSGSignal
	public class MSGSignal : Message
	{
		public SignalObject signal;

	}
	#endregion MSGSignal

	#region MSGSwitchStatus
	public class MSGSwitchStatus : Message
	{
		static SortedList<uint, TrJunctionNode> SwitchState;
		string msgx = "";

		public MSGSwitchStatus()
		{
			if (SwitchState == null)
			{
				SwitchState = new SortedList<uint, TrJunctionNode>();
				uint key = 0;
				foreach (TrackNode t in Program.Simulator.TDB.TrackDB.TrackNodes)
				{
					if (t != null && t.TrJunctionNode != null)
					{
						key = t.Index;
						SwitchState.Add(key, t.TrJunctionNode);
					}
				}
			}
			msgx = "";
			foreach (System.Collections.Generic.KeyValuePair<uint, TrJunctionNode> t in SwitchState)
			{
				if (t.Value.SelectedRoute > 9 && t.Value.SelectedRoute < 0)
				{
					throw new Exception("Selected route is " + t.Value.SelectedRoute + ". Please inform OR for the problem");
				}
				msgx += t.Value.SelectedRoute;
			}
		}

		public MSGSwitchStatus(string m)
		{
			if (SwitchState == null)
			{
				uint key = 0;
				SwitchState = new SortedList<uint, TrJunctionNode>();
				foreach (TrackNode t in Program.Simulator.TDB.TrackDB.TrackNodes)
				{
					if (t != null && t.TrJunctionNode != null)
					{
						key = t.Index;
						SwitchState.Add(key, t.TrJunctionNode);
					}
				}

			}
			msgx = m;
		}

		public override void HandleMsg() //only client will get message, thus will set states
		{
			if (MPManager.IsServer() ) return; //server will ignore it

			int i = 0;
			foreach (System.Collections.Generic.KeyValuePair<uint, TrJunctionNode> t in SwitchState)
			{
				t.Value.SelectedRoute = msgx[i] - 48; //ASCII code 48 is 0
				i++;
			}
			//System.Console.WriteLine(msg);

		}

		public override string ToString()
		{
			string tmp = "SWITCHSTATES " + msgx;
			return "" + tmp.Length + ": " + tmp;
		}
	}
	#endregion MSGSwitchStatus

	#region MSGTrain
	//message to add new train from either a string (received message), or a Train (building a message)
	public class MSGTrain : Message
	{
		string[] cars;
		string[] ids;
		int[] flipped; //if a wagon is engine
		int TrainNum;
		int direction;
		int TileX, TileZ;
		float X, Z, Travelled;
		public MSGTrain(string m)
		{
			//System.Console.WriteLine(m);
			int index = m.IndexOf(' '); int last = 0;
			TrainNum = int.Parse(m.Substring(0, index + 1));
			m = m.Remove(0, index + 1);
			index = m.IndexOf(' ');
			direction = int.Parse(m.Substring(0, index + 1));
			m = m.Remove(0, index + 1);
			index = m.IndexOf(' ');
			TileX = int.Parse(m.Substring(0, index + 1));
			m = m.Remove(0, index + 1);
			index = m.IndexOf(' ');
			TileZ = int.Parse(m.Substring(0, index + 1));
			m = m.Remove(0, index + 1);
			index = m.IndexOf(' ');
			X = float.Parse(m.Substring(0, index + 1));
			m = m.Remove(0, index + 1);
			index = m.IndexOf(' ');
			Z = float.Parse(m.Substring(0, index + 1));
			m = m.Remove(0, index + 1);
			index = m.IndexOf(' ');
			Travelled = float.Parse(m.Substring(0, index + 1));
			m = m.Remove(0, index + 1);
			string[] areas = m.Split('\t');
			cars = new string[areas.Length-1];//with an empty "" at end
			ids = new string[areas.Length - 1];
			flipped = new int[areas.Length - 1];
			for (var i = 0; i < cars.Length; i++)
			{
				index = areas[i].IndexOf('\"');
				last = areas[i].LastIndexOf('\"');
				cars[i] = areas[i].Substring(index + 1, last - index - 1);
				string tmp = areas[i].Remove(0, last + 1);
				tmp = tmp.Trim();
				string[] carinfo = tmp.Split('\n');
				ids[i] = carinfo[0];
				flipped[i] = int.Parse(carinfo[1]);
			}

			//System.Console.WriteLine(this.ToString());

		}
		public MSGTrain(Train t, int n)
		{
			cars = new string[t.Cars.Count];
			ids = new string[t.Cars.Count];
			flipped = new int[t.Cars.Count];
			for (var i = 0; i < t.Cars.Count; i++)
			{
				cars[i] = t.Cars[i].WagFilePath;
				ids[i] = t.Cars[i].CarID;
				if (t.Cars[i].Flipped == true) flipped[i] = 1;
				else flipped[i] = 0;
			}
			TrainNum = n;
			direction = t.RearTDBTraveller.Direction==Traveller.TravellerDirection.Forward?1:0;
			TileX = t.RearTDBTraveller.TileX;
			TileZ = t.RearTDBTraveller.TileZ;
			X = t.RearTDBTraveller.X;
			Z = t.RearTDBTraveller.Z;
			Travelled = t.travelled;
		}

		public override void HandleMsg() //only client will get message, thus will set states
		{
			if (MPManager.IsServer()) return; //server will ignore it
			//System.Console.WriteLine(this.ToString());
			// construct train data
			foreach (Train t in Program.Simulator.Trains)
			{
				if (t.Number == this.TrainNum) return; //already add it
			}
			Train train = new Train(Program.Simulator);
			train.TrainType = Train.TRAINTYPE.REMOTE;
			int consistDirection = direction;
			train.travelled = Travelled;
			train.RearTDBTraveller = new Traveller(Program.Simulator.TSectionDat, Program.Simulator.TDB.TrackDB.TrackNodes, TileX, TileZ, X, Z, direction == 1 ? Traveller.TravellerDirection.Forward : Traveller.TravellerDirection.Backward);
			//if (consistDirection != 1)
			//	train.RearTDBTraveller.ReverseDirection();
			TrainCar previousCar = null;
			for(var i = 0; i < cars.Length; i++)// cars.Length-1; i >= 0; i--) {
			{
				string wagonFilePath = Program.Simulator.BasePath + @"\trains\trainset\" + cars[i];
				try
				{
					TrainCar car = RollingStock.Load(Program.Simulator, wagonFilePath, previousCar);
					bool flip = true;
					if (flipped[i] == 0) flip = false;
					car.Flipped = flip ;
					car.CarID = ids[i];
					train.Cars.Add(car);
					car.Train = train;
					previousCar = car;
				}
				catch (Exception error)
				{
					System.Console.WriteLine( wagonFilePath +" " + error);
				}
			}// for each rail car

			if (train.Cars.Count == 0) return;

			train.CalculatePositionOfCars(0);
			train.InitializeBrakes();
			train.InitializeSignals(false);

			train.Number = this.TrainNum;
			if (train.Cars[0] is MSTSLocomotive) train.LeadLocomotive = train.Cars[0];
			lock (Program.Simulator.Trains) { Program.Simulator.Trains.Add(train); }


		}

		public override string ToString()
		{
			string tmp = "TRAIN " + TrainNum + " " + direction + " " + TileX + " " + TileZ + " " + X + " " + Z + " " + Travelled + " ";
			for(var i = 0; i < cars.Length; i++) 
			{
				var c = cars[i];
				var index = c.LastIndexOf("\\trains\\trainset\\", StringComparison.OrdinalIgnoreCase);
				if (index > 0)
				{
					c = c.Remove(0, index + 17);
				}//c: wagon path without folder name

				tmp += "\"" + c + "\"" + " " + ids[i] + "\n" + flipped[i] + "\t";
			}
			return "" + tmp.Length + ": " + tmp;
		}
	}

	#endregion MSGTrain

	#region MSGUpdateTrain

	//message to add new train from either a string (received message), or a Train (building a message)
	public class MSGUpdateTrain : Message
	{
		string[] cars;
		string[] ids;
		int[] flipped; //if a wagon is engine
		int TrainNum;
		int direction;
		int TileX, TileZ;
		float X, Z, Travelled;
		string user;
		public MSGUpdateTrain(string m)
		{
			//System.Console.WriteLine(m);
			int index = m.IndexOf(' '); int last = 0;
			user = m.Substring(0, index + 1);
			m = m.Remove(0, index + 1);
			user = user.Trim();

			index = m.IndexOf(' ');
			TrainNum = int.Parse(m.Substring(0, index + 1));
			m = m.Remove(0, index + 1);
			index = m.IndexOf(' ');
			direction = int.Parse(m.Substring(0, index + 1));
			m = m.Remove(0, index + 1);
			index = m.IndexOf(' ');
			TileX = int.Parse(m.Substring(0, index + 1));
			m = m.Remove(0, index + 1);
			index = m.IndexOf(' ');
			TileZ = int.Parse(m.Substring(0, index + 1));
			m = m.Remove(0, index + 1);
			index = m.IndexOf(' ');
			X = float.Parse(m.Substring(0, index + 1));
			m = m.Remove(0, index + 1);
			index = m.IndexOf(' ');
			Z = float.Parse(m.Substring(0, index + 1));
			m = m.Remove(0, index + 1);
			index = m.IndexOf(' ');
			Travelled = float.Parse(m.Substring(0, index + 1));
			m = m.Remove(0, index + 1);
			string[] areas = m.Split('\t');
			cars = new string[areas.Length - 1];//with an empty "" at end
			ids = new string[areas.Length - 1];
			flipped = new int[areas.Length - 1];
			for (var i = 0; i < cars.Length; i++)
			{
				index = areas[i].IndexOf('\"');
				last = areas[i].LastIndexOf('\"');
				cars[i] = areas[i].Substring(index + 1, last - index - 1);
				string tmp = areas[i].Remove(0, last + 1);
				tmp = tmp.Trim();
				string[] carinfo = tmp.Split('\n');
				ids[i] = carinfo[0];
				flipped[i] = int.Parse(carinfo[1]);
			}

			//System.Console.WriteLine(this.ToString());

		}
		public MSGUpdateTrain(string u, Train t, int n)
		{
			user = u;
			cars = new string[t.Cars.Count];
			ids = new string[t.Cars.Count];
			flipped = new int[t.Cars.Count];
			for (var i = 0; i < t.Cars.Count; i++)
			{
				cars[i] = t.Cars[i].WagFilePath;
				ids[i] = t.Cars[i].CarID;
				if (t.Cars[i].Flipped == true) flipped[i] = 1;
				else flipped[i] = 0;
			}
			TrainNum = n;
			direction = t.RearTDBTraveller.Direction == Traveller.TravellerDirection.Forward ? 1 : 0;
			TileX = t.RearTDBTraveller.TileX;
			TileZ = t.RearTDBTraveller.TileZ;
			X = t.RearTDBTraveller.X;
			Z = t.RearTDBTraveller.Z;
			Travelled = t.travelled;
		}

		TrainCar findCar(Train t, string name)
		{
			foreach (TrainCar car in t.Cars)
			{
				if (car.CarID == name) return car;
			}
			return null;
		}
		public override void HandleMsg() //only client will get message, thus will set states
		{
			if (MPManager.IsServer()) return; //server will ignore it
			if (user != MPManager.GetUserName()) return; //not the one requested GetTrain
			// construct train data
			foreach (Train train in Program.Simulator.Trains)
			{
				if (train.Number == this.TrainNum) //the train exists, update information
				{
					Traveller traveller = new Traveller(Program.Simulator.TSectionDat, Program.Simulator.TDB.TrackDB.TrackNodes, TileX, TileZ, X, Z, direction == 1 ? Traveller.TravellerDirection.Forward : Traveller.TravellerDirection.Backward);
					TrainCar previousCar = null;
					List<TrainCar> tmpCars = new List<TrainCar>();
					for (var i = 0; i < cars.Length; i++)// cars.Length-1; i >= 0; i--) {
					{
						string wagonFilePath = Program.Simulator.BasePath + @"\trains\trainset\" + cars[i];
						try
						{
							TrainCar car = findCar(train, ids[i]);
							if (car == null) car = RollingStock.Load(Program.Simulator, wagonFilePath, previousCar);
							//car.PreviousCar = previousCar;
							bool flip = true;
							if (flipped[i] == 0) flip = false;
							car.Flipped = flip;
							car.CarID = ids[i];
							tmpCars.Add(car);
							car.Train = train;
							previousCar = car;
						}
						catch (Exception error)
						{
							System.Console.WriteLine(wagonFilePath + " " + error);
						}
					}// for each rail car

					if (tmpCars.Count == 0) return;

					train.Cars = tmpCars;
					train.RearTDBTraveller = traveller;
					train.CalculatePositionOfCars(0);
					train.travelled = Travelled;
					return;
				}
			}

			//not found, create new train
			Train train1 = new Train(Program.Simulator);
			train1.TrainType = Train.TRAINTYPE.REMOTE;
			int consistDirection = direction;
			train1.travelled = Travelled;
			train1.RearTDBTraveller = new Traveller(Program.Simulator.TSectionDat, Program.Simulator.TDB.TrackDB.TrackNodes, TileX, TileZ, X, Z, direction == 1 ? Traveller.TravellerDirection.Forward : Traveller.TravellerDirection.Backward);
			TrainCar previousCar1 = null;
			for (var i = 0; i < cars.Length; i++)// cars.Length-1; i >= 0; i--) {
			{
				string wagonFilePath = Program.Simulator.BasePath + @"\trains\trainset\" + cars[i];
				try
				{
					TrainCar car = RollingStock.Load(Program.Simulator, wagonFilePath, previousCar1);
					bool flip = true;
					if (flipped[i] == 0) flip = false;
					car.Flipped = flip;
					car.CarID = ids[i];
					train1.Cars.Add(car);
					car.Train = train1;
					previousCar1 = car;
				}
				catch (Exception error)
				{
					System.Console.WriteLine(wagonFilePath + " " + error);
				}
			}// for each rail car

			if (train1.Cars.Count == 0) return;

			train1.CalculatePositionOfCars(0);
			train1.InitializeBrakes();
			train1.InitializeSignals(false);

			train1.Number = this.TrainNum;
			if (train1.Cars[0] is MSTSLocomotive) train1.LeadLocomotive = train1.Cars[0];
			lock (Program.Simulator.Trains) { Program.Simulator.Trains.Add(train1); }
		}

		public override string ToString()
		{
			string tmp = "UPDATETRAIN " + user + " " + TrainNum + " " + direction + " " + TileX + " " + TileZ + " " + X + " " + Z + " " + Travelled + " ";
			for (var i = 0; i < cars.Length; i++)
			{
				var c = cars[i];
				var index = c.LastIndexOf("\\trains\\trainset\\", StringComparison.OrdinalIgnoreCase);
				if (index > 0)
				{
					c = c.Remove(0, index + 17);
				}//c: wagon path without folder name

				tmp += "\"" + c + "\"" + " " + ids[i] + "\n" + flipped[i] + "\t";
			}
			return "" + tmp.Length + ": " + tmp;
		}
	}

	#endregion MSGUpdateTrain

	#region MSGRemoveTrain
	//remove AI trains
	public class MSGRemoveTrain : Message
	{
		public List<int> trains;

		public MSGRemoveTrain(string m)
		{
			string[] tmp = m.Split(' ');
			trains = new List<int>();
			for (var i = 0; i < tmp.Length; i++)
			{
				trains.Add(int.Parse(tmp[i]));
			}
		}

		public MSGRemoveTrain(List<Train> ts)
		{
			trains = new List<int>();
			foreach (Train t in ts)
			{
				trains.Add(t.Number);
			}
		}

		public override string ToString()
		{

			string tmp = "REMOVETRAIN";
			foreach (int i in trains)
			{
				tmp += " " + i;
			}
			return "" + tmp.Length + ": " + tmp;
		}

		public override void HandleMsg()
		{
			foreach (int i in trains)
			{
				foreach (Train train in Program.Simulator.Trains)
				{
					if (i == train.Number)
					{
						MPManager.Instance().AddRemovedTrains(train); //added to the removed list, treated later to be thread safe
					}
				}
			}
		}

	}

	#endregion MSGRemoveTrain

	#region MSGServer
	public class MSGServer : Message
	{
		public MSGServer(string m)
		{
		}


		public override string ToString()
		{
			string tmp = "SERVER YOU";
			return "" + tmp.Length + ": " + tmp;
		}

		public override void HandleMsg()
		{
			if (Program.Server != null) return; //already a server, not need to worry
			Program.Server = new Server(Program.Client.UserName + ' ' + Program.Client.Code, Program.Client);
			//System.Console.WriteLine(this.ToString());
		}
	}
	#endregion MSGServer

	#region MSGAlive
	public class MSGAlive : Message
	{
		string user;
		public MSGAlive(string m)
		{
			user = m;
		}


		public override string ToString()
		{
			string tmp = "ALIVE " + user;
			return "" + tmp.Length + ": " + tmp;
		}

		public override void HandleMsg()
		{
			//nothing to worry at this stage
			//System.Console.WriteLine(this.ToString());
		}
	}
	#endregion MSGAlive

	#region MSGTrainMerge
	//message to add new train from either a string (received message), or a Train (building a message)
	public class MSGTrainMerge : Message
	{
		int TrainNumRetain;
		int TrainNumRemoved;
		int direction;
		int TileX, TileZ;
		float X, Z, Travelled;
		public MSGTrainMerge(string m)
		{
			m = m.Trim();
			string[] areas = m.Split(' ');
			TrainNumRetain = int.Parse(areas[0]);
			TrainNumRemoved = int.Parse(areas[1]);
			direction = int.Parse(areas[2]);
			TileX = int.Parse(areas[3]);
			TileZ = int.Parse(areas[4]);
			X = float.Parse(areas[5]);
			Z = float.Parse(areas[6]);
			Travelled = float.Parse(areas[7]);
		}
		public MSGTrainMerge(Train t1, Train t2)
		{
			TrainNumRetain = t1.Number;
			TrainNumRemoved = t2.Number;
			direction = t1.RearTDBTraveller.Direction == Traveller.TravellerDirection.Forward ? 1 : 0; 
			TileX = t1.RearTDBTraveller.TileX;
			TileZ = t1.RearTDBTraveller.TileZ;
			X = t1.RearTDBTraveller.X;
			Z = t1.RearTDBTraveller.Z;
			Travelled = t1.travelled;

		}

		public override void HandleMsg() 
		{

		}

		public override string ToString()
		{
			string tmp = "TRAINMERGE " + TrainNumRetain + " " + TrainNumRemoved + " " + direction + " " + TileX + " " + TileZ + " " + X + " " + Z + " " + Travelled;
			return "" + tmp.Length + ": " + tmp;
		}
	}
	#endregion MSGTrainMerge

	#region MSGMessage
	//message to add new train from either a string (received message), or a Train (building a message)
	public class MSGMessage : Message
	{
		string msgx;
		string level;
		string user; 
		public MSGMessage(string m)
		{
			m.Trim();
			string[] t = m.Split('\t');
			user = t[0];
			level = t[1];
			msgx = t[2];
		}

		public MSGMessage(string u, string l, string m)
		{
			user = u;
			level = l;

			msgx = m;
		}

		public override void HandleMsg()
		{
			if (MPManager.GetUserName() == user)
			{
				if (Program.Simulator.Confirmer != null)
					Program.Simulator.Confirmer.Message(level, msgx + " will be in single mode");
				else { System.Console.WriteLine(level + ": " + msgx + ", will be in single mode"); }
				if (level == "Error" && !MPManager.IsServer())//if is a client, fatal error, will close the connection, and get into single mode
				{
					MPManager.Notify((new MSGQuit(MPManager.GetUserName())).ToString());//to be nice, still send a quit before close the connection
					throw new MultiPlayerError();//this is a fatal error, thus the client will be stopped in ClientComm
				}
			}
		}

		public override string ToString()
		{
			string tmp = "MESSAGE " + user + "\t" + level + "\t" + msgx;
			return "" + tmp.Length + ": " + tmp;
		}
	}

	#endregion MSGMessage

	#region MSGEvent
	public class MSGEvent : Message
	{
		public string user;
		public string EventName;
		public int EventState;

		public MSGEvent(string m)
		{
			string[] tmp = m.Split(' '); 
			if (tmp.Length != 3) throw new Exception("Parsing error " + m);
			user = tmp[0].Trim();
			EventName = tmp[1].Trim();
			EventState = int.Parse(tmp[2]);
		}

		public MSGEvent(string m, string e, int ID)
		{
			user = m.Trim();
			EventName = e;
			EventState = ID;
		}

		public override string ToString()
		{

			string tmp = "EVENT " + user + " " + EventName + " " + EventState;
			return "" + tmp.Length + ": " + tmp;
		}

		public override void HandleMsg()
		{
			if (user == MPManager.GetUserName()) return; //avoid myself
			Train t = MPManager.OnlineTrains.findTrain(user);
			if (t == null) return;

			if (EventName == "HORN")
			{
				t.SignalEvent(EventState);
				MPManager.BroadCast(this.ToString()); //if the server, will broadcast
			}
			else if (EventName == "PANTO2")
			{
				MSTSWagon w = (MSTSWagon)t.Cars[0];
				if (w == null) return;

				w.FrontPanUp = (EventState == 1 ? true : false);

				foreach (TrainCar car in t.Cars)
					if (car is MSTSWagon) ((MSTSWagon)car).FrontPanUp = w.FrontPanUp;
				MPManager.BroadCast(this.ToString()); //if the server, will broadcast
			}
			else if (EventName == "PANTO1")
			{
				MSTSWagon w = (MSTSWagon)t.Cars[0];
				if (w == null) return;

				w.AftPanUp = (EventState == 1 ? true : false);

				foreach (TrainCar car in t.Cars)
					if (car is MSTSWagon) ((MSTSWagon)car).AftPanUp = w.AftPanUp;
				MPManager.BroadCast(this.ToString()); //if the server, will broadcast
			}
			else if (EventName == "BELL")
			{
				if (t.LeadLocomotive != null) t.LeadLocomotive.SignalEvent(EventState == 0 ? EventID.BellOff : EventID.BellOn);
				MPManager.BroadCast(this.ToString()); //if the server, will broadcast
			}
			else if (EventName == "WIPER")
			{
				if (t.LeadLocomotive != null) t.LeadLocomotive.SignalEvent(EventState == 0 ? EventID.WiperOff : EventID.WiperOn);
				MPManager.BroadCast(this.ToString()); //if the server, will broadcast
			}
			else if (EventName == "HEADLIGHT")
			{
				if (t.LeadLocomotive != null && EventState == 0) t.LeadLocomotive.SignalEvent(EventID.HeadlightOff);
				if (t.LeadLocomotive != null && EventState == 1) t.LeadLocomotive.SignalEvent(EventID.HeadlightDim);
				if (t.LeadLocomotive != null && EventState == 2) t.LeadLocomotive.SignalEvent(EventID.HeadlightOn);
				MPManager.BroadCast(this.ToString()); //if the server, will broadcast
			}
			else return;
		}

	}

	#endregion MSGEvent

	#region MSGQuit
	public class MSGQuit : Message
	{
		public string user;

		public MSGQuit(string m)
		{
			user = m.Trim();
		}

		public override string ToString()
		{

			string tmp = "QUIT " + user;
			return "" + tmp.Length + ": " + tmp;
		}

		public override void HandleMsg()
		{
			if (user == MPManager.GetUserName()) return; //avoid myself

			bool ServerQuit = false;
			if (Program.Client != null && user.Contains("ServerHasToQuit")) //the server quits, will send a message with ServerHasToQuit\tServerName
			{
				Program.Simulator.Confirmer.Message("Error", "Server quits, will play as single mode");
				user = user.Replace("ServerHasToQuit\t", ""); //get the user name of server from the message
				ServerQuit = true;
			}
			OnlinePlayer p = null;
			if (MPManager.OnlineTrains.Players.ContainsKey(user))
			{
				p = MPManager.OnlineTrains.Players[user];
			}
			if (p != null && Program.Simulator.Confirmer != null) Program.Simulator.Confirmer.Message("Info:", this.user + " quit.");
			if (MPManager.IsServer())
			{
				if (p != null)
				{
					lock (Program.Server.Players)
					{
						Program.Server.Players.Remove(p);
					}
					//if the one quit controls my train, I will gain back the control
					if (p.Train == Program.Simulator.PlayerLocomotive.Train) 
						Program.Simulator.PlayerLocomotive.Train.TrainType = Train.TRAINTYPE.PLAYER;
					MPManager.Instance().AddRemovedPlayer(p);
				}
				MPManager.BroadCast(this.ToString()); //if the server, will broadcast
			}
			else //client will remove train
			{
				if (p != null)
				{
					//if the one quit controls my train, I will gain back the control
					if (p.Train == Program.Simulator.PlayerLocomotive.Train)
						Program.Simulator.PlayerLocomotive.Train.TrainType = Train.TRAINTYPE.PLAYER;
					MPManager.Instance().AddRemovedPlayer(p);
					if (ServerQuit)
					{
						//no matter what, let player gain back the control of the player train
						Program.Simulator.PlayerLocomotive.Train.TrainType = Train.TRAINTYPE.PLAYER;
						throw new MultiPlayerError(); //server quit, end communication by throwing this error 
					}
				}
			}
		}

	}

	#endregion MSGQuit

	#region MSGGetTrain
	public class MSGGetTrain : Message
	{
		public int num;
		public string user;

		public MSGGetTrain(string u, int m)
		{
			user = u; num = m;
		}

		public MSGGetTrain(string m)
		{
			string[] tmp = m.Split(' ');
			user = tmp[0]; num = int.Parse(tmp[1]);
		}

		public override string ToString()
		{

			string tmp = "GETTRAIN " + user + " " + num;
			return "" + tmp.Length + ": " + tmp;
		}

		public override void HandleMsg()
		{
			if (MPManager.IsServer())
			{
				foreach (Train t in Program.Simulator.Trains)
				{
					if (t.Number == num) //found it, broadcast to everyone
					{
						MPManager.BroadCast((new MSGUpdateTrain(user, t, t.Number)).ToString());
					}
				}
			}
		}

	}
	#endregion MSGGetTrain

	#region MSGUncouple

	public class MSGUncouple : Message
	{
		public string user, newTrainName, carID, firstCarIDOld, firstCarIDNew;
		public int TileX1, TileZ1;
		public float X1, Z1, Travelled1, Speed1;
		public int trainDirection;
		public int TileX2, TileZ2;
		public float X2, Z2, Travelled2, Speed2;
		public int train2Direction;
		public int newTrainNumber;
		public int oldTrainNumber;
		public int whichIsPlayer;
		string[] ids1;
		string[] ids2;
		int[] flipped1;
		int[] flipped2;

		TrainCar FindCar(List<TrainCar> list, string id)
		{
			foreach (TrainCar car in list) if (car.CarID == id) return car;
			return null;
		}
		public MSGUncouple(string m)
		{
			string[] areas = m.Split('\t');
			user = areas[0].Trim();

			whichIsPlayer = int.Parse(areas[1].Trim());

			firstCarIDOld = areas[2].Trim();

			firstCarIDNew = areas[3].Trim();

			string[] tmp = areas[4].Split(' ');
			TileX1 = int.Parse(tmp[0]); TileZ1 = int.Parse(tmp[1]);
			X1 = float.Parse(tmp[2]); Z1 = float.Parse(tmp[3]); Travelled1 = float.Parse(tmp[4]); Speed1 = float.Parse(tmp[5]); trainDirection = int.Parse(tmp[6]);
			oldTrainNumber = int.Parse(tmp[7]);
			tmp = areas[5].Split('\n');
			ids1 = new string[tmp.Length - 1];
			flipped1 = new int[tmp.Length - 1];
			for (var i = 0; i < ids1.Length; i++)
			{
				string[] field = tmp[i].Split('\r');
				ids1[i] = field[0].Trim();
				flipped1[i] = int.Parse(field[1].Trim());
			}

			tmp = areas[6].Split(' ');
			TileX2 = int.Parse(tmp[0]); TileZ2 = int.Parse(tmp[1]);
			X2 = float.Parse(tmp[2]); Z2 = float.Parse(tmp[3]); Travelled2 = float.Parse(tmp[4]); Speed2 = float.Parse(tmp[5]); train2Direction = int.Parse(tmp[6]);
			newTrainNumber = int.Parse(tmp[7]);

			tmp = areas[7].Split('\n');
			ids2 = new string[tmp.Length - 1];
			flipped2 = new int[tmp.Length - 1];
			for (var i = 0; i < ids2.Length; i++)
			{
				string[] field = tmp[i].Split('\r');
				ids2[i] = field[0].Trim();
				flipped2[i] = int.Parse(field[1].Trim());
			}
		}

		public MSGUncouple(Train t, Train newT, string u, string ID, TrainCar car)
		{
			carID = ID;
			user = u;
			TileX1 = t.RearTDBTraveller.TileX; TileZ1 = t.RearTDBTraveller.TileZ; X1 = t.RearTDBTraveller.X; Z1 = t.RearTDBTraveller.Z; Travelled1 = t.travelled; Speed1 = t.SpeedMpS;
			trainDirection = t.RearTDBTraveller.Direction == Traveller.TravellerDirection.Forward ? 0 : 1;//0 forward, 1 backward
			TileX2 = newT.RearTDBTraveller.TileX; TileZ2 = newT.RearTDBTraveller.TileZ; X2 = newT.RearTDBTraveller.X; Z2 = newT.RearTDBTraveller.Z; Travelled2 = newT.travelled; Speed2 = newT.SpeedMpS;
			train2Direction = newT.RearTDBTraveller.Direction == Traveller.TravellerDirection.Forward ? 0 : 1;//0 forward, 1 backward
			if (MPManager.IsServer()) newTrainNumber = newT.Number;//serer will use the correct number
			else newTrainNumber = 1000000 + Program.Random.Next(1000000);//client: temporary assign a train number 1000000-2000000, will change to the correct one after receiving response from the server

			//housekeeping, one train may contain the player locomotive, thus it should be player controlled
			if (newT.LeadLocomotive == null) newT.LeadNextLocomotive();

			if (t.LeadLocomotive == Program.Simulator.PlayerLocomotive)
			{
				t.TrainType = Train.TRAINTYPE.PLAYER;
			}
			if (newT.LeadLocomotive == Program.Simulator.PlayerLocomotive)
			{
				newT.TrainType = Train.TRAINTYPE.PLAYER;
			}

			//if one of the train holds other player locomotives
			foreach (var pair in MPManager.OnlineTrains.Players)
			{
				string check = pair.Key + " -";
				foreach (var car1 in t.Cars) if (car1.CarID.StartsWith(check)) { t.TrainType = Train.TRAINTYPE.REMOTE; break; }
				foreach (var car1 in newT.Cars) if (car1.CarID.StartsWith(check)) { newT.TrainType = Train.TRAINTYPE.REMOTE; break; }
			}
			oldTrainNumber = t.Number;
			newTrainName = "UC" + newTrainNumber; newT.Number = newTrainNumber;

			firstCarIDNew = newT.Cars[0].CarID;
			firstCarIDOld = t.Cars[0].CarID;

			ids1 = new string[t.Cars.Count];
			flipped1 = new int[t.Cars.Count];
			for (var i = 0; i < ids1.Length; i++)
			{
				ids1[i] = t.Cars[i].CarID;
				flipped1[i] = t.Cars[i].Flipped == true ? 1 : 0;
			}

			ids2 = new string[newT.Cars.Count];
			flipped2 = new int[newT.Cars.Count];
			for (var i = 0; i < ids2.Length; i++)
			{
				ids2[i] = newT.Cars[i].CarID;
				flipped2[i] = newT.Cars[i].Flipped == true ? 1 : 0;
			}

			//to see which train contains the car (PlayerLocomotive)
			if (t.Cars.Contains(car)) whichIsPlayer = 0;
			else if (newT.Cars.Contains(car)) whichIsPlayer = 1;
			else whichIsPlayer = 2;
		}

		string FillInString(int i)
		{
			string tmp = "";
			if (i == 1)
			{
				for (var j = 0; j < ids1.Length; j++)
				{
					tmp += ids1[j] + "\r" + flipped1[j] + "\n";
				}
			}
			else
			{
				for (var j = 0; j < ids2.Length; j++)
				{
					tmp += ids2[j] + "\r" + flipped2[j] + "\n";
				}
			}
			return tmp;
		}
		public override string ToString()
		{
			string tmp = "UNCOUPLE " + user + "\t" + whichIsPlayer + "\t" + firstCarIDOld + "\t" + firstCarIDNew
				+ "\t" + TileX1 + " " + TileZ1 + " " + X1 + " " + Z1 + " " + Travelled1 + " " + Speed1 + " " + trainDirection + " " + oldTrainNumber + "\t"
				+ FillInString(1)
				+ "\t" + TileX2 + " " + TileZ2 + " " + X2 + " " + Z2 + " " + Travelled2 + " " + Speed2 + " " + train2Direction + " " + newTrainNumber + "\t"
				+ FillInString(2);
			return "" + tmp.Length + ": " + tmp;
		}

		public override void HandleMsg()
		{
			if (user == MPManager.GetUserName()) //received from the server, but it is about mine action of uncouple
			{
				foreach (Train t in Program.Simulator.Trains)
				{
					foreach (TrainCar car in t.Cars)
					{
						if (car.CarID == firstCarIDOld)//got response about this train
						{
							t.Number = oldTrainNumber;
						}
						if (car.CarID == firstCarIDNew)//got response about this train
						{
							t.Number = newTrainNumber;
						}
					}
				}

			}
			else
			{
				TrainCar lead = null;
				Train train = null;
				List<TrainCar> trainCars = null;
				foreach (Train t in Program.Simulator.Trains)
				{
					var found = false;
					foreach (TrainCar car in t.Cars)
					{
						if (car.CarID == firstCarIDOld)//got response about this train
						{
							found = true;
							break;
						}
					}
					if (found == true)
					{
						train = t;
						lead = train.LeadLocomotive;
						trainCars = t.Cars;
						List<TrainCar> tmpcars = new List<TrainCar>();
						for (var i = 0; i < ids1.Length; i++)
						{
							TrainCar car = FindCar(trainCars, ids1[i]);
							if (car == null) continue;
							car.Flipped = flipped1[i] == 0 ? false : true;
							tmpcars.Add(car); 
						}
						if (tmpcars.Count == 0) return;
						t.Cars = tmpcars;
						Traveller.TravellerDirection d1 = Traveller.TravellerDirection.Forward;
						if (trainDirection == 1) d1 = Traveller.TravellerDirection.Backward;
						t.RearTDBTraveller = new Traveller(Program.Simulator.TSectionDat, Program.Simulator.TDB.TrackDB.TrackNodes, TileX1, TileZ1, X1, Z1, d1);
						t.CalculatePositionOfCars(0);  // fix the front traveller
						t.travelled = Travelled1;
						t.SpeedMpS = Speed1;
						t.LeadLocomotive = lead;
						if (train.LeadLocomotive == Program.Simulator.PlayerLocomotive) train.TrainType = Train.TRAINTYPE.PLAYER;
						break;
					}
				}

				if (train == null || trainCars == null) return;

				Train train2 = new Train(Program.Simulator);
				train2.TrainType = Train.TRAINTYPE.REMOTE;
				List<TrainCar> tmpcars2 = new List<TrainCar>();
				for (var i = 0; i < ids2.Length; i++)
				{
					TrainCar car = FindCar(trainCars, ids2[i]);
					if (car == null) continue;
					tmpcars2.Add(car);
					car.Flipped = flipped2[i] == 0 ? false : true;
				}
				if (tmpcars2.Count == 0) return;
				train2.Cars = tmpcars2;
				train2.LeadLocomotive = null;
				train2.LeadNextLocomotive();
				if (train2.LeadLocomotive == Program.Simulator.PlayerLocomotive) train2.TrainType = Train.TRAINTYPE.PLAYER;

				Traveller.TravellerDirection d2 = Traveller.TravellerDirection.Forward;
				if (train2Direction == 1) d2 = Traveller.TravellerDirection.Backward;

				// and fix up the travellers
				train2.RearTDBTraveller = new Traveller(Program.Simulator.TSectionDat, Program.Simulator.TDB.TrackDB.TrackNodes, TileX2, TileZ2, X2, Z2, d2);
				train2.travelled = Travelled2;
				train2.SpeedMpS = Speed2;

				train2.CalculatePositionOfCars(0);  // fix the front traveller

				foreach (TrainCar car in train2.Cars) car.Train = train2;

				train2.InitializeSignals(false);
				lock (Program.Simulator.Trains) { Program.Simulator.Trains.Add(train2); }
				train.UncoupledFrom = train2;
				train2.UncoupledFrom = train;

				if (whichIsPlayer == 0 && MPManager.OnlineTrains.findTrain(user) != null) MPManager.OnlineTrains.Players[user].Train = train;
				else if (whichIsPlayer == 1 && MPManager.OnlineTrains.findTrain(user) != null) MPManager.OnlineTrains.Players[user].Train = train2; //the player may need to update the train it drives

				if (MPManager.IsServer())
				{
					this.newTrainNumber = train2.Number;//we got a new train number, will tell others.
					this.oldTrainNumber = train.Number;
					MPManager.BroadCast(this.ToString());//if server receives this, will tell others, including whoever sent the information
				}
				else
				{
					train2.Number = this.newTrainNumber; //client receives a message, will use the train number specified by the server
					train.Number = this.oldTrainNumber;
				}
			}
		}
	}
	#endregion MSGUncouple
	
	#region MSGCouple
	public class MSGCouple : Message
	{
		string[] cars;
		string[] ids;
		int[] flipped; //if a wagon is engine
		int TrainNum;
		int RemovedTrainNum;
		int direction;
		int TileX, TileZ, Lead;
		float X, Z, Travelled;
		string whoControls;

		public MSGCouple(string m)
		{
			//System.Console.WriteLine(m);
			int index = m.IndexOf(' '); int last = 0;
			TrainNum = int.Parse(m.Substring(0, index + 1));
			m = m.Remove(0, index + 1);
			index = m.IndexOf(' ');
			RemovedTrainNum = int.Parse(m.Substring(0, index + 1));
			m = m.Remove(0, index + 1);
			index = m.IndexOf(' ');
			direction = int.Parse(m.Substring(0, index + 1));
			m = m.Remove(0, index + 1);
			index = m.IndexOf(' ');
			TileX = int.Parse(m.Substring(0, index + 1));
			m = m.Remove(0, index + 1);
			index = m.IndexOf(' ');
			TileZ = int.Parse(m.Substring(0, index + 1));
			m = m.Remove(0, index + 1);
			index = m.IndexOf(' ');
			X = float.Parse(m.Substring(0, index + 1));
			m = m.Remove(0, index + 1);
			index = m.IndexOf(' ');
			Z = float.Parse(m.Substring(0, index + 1));
			m = m.Remove(0, index + 1);
			index = m.IndexOf(' ');
			Travelled = float.Parse(m.Substring(0, index + 1));
			m = m.Remove(0, index + 1);
			index = m.IndexOf(' ');
			Lead = int.Parse(m.Substring(0, index + 1));
			m = m.Remove(0, index + 1);
			index = m.IndexOf(' ');
			whoControls = m.Substring(0, index + 1).Trim();
			m = m.Remove(0, index + 1);
			string[] areas = m.Split('\t');
			cars = new string[areas.Length - 1];//with an empty "" at end
			ids = new string[areas.Length - 1];
			flipped = new int[areas.Length - 1];
			for (var i = 0; i < cars.Length; i++)
			{
				index = areas[i].IndexOf('\"');
				last = areas[i].LastIndexOf('\"');
				cars[i] = areas[i].Substring(index + 1, last - index - 1);
				string tmp = areas[i].Remove(0, last + 1);
				tmp = tmp.Trim();
				string[] carinfo = tmp.Split('\n');
				ids[i] = carinfo[0];
				flipped[i] = int.Parse(carinfo[1]);
			}

			//System.Console.WriteLine(this.ToString());

		}

		public MSGCouple(Train t, Train oldT)
		{
			cars = new string[t.Cars.Count];
			ids = new string[t.Cars.Count];
			flipped = new int[t.Cars.Count];
			for (var i = 0; i < t.Cars.Count; i++)
			{
				cars[i] = t.Cars[i].WagFilePath;
				ids[i] = t.Cars[i].CarID;
				if (t.Cars[i].Flipped == true) flipped[i] = 1;
				else flipped[i] = 0;
			}
			TrainNum = t.Number;
			RemovedTrainNum = oldT.Number;
			direction = t.RearTDBTraveller.Direction == Traveller.TravellerDirection.Forward ? 0 : 1;
			TileX = t.RearTDBTraveller.TileX;
			TileZ = t.RearTDBTraveller.TileZ;
			X = t.RearTDBTraveller.X;
			Z = t.RearTDBTraveller.Z;
			Travelled = t.travelled;
			MPManager.Instance().RemoveUncoupledTrains(t); //remove the trains from uncoupled train lists
			MPManager.Instance().RemoveUncoupledTrains(oldT);
			var j = 0;
			Lead = -1;
			foreach(TrainCar car in t.Cars) {
				if (car == t.LeadLocomotive) {Lead = j; break;}
				j++;
			}
			whoControls = "NA";
			var index = t.LeadLocomotive.CarID.IndexOf(" - ");
			if (index > 0)
			{
				whoControls = t.LeadLocomotive.CarID.Substring(0, index);
			}
		}

		public override string ToString()
		{
			string tmp = "COUPLE " + TrainNum + " " + RemovedTrainNum + " " + direction + " " + TileX + " " + TileZ + " " + X + " " + Z + " " + Travelled + " " +Lead + " " + whoControls + " ";
			for (var i = 0; i < cars.Length; i++)
			{
				var c = cars[i];
				var index = c.LastIndexOf("\\trains\\trainset\\", StringComparison.OrdinalIgnoreCase);
				if (index > 0)
				{
					c = c.Remove(0, index + 17);
				}//c: wagon path without folder name

				tmp += "\"" + c + "\"" + " " + ids[i] + "\n" + flipped[i] + "\t";
			}
			return "" + tmp.Length + ": " + tmp;
		}

		private TrainCar FindCar(Train t1, Train t2, string carID)
		{
			foreach (TrainCar c in t1.Cars) if (c.CarID == carID) return c;
			foreach (TrainCar c in t2.Cars) if (c.CarID == carID) return c;
			return null;
		}
		public override void HandleMsg()
		{
			if (MPManager.IsServer()) return;//server will not receive this from client
			string PlayerTrainID;
			if (Program.Simulator.PlayerLocomotive != null) PlayerTrainID = Program.Simulator.PlayerLocomotive.CarID;
			else PlayerTrainID = "NULL";
			Train train = null, train2 = null;

			foreach (Train t in Program.Simulator.Trains)
			{
				if (t.Number == this.TrainNum) train = t;
				if (t.Number == this.RemovedTrainNum) train2 = t;
			}

			TrainCar lead = train.LeadLocomotive;
			if (lead == null) lead = train2.LeadLocomotive;

			if (Program.Simulator.PlayerLocomotive != null && Program.Simulator.PlayerLocomotive.Train == train2)
			{
				Train tmp = train2; train2 = train; train = tmp; Program.Simulator.PlayerLocomotive.Train = train;
			}

			if (train == null || train2 == null) return; //did not find the trains to op on

			//if (consistDirection != 1)
			//	train.RearTDBTraveller.ReverseDirection();
			TrainCar previousCar = null;
			List<TrainCar> tmpCars = new List<TrainCar>();
			for (var i = 0; i < cars.Length; i++)// cars.Length-1; i >= 0; i--) {
			{
				TrainCar car = FindCar(train, train2, ids[i]);
				if (car == null) continue;
				//car.PreviousCar = previousCar;
				bool flip = true;
				if (flipped[i] == 0) flip = false;
				car.Flipped = flip;
				car.CarID = ids[i];
				tmpCars.Add(car);
				car.Train = train;
				previousCar = car;

			}// for each rail car
			if (tmpCars.Count == 0) return;
			//List<TrainCar> oldList = train.Cars;
			train.Cars = tmpCars;
			
			train.travelled = Travelled;
			train.RearTDBTraveller = new Traveller(Program.Simulator.TSectionDat, Program.Simulator.TDB.TrackDB.TrackNodes, TileX, TileZ, X, Z, direction == 0 ? Traveller.TravellerDirection.Forward : Traveller.TravellerDirection.Backward);
			
			train.CalculatePositionOfCars(0);
			train.LeadLocomotive = null; train2.LeadLocomotive = null;
			if (Lead != -1 && Lead < train.Cars.Count ) train.LeadLocomotive = train.Cars[Lead];

			if (train.LeadLocomotive == null) train.LeadNextLocomotive();

			MPManager.Instance().AddRemovedTrains(train2);

			//mine is not the leading locomotive, thus I give up the control
			if (train.LeadLocomotive != Program.Simulator.PlayerLocomotive)
			{
				train.TrainType = Train.TRAINTYPE.REMOTE; //make the train remote controlled
			}

			//update the remote user's train
			if (MPManager.OnlineTrains.findTrain(whoControls) != null) MPManager.OnlineTrains.Players[whoControls].Train = train;
		}
	}
	#endregion MSGCouple

	#region MSGSignalStatus
	public class MSGSignalStatus : Message
	{

		//local data here
		//signalObj.Signal.GetAspect(), signalObj.Signal.nextSigRef
		//signalObjects[nextSigRef].SetSignalState(state)
		string msgx = "";
		static SortedList<int, SignalHead> signals;
		//constructor to create a message from signal data
		public MSGSignalStatus()
		{
			if (signals == null)
			{
				signals = new SortedList<int, SignalHead>();
				if (Program.Simulator.Signals.SignalObjects != null)
				{
					foreach (var s in Program.Simulator.Signals.SignalObjects)
					{
						if (s != null && s.isSignal && s.SignalHeads != null)
							foreach (var h in s.SignalHeads)
							{
								//System.Console.WriteLine(h.TDBIndex);
								signals.Add(h.TDBIndex * 1000 + h.trItemIndex, h);
							}
					}
				}
			}

			msgx = "";
			foreach (var t in signals)
			{
				msgx += "" + (int)t.Value.state + "" + t.Value.draw_state;
			}
		}

		//constructor to decode the message "m"
		public MSGSignalStatus(string m)
		{
			if (signals == null)
			{
				signals = new SortedList<int, SignalHead>();
				if (Program.Simulator.Signals.SignalObjects != null)
				{
					foreach (var s in Program.Simulator.Signals.SignalObjects)
					{
						if (s != null && s.isSignal && s.SignalHeads != null)
							foreach (var h in s.SignalHeads)
							{
								//System.Console.WriteLine(h.TDBIndex);
								signals.Add(h.TDBIndex * 1000 + h.trItemIndex, h);
							}
					}
				}
			}
			msgx = m;
		}

		//how to handle the message?
		public override void HandleMsg() //only client will get message, thus will set states
		{
			if (Program.Server != null) return; //server will ignore it

			if (signals.Count != msgx.Length / 2) { System.Console.WriteLine("Error in synchronizing signals"); return; }
			int i = 0;
			foreach (var t in signals)
			{
				t.Value.state =(SignalHead.SIGASP) (msgx[2*i] - 48); //ASCII code 48 is 0
				t.Value.draw_state = msgx[2 * i + 1] - 48;
				//System.Console.Write(msgx[i]-48);
				i++;
			}
			//System.Console.Write("\n");

		}

		public override string ToString()
		{
			string tmp = "SIGNALSTATES " + msgx; // fill in the message body here
			return "" + tmp.Length + ": " + tmp;
		}
	}
	#endregion MSGSignalStatus

}
