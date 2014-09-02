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

namespace ORTS.Scripting.Api
{
    public abstract class AbstractScriptClass
    {
        /// <summary>
        /// Clock value (in seconds) for the simulation. Starts with a value = session start time.
        /// </summary>
        public Func<float> ClockTime;
        /// <summary>
        /// Clock value (in seconds) for the simulation. Starts with a value = 0.
        /// </summary>
        public Func<float> GameTime;
        /// <summary>
        /// Running total of distance travelled - always positive, updated by train physics.
        /// </summary>
        public Func<float> DistanceM;
        /// <summary>
        /// Either define a new custom control, that will be common for all scripts of a particular locomotive,
        /// or hide from cab renderer the original calculated value of a predefined control, which then can be exposed by the script.
        /// Note: the original calculated value will still be available through GetControlValue.
        /// </summary>
        public Action<string> RegisterControl;
        /// <summary>
        /// Either set the value of a custom control, or set a value for a predefined control.
        /// ("ControlName", index, value)
        /// </summary>
        public Action<string, int, float> SetControlValue;
        /// <summary>
        /// Either read the value of a custom control, or read the original value of a taken over predefined control
        /// ("ControlName", index)
        /// </summary>
        public Func<string, int, float> GetControlValue;
    }

    /// <summary>
    /// Base class for Timer and OdoMeter. Not to be used directly.
    /// </summary>
    public class Counter
    {
        float EndValue;
        protected Func<float> CurrentValue;

        public float AlarmValue { get; private set; }
        public float RemainingValue { get { return EndValue - CurrentValue(); } }
        public bool Started { get; private set; }
        public void Setup(float alarmValue) { AlarmValue = alarmValue; }
        public void Start() { EndValue = CurrentValue() + AlarmValue; Started = true; }
        public void Stop() { Started = false; }
        public bool Triggered { get { return Started && CurrentValue() >= EndValue; } }
    }

    public class Timer : Counter
    {
        public Timer(AbstractScriptClass asc)
        {
            CurrentValue = asc.GameTime;
        }
    }

    public class OdoMeter : Counter
    {
        public OdoMeter(AbstractScriptClass asc)
        {
            CurrentValue = asc.DistanceM;
        }
    }
}
