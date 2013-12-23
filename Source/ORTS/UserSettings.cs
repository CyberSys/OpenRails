﻿// COPYRIGHT 2012, 2013 by the Open Rails project.
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using ORTS.Common;

namespace ORTS
{
	[AttributeUsage(AttributeTargets.Property)]
	public sealed class DefaultAttribute : Attribute
	{
		public readonly object Value;
		public DefaultAttribute(object value)
		{
			Value = value;
		}
	}

	[AttributeUsage(AttributeTargets.Property)]
	public sealed class DoNotSaveAttribute : Attribute
	{
	}

	public class UserSettings : Settings
	{
		public static readonly string RegistryKey;        // ie @"SOFTWARE\OpenRails\ORTS"
		public static readonly string SettingsFilePath;   // ie @"C:\Program Files\Open Rails\OpenRails.ini"
		public static readonly string UserDataFolder;     // ie @"C:\Users\Wayne\AppData\Roaming\Open Rails"
		public static readonly string DeletedSaveFolder;  // ie @"C:\Users\Wayne\AppData\Roaming\Open Rails\Deleted Saves"
		public static readonly string SavePackFolder;     // ie @"C:\Users\Wayne\AppData\Roaming\Open Rails\Save Packs"

		static UserSettings()
		{
			// Only one of these is allowed; if the INI file exists, we use that, otherwise we use the registry.
			RegistryKey = "SOFTWARE\\OpenRails\\ORTS";
			SettingsFilePath = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "OpenRails.ini");
			if (File.Exists(SettingsFilePath))
				RegistryKey = null;
			else
				SettingsFilePath = null;

			UserDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), Application.ProductName);
			if (!Directory.Exists(UserDataFolder)) Directory.CreateDirectory(UserDataFolder);
			DeletedSaveFolder = Path.Combine(UserDataFolder, "Deleted Saves");
			SavePackFolder = Path.Combine(UserDataFolder, "Save Packs");
		}

		readonly Dictionary<string, object> CustomDefaultValues = new Dictionary<string, object>();

		#region User Settings

		// Please put all user settings in here as auto-properties. Public properties
		// of type 'string', 'int', 'bool', 'string[]' and 'int[]' are automatically loaded/saved.

		// Main menu settings:
		[Default(true)]
		public bool Logging { get; set; }
		[Default(false)]
		public bool FullScreen { get; set; }
		[Default("")]
		public string Multiplayer_User { get; set; }
		[Default("127.0.0.1")]
		public string Multiplayer_Host { get; set; }
		[Default(30000)]
		public int Multiplayer_Port { get; set; }

		// General settings:
		[Default(false)]
		public bool Alerter { get; set; }
		[Default(false)]
		public bool GraduatedRelease { get; set; }
		[Default(21)]
		public int BrakePipeChargingRate { get; set; }
		[Default(false)]
		public bool SuppressConfirmations { get; set; }
		[Default(false)]
		public bool ViewDispatcher { get; set; }

		// Audio settings:
		[Default(5)]
		public int SoundDetailLevel { get; set; }
		[Default(false)]
		public bool MSTSBINSound { get; set; }
		[Default(100)]
		public int SoundVolumePercent { get; set; }

		// Video settings:
		[Default(10)]
		public int WorldObjectDensity { get; set; }
		[Default("1024x768")]
		public string WindowSize { get; set; }
		[Default(false)]
		public bool TrainLights { get; set; }
		[Default(false)]
		public bool Precipitation { get; set; }
		[Default(false)]
		public bool Wire { get; set; }
		[Default(false)]
		public bool DynamicShadows { get; set; }
		[Default(false)]
		public bool WindowGlass { get; set; }
		[Default(45)] // MSTS uses 60 FOV horizontally, on 4:3 displays this is 45 FOV vertically (what OR uses).
		public int ViewingFOV { get; set; }
		[Default(0)]
		public int Cab2DStretch { get; set; }
		[Default(2000)]
		public int ViewingDistance { get; set; }

		// Simulation settings:
		[Default(true)]
		public bool UseAdvancedAdhesion { get; set; }
		[Default(10)]
		public int AdhesionMovingAverageFilterSize { get; set; }
		[Default(false)]
		public bool BreakCouplers { get; set; }
		[Default(false)]
		public bool OverrideNonElectrifiedRoutes { get; set; }

		// Experimental settings for super-elevation:
		[Default(0)]
		public int UseSuperElevation { get; set; }
		[Default(50)]
		public int SuperElevationMinLen { get; set; }
		[Default(1435)]
		public int SuperElevationGauge { get; set; }

		// Experimental settings for distant mountains:
		[Default(false)]
		public bool DistantMountains { get; set; }
		[Default(40000)]
		public int DistantMountainsViewingDistance { get; set; }

		// Experimental settings for LOD extension:
		[Default(false)]
		public bool LODViewingExtention { get; set; }

		// Experimental settings for auto-tuning performance:
		[Default(false)]
		public bool PerformanceTuner { get; set; }
		[Default(60)]
		public int PerformanceTunerTarget { get; set; }

		// Experimental settings for overhead wire:
		[Default(false)]
		public bool DoubleWire { get; set; }

		// Experimental settings for loading stuttering:
		[Default(0)]
		public int LoadingDelay { get; set; }

		// Data Logger settings:
		[Default("comma")]
		public string DataLoggerSeparator { set; get; }
		[Default("route")]
		public string DataLogSpeedUnits { get; set; }
		[Default(false)]
		public bool DataLogStart { get; set; }
		[Default(true)]
		public bool DataLogPerformance { get; set; }
		[Default(false)]
		public bool DataLogPhysics { get; set; }
		[Default(false)]
		public bool DataLogMisc { get; set; }

		// Hidden settings:
		[Default(0)]
		public int CarVibratingLevel { get; set; }
		[Default("OpenRailsLog.txt")]
		public string LoggingFilename { get; set; }
		[Default("")] // If left as "", OR will use the user's desktop folder
		public string LoggingPath { get; set; }
		[Default("")]
		public string ScreenshotPath { get; set; }
		[Default(0)]
		public int ShaderModel { get; set; }
		[Default(false)]
		public bool ShadowAllShapes { get; set; }
		[Default(true)]
		public bool ShadowMapBlur { get; set; }
		[Default(4)]
		public int ShadowMapCount { get; set; }
		[Default(0)]
		public int ShadowMapDistance { get; set; }
		[Default(1024)]
		public int ShadowMapResolution { get; set; }
		[Default(false)]
		public bool VerticalSync { get; set; }
		[Default(10)]
		public int Multiplayer_UpdateInterval { get; set; }
		[Default("http://openrails.org/images/support-logos.jpg")]
		public string AvatarURL { get; set; }
		[Default(false)]
		public bool ShowAvatar { get; set; }

		// Internal settings:
		[Default(false)]
		public bool DataLogger { get; set; }
		[Default(false)]
		public bool Profiling { get; set; }
		[Default(0)]
		public int ProfilingFrameCount { get; set; }
		[Default(0)]
		public int ProfilingTime { get; set; }
		[Default(0)]
		public int ReplayPauseBeforeEndS { get; set; }
		[Default(true)]
		public bool ReplayPauseBeforeEnd { get; set; }
		[Default(true)]
		public bool ShowErrorDialogs { get; set; }
		[Default(new string[0])]
		public string[] Menu_Selection { get; set; }
		[Default(false)]
		public bool Multiplayer { get; set; }
		[Default(new[] { 50, 50 })]
		public int[] WindowPosition_Activity { get; set; }
		[Default(new[] { 50, 0 })]
		public int[] WindowPosition_Compass { get; set; }
		[Default(new[] { 100, 100 })]
		public int[] WindowPosition_DriverAid { get; set; }
		[Default(new[] { 50, 50 })]
		public int[] WindowPosition_Help { get; set; }
		[Default(new[] { 0, 100 })]
		public int[] WindowPosition_NextStation { get; set; }
		[Default(new[] { 50, 50 })]
		public int[] WindowPosition_Quit { get; set; }
		[Default(new[] { 0, 50 })]
		public int[] WindowPosition_Switch { get; set; }
		[Default(new[] { 100, 0 })]
		public int[] WindowPosition_TrackMonitor { get; set; }
		[Default(new[] { 50, 50 })]
		public int[] WindowPosition_TrainOperations { get; set; }
		[Default(new[] { 50, 50 })]
		public int[] WindowPosition_CarOperations { get; set; }
		[Default(new[] { 50, 50 })]
		public int[] WindowPosition_ComposeMessage { get; set; }

		// Menu-game communication settings:
		[Default(false)]
		[DoNotSave]
		public bool MultiplayerClient { get; set; }
		[Default(false)]
		[DoNotSave]
		public bool MultiplayerServer { get; set; }

		#endregion

		public UserSettings(IEnumerable<string> options)
			: base(SettingStore.GetSettingStore(SettingsFilePath, RegistryKey, null))
		{
			CustomDefaultValues["LoggingPath"] = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
			CustomDefaultValues["ScreenshotPath"] = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), Application.ProductName);
			CustomDefaultValues["Multiplayer_User"] = Environment.UserName;
			Load(options);
		}

		public override object GetDefaultValue(string name)
		{
			var property = GetType().GetProperty(name);

			if (CustomDefaultValues.ContainsKey(property.Name))
				return CustomDefaultValues[property.Name];

			if (property.GetCustomAttributes(typeof(DefaultAttribute), false).Length > 0)
				return (property.GetCustomAttributes(typeof(DefaultAttribute), false)[0] as DefaultAttribute).Value;

			throw new InvalidDataException(String.Format("Setting {0} has no default value.", property.Name));
		}

		PropertyInfo GetProperty(string name)
		{
			return GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
		}

		PropertyInfo[] GetProperties()
		{
			return GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
		}

		protected override object GetValue(string name)
		{
			return GetProperty(name).GetValue(this, null);
		}

		protected override void SetValue(string name, object value)
		{
			GetProperty(name).SetValue(this, value, null);
		}

		protected override void Load(bool allowUserSettings, Dictionary<string, string> optionsDictionary)
		{
			foreach (var property in GetProperties())
				Load(allowUserSettings, optionsDictionary, property.Name, property.PropertyType);
		}

		public override void Save()
		{
			foreach (var property in GetProperties())
				if (property.GetCustomAttributes(typeof(DoNotSaveAttribute), false).Length == 0)
					Save(property.Name, property.PropertyType);
		}

		public override void Save(string name)
		{
			var property = GetProperty(name);
			if (property.GetCustomAttributes(typeof(DoNotSaveAttribute), false).Length == 0)
				Save(property.Name, property.PropertyType);
		}

		public void Log()
		{
			foreach (var property in GetProperties().OrderBy(p => p.Name))
			{
				var value = property.GetValue(this, null);
				var source = Sources[property.Name] == Source.CommandLine ? "(command-line)" : Sources[property.Name] == Source.User ? "(user set)" : "";
				if (property.PropertyType == typeof(string[]))
					Console.WriteLine("{0,-30} = {2,-14} {1}", property.Name, String.Join(", ", ((string[])value).Select(v => v.ToString()).ToArray()), source);
				else if (property.PropertyType == typeof(int[]))
					Console.WriteLine("{0,-30} = {2,-14} {1}", property.Name, String.Join(", ", ((int[])value).Select(v => v.ToString()).ToArray()), source);
				else
					Console.WriteLine("{0,-30} = {2,-14} {1}", property.Name, value, source);
			}
		}
	}
}
