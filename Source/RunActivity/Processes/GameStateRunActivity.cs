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

// Define this to include extra data on loading performance and progress indications.
//#define DEBUG_LOADING

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ORTS.Common;
using ORTS.MultiPlayer;
using ORTS.Viewer3D;

namespace ORTS.Processes
{
    public class GameStateRunActivity : GameState
    {
        static string[] Arguments;
        static Random Random { get { return Program.Random; } set { Program.Random = value; } }  // primary random number generator used throughout the program
        static Simulator Simulator { get { return Program.Simulator; } set { Program.Simulator = value; } }

        //for Multiplayer
        static Server Server { get { return Program.Server; } set { Program.Server = value; } }
        static ClientComm Client { get { return Program.Client; } set { Program.Client = value; } }
        static string UserName { get { return Program.UserName; } set { Program.UserName = value; } }
        static string Code { get { return Program.Code; } set { Program.Code = value; } }

        static Viewer Viewer { get { return Program.Viewer; } set { Program.Viewer = value; } }
        static ORTraceListener ORTraceListener { get { return Program.ORTraceListener; } set { Program.ORTraceListener = value; } }
        static string logFileName { get { return Program.logFileName; } set { Program.logFileName = value; } }

        struct savedValues
        {
            public float initialTileX;
            public float initialTileZ;
            public string[] args;
        }

        static Debugging.DispatchViewer DebugViewer { get { return Program.DebugViewer; } set { Program.DebugViewer = value; } }
        static Debugging.SoundDebugForm SoundDebugForm { get { return Program.SoundDebugForm; } set { Program.SoundDebugForm = value; } }

        LoadingPrimitive Loading;
        LoadingScreenPrimitive LoadingScreen;
        LoadingBarPrimitive LoadingBar;
        Matrix LoadingMatrix = Matrix.Identity;

        public GameStateRunActivity(string[] args)
        {
            Arguments = args;
        }

        internal override void Update(RenderFrame frame, double totalRealSeconds)
        {
            UpdateLoading();

            if (Loading != null)
            {
                frame.AddPrimitive(Loading.Material, Loading, RenderPrimitiveGroup.Overlay, ref LoadingMatrix);
            }

            if (LoadingScreen != null)
            {
                frame.AddPrimitive(LoadingScreen.Material, LoadingScreen, RenderPrimitiveGroup.Overlay, ref LoadingMatrix);
            }

            if (LoadingBar != null)
            {
                LoadingBar.Material.Shader.LoadingPercent = LoadedPercent;
                frame.AddPrimitive(LoadingBar.Material, LoadingBar, RenderPrimitiveGroup.Overlay, ref LoadingMatrix);
            }

            base.Update(frame, totalRealSeconds);
        }

        internal override void Load()
        {
            // Load loading image first!
            if (Loading == null)
                Loading = new LoadingPrimitive(Game);
            if (LoadingBar == null)
                LoadingBar = new LoadingBarPrimitive(Game);

            var args = Arguments;

            // Look for an action to perform.
            var action = "";
            var actions = new[] { "start", "resume", "replay", "replay_from_save", "test" };
            foreach (var possibleAction in actions)
                if (args.Contains("-" + possibleAction) || args.Contains("/" + possibleAction, StringComparer.OrdinalIgnoreCase))
                    action = possibleAction;

            // Collect all non-action options.
            var options = args.Where(a => (a.StartsWith("-") || a.StartsWith("/")) && !actions.Contains(a.Substring(1))).Select(a => a.Substring(1));

            // Collect all non-options as data.
            var data = args.Where(a => !a.StartsWith("-") && !a.StartsWith("/")).ToArray();

            // No action, check for data; for now assume any data is good data.
            if ((action.Length == 0) && (data.Length > 0))
                action = "start";

            var settings = Game.Settings;

            Action doAction = () =>
            {
                // Do the action specified or write out some help.
                switch (action)
                {
                    case "start":
                    case "start-profile":
                        InitLogging(settings, args);
                        InitLoading(args);
                        Start(settings, data);
                        break;
                    case "resume":
                        InitLogging(settings, args);
                        InitLoading(args);
                        Resume(settings, data);
                        break;
                    case "replay":
                        InitLogging(settings, args);
                        InitLoading(args);
                        Replay(settings, data);
                        break;
                    case "replay_from_save":
                        InitLogging(settings, args);
                        InitLoading(args);
                        ReplayFromSave(settings, data);
                        break;
                    case "test":
                        InitLogging(settings, args, true);
                        InitLoading(args);
                        Test(settings, data);
                        break;
                    default:
                        MessageBox.Show("To start " + Application.ProductName + ", please run 'OpenRails.exe'.\n\n"
                                + "If you are attempting to debug this component, please run 'OpenRails.exe' and execute the scenario you are interested in. "
                                + "In the log file, the command-line arguments used will be listed at the top. "
                                + "You should then configure your debug environment to execute this component with those command-line arguments.",
                                Application.ProductName + " " + VersionInfo.VersionOrBuild);
                        Game.Exit();
                        break;
                }
            };
            if (Debugger.IsAttached) // Separate code path during debugging, so IDE stops at the problem and not at the message.
            {
                doAction();
            }
            else
            {
                try
                {
                    doAction();
                }
                catch (ThreadAbortException)
                {
                    // This occurs if we're aborting the loading. Don't report this.
                }
                catch (Exception error)
                {
                    Trace.WriteLine(new FatalException(error));
                    if (settings.ShowErrorDialogs)
                    {
                        // If we had a load error but the inner error is one we handle here specially, unwrap it and discard the extra file information.
                        var loadError = error as FileLoadException;
                        if (loadError != null && (error.InnerException is FileNotFoundException || error.InnerException is DirectoryNotFoundException))
                            error = error.InnerException;

                        if (error is IncompatibleSaveException)
                        {
                            MessageBox.Show(error.Message, Application.ProductName + " " + VersionInfo.VersionOrBuild, MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                        else if (error is FileNotFoundException)
                        {
                            MessageBox.Show(String.Format(
                                    "An essential file is missing and {0} cannot continue.\n\n" +
                                    "    {1}",
                                    Application.ProductName, (error as FileNotFoundException).FileName),
                                    Application.ProductName + " " + VersionInfo.VersionOrBuild, MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                        else if (error is DirectoryNotFoundException)
                        {
                            // This is a hack to try and extract the actual file name from the exception message. It isn't available anywhere else.
                            var re = new Regex("'([^']+)'").Match(error.Message);
                            var fileName = re.Groups[1].Success ? re.Groups[1].Value : error.Message;
                            MessageBox.Show(String.Format(
                                    "An essential folder is missing and {0} cannot continue.\n\n" +
                                    "    {1}",
                                    Application.ProductName, fileName),
                                    Application.ProductName + " " + VersionInfo.VersionOrBuild, MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                        else
                        {
                            var errorSummary = error.GetType().FullName + ": " + error.Message;
                            var logFile = Path.Combine(settings.LoggingPath, settings.LoggingFilename);
                            var openTracker = MessageBox.Show(String.Format(
                                    "A fatal error has occured and {0} cannot continue.\n\n" +
                                    "    {1}\n\n" +
                                    "This error may be due to bad data or a bug. You can help improve {0} by reporting this error in our bug tracker at http://launchpad.net/or and attaching the log file {2}.\n\n" +
                                    ">>> Please report this error to the {0} bug tracker <<<",
                                    Application.ProductName, errorSummary, logFile),
                                    Application.ProductName + " " + VersionInfo.VersionOrBuild, MessageBoxButtons.OKCancel, MessageBoxIcon.Error);
                            if (openTracker == DialogResult.OK)
                                Process.Start("http://launchpad.net/or");
                            // James Ross would prefer to do this:
                            //   Process.Start("http://bugs.launchpad.net/or/+filebug?field.title=" + Uri.EscapeDataString(errorSummary));
                            // but unfortunately if you need to log in (as most people might), Launchpad munges the title
                            // and leaves you with garbage. Plus, landing straight on a login page might confuse some people.
                        }
                    }
                    // Make sure we quit after handling an error.
                    Game.Exit();
                }
            }
            UninitLoading();
        }

        /// <summary>
        /// Run the specified activity from the beginning.
        /// </summary>
        void Start(UserSettings settings, string[] args)
        {
            InitSimulator(settings, args);
            Simulator.Start();

            Viewer = new Viewer(Simulator, Game);
            Viewer.Log = new CommandLog(Viewer);

            if (Client != null)
            {
                Client.Send((new MSGPlayer(Program.UserName, Program.Code, Program.Simulator.conFileName, Program.Simulator.patFileName, Program.Simulator.Trains[0], 0, Program.Simulator.Settings.AvatarURL)).ToString());
            }

            Game.ReplaceState(new GameStateViewer3D(Viewer));
        }

        /// <summary>
        /// Save the current game state for later resume.
        /// </summary>
        [CallOnThread("Updater")]
        public static void Save()
        {
            if (MPManager.IsMultiPlayer()) return; //no save for multiplayer sessions yet
            // Prefix with the activity filename so that, when resuming from the Menu.exe, we can quickly find those Saves 
            // that are likely to match the previously chosen route and activity.
            // Append the current date and time, so that each file is unique.
            // This is the "sortable" date format, ISO 8601, but with "." in place of the ":" which are not valid in filenames.
            var fileStem = String.Format("{0} {1:yyyy'-'MM'-'dd HH'.'mm'.'ss}", Simulator.Activity != null ? Simulator.ActivityFileName : Simulator.RoutePathName, DateTime.Now);

            using (BinaryWriter outf = new BinaryWriter(new FileStream(UserSettings.UserDataFolder + "\\" + fileStem + ".save", FileMode.Create, FileAccess.Write)))
            {
                // Save some version identifiers so we can validate on load.
                outf.Write(VersionInfo.Version);
                outf.Write(VersionInfo.Build);

                // Save heading data used in Menu.exe
                outf.Write(Simulator.RouteName);
                outf.Write(Simulator.PathName);

                outf.Write((int)Simulator.GameTime);
                outf.Write(DateTime.Now.ToBinary());
                outf.Write(Simulator.Trains[0].FrontTDBTraveller.TileX + (Simulator.Trains[0].FrontTDBTraveller.X / 2048));
                outf.Write(Simulator.Trains[0].FrontTDBTraveller.TileZ + (Simulator.Trains[0].FrontTDBTraveller.Z / 2048));
                outf.Write(Simulator.InitialTileX);
                outf.Write(Simulator.InitialTileZ);

                // Now save the data used by RunActivity.exe
                outf.Write(Arguments.Length);
                foreach (var argument in Arguments)
                    outf.Write(argument);

                // The Save command is the only command that doesn't take any action. It just serves as a marker.
                new SaveCommand(Viewer.Log, fileStem);
                Viewer.Log.SaveLog(Path.Combine(UserSettings.UserDataFolder, fileStem + ".replay"));

                // Copy the logfile to the save folder
                CopyLog(Path.Combine(UserSettings.UserDataFolder, fileStem + ".txt"));

                Simulator.Save(outf);
                Viewer.Save(outf, fileStem);

                Console.WriteLine();
            }
        }

        /// <summary>
        /// Resume a saved game.
        /// </summary>
        void Resume(UserSettings settings, string[] args)
        {
            // If "-resume" also specifies a save file then use it
            // E.g. RunActivity.exe -resume "yard_two 2012-03-20 22.07.36"
            // else use most recently changed *.save
            // E.g. RunActivity.exe -resume

            // First use the .save file to check the validity and extract the route and activity.
            var saveFile = GetSaveFile(args);
            using (BinaryReader inf = new BinaryReader(new FileStream(saveFile, FileMode.Open, FileAccess.Read)))
            {
                ValidateSave(saveFile, inf);
                var values = GetSavedValues(inf);
                InitSimulator(settings, values.args, "Resume");
                Simulator.Restore(inf, values.initialTileX, values.initialTileZ);
                Viewer = new Viewer(Simulator, Game);
                Viewer.Restore(inf);

                // Reload the command log
                Viewer.Log = new CommandLog(Viewer);
                Viewer.Log.LoadLog(Path.ChangeExtension(saveFile, "replay"));

                Game.ReplaceState(new GameStateViewer3D(Viewer));
            }
        }

        /// <summary>
        /// Replay a saved game.
        /// </summary>
        void Replay(UserSettings settings, string[] args)
        {
            // If "-replay" also specifies a save file then use it
            // E.g. RunActivity.exe -replay "yard_two 2012-03-20 22.07.36"
            // else use most recently changed *.save
            // E.g. RunActivity.exe -replay

            // First use the .save file to check the validity and extract the route and activity.
            string saveFile = GetSaveFile(args);
            using (BinaryReader inf = new BinaryReader(new FileStream(saveFile, FileMode.Open, FileAccess.Read)))
            {
                inf.ReadString();    // Revision
                inf.ReadString();    // Build
                savedValues values = GetSavedValues(inf);
                InitSimulator(settings, values.args, "Replay");
                Simulator.Start();
                Viewer = new Viewer(Simulator, Game);
            }

            Viewer.Log = new CommandLog(Viewer);
            // Load command log to replay
            Viewer.ReplayCommandList = new List<ICommand>();
            string replayFile = Path.ChangeExtension(saveFile, "replay");
            Viewer.Log.LoadLog(replayFile);
            foreach (var c in Viewer.Log.CommandList)
            {
                Viewer.ReplayCommandList.Add(c);
            }
            Viewer.Log.CommandList.Clear();
            CommandLog.ReportReplayCommands(Viewer.ReplayCommandList);

            Game.ReplaceState(new GameStateViewer3D(Viewer));
        }

        /// <summary>
        /// Replay the last segment of a saved game.
        /// </summary>
        void ReplayFromSave(UserSettings settings, string[] args)
        {
            // E.g. RunActivity.exe -replay_from_save "yard_two 2012-03-20 22.07.36"
            var saveFile = GetSaveFile(args);

            // Find previous save file and move commands to be replayed into replay list.
            var log = new CommandLog();
            var logFile = saveFile.Replace(".save", ".replay");
            log.LoadLog(logFile);
            var replayCommandList = new List<ICommand>();

            // Scan backwards to find previous saveFile (ignore any that user has deleted).
            var count = log.CommandList.Count;
            var previousSaveFile = "";
            for (int i = count - 2; // -2 so we skip over the final save command
                    i >= 0; i--)
            {
                var c = log.CommandList[i] as SaveCommand;
                if (c != null)
                {
                    var f = Path.Combine(UserSettings.UserDataFolder, c.FileStem);
                    if (!f.EndsWith(".save"))
                        f += ".save";
                    if (File.Exists(f))
                    {
                        previousSaveFile = f;
                        // Move commands after this to the replay command list.
                        for (var j = i + 1; j < count; j++)
                        {
                            replayCommandList.Add(log.CommandList[i + 1]);
                            log.CommandList.RemoveAt(i + 1);
                        }
                        break;
                    }
                }
            }
            if (previousSaveFile == "")
            {
                // No save file found so just replay from start
                replayCommandList.AddRange(log.CommandList);    // copy the commands before deleting them.
                log.CommandList.Clear();
                // But we have no args, so have to get these from the Save
                using (var inf = new BinaryReader(new FileStream(saveFile, FileMode.Open, FileAccess.Read)))
                {
                    ValidateSave(saveFile, inf);
                    var values = GetSavedValues(inf);
                    InitSimulator(settings, values.args, "Replay");
                }
                Simulator.Start();
                Viewer = new Viewer(Simulator, Game);
            }
            else
            {
                // Resume from previousSaveFile
                // and then replay
                using (var inf = new BinaryReader(new FileStream(previousSaveFile, FileMode.Open, FileAccess.Read)))
                {
                    ValidateSave(previousSaveFile, inf);
                    savedValues values = GetSavedValues(inf);
                    InitSimulator(settings, values.args, "Resume");
                    Simulator.Restore(inf, values.initialTileX, values.initialTileZ);
                    Viewer = new Viewer(Simulator, Game);
                    Viewer.Restore(inf);
                }
            }

            // Now Viewer exists, link the log to it in both directions
            Viewer.Log = log;
            log.Viewer = Viewer;
            // Now Simulator exists, link the viewer to it
            Viewer.Log.Simulator = Simulator;
            Viewer.ReplayCommandList = replayCommandList;
            CommandLog.ReportReplayCommands(Viewer.ReplayCommandList);

            Game.ReplaceState(new GameStateViewer3D(Viewer));
        }

        /// <summary>
        /// Tests that RunActivity.exe can launch a specific activity or explore.
        /// </summary>
        void Test(UserSettings settings, string[] args)
        {
            var startTime = DateTime.Now;
            var exitGameState = new GameStateViewer3DTest(args);
            try
            {
                InitSimulator(settings, args, "Test");
                Simulator.Start();
                Viewer = new Viewer(Simulator, Game);
                Viewer.Log = new CommandLog(Viewer);
                Game.ReplaceState(exitGameState);
                Game.PushState(new GameStateViewer3D(Viewer));
                exitGameState.LoadTime = (DateTime.Now - startTime).TotalSeconds - Viewer.RealTime;
                exitGameState.Passed = true;
            }
            catch
            {
                Game.ReplaceState(exitGameState);
            }
        }

        class GameStateViewer3DTest : GameState
        {
            public bool Passed;
            public double LoadTime;

            readonly string[] Args;

            public GameStateViewer3DTest(string[] args)
            {
                Args = args;
            }

            internal override void Load()
            {
                Game.PopState();
            }

            internal override void Dispose()
            {
                ExportTestSummary(Game.Settings, Args, Passed, LoadTime);
                Environment.ExitCode = Passed ? 0 : 1;

                base.Dispose();
            }

            static void ExportTestSummary(UserSettings settings, string[] args, bool passed, double loadTime)
            {
                // Append to CSV file in format suitable for Excel
                var summaryFileName = Path.Combine(UserSettings.UserDataFolder, "TestingSummary.csv");
                // Could fail if already opened by Excel
                try
                {
                    using (var writer = File.AppendText(summaryFileName))
                    {
                        // Route, Activity, Passed, Errors, Warnings, Infos, Load Time, Frame Rate
                        writer.WriteLine("{0},{1},{2},{3},{4},{5},{6:F1},{7:F1}",
                            Simulator != null && Simulator.TRK != null && Simulator.TRK.Tr_RouteFile != null ? Simulator.TRK.Tr_RouteFile.Name.Replace(",", ";") : "",
                            Simulator != null && Simulator.Activity != null && Simulator.Activity.Tr_Activity != null && Simulator.Activity.Tr_Activity.Tr_Activity_Header != null ? Simulator.Activity.Tr_Activity.Tr_Activity_Header.Name.Replace(",", ";") : "",
                            passed ? "Yes" : "No",
                            ORTraceListener != null ? ORTraceListener.Counts[0] + ORTraceListener.Counts[1] : 0,
                            ORTraceListener != null ? ORTraceListener.Counts[2] : 0,
                            ORTraceListener != null ? ORTraceListener.Counts[3] : 0,
                            loadTime,
                            Viewer != null && Viewer.RenderProcess != null ? Viewer.RenderProcess.FrameRate.SmoothedValue : 0);
                    }
                }
                catch { } // Ignore any errors
            }
        }

        void InitLogging(UserSettings settings, string[] args)
        {
            InitLogging(settings, args, false);
        }

        void InitLogging(UserSettings settings, string[] args, bool appendLog)
        {
            if (settings.LoggingPath == "")
            {
                settings.LoggingPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            }
            if (settings.Logging)
            {
                if ((settings.LoggingPath.Length > 0) && Directory.Exists(settings.LoggingPath))
                {
                    var fileName = settings.LoggingFilename;
                    try
                    {
                        fileName = String.Format(fileName, Application.ProductName, VersionInfo.VersionOrBuild, VersionInfo.Version, VersionInfo.Build, DateTime.Now);
                    }
                    catch { }
                    foreach (var ch in Path.GetInvalidFileNameChars())
                        fileName = fileName.Replace(ch, '.');

                    logFileName = Path.Combine(settings.LoggingPath, fileName);
                    // Ensure we start with an empty file.
                    if (!appendLog)
                        File.Delete(logFileName);
                    // Make Console.Out go to the log file AND the output stream.
                    Console.SetOut(new FileTeeLogger(logFileName, Console.Out));
                    // Make Console.Error go to the new Console.Out.
                    Console.SetError(Console.Out);
                }
            }

            // Captures Trace.Trace* calls and others and formats.
            ORTraceListener = new ORTraceListener(Console.Out, !settings.Logging);
            ORTraceListener.TraceOutputOptions = TraceOptions.Callstack;
            // Trace.Listeners and Debug.Listeners are the same list.
            Trace.Listeners.Add(ORTraceListener);

            Console.WriteLine("{0} is starting...", Application.ProductName); { int i = 0; foreach (var a in args) { Console.WriteLine(String.Format("Argument {0} = {1}", i++, a)); } }

            Console.WriteLine("Version    = {0}", VersionInfo.Version.Length > 0 ? VersionInfo.Version : "<none>");
            Console.WriteLine("Build      = {0}", VersionInfo.Build);
            if (logFileName.Length > 0)
                Console.WriteLine("Logfile    = {0}", logFileName);
            LogSeparator();
            settings.Log();
            LogSeparator();
            if (!settings.Logging)
            {
                Console.WriteLine("Logging is disabled, only fatal errors will appear here.");
                LogSeparator();
            }
        }

        #region Loading progress indication calculations

        const int LoadingSampleCount = 100;

        string LoadingDataKey;
        string LoadingDataFilePath;
        long LoadingBytesInitial;
        int LoadingTime;
        DateTime LoadingStart;
        long[] LoadingBytesExpected;
        List<long> LoadingBytesActual;
        TimeSpan LoadingBytesSampleRate;
        DateTime LoadingNextSample = DateTime.MinValue;
        float LoadedPercent = -1;

        void InitLoading(string[] args)
        {
            // Get the initial bytes; this is subtracted from all further uses of GetProcessBytesLoaded().
            LoadingBytesInitial = GetProcessBytesLoaded();

            // We hash together all the appropriate arguments to the program as the key for the loading cache file.
            // Arguments without a '.' in them and those starting '/' are ignored, since they are explore activity
            // configuration (time, season, etc.) or flags like /test which we don't want to change on.
            LoadingDataKey = String.Join(" ", args.Where(a => a.Contains('.') && !a.StartsWith("-") && !a.StartsWith("/")).ToArray()).ToLowerInvariant();
            var hash = new MD5CryptoServiceProvider();
            hash.ComputeHash(Encoding.Default.GetBytes(LoadingDataKey));
            var loadingHash = String.Join("", hash.Hash.Select(h => h.ToString("x2")).ToArray());
            var dataPath = Path.Combine(UserSettings.UserDataFolder, "Load Cache");
            LoadingDataFilePath = Path.Combine(dataPath, loadingHash + ".dat");

            if (!Directory.Exists(dataPath))
                Directory.CreateDirectory(dataPath);

            var loadingTime = 0;
            var bytesExpected = new long[LoadingSampleCount];
            var bytesActual = new List<long>(LoadingSampleCount);
            // The loading of the cached data doesn't matter if anything goes wrong; we'll simply have no progress bar.
            try
            {
                using (var data = File.OpenRead(LoadingDataFilePath))
                {
                    using (var reader = new BinaryReader(data))
                    {
                        reader.ReadString();
                        loadingTime = reader.ReadInt32();
                        for (var i = 0; i < LoadingSampleCount; i++)
                            bytesExpected[i] = reader.ReadInt64();
                    }
                }
            }
            catch { }

            LoadingTime = loadingTime;
            LoadingStart = DateTime.Now;
            LoadingBytesExpected = bytesExpected;
            LoadingBytesActual = bytesActual;
            // Using the cached loading time, pick a sample rate that will get us ~100 samples. Clamp to 100ms < x < 10,000ms.
            LoadingBytesSampleRate = new TimeSpan(0, 0, 0, 0, (int)MathHelper.Clamp(loadingTime / LoadingSampleCount, 100, 10000));
            LoadingNextSample = LoadingStart + LoadingBytesSampleRate;

#if DEBUG_LOADING
            Console.WriteLine("Loader: Cache key  = {0}", LoadingDataKey);
            Console.WriteLine("Loader: Cache file = {0}", LoadingDataFilePath);
            Console.WriteLine("Loader: Expected   = {0:N0} bytes", LoadingBytesExpected[LoadingSampleCount - 1]);
            Console.WriteLine("Loader: Sampler    = {0:N0} ms", LoadingBytesSampleRate);
            LogSeparator();
#endif
        }

        void UpdateLoading()
        {
            if (LoadingBytesActual == null)
                return;

            var bytes = GetProcessBytesLoaded() - LoadingBytesInitial;

            // Negative indicates no progress data; this happens if the loaded bytes exceeds the cached maximum expected bytes.
            LoadedPercent = -(float)(DateTime.Now - LoadingStart).TotalSeconds / 15;
            for (var i = 0; i < LoadingSampleCount; i++)
            {
                // Find the first expected sample with more bytes. This means we're currently in the (i - 1) to (i) range.
                if (bytes <= LoadingBytesExpected[i])
                {
                    // Calculate the position within the (i - 1) to (i) range using straight interpolation.
                    var expectedP = i == 0 ? 0 : LoadingBytesExpected[i - 1];
                    var expectedC = LoadingBytesExpected[i];
                    var index = i + (float)(bytes - expectedP) / (expectedC - expectedP);
                    LoadedPercent = index / LoadingSampleCount;
                    break;
                }
            }

            if (DateTime.Now > LoadingNextSample)
            {
                // Record a sample every time we should.
                LoadingBytesActual.Add(bytes);
                LoadingNextSample += LoadingBytesSampleRate;
            }
        }

        void UninitLoading()
        {
            if (LoadingDataKey == null)
                return;

            var loadingTime = DateTime.Now - LoadingStart;
            var bytes = GetProcessBytesLoaded() - LoadingBytesInitial;
            LoadingBytesActual.Add(bytes);

            // Convert from N samples to 100 samples.
            var bytesActual = new long[LoadingSampleCount];
            for (var i = 0; i < LoadingSampleCount; i++)
            {
                var index = (float)(i + 1) / LoadingSampleCount * (LoadingBytesActual.Count - 1);
                var indexR = index - Math.Floor(index);
                bytesActual[i] = (int)(LoadingBytesActual[(int)Math.Floor(index)] * indexR + LoadingBytesActual[(int)Math.Ceiling(index)] * (1 - indexR));
            }

            var bytesExpected = LoadingBytesExpected;
            var expected = bytesExpected[LoadingSampleCount - 1];
            var difference = bytes - expected;

            Console.WriteLine("Loader: Time       = {0:N0} ms", loadingTime.ToString());
            Console.WriteLine("Loader: Expected   = {0:N0} bytes", expected);
            Console.WriteLine("Loader: Actual     = {0:N0} bytes", bytes);
            Console.WriteLine("Loader: Difference = {0:N0} bytes ({1:P1})", difference, (float)difference / expected);
#if DEBUG_LOADING
            for (var i = 0; i < LoadingSampleCount; i++)
                Console.WriteLine("Loader: Sample {0,2}  = {1,13:N0} / {2,13:N0} ({3:N0})", i, bytesExpected[i], bytesActual[i], bytesActual[i] - bytesExpected[i]);
#endif
            Console.WriteLine();

            // Smoothly move all expected values towards actual values, by 10% each run. First run will just copy actual values.
            for (var i = 0; i < LoadingSampleCount; i++)
                bytesExpected[i] = bytesExpected[i] > 0 ? bytesExpected[i] * 9 / 10 + bytesActual[i] / 10 : bytesActual[i];

            // Like loading, saving the loading cache data doesn't matter if it fails. We'll just have no data to show progress with.
            try
            {
                using (var data = File.OpenWrite(LoadingDataFilePath))
                {
                    data.SetLength(0);
                    using (var writer = new BinaryWriter(data))
                    {
                        writer.Write(LoadingDataKey);
                        writer.Write((int)loadingTime.TotalMilliseconds);
                        for (var i = 0; i < LoadingSampleCount; i++)
                            writer.Write(bytesExpected[i]);
                    }
                }
            }
            catch { }
        }

        #endregion

        static void CopyLog(string toFile)
        {
            if (logFileName.Length == 0) return;
            File.Copy(logFileName, toFile, true);
        }

        void InitSimulator(UserSettings settings, string[] args)
        {
            InitSimulator(settings, args, "");
        }

        void InitSimulator(UserSettings settings, string[] args, string mode)
        {
            Console.WriteLine(mode.Length > 0 ? "Mode       = {0} {1}" : "Mode       = {1}", mode, args.Length == 1 ? "Activity" : "Explore");
            if (args.Length == 1)
            {
                Console.WriteLine("Activity   = {0}", args[0]);
            }
            else if (args.Length == 3)
            {
                Console.WriteLine("Activity   = {0}", args[0]);
            }
            else if (args.Length == 4)
            {
                Console.WriteLine("Activity   = {0}", args[0]);
            }
            else
            {
                Console.WriteLine("Path       = {0}", args[0]);
                Console.WriteLine("Consist    = {0}", args[1]);
                Console.WriteLine("Time       = {0}", args[2]);
                Console.WriteLine("Season     = {0}", args[3]);
                Console.WriteLine("Weather    = {0}", args[4]);
            }
            LogSeparator();
            if (settings.MultiplayerServer || settings.MultiplayerClient)
            {
                if (settings.MultiplayerServer)
                    Console.WriteLine("Multiplayer Server");
                else
                    Console.WriteLine("Multiplayer Client");
                Console.WriteLine("User       = {0}", settings.Multiplayer_User);
                if (settings.MultiplayerClient)
                    Console.WriteLine("Host       = {0}", settings.Multiplayer_Host);
                Console.WriteLine("Port       = {0}", settings.Multiplayer_Port);
                LogSeparator();
            }

            Arguments = args;
            Simulator = new Simulator(settings, args[0]);

            if (LoadingScreen == null)
                LoadingScreen = new LoadingScreenPrimitive(Game);

            if (args.Length == 1)
                Simulator.SetActivity(args[0]);
            else if (args.Length == 5)
                Simulator.SetExplore(args[0], args[1], args[2], args[3], args[4]);

            if (settings.MultiplayerServer)
            {
                try
                {
                    Server = new Server(settings.Multiplayer_User + " 1234", settings.Multiplayer_Port);
                    UserName = Server.UserName;
                    Debug.Assert(UserName.Length >= 4 && UserName.Length <= 10 && !UserName.Contains('\"') && !UserName.Contains('\'') && !char.IsDigit(UserName[0]),
                        "Error in the user name: should not start with digits, be 4-10 characters long and no special characters");
                    Code = Server.Code;
                    MPManager.Instance().MPUpdateInterval = settings.Multiplayer_UpdateInterval;
                }
                catch (Exception error)
                {
                    Trace.WriteLine(error);
                    Console.WriteLine("Connection error - will play in single mode.");
                    Server = null;
                }
            }

            if (settings.MultiplayerClient)
            {
                try
                {
                    MPManager.Instance().MPUpdateInterval = settings.Multiplayer_UpdateInterval;
                    Client = new ClientComm(settings.Multiplayer_Host, settings.Multiplayer_Port, settings.Multiplayer_User + " 1234");
                    UserName = Client.UserName;
                    Debug.Assert(UserName.Length >= 4 && UserName.Length <= 10 && !UserName.Contains('\"') && !UserName.Contains('\'') && !char.IsDigit(UserName[0]),
                        "Error in the user name: should not start with digits, be 4-10 characters long and no special characters");
                    Code = Client.Code;
                }
                catch (Exception error)
                {
                    Trace.WriteLine(error);
                    Console.WriteLine("Connection error - will play in single mode.");
                    Client = null;
                }
            }
        }

        void LogSeparator()
        {
            Console.WriteLine(new String('-', 80));
        }

        void ValidateSave(string fileName, BinaryReader inf)
        {
            // Read in validation data.
            var version = "<unknown>";
            var build = "<unknown>";
            var versionOkay = false;
            try
            {
                version = inf.ReadString().Replace("\0", "");
                build = inf.ReadString().Replace("\0", "");
                versionOkay = (version == VersionInfo.Version) && (build == VersionInfo.Build);
            }
            catch { }

            if (!versionOkay)
            {
                if (Debugger.IsAttached)
                {
                    // Only if debugging, then allow user to continue as
                    // resuming from saved activities is useful in debugging.
                    // (To resume from the latest save, set 
                    // RunActivity > Properties > Debug > Command line arguments = "-resume")
                    Trace.WriteLine(new IncompatibleSaveException(fileName, version, build, VersionInfo.Version, VersionInfo.Build));
                    LogSeparator();
                }
                else
                {
                    throw new IncompatibleSaveException(fileName, version, build, VersionInfo.Version, VersionInfo.Build);
                }
            }
        }

        string GetSaveFile(string[] args)
        {
            if (args.Length == 0)
            {
                return GetMostRecentSave();
            }
            string saveFile = args[0];
            if (!saveFile.EndsWith(".save")) { saveFile += ".save"; }
            return Path.Combine(UserSettings.UserDataFolder, saveFile);
        }

        string GetMostRecentSave()
        {
            var directory = new DirectoryInfo(UserSettings.UserDataFolder);
            var file = directory.GetFiles("*.save")
             .OrderByDescending(f => f.LastWriteTime)
             .First();
            if (file == null) throw new FileNotFoundException(String.Format(
               "Activity Save file '*.save' not found in folder {0}", directory));
            return file.FullName;
        }

        savedValues GetSavedValues(BinaryReader inf)
        {
            savedValues values = default(savedValues);
            // Skip the heading data used in Menu.exe
            inf.ReadString();    // Route name
            inf.ReadString();    // Path name
            inf.ReadInt32();     // Time elapsed in game (secs)
            inf.ReadInt64();     // Date and time in real world
            inf.ReadSingle();    // Current location of player train TileX
            inf.ReadSingle();    // Current location of player train TileZ

            // Read initial position and pass to Simulator so it can be written out if another save is made.
            values.initialTileX = inf.ReadSingle();  // Initial location of player train TileX
            values.initialTileZ = inf.ReadSingle();  // Initial location of player train TileZ

            // Read in the real data...
            var savedArgs = new string[inf.ReadInt32()];
            for (var i = 0; i < savedArgs.Length; i++)
                savedArgs[i] = inf.ReadString();
            values.args = savedArgs;
            return values;
        }

        long GetProcessBytesLoaded()
        {
            NativeMathods.IO_COUNTERS counters;
            if (NativeMathods.GetProcessIoCounters(Process.GetCurrentProcess().Handle, out counters))
                return (long)counters.ReadTransferCount;

            return 0;
        }

        class LoadingPrimitive : RenderPrimitive
        {
            public readonly LoadingMaterial Material;
            readonly VertexDeclaration VertexDeclaration;
            readonly VertexBuffer VertexBuffer;

            public LoadingPrimitive(Game game)
            {
                Material = GetMaterial(game);
                var verticies = GetVerticies(game);
                VertexDeclaration = new VertexDeclaration(game.GraphicsDevice, VertexPositionTexture.VertexElements);
                VertexBuffer = new VertexBuffer(game.GraphicsDevice, VertexPositionTexture.SizeInBytes * verticies.Length, BufferUsage.WriteOnly);
                VertexBuffer.SetData(verticies);
            }

            virtual protected LoadingMaterial GetMaterial(Game game)
            {
                return new LoadingMaterial(game);
            }

            virtual protected VertexPositionTexture[] GetVerticies(Game game)
            {
                var dd = (float)Material.Texture.Width / 2;
                return new[] {
				    new VertexPositionTexture(new Vector3(-dd - 0.5f, +dd + 0.5f, -1), new Vector2(0, 0)),
				    new VertexPositionTexture(new Vector3(+dd - 0.5f, +dd + 0.5f, -1), new Vector2(1, 0)),
				    new VertexPositionTexture(new Vector3(-dd - 0.5f, -dd + 0.5f, -1), new Vector2(0, 1)),
				    new VertexPositionTexture(new Vector3(+dd - 0.5f, -dd + 0.5f, -1), new Vector2(1, 1)),
			    };
            }
            
            public override void Draw(GraphicsDevice graphicsDevice)
            {
                graphicsDevice.VertexDeclaration = VertexDeclaration;
                graphicsDevice.Vertices[0].SetSource(VertexBuffer, 0, VertexPositionTexture.SizeInBytes);
                graphicsDevice.DrawPrimitives(PrimitiveType.TriangleStrip, 0, 2);
            }
        }

        class LoadingScreenPrimitive : LoadingPrimitive
        {
            public LoadingScreenPrimitive(Game game)
                : base(game)
            {
            }

            protected override LoadingMaterial GetMaterial(Game game)
            {
                return new LoadingScreenMaterial(game);
            }

            protected override VertexPositionTexture[] GetVerticies(Game game)
            {
                float w, h;

                if (Material.Texture == null)
                {
                    w = h = 0;
                }
                else
                {
                    w = (float)Material.Texture.Width;
                    h = (float)Material.Texture.Height;
                    var scaleX = (float)game.RenderProcess.DisplaySize.X / w;
                    var scaleY = (float)game.RenderProcess.DisplaySize.Y / h;
                    var scale = scaleX < scaleY ? scaleX : scaleY;
                    w = w * scale / 2;
                    h = h * scale / 2;
                }
                return new[] {
				    new VertexPositionTexture(new Vector3(-w - 0.5f, +h + 0.5f, -1), new Vector2(0, 0)),
				    new VertexPositionTexture(new Vector3(+w - 0.5f, +h + 0.5f, -1), new Vector2(1, 0)),
				    new VertexPositionTexture(new Vector3(-w - 0.5f, -h + 0.5f, -1), new Vector2(0, 1)),
				    new VertexPositionTexture(new Vector3(+w - 0.5f, -h + 0.5f, -1), new Vector2(1, 1)),
			    };
            }
        }

        class LoadingBarPrimitive : LoadingPrimitive
        {
            public LoadingBarPrimitive(Game game)
                : base(game)
            {
            }

            protected override LoadingMaterial GetMaterial(Game game)
            {
                return new LoadingBarMaterial(game);
            }

            protected override VertexPositionTexture[] GetVerticies(Game game)
            {
                var margin = 10;
                var w = game.RenderProcess.DisplaySize.X - 2 * margin;
                var h = 2 * margin;
                var x = -w / 2 + 0.5f;
                var y = game.RenderProcess.DisplaySize.Y / 2 - h - margin + 0.5f;
                return new[] {
				    new VertexPositionTexture(new Vector3(x, -y, -1), new Vector2(0, 0)),
				    new VertexPositionTexture(new Vector3(x + w, -y, -1), new Vector2(1, 0)),
				    new VertexPositionTexture(new Vector3(x, -y - h, -1), new Vector2(0, 1)),
				    new VertexPositionTexture(new Vector3(x + w, -y - h, -1), new Vector2(1, 1)),
			    };
            }
        }

        class LoadingMaterial : Material
        {
            public readonly LoadingShader Shader;
            public readonly Texture2D Texture;

            public LoadingMaterial(Game game)
                : base(null, null)
            {
                Shader = new LoadingShader(game.RenderProcess.GraphicsDevice);
                Texture = GetTexture(game);
            }

            virtual protected Texture2D GetTexture(Game game)
            {
                return Texture2D.FromFile(game.RenderProcess.GraphicsDevice, Path.Combine(game.ContentPath, "Loading.png"));
            }

            public override void SetState(GraphicsDevice graphicsDevice, Material previousMaterial)
            {
                Shader.CurrentTechnique = Shader.Techniques["Loading"];
                Shader.LoadingTexture = Texture;

                graphicsDevice.RenderState.AlphaBlendEnable = true;
                graphicsDevice.RenderState.DestinationBlend = Blend.InverseSourceAlpha;
                graphicsDevice.RenderState.SourceBlend = Blend.SourceAlpha;
            }

            public override void Render(GraphicsDevice graphicsDevice, IEnumerable<RenderItem> renderItems, ref Matrix XNAViewMatrix, ref Matrix XNAProjectionMatrix)
            {
                Shader.Begin();
                Shader.CurrentTechnique.Passes[0].Begin();
                foreach (var item in renderItems)
                {
                    Shader.WorldViewProjection = item.XNAMatrix * XNAViewMatrix * XNAProjectionMatrix;
                    Shader.CommitChanges();
                    item.RenderPrimitive.Draw(graphicsDevice);
                }
                Shader.CurrentTechnique.Passes[0].End();
                Shader.End();
            }

            public override void ResetState(GraphicsDevice graphicsDevice)
            {
                graphicsDevice.RenderState.AlphaBlendEnable = false;
                graphicsDevice.RenderState.DestinationBlend = Blend.Zero;
                graphicsDevice.RenderState.SourceBlend = Blend.One;
            }
        }

        class LoadingScreenMaterial : LoadingMaterial
        {
            public LoadingScreenMaterial(Game game)
                : base(game)
            {
            }

            protected override Texture2D GetTexture(Game game)
            {
                var path = Path.Combine(Simulator.RoutePath, "load.ace");
                if (File.Exists(path))
                    return MSTS.ACEFile.Texture2DFromFile(game.RenderProcess.GraphicsDevice, path);
                return null;
            }
        }

        class LoadingBarMaterial : LoadingMaterial
        {
            public LoadingBarMaterial(Game game)
                : base(game)
            {
            }

            public override void SetState(GraphicsDevice graphicsDevice, Material previousMaterial)
            {
                base.SetState(graphicsDevice, previousMaterial);
                Shader.CurrentTechnique = Shader.Techniques["LoadingBar"];
            }
        }

        class LoadingShader : Shader
        {
            readonly EffectParameter worldViewProjection;
            readonly EffectParameter loadingPercent;
            readonly EffectParameter loadingTexture;

            public Matrix WorldViewProjection { set { worldViewProjection.SetValue(value); } }

            public float LoadingPercent { set { loadingPercent.SetValue(value); } }

            public Texture2D LoadingTexture { set { loadingTexture.SetValue(value); } }

            public LoadingShader(GraphicsDevice graphicsDevice)
                : base(graphicsDevice, "Loading")
            {
                worldViewProjection = Parameters["WorldViewProjection"];
                loadingPercent = Parameters["LoadingPercent"];
                loadingTexture = Parameters["LoadingTexture"];
            }
        }

        static class NativeMathods
        {
            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern bool GetProcessIoCounters(IntPtr hProcess, out IO_COUNTERS lpIoCounters);

            [StructLayout(LayoutKind.Sequential)]
            public struct IO_COUNTERS
            {
                public UInt64 ReadOperationCount;
                public UInt64 WriteOperationCount;
                public UInt64 OtherOperationCount;
                public UInt64 ReadTransferCount;
                public UInt64 WriteTransferCount;
                public UInt64 OtherTransferCount;
            };
        }
    }

    public sealed class IncompatibleSaveException : Exception
    {
        public IncompatibleSaveException(string fileName, string version, string build, string gameVersion, string gameBuild)
            : base(version.Length > 0 && build.Length > 0 ?
                String.Format("Saved game file is not compatible with this version of {0}.\n\nFile: {1}\nSave: {4} ({5})\nGame: {2} ({3})", Application.ProductName, fileName, gameVersion, gameBuild, version, build) :
            String.Format("Saved game file is not compatible with this version of {0}.\n\nFile: {1}\nGame: {2} ({3})", Application.ProductName, fileName, gameVersion, gameBuild))
        {
        }
    }
}
