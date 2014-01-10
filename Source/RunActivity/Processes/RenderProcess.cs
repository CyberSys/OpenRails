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

using System;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ORTS.Processes
{
    [CallOnThread("Render")]
    public class RenderProcess
    {
        public const int ShadowMapCountMaximum = 4;
        public const int ShadowMapMipCount = 1;

        public Point DisplaySize { get; private set; }
        public GraphicsDevice GraphicsDevice { get { return Game.GraphicsDevice; } }
        public bool IsActive { get { return Game.IsActive; } }
        public Viewer3D Viewer { get { return Game.State is GameStateViewer3D ? (Game.State as GameStateViewer3D).Viewer : null; } }

        public Profiler Profiler { get; private set; }
        public Game Game { get; private set; }

        public Vector2 WindowSize { get; private set; }

        public GraphicsDeviceManager GraphicsDeviceManager { get; private set; }

        RenderFrame CurrentFrame;   // a frame contains a list of primitives to draw at a specified time
        RenderFrame NextFrame;      // we prepare the next frame in the background while the current one is rendering,

        public bool IsMouseVisible { get; set; }  // handles cross thread issues by signalling RenderProcess of a change

        // Diagnostic information
        public SmoothedData FrameRate { get; private set; }
        public SmoothedDataWithPercentiles FrameTime { get; private set; }
        public int[] PrimitiveCount { get; private set; }
        public int[] PrimitivePerFrame { get; private set; }
        public int[] ShadowPrimitiveCount { get; private set; }
        public int[] ShadowPrimitivePerFrame { get; private set; }

        // Dynamic shadow map setup.
        public static int ShadowMapCount = -1; // number of shadow maps
        public static int[] ShadowMapDistance; // distance of shadow map center from camera
        public static int[] ShadowMapDiameter; // diameter of shadow map
        public static float[] ShadowMapLimit; // diameter of shadow map far edge from camera

        internal RenderProcess(Game game)
        {
            Profiler = new Profiler("Render");
            Game = game;
            Profiler.SetThread();

            Game.Window.Title = "Open Rails";
            GraphicsDeviceManager = new GraphicsDeviceManager(game);

            var windowSizeParts = Game.Settings.WindowSize.Split(new[] { 'x' }, 2);
            WindowSize = new Vector2(Convert.ToInt32(windowSizeParts[0]), Convert.ToInt32(windowSizeParts[1]));

            FrameRate = new SmoothedData();
            FrameTime = new SmoothedDataWithPercentiles();
            PrimitiveCount = new int[(int)RenderPrimitiveSequence.Sentinel];
            PrimitivePerFrame = new int[(int)RenderPrimitiveSequence.Sentinel];

            // Run the game initially at 10FPS fixed-time-step. Do not change this! It affects the loading performance.
            Game.IsFixedTimeStep = true;
            Game.TargetElapsedTime = TimeSpan.FromMilliseconds(100);
            Game.InactiveSleepTime = TimeSpan.FromMilliseconds(100);

            // Set up the rest of the graphics according to the settings.
            GraphicsDeviceManager.SynchronizeWithVerticalRetrace = Game.Settings.VerticalSync;
            GraphicsDeviceManager.PreferredBackBufferWidth = (int)WindowSize.X;
            GraphicsDeviceManager.PreferredBackBufferHeight = (int)WindowSize.Y;
            GraphicsDeviceManager.IsFullScreen = false;
            GraphicsDeviceManager.PreferMultiSampling = true;
            GraphicsDeviceManager.PreparingDeviceSettings += new EventHandler<PreparingDeviceSettingsEventArgs>(GDM_PreparingDeviceSettings);

            if (Game.Settings.FullScreen)
                ToggleFullScreen();
        }

        void GDM_PreparingDeviceSettings(object sender, PreparingDeviceSettingsEventArgs e)
        {
            // This enables NVIDIA PerfHud to be run on Open Rails.
            foreach (var adapter in GraphicsAdapter.Adapters)
            {
                if (adapter.Description.Contains("PerfHUD"))
                {
                    e.GraphicsDeviceInformation.Adapter = adapter;
                    e.GraphicsDeviceInformation.DeviceType = DeviceType.Reference;
                    break;
                }
            }

            // This stops ResolveBackBuffer() clearing the back buffer.
            e.GraphicsDeviceInformation.PresentationParameters.RenderTargetUsage = RenderTargetUsage.PreserveContents;
            e.GraphicsDeviceInformation.PresentationParameters.AutoDepthStencilFormat = DepthFormat.Depth24Stencil8;
        }

        internal void Start()
        {
            DisplaySize = new Point(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);

            if (Game.Settings.ShaderModel == 0)
                Game.Settings.ShaderModel = GraphicsDevice.GraphicsDeviceCapabilities.PixelShaderVersion.Major;
            else if (Game.Settings.ShaderModel < 2)
                Game.Settings.ShaderModel = 2;
            else if (Game.Settings.ShaderModel > 3)
                Game.Settings.ShaderModel = 3;

            if (Game.Settings.ShadowMapDistance == 0)
                Game.Settings.ShadowMapDistance = Game.Settings.ViewingDistance / 2;

            ShadowMapCount = Game.Settings.ShadowMapCount;
            if (!Game.Settings.DynamicShadows)
                ShadowMapCount = 0;
            else if ((ShadowMapCount > 1) && (Game.Settings.ShaderModel < 3))
                ShadowMapCount = 1;
            else if (ShadowMapCount < 0)
                ShadowMapCount = 0;
            else if (ShadowMapCount > ShadowMapCountMaximum)
                ShadowMapCount = ShadowMapCountMaximum;
            if (ShadowMapCount < 1)
                Game.Settings.DynamicShadows = false;

            ShadowMapDistance = new int[ShadowMapCount];
            ShadowMapDiameter = new int[ShadowMapCount];
            ShadowMapLimit = new float[ShadowMapCount];

            ShadowPrimitiveCount = new int[ShadowMapCount];
            ShadowPrimitivePerFrame = new int[ShadowMapCount];

            InitializeShadowMapLocations();

            CurrentFrame = new RenderFrame(Game);
            NextFrame = new RenderFrame(Game);
        }

        void InitializeShadowMapLocations()
        {
            var ratio = (float)DisplaySize.X / DisplaySize.Y;
            var fov = MathHelper.ToRadians(Game.Settings.ViewingFOV);
            var n = (float)0.5;
            var f = (float)Game.Settings.ShadowMapDistance;
            if (f == 0)
                f = Game.Settings.ViewingDistance / 2;

            var m = (float)ShadowMapCount;
            var LastC = n;
            for (var shadowMapIndex = 0; shadowMapIndex < ShadowMapCount; shadowMapIndex++)
            {
                //     Clog  = split distance i using logarithmic splitting
                //         i
                // Cuniform  = split distance i using uniform splitting
                //         i
                //         n = near view plane
                //         f = far view plane
                //         m = number of splits
                //
                //                   i/m
                //     Clog  = n(f/n)
                //         i
                // Cuniform  = n+(f-n)i/m
                //         i

                // Calculate the two Cs and average them to get a good balance.
                var i = (float)(shadowMapIndex + 1);
                var Clog = n * (float)Math.Pow(f / n, i / m);
                var Cuniform = n + (f - n) * i / m;
                var C = (3 * Clog + Cuniform) / 4;

                // This shadow map goes from LastC to C; calculate the correct center and diameter for the sphere from the view frustum.
                var height1 = (float)Math.Tan(fov / 2) * LastC;
                var height2 = (float)Math.Tan(fov / 2) * C;
                var width1 = height1 * ratio;
                var width2 = height2 * ratio;
                var corner1 = new Vector3(height1, width1, LastC);
                var corner2 = new Vector3(height2, width2, C);
                var cornerCenter = (corner1 + corner2) / 2;
                var length = cornerCenter.Length();
                cornerCenter.Normalize();
                var center = length / Vector3.Dot(cornerCenter, Vector3.UnitZ);
                var diameter = 2 * (float)Math.Sqrt(height2 * height2 + width2 * width2 + (C - center) * (C - center));

                ShadowMapDistance[shadowMapIndex] = (int)center;
                ShadowMapDiameter[shadowMapIndex] = (int)diameter;
                ShadowMapLimit[shadowMapIndex] = C;
                LastC = C;
            }
        }

        internal void Update(GameTime gameTime)
        {
            if (IsMouseVisible != Game.IsMouseVisible)
                Game.IsMouseVisible = IsMouseVisible;

            if (ToggleFullScreenRequested)
            {
                GraphicsDeviceManager.ToggleFullScreen();
                ToggleFullScreenRequested = false;
            }

            if (gameTime.TotalRealTime.TotalSeconds > 0.001)
            {
                Game.UpdaterProcess.WaitTillFinished();

                // Must be done in XNA Game thread.
                UserInput.Update(Game);

                // Swap frames and start the next update (non-threaded updater does the whole update).
                SwapFrames(ref CurrentFrame, ref NextFrame);
                Game.UpdaterProcess.StartUpdate(NextFrame, gameTime.TotalRealTime.TotalSeconds);
            }
        }

        int ProfileFrames = 0;

        internal void BeginDraw()
        {
            if (Game.Settings.Profiling)
                if ((Game.Settings.ProfilingFrameCount > 0 && ++ProfileFrames > Game.Settings.ProfilingFrameCount) || (Game.Settings.ProfilingTime > 0 && Viewer != null && Viewer.RealTime >= Game.Settings.ProfilingTime))
                    Game.PopState();

            if (Game.State == null)
                return;

            Profiler.Start();

            // Sort-of hack to allow the NVIDIA PerfHud to display correctly.
            GraphicsDevice.RenderState.DepthBufferEnable = true;

            CurrentFrame.IsScreenChanged = (DisplaySize.X != GraphicsDevice.Viewport.Width) || (DisplaySize.Y != GraphicsDevice.Viewport.Height);
            if (CurrentFrame.IsScreenChanged)
            {
                DisplaySize = new Point(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
                InitializeShadowMapLocations();
            }

            Game.State.BeginRender(CurrentFrame);
        }

        [ThreadName("Render")]
        internal void Draw()
        {
            if (Debugger.IsAttached)
            {
                CurrentFrame.Draw(Game.GraphicsDevice);
            }
            else
            {
                try
                {
                    CurrentFrame.Draw(Game.GraphicsDevice);
                }
                catch (Exception error)
                {
                    Game.ProcessReportError(error);
                }
            }
        }

        internal void EndDraw()
        {
            if (Game.State == null)
                return;

            Game.State.EndRender(CurrentFrame);

            for (var i = 0; i < (int)RenderPrimitiveSequence.Sentinel; i++)
            {
                PrimitivePerFrame[i] = PrimitiveCount[i];
                PrimitiveCount[i] = 0;
            }
            for (var shadowMapIndex = 0; shadowMapIndex < ShadowMapCount; shadowMapIndex++)
            {
                ShadowPrimitivePerFrame[shadowMapIndex] = ShadowPrimitiveCount[shadowMapIndex];
                ShadowPrimitiveCount[shadowMapIndex] = 0;
            }

            // Sort-of hack to allow the NVIDIA PerfHud to display correctly.
            GraphicsDevice.RenderState.DepthBufferEnable = false;

            Profiler.Stop();
        }

        internal void Stop()
        {
            if (Game.Settings.Profiling)
                Game.Settings.ProfilingFrameCount = ProfileFrames;
        }

        static void SwapFrames(ref RenderFrame frame1, ref RenderFrame frame2)
        {
            RenderFrame temp = frame1;
            frame1 = frame2;
            frame2 = temp;
        }

        bool ToggleFullScreenRequested = false;
        [CallOnThread("Updater")]
        public void ToggleFullScreen()
        {
            var IsFullScreen = !GraphicsDeviceManager.IsFullScreen;
            if (IsFullScreen)
            {
                System.Windows.Forms.Screen screen = System.Windows.Forms.Screen.PrimaryScreen;
                GraphicsDeviceManager.PreferredBackBufferWidth = screen.Bounds.Width;
                GraphicsDeviceManager.PreferredBackBufferHeight = screen.Bounds.Height;
                GraphicsDeviceManager.PreferredBackBufferFormat = SurfaceFormat.Color;
                GraphicsDeviceManager.PreferredDepthStencilFormat = DepthFormat.Depth32;
            }
            else
            {
                GraphicsDeviceManager.PreferredBackBufferWidth = (int)WindowSize.X;
                GraphicsDeviceManager.PreferredBackBufferHeight = (int)WindowSize.Y;
            }
            ToggleFullScreenRequested = true;
        }

        [CallOnThread("Render")]
        [CallOnThread("Updater")]
        public void ComputeFPS(float elapsedRealTime)
        {
            if (elapsedRealTime < 0.001)
                return;

            FrameRate.Update(elapsedRealTime, 1f / elapsedRealTime);
            FrameTime.Update(elapsedRealTime, elapsedRealTime);
        }
    }
}
