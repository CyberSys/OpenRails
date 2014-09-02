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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.IO;
using System.ComponentModel;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using ORTS.Settings;

namespace Orts.Common.Scripting
{
    public class KeyMapFile
    {
        public static JsonSerializerSettings KeyMapSerializerSettings = new JsonSerializerSettings() 
        { 
            DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate,
            Converters = { new StringEnumConverter() },
        };

        public static List<KeyMapCommand> Load(string fileName)
        {
            List<KeyMapCommand> KeyMap;

            var streamReader = new StreamReader(fileName);
            try
            {
                KeyMap = JsonConvert.DeserializeObject<List<KeyMapCommand>>(streamReader.ReadToEnd(), KeyMapSerializerSettings);
            }
            catch (Exception error)
            {
                Trace.TraceWarning("Keymap loading failed: {0} in file {1}", error.Message, fileName);
                KeyMap = null;
            }
            streamReader.Close();
            return KeyMap;
        }
    }

    public class KeyMapCommand
    {
        [JsonProperty]
        public string Name { get; private set; }

        [JsonProperty]
        public int ScanCode { get; private set; }

        [JsonProperty]
        [DefaultValue(1)]
        public int Index { get; private set; }

        [JsonProperty]
        [DefaultValue(new KeyModifiers[] { KeyModifiers.None })]
        public KeyModifiers[] Modifiers { get; private set; }

        [JsonProperty]
        [DefaultValue(ButtonStates.Pressed)]
        public ButtonStates ButtonState { get; private set; }

        [JsonProperty]
        [DefaultValue(Events.ChangeTo)]
        public Events Event { get; private set; }

        [JsonProperty]
        [DefaultValue(float.MinValue)]
        public float Value { get; private set; }

        [JsonProperty]
        [DefaultValue(float.MaxValue)]
        public float ToValue { get; private set; }

        [JsonProperty]
        [DefaultValue(0)]
        public int SoundTrigger { get; private set; }

        public enum Events
        {
            ChangeTo,
            StartContinuousChange,
            StopContinuousChange,
        }

        public enum ButtonStates
        {
            Released,
            Pressed,
        }
    }
}
