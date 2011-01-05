﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Data;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.IO;
using MSTS;
using Microsoft.Win32;
using System.Text.RegularExpressions;
using System.Reflection;

namespace MenuWPF
{
	/// <summary>
	/// New Windows Presentation Foundation Main Menu for Open Rails
	/// </summary>
	public partial class MainWindow : Window
    {
        #region Members

        public const string FolderDataFileName = "folder.dat";
        private BackgroundWorker bgWork;
        private Dictionary<EngineInfo, List<string>> EnginesWithConsists;

        #region ex-Program class
        const string RunActivityProgram = "runactivity.exe";

        public static string Revision;        // ie 078
        public static string Build;           // ie "0.0.3661.19322 Sat 01/09/2010  10:44 AM"
        public static string RegistryKey;     // ie "SOFTWARE\\OpenRails\\ORTS"
        public static string UserDataFolder;  // ie "C:\\Users\\Wayne\\AppData\\Roaming\\ORTS"

        List<string> Consists;
        List<string> Paths;
        #endregion

        string FolderDataFile;
        List<Folder> Folders;
        List<Route> Routes;
        List<Activity> Activities;

        public Folder SelectedFolder { get { return listBoxRoutes.SelectedIndex < 0 ? Folders[0] : Routes[listBoxRoutes.SelectedIndex].Folder; } }
        public Route SelectedRoute { get { return listBoxRoutes.SelectedIndex < 0 ? null : Routes[listBoxRoutes.SelectedIndex]; } }
        public Activity SelectedActivity { get { return listBoxActivities.SelectedIndex < 0 ? null : Activities[listBoxActivities.SelectedIndex]; } set { if (listBoxActivities.SelectedIndex >= 0) Activities[listBoxActivities.SelectedIndex] = value; } }
        
        //public Folder SelectedFolder { get { return Folders[0]; } }
        
        public class Folder
        {
            public readonly string Name;
            public readonly string Path;

            public Folder(string name, string path)
            {
                Name = name;
                Path = path;
            }
        }

        public class Route
        {
            public readonly string Name;
            public readonly string Path;
            public readonly TRKFile TRKFile;
            public readonly Folder Folder;  //added to concatenate with the originary MSTS installation folder

            public Route(string name, string path, TRKFile trkFile, Folder folder)
            {
                Name = name;
                Path = path;
                TRKFile = trkFile;
                Folder = folder;
            }
        }

        public class Activity
        {
            public readonly string Name;
            public readonly string FileName;
            public readonly ACTFile ACTFile;

            public Activity(string name, string fileName, ACTFile actFile)
            {
                Name = name;
                FileName = fileName;
                ACTFile = actFile;
            }
        }

        public class ExploreActivity : Activity
        {
            public readonly string Path;
            public readonly string Consist;
            public readonly int StartHour;
            public readonly int Season;
            public readonly int Weather;

            public ExploreActivity(string path, string consist, int season, int weather, int startHour)
                : base("- Explore Route -", null, null)
            {
                Path = path;
                Consist = consist;
                Season = season;
                Weather = weather;
                StartHour = startHour;
            }

            public ExploreActivity()
                : this("", "", 0, 0, 12)
            {
            }
        }
        #endregion

        #region Constructor
        public MainWindow()
		{
			this.InitializeComponent();
            bgWork = new BackgroundWorker();
            bgWork.DoWork += new DoWorkEventHandler(bgWork_DoWork);
            bgWork.RunWorkerCompleted += new RunWorkerCompletedEventHandler(bgWork_RunWorkerCompleted);
            SetBuildRevision();
            UserDataFolder = System.IO.Path.GetDirectoryName(System.IO.Path.GetDirectoryName(Environment.GetFolderPath(Environment.SpecialFolder.Cookies)));
            UserDataFolder = UserDataFolder.Substring(0, UserDataFolder.LastIndexOf("\\") + 1);
            
            RegistryKey = "SOFTWARE\\OpenRails\\ORTS";
            // Set title to show revision or build info.
            //Content = String.Format(Revision == "000" ? "{0} BUILD {2}" : "{0} V{1}", AppDomain.CurrentDomain.FriendlyName, Revision, Build);
            Assembly exeAssembly = Assembly.GetExecutingAssembly();
            AssemblyProductAttribute prodName = (AssemblyProductAttribute)exeAssembly.GetCustomAttributes(typeof(System.Reflection.AssemblyProductAttribute), false).Single();
            UserDataFolder += prodName.Product;
            FolderDataFile = UserDataFolder + @"\" + FolderDataFileName;
            //Load the folders
            LoadFolders();
            //Loading the routes
            LoadRoutes();

            CleanupPre021();
        }

        
        #endregion

        #region Event Handlers
        private void bgWork_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            throw new NotImplementedException();
        }

        private void bgWork_DoWork(object sender, DoWorkEventArgs e)
        {
            throw new NotImplementedException();
        }

        private void winMain_Closing(object sender, CancelEventArgs e)
        {
            
        }

        private void btnStart_Click(object sender, System.Windows.RoutedEventArgs e)
		{
            MainStart();
		}

		private void btnOptions_Click(object sender, System.Windows.RoutedEventArgs e)
		{
            MenuWPF.OptionsWindow winOptions = new MenuWPF.OptionsWindow(RegistryKey);
            winOptions.ShowDialog();
		}

		private void btnQuit_Click(object sender, System.Windows.RoutedEventArgs e)
		{
            Close();
        }

        private void listBoxRoutes_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Paths = FillPaths(Routes[listBoxRoutes.SelectedIndex].Path);
            LoadActivities();
            DisplayRouteDetails();
        }

        private void listBoxActivities_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            //Load activity details
            DisplayActivityDetails();
        }
        #endregion

        #region Methods
        /// <summary>
        /// Old method of Windows Form Application to start the program, 
        /// now included in the main window.
        /// </summary>
        void MainStart()
        {
            try
            {
                string parameter;

                if (SelectedActivity is ExploreActivity)
                {
                    int hour = 10;
                    
                    Regex reg = new Regex("^([0-1][0-9]|[2][0-3]):([0-5][0-9])$"); //Match a string format of HH:MM
                    if (reg.IsMatch(cboStartingTime.Text))
                    {
                        int.TryParse(cboStartingTime.Text.Trim().Substring(0, cboStartingTime.Text.Trim().IndexOf(':')), out hour);
                    }
                    else
                    {
                        MessageBox.Show("Invalid starting time", "Warning", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                        return;
                    }
                    
                    parameter = String.Format("\"{0}\" \"{1}\" {2} {3} {4}", Paths[cboPath.SelectedIndex], Consists[cboConsist.SelectedIndex], hour, cboSeason.SelectedIndex, cboWeather.SelectedIndex);
                }
                else
                    parameter = String.Format("\"{0}\"", SelectedActivity.FileName);

                // find the RunActivity program, normally in the startup path, 
                //  but while debugging it will be in an adjacent directory
                string RunActivityFolder = AppDomain.CurrentDomain.BaseDirectory.ToLower();

                System.Diagnostics.ProcessStartInfo objPSI = new System.Diagnostics.ProcessStartInfo();
                objPSI.FileName = System.IO.Path.Combine(RunActivityFolder, RunActivityProgram);
                objPSI.Arguments = parameter;
                objPSI.WindowStyle = System.Diagnostics.ProcessWindowStyle.Normal; // or Hidden, Maximized or Normal 
                objPSI.WorkingDirectory = RunActivityFolder;

                System.Diagnostics.Process objProcess = System.Diagnostics.Process.Start(objPSI);

                while (objProcess.HasExited == false)
                    System.Threading.Thread.Sleep(100);

                int retVal = objProcess.ExitCode;
                
            }
            catch (Exception error)
            {
                MessageBox.Show(error.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
		/// Set up the global Build and Revision variables
		/// from assembly data and the revision.txt file.
		/// </summary>
		private void SetBuildRevision()
		{
            try
            {
                using (StreamReader f = new StreamReader("Revision.txt"))
                {
                    string line = f.ReadLine();
                    string rev = line.Substring(11);
                    int i = rev.IndexOf('$');
                    Revision = rev.Substring(0, i).Trim();

                    System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
                    Version version = assembly.GetName().Version;

                    Build = version.ToString();
                    Build = Build + " " + f.ReadLine();  // date
                    Build = Build + " " + f.ReadLine(); // time
                }
            }
            catch
            {
                Revision = "";
                System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
                Version version = assembly.GetName().Version;

                Build = version.ToString();
            }
            finally
            {
                this.Title += " BUILD: " + Build;
            }
		}

        //methods imported from the old Menu Windows Forms Application
        void CleanupPre021()
        {
            // Handle cleanup from pre version 0021
            if (null != Registry.CurrentUser.OpenSubKey("SOFTWARE\\ORTS"))
                Registry.CurrentUser.DeleteSubKeyTree("SOFTWARE\\ORTS");

            if (!File.Exists(FolderDataFile))
            {
                // Handle name change that occured at version 0021
                string oldFolderDataFileName = UserDataFolder + @"\..\ORTS\folder.dat";
                try
                {
                    if (File.Exists(oldFolderDataFileName))
                    {
                        File.Copy(oldFolderDataFileName, FolderDataFile);
                        Directory.Delete(System.IO.Path.GetDirectoryName(oldFolderDataFileName), true);
                    }
                }
                catch
                {
                }
            }
        }

        //============================================================================================
        private void LoadFolders()
        {
            Folders = new List<Folder>();

            if (File.Exists(FolderDataFile))
            {
                try
                {
                    using (var inf = new BinaryReader(File.Open(FolderDataFile, FileMode.Open)))
                    {
                        var count = inf.ReadInt32();
                        for (var i = 0; i < count; ++i)
                        {
                            var path = inf.ReadString();
                            var name = inf.ReadString();
                            Folders.Add(new Folder(name, path));
                        }
                    }
                }
                catch (Exception error)
                {
                    MessageBox.Show(error.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }

            if (Folders.Count == 0)
            {
                try
                {
                    Folders.Add(new Folder("- Default -", MSTSPath.Base()));
                }
                catch (Exception)
                {
                    MessageBox.Show("Microsoft Train Simulator doesn't appear to be installed.\nClick on 'Add...' to point Open Rails at your Microsoft Train Simulator folder.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }

            Folders = Folders.OrderBy(f => f.Name).ToList();

            
        }

        //================================================================================
        private void SaveFolders()
        {
            using (BinaryWriter outf = new BinaryWriter(File.Open(FolderDataFile, FileMode.Create)))
            {
                outf.Write(Folders.Count);
                foreach (var folder in Folders)
                {
                    outf.Write(folder.Path);
                    outf.Write(folder.Name);
                }
            }
        }

        //===========================================================================
        private void LoadRoutes()
        {
            Routes = new List<Route>();
            try
            {
                foreach (Folder f in Folders)
                {
                    foreach (string directory in Directory.GetDirectories(SelectedFolder.Path + @"\ROUTES"))
                    {
                        try
                        {
                            TRKFile trkFile = new TRKFile(MSTSPath.GetTRKFileName(directory));
                            Routes.Add(new Route(trkFile.Tr_RouteFile.Name, directory, trkFile, f));
                        }
                        catch
                        {
                        }
                    }
                }
            

                //Routes = Routes.OrderBy(r => r.Folder.Name).OrderBy(r => r.Name).ToList();

                listBoxRoutes.Items.Clear();
                foreach (var route in Routes)
                    listBoxRoutes.Items.Add(route.Folder.Name + "/" + route.Name);

                if (Routes.Count > 0)
                {
                    listBoxRoutes.SelectedIndex = 0;
                    FillConsists();
                }
                else
                    listBoxRoutes.UnselectAll();

                if (Routes.Count == 0)   //for what does this serve ? If no route, no game !! ??
                    LoadActivities();
            }
            catch (Exception error)
            {
                MessageBox.Show(error.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        //===========================================================================
        private void LoadActivities()
        {
            Activities = new List<Activity>();

            if (SelectedRoute != null)
            {
                try
                {
                    Activities.Add(new ExploreActivity());
                    foreach (var file in Directory.GetFiles(SelectedRoute.Path + @"\ACTIVITIES", "*.act"))
                    {
                        if (System.IO.Path.GetFileName(file).StartsWith("ITR_e1_s1_w1_t1", StringComparison.OrdinalIgnoreCase))
                            continue;
                        try
                        {
                            var actFile = new ACTFile(file, true);
                            Activities.Add(new Activity(actFile.Tr_Activity.Tr_Activity_Header.Name, file, actFile));
                        }
                        catch (Exception)
                        {
                        }
                    }
                }
                catch (Exception error)
                {
                    MessageBox.Show(error.ToString(), AppDomain.CurrentDomain.FriendlyName);
                }
            }

            Activities = Activities.OrderBy(a => a.Name).ToList();

            listBoxActivities.Items.Clear();
            foreach (var activity in Activities)
                listBoxActivities.Items.Add(activity.Name);

            if (Activities.Count > 0)
                listBoxActivities.SelectedIndex = 0;
            else
                listBoxActivities.UnselectAll();
        }

        //===========================================================================================
        private void DisplayRouteDetails()
        {
            if (listBoxRoutes.SelectedIndex >= 0)
            {
                FlowDocument flowDoc = new FlowDocument();
                //Generate the FlowDocument from the text parsed from the TRK files
                Paragraph pTitle = new Paragraph();
                pTitle.Inlines.Add(new Bold(new Run(Routes[listBoxRoutes.SelectedIndex].TRKFile.Tr_RouteFile.Name)));
                string[] lines = Routes[listBoxRoutes.SelectedIndex].TRKFile.Tr_RouteFile.Description.Split(new String[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);
                flowDoc.Blocks.Add(pTitle);

                foreach (string line in lines)
                {
                    Paragraph paragraph = new Paragraph();
                    paragraph.Inlines.Add(new Run(line));
                    flowDoc.Blocks.Add(paragraph);
                }
                //#7FFFFFFF
                flowDoc.FontFamily = FontFamily;
                flowDoc.Background = new SolidColorBrush(Color.FromArgb(Convert.ToByte("00", 16), 251, 251, 251));
                docRouteDetail.Document = flowDoc;
                lines = null;
            }
        }

        //================================================================================
        private void DisplayActivityDetails()
        {
            if (listBoxActivities.SelectedIndex > 0)
            {
                //Display activity details
                docActivityDescription.Document.Blocks.Clear();
                string[] lines = Activities[listBoxActivities.SelectedIndex].ACTFile.Tr_Activity.Tr_Activity_Header.Description.Split(new String[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string line in lines)
                {
                    Paragraph p = new Paragraph();
                    p.Inlines.Add(new Run(line));
                    docActivityDescription.Document.Blocks.Add(p);
                }
                lines = null;
                cboStartingTime.Text = Activities[listBoxActivities.SelectedIndex].ACTFile.Tr_Activity.Tr_Activity_Header.StartTime.FormattedStartTime();
                cboStartingTime.IsEnabled = false;

                //Show the activity special fields
                lblDescription.Visibility = Visibility.Visible;
                lblDifficulty.Visibility = Visibility.Visible;
                lblDuration.Visibility = Visibility.Visible;
                labelDifficulty.Visibility = Visibility.Visible;
                labelDuration.Visibility = Visibility.Visible;
                docActivityDescription.Visibility = Visibility.Visible;
                //================================
                labelDuration.Content = Activities[listBoxActivities.SelectedIndex].ACTFile.Tr_Activity.Tr_Activity_Header.Duration.FormattedDurationTime();
                cboSeason.SelectedIndex = (int)Activities[listBoxActivities.SelectedIndex].ACTFile.Tr_Activity.Tr_Activity_Header.Season;
                cboSeason.IsEnabled = false;
                cboWeather.SelectedIndex = (int)Activities[listBoxActivities.SelectedIndex].ACTFile.Tr_Activity.Tr_Activity_Header.Weather;
                cboWeather.IsEnabled = false;
                labelDifficulty.Content = Activities[listBoxActivities.SelectedIndex].ACTFile.Tr_Activity.Tr_Activity_Header.Difficulty.ToString();


                cboPath.SelectedIndex = -1;//Paths.IndexOf(Activities[listBoxActivities.SelectedIndex].ACTFile.Tr_Activity.Tr_Activity_Header.PathID);
                cboPath.IsEnabled = false;
                cboConsist.SelectedIndex = -1;
                cboConsist.IsEnabled = false;
            }
            else
            {
                docActivityDescription.Document.Blocks.Clear();
                cboStartingTime.SelectedIndex = 2;
                cboStartingTime.IsEnabled = true;

                //Hide the activity special fields
                lblDescription.Visibility = Visibility.Hidden;
                lblDifficulty.Visibility = Visibility.Hidden;
                lblDuration.Visibility = Visibility.Hidden;
                labelDifficulty.Visibility = Visibility.Hidden;
                labelDuration.Visibility = Visibility.Hidden;
                docActivityDescription.Visibility = Visibility.Hidden;
                //================================
                cboSeason.SelectedIndex = 1;
                cboSeason.IsEnabled = true;
                cboWeather.SelectedIndex = 0;
                cboWeather.IsEnabled = true;
                cboPath.SelectedIndex = 0;
                cboPath.IsEnabled = true;
                cboConsist.SelectedIndex = 0;
                cboConsist.IsEnabled = true;

            }
        }

        /// <summary>
        /// Method to fill the paths combo box
        /// </summary>
        /// <param name="route">The path of the route to load the paths for</param>
        List<string> FillPaths(string route)
        {
            List<string> paths = new List<string>();
            string[] patfiles = Directory.GetFiles(route + @"\paths");
            cboPath.Items.Clear();
            foreach (string file in patfiles)
            {
                cboPath.Items.Add(System.IO.Path.GetFileName(file));
                paths.Add(file);
                
            }
            patfiles = null;
            return paths;
        }

        /// <summary>
        /// Method to fill the list of engines and consists
        /// </summary>
        private void FillConsists()
        {
            if (this.Dispatcher.CheckAccess())
            {
                string[] confiles = Directory.GetFiles(SelectedFolder.Path + @"\trains\consists");
                EnginesWithConsists = new Dictionary<EngineInfo, List<string>>(new EngineInfoEqualityComparer());
                foreach (string file in confiles)
                {
                    using (StreamReader sr = new StreamReader(file))
                    {
                        string fileContent = sr.ReadToEnd();
                        //Check that the consist file contains an engine
                        if (fileContent.ToLower().Contains("enginedata"))
                        {
                            string couple = fileContent.Substring(fileContent.ToLower().IndexOf("enginedata") + 10);
                            couple = couple.Substring(couple.IndexOf("(") + 1);
                            couple = couple.Substring(0, couple.IndexOf(")")).Trim();
                            string key = "";
                            if (couple.Substring(0, 1) == "\"")
                            {
                                key = couple.Substring(1);
                                key = key.Substring(0, key.IndexOf("\"")).Trim();
                            }
                            else
                            {
                                key = couple.Substring(0, couple.IndexOf(" "));
                            }

                            string engineFolder = couple.Substring(key.Length);
                            engineFolder = engineFolder.Replace("\"", "").Trim();
                            //Check if the engine file exists
                            if (File.Exists(SelectedFolder.Path + @"\trains\trainset\" + engineFolder + "\\" + key + ".eng"))
                            {
                                StreamReader srEngine = new StreamReader(SelectedFolder.Path + @"\trains\trainset\" + engineFolder + "\\" + key + ".eng");
                                string engineContent = srEngine.ReadToEnd();
                                EngineInfo engKey = GetEngineInfo(engineContent);
                                engKey.ID = key;
                                //Check if it contains a Cabview => player driveable engine
                                if (engineContent.ToLower().Contains("cabview") && engineContent.ToLower().IndexOf("cabview") < engineContent.ToLower().IndexOf("description"))
                                {
                                    if (EnginesWithConsists.ContainsKey(engKey))
                                    {
                                        EnginesWithConsists[engKey].Add(System.IO.Path.GetFileName(file).Replace(".con", ""));
                                    }
                                    else
                                    {
                                        EnginesWithConsists.Add(engKey, new List<string>());
                                        EnginesWithConsists[engKey].Add(System.IO.Path.GetFileName(file).Replace(".con", ""));
                                    }
                                }
                                srEngine.Close();
                                srEngine = null;
                                engineContent = null;
                            }
                            else
                            {
                                //Display error message ?
                            }
                            
                        }
                    }
                        
                }
                    //cboConsist.Items.Add(System.IO.Path.GetFileName(file));
                    //consists.Add(file);
                confiles = null;
                //Populate the comboBoxes
            }
            else
            {
                this.Dispatcher.Invoke(new FillConsistsDelegate(FillConsists));
            }
        }

        /// <summary>
        /// Create an EngineInfo object with the data found in the eng file
        /// </summary>
        /// <param name="engContent">The text content of the eng file</param>
        /// <returns>EngineInfo</returns>
        private EngineInfo GetEngineInfo(string engContent)
        {
            EngineInfo eng = new EngineInfo();

            try
            {
                eng.Coupling = (CouplingType)Enum.Parse(typeof(CouplingType), ParseTag("Type", ParseTag("Coupling", engContent)), true);
                eng.Description = ParseTag("Description", engContent);
                eng.FreightAnim = ParseTag("FreightAnim", engContent);
                //eng.ID = ParseTag("Wagon", engContent);
                string len = ParseTag("Size", engContent);
                len = len.Substring(len.LastIndexOf(" ") + 1);
                len = len.Replace("m", "").Replace('.', ',');
                eng.Length = double.Parse(len);

                string ms = ParseTag("Mass", engContent);
                eng.Mass = double.Parse(ms.Substring(0, ms.IndexOf("t")).Replace('.', ','));

                string mcf = ParseTag("MaxContinuousForce", engContent).ToLower();
                if (mcf.Contains("kn")) eng.MaxContinuousForce = double.Parse(mcf.Substring(0, mcf.IndexOf("kn")).Replace('.', ',').Trim());
                else if (mcf.Contains("lbf")) eng.MaxContinuousForce = double.Parse(mcf.Substring(0, mcf.IndexOf("lbf")).Replace('.', ',').Trim());
                
                string mf = ParseTag("MaxForce", engContent).ToLower();
                if (mf.Contains("kn")) eng.MaxForce = double.Parse(mf.Substring(0, mf.IndexOf("kn")).Replace('.', ',').Trim());
                else if (mf.Contains("lbf")) eng.MaxForce = double.Parse(mf.Substring(0, mf.IndexOf("lbf")).Replace('.', ',').Trim());
                
                string mp = ParseTag("MaxPower", engContent).ToLower();
                if (mp.Contains("kw")) eng.MaxPower = double.Parse(mp.Substring(0, mp.IndexOf("kw")).Replace('.', ',').Trim());
                else if (mp.Contains("hp")) eng.MaxPower = double.Parse(mp.Substring(0, mp.IndexOf("hp")).Replace('.', ',').Trim());
                eng.Name = ParseTag("Name", engContent);
                eng.Shape = ParseTag("WagonShape", engContent);
                eng.Type = (EngineType)Enum.Parse(typeof(EngineType), ParseTag("Type", ParseTag("Engine (", engContent.Substring(engContent.IndexOf("Engine") + 6), "Engine(", true)), true);
                
            }
            catch
            {

            }
            return eng;
        }

        /// <summary>
        /// Gets the value between the brackets ( ) of a given tag
        /// </summary>
        /// <param name="tagName">The name of the tag</param>
        /// <param name="fileContent">The text where to search the tag and its value</param>
        /// <returns>Value of the tag</returns>
        private string ParseTag(string tagName, string fileContent)
        {
            string tagValue = "";
            if (fileContent.Contains(tagName))
            {
                tagValue = fileContent.Substring(fileContent.IndexOf(tagName) + tagName.Length);
                tagValue = tagValue.Substring(tagValue.IndexOf("(") + 1);
                if (tagValue.Contains(")")) tagValue = tagValue.Substring(0, tagValue.IndexOf(")")).Trim();
                tagValue = tagValue.Replace("\"", "").Trim();
            }
            return tagValue;
        }

        /// <summary>
        /// Gets the value between the brackets ( ) of a given tag
        /// </summary>
        /// <param name="tagName">The name of the tag</param>
        /// <param name="fileContent">The text where to search the tag and its value</param>
        /// <param name="alternateTag">If the first tag is not found look for a second tag</param>
        /// <param name="parentTag">true if the tag contains another tag to search</param>
        /// <returns>Value of the tag</returns>
        private string ParseTag(string tagName, string fileContent, string alternateTag, bool parentTag)
        {
            string tagValue = "";
            if (fileContent.Contains(tagName))
            {
                tagValue = fileContent.Substring(fileContent.IndexOf(tagName) + tagName.Length);
                tagValue = tagValue.Substring(tagValue.IndexOf("(") + 1);
                if (!parentTag)
                {
                    if (tagValue.Contains(")")) tagValue = tagValue.Substring(0, tagValue.IndexOf(")")).Trim();
                    tagValue = tagValue.Replace("\"", "").Trim();
                }
            }
            else if (fileContent.Contains(alternateTag))
            {
                tagValue = fileContent.Substring(fileContent.IndexOf(alternateTag) + alternateTag.Length);
                tagValue = tagValue.Substring(tagValue.IndexOf("(") + 1);
                if (!parentTag)
                {
                    if (tagValue.Contains(")")) tagValue = tagValue.Substring(0, tagValue.IndexOf(")")).Trim();
                    tagValue = tagValue.Replace("\"", "").Trim();
                }
            }
            return tagValue;
        }

        #endregion

        #region Delegates

        private delegate void FillConsistsDelegate();

        #endregion
    }
}