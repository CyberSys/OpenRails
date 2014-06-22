﻿// COPYRIGHT 2009, 2010, 2011, 2012, 2013 by the Open Rails project.
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

// This file is the responsibility of the 3D & Environment Team. 

using ORTS.Common;
using System;
using System.Diagnostics;
using System.Threading;

namespace ORTS.Processes
{
    public class LoaderProcess
    {
        public readonly Profiler Profiler = new Profiler("Loader");
        readonly ProcessState State = new ProcessState("Loader");
        readonly Game Game;
        readonly Thread Thread;

        public LoaderProcess(Game game)
        {
            Game = game;
            Thread = new Thread(LoaderThread);
        }

        public void Start()
        {
            Thread.Start();
        }

        public void Stop()
        {
            Thread.Abort();
        }

        public bool Finished
        {
            get
            {
                return State.Finished;
            }
        }

        public void WaitTillFinished()
        {
            State.WaitTillFinished();
        }

        [ThreadName("Loader")]
        void LoaderThread()
        {
            Profiler.SetThread();

            while (Thread.CurrentThread.ThreadState == System.Threading.ThreadState.Running)
            {
                // Wait for a new Update() command
                State.WaitTillStarted();
                try
                {
                    if (!DoLoad())
                        return;
                }
                finally
                {
                    // Signal finished so RenderProcess can start drawing
                    State.SignalFinish();
                }
            }
        }

        [CallOnThread("Updater")]
        internal void StartLoad()
        {
            Debug.Assert(State.Finished);
            State.SignalStart();
        }

        [ThreadName("Loader")]
        bool DoLoad()
        {
            if (Debugger.IsAttached)
            {
                Load();
            }
            else
            {
                try
                {
                    Load();
                }
                catch (Exception error)
                {
                    if (!(error is ThreadAbortException))
                    {
                        // Unblock anyone waiting for us, report error and die.
                        State.SignalFinish();
                        Game.ProcessReportError(error);
                        return false;
                    }
                }
            }
            return true;
        }

        [CallOnThread("Loader")]
        public void Load()
        {
            Profiler.Start();
            try
            {
                Game.State.Load();
            }
            finally
            {
                Profiler.Stop();
            }
        }
    }
}
