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
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Orts.Formats.Msts;
using ORTS.Common;
using ORTS.Scripting.Api;
using Orts.Simulation;
using Orts.Simulation.RollingStocks;
using ORTS.Settings;

namespace Orts.Common.Scripting
{
    public enum ORTSControlType
    {
        // Generally, apart from exceptions, index 0 means all cars in the train except current, 1 means current car.
        //
        // Get or set controller (defined in eng file) positions directly. Should rarely be needed to set.
        ORTSDirection, // value -1: backwards, 0: neutral, 1: forwards
        ORTSThrottle, // value 0-1
        ORTSDynamicBrake, // value 0-1
        ORTSEngineBrake, // value 0-1
        ORTSTrainBrake, // value 0-1

        // Controller interventions without actually moving the levers.
        // Can be used for e.g. automatic speed control (like AFB) or train protection brake intervention
        ORTSThrottleIntervention, // value -1: off, 0-1: active
        ORTSDynamicBrakeIntervention, // value -1:off, 0-1: active
        ORTSEngineBrakeIntervention, // value -1: off, 0: neutral, 1: full service, 2: emergency
        ORTSTrainBrakeIntervention, // value -1: off, 0: neutral, 1: full service, 2: emergency

        // Subsystems keyboard commands already exist for
        ORTSPantograph, // index is the pantograph number
        ORTSBailOff,
        ORTSInitializeBrakes,
        ORTSHandbrake,
        ORTSTrainRetainers,
        ORTSBrakeHoseConnect,
        ORTSSander,
        ORTSWiper,
        ORTSHorn,
        ORTSBell,
        ORTSHeadLight,
        ORTSCabLight,
        ORTSPowerOn, // index: dieselEngine nr.; value 0: stopped, 1: starting, 2: running, 3: stopping
        ORTSMirror,
        ORTSDoorLeft,
        ORTSDoorRight,
        ORTSCylinderCock,
        ORTSOdoMeter, // index 0: head, 1: tail

        // Subsystems no dedicated keyboard assigned for
        ORTSAuxPowerOn,
        ORTSPowerAuthorization,
        ORTSCircuitBraker,
        ORTSCompressor,
        ORTSAcceptRemoteControlSignals,

        // Physics parameters
        ORTSSpeedMpS, // read-only
        ORTSDistanceM, // read-only
        ORTSClockTimeS, // read-only
        ORTSBrakeResevoirPressureBar, // read-only, index 0: vacuum, 1: cylinder, 2: main reservoir
        ORTSBrakeLinePressureBar, // index 0: vacuum, 1: main, 2: equalising, 3: engine brake, 4: EP control

        // Train protection
        ORTSAlerterButton,
        ORTSEmergencyPushButton,
        ORTSVigilanceAlarm,
        ORTSMonitoringState, // index is 0
        ORTSInterventionSpeedMpS, // index 0: release, 1: apply

        // Signalling data
        // For these index 0: current, x: next x
        ORTSSignalAspect,
        ORTSSignalSpeedLimitMpS,
        ORTSSignalDistanceM,
        ORTSPostSpeedLimitMpS,
        ORTSPostDistanceM,

        // Sound triggers, write-only
        ORTSDiscreteTrigger, // value is the trigger number
    }

    public class ContentScript
    {
        // Control types to be used by functions GetControlValue, SetControlValue:
        //
        // 1. OpenRails keyboard commands
        //      - Defined in UserCommands enum, e.g. ControlThrottleStartIncrease, ControlHorn
        //      - Index: always 1
        //      - By taking over control, the automatic execution upon keypress/release is disabled, an event is signalled to the script instead.
        //      - SetControlValue: Execute the command assigned to keypress (1) or key release (0), or take over control from core (2).
        //      - GetControlValue: Return result of IsDown method, whether key is released/pressed (0/1).
        //      -
        //      - Seems redundant to redefining the key as a custom command in keymap.json, and listening to the custom events...
        //          Although in this case a code would be needed to search for the command assigned to the key, and disable it.
        //
        // 2. Custom keyboard commands
        //      - Defined in keymap.json.
        //      - Index: always 1
        //      - When the assigned key is pressed or released, an event is signalled to the script to handle.
        //      - Can be any string except the built-in MSTS control names or the ones starting with "Control" or "ORTS".
        //      - SetControlValue: Force signalling an event of press/release to the script.
        //      - GetControlValue: Return result of IsDown method, whether key is pressed/released.
        //
        // 3. MSTS cabview controls
        //      - Defined in CABViewControlTypes enum, e.g. WIPERS, HORN, AMMETER.
        //      - Index: always 1
        //      - By taking over control the displayed value in cabview will be the one set by the script.
        //      - Can be used as an animation node name in 3D cabviews.
        //      - SetControlValue: Set overridden value to be displayed in cabview. Will stopped to be overridden by built-in data after first use.
        //      - GetControlValue: Get the data value of the original control, in units as configured in cvf. If unconfigured, get in SI units.
        //
        // 4. Additional OpenRails controls
        //      - Defined in ORTSControlType enum, e.g. ORTSSignalSpeedLimitMpS, ORTSSpeedMpS
        //      - Index: Some of them have index values other than 1.
        //      - Make interaction with OpenRails physics and signalling code possible.
        //      - Some of the controls are read-only, SetControlValue on these has no effect.
        //      - SetControlValue: Set the value of associated parameter, nothing special.
        //      - GetControlValue: Get the value of associated parameter, nothing special.
        //
        // 5. Custom controls
        //      - Defined with script RegisterControl call
        //      - Index: always 1
        //      - Can be any string except the built-in MSTS control names or the ones starting with "Control" or "ORTS".
        //      - Can be used as an animation node name in 3D cabviews.
        //      - Can be thought on it as a variable common to all scripts for a particular locomotive.
        //      - SetControlValue: Set the value of control. Custom name will be registered implicitly at first use.
        //      - GetControlValue: Get the value of control. Custom name will be registered implicitly at first use.
        //
        
        readonly MSTSLocomotive Locomotive;
        readonly Simulator Simulator;
        List<KeyMapCommand> KeyMap;

        private string ScriptName { get; set; }
        private string KeyMapFileName { get; set; }
        public string SoundFileName { get; private set; }
        ControllerScript Script;

        // Receive delegates from Viewer3D.UserInput
        public static Func<UserCommands, bool> UserInputIsDown;
        public static Func<UserCommandInput, bool> UserInputIsPressed;
        public static Func<UserCommandInput, bool> UserInputIsReleased;

        /// <summary>
        /// Command names the script wants to handle, assigned to their keyboard commands.
        /// It can either be a custom command name defined in keymap.json,
        /// or a built-in UserCommands "Control" name the execution was taken over by the script.
        /// In both cases an event is signalled to the script with command name, if associated key is pressed or released.
        /// </summary>
        public Dictionary<string, UserCommandInput> ScriptedCommands = new Dictionary<string, UserCommandInput>();
        /// <summary>
        /// Command names defined in keymap.json, assigned to their { released_command, pressed_command }.
        /// Only the custom command names are stored here.
        /// </summary>
        private Dictionary<string, List<KeyMapCommand>> KeyMapCommands = new Dictionary<string, List<KeyMapCommand>>();
        /// <summary>
        /// Control names the script wants to handle, assigned to a blank or to their cvf-defined cabview configuration.
        /// It can be either a custom control name, or a built-in MSTS cabview control name, for which the script wants to override the visible value.
        /// A name can be added here by RegisterControl script method. Custom controls will be commonly visible among scripts of a particular locomotive.
        /// </summary>
        public Dictionary<string, CabViewControl> ScriptedControls = new Dictionary<string, CabViewControl>();
        /// <summary>
        /// Control names defined in cvf file, assigned to their configuration object.
        /// </summary>
        private Dictionary<string, CabViewControl> ConfiguredControls = new Dictionary<string, CabViewControl>();
        /// <summary>
        /// Keymap commands actually changing continuously, with their changing speed [1/s]
        /// </summary>
        private Dictionary<KeyMapCommand, float> ChangingControls = new Dictionary<KeyMapCommand, float>();

        /// <summary>
        /// String -> enum lookup table for MSTS cabview control names to be used in GetControlValue("WIPERS", 0) style script methods,
        /// defined to avoid using code reflection beyond initialization.
        /// </summary>
        private static readonly Dictionary<string, UserCabViewControl> MSTSControlTypes = new Dictionary<string, UserCabViewControl>();
        /// <summary>
        /// String -> enum lookup table for additional cabview control names to be used in GetControlValue("ORTSSpeedMpS", 0) style script methods,
        /// defined to avoid using code reflection beyond initialization.
        /// </summary>
        private static readonly Dictionary<string, UserCabViewControl> ORTSControlTypes = new Dictionary<string, UserCabViewControl>();
        /// <summary>
        /// A string -> enum lookup table for UserCommands starting with "Control" (e.g. ControlThrottleIncrease),
        /// defined to avoid using code reflection beyond initialization.
        /// </summary>
        private static readonly Dictionary<string, UserCommands> ORTSKeyboardCommands = new Dictionary<string, UserCommands>();
        
        public ContentScript(MSTSLocomotive locomotive)
        {
            ScriptName = "TestScript.cs";
            SoundFileName = "TestSound.sms";
            KeyMapFileName = "TestKeyMap.json";

            Simulator = locomotive.Simulator;
            Locomotive = locomotive;

            if (MSTSControlTypes.Count == 0)
                foreach (var controlType in (CABViewControlTypes[])Enum.GetValues(typeof(CABViewControlTypes)))
                    MSTSControlTypes.Add(controlType.ToString(), new UserCabViewControl() { ControlType = controlType });

            if (ORTSControlTypes.Count == 0)
                foreach (var controlType in (ORTSControlType[])Enum.GetValues(typeof(ORTSControlType)))
                    ORTSControlTypes.Add(controlType.ToString(), new UserCabViewControl() { ORTSControlType = controlType });

            if (ORTSKeyboardCommands.Count == 0)
                foreach (var controlCommand in (UserCommands[])Enum.GetValues(typeof(UserCommands)))
                    ORTSKeyboardCommands.Add(controlCommand.ToString(), controlCommand);
        }

        public ContentScript(ContentScript contentScript) : this(contentScript.Locomotive)
        {
            ScriptName = contentScript.ScriptName;
            SoundFileName = contentScript.SoundFileName;
            KeyMapFileName = contentScript.KeyMapFileName;
        }

        public ContentScript Clone()
        {
            return new ContentScript(this);
        }

        /// <summary>
        /// Execute action on either the whole train, or just the current locomotive, based on scope parameter
        /// </summary>
        /// <typeparam name="T">vehicle type</typeparam>
        /// <param name="scope">0: train, 1: current locomotive</param>
        /// <param name="action">action to execute</param>
        private void TrainCarAction<T>(int scope, Action<T> action) where T : MSTSWagon
        {
            if (scope == 0 && Locomotive.Train != null)
            {
                foreach (var car in Locomotive.Train.Cars)
                    if (car != Locomotive && car is T && car.AcceptMUSignals)
                        action(car as T);
            }
            else if (scope == 1 && Locomotive is T)
                action(Locomotive as T);
        }

        public void Initialize()
        {
            var pathArray = new string[] { Path.Combine(Path.GetDirectoryName(Locomotive.WagFilePath), "Script") };
            Script = Simulator.ScriptManager.Load(pathArray, ScriptName) as ControllerScript;

            if (Script == null)
                Script = new DummyControllerScript();

            Script.SetControlValue = (controlName, index, value) => SetControlValue(controlName, index, value);
            Script.GetControlValue = (controlName, index) => GetControlValue(controlName, index);
            
            foreach (var cabViewList in Locomotive.CabViewList)
                foreach (var cabViewControl in cabViewList.CVFFile.CabViewControls)
                    if (!ConfiguredControls.ContainsKey(cabViewControl.ControlType.ToString()))
                        ConfiguredControls.Add(cabViewControl.ControlType.ToString(), cabViewControl);
            
            LoadKeyMap();

            Script.Initialize();
        }
        
        private void LoadKeyMap()
        {
            if (KeyMapFileName == null)
                return;
            
            var keyMapPathArray = new[] {
                Path.Combine(Path.GetDirectoryName(Locomotive.WagFilePath), "Script"),
            };
            var keyMapPath = ORTSPaths.GetFileFromFolders(keyMapPathArray, KeyMapFileName);
            if (File.Exists(keyMapPath))
                KeyMap = KeyMapFile.Load(keyMapPath);

            if (KeyMap == null || KeyMap.Count == 0)
                return;

            // Put an existing command onto an other key, or just register the custom command name
            foreach (var command in KeyMap)
            {
                var modifiers = KeyModifiers.None;
                foreach (var modifier in command.Modifiers)
                    modifiers |= modifier;
                var userCommandKeyInput = new UserCommandKeyInput(command.ScanCode, modifiers);

                if (ORTSKeyboardCommands.ContainsKey(command.Name))
                    Locomotive.Simulator.Settings.Input.Commands[(int)ORTSKeyboardCommands[command.Name]] = userCommandKeyInput;
                else
                {
                    ScriptedCommands.Add(CommandKey(command), userCommandKeyInput);

                    if (!KeyMapCommands.ContainsKey(command.Name))
                        KeyMapCommands.Add(command.Name, new List<KeyMapCommand>());

                    KeyMapCommands[command.Name].Add(command);
                    DisableByKey(userCommandKeyInput);
                }
            }
        }

        /// <summary>
        /// Search in built-in keymap for key combination, and disable if found
        /// </summary>
        public void DisableByKey(UserCommandKeyInput input)
        {
            foreach (var command in ORTSKeyboardCommands.Values)
            {
                var commandKeyInput = Locomotive.Simulator.Settings.Input.Commands[(int)command] as UserCommandKeyInput;
                if (commandKeyInput != null)
                {
                    if (commandKeyInput.ScanCode == input.ScanCode
                        && commandKeyInput.Control == input.Control
                        && commandKeyInput.Alt == input.Alt
                        && commandKeyInput.Shift == input.Shift)
                    {
                        RegisterControl(command.ToString(), 1);
                        break;
                    }
                }
            }
        }

        public class UserCabViewControl : CabViewControl
        {
            public ORTSControlType ORTSControlType;

            public UserCabViewControl() { }
        }

        /// <summary>
        /// Called to define a new custom control or command, or override a built-in MSTS cabview control value, or disable a built-in keyboard command
        /// </summary>
        private void RegisterControl(string controlName, int index)
        {
            if (controlName.Substring(0, 4) == "ORTS") return;
            if (ScriptedControls.ContainsKey(controlName)) return;
            if (ScriptedCommands.ContainsKey(controlName)) return;

            if (ORTSKeyboardCommands.ContainsKey(controlName))
            {
                // Disable original UserCommand, not to execute automatically, but signal an event to the script instead.
                ScriptedCommands.Add(controlName, Locomotive.Simulator.Settings.Input.Commands[(int)ORTSKeyboardCommands[controlName]]);
            }
            else
            {
                ScriptedControls.Add(controlName, ConfiguredControls.ContainsKey(controlName) ? ConfiguredControls[controlName] : new UserCabViewControl() { MinValue = double.MinValue, MaxValue = double.MaxValue });
            }
        }
        
        private void UnregisterControl(string controlName, int index)
        {
            if (!ScriptedCommands.ContainsKey(controlName)) return;

            if (ORTSKeyboardCommands.ContainsKey(controlName) && index == 1)
                ScriptedCommands.Remove(controlName);
        }

        /// <summary>
        /// Get the value of a control. Getting a value is possible only for the current locomotive,
        /// thus use the method with index = 1 generally, apart from exceptions.
        /// </summary>
        /// <param name="controlName">Case sensitive name of the control</param>
        /// <param name="index">0: train, 1: current locomotive</param>
        public static float GetControlValue(MSTSLocomotive locomotive, string controlName, int index)
        {
            if (ORTSControlTypes.ContainsKey(controlName))
            {
                if (locomotive is MSTSSteamLocomotive)
                {
                    var steamLocomotive = locomotive as MSTSSteamLocomotive;
                    switch (ORTSControlTypes[controlName].ORTSControlType)
                    {
                        case ORTSControlType.ORTSCylinderCock: return steamLocomotive.CylinderCocksAreOpen ? 1 : 0;
                    }
                }
                else if (locomotive is MSTSElectricLocomotive)
                {
                    var electricLocomotive = locomotive as MSTSElectricLocomotive;
                    switch (ORTSControlTypes[controlName].ORTSControlType)
                    {
                        case ORTSControlType.ORTSCircuitBraker: return index != 1 ? 0 : electricLocomotive.PowerSupply.CircuitBreaker.State == CircuitBreakerState.Closed ? 1 : 0;
                    }
                }
                switch (ORTSControlTypes[controlName].ORTSControlType)
                {
                    case ORTSControlType.ORTSThrottle: return index != 1 ? 0 : locomotive.ThrottleController.CurrentValue;
                    case ORTSControlType.ORTSDynamicBrake: return index != 1 ? 0 : locomotive.DynamicBrakeController.CurrentValue;
                    case ORTSControlType.ORTSEngineBrake: return index != 1 ? 0 : locomotive.EngineBrakeController.CurrentValue;
                    case ORTSControlType.ORTSTrainBrake: return index != 1 ? 0 : locomotive.TrainBrakeController.CurrentValue;
                    case ORTSControlType.ORTSDirection: return index != 1 ? 0 : locomotive.Direction == Direction.Reverse ? 0 : locomotive.Direction == Direction.N ? 1 : 2;
                    case ORTSControlType.ORTSThrottleIntervention: return index != 1 ? 0 : locomotive.ThrottleIntervention;
                    case ORTSControlType.ORTSDynamicBrakeIntervention: return index != 1 ? 0 : locomotive.DynamicBrakeIntervention;
                    case ORTSControlType.ORTSEngineBrakeIntervention: return index != 1 ? 0 : locomotive.EngineBrakeIntervention;
                    case ORTSControlType.ORTSTrainBrakeIntervention: return index != 1 ? 0 : locomotive.TrainBrakeIntervention;
                    case ORTSControlType.ORTSPowerOn:
                        var dieselLocomotive = locomotive as MSTSDieselLocomotive;
                        if (dieselLocomotive == null)
                            return locomotive.PowerOn ? 1 : 0;
                        else
                            return index >= 0 && index < dieselLocomotive.DieselEngines.Count ? 0 : (int)dieselLocomotive.DieselEngines[index].EngineStatus;
                    case ORTSControlType.ORTSAuxPowerOn: return index != 1 ? 0 : locomotive.AuxPowerOn ? 1 : 0;
                    case ORTSControlType.ORTSCompressor: return index != 1 ? 0 : locomotive.CompressorIsOn ? 1 : 0;
                    case ORTSControlType.ORTSPowerAuthorization:
                        return index != 1 ? 0 : (locomotive.Train != null ? locomotive.Train.LeadLocomotive as MSTSLocomotive : locomotive).TrainControlSystem.PowerAuthorization ? 1 : 0;
                    case ORTSControlType.ORTSPantograph:
                        if (locomotive.Pantographs[index] == null) return 0;
                        switch (locomotive.Pantographs[index].State)
                        {
                            case PantographState.Down: return 0;
                            case PantographState.Lowering: return 1;
                            case PantographState.Raising: return 2;
                            case PantographState.Up: return 3;
                        }
                        break;
                    case ORTSControlType.ORTSBailOff: return index != 1 ? 0 : locomotive.BailOff ? 1 : 0;
                    case ORTSControlType.ORTSHandbrake: return index != 1 ? 0 : locomotive.BrakeSystem.GetHandbrakeStatus() ? 1 : 0;
                    case ORTSControlType.ORTSTrainRetainers: return index != 1 ? 0 : locomotive.Train.RetainerPercent / 100;
                    case ORTSControlType.ORTSBrakeHoseConnect: return index != 1 ? 0 : locomotive.BrakeSystem.BrakeLine1PressurePSI < 0 ? 0 : 1;
                    case ORTSControlType.ORTSSander: return index != 1 ? 0 : locomotive.Sander ? 1 : 0;
                    case ORTSControlType.ORTSWiper: return index != 1 ? 0 : locomotive.Wiper ? 1 : 0;
                    case ORTSControlType.ORTSHorn: return index != 1 ? 0 : locomotive.Horn ? 1 : 0;
                    case ORTSControlType.ORTSBell: return index != 1 ? 0 : locomotive.Bell ? 1 : 0;
                    case ORTSControlType.ORTSMirror: return index != 1 ? 0 : locomotive.MirrorOpen ? 1 : 0;
                    case ORTSControlType.ORTSAcceptRemoteControlSignals: return index != 1 ? 0 : locomotive.AcceptMUSignals ? 1 : 0;
                    case ORTSControlType.ORTSDoorLeft: return index != 1 ? 0 : locomotive.DoorLeftOpen ? 1 : 0;
                    case ORTSControlType.ORTSDoorRight: return index != 1 ? 0 : locomotive.DoorRightOpen ? 1 : 0;
                    case ORTSControlType.ORTSHeadLight: return index != 1 ? 0 : locomotive.Headlight;
                    case ORTSControlType.ORTSCabLight: return index != 1 ? 0 : locomotive.CabLightOn ? 1 : 0;
                    case ORTSControlType.ORTSAlerterButton: return index != 1 ? 0 : locomotive.TrainControlSystem.AlerterButtonPressed ? 1 : 0;
                    case ORTSControlType.ORTSEmergencyPushButton: return index != 1 ? 0 : locomotive.EmergencyButtonPressed ? 1 : 0;
                    case ORTSControlType.ORTSVigilanceAlarm: return index != 1 ? 0 : locomotive.AlerterSnd ? 1 : 0;
                    case ORTSControlType.ORTSMonitoringState: return index != 0 ? 0 : (float)locomotive.TrainControlSystem.MonitoringStatus;
                    case ORTSControlType.ORTSInterventionSpeedMpS: return index == 0 ? 0 : index == 1 ? locomotive.TrainControlSystem.InterventionSpeedLimitMpS : 0;
                    case ORTSControlType.ORTSSpeedMpS: return index != 1 ? 0 : Math.Abs(locomotive.SpeedMpS);
                    case ORTSControlType.ORTSDistanceM: return index != 1 ? 0 : locomotive.DistanceM;
                    case ORTSControlType.ORTSClockTimeS: return index != 1 ? 0 : (float)locomotive.Simulator.ClockTime;
                    case ORTSControlType.ORTSOdoMeter: return index == 0 ? locomotive.OdometerM : index == 1 && locomotive.Train != null ? locomotive.OdometerM - locomotive.Train.Length : 0;
                    case ORTSControlType.ORTSBrakeLinePressureBar:
                        if (locomotive.Train != null)
                            switch (index)
                            {
                                case 0: return -Bar.FromInHg(locomotive.Train.BrakeLine1PressurePSIorInHg);
                                case 1: return Bar.FromPSI(locomotive.Train.BrakeLine1PressurePSIorInHg);
                                case 2: return Bar.FromPSI(locomotive.Train.BrakeLine2PressurePSI);
                                case 3: return Bar.FromPSI(locomotive.Train.BrakeLine3PressurePSI);
                                case 4: return locomotive.Train.BrakeLine4;
                            }
                        return float.MaxValue;
                    case ORTSControlType.ORTSBrakeResevoirPressureBar:
                        switch (index)
                        {
                            case 0: return -Bar.FromPSI(locomotive.BrakeSystem.GetVacResPressurePSI());
                            case 1: return Bar.FromPSI(locomotive.BrakeSystem.GetCylPressurePSI());
                            case 2: return Bar.FromPSI(locomotive.BrakeSystem.BrakeLine1PressurePSI);
                            default: return 0;
                        }
                    case ORTSControlType.ORTSSignalAspect: return locomotive.TrainControlSystem.SignalItem(index, ORTSControlType.ORTSSignalAspect);
                    case ORTSControlType.ORTSSignalSpeedLimitMpS: return locomotive.TrainControlSystem.SignalItem(index, ORTSControlType.ORTSSignalSpeedLimitMpS);
                    case ORTSControlType.ORTSSignalDistanceM: return locomotive.TrainControlSystem.SignalItem(index, ORTSControlType.ORTSSignalDistanceM);
                    case ORTSControlType.ORTSPostSpeedLimitMpS: return locomotive.TrainControlSystem.SignalItem(index, ORTSControlType.ORTSPostSpeedLimitMpS);
                    case ORTSControlType.ORTSPostDistanceM: return locomotive.TrainControlSystem.SignalItem(index, ORTSControlType.ORTSPostDistanceM);
                    default: return 0;
                }
                return 0;
            }
            else
                return float.MinValue;
        }

        /// <summary>
        /// Get the value of a control. Getting a value is possible only for the current locomotive,
        /// thus use the method with index = 1 generally, apart from exceptions.
        /// </summary>
        /// <param name="controlName">Case sensitive name of the control</param>
        /// <param name="index">0: train, 1: current locomotive</param>
        public float GetControlValue(string controlName, int index)
        {
            if (ORTSControlTypes.ContainsKey(controlName))
                return GetControlValue(Locomotive, controlName, index);
                
            // Make the original data available for scripts in first place, even if the control is taken over!
            if (ConfiguredControls.ContainsKey(controlName)) return index != 1 ? 0 : Locomotive.GetDataOf(ConfiguredControls[controlName]);
            else if (MSTSControlTypes.ContainsKey(controlName)) return index != 1 ? 0 : Locomotive.GetDataOf(MSTSControlTypes[controlName]);
            else if (ScriptedControls.ContainsKey(controlName)) return (float)ScriptedControls[controlName].OldValue;
            else if (ORTSKeyboardCommands.ContainsKey(controlName)) return (UserInputIsDown(ORTSKeyboardCommands[controlName])) ? 1 : 0;
            else
            {
                // First use of a custom control, register implicitly
                RegisterControl(controlName, index);
                return 0;
            }
        }
        
        public void SetControlValue(string controlName, int index, float value)
        {
            if (ORTSControlTypes.ContainsKey(controlName))
            {
                switch (ORTSControlTypes[controlName].ORTSControlType)
                {
                    // Board computer doesn't normally move levers, just overrides their settings
                    case ORTSControlType.ORTSThrottle: if (index == 1) Locomotive.ThrottleController.SetValue(MathHelper.Clamp(value, 0, 1)); break;
                    case ORTSControlType.ORTSDynamicBrake: if (index == 1) Locomotive.DynamicBrakeController.SetValue(MathHelper.Clamp(value, -1, 1)); break;
                    case ORTSControlType.ORTSEngineBrake: if (index == 1) Locomotive.EngineBrakeController.SetValue(MathHelper.Clamp(value, 0, 1)); break;
                    case ORTSControlType.ORTSTrainBrake: if (index == 1) Locomotive.TrainBrakeController.SetValue(MathHelper.Clamp(value, 0, 1)); break;
                    case ORTSControlType.ORTSDirection: if (index == 1) Locomotive.SetDirection(value > 1 ? Direction.Forward : value < 1 ? Direction.Reverse : Direction.N); break;
                    case ORTSControlType.ORTSThrottleIntervention: TrainCarAction<MSTSLocomotive>(index, l => l.ThrottleIntervention = MathHelper.Clamp(value, -1, 1)); break;
                    case ORTSControlType.ORTSDynamicBrakeIntervention: TrainCarAction<MSTSLocomotive>(index, l => l.DynamicBrakeIntervention = MathHelper.Clamp(value, -1, 1)); break;
                    case ORTSControlType.ORTSEngineBrakeIntervention: TrainCarAction<MSTSLocomotive>(index, l => l.EngineBrakeIntervention = (int)MathHelper.Clamp(value, -1, 2)); break;
                    case ORTSControlType.ORTSTrainBrakeIntervention: TrainCarAction<MSTSLocomotive>(index, l => l.TrainBrakeIntervention = (int)MathHelper.Clamp(value, -1, 2)); break;
                    case ORTSControlType.ORTSDiscreteTrigger: TrainCarAction<MSTSLocomotive>(index, l => HandleEvent(l.EventHandlers, (int)value)); break;
                    case ORTSControlType.ORTSPowerOn: TrainCarAction<MSTSLocomotive>(index, l => l.SetPower(value > 0)); break;
                    case ORTSControlType.ORTSCircuitBraker: TrainCarAction<MSTSLocomotive>(index, l => l.SignalEvent(value > 0 ? PowerSupplyEvent.CloseCircuitBreaker : PowerSupplyEvent.OpenCircuitBreaker, index)); break;
                    case ORTSControlType.ORTSPowerAuthorization: TrainCarAction<MSTSLocomotive>(index, l => l.TrainControlSystem.SetPowerAuthorization(value > 0)); break;
                    case ORTSControlType.ORTSAuxPowerOn: TrainCarAction<MSTSElectricLocomotive>(index, l => l.PowerSupply.AuxiliaryState = value > 0 ? PowerSupplyState.PowerOn : PowerSupplyState.PowerOff); break;
                    case ORTSControlType.ORTSCompressor: if (index == 1) Locomotive.SignalEvent(value > 0 ? Event.CompressorOn : Event.CompressorOff); break;
                    case ORTSControlType.ORTSPantograph: Locomotive.SignalEvent(value > 0 ? PowerSupplyEvent.RaisePantograph : PowerSupplyEvent.LowerPantograph, index); break;
                    case ORTSControlType.ORTSBailOff: if (index == 1) Locomotive.SetBailOff(value > 0); break;
                    case ORTSControlType.ORTSInitializeBrakes: if (index == 0 && value == 1 && Locomotive.Train != null) Locomotive.Train.UnconditionalInitializeBrakes(); break;
                    case ORTSControlType.ORTSHandbrake: TrainCarAction<MSTSWagon>(index, l => l.BrakeSystem.SetHandbrakePercent(value * 100)); break;
                    case ORTSControlType.ORTSTrainRetainers: if (index == 0) Locomotive.SetTrainRetainers(value > 0); break;
                    case ORTSControlType.ORTSBrakeHoseConnect: if (index == 1) Locomotive.BrakeHoseConnect(value > 0); break;
                    case ORTSControlType.ORTSSander: TrainCarAction<MSTSLocomotive>(index, l => l.SignalEvent(value > 0 ? Event.SanderOn : Event.SanderOff)); break;
                    case ORTSControlType.ORTSWiper: if (index == 1) Locomotive.SignalEvent(value > 0 ? Event.WiperOn : Event.WiperOff); break;
                    case ORTSControlType.ORTSHorn: if (index == 1) Locomotive.SignalEvent(value > 0 ? Event.HornOn : Event.HornOff); break;
                    case ORTSControlType.ORTSBell: if (index == 1) Locomotive.SignalEvent(value > 0 ? Event.BellOn : Event.BellOff); break;
                    case ORTSControlType.ORTSHeadLight: if (index == 1) Locomotive.Headlight = (int)MathHelper.Clamp(value, 0, 2); break;
                    case ORTSControlType.ORTSCabLight: if (index == 1) Locomotive.CabLightOn = value > 0; break;
                    case ORTSControlType.ORTSAlerterButton: if (index == 1) { Locomotive.AlerterPressed(value > 0); if (value > 0) Locomotive.SignalEvent(Event.VigilanceAlarmReset); } break;
                    case ORTSControlType.ORTSEmergencyPushButton: if (index == 1) { Locomotive.EmergencyButtonPressed = !Locomotive.EmergencyButtonPressed; Locomotive.TrainBrakeController.EmergencyBrakingPushButton = Locomotive.EmergencyButtonPressed; } break;
                    case ORTSControlType.ORTSVigilanceAlarm: if (index == 1) Locomotive.SignalEvent(value > 0 ? Event.VigilanceAlarmOn : Event.VigilanceAlarmOff); break;
                    case ORTSControlType.ORTSMonitoringState: if (index == 0) Locomotive.TrainControlSystem.MonitoringStatus = (MonitoringStatus)(int)MathHelper.Clamp(value, 0, 5); break;
                    case ORTSControlType.ORTSInterventionSpeedMpS: if (index == 0) { } else if (index == 1) Locomotive.TrainControlSystem.InterventionSpeedLimitMpS = value; break;
                    case ORTSControlType.ORTSSignalAspect: if (index == 0) Locomotive.TrainControlSystem.CabSignalAspect = (TrackMonitorSignalAspect)(int)MathHelper.Clamp(value, 0, 9); break;
                    case ORTSControlType.ORTSSignalSpeedLimitMpS: if (index == 0) Locomotive.TrainControlSystem.CurrentSpeedLimitMpS = value; else if (index == 1) Locomotive.TrainControlSystem.NextSpeedLimitMpS = value; break;
                    case ORTSControlType.ORTSMirror: if (index == 1 && (value > 0 != Locomotive.MirrorOpen)) Locomotive.ToggleMirrors(); break;
                    case ORTSControlType.ORTSAcceptRemoteControlSignals: if (index == 1) Locomotive.AcceptMUSignals = value > 0; break;
                    case ORTSControlType.ORTSDoorLeft: TrainCarAction<MSTSWagon>(index, l => { if (l.DoorLeftOpen != value > 0) { l.DoorLeftOpen = value > 0; l.SignalEvent(value > 0 ? Event.DoorOpen : Event.DoorClose); } }); break;
                    case ORTSControlType.ORTSDoorRight: TrainCarAction<MSTSWagon>(index, l => { if (l.DoorRightOpen != value > 0) { l.DoorRightOpen = value > 0; l.SignalEvent(value > 0 ? Event.DoorOpen : Event.DoorClose); } }); break;
                    case ORTSControlType.ORTSCylinderCock: TrainCarAction<MSTSSteamLocomotive>(index, l => { if (l.CylinderCocksAreOpen != value > 0) l.ToggleCylinderCocks(); }); break;
                    case ORTSControlType.ORTSOdoMeter: if (index == 0 && value == 0) Locomotive.OdometerReset(); break;
                    case ORTSControlType.ORTSBrakeLinePressureBar:
                        if (Locomotive.Train != null && Locomotive.IsLeadLocomotive())
                            switch (index)
                            {
                                case 0: Locomotive.Train.BrakeLine1PressurePSIorInHg = -Bar.ToInHg(value); break;
                                case 1: Locomotive.Train.BrakeLine1PressurePSIorInHg = Bar.ToPSI(value); break;
                                case 2: Locomotive.Train.BrakeLine2PressurePSI = Bar.ToPSI(value); break;
                                case 3: Locomotive.Train.BrakeLine3PressurePSI = Bar.ToPSI(value); break;
                                case 4: Locomotive.Train.BrakeLine4 = value; break;
                            }
                        break;
                }
                return;
            }
            
            // Enable/disable built-in keyboard commands
            if (ORTSKeyboardCommands.ContainsKey(controlName))
            {
                if (index == 1)
                {
                    if (value == 0)
                        RegisterControl(controlName, index);
                    else
                        UnregisterControl(controlName, index);
                }
                return;
            }

            var scriptedControl = ScriptedControls.ContainsKey(controlName);
            
            // First use of a built-in MSTS cabcontrol, register implicitly
            // for being able to override the calculated value.
            if (!scriptedControl && MSTSControlTypes.ContainsKey(controlName))
            {
                RegisterControl(controlName, index);
                scriptedControl = ScriptedControls.ContainsKey(controlName);
            }
            
            // First use of a custom control, register implicitly.
            // Also register ScriptedCommands, so that their values could be managed.
            if (!scriptedControl)
            {
                RegisterControl(controlName, index);
                scriptedControl = ScriptedControls.ContainsKey(controlName);
            }

            if (scriptedControl)
            {
                if (index == 0)
                    TrainCarAction<MSTSLocomotive>(0, l => SignalEvent(l.ContentScripts, controlName, value));
                else if (index == 1)
                    ScriptedControls[controlName].OldValue = MathHelper.Clamp(value, (float)ScriptedControls[controlName].MinValue, (float)ScriptedControls[controlName].MaxValue);
                return;
            }
        }

        /// <summary>
        /// Return the override value for a built-in MSTS cabcontrol.
        /// </summary>
        public float GetScriptedDataOf(CabViewControl cvc)
        {
            if (ScriptedControls.ContainsKey(cvc.ControlType.ToString()))
                return (float)ScriptedControls[cvc.ControlType.ToString()].OldValue;
            
            return Locomotive.GetDataOf(cvc);
        }
        
        public void Update(float elapsedSeconds)
        {
            foreach (var command in ChangingControls.Keys)
            {
                var oldValue = GetControlValue(command.Name, 1);
                var speed = ChangingControls[command];
                var newValue = oldValue + speed * elapsedSeconds;
                newValue = speed > 0 ? Math.Min(newValue, command.ToValue) : Math.Max(newValue, command.ToValue);

                if (newValue != oldValue)
                {
                    TrainCarAction<MSTSLocomotive>(1, l => SignalEvent(l.ContentScripts, command.Name, newValue));
                    SetControlValue(command.Name, 1, newValue);
                }
            }
            
            if (Script != null)
                Script.Update(elapsedSeconds);

            foreach (var aaa in Locomotive.Train.Cars)
                Console.WriteLine("___{0} {1} {2}", aaa.CarID, (aaa as MSTSWagon).DoorLeftOpen, (aaa as MSTSWagon).DoorRightOpen);
        }

        /// <summary>
        /// Handle sound triggers
        /// </summary>
        public static void HandleEvent(List<EventHandler> eventHandlers,int customEventID)
        {
            foreach (var eventHandler in eventHandlers)
                eventHandler.HandleEvent((Event)customEventID);
            //eventHandler.HandleEvent((Event)customEventID, script);
        }

        public static int SignalEvent(List<ContentScript> scripts, string controlName, float? value)
        {
            var takenOver = 0;
            foreach (var script in scripts)
            {
                if ((script.ScriptedCommands.ContainsKey(controlName) || script.ScriptedControls.ContainsKey(controlName))
                    && script.Script != null)
                {
                    script.Script.HandleEvent(controlName, value);
                    takenOver = 1;
                }
            }
            return takenOver;
        }

        public void Save(BinaryWriter outf)
        {
            //outf.Write(CurrentValue);
        }

        public void Restore(BinaryReader inf)
        {
            //SendEvent(BrakeControllerEvent.SetCurrentValue, inf.ReadSingle());
        }

        private static string CommandKey(KeyMapCommand command)
        {
            return command.GetHashCode().ToString();
        }

        private Dictionary<string, KeyMapCommand> ActivatedKeyMapCommands = new Dictionary<string, KeyMapCommand>();
        
        /// <summary>
        // Called from MSTSLocomotiveViewer.HandleUserInput. Handles custom commands only.
        /// </summary>
        public void HandleUserInput(ElapsedTime elapsedTime)
        {
            if (KeyMap == null || KeyMap.Count == 0)
                return;

            ActivatedKeyMapCommands.Clear();

            foreach (var command in KeyMap)
            {
                if (ORTSKeyboardCommands.ContainsKey(command.Name)
                    || ActivatedKeyMapCommands.ContainsKey(command.Name)
                    || command.ButtonState == KeyMapCommand.ButtonStates.Pressed && !UserInputIsPressed(ScriptedCommands[CommandKey(command)])
                    || command.ButtonState == KeyMapCommand.ButtonStates.Released && !UserInputIsReleased(ScriptedCommands[CommandKey(command)])
                    || command.Event == KeyMapCommand.Events.ChangeTo && command.Value != float.MinValue && command.Value != GetControlValue(command.Name, command.Index)
                    || command.Event == KeyMapCommand.Events.StartContinuousChange && ChangingControls.ContainsKey(command)
                    || command.Event == KeyMapCommand.Events.StopContinuousChange && !ChangingControls.ContainsKey(command))
                    continue;
                
                ActivatedKeyMapCommands.Add(command.Name, command);
            }

            // Must be rolled through the command pattern for the replay to stay working correctly
            foreach (var command in ActivatedKeyMapCommands.Values)
            {
                new ScriptedControlCommand(Simulator.Log, command);
            }
        }
        
        /// <summary>
        /// When a key of a custom command defined in keymap was pressed, signal an event to the script and store the resulting value.
        /// </summary>
        public void Execute(KeyMapCommand command)
        {
            switch (command.Event)
            {
                case KeyMapCommand.Events.ChangeTo:
                    var toValue = command.ToValue == float.MaxValue && command.Index == 1
                        ? 1 - MathHelper.Clamp((float)ScriptedControls[command.Name].OldValue, 0, 1)
                        : command.ToValue;
                    TrainCarAction<MSTSLocomotive>(command.Index, l => SignalEvent(l.ContentScripts, command.Name, toValue));
                    if (command.Index == 1)
                        SetControlValue(command.Name, command.Index, toValue);
                    break;
                case KeyMapCommand.Events.StartContinuousChange:
                    if (command.Index == 1 && !ChangingControls.ContainsKey(command))
                        ChangingControls.Add(command, command.Value == float.MinValue ? 0.1f : command.Value);
                    break;
                case KeyMapCommand.Events.StopContinuousChange:
                    if (command.Index == 1 && ChangingControls.ContainsKey(command))
                        ChangingControls.Remove(command);
                    break;
            }
        }
    }

    public class DummyControllerScript : ControllerScript
    {
        public DummyControllerScript() { }
        public override void Initialize() { }
        public override void Update(float elapsedClockSeconds) { }
        public override void HandleEvent(string controlName, float? value) { }
    }
}