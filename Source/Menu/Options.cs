﻿// COPYRIGHT 2009, 2010, 2011, 2012, 2013, 2014 by the Open Rails project.
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

using GNU.Gettext;
using GNU.Gettext.WinForms;
using ORTS.Settings;
using ORTS.Updater;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace ORTS
{
    public partial class OptionsForm : Form
    {
        readonly UserSettings Settings;
        readonly UpdateManager UpdateManager;

        private GettextResourceManager catalog = new GettextResourceManager("Menu");
        private Boolean Initialized = false;

        public class Language
        {
            public string Code { get; set; }
            public string Name { get; set; }
        }

        public OptionsForm(UserSettings settings, UpdateManager updateManager)
        {
            InitializeComponent();

            Localizer.Localize(this, catalog);

            Settings = settings;
            UpdateManager = updateManager;

            // Collect all the available language codes by searching for
            // localisation files, but always include English (base language).
            var languageCodes = new List<string> { "en" };
            foreach (var path in Directory.GetDirectories(Path.GetDirectoryName(Application.ExecutablePath)))
                if (Directory.GetFiles(path, "*.Messages.resources.dll").Length > 0)
                    languageCodes.Add(Path.GetFileName(path));

            // Turn the list of codes in to a list of code + name pairs for
            // displaying in the dropdown list.
            comboLanguage.DataSource = 
                new[] { new Language { Code = "", Name = "System" } }
                .Union(languageCodes
                    .Select(lc => new Language { Code = lc, Name = CultureInfo.GetCultureInfo(lc).NativeName })
                    .OrderBy(l => l.Name)
                )
                .ToList();
            comboLanguage.DisplayMember = "Name";
            comboLanguage.ValueMember = "Code";
            comboLanguage.SelectedValue = Settings.Language;

            // Windows 2000 and XP should use 8.25pt Tahoma, while Windows
            // Vista and later should use 9pt "Segoe UI". We'll use the
            // Message Box font to allow for user-customizations, though.
            Font = SystemFonts.MessageBoxFont;
            AdhesionLevelValue.Font = new Font(Font, FontStyle.Bold);

            // Fix up the TrackBars on TabPanels to match the current theme.
            if (!Application.RenderWithVisualStyles)
            {
                trackAdhesionFactor.BackColor = BackColor;
                trackAdhesionFactorChange.BackColor = BackColor;
                trackDayAmbientLight.BackColor = BackColor;
                trackLODBias.BackColor = BackColor;
            }

            // General tab
            checkAlerter.Checked = Settings.Alerter;
            checkAlerterDisableExternal.Checked = Settings.AlerterDisableExternal;
            checkViewDispatcher.Checked = Settings.ViewDispatcher;
            checkUseLargeAddressAware.Checked = Settings.UseLargeAddressAware;
            checkSuppressConfirmations.Checked = Settings.SuppressConfirmations;
            checkRetainers.Checked = Settings.RetainersOnAllCars;
            checkGraduatedRelease.Checked = Settings.GraduatedRelease;
            numericBrakePipeChargingRate.Value = Settings.BrakePipeChargingRate;
            comboLanguage.Text = Settings.Language;
            comboPressureUnit.Text = Settings.PressureUnit;

            // Audio tab
            checkMSTSBINSound.Checked = Settings.MSTSBINSound;
            numericSoundVolumePercent.Value = Settings.SoundVolumePercent;
            numericSoundDetailLevel.Value = Settings.SoundDetailLevel;

            // Video tab
            checkDynamicShadows.Checked = Settings.DynamicShadows;
            checkFastFullScreenAltTab.Checked = Settings.FastFullScreenAltTab;
            checkWindowGlass.Checked = Settings.WindowGlass;
            checkModelInstancing.Checked = Settings.ModelInstancing;
            checkWire.Checked = Settings.Wire;
            checkVerticalSync.Checked = Settings.VerticalSync;
            numericCab2DStretch.Value = Settings.Cab2DStretch;
            numericViewingDistance.Value = Settings.ViewingDistance;
            checkDistantMountains.Checked = Settings.DistantMountains;
            numericDistantMountainsViewingDistance.Value = Settings.DistantMountainsViewingDistance / 1000;
            numericViewingFOV.Value = Settings.ViewingFOV;
            numericWorldObjectDensity.Value = Settings.WorldObjectDensity;
            comboWindowSize.Text = Settings.WindowSize;
            trackDayAmbientLight.Value = Settings.DayAmbientLight;

            // Simulation tab
            checkUseAdvancedAdhesion.Checked = Settings.UseAdvancedAdhesion;
            numericAdhesionMovingAverageFilterSize.Value = Settings.AdhesionMovingAverageFilterSize;
            checkBreakCouplers.Checked = Settings.BreakCouplers;
            checkCurveResistanceSpeedDependent.Checked = Settings.CurveResistanceSpeedDependent;
            checkCurveSpeedDependent.Checked = Settings.CurveSpeedDependent;
            checkTunnelResistanceDependent.Checked = Settings.TunnelResistanceDependent;
            checkOverrideNonElectrifiedRoutes.Checked = Settings.OverrideNonElectrifiedRoutes;
            checkHotStart.Checked = Settings.HotStart;

            // Keyboard tab
            InitializeKeyboardSettings();

            // DataLogger tab
            var dictionaryDataLoggerSeparator = new Dictionary<string, string>();
            dictionaryDataLoggerSeparator.Add("comma",catalog.GetString("comma"));
            dictionaryDataLoggerSeparator.Add("semicolon", catalog.GetString("semicolon"));
            dictionaryDataLoggerSeparator.Add("tab", catalog.GetString("tab"));
            dictionaryDataLoggerSeparator.Add("space", catalog.GetString("space"));
            comboDataLoggerSeparator.DataSource = new BindingSource(dictionaryDataLoggerSeparator, null);
            comboDataLoggerSeparator.DisplayMember = "Value";
            comboDataLoggerSeparator.ValueMember = "Key";
            comboDataLoggerSeparator.Text = catalog.GetString(Settings.DataLoggerSeparator);
            comboDataLogSpeedUnits.Text = Settings.DataLogSpeedUnits;
            checkDataLogger.Checked = Settings.DataLogger;
            checkDataLogPerformance.Checked = Settings.DataLogPerformance;
            checkDataLogPhysics.Checked = Settings.DataLogPhysics;
            checkDataLogMisc.Checked = Settings.DataLogMisc;

            // Evaluation tab
            checkDataLogTrainSpeed.Checked = Settings.DataLogTrainSpeed;
            numericDataLogTSInterval.Value = Settings.DataLogTSInterval;
            checkListDataLogTSContents.Items.AddRange(new object[] {
                catalog.GetString("Time"),
                catalog.GetString("Train Speed"),
                catalog.GetString("Max. Speed"),
                catalog.GetString("Signal State"),
                catalog.GetString("Track Elevation"),
                catalog.GetString("Direction"),
                catalog.GetString("Control Mode"),
                catalog.GetString("Distance Travelled"),
                catalog.GetString("Throttle %"),
                catalog.GetString("Brake Cyl Press"),
                catalog.GetString("Dyn Brake %"),
                catalog.GetString("Gear Setting")
            });
            for (var i = 0; i < checkListDataLogTSContents.Items.Count; i++)
                checkListDataLogTSContents.SetItemChecked(i, Settings.DataLogTSContents[i] == 1);
            checkDataLogStationStops.Checked = Settings.DataLogStationStops;

            // Updater tab
            var updateChannelNames = new Dictionary<string, string> {
                { "release", catalog.GetString("Release channel (Recommended for users)") },
                { "experimental", catalog.GetString("Experimental channel (For supporters)") },
                { "nightly", catalog.GetString("Nightly channel (For developers)") },
                { "", catalog.GetString("None") },
            };
            var updateChannelDescriptions = new Dictionary<string, string> {
                { "release", catalog.GetString("The release channel contains only official, hand-picked stable versions.") },
                { "experimental", catalog.GetString("The experimental channel contains automatically generated weekly versions.") },
                { "nightly", catalog.GetString("The nightly channel contains every single version created.") },
                { "", catalog.GetString("No automatic updates.") },
            };
            var spacing = labelUpdateChannel.Margin.All * 2;
            var indent = 20;
            var top = labelUpdateChannel.Bottom + spacing;
            foreach (var channel in UpdateManager.GetChannels())
            {
                var radio = new RadioButton() {
                    Text = updateChannelNames[channel.ToLowerInvariant()],
                    Margin = labelUpdateChannel.Margin,
                    Left = spacing,
                    Top = top,
                    Checked = updateManager.ChannelName.Equals(channel, StringComparison.InvariantCultureIgnoreCase),
                    AutoSize = true,
                    Tag = channel,
                };
                tabPageUpdater.Controls.Add(radio);
                top += radio.Height + spacing;
                var label = new Label() {
                    Text = updateChannelDescriptions[channel.ToLowerInvariant()],
                    Margin = labelUpdateChannel.Margin,
                    Left = spacing + indent,
                    Top = top,
                    Width = tabPageUpdater.ClientSize.Width - indent - spacing * 2,
                    AutoSize = true,
                };
                tabPageUpdater.Controls.Add(label);
                top += label.Height + spacing;
            }

            // Experimental tab
            numericUseSuperElevation.Value = Settings.UseSuperElevation;
            numericSuperElevationMinLen.Value = Settings.SuperElevationMinLen;
            numericSuperElevationGauge.Value = Settings.SuperElevationGauge;
            checkPerformanceTuner.Checked = Settings.PerformanceTuner;
            numericPerformanceTunerTarget.Value = Settings.PerformanceTunerTarget;
            checkDoubleWire.Checked = Settings.DoubleWire;
            checkEnhancedActCompatibility.Checked = Settings.EnhancedActCompatibility;
            checkNoForcedRedAtStationStops.Checked = Settings.NoForcedRedAtStationStops;
            trackLODBias.Value = Settings.LODBias;
            trackLODBias_ValueChanged(null, null);
            checkConditionalLoadOfNightTextures.Checked = Settings.ConditionalLoadOfNightTextures;
            checkSignalLightGlow.Checked = Settings.SignalLightGlow;
            checkExtendedAIShunting.Checked = Settings.ExtendedAIShunting;
            checkAutopilot.Checked = Settings.Autopilot;
            checkCircularSpeedGauge.Checked = Settings.CircularSpeedGauge;
            checkLODViewingExtention.Checked = Settings.LODViewingExtention;
            checkPreferDDSTexture.Checked = Settings.PreferDDSTexture;
            checkUseLocationPassingPaths.Checked = Settings.UseLocationPassingPaths;
            checkUseMSTSEnv.Checked = Settings.UseMSTSEnv;
            trackAdhesionFactor.Value = Settings.AdhesionFactor;
            checkAdhesionPropToWeather.Checked = Settings.AdhesionProportionalToWeather;
            trackAdhesionFactorChange.Value = Settings.AdhesionFactorChange;
            SetAdhesionLevelValue();

            Initialized = true;
        }

        static string ParseCategoryFrom(string name)
        {
            var len = name.IndexOf(' ');
            if (len == -1)
                return "";
            else
                return name.Substring(0, len);
        }


        static string ParseDescriptorFrom(string name)
        {
            var len = name.IndexOf(' ');
            if (len == -1)
                return name;
            else
                return name.Substring(len + 1);
        }

        void InitializeKeyboardSettings()
        {
            panelKeys.Controls.Clear();
            var columnWidth = (panelKeys.ClientSize.Width - 20) / 2;

            var tempLabel = new Label();
            var tempKIC = new KeyInputControl(Settings.Input.Commands[(int)UserCommands.GameQuit], InputSettings.DefaultCommands[(int)UserCommands.GameQuit]);
            var rowTop = Math.Max(tempLabel.Margin.Top, tempKIC.Margin.Top);
            var rowHeight = tempKIC.Height;
            var rowSpacing = rowHeight + tempKIC.Margin.Vertical;

            var lastCategory = "";
            var i = 0;
            foreach (UserCommands command in Enum.GetValues(typeof(UserCommands)))
            {
                var name = InputSettings.GetPrettyLocalizedName(command);
                var category = ParseCategoryFrom(name);
                var descriptor = ParseDescriptorFrom(name);

                if (category != lastCategory)
                {
                    var catlabel = new Label();
                    catlabel.Location = new Point(tempLabel.Margin.Left, rowTop + rowSpacing * i);
                    catlabel.Size = new Size(columnWidth - tempLabel.Margin.Horizontal, rowHeight);
                    catlabel.Text = category;
                    catlabel.TextAlign = ContentAlignment.MiddleCenter;
                    catlabel.Font = new Font(catlabel.Font, FontStyle.Bold);
                    panelKeys.Controls.Add(catlabel);

                    lastCategory = category;
                    ++i;
                }

                var label = new Label();
                label.Location = new Point(tempLabel.Margin.Left, rowTop + rowSpacing * i);
                label.Size = new Size(columnWidth - tempLabel.Margin.Horizontal, rowHeight);
                label.Text = descriptor;
                label.TextAlign = ContentAlignment.MiddleRight;
                panelKeys.Controls.Add(label);

                var keyInputControl = new KeyInputControl(Settings.Input.Commands[(int)command], InputSettings.DefaultCommands[(int)command]);
                keyInputControl.Location = new Point(columnWidth + tempKIC.Margin.Left, rowTop + rowSpacing * i);
                keyInputControl.Size = new Size(columnWidth - tempKIC.Margin.Horizontal, rowHeight);
                keyInputControl.ReadOnly = true;
                keyInputControl.Tag = command;
                panelKeys.Controls.Add(keyInputControl);
                toolTip1.SetToolTip(keyInputControl, catalog.GetString("Click to change this key"));

                ++i;
            }
        }

        void SaveKeyboardSettings()
        {
            foreach (Control control in panelKeys.Controls)
                if (control is KeyInputControl)
                    Settings.Input.Commands[(int)control.Tag].PersistentDescriptor = (control as KeyInputControl).UserInput.PersistentDescriptor;
        }

        void buttonOK_Click(object sender, EventArgs e)
        {
            var result = Settings.Input.CheckForErrors();
            if (result != "" && DialogResult.Yes != MessageBox.Show(catalog.GetString("Continue with conflicting key assignments?\n\n") + result, Application.ProductName, MessageBoxButtons.YesNo))
                return;

            // General tab
            Settings.Alerter = checkAlerter.Checked;
            Settings.AlerterDisableExternal = checkAlerterDisableExternal.Checked;
            Settings.ViewDispatcher = checkViewDispatcher.Checked;
            Settings.UseLargeAddressAware = checkUseLargeAddressAware.Checked;
            Settings.SuppressConfirmations = checkSuppressConfirmations.Checked;
            Settings.RetainersOnAllCars = checkRetainers.Checked;
            Settings.GraduatedRelease = checkGraduatedRelease.Checked;
            Settings.BrakePipeChargingRate = (int)numericBrakePipeChargingRate.Value;
            Settings.Language = comboLanguage.SelectedValue.ToString();
            Settings.PressureUnit = comboPressureUnit.Text;
            
            // Audio tab
            Settings.MSTSBINSound = checkMSTSBINSound.Checked;
            Settings.SoundVolumePercent = (int)numericSoundVolumePercent.Value;
            Settings.SoundDetailLevel = (int)numericSoundDetailLevel.Value;
            
            // Video tab
            Settings.DynamicShadows = checkDynamicShadows.Checked;
            Settings.FastFullScreenAltTab = checkFastFullScreenAltTab.Checked;
            Settings.WindowGlass = checkWindowGlass.Checked;
            Settings.ModelInstancing = checkModelInstancing.Checked;
            Settings.Wire = checkWire.Checked;
            Settings.VerticalSync = checkVerticalSync.Checked;
            Settings.Cab2DStretch = (int)numericCab2DStretch.Value;
            Settings.ViewingDistance = (int)numericViewingDistance.Value;
            Settings.DistantMountains = checkDistantMountains.Checked;
            Settings.DistantMountainsViewingDistance = (int)numericDistantMountainsViewingDistance.Value * 1000;
            Settings.ViewingFOV = (int)numericViewingFOV.Value;
            Settings.WorldObjectDensity = (int)numericWorldObjectDensity.Value;
            Settings.WindowSize = comboWindowSize.Text;
            Settings.DayAmbientLight = (int)trackDayAmbientLight.Value;
            
            // Simulation tab
            Settings.UseAdvancedAdhesion = checkUseAdvancedAdhesion.Checked;
            Settings.AdhesionMovingAverageFilterSize = (int)numericAdhesionMovingAverageFilterSize.Value;
            Settings.BreakCouplers = checkBreakCouplers.Checked;
            Settings.CurveResistanceSpeedDependent = checkCurveResistanceSpeedDependent.Checked;
            Settings.CurveSpeedDependent = checkCurveSpeedDependent.Checked;
            Settings.TunnelResistanceDependent = checkTunnelResistanceDependent.Checked;
            Settings.OverrideNonElectrifiedRoutes = checkOverrideNonElectrifiedRoutes.Checked;
            Settings.HotStart = checkHotStart.Checked;
            
            // Keyboard tab
            // These are edited live.
            
            // DataLogger tab
            Settings.DataLoggerSeparator = ((KeyValuePair<string, string>)comboDataLoggerSeparator.SelectedItem).Key;
            Settings.DataLogSpeedUnits = comboDataLogSpeedUnits.Text;
            Settings.DataLogger = checkDataLogger.Checked;
            Settings.DataLogPerformance = checkDataLogPerformance.Checked;
            Settings.DataLogPhysics = checkDataLogPhysics.Checked;
            Settings.DataLogMisc = checkDataLogMisc.Checked;
            
            // Evaluation tab
            Settings.DataLogTrainSpeed = checkDataLogTrainSpeed.Checked;
            Settings.DataLogTSInterval = (int)numericDataLogTSInterval.Value;
            for (var i = 0; i < checkListDataLogTSContents.Items.Count; i++)
                Settings.DataLogTSContents[i] = checkListDataLogTSContents.GetItemChecked(i) ? 1 : 0;
            Settings.DataLogStationStops = checkDataLogStationStops.Checked;
            
            // Updater tab
            foreach (Control control in tabPageUpdater.Controls)
                if ((control is RadioButton) && (control as RadioButton).Checked)
                    UpdateManager.SetChannel((string)control.Tag);

            // Experimental tab
            Settings.UseSuperElevation = (int)numericUseSuperElevation.Value;
            Settings.SuperElevationMinLen = (int)numericSuperElevationMinLen.Value;
            Settings.SuperElevationGauge = (int)numericSuperElevationGauge.Value;
            Settings.PerformanceTuner = checkPerformanceTuner.Checked;
            Settings.PerformanceTunerTarget = (int)numericPerformanceTunerTarget.Value;
            Settings.DoubleWire = checkDoubleWire.Checked;
            Settings.EnhancedActCompatibility = checkEnhancedActCompatibility.Checked;
            Settings.NoForcedRedAtStationStops =  checkNoForcedRedAtStationStops.Checked;
            Settings.LODBias = trackLODBias.Value;
            Settings.ConditionalLoadOfNightTextures = checkConditionalLoadOfNightTextures.Checked;
            Settings.SignalLightGlow = checkSignalLightGlow.Checked;
            Settings.ExtendedAIShunting = checkExtendedAIShunting.Checked;
            Settings.Autopilot = checkAutopilot.Checked;
            Settings.CircularSpeedGauge = checkCircularSpeedGauge.Checked;
            Settings.LODViewingExtention = checkLODViewingExtention.Checked;
            Settings.PreferDDSTexture = checkPreferDDSTexture.Checked;
            Settings.UseLocationPassingPaths = checkUseLocationPassingPaths.Checked;
            Settings.UseMSTSEnv = checkUseMSTSEnv.Checked;
            Settings.AdhesionFactor = (int)trackAdhesionFactor.Value;
            Settings.AdhesionProportionalToWeather = checkAdhesionPropToWeather.Checked;
            Settings.AdhesionFactorChange = (int)trackAdhesionFactorChange.Value;

            Settings.Save();

            DialogResult = DialogResult.OK;
        }

        void buttonDefaultKeys_Click(object sender, EventArgs e)
        {
            if (DialogResult.Yes == MessageBox.Show(catalog.GetString("Remove all custom key assignments?"), Application.ProductName, MessageBoxButtons.YesNo))
            {
                Settings.Input.Reset();
                InitializeKeyboardSettings();
            }
        }

        void buttonExport_Click(object sender, EventArgs e)
        {
            var outputPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Open Rails Keyboard.txt");
            Settings.Input.DumpToText(outputPath);
            MessageBox.Show(catalog.GetString("A listing of all keyboard commands and keys has been placed here:\n\n") + outputPath, Application.ProductName);
        }

        void buttonCheckKeys_Click(object sender, EventArgs e)
        {
            var errors = Settings.Input.CheckForErrors();
            if (errors != "")
                MessageBox.Show(errors, Application.ProductName);
            else
                MessageBox.Show(catalog.GetString("No errors found."), Application.ProductName);
        }

        private void comboBoxWindowSize_SelectedIndexChanged( object sender, EventArgs e ) {
            var windowSizeParts = comboWindowSize.Text.Split( new[] { 'x' }, 2 );
            double width = Convert.ToDouble( windowSizeParts[0] );
            double height = Convert.ToDouble( windowSizeParts[1] );
            double aspectRatio = width / height;
            bool wideScreen = aspectRatio > (4.0 / 3.0); 
            numericCab2DStretch.Enabled = wideScreen;
        }

        private void numericUpDownFOV_ValueChanged(object sender, EventArgs e)
        {
            labelFOVHelp.Text = catalog.GetStringFmt("{0:F0}° vertical FOV is the same as:\n{1:F0}° horizontal FOV on 4:3\n{2:F0}° horizontal FOV on 16:9", numericViewingFOV.Value, numericViewingFOV.Value * 4 / 3, numericViewingFOV.Value * 16 / 9);
        }

        private void trackBarDayAmbientLight_Scroll(object sender, EventArgs e)
        {
            toolTip1.SetToolTip(trackDayAmbientLight, (trackDayAmbientLight.Value * 5).ToString() + " %");
        }

        private void comboBoxLanguage_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (Initialized)
                MessageBox.Show(catalog.GetStringFmt("Please restart {0} in order to load the new language.", Application.ProductName), Application.ProductName);
        }

        private void AdhesionFactorTrackBar1_ValueChanged(object sender, EventArgs e)
        {
            SetAdhesionLevelValue();
            AdhesionFactorValueLabel.Text = trackAdhesionFactor.Value.ToString() + "%";
            AdhesionFactorChangeValueLabel.Text = trackAdhesionFactorChange.Value.ToString() + "%";
        }

        private void SetAdhesionLevelValue()
        {
            int level = trackAdhesionFactor.Value - trackAdhesionFactorChange.Value;
            if (checkAdhesionPropToWeather.Checked)
                level -= 40;

            if (level > 159)
                AdhesionLevelValue.Text = catalog.GetString("Very easy");
            else if (level > 139)
                AdhesionLevelValue.Text = catalog.GetString("Easy");
            else if (level > 119)
                AdhesionLevelValue.Text = catalog.GetString("MSTS Compatibile");
            else if (level > 89)
                AdhesionLevelValue.Text = catalog.GetString("Normal");
            else if (level > 69)
                AdhesionLevelValue.Text = catalog.GetString("Hard");
            else if (level > 59)
                AdhesionLevelValue.Text = catalog.GetString("Very Hard");
            else
                AdhesionLevelValue.Text = catalog.GetString("Good luck!");
        }

        private void AdhesionPropToWeatherCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            SetAdhesionLevelValue();
        }

        private void trackLODBias_ValueChanged(object sender, EventArgs e)
        {
            if (trackLODBias.Value == -100)
                labelLODBias.Text = catalog.GetStringFmt("No detail (-{0}%)", -trackLODBias.Value);
            else if (trackLODBias.Value < 0)
                labelLODBias.Text = catalog.GetStringFmt("Less detail (-{0}%)", -trackLODBias.Value);
            else if (trackLODBias.Value == 0)
                labelLODBias.Text = catalog.GetStringFmt("Default detail (+{0}%)", trackLODBias.Value);
            else if (trackLODBias.Value < 100)
                labelLODBias.Text = catalog.GetStringFmt("More detail (+{0}%)", trackLODBias.Value);
            else
                labelLODBias.Text = catalog.GetStringFmt("All detail (+{0}%)", trackLODBias.Value);
        }
    }
}
