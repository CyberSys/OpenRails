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

#define GEARBOX_DEBUG_LOG

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ORTS.Common;
using ORTS.Popups;


namespace ORTS
{
    /// <summary>
    /// Displays Viewer frame rate and Viewer.Text debug messages in the upper left corner of the screen.
    /// </summary>
    public class InfoDisplay
    {
        readonly Viewer3D Viewer;
        readonly DataLogger Logger = new DataLogger();
        readonly int ProcessorCount = System.Environment.ProcessorCount;

        bool DrawCarNumber = false;
        // F6 reveals labels for both sidings and platforms.
        // Booleans for both so they can also be used independently.
        bool DrawSiding = false;
        bool DrawPlatform = false;

        SpriteBatchMaterial TextMaterial;
        Label3DMaterial DrawInforMaterial;

        Matrix Identity = Matrix.Identity;
        int FrameNumber = 0;
        double LastUpdateRealTime = 0;   // update text message only 10 times per second

        [StructLayout(LayoutKind.Sequential, Size = 40)]
        struct PROCESS_MEMORY_COUNTERS
        {
            public int cb;
            public int PageFaultCount;
            public int PeakWorkingSetSize;
            public int WorkingSetSize;
            public int QuotaPeakPagedPoolUsage;
            public int QuotaPagedPoolUsage;
            public int QuotaPeakNonPagedPoolUsage;
            public int QuotaNonPagedPoolUsage;
            public int PagefileUsage;
            public int PeakPagefileUsage;
        }

        [DllImport("psapi.dll", SetLastError = true)]
        static extern bool GetProcessMemoryInfo(IntPtr hProcess, out PROCESS_MEMORY_COUNTERS counters, int size);

        [DllImport("kernel32.dll")]
        static extern IntPtr OpenProcess(int dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, int dwProcessId);

        readonly IntPtr ProcessHandle;
        PROCESS_MEMORY_COUNTERS ProcessMemoryCounters;

        public InfoDisplay(Viewer3D viewer)
        {
            Viewer = viewer;
            TextMaterial = (SpriteBatchMaterial)viewer.MaterialManager.Load("SpriteBatch");
            DrawInforMaterial = (Label3DMaterial)viewer.MaterialManager.Load("Label3D");

            ProcessHandle = OpenProcess(0x410 /* PROCESS_QUERY_INFORMATION | PROCESS_VM_READ */, false, Process.GetCurrentProcess().Id);
            ProcessMemoryCounters = new PROCESS_MEMORY_COUNTERS() { cb = 40 };

            if (Viewer.Settings.DataLogger)
                DataLoggerStart(Viewer.Settings);
        }

        [ThreadName("Render")]
        internal void Terminate()
        {
            if (Viewer.Settings.DataLogger)
                DataLoggerStop();
        }

        public void HandleUserInput(ElapsedTime elapsedTime)
        {
            if (UserInput.IsPressed(UserCommands.DebugLogger))
            {
                Viewer.Settings.DataLogger = !Viewer.Settings.DataLogger;
                if (Viewer.Settings.DataLogger)
                    DataLoggerStart(Viewer.Settings);
                else
                    DataLoggerStop();
            }
            if (UserInput.IsPressed(UserCommands.DisplayCarLabels))
                DrawCarNumber = !DrawCarNumber;
            if (UserInput.IsPressed(UserCommands.DisplayStationLabels))
            {
                // Steps along a sequence of 5 states
                // none > both > sidings only > platforms only > none
                //   00 >   11 >           10 >             01 >   00

                // Set the first 2 bits of an int
                int bitArray = 0;
                bitArray += DrawSiding ? 1 : 0;
                bitArray += DrawPlatform ? 2 : 0;
                // Decrement the int to step along the sequence
                bitArray--;
                // Extract first 2 bits of the int
                DrawSiding = ((bitArray & 1) == 1);
                DrawPlatform = ((bitArray & 2) == 2);
                // Take modulus 4 to keep in range 0-3. +1 as messages are in range 1-4
                Viewer.Simulator.Confirmer.Confirm(CabControl.Labels, (CabSetting)(bitArray % 4) + 1);
            }
        }

        public void PrepareFrame(RenderFrame frame, ElapsedTime elapsedTime)
        {
            FrameNumber++;

            if (Viewer.RealTime - LastUpdateRealTime >= 0.25)
            {
                double elapsedRealSeconds = Viewer.RealTime - LastUpdateRealTime;
                LastUpdateRealTime = Viewer.RealTime;
                Profile(elapsedRealSeconds);
            }

            //Here's where the logger stores the data from each frame
            if (Viewer.Settings.DataLogger)
            {
                Logger.Separator = (DataLogger.Separators)Enum.Parse(typeof(DataLogger.Separators), Viewer.Settings.DataLoggerSeparator);
                if (Viewer.Settings.DataLogPerformance)
                {
                    Logger.Data(VersionInfo.Version);
                    Logger.Data(FrameNumber.ToString("F0"));
                    Logger.Data(GetWorkingSetSize().ToString("F0"));
                    Logger.Data(GC.GetTotalMemory(false).ToString("F0"));
                    Logger.Data(GC.CollectionCount(0).ToString("F0"));
                    Logger.Data(GC.CollectionCount(1).ToString("F0"));
                    Logger.Data(GC.CollectionCount(2).ToString("F0"));
                    Logger.Data(ProcessorCount.ToString("F0"));
                    Logger.Data(Viewer.RenderProcess.FrameRate.Value.ToString("F0"));
                    Logger.Data(Viewer.RenderProcess.FrameTime.Value.ToString("F4"));
                    Logger.Data(Viewer.RenderProcess.FrameTime.P95.ToString("F4"));
                    Logger.Data(Viewer.RenderProcess.FrameTime.P99.ToString("F4"));
                    Logger.Data(Viewer.RenderProcess.ShadowPrimitivePerFrame.Sum().ToString("F0"));
                    Logger.Data(Viewer.RenderProcess.PrimitivePerFrame.Sum().ToString("F0"));
                    Logger.Data(Viewer.RenderProcess.Profiler.Wall.Value.ToString("F0"));
                    Logger.Data(Viewer.UpdaterProcess.Profiler.Wall.Value.ToString("F0"));
                    Logger.Data(Viewer.LoaderProcess.Profiler.Wall.Value.ToString("F0"));
                    Logger.Data(Viewer.SoundProcess.Profiler.Wall.Value.ToString("F0"));
                }
                if (Viewer.Settings.DataLogPhysics)
                {
                    Logger.Data(FormattedPreciseTime(Viewer.Simulator.ClockTime));
                    Logger.Data(Viewer.PlayerLocomotive.Direction.ToString());
                    Logger.Data(Viewer.PlayerTrain.MUReverserPercent.ToString("F0"));
                    Logger.Data(Viewer.PlayerLocomotive.ThrottlePercent.ToString("F0"));
                    Logger.Data(Viewer.PlayerLocomotive.MotiveForceN.ToString("F0"));
                    Logger.Data(Viewer.PlayerLocomotive.BrakeForceN.ToString("F0"));
                    Logger.Data((Viewer.PlayerLocomotive as MSTSLocomotive).LocomotiveAxle.AxleForceN.ToString("F2"));
                    Logger.Data((Viewer.PlayerLocomotive as MSTSLocomotive).LocomotiveAxle.SlipSpeedPercent.ToString("F1"));
#if !NEW_SIGNALLING
                Logger.Data(TrackMonitorWindow.FormatSpeed(Viewer.PlayerLocomotive.SpeedMpS, Viewer.MilepostUnitsMetric));
#else
                    switch (Viewer.Settings.DataLogSpeedUnits)
                    {
                        case "route":
                            Logger.Data(FormatStrings.FormatSpeed(Viewer.PlayerLocomotive.SpeedMpS, Viewer.MilepostUnitsMetric));
                            break;
                        case "mps":
                            Logger.Data(Viewer.PlayerLocomotive.SpeedMpS.ToString("F1"));
                            break;
                        case "mph":
                            Logger.Data(MpS.FromMpS(Viewer.PlayerLocomotive.SpeedMpS, false).ToString("F1"));
                            break;
                        case "kmph":
                            Logger.Data(MpS.FromMpS(Viewer.PlayerLocomotive.SpeedMpS, true).ToString("F1"));
                            break;
                        default:
                            Logger.Data(FormatStrings.FormatSpeed(Viewer.PlayerLocomotive.SpeedMpS, Viewer.MilepostUnitsMetric));
                            break;
                    }
#endif
                    Logger.Data((Viewer.PlayerLocomotive.DistanceM.ToString("F0")));
                    Logger.Data((Viewer.PlayerLocomotive.GravityForceN.ToString("F0")));

                    if ((Viewer.PlayerLocomotive as MSTSLocomotive).TrainBrakeController != null)
                        Logger.Data((Viewer.PlayerLocomotive as MSTSLocomotive).TrainBrakeController.CurrentValue.ToString("F2"));
                    else
                        Logger.Data("null");

                    if ((Viewer.PlayerLocomotive as MSTSLocomotive).EngineBrakeController != null)
                        Logger.Data((Viewer.PlayerLocomotive as MSTSLocomotive).EngineBrakeController.CurrentValue.ToString("F2"));
                    else
                        Logger.Data("null");

                    Logger.Data(Viewer.PlayerLocomotive.BrakeSystem.GetCylPressurePSI().ToString("F0"));
                    Logger.Data((Viewer.PlayerLocomotive as MSTSLocomotive).MainResPressurePSI.ToString("F0"));
                    Logger.Data((Viewer.PlayerLocomotive as MSTSLocomotive).CompressorOn.ToString());
#if GEARBOX_DEBUG_LOG
                    if (Viewer.PlayerLocomotive.GetType() == typeof(MSTSDieselLocomotive))
                    {
                        Logger.Data((Viewer.PlayerLocomotive as MSTSDieselLocomotive).DieselEngines[0].RealRPM.ToString("F0"));
                        Logger.Data((Viewer.PlayerLocomotive as MSTSDieselLocomotive).DieselEngines[0].DemandedRPM.ToString("F0"));
                        Logger.Data((Viewer.PlayerLocomotive as MSTSDieselLocomotive).DieselEngines[0].LoadPercent.ToString("F0"));
                        if ((Viewer.PlayerLocomotive as MSTSDieselLocomotive).DieselEngines.HasGearBox)
                        {
                            Logger.Data((Viewer.PlayerLocomotive as MSTSDieselLocomotive).DieselEngines[0].GearBox.CurrentGearIndex.ToString());
                            Logger.Data((Viewer.PlayerLocomotive as MSTSDieselLocomotive).DieselEngines[0].GearBox.NextGearIndex.ToString());
                            Logger.Data((Viewer.PlayerLocomotive as MSTSDieselLocomotive).DieselEngines[0].GearBox.ClutchPercent.ToString());
                        }
                        else
                        {
                            Logger.Data("null");
                            Logger.Data("null");
                            Logger.Data("null");
                        }
                        Logger.Data((Viewer.PlayerLocomotive as MSTSDieselLocomotive).DieselFlowLps.ToString("F2"));
                        Logger.Data((Viewer.PlayerLocomotive as MSTSDieselLocomotive).DieselLevelL.ToString("F0"));
                        Logger.Data("null");
                        Logger.Data("null");
                        Logger.Data("null");
                    }
                    if (Viewer.PlayerLocomotive.GetType() == typeof(MSTSElectricLocomotive))
                    {
                        Logger.Data((Viewer.PlayerLocomotive as MSTSElectricLocomotive).Pan1Up.ToString());
                        Logger.Data((Viewer.PlayerLocomotive as MSTSElectricLocomotive).Pan2Up.ToString());
                        Logger.Data("null");
                        Logger.Data("null");
                        Logger.Data("null");
                        Logger.Data("null");
                        Logger.Data("null");
                        Logger.Data("null");
                        Logger.Data("null");
                        Logger.Data("null");
                        Logger.Data("null");
                    }
                    if (Viewer.PlayerLocomotive.GetType() == typeof(MSTSSteamLocomotive))
                    {
                        Logger.Data((Viewer.PlayerLocomotive as MSTSSteamLocomotive).BlowerSteamUsageLBpS.ToString("F0"));
                        Logger.Data((Viewer.PlayerLocomotive as MSTSSteamLocomotive).BoilerPressurePSI.ToString("F0"));
                        Logger.Data((Viewer.PlayerLocomotive as MSTSSteamLocomotive).CylinderCocksOpen.ToString());
                        Logger.Data((Viewer.PlayerLocomotive as MSTSSteamLocomotive).EvaporationLBpS.ToString("F0"));
                        Logger.Data((Viewer.PlayerLocomotive as MSTSSteamLocomotive).FireMassKG.ToString("F0"));
                        Logger.Data((Viewer.PlayerLocomotive as MSTSSteamLocomotive).SteamUsageLBpS.ToString("F0"));
                        if ((Viewer.PlayerLocomotive as MSTSSteamLocomotive).BlowerController != null)
                            Logger.Data((Viewer.PlayerLocomotive as MSTSSteamLocomotive).BlowerController.CurrentValue.ToString("F0"));
                        else
                            Logger.Data("null");

                        if ((Viewer.PlayerLocomotive as MSTSSteamLocomotive).DamperController != null)
                            Logger.Data((Viewer.PlayerLocomotive as MSTSSteamLocomotive).DamperController.CurrentValue.ToString("F0"));
                        else
                            Logger.Data("null");
                        if ((Viewer.PlayerLocomotive as MSTSSteamLocomotive).FiringRateController != null)
                            Logger.Data((Viewer.PlayerLocomotive as MSTSSteamLocomotive).FiringRateController.CurrentValue.ToString("F0"));
                        else
                            Logger.Data("null");
                        if ((Viewer.PlayerLocomotive as MSTSSteamLocomotive).Injector1Controller != null)
                            Logger.Data((Viewer.PlayerLocomotive as MSTSSteamLocomotive).Injector1Controller.CurrentValue.ToString("F0"));
                        else
                            Logger.Data("null");
                        if ((Viewer.PlayerLocomotive as MSTSSteamLocomotive).Injector2Controller != null)
                            Logger.Data((Viewer.PlayerLocomotive as MSTSSteamLocomotive).Injector2Controller.CurrentValue.ToString("F0"));
                        else
                            Logger.Data("null");
                    }

#endif
                }

                Logger.End();
            }
            if (DrawSiding == true || DrawPlatform == true)
            {
                // TODO: Don't construct new ItemLabelPrimitive on every frame.
                var worldFiles = Viewer.World.Scenery.WorldFiles;
                foreach (var worldFile in worldFiles)
                {
                    if (DrawSiding == true && worldFile.sidings != null)
                        foreach (var siding in worldFile.sidings)
                            frame.AddPrimitive(DrawInforMaterial, new ItemLabelPrimitive(DrawInforMaterial, siding, Color.Coral), RenderPrimitiveGroup.World, ref Identity);
                    if (DrawPlatform == true && worldFile.platforms != null)
                        foreach (var platform in worldFile.platforms)
                            frame.AddPrimitive(DrawInforMaterial, new ItemLabelPrimitive(DrawInforMaterial, platform, Color.Yellow), RenderPrimitiveGroup.World, ref Identity);
                }
            }
            if (DrawCarNumber == true)
            {
                // TODO: Don't construct new CarLabelPrimitive on every frame.
                var cars = Viewer.World.Trains.Cars;
                foreach (var car in cars.Keys)
                    frame.AddPrimitive(DrawInforMaterial, new CarLabelPrimitive(DrawInforMaterial, car), RenderPrimitiveGroup.World, ref Identity);
            }
        }

        [CallOnThread("Loader")]
        public void Mark()
        {
            TextMaterial.Mark();
            DrawInforMaterial.Mark();
        }

        int GetWorkingSetSize()
        {
            // Get memory usage (working set).
            GetProcessMemoryInfo(ProcessHandle, out ProcessMemoryCounters, ProcessMemoryCounters.cb);
            var memory = ProcessMemoryCounters.WorkingSetSize;
            return memory;
        }

        /// <summary>
        /// Converts duration in seconds to hours, minutes and integer seconds
        /// </summary>
        /// <param name="clockTimeSeconds"></param>
        /// <returns></returns>
        public static string FormattedTime(double clockTimeSeconds) //some measure of time so it can be sorted.  Good enuf for now. Might add more later. Okay
        {
            int hour = (int)(clockTimeSeconds / (60.0 * 60.0));
            clockTimeSeconds -= hour * 60.0 * 60.0;
            int minute = (int)(clockTimeSeconds / 60.0);
            clockTimeSeconds -= minute * 60.0;
            int seconds = (int)clockTimeSeconds;
            // Reset clock before and after midnight
            if (hour >= 24)
                hour %= 24;
            if (hour < 0)
                hour += 24;
            if (minute < 0)
                minute += 60;
            if (seconds < 0)
                seconds += 60;

            return string.Format("{0:D2}:{1:D2}:{2:D2}", hour, minute, seconds);
        }

        /// <summary>
        /// Converts duration in seconds to hours, minutes and seconds to 2 decimal places.
        /// </summary>
        /// <param name="clockTimeSeconds"></param>
        /// <returns></returns>
        public static string FormattedPreciseTime(double clockTimeSeconds) //some measure of time so it can be sorted.  Good enuf for now. Might add more later. Okay
        {
            int hour = (int)(clockTimeSeconds / (60.0 * 60.0));
            clockTimeSeconds -= hour * 60.0 * 60.0;
            int minute = (int)(clockTimeSeconds / 60.0);
            clockTimeSeconds -= minute * 60.0;
            double seconds = clockTimeSeconds;
            // Reset clock before and after midnight
            if (hour >= 24)
                hour %= 24;
            if (hour < 0)
                hour += 24;
            if (minute < 0)
                minute += 60;
            if (seconds < 0)
                seconds += 60;

            return string.Format("{0:D2}:{1:D2}:{2:00.00}", hour, minute, seconds);
        }


        /// <summary>
        /// Converts duration from seconds to hours and minutes (to the nearest minute)
        /// </summary>
        /// <param name="clockTimeSeconds"></param>
        /// <returns></returns>
        public static string FormattedApproxTime(double clockTimeSeconds)
        {
            int hour = (int)(clockTimeSeconds / (60.0 * 60.0));
            clockTimeSeconds -= hour * 60.0 * 60.0;
            int minute = (int)((clockTimeSeconds / 60.0) + 0.5);    // + 0.5 to round to nearest minute
            clockTimeSeconds -= minute * 60.0;
            // Reset clock before and after midnight
            if (hour >= 24)
                hour %= 24;
            if (hour < 0)
                hour += 24;
            if (minute < 0)
                minute += 60;

            return string.Format("{0:D2}:{1:D2}", hour, minute);
        }

        static void DataLoggerStart(UserSettings settings)
        {
            using (StreamWriter file = File.AppendText("dump.csv"))
            {
                DataLogger.Separators separator = (DataLogger.Separators)Enum.Parse(typeof(DataLogger.Separators), settings.DataLoggerSeparator);
                string headerLine = "";
                if (settings.DataLogPerformance)
                {
                    headerLine = String.Join(Convert.ToString((char)separator),
                        new string[] 
                            {    
                                "SVN",
                                "Frame",
                                "Memory",
                                "Memory (Managed)",
                                "Gen 0 GC",
                                "Gen 1 GC",
                                "Gen 2 GC",
                                "Processors",
                                "Frame Rate",
                                "Frame Time",
                                "Frame Time P95",
                                "Frame Time P99",
                                "Shadow Primitives",
                                "Render Primitives",
                                "Render Process",
                                "Updater Process",
                                "Loader Process",
                                "Sound Process"
                            }
                        );
                }
                if (settings.DataLogPhysics)
                {
                    if (settings.DataLogPerformance)
                        headerLine += Convert.ToString((char)separator);

                    headerLine += String.Join(Convert.ToString((char)separator),
                            new string[] 
                            {
                                "Time",
                                "Player Direction",
                                "Player Reverser [%]",
                                "Player Throttle [%]",
                                "Player Motive Force [N]",
                                "Player Brake Force [N]",
                                "Player Axle Force [N]",
                                "Player Wheelslip",
                                "Player Speed [" + settings.DataLogSpeedUnits + "]",
                                "Distance [m]",
                                "Player Gravity Force [N]",
                                "Train Brake",
                                "Engine Brake",
                                "Player Cylinder PSI",
                                "Player Main Res PSI",
                                "Player Compressor On",
                                "D:Real RPM / E:panto 1 / S:Blower usage LBpS",
                                "D:Demanded RPM / E:panto 2 / S:Boiler PSI",
                                "D:Load % / E:null / S:Cylinder Cocks open",
                                "D:Gearbox Current Gear / E:null / S:Evaporation LBpS",
                                "D:Gearbox Next Gear / E:null / S:Fire Mass KG",
                                "D:Clutch % / E:null / S:Steam usage LBpS",
                                "D:Fuel Flow Lps / E:null / S:Blower",
                                "D:Fuel level L / E:null / S:Damper",
                                "D:null / E:null / S:Firing Rate",
                                "D:null / E:null / S:Injector 1",
                                "D:null / E:null / S:Injector 2"
                            }
                        );
                }
                //Ready to use...
                //if (settings.DataLogMisc)
                //{
                //    if (settings.DataLogPerformance || settings.DataLogPhysics)
                //        headerLine += Convert.ToString((char)separator);
                //    headerLine += String.Join(Convert.ToString((char)separator),
                //        new string[] {"null",
                //        "null"});
                //}

                file.WriteLine(headerLine);

                file.Close();
            }
        }

        void DataLoggerStop()
        {
            Logger.Flush();
        }

        public void Profile(double elapsedRealSeconds) // should be called every 100mS
        {
            if (elapsedRealSeconds < 0.01)  // just in case
                return;

            Viewer.RenderProcess.Profiler.Mark();
            Viewer.UpdaterProcess.Profiler.Mark();
            Viewer.LoaderProcess.Profiler.Mark();
            Viewer.SoundProcess.Profiler.Mark();
        }
    }

    public abstract class InfoLabelPrimitive : RenderPrimitive
    {
        public readonly Label3DMaterial Material;

        protected readonly Viewer3D Viewer;
        protected readonly Color Color;
        protected readonly Color Outline;

        protected InfoLabelPrimitive(Label3DMaterial material, Color color, Color outline)
        {
            Material = material;
            Viewer = material.Viewer;
            Color = color;
            Outline = outline;
        }

        protected void DrawLabel(WorldPosition position, float yOffset, string text)
        {
            var lineLocation3D = position.XNAMatrix.Translation;
            lineLocation3D.X += (position.TileX - Viewer.Camera.TileX) * 2048;
            lineLocation3D.Y += yOffset;
            lineLocation3D.Z += (Viewer.Camera.TileZ - position.TileZ) * 2048;

            var lineLocation2DStart = Viewer.GraphicsDevice.Viewport.Project(lineLocation3D, Viewer.Camera.XNAProjection, Viewer.Camera.XNAView, Matrix.Identity);
            if (lineLocation2DStart.Z > 1 || lineLocation2DStart.Z < 0)
                return; // Out of range or behind the camera

            lineLocation3D.Y += 10;
            var lineLocation2DEndY = Viewer.GraphicsDevice.Viewport.Project(lineLocation3D, Viewer.Camera.XNAProjection, Viewer.Camera.XNAView, Matrix.Identity).Y;

            var labelLocation2D = Material.GetTextLocation((int)lineLocation2DStart.X, (int)lineLocation2DEndY - Material.Font.Height, text);
            lineLocation2DEndY = labelLocation2D.Y + Material.Font.Height;

            Material.Font.Draw(Material.SpriteBatch, labelLocation2D, text, Color, Outline);
            Material.SpriteBatch.Draw(Material.Texture, new Vector2(lineLocation2DStart.X - 1, lineLocation2DEndY), null, Outline, 0, Vector2.Zero, new Vector2(4, lineLocation2DStart.Y - lineLocation2DEndY), SpriteEffects.None, lineLocation2DStart.Z);
            Material.SpriteBatch.Draw(Material.Texture, new Vector2(lineLocation2DStart.X, lineLocation2DEndY), null, Color, 0, Vector2.Zero, new Vector2(2, lineLocation2DStart.Y - lineLocation2DEndY), SpriteEffects.None, lineLocation2DStart.Z);
        }
    }

    public class CarLabelPrimitive : InfoLabelPrimitive
    {
        readonly TrainCar Car;

        public CarLabelPrimitive(Label3DMaterial material, TrainCar car)
            : base(material, Color.Blue, Color.White)
        {
            Car = car;
        }

        public override void Draw(GraphicsDevice graphicsDevice)
        {
            DrawLabel(Car.WorldPosition, Car.HeightM, Car.CarID);
        }
    }

    public class ItemLabelPrimitive : InfoLabelPrimitive
    {
        readonly TrItemLabel Item;

        public ItemLabelPrimitive(Label3DMaterial material, TrItemLabel item, Color color)
            : base(material, color, Color.Black)
        {
            Item = item;
        }

        public override void Draw(GraphicsDevice graphicsDevice)
        {
            DrawLabel(Item.Location, 0, Item.ItemName);
        }
    }
}
