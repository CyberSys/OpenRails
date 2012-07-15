﻿// COPYRIGHT 2009, 2010, 2011 by the Open Rails project.
// This code is provided to enable you to contribute improvements to the open rails program.  
// Use of the code for any other purpose or distribution of the code to anyone else
// is prohibited without specific written permission from admin@openrails.org.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Win32;
using ORTS.Menu;    // needed for Activity

namespace ORTS
{
    public partial class MainForm : Form
    {
        public enum MultiplayerMode
        {
            None,
            Server,
            Client,
        }

        bool Initialized;
        List<Folder> Folders = new List<Folder>();
        List<Route> Routes = new List<Route>();
        List<Activity> Activities = new List<Activity>();
        Task<List<Route>> RouteLoader;
        Task<List<Activity>> ActivityLoader;

        // To pre-load selection from previous choice
        int listBoxFoldersSelectedIndex;
        int listBoxRoutesSelectedIndex;
        int listBoxActivitiesSelectedIndex;

        public Folder SelectedFolder { get { return listBoxFolders.SelectedIndex < 0 ? null : Folders[listBoxFolders.SelectedIndex]; } }
        public Route SelectedRoute { get { return listBoxRoutes.SelectedIndex < 0 ? null : Routes[listBoxRoutes.SelectedIndex]; } }
        public Activity SelectedActivity { get { return listBoxActivities.SelectedIndex < 0 ? null : Activities[listBoxActivities.SelectedIndex]; } set { if (listBoxActivities.SelectedIndex >= 0) Activities[listBoxActivities.SelectedIndex] = value; } }
        public string SelectedSaveFile { get; set; }
        public MultiplayerMode Multiplayer { get; set; }

        #region Main Form
        public MainForm()
        {
            InitializeComponent();

            // Windows 2000 and XP should use 8.25pt Tahoma, while Windows
            // Vista and later should use 9pt "Segoe UI". We'll use the
            // Message Box font to allow for user-customizations, though.
            Font = SystemFonts.MessageBoxFont;

            // Set title to show revision or build info.
            Text = String.Format(Program.Version.Length > 0 ? "{0} {1}" : "{0} BUILD {2}", Application.ProductName, Program.Version, Program.Build);

            CleanupPre021();
        }

        void MainForm_Shown(object sender, EventArgs e)
        {
            LoadOptions();

            if (!Initialized)
            {
                Initialized = true;

                LoadFolders();

                if (Folders.Count == 0)
                    MessageBox.Show("Microsoft Train Simulator doesn't appear to be installed.\nClick on 'Add...' to point Open Rails at your Microsoft Train Simulator folder.", Application.ProductName);
            }
        }

        void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            SaveOptions();
            if (RouteLoader != null)
                RouteLoader.Cancel();
            if (ActivityLoader != null)
                ActivityLoader.Cancel();

            // Empty the deleted_saves folder
            var userDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), Application.ProductName);
            var folderToDelete = userDataFolder + @"\deleted_saves";
            if (Directory.Exists(folderToDelete))
            {
                Directory.Delete(folderToDelete, true);   // true removes all contents as well as folder
            }
            // Tidy up after versions which used SAVE.BIN
            var file = userDataFolder + @"\SAVE.BIN";
            if (File.Exists(file))
                File.Delete(file);
        }
        #endregion

        #region Folders
        void listBoxFolder_SelectedIndexChanged(object sender, EventArgs e)
        {
            LoadRoutes();
        }

        void buttonFolderAdd_Click(object sender, EventArgs e)
        {
            using (var folderBrowser = new FolderBrowserDialog())
            {
                folderBrowser.SelectedPath = SelectedFolder != null ? SelectedFolder.Path : "";
                folderBrowser.Description = "Navigate to your alternate MSTS installation folder.";
                folderBrowser.ShowNewFolderButton = false;
                if (folderBrowser.ShowDialog(this) == DialogResult.OK)
                {
                    using (var form = new FormFolderName())
                    {
                        form.textBoxDescription.Text = Path.GetFileName(folderBrowser.SelectedPath);
                        if (form.ShowDialog(this) == DialogResult.OK)
                        {
                            Folders.Add(new Folder(form.textBoxDescription.Text, folderBrowser.SelectedPath));
                            SaveFolders();
                            LoadFolders();
                        }
                    }
                }
            }
        }

        void buttonFolderRemove_Click(object sender, EventArgs e)
        {
            var index = listBoxFolders.SelectedIndex;
            if (index >= 0)
            {
                listBoxFolders.ClearSelected();
                listBoxFolders.Items.RemoveAt(index);
                Folders.RemoveAt(index);
                SaveFolders();
                if (listBoxFolders.Items.Count > 0)
                    listBoxFolders.SelectedIndex = 0;
            }
        }
        #endregion

        #region Routes
        void listBoxRoutes_SelectedIndexChanged(object sender, EventArgs e)
        {
            LoadActivities();
        }

        void listBoxRoutes_DoubleClick(object sender, EventArgs e)
        {
            DisplayRouteDetails();
        }

        void buttonRouteDetails_Click(object sender, EventArgs e)
        {
            DisplayRouteDetails();
        }
        #endregion

        #region Activities
        void listBoxActivities_DoubleClick(object sender, EventArgs e)
        {
            DisplayActivityDetails();
        }

        void buttonActivityDetails_Click(object sender, EventArgs e)
        {
            DisplayActivityDetails();
        }
        #endregion

        #region Misc. buttons and options
        void buttonSwitchStyle_Click(object sender, EventArgs e)
        {
            using (var RK = Registry.CurrentUser.CreateSubKey(Program.RegistryKey))
            {
                if (RK != null)
                    RK.SetValue("LauncherMenu", 2);
            }
            Process.Start(Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "MenuWPF.exe"));
            Close();
        }

        void buttonTesting_Click(object sender, EventArgs e)
        {
            using (var form = new TestingForm(this, SelectedRoute, SelectedActivity))
            {
                form.ShowDialog(this);
            }
        }

        void buttonOptions_Click(object sender, EventArgs e)
        {
            using (var form = new OptionsForm())
            {
                form.ShowDialog(this);
            }
        }

        void buttonResume_Click(object sender, EventArgs e)
        {
            using (var form = new ResumeForm(SelectedRoute, SelectedActivity))
            {
                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    SelectedSaveFile = form.SelectedSaveFile;
                    DialogResult = DialogResult.Retry;
                }
            }
        }

        void buttonMultiplayer_Click(object sender, EventArgs e)
        {
            using (var form = new MultiplayerForm())
            {
                if (form.ShowDialog(this) == DialogResult.OK)
                    DialogResult = DialogResult.OK;
            }
        }

        void buttonStart_Click(object sender, EventArgs e)
        {
            SaveOptions();

            Multiplayer = MultiplayerMode.None;
            if (checkBoxMultiplayer.Checked && !GetMultiplayerInfo())
                return;

            if (SelectedActivity != null && SelectedActivity.FileName != null)
            {
                DialogResult = DialogResult.OK;
            }
            else if (SelectedActivity != null && SelectedActivity.FileName == null)
            {
                if (GetExploreInfo())
                    DialogResult = DialogResult.OK;
            }
        }
        #endregion

        void CleanupPre021()
        {
            // Handle cleanup from pre version 0021
            using (var RK = Registry.CurrentUser.OpenSubKey("SOFTWARE\\ORTS"))
            {
                if (RK != null)
                    Registry.CurrentUser.DeleteSubKeyTree("SOFTWARE\\ORTS");
            }

            if (!File.Exists(Folder.FolderDataFile))
            {
                // Handle name change that occured at version 0021
                var oldFolderDataFileName = Program.UserDataFolder + @"\..\ORTS\folder.dat";
                try
                {
                    if (File.Exists(oldFolderDataFileName))
                    {
                        File.Copy(oldFolderDataFileName, Folder.FolderDataFile);
                        Directory.Delete(Path.GetDirectoryName(oldFolderDataFileName), true);
                    }
                }
                catch
                {
                }
            }
        }

        void LoadOptions()
        {
            // Restore retained settings
            using (var RK = Registry.CurrentUser.OpenSubKey(Program.RegistryKey))
            {
                if (RK != null)
                {
                    // Registry stores strings which can be cast into integers but not into booleans,
                    // so we convert to an integer, which if == 1, return true.
                    checkBoxWarnings.Checked = ((int)RK.GetValue("Logging", 1) == 1) ? true : false;
                    // true : false reversed as Windowed is opposite of Fullscreen
                    checkBoxWindowed.Checked = ((int)RK.GetValue("Fullscreen", 0) == 1) ? false : true;
                    listBoxFoldersSelectedIndex = (int)RK.GetValue("Folders", -1);
                    listBoxRoutesSelectedIndex = (int)RK.GetValue("Routes", -1);
                    listBoxActivitiesSelectedIndex = (int)RK.GetValue("Activities", -1);
                    checkBoxMultiplayer.Checked = ((int)RK.GetValue("Multiplayer", 0) == 1) ? true : false;
                }
            }
        }

        void SaveOptions()
        {
            // Retain settings for convenience
            using (var RK = Registry.CurrentUser.CreateSubKey(Program.RegistryKey))
            {
                // Registry will not accept booleans, so use integers instead.
                RK.SetValue("Logging", checkBoxWarnings.Checked ? 1 : 0);
                // 1 : 0 reversed as Windowed is opposite of Fullscreen
                RK.SetValue("Fullscreen", checkBoxWindowed.Checked ? 0 : 1);
                RK.SetValue("Folders", listBoxFolders.SelectedIndex);
                RK.SetValue("Routes", listBoxRoutes.SelectedIndex);
                RK.SetValue("Activities", listBoxActivities.SelectedIndex);
                RK.SetValue("Multiplayer", checkBoxMultiplayer.Checked ? 1 : 0);
            }
        }

        void LoadFolders()
        {
            try
            {
                Folders = Folder.GetFolders().OrderBy(f => f.Name).ToList();
            }
            catch (Exception error)
            {
                MessageBox.Show(error.ToString());
            }

            listBoxFolders.Items.Clear();
            foreach (var folder in Folders)
                listBoxFolders.Items.Add(folder.Name);

            if (Folders.Count > 0)
                listBoxFolders.SelectedIndex = Math.Min(listBoxFoldersSelectedIndex, listBoxFolders.Items.Count - 1);
            else
                listBoxFolders.ClearSelected();
        }

        void SaveFolders()
        {
            Folder.SetFolders(Folders);
        }

        void LoadRoutes()
        {
            if (RouteLoader != null)
                RouteLoader.Cancel();

            listBoxRoutes.Items.Clear();
            var selectedFolder = SelectedFolder;
            RouteLoader = new Task<List<Route>>(this, () => Route.GetRoutes(selectedFolder).OrderBy(r => r.Name).ToList(), (routes) =>
            {
                Routes = routes;
                labelRoutes.Visible = Routes.Count == 0;
                foreach (var route in Routes)
                    listBoxRoutes.Items.Add(route.Name);
                if (Routes.Count > 0)
                {
                    listBoxRoutes.SelectedIndex = Math.Min(listBoxRoutesSelectedIndex, listBoxRoutes.Items.Count - 1);
                    listBoxRoutesSelectedIndex = 0; // Not needed after first use. Reset so any change in folder will select first route.
                }
                else
                {
                    listBoxRoutes.ClearSelected();
                }
            });
        }

        void LoadActivities()
        {
            if (ActivityLoader != null)
                ActivityLoader.Cancel();

            listBoxActivities.Items.Clear();
            var selectedRoute = SelectedRoute;
            ActivityLoader = new Task<List<Activity>>(this, () => Activity.GetActivities(selectedRoute).OrderBy(a => a.Name).ToList(), (activities) =>
            {
                Activities = activities;
                labelActivities.Visible = Activities.Count == 0;
                foreach (var activity in Activities)
                    listBoxActivities.Items.Add(activity.Name);
                if (Activities.Count > 0)
                {
                    listBoxActivities.SelectedIndex = Math.Min(listBoxActivitiesSelectedIndex, listBoxActivities.Items.Count - 1);
                    listBoxActivitiesSelectedIndex = 0; // Not needed after first use. Reset so any change in route will select first activity.
                }
                else
                {
                    listBoxActivities.ClearSelected();
                }
            });
        }

        void DisplayRouteDetails()
        {
            if (listBoxRoutes.SelectedIndex >= 0)
            {
                using (var form = new DetailsForm(SelectedRoute))
                {
                    form.ShowDialog(this);
                }
            }
        }

        void DisplayActivityDetails()
        {
            if (listBoxActivities.SelectedIndex == 0)
                GetExploreInfo();
            else if (listBoxActivities.SelectedIndex > 0)
            {
                using (var form = new DetailsForm(SelectedActivity))
                {
                    form.ShowDialog(this);
                }
            }
        }

        bool GetExploreInfo()
        {
            using (var form = new ExploreForm(SelectedFolder, SelectedRoute, (ExploreActivity)SelectedActivity))
            {
                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    SelectedActivity = form.NewExploreActivity;
                    return true;
                }
                return false;
            }
        }

        bool GetMultiplayerInfo()
        {
            using (var form = new MultiplayerForm())
            {
                switch (form.ShowDialog(this))
                {
                    case DialogResult.Yes:
                        Multiplayer = MultiplayerMode.Server;
                        return true;
                    case DialogResult.No:
                        Multiplayer = MultiplayerMode.Client;
                        return true;
                    default:
                        return false;
                }
            }
        }
    }
}