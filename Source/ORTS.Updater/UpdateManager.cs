﻿// COPYRIGHT 2014 by the Open Rails project.
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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using Ionic.Zip;
using Newtonsoft.Json;
using ORTS.Settings;

namespace ORTS.Updater
{
    public class UpdateManager
    {
        // The date on this is fairly arbitrary - it's only used in a calculation to round the DateTime up to the next TimeSpan period.
        readonly DateTime BaseDateTimeMidnightLocal = new DateTime(2010, 1, 1, 0, 0, 0, DateTimeKind.Local);

        public readonly string BasePath;
        public readonly string ProductName;
        public readonly string ProductVersion;
        readonly UpdateSettings Settings;
        readonly UpdateState State;

        public Update LatestUpdate { get; private set; }
        public Exception LatestUpdateError { get; private set; }

        public Exception UpdateError { get; private set; }

        public UpdateManager(string basePath, string productName, string productVersion)
        {
            if (!Directory.Exists(basePath)) throw new ArgumentException("The specified path must be valid and exist as a directory.", "basePath");
            BasePath = basePath;
            ProductName = productName;
            ProductVersion = productVersion;
            try
            {
                Settings = new UpdateSettings();
                State = new UpdateState();
            }
            catch (ArgumentException)
            {
                // Updater.ini doesn't exist. That's cool, we'll just disable updating.
            }
        }

        public void Check()
        {
            if (Settings == null)
                return;

            try
            {
                if (DateTime.Now < State.NextCheck)
                {
                    LatestUpdate = State.Update.Length > 0 ? JsonConvert.DeserializeObject<Update>(State.Update) : null;
                    LatestUpdateError = State.Update.Length > 0 ? null : new InvalidDataException("Last update check failed.");
                    return;
                }

                ResetCachedUpdate();

                var client = new WebClient()
                {
                    CachePolicy = new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.BypassCache),
                    Encoding = Encoding.UTF8,
                };
                client.Headers[HttpRequestHeader.UserAgent] = GetUserAgent();
                var updateUri = new Uri(Settings.URL);
                var updateData = client.DownloadString(updateUri);
                LatestUpdate = JsonConvert.DeserializeObject<Update>(updateData);
                LatestUpdateError = null;

                CacheUpdate(updateData);
            }
            catch (Exception error)
            {
                // This could be a problem deserializing the LastUpdate or fetching/deserializing the new update. It doesn't really matter, we record an error.
                LatestUpdate = null;
                LatestUpdateError = error;
                Trace.WriteLine(error);

                ResetCachedUpdate();
            }
        }

        public void Prepare()
        {
            if (LatestUpdate == null) throw new InvalidOperationException("Cannot get update when no LatestUpdate exists.");
            try
            {
                TestUpdateWrites();
                CleanDirectories();
                DownloadUpdate();
                ExtractUpdate();
            }
            catch (Exception error)
            {
                UpdateError = error;
                return;
            }
        }

        public bool Apply()
        {
            try
            {
                if (UpdateIsReady())
                {
                    ApplyUpdate();
                    return true;
                }
            }
            catch (Exception error)
            {
                UpdateError = error;
            }
            return false;
        }

        public void Clean()
        {
            try
            {
                CleanDirectories();
            }
            catch (Exception error)
            {
                UpdateError = error;
                return;
            }
        }

        string GetUserAgent()
        {
            return String.Format("{0}/{1}", ProductName, ProductVersion);
        }

        void ResetCachedUpdate()
        {
            State.LastCheck = DateTime.Now;
            // So what we're doing here is rounding up the DateTime (LastCheck) to the next TimeSpan (TTL) period. For
            // example, if the TTL was 1 hour, we'd round up the the start of the next hour. Similarly, if the TTL was
            // 1 day, we'd round up to midnight (the start of the next day). The purpose of this is to avoid 2 * TTL 
            // checking which might well occur if you always launch Open Rails around the same time of day each day -
            // if they launch it at 6:00PM on Monday, then 5:30PM on Tuesday, they won't get an update chech on
            // Tuesday. With the time rounding, they should get one check/day if the TTL is 1 day and they open it
            // every day. (This is why BaseDateTimeMidnightLocal uses the local midnight!)
            State.NextCheck = Settings.TTL.TotalMinutes > 1 ? BaseDateTimeMidnightLocal.AddSeconds(Math.Ceiling((State.LastCheck - BaseDateTimeMidnightLocal).TotalSeconds / Settings.TTL.TotalSeconds) * Settings.TTL.TotalSeconds) : State.LastCheck + TimeSpan.FromMinutes(1);
            State.Update = "";
            State.Save();
        }

        void CacheUpdate(string updateData)
        {
            State.Update = updateData;
            State.Save();
        }

        string PathUpdateTest { get { return Path.Combine(BasePath, "UpdateTest"); } }
        string PathUpdateDirty { get { return Path.Combine(BasePath, "UpdateDirty"); } }
        string PathUpdateStage { get { return Path.Combine(BasePath, "UpdateStage"); } }
        string FileUpdateStage { get { return Path.Combine(PathUpdateStage, "Update.zip"); } }
        string FileUpdateStageIsReady { get { return Path.Combine(PathUpdateStage, "OpenRails.exe"); } }
        string FileSettings { get { return Path.Combine(BasePath, "OpenRails.ini"); } }

        void TestUpdateWrites()
        {
            Directory.CreateDirectory(PathUpdateTest);
            Directory.Delete(PathUpdateTest, true);
        }

        void CleanDirectories()
        {
            if (Directory.Exists(PathUpdateDirty))
                Directory.Delete(PathUpdateDirty, true);

            if (Directory.Exists(PathUpdateStage))
                Directory.Delete(PathUpdateStage, true);
        }

        void DownloadUpdate()
        {
            if (!Directory.Exists(PathUpdateStage))
                Directory.CreateDirectory(PathUpdateStage);

            var updateUri = new Uri(Settings.URL);
            var uri = new Uri(updateUri, LatestUpdate.Url);
            var client = new WebClient();
            client.Headers[HttpRequestHeader.UserAgent] = GetUserAgent();
            client.DownloadFile(uri, FileUpdateStage);
        }

        void ExtractUpdate()
        {
            using (var zip = ZipFile.Read(FileUpdateStage))
                zip.ExtractAll(PathUpdateStage, ExtractExistingFileAction.OverwriteSilently);

            File.Delete(FileUpdateStage);
        }

        bool UpdateIsReady()
        {
            // The staging directory must exist, contain OpenRails.exe (be ready) and NOT contain the update zip.
            return Directory.Exists(PathUpdateStage)
                && File.Exists(FileUpdateStageIsReady)
                && !File.Exists(FileUpdateStage);
        }

        void ApplyUpdate()
        {
            // Create dirty directory for old version.
            if (!Directory.Exists(PathUpdateDirty))
                Directory.CreateDirectory(PathUpdateDirty);

            // Copy (almost) all files from current version to dirty.
            foreach (var file in Directory.GetFiles(BasePath))
                if (!file.Equals(FileSettings, StringComparison.OrdinalIgnoreCase))
                    File.Move(file, Path.Combine(PathUpdateDirty, Path.GetFileName(file)));

            // Copy (almost) all directories from current version to dirty.
            foreach (var directory in Directory.GetDirectories(BasePath))
                if (!directory.Equals(PathUpdateDirty, StringComparison.OrdinalIgnoreCase) && !directory.Equals(PathUpdateStage, StringComparison.OrdinalIgnoreCase))
                    Directory.Move(directory, Path.Combine(PathUpdateDirty, Path.GetFileName(directory)));

            // Copy all files from new version to base path.
            foreach (var file in Directory.GetFiles(PathUpdateStage))
                File.Move(file, Path.Combine(BasePath, Path.GetFileName(file)));

            // Copy all directories from new version to base path.
            foreach (var directory in Directory.GetDirectories(PathUpdateStage))
                Directory.Move(directory, Path.Combine(BasePath, Path.GetFileName(directory)));

            // Forcing a save of the state adds back this information to the new "Updater.ini" file, without overwriting the new updater settings.
            State.Save();
        }
    }

    public class Update
    {
        [JsonProperty]
        public DateTime Date { get; private set; }

        [JsonProperty]
        public string Url { get; private set; }

        [JsonProperty]
        public string Version { get; private set; }
    }
}
