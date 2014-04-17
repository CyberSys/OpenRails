// COPYRIGHT 2014 by the Open Rails project.
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
//
//
//
// ENHANCEMENT list for Trackviewer      
// Ideas from others
//      Be able to list the issues directly without going through the ORTS logfile
//      Make it XNA independent.
//      Import & export.
//          via JSON? Might be a good way to learn JSON.
//          if we are going to write own routines, then use stringbuilder
//
// Steps to take for each release.
//      Always: 1. Update SVN. 
//              2. look at all to-dos and remove temporary changes. 
//              3. update version. 
//              4. remove debug. 
//              5. Set xml compiler version on, check all xml warnings, and turn it off again.
//              6. run fxcop
//              7. test
//
// Little things
//      Add y to statusbar, but perhaps only for items?
//      
// Looks and usability
//      drawTrains: add y, add direction=angle. Add option to (re-)connect to ORTS. Remove http variant. train replaces mouselocation
//
// Code improvements
//      remove drawTrains?
//      remove dependency on ORTS.Settings. Even though it means a bit of code duplication
//      colors should not be string based, but enum.
//
// MSTS trackviewer features perhaps to take over:
//      different color for switches
//      track width option
//      add slope and height
//      Save and restore? But that is like writing/reading tsection.dat, .tdb, .rdb., and .pat files.
// 
// ORTS specific items to add
//      new signalling TrackCircuitSection number. Cumbersome because of dependence on Simulator.
//      Add milepost and speedpost texture?
//
// Further ideas
//      add crossover?
//
// Performance improvements
//      How can I measure performance. I do not want FPS, but it might help measuring improvement.
//      Instead of creating arcs from lines, create arc textures depending on need
//      Once we use more textures, let draw sort them itself. But this needs that we specify z-depth for all textures
//      Split basicshapes into 'static' and 'mouse-dependent'
//          update the static part only when needed.
//          update the mouse-dependent only when needed.
//      Perhaps even create a texture from tracks, one for current items, and draw these, and only draw hightlights directly
//          This might already save some time related to the inset (because it can re-use the texture if we make it big enough)
//          We can also make it more advanced, to support lots of shifting/zooming without generating a new track texture
//          But might take up memory? 
//
using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Graphics.Color;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;
using Microsoft.Xna.Framework.Net;
using Microsoft.Xna.Framework.Storage;

using System.Windows.Forms;
using System.Drawing;
using System.Reflection;
using GNU.Gettext;
using ORTS.Menu;
using ORTS.Common;
using ORTS.TrackViewer.Drawing;
using ORTS.TrackViewer.UserInterface;
using ORTS.TrackViewer.Editing;

namespace ORTS.TrackViewer
{

    /// <summary>
    /// Delegate that can be called by routines such that we can draw it to the screen
    /// </summary>
    /// <param name="message">Message to draw</param>
    public delegate void MessageDelegate(string message);
    
    /// <summary>
    /// This is the main type for your game
    /// </summary>
    public class TrackViewer : Microsoft.Xna.Framework.Game
    {
        #region Public members
        /// <summary>String showing the version of the program</summary>
        public readonly static string TrackViewerVersion = "2014/04/17";
        /// <summary>Path where the content (like .png files) is stored</summary>
        public string ContentPath { get; private set; }
        /// <summary>Folder where MSTS is installed (or at least, where the files needed for tracks, routes and paths are stored)</summary>
        public Folder InstallFolder { get; private set; }
        /// <summary>List of available routes (in the install directory)</summary>
        public Collection<Route> Routes { get; private set; } // Collection because of FxCop
        /// <summary>List of available paths in the current route</summary>
        public Collection<Path> Paths { get; private set; } // Collection because of FxCop
        /// <summary>Route, ie with a path c:\program files\microsoft games\train simulator\routes\usa1  - may be different on different pc's</summary>
        public Route CurrentRoute { get; private set; }
        /// <summary>Route that was used last time</summary>
        private Route DefaultRoute;
        /// <summary>Width of the drawing screen in pixels</summary>
        public int ScreenW { get; private set; }
        /// <summary>Height of the drawing screen in pixels</summary>
        public int ScreenH { get; private set; }
        /// <summary>(Draw)trackDB, that also contains the track data base and the track section data</summary>
        public DrawTrackDB DrawTrackDB { get; private set; }
        /// <summary>Main draw area</summary>
        public DrawArea DrawArea { get; private set; }
        /// <summary>The frame rate</summary>
        public SmoothedData FrameRate { get; private set; }

        /// <summary>The Path editor</summary>
        public PathEditor PathEditor { get; private set; }
        /// <summary>The routines to draw the .pat file</summary>
        public DrawPATfile DrawPATfile { get; private set; }

        #endregion
        #region Private members
        GraphicsDeviceManager graphics;
        SpriteBatch spriteBatch;

        /// <summary>Draw area for the inset</summary>
        ShadowDrawArea drawAreaInset;
        /// <summary>The scale ruler to draw on screen</summary>
        DrawScaleRuler drawScaleRuler;
        /// <summary>For drawing real world longitude and latitude</summary>
        DrawLongitudeLatitude drawLongitudeLatitude;
        /// <summary>The routines to draw trains from runactivy</summary>
        DrawTrains drawTrains;
        /// <summary>The routines to draw the world tiles</summary>
        DrawWorldTiles drawWorldTiles;

        /// <summary>The menu at the top</summary>
        MenuControl menuControl;
        /// <summary>The status bar at the bottom</summary>
        StatusBarControl statusBarControl;

        /// <summary></summary>
        private bool lostFocus;  //when we have lost focus, we do not want to enable shifting with mouse
        /// <summary></summary>
        private int skipDrawAmount; // number of times we want to skip draw because nothing happened
        /// <summary></summary>
        private const int maxSkipDrawAmount = 10;
        #endregion

        /// <summary>
        /// This is the 'catalog' needed for localization of TrackViewer (meaning translating it to different languages)
        /// </summary>
        public static GettextResourceManager catalog = new GettextResourceManager("Contrib");

        /// <summary>
        /// Constructor. This is where it all starts.
        /// </summary>
        public TrackViewer()
        {
            graphics = new GraphicsDeviceManager(this);
            ContentPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath), "Content");
           
            Content.RootDirectory = "Content";
            ScreenH = graphics.PreferredBackBufferHeight;
            ScreenW = graphics.PreferredBackBufferWidth;
            SetAliasing();
            graphics.IsFullScreen = false;
            Window.AllowUserResizing = true;
            Window.ClientSizeChanged += new System.EventHandler(Window_ClientSizeChanged);
        

            //we do not a very fast behaviour, but we do need to get all key presses
            IsFixedTimeStep = true;
            TargetElapsedTime = TimeSpan.FromSeconds(0.05);
            FrameRate = new SmoothedData(0.5f);
            InitLogging();
        }

        /// <summary>
        /// Set aliasing depending on the settings (set in the menu)
        /// </summary>
        public void SetAliasing()
        {
            // Personally, I do not think anti-aliasing looks crisp at all
            graphics.PreferMultiSampling = Properties.Settings.Default.doAntiAliasing;
        }

        void Window_ClientSizeChanged(object sender, EventArgs e)
        {
            ScreenW = Window.ClientBounds.Width;
            ScreenH = Window.ClientBounds.Height;
            if (ScreenW == 0 || ScreenH == 0)
            {   // if something went wrong during fast window switching, let's not continue
                return;
            }
            setSubwindowSizes();
        }

        /// <summary>
        /// Allows the game to perform any initialization it needs to before starting to run.
        /// This is where it can query for any required services and load any non-graphic
        /// relation ontent.  Calling base.Initialize will enumerate through any components
        /// and initialize them as well.
        /// </summary>
        protected override void Initialize()
        {
            TVInputSettings.SetDefaults();

            statusBarControl = new StatusBarControl(this);
            DrawColors.Initialize();
            menuControl = new MenuControl(this);

            Localize(statusBarControl);
            Localize(menuControl);
            
            drawWorldTiles = new DrawWorldTiles();
            drawTrains = new DrawTrains();
            drawScaleRuler = new DrawScaleRuler();
            DrawArea = new DrawArea(drawScaleRuler);
            drawAreaInset = new ShadowDrawArea(null);
            drawAreaInset.StrictChecking = true;
            setSubwindowSizes();
            

            this.IsMouseVisible = true;

            // install folder
            if (String.IsNullOrEmpty(Properties.Settings.Default.installDirectory))
            {
                try
                {
                    Properties.Settings.Default.installDirectory = MSTS.MSTSPath.Base();
                }
                catch {}
            }
            InstallFolder = new Folder("default", Properties.Settings.Default.installDirectory);

            findRoutes(InstallFolder);
            
            
            base.Initialize();
        }

        /// <summary>
        /// Set the sizes of the various subwindows that they can use to draw upon. 
        /// </summary>
        void setSubwindowSizes()
        {
            int insetRatio = 10;
            int menuHeight = menuControl.MenuHeight;
            int statusbarHeight = statusBarControl.StatusbarHeight;
            menuControl.SetScreenSize(ScreenW, menuHeight);
            statusBarControl.SetScreenSize(ScreenW, statusbarHeight, ScreenH);

            DrawArea.SetScreenSize(0, menuHeight, ScreenW, ScreenH - statusbarHeight - menuHeight);
            drawAreaInset.SetScreenSize(ScreenW - ScreenW / insetRatio, menuHeight + 1, ScreenW / insetRatio, ScreenH / insetRatio);
            drawScaleRuler.SetLowerLeftPoint(10, ScreenH - statusbarHeight - 10);
            drawLongitudeLatitude = new DrawLongitudeLatitude(10, menuHeight + 10);

        }
 
        /// <summary>
        /// LoadContent will be called once per game and is the place to load
        /// all of your content.
        /// </summary>
        protected override void LoadContent()
        {
            // Create a new SpriteBatch, which can be used to draw textures.
            spriteBatch = new SpriteBatch(GraphicsDevice);
            BasicShapes.LoadContent(GraphicsDevice, spriteBatch, ContentPath);
            drawAreaInset.LoadContent(GraphicsDevice, spriteBatch, 2, 2, 2);
        }

        /// <summary>
        /// UnloadContent will be called once per game and is the place to unload
        /// all content.
        /// </summary>
        protected override void UnloadContent()
        {
            // Unload any non ContentManager content here
        }

        /// <summary>
        /// Allows the game to run logic such as updating the world,
        /// checking for collisions, gathering input, and playing audio.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Update(GameTime gameTime)
        {
            // Even if not active, still do update from trains
            if (drawTrains.Update(DrawArea)) skipDrawAmount = 0; 

            if (!this.IsActive)
            {
                lostFocus = true;
                return;
            }
 
            TVUserInput.Update();
            if (lostFocus)
            {
                // if the previous call was in inactive mode, we do want TVUserIut to be updated, but we will only
                // act on it the next round.
                lostFocus = false;
                return;
            }

            BasicShapes.Update(GraphicsDevice);
            DrawTrackDB.ClearHighlightOverrides(); // when update is called, we are not searching via menu

            // First check all the buttons that can be kept down.
            if (TVUserInput.IsDown(TVUserCommands.ShiftLeft)) { DrawArea.ShiftLeft(); skipDrawAmount = 0; }
            if (TVUserInput.IsDown(TVUserCommands.ShiftRight)) { DrawArea.ShiftRight(); skipDrawAmount = 0; }
            if (TVUserInput.IsDown(TVUserCommands.ShiftUp)) {DrawArea.ShiftUp(); skipDrawAmount=0;}
            if (TVUserInput.IsDown(TVUserCommands.ShiftDown)) { DrawArea.ShiftDown(); skipDrawAmount = 0; }

            if (TVUserInput.IsDown(TVUserCommands.ZoomIn)) { DrawArea.Zoom(-1); skipDrawAmount = 0; }
            if (TVUserInput.IsDown(TVUserCommands.ZoomOut)) {DrawArea.Zoom(1); skipDrawAmount = 0;}

            if (TVUserInput.Changed)
            {
                skipDrawAmount = 0;
            }

            if (TVUserInput.IsPressed(TVUserCommands.Quit)) this.Quit();

            if (TVUserInput.IsPressed(TVUserCommands.ZoomInSlow)) DrawArea.Zoom(-1);
            if (TVUserInput.IsPressed(TVUserCommands.ZoomOutSlow)) DrawArea.Zoom(1);
            if (TVUserInput.IsPressed(TVUserCommands.ZoomToTile)) DrawArea.ZoomToTile();
            if (TVUserInput.IsPressed(TVUserCommands.ZoomReset))
            {
                DrawArea.ZoomReset(DrawTrackDB);
                drawAreaInset.ZoomReset(DrawTrackDB);  // needed in case window was resized
            }
            
            if (DrawPATfile != null && Properties.Settings.Default.showPATfile)
            {
                if (TVUserInput.IsPressed(TVUserCommands.ExtendPath))     DrawPATfile.ExtendPath();
                if (TVUserInput.IsPressed(TVUserCommands.ExtendPathFull)) DrawPATfile.ExtendPathFull();
                if (TVUserInput.IsPressed(TVUserCommands.ReducePath))     DrawPATfile.ReducePath();
                if (TVUserInput.IsPressed(TVUserCommands.ReducePathFull)) DrawPATfile.ReducePathFull();
                if (TVUserInput.IsDown(TVUserCommands.ShiftToLocation)) DrawArea.ShiftToLocation(DrawPATfile.CurrentLocation);
            }

            if (PathEditor != null && Properties.Settings.Default.showTrainpath)
            {
                if (TVUserInput.IsPressed(TVUserCommands.ExtendPath))     PathEditor.ExtendPath();
                if (TVUserInput.IsPressed(TVUserCommands.ExtendPathFull)) PathEditor.ExtendPathFull();
                if (TVUserInput.IsPressed(TVUserCommands.ReducePath))     PathEditor.ReducePath();
                if (TVUserInput.IsPressed(TVUserCommands.ReducePathFull)) PathEditor.ReducePathFull();
                if (TVUserInput.IsDown(TVUserCommands.ShiftToLocation)) DrawArea.ShiftToLocation(PathEditor.CurrentLocation);

                if (TVUserInput.IsPressed(TVUserCommands.EditorUndo)) PathEditor.Undo();
                if (TVUserInput.IsPressed(TVUserCommands.EditorRedo)) PathEditor.Redo();
                if (TVUserInput.IsMouseXButton1Pressed()) PathEditor.Undo();
                if (TVUserInput.IsMouseXButton2Pressed()) PathEditor.Redo();
            }

            if (PathEditor != null && PathEditor.EditingIsActive)
            {
                if (TVUserInput.IsMouseRightButtonPressed())
                {
                    PathEditor.OnLeftMouseRelease(); // any action done with left mouse is cancelled now
                    PathEditor.PopupContextMenu(TVUserInput.MouseLocationX, TVUserInput.MouseLocationY);
                }

                if (TVUserInput.IsDown(TVUserCommands.EditorTakesMouseClick))
                {
                    if (TVUserInput.IsMouseLeftButtonPressed())
                    {
                        PathEditor.OnLeftMouseClick(TVUserInput.MouseLocationX, TVUserInput.MouseLocationY);
                    }
                    if (TVUserInput.IsMouseLeftButtonDown())
                    {
                        PathEditor.OnLeftMouseMoved(); // to make sure it is reactive enough, don't even care if mouse is really moved
                    }
                    if (TVUserInput.IsMouseLeftButtonReleased())
                    {
                        PathEditor.OnLeftMouseRelease();
                    }
                }

                if (TVUserInput.IsReleased(TVUserCommands.EditorTakesMouseClick))
                {
                    PathEditor.OnLeftMouseCancel();
                }
            }

            if (!TVUserInput.IsDown(TVUserCommands.EditorTakesMouseClick))
            {
                if (TVUserInput.IsMouseMoved() && TVUserInput.IsMouseLeftButtonDown())
                {
                    DrawArea.ShiftArea(TVUserInput.MouseMoveX(), TVUserInput.MouseMoveY());
                }
            }

            if (TVUserInput.IsMouseWheelChanged())
            {
                int mouseWheelChange = TVUserInput.MouseWheelChange();
                if (TVUserInput.IsDown(TVUserCommands.MouseZoomSlow))
                {
                    DrawArea.Zoom(mouseWheelChange > 0 ? -1 : 1);  
                }
                else
                {
                    DrawArea.Zoom(-mouseWheelChange / 40);
                }
            }

           

            DrawArea.Update();
            drawAreaInset.Update();
            drawAreaInset.Follow(DrawArea, 10f);

            if (TVUserInput.IsPressed(TVUserCommands.ToggleShowSignals)) menuControl.MenuToggleShowSignals();
            if (TVUserInput.IsPressed(TVUserCommands.ToggleShowSidings)) menuControl.MenuToggleShowSidings();
            if (TVUserInput.IsPressed(TVUserCommands.ToggleShowSidingNames)) menuControl.MenuToggleShowSidingNames();
            if (TVUserInput.IsPressed(TVUserCommands.ToggleShowPlatforms)) menuControl.MenuToggleShowPlatforms();
            if (TVUserInput.IsPressed(TVUserCommands.ToggleShowPlatformNames)) menuControl.MenuToggleShowPlatformNames();
            if (TVUserInput.IsPressed(TVUserCommands.ToggleShowSpeedLimits)) menuControl.MenuToggleShowSpeedLimits();
            if (TVUserInput.IsPressed(TVUserCommands.ToggleShowMilePosts)) menuControl.MenuToggleShowMilePosts();
            if (TVUserInput.IsPressed(TVUserCommands.ToggleShowTrainpath)) menuControl.MenuToggleShowTrainpath();
            if (TVUserInput.IsPressed(TVUserCommands.ToggleShowPatFile)) menuControl.MenuToggleShowPatFile();


            if (TVUserInput.IsPressed(TVUserCommands.Debug)) runDebug();

            base.Update(gameTime);
            
        }
        
        /// <summary>
        /// Simplified Draw routine that only shows background and a message. 
        /// </summary>
        /// <param name="message">The message you want to show</param>
        private void DrawLoadingMessage(string message)
        {
            // This is not really a game State, because it is not used interactively. In fact, Draw itself is
            // probably not called because the program is doing other things
            BeginDraw();
            GraphicsDevice.Clear(DrawColors.colorsNormal["clearwindow"]);
            spriteBatch.Begin();
            // it is better to have integer locations, otherwise text is difficult to read
            Vector2 messageLocation = new Vector2((float) Math.Round(ScreenW / 2f), (float) Math.Round(ScreenH / 2f));
            BasicShapes.DrawStringLoading(messageLocation, Color.Black, message);

            // we have to redo the, because we now first have to load the characters into textures.
            BasicShapes.Update(GraphicsDevice);
            BasicShapes.DrawStringLoading(messageLocation, Color.Black, message);
            spriteBatch.End();
            EndDraw();
        }

        /// <summary>
        /// This is called when the game should draw itself.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Draw(GameTime gameTime)
        {

            // Even if there is nothing new to draw for main window, we might still need to draw for the shadow textures.
            if (DrawTrackDB != null && Properties.Settings.Default.showInset)
            {
                drawAreaInset.DrawShadowTextures(DrawTrackDB.DrawTracks, DrawColors.colorsNormal["clearwindowinset"]);
            }
            
            // if there is nothing to draw, be done.
            if (--skipDrawAmount > 0)
            {
                return;
            }

            GraphicsDevice.Clear(DrawColors.colorsNormal["clearwindow"]);
            if (DrawTrackDB == null) return;

            spriteBatch.Begin();
            drawWorldTiles.Draw(DrawArea);
            DrawArea.DrawTileGrid();
            
            DrawTrackDB.DrawRoads(DrawArea);
            DrawTrackDB.DrawTracks(DrawArea);
            DrawTrackDB.DrawJunctionAndEndNodes(DrawArea);
            DrawTrackDB.DrawTrackItems(DrawArea);
            DrawTrackDB.DrawRoadTrackItems(DrawArea);
            DrawTrackDB.DrawHighlights(DrawArea, true);

            if (Properties.Settings.Default.showInset)
            {
                drawAreaInset.DrawBackground(DrawColors.colorsNormal["clearwindowinset"]);
                //drawTrackDB.DrawTracks(drawAreaInset); //replaced by next line
                drawAreaInset.DrawShadowedTextures(); 
                DrawTrackDB.DrawHighlights(drawAreaInset, false);
                drawAreaInset.DrawBorder(Color.Red, DrawArea);
                drawAreaInset.DrawBorder(Color.Black);
            }

            if (DrawPATfile != null && Properties.Settings.Default.showPATfile) DrawPATfile.Draw(DrawArea);
            if (PathEditor != null && Properties.Settings.Default.showTrainpath) PathEditor.Draw(DrawArea);

            CalculateFPS(gameTime);
            
            statusBarControl.Update(this, DrawArea.MouseLocation);

            drawScaleRuler.Draw();
            drawLongitudeLatitude.Draw(DrawArea.MouseLocation);

            drawTrains.Draw(DrawArea);

            spriteBatch.End();

            base.Draw(gameTime);
            skipDrawAmount = maxSkipDrawAmount;
        }

 
        void CalculateFPS(GameTime gameTime)
        {
            float elapsedRealTime = (float)gameTime.ElapsedRealTime.TotalSeconds;
            FrameRate.Update(elapsedRealTime, 1f / elapsedRealTime);
        }

        /// <summary>
        /// Ask the user if we really want to quit or not, and if yes, well, quit.
        /// </summary>
        public void Quit()
        {
            string message = String.Empty;
            if (PathEditor!=null && PathEditor.HasModifiedPath)  {
                message = catalog.GetString("The path you are working on has un-saved changes.\n");
            }
            message += catalog.GetString("Do you really want to Quit?");

            if (MessageBox.Show(message, "Question", MessageBoxButtons.YesNo, System.Windows.Forms.MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                this.Exit();
            }
        }
 
        /// <summary>
        /// Open up a dialog so the user can select the install directory 
        /// (which should contain a sub-directory called ROUTES).
        /// </summary>
        public void SelectInstallFolder ()
        {
            if (!CanDiscardModifiedPath()) return;
            string folderPath = "";

            FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog();
            if (InstallFolder != null)
            {
                folderBrowserDialog.SelectedPath = InstallFolder.Path;
            }
            folderBrowserDialog.ShowNewFolderButton = false;
            DialogResult dialogResult = folderBrowserDialog.ShowDialog();

            if (dialogResult == DialogResult.OK)
            {
                folderPath = folderBrowserDialog.SelectedPath;
            }

            if (!String.IsNullOrEmpty(folderPath))
            {

                Folder newInstallFolder = new Folder("installFolder", folderPath);
                bool foundroutes = findRoutes(newInstallFolder);
                if (!foundroutes)
                {
                    MessageBox.Show(catalog.GetString("Directory is not a valid install directory.\nThe install directory needs to contain ROUTES, GLOBAL, ..."));
                    return;
                }

                InstallFolder = newInstallFolder;

                // make sure the current route is disabled,
                CurrentRoute = null;
                DrawTrackDB = null;
                PathEditor = null;

                Properties.Settings.Default.installDirectory = folderPath;
                Properties.Settings.Default.Save();         
            }
        }

        /// <summary>
        /// Find the available routes, and if possible load the first one.
        /// </summary>
        /// <returns>True if the route loading was successfull</returns>
        private bool findRoutes(Folder newInstallFolder)
        {
            if (newInstallFolder == null) return false;
            List<Route> newRoutes = Route.GetRoutes(newInstallFolder).OrderBy(r => r.ToString()).ToList();

            if (newRoutes.Count > 0)
            {
                // set default route
                DefaultRoute = newRoutes[0];
                foreach (Route tryRoute in newRoutes)
                {
                    string dirName = tryRoute.Path.Split('\\').Last();
                    if (dirName == Properties.Settings.Default.defaultRoute)
                    {
                        DefaultRoute = tryRoute;
                    }
                }
                //setRoute(defaultRoute);

                Routes = new Collection<Route>(newRoutes);
                menuControl.PopulateRoutes();
                return true;
            }
            else
            {
                return false;
            }            
        }

        /// <summary>
        /// Load the default route. This would be either the route used last time, the current route, or else the first available route.
        /// </summary>
        public void SetDefaultRoute()
        {
            SetRoute(DefaultRoute);
        }

        /// <summary>
        /// Set and load a new route
        /// </summary>
        /// <param name="newRoute">The route to load, containing amongst other the directory name of the route</param>
        public void SetRoute(Route newRoute)
        {
            if (newRoute == null) return;
            if (!CanDiscardModifiedPath()) return;

            DrawLoadingMessage(catalog.GetString("Loading route..."));

            try
            {
                MessageDelegate messageHandler = new MessageDelegate(DrawLoadingMessage);
                DrawTrackDB = new DrawTrackDB(newRoute.Path, messageHandler);
                CurrentRoute = newRoute;

                Properties.Settings.Default.defaultRoute = CurrentRoute.Path.Split('\\').Last();
                if (Properties.Settings.Default.zoomRoutePath != CurrentRoute.Path)
                {
                    Properties.Settings.Default.zoomScale = -1; // To disable the use of zoom reset
                }
                Properties.Settings.Default.Save();
                DrawArea.ZoomReset(DrawTrackDB);
                drawAreaInset.ZoomReset(DrawTrackDB);
                SetTitle();
            }
            catch
            {
                MessageBox.Show(catalog.GetString("Route cannot be loaded. Sorry"));
            }

            if (CurrentRoute == null) return;

            PathEditor = null;
            try
            {
                findPaths();
            }
            catch { }

            try
            {
                drawWorldTiles.SetRoute(CurrentRoute.Path);
            }
            catch { }

            menuControl.PopulatePlatforms();
            menuControl.PopulateSidings();
        }

        /// <summary>
        /// Set the title of the window itself
        /// </summary>
        void SetTitle()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            AssemblyTitleAttribute assemblyTitle = assembly.GetCustomAttributes(typeof(AssemblyTitleAttribute), false)[0] as AssemblyTitleAttribute;
            Window.Title = assemblyTitle.Title + ": " + DrawTrackDB.RouteName;
        }

        /// <summary>
        /// Find the paths (.pat files) belonging to the current route, and update the menu
        /// </summary>
        private void findPaths()
        {
            List<Path> newPaths = Path.GetPaths(CurrentRoute).OrderBy(r => r.Name).ToList();
            Paths = new Collection<Path>(newPaths);
            menuControl.PopulatePaths();
            SetPath(null);   
        }

        /// <summary>
        /// Once a path has been selected, do the necessary loading.
        /// </summary>
        /// <param name="path">Path (with FilePath) that has to be loaded</param>
        internal void SetPath(Path path)
        {
            if (!CanDiscardModifiedPath()) return;

            if (path == null)
            {
                DrawPATfile = null;
                PathEditor = null;
            }
            else
            {
                DrawLoadingMessage(catalog.GetString("Loading .pat file ..."));
                DrawPATfile = new DrawPATfile(path);

                DrawLoadingMessage(catalog.GetString("Processing .pat file ..."));
                PathEditor = new PathEditor(DrawTrackDB, path);
                DrawLoadingMessage(" ...");
            }   
        }

        internal void NewPath()
        {
            if (!CanDiscardModifiedPath()) return;
            string pathsDirectory = System.IO.Path.Combine(CurrentRoute.Path, "PATHS");
            PathEditor = new PathEditor(DrawTrackDB, pathsDirectory);
            DrawPATfile = null;
        }

        /// <summary>
        /// If the path has been modified, ask the user if he really wants to discard it
        /// </summary>
        /// <returns>false if there is a modified path that the user does not want to discard.</returns>
        bool CanDiscardModifiedPath()
        {
            if (PathEditor == null) return true;
            if (!PathEditor.HasModifiedPath) return true;
            DialogResult dialogResult = MessageBox.Show(
                        "Path has been modified. Loading a new path will discard changes.\n" +
                        "Do you want to continue?", 
                        "Trackviewer Path Editor", MessageBoxButtons.OKCancel, 
                        System.Windows.Forms.MessageBoxIcon.Question);
            return (dialogResult == DialogResult.OK);
        }

        /// <summary>
        /// Find a track node, center around it and highlight it
        /// </summary>
        /// <param name="trackNumberIndex">Index of the track node</param>
        public void CenterAroundTrackNode(int trackNumberIndex)
        {
            CenterAround(DrawTrackDB.TrackNodeHighlightOverride(trackNumberIndex));
        }

        /// <summary>
        /// Find a Road track node, center around it and highlight it
        /// </summary>
        /// <param name="trackNumberIndex">Index of the track node</param>
        public void CenterAroundTrackNodeRoad(int trackNumberIndex)
        {
            CenterAround(DrawTrackDB.TrackNodeHighlightOverrideRoad(trackNumberIndex));
        }

        /// <summary>
        /// Find a trackItem and center around it and highlight it
        /// </summary>
        /// <param name="trackItemIndex">Index of the track item</param>
        public void CenterAroundTrackItem(int trackItemIndex)
        {
            WorldLocation itemLocation = DrawTrackDB.TrackItemHighlightOverride(trackItemIndex);
            if (itemLocation == null) return;
            CenterAround(itemLocation);
        }

        /// <summary>
        /// Find a road trackItem and center around it and highlight it
        /// </summary>
        /// <param name="trackItemIndex">Index of the track item</param>
        public void CenterAroundTrackItemRoad(int trackItemIndex)
        {
            WorldLocation itemLocation = DrawTrackDB.TrackItemHighlightOverrideRoad(trackItemIndex);
            if (itemLocation == null) return;
            CenterAround(itemLocation);
        }

        /// <summary>
        /// Center around a certain world-location. In particular, outside the normal Draw/Update loop. So it does a draw itself
        /// To be used from additional windows (like search).
        /// </summary>
        /// <param name="centerLocation">Location to center the view window around</param>
        public void CenterAround(WorldLocation centerLocation)
        {
            if (centerLocation == null) return;

            DrawArea.ShiftToLocation(centerLocation);
            DrawArea.MouseLocation = centerLocation;
            drawAreaInset.Follow(DrawArea, 10f);
            BeginDraw();
            skipDrawAmount = 0; // make sure the draw is really done.
            Draw(new GameTime());
            EndDraw();

        }

        void runDebug()
        {
            //Properties.Settings.Default.statusShowFPS = false;
            //SetDefaultRoute();
            //SetPath(Paths[0]);
            //DrawArea.ZoomToTile();
            //DrawArea.Zoom(-15);
            //CenterAroundTrackNode(31);
            //NewPath();
            //drawArea.ShiftToLocation(pathEditor.CurrentLocation);
            ////drawArea.ShiftToLocation(pathEditor.trainpath.FirstNode.location);

            PathEditor.EditingIsActive = true;
            //pathEditor.ExtendPathFull();
            ////Exit();
        }

        /// <summary>
        /// Routine to localize (make languague-dependent) a WPF/framework element, like a menu.
        /// </summary>
        /// <param name="element">The element that is checked for localizable parameters</param>
        public static void Localize(System.Windows.FrameworkElement element)
        {
            foreach (var child in System.Windows.LogicalTreeHelper.GetChildren(element))
            {
                System.Windows.FrameworkElement childAsElement = child as System.Windows.FrameworkElement;
                if (childAsElement != null)
                {
                    Localize(childAsElement);
                }
            }

            var objType = element.GetType();
            PropertyInfo property;
            string[] propertyTags = { "Content", "Header", "Text", "Title", "ToolTip" };

            foreach (var tag in propertyTags)
            {
                property = objType.GetProperty(tag);
                if (property != null && property.CanRead && property.CanWrite && property.GetValue(element, null) is String)
                    property.SetValue(element, catalog.GetString(property.GetValue(element, null) as string), null);
            }
        }

        static void InitLogging()
        {   // debug only
            //string logFileName = "C:\\tvlog.txt";
            //System.IO.File.Delete(logFileName);
            //Console.SetOut(new FileTeeLogger(logFileName, Console.Out));
            //// Make Console.Error go to the new Console.Out.
            //Console.SetError(Console.Out);
            //Console.WriteLine("started logging");
        }
    }
}

/*
 * Layer depth of various items (1.0 is back, 0.0 is front)
 *      tiles               lines
 *      grid                lines
 *      track               lines (but perhaps later arcs as well)
 *      track high          idem
 *      track hot           idem
 *      inset background    lines
 *      inset track         lines
 *      inset track high    lines
 *      inset border (or perhaps same as inset track high) lines
 *      items               various textures, and especially here we want to be able to sort.
 *                          Possible alternative is to use a big texture, and use only part of it.
 *      items high          idem
 *      path                lines, textures
 *      ruler               lines
 *      text                text
*/