﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Diagnostics;

namespace ORTS.MultiPlayer
{
	public class ClientComm
	{
		private Thread listenThread;
		private TcpClient client;
		public string UserName;
		public string Code;
		public Decoder decoder;
		public bool Connected = false;

		public void Stop()
		{
			try
			{
				client.Close();
				listenThread.Abort();
			}
			catch (Exception) { }
		}
		public ClientComm(string serverIP, int serverPort, string s)
		{
			client = new TcpClient();

			IPEndPoint serverEndPoint = new IPEndPoint(IPAddress.Parse(serverIP), serverPort);

			client.Connect(serverEndPoint);
			string[] tmp = s.Split(' ');
			UserName = tmp[0];
			Code = tmp[1];
			decoder = new Decoder();

			listenThread = new Thread(new ParameterizedThreadStart(this.Receive));
			listenThread.Start(client);

		}

		public void Receive(object client)
		{

			TcpClient tcpClient = (TcpClient)client;
			NetworkStream clientStream = tcpClient.GetStream();

			byte[] message = new byte[8192];
			int bytesRead;

			while (true)
			{
				bytesRead = 0;
				//System.Threading.Thread.Sleep(Program.Random.Next(50, 200));
				try
				{
					//blocks until a client sends a message
					bytesRead = clientStream.Read(message, 0, 8192);
				}
				catch
				{
					//a socket error has occured
					break;
				}

				if (bytesRead == 0)
				{
					//the client has disconnected from the server
					break;
				}

				//message has successfully been received
				string info = "";
				try
				{
					decoder.PushMsg(Encoding.Unicode.GetString(message, 0, bytesRead));//encoder.GetString(message, 0, bytesRead));
					info = decoder.GetMsg();
					while (info != null)
					{
						//System.Console.WriteLine(info);
						Message msg = Message.Decode(info);
						if (Connected || msg is MSGPlayer) msg.HandleMsg();
						info = decoder.GetMsg();
					}
				}
				catch (MultiPlayerError)
				{
					break;
				}
				catch (Exception e)
				{
					System.Console.WriteLine(e.Message + info);
				}
			}
			if (Program.Simulator.Confirmer != null) Program.Simulator.Confirmer.Message("Error", "Connection to the server is lost, will play as single mode");
			try
			{
				foreach (var p in MPManager.OnlineTrains.Players)
				{
					MPManager.Instance().AddRemovedPlayer(p.Value);
				}
			}
			catch (Exception) { }
			
			//no matter what, let player gain back the control of the player train
			if (Program.Simulator.PlayerLocomotive != null && Program.Simulator.PlayerLocomotive.Train != null)
			{
				Program.Simulator.PlayerLocomotive.Train.TrainType = Train.TRAINTYPE.PLAYER;
				Program.Simulator.PlayerLocomotive.Train.LeadLocomotive = Program.Simulator.PlayerLocomotive;
			}
			if (Program.Simulator.Confirmer != null) Program.Simulator.Confirmer.Message("Info", "Shift-E to gain control of your train");

			Program.Client = null;
			tcpClient.Close();
			listenThread.Abort();
		}

		private object lockObj = new object();
		public void Send(string msg)
		{
			byte[] buffer = Encoding.Unicode.GetBytes(msg);//encoder.GetBytes(msg);

			try
			{
				NetworkStream clientStream = client.GetStream();
				lock (lockObj)//in case two threads want to write at the same buffer
				{
					clientStream.Write(buffer, 0, buffer.Length);
					clientStream.Flush();
				}
			}
			catch
			{
			}
		}

	}
}
