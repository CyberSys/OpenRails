﻿/* 3D Viewer

    /// This a 3D viewer.  It connects to a simulator engine, rendering the route content and
    /// rolling stock.
    /// 
    /// When the 3D viewer is constructed its passed a reference to the simulator engine, and a flag
    /// indicating if it should operate in fullscreen mode or windowed mode.   After construction, 
    /// LookAt attaches the viewer a TrainCar in the simulator.
    /// 
 *  
 *  The Viewer class actually represents the screen window on which the camera is rendered.
 * 
 * TODO, add note re abandoning Viewer.Components
 *      - control over render order - ie sorting by material to minimize state changes
 *      - multitasking issues
 *      - multipass techniques, such as shadow mapping
 * 
 * 
 */
/// COPYRIGHT 2009 by the Open Rails project.
/// This code is provided to enable you to contribute improvements to the open rails program.  
/// Use of the code for any other purpose or distribution of the code to anyone else
/// is prohibited without specific written permission from admin@openrails.org.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;
using Microsoft.Xna.Framework.Net;
using Microsoft.Xna.Framework.Storage;
using MSTS;
using System.Threading;
using IrrKlang;
using System.IO;
using Microsoft.Win32;

namespace ORTS
{
    public class Viewer3D
    {
        // User setups
        public int SoundDetailLevel;        // used to select which sound scaleability group to use.
        public int WorldObjectDensity;
        public float ViewingDistance;       // used for culling  
        public UpdaterProcess UpdaterProcess = null;
        public LoaderProcess LoaderProcess;
        public RenderProcess RenderProcess;
        // Access to the XNA Game class
        public GraphicsDeviceManager GDM;  
        public GraphicsDevice GraphicsDevice;
        // Components
        public Simulator Simulator;
        InfoDisplay InfoDisplay;
        SkyDrawer SkyDrawer;
        TerrainDrawer TerrainDrawer;
        SceneryDrawer SceneryDrawer;
        TrainDrawer TrainDrawer;
        public ISoundEngine SoundEngine;  // IrrKlang Sound Device
        // Route Information
        public Tiles Tiles = null;
        public ENVFile ENVFile;
        public TTypeDatFile TTypeDatFile;
        // Cameras
        public Camera Camera;   // Current Camera
        private CabCamera CabCamera;
        private TrackingCamera FrontCamera;
        private TrackingCamera BackCamera;
        private PassengerCamera PassengerCamera;
        private BrakemanCamera BrakemanCamera;
        public TrainCarViewer PlayerLocomotiveViewer = null;  // we are controlling this loco, or null if we aren't controlling any

        /// <summary>
        /// Construct a viewer.  At this time background processes are not running
        /// and the graphics device is not ready to accept content.
        /// </summary>
        /// <param name="simulator"></param>
        public Viewer3D(Simulator simulator)
        {
            Simulator = simulator;

            UserSetup();

            Console.WriteLine();
            SoundEngine = new ISoundEngine();
            SoundEngine.SetListenerPosition(new IrrKlang.Vector3D(0, 0, 0), new IrrKlang.Vector3D(0, 0, 1));
            SoundEngine.SoundVolume = 0;  // while loading
            ReadENVFile();
            TTypeDatFile = new TTypeDatFile(Simulator.RoutePath + @"\TTYPE.DAT");
            Tiles = new Tiles(Simulator.RoutePath + @"\TILES\");
            SetupBackgroundProcesses( );
        }

        public void Run()
        {
            RenderProcess.Run();
        }

        /// <summary>
        /// Setup the game settings provided by the user in the main menu screen.
        /// </summary>
        public void UserSetup()
        {
            // Restore retained settings
            WorldObjectDensity = 10;
            SoundDetailLevel = 5;
            ViewingDistance = 2000;
            RegistryKey RK = Registry.CurrentUser.OpenSubKey(Program.RegistryKey);
            if (RK != null)
            {
                WorldObjectDensity = (int)RK.GetValue("WorldObjectDensity", WorldObjectDensity);
                SoundDetailLevel = (int)RK.GetValue("SoundDetailLevel", SoundDetailLevel);
                ViewingDistance = (int)RK.GetValue("ViewingDistance", (int)ViewingDistance);
            }
            ViewingDistance = Math.Min(Simulator.TRK.ORTRKData.MaxViewingDistance, ViewingDistance);
            Materials.ViewingDistance = ViewingDistance;
        }

        /// <summary>
        /// Called once before the graphics device is started to configure the 
        /// graphics card and XNA game engine.
        /// Executes in the RenderProcess thread.
        /// </summary>
        public void Configure(RenderProcess renderProcess)
        {
            RenderProcess = renderProcess;
            renderProcess.Window.Title = "Open Rails";

            GDM = renderProcess.GraphicsDeviceManager;

            renderProcess.Content.RootDirectory = "Content";

            // TODO, this may cause problems with video cards not set up to handle these settings
            // do we need to check device capabilities first?
            GDM.SynchronizeWithVerticalRetrace = true;
            renderProcess.IsFixedTimeStep = false; // you get smoother animation if we pace to video card retrace setting
            renderProcess.TargetElapsedTime = TimeSpan.FromMilliseconds(1); // setting this a value near refresh rate, ie 16ms, causes hickups ( beating against refresh rate )
            GDM.PreferredBackBufferWidth = 1024; // screen.Bounds.Width; // 1680;
            GDM.PreferredBackBufferHeight = 768; // screen.Bounds.Height; // 1050;
            GDM.IsFullScreen = false;
            GDM.PreferMultiSampling = true;
            GDM.PreferredBackBufferFormat = SurfaceFormat.Bgr32;

        }

        /// <summary>
        /// Called once after the graphics device is ready
        /// to load any static graphics content, background 
        /// processes haven't started yet.
        /// Executes in the RenderProcess thread.
        /// </summary>
        public void Initialize(RenderProcess renderProcess)
        {
            GraphicsDevice = renderProcess.GraphicsDevice;
            ISound ambientSound = SoundEngine.Play2D(Simulator.BasePath + @"\SOUND\gen_urb1.wav", true);  // TODO temp code
            ambientSound.Volume = 0.2f;

            InfoDisplay = new InfoDisplay(this);
            SkyDrawer = new SkyDrawer(this);
            TerrainDrawer = new TerrainDrawer(this);
            SceneryDrawer = new SceneryDrawer(this);
            TrainDrawer = new TrainDrawer(this);

            PlayerLocomotiveViewer =  GetPlayerLocomotiveViewer();

            // Set up cameras
            CabCamera = new CabCamera(this);
            FrontCamera = new TrackingCamera(this, Tether.ToFront);
            BackCamera = new TrackingCamera(this, Tether.ToRear);
            PassengerCamera = new PassengerCamera(this);
            BrakemanCamera = new BrakemanCamera(this);

            FrontCamera.Activate();
        }

        /// <summary>
        /// Called 10 times per second when its safe to read volatile data
        /// from the simulator and viewer classes in preparation
        /// for the Load call.  Copy data to local storage for use 
        /// in the next load call.
        /// Executes in the UpdaterProcess thread.
        /// </summary>
        public void LoadPrep()
        {
            TerrainDrawer.LoadPrep();
            SceneryDrawer.LoadPrep();
            TrainDrawer.LoadPrep();
        }

        /// <summary>
        /// Called 10 times a second to load graphics content
        /// that comes and goes as the player and trains move.
        /// Called from background LoaderProcess Thread
        /// Do not access volatile data from the simulator 
        /// and viewer classes during the Load call ( see
        /// LoadPrep() )
        /// Executes in the LoaderProcess thread.
        /// </summary>
        public void Load( RenderProcess renderProcess )
        {
            TerrainDrawer.Load(renderProcess);
            SceneryDrawer.Load(renderProcess);
            TrainDrawer.Load(renderProcess);
        }

        /// <summary>
        /// Called whenever a key or mouse buttin is pressed for handling user input
        /// elapsedTime represents the the time since the last call to HandleUserInput
        /// Examine the static class UserInput for mouse and keyboard status
        /// Executes in the UpdaterProcess thread.
        /// </summary>
        public void HandleUserInput( ElapsedTime elapsedTime )
        {
            Camera.HandleUserInput( elapsedTime );

            if( PlayerLocomotiveViewer != null )
                PlayerLocomotiveViewer.HandleUserInput( elapsedTime);

            InfoDisplay.HandleUserInput(elapsedTime);

            // Check for game control keys
            if (UserInput.IsKeyDown(Keys.Escape)) {  Stop(); return; }
            if (UserInput.IsAltPressed(Keys.Enter)) { ToggleFullscreen(); }

            // Pause
            if (UserInput.IsPressed(Keys.Pause) && UserInput.IsShiftDown()) 
            { 
                Simulator.Paused = !Simulator.Paused; 
            } 

            // Change view point - cab, passenger, outside, etc
            if (UserInput.IsPressed(Keys.D1)) CabCamera.Activate();
            if (UserInput.IsPressed(Keys.D2)) FrontCamera.Activate();
            if (UserInput.IsPressed(Keys.D3)) BackCamera.Activate();
            if (UserInput.IsPressed(Keys.D6)) BrakemanCamera.Activate();
            if (UserInput.IsPressed(Keys.D5)) PassengerCamera.Activate();
            if (UserInput.IsPressed(Keys.D4)
              || UserInput.IsPressed(Keys.D7)
              || UserInput.IsPressed(Keys.D8)) (new Camera(this, Camera)).Activate();

            if (UserInput.IsPressed(Keys.G) && !UserInput.IsShiftDown()) Simulator.SwitchTrackAhead();
            if (UserInput.IsPressed(Keys.G) && UserInput.IsShiftDown()) Simulator.SwitchTrackBehind();

            // Uncoupling?
            if (!Simulator.Paused && UserInput.IsKeyDown(Keys.U))
            {
                RenderProcess.IsMouseVisible = true;
                if (UserInput.MouseState.LeftButton == ButtonState.Pressed)
                    TryUncoupleAt( UserInput.MouseState.X, UserInput.MouseState.Y);
            }
            else
            {
                RenderProcess.IsMouseVisible = false;
            }

        }

        /// <summary>
        /// Called every frame to update animations and load the frame contents .
        /// Note:  this doesn't actually draw on the screen surface, but 
        /// instead prepares a list of drawing primitives that will be rendered
        /// later in RenderFrame.Draw() by the RenderProcess thread.
        /// elapsedTime represents the the time since the last call to PrepareFrame
        /// Executes in the UpdaterProcess thread.
        /// </summary>
        public void PrepareFrame(RenderFrame frame, ElapsedTime elapsedTime )
        {
            // Mute sound when paused
            if (Simulator.Paused)
                SoundEngine.SoundVolume = 0;
            else
                SoundEngine.SoundVolume = 1;

            if (ScreenHasChanged())
                NotifyCamerasOfScreenChange();
            Camera.PrepareFrame(frame, elapsedTime);
            SkyDrawer.PrepareFrame(frame, elapsedTime);
            TerrainDrawer.PrepareFrame(frame, elapsedTime);
            SceneryDrawer.PrepareFrame(frame, elapsedTime);
            TrainDrawer.PrepareFrame(frame, elapsedTime);
            InfoDisplay.PrepareFrame(frame, elapsedTime);
        }


        /// <summary>
        /// Unload all graphical content and restore memory
        /// Executes in the RenderProcess thread.
        /// </summary>
        public void Unload(RenderProcess renderProcess)
        {
            SoundEngine.StopAllSounds();
        }

        public void Stop()
        {
            RenderProcess.Stop();
        }

        /// <summary>
        /// Determine the correct environment files for this activity and read it in.
        /// </summary>
        private void ReadENVFile()
        {
            string envFileName = Simulator.TRK.Tr_RouteFile.Environment.ENVFileName(
                                        Simulator.Activity.Tr_Activity.Tr_Activity_Header.Season,
                                        Simulator.Activity.Tr_Activity.Tr_Activity_Header.Weather);

            ENVFile = new ENVFile(Simulator.RoutePath + @"\ENVFILES\" + envFileName);
        }

        /// <summary>
        /// Adjust all projection matrixes and buffer sizes
        /// </summary>
        private void ToggleFullscreen()
        {
            bool IsFullScreen = !GDM.IsFullScreen;
            if (IsFullScreen)
            {
                System.Windows.Forms.Screen screen = System.Windows.Forms.Screen.PrimaryScreen;
                GDM.PreferredBackBufferWidth = screen.Bounds.Width; // 1680;
                GDM.PreferredBackBufferHeight = screen.Bounds.Height;
            }
            else
            {
                GDM.PreferredBackBufferWidth = 1024;
                GDM.PreferredBackBufferHeight = 768;
            }
            RenderProcess.ToggleFullScreen();
        }

        /// <summary>
        /// Return true if the screen has changed dimensions
        /// </summary>
        /// <returns></returns>
        private bool ScreenHasChanged()
        {
            if (RenderProcess.GraphicsDeviceManager.IsFullScreen != isFullScreen)
            {
                isFullScreen = RenderProcess.GraphicsDeviceManager.IsFullScreen;
                return true;
            }
            return false;
        }
        private bool isFullScreen = false;

        /// <summary>
        /// Called when we detect that the screen size has changed
        /// </summary>
        private void NotifyCamerasOfScreenChange()
        {
            // since each camera has its own projection matrix, they all have to be notified
            Camera.ScreenChanged();
            CabCamera.ScreenChanged();
            FrontCamera.ScreenChanged();
            BackCamera.ScreenChanged();
            PassengerCamera.ScreenChanged();
            BrakemanCamera.ScreenChanged();
        }

        private TrainCarViewer GetPlayerLocomotiveViewer()
        {
            return TrainDrawer.GetViewer(Simulator.PlayerLocomotive);
        }

        /// <summary>
        /// The user has left clicked with U pressed.   
        /// If the mouse was over a coupler, then uncouple the car.
        /// </summary>
        /// <param name="mouseX"></param>
        /// <param name="mouseY"></param>
        private void TryUncoupleAt(int mouseX, int mouseY)
        {
            Vector3 nearsource = new Vector3((float)mouseX, (float)mouseY, 0f);
            Vector3 farsource = new Vector3((float)mouseX, (float)mouseY, 1f);
            Matrix world = Matrix.CreateTranslation(0, 0, 0);
            Vector3 nearPoint = GraphicsDevice.Viewport.Unproject(nearsource, Camera.XNAProjection, Camera.XNAView, world);
            Vector3 farPoint = GraphicsDevice.Viewport.Unproject(farsource, Camera.XNAProjection, Camera.XNAView, world);

            // Create a ray from the near clip plane to the far clip plane.
            Vector3 direction = farPoint - nearPoint;
            direction.Normalize();
            Ray pickRay = new Ray(nearPoint, direction);

            // check each car
            TDBTraveller traveller = new TDBTraveller(Simulator.PlayerTrain.FrontTDBTraveller);
            traveller.ReverseDirection();
            foreach (TrainCar car in Simulator.PlayerTrain.Cars)
            {
                traveller.Move(car.Length);

                Vector3 xnaCenter = Camera.XNALocation(traveller.WorldLocation);
                float radius = 2f;  // 2 meter click range
                BoundingSphere boundingSphere = new BoundingSphere(xnaCenter, radius);

                if (null != pickRay.Intersects(boundingSphere))
                {
                    Simulator.UncoupleBehind(car);
                    break;
                }
            }
        }

        public void SetupBackgroundProcesses()
        {
            int processors =  System.Environment.ProcessorCount;
            RenderProcess = new RenderProcess( this);   // the order is important, since one process depends on the next
            LoaderProcess = new LoaderProcess( this);
            if (processors > 1)
                UpdaterProcess = new UpdaterProcess( this);
        }


    } // Viewer3D
} // namespace ORTS
