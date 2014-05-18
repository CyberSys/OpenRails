﻿// COPYRIGHT 2009, 2010, 2011, 2013 by the Open Rails project.
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
using System.Text;
using GNU.Gettext;

namespace ORTS.Common
{
    // Classes are provided for converting into and out of these internal units.
    // OR will use metric units (m, kg, s, A, 'C) for internal properties and calculations, preferably from SI (m/s, not km/hr).
    // Currently (v1618), some internal units are Imperial and will be changed.
    // Use these classes rather than in-line literal factors.
    //
    // For example to convert a number from metres to inches, use "DiameterIn = M.ToIn(DiameterM);"
    // 
    // Many units begin with a lowercase letter (kg, kW, in, lb) but capitalised here (Kg, KW, In, Lb) for ease of reading.
    //
    // Web research suggests that VC++ will optimize "/ 2.0" replacing it with "* 0.5f" but VC# will not and cost is around 15 cycles.

    public enum PressureUnit
    {
        None,
        KPa,
        Bar,
        PSI,
        InHg,
        KgfpCm2
    }

    /// <summary>
    /// Distance conversions from and to metres
    /// </summary>
    public class Me {   // Not M to avoid conflict with MSTSMath.M
        public static float FromMi(float m) { return m * 1609.344f; }   // miles => metres
        public static float   ToMi(float m) { return m / 1609.344f; }   // metres => miles
        public static float FromYd(float y) { return y * 0.9144f; }     // yards => metres
        public static float   ToYd(float m) { return m / 0.9144f; }     // metres => yards
        public static float FromFt(float f) { return f * 0.3048f; }     // feet => metres
        public static float   ToFt(float m) { return m / 0.3048f; }     // metres => feet
        public static float FromIn(float i) { return i * 0.0254f; }     // inches => metres
        public static float   ToIn(float m) { return m / 0.0254f; }     // metres => inches
    }

    /// <summary>
    /// Speed conversions from and to metres/sec
    /// </summary>
	public class MpS
    {
        public static float FromMpH(float m)    { return m / 2.23693629f; }    // miles per hour => metres per sec
        public static float   ToMpH(float m)    { return m * 2.23693629f; }    // metres per sec => miles per hour
        public static float FromKpH(float k)    { return k / 3.600f; }    // kilometres per hour => metres per sec
        public static float   ToKpH(float m)    { return m * 3.600f; }    // metres per sec => kilometres per hour
        
        public static float FromMpS(float speed, bool isMetric)
        {
            return isMetric ? ToKpH(speed) : ToMpH(speed);
        }

        public static float ToMpS(float speed, bool isMetric)
        {
            return isMetric ? FromKpH(speed) : FromMpH(speed);
        }
    }

    public class Miles
    {
        public static float FromM(float distance, bool isMetric)
        {
            return isMetric ? distance : Me.FromMi(distance);
        }
        public static float ToM(float distance, bool isMetric)
        {
            return isMetric ? distance : Me.ToMi(distance);
        }
    }

    public class FormatStrings
    {
        static GettextResourceManager Catalog = new GettextResourceManager("ORTS.Common");
        static string m = Catalog.GetString("m");
        static string km = Catalog.GetString("km");
        static string mi = Catalog.GetString("mi");
        static string yd = Catalog.GetString("yd");
        static string kmph = Catalog.GetString("km/h");
        static string mph = Catalog.GetString("mph");
        static string kpa = Catalog.GetString("kPa");
        static string bar = Catalog.GetString("bar");
        static string psi = Catalog.GetString("psi");
        static string inhg = Catalog.GetString("inHg");
        static string kgfpcm2 = Catalog.GetString("kgf/cm^2");

        /// <summary>
        /// Formatted unlocalized speed string, used in reports and logs.
        /// </summary>
        public static string FormatSpeed(float speed, bool isMetric)
        {
            return String.Format("{0:F1}{1}", MpS.FromMpS(speed, isMetric), isMetric ? "km/h" : "mph");
        }

        /// <summary>
        /// Formatted localized speed string, used to display tracking speed, with 1 decimal precision
        /// </summary>
        public static string FormatSpeedDisplay(float speed, bool isMetric)
        {
            return String.Format("{0:F1} {1}", MpS.FromMpS(speed, isMetric), isMetric ? kmph : mph);
        }

        /// <summary>
        /// Formatted localized speed string, used to display speed limits, with 0 decimal precision
        /// </summary>
        public static string FormatSpeedLimit(float speed, bool isMetric)
        {
            return String.Format("{0:F0} {1}", MpS.FromMpS(speed, isMetric), isMetric ? kmph : mph);
        }

        /// <summary>
        /// Formatted unlocalized distance string, used in reports and logs.
        /// </summary>
        public static string FormatDistance(float distance, bool isMetric)
        {
            if (isMetric)
            {
                // <0.1 kilometers, show meters.
                if (Math.Abs(distance) < 100)
                    return String.Format("{0:N0}m", distance);
                return String.Format("{0:F1}km", distance / 1000f);
            }
            // <0.1 miles, show yards.
            if (Math.Abs(distance) < Me.FromMi(0.1f))
                return String.Format("{0:N0}yd", Me.ToYd(distance));
            return String.Format("{0:F1}mi", Me.ToMi(distance));
        }

        /// <summary>
        /// Formatted localized distance string, as displayed in in-game windows
        /// </summary>
        public static string FormatDistanceDisplay(float distance, bool isMetric)
        {
            if (isMetric)
            {
                // <0.1 kilometers, show meters.
                if (Math.Abs(distance) < 100)
                    return String.Format("{0:N0} {1}", distance, m);
                return String.Format("{0:F1} {1}", distance / 1000f, km);
            }
            // <0.1 miles, show yards.
            if (Math.Abs(distance) < Me.FromMi(0.1f))
                return String.Format("{0:N0} {1}", Me.ToYd(distance), yd);
            return String.Format("{0:F1} {1}", Me.ToMi(distance), mi);
        }

        public static string FormatMass(float mass, bool isMetric)
        {
            if (isMetric)
            {
                // <0.1 tons, show kilograms.
                if (Math.Abs(mass) > 1000)
                    return String.Format("{0:N0}t", mass / 1000.0);
                return String.Format("{0:F1}kg", mass);
            }
            return String.Format("{0:F1}Lb", Kg.ToLb(mass));
        }
    
        /// <summary>
        /// Formatted localized pressure string
        /// </summary>
        public static string FormatPressure(float pressure, PressureUnit inputUnit, PressureUnit outputUnit, bool unitDisplayed)
        {
            if (inputUnit == PressureUnit.None || outputUnit == PressureUnit.None)
                return "";

            string unit = "";
            string format = "";
            switch (outputUnit)
            {
                case PressureUnit.KPa:
                    unit = kpa;
                    format = "{0:F0}";
                    switch (inputUnit)
                    {
                        case PressureUnit.Bar:
                            pressure = KPa.FromBar(pressure);
                            break;
                        case PressureUnit.PSI:
                            pressure = KPa.FromPSI(pressure);
                            break;
                        case PressureUnit.InHg:
                            pressure = KPa.FromInHg(pressure);
                            break;
                        case PressureUnit.KgfpCm2:
                            pressure = KPa.FromKgfpCm2(pressure);
                            break;
                    }
                    break;

                case PressureUnit.Bar:
                    unit = bar;
                    format = "{0:F1}";
                    switch (inputUnit)
                    {
                        case PressureUnit.KPa:
                            pressure = Bar.FromKPa(pressure);
                            break;
                        case PressureUnit.PSI:
                            pressure = Bar.FromPSI(pressure);
                            break;
                        case PressureUnit.InHg:
                            pressure = Bar.FromInHg(pressure);
                            break;
                        case PressureUnit.KgfpCm2:
                            pressure = Bar.FromKgfpCm2(pressure);
                            break;
                    }
                    break;

                case PressureUnit.PSI:
                    unit = psi;
                    format = "{0:F0}";
                    switch (inputUnit)
                    {
                        case PressureUnit.KPa:
                            pressure = KPa.ToPSI(pressure);
                            break;
                        case PressureUnit.Bar:
                            pressure = Bar.ToPSI(pressure);
                            break;
                        case PressureUnit.InHg:
                            pressure = KPa.ToPSI(KPa.FromInHg(pressure));
                            break;
                        case PressureUnit.KgfpCm2:
                            pressure = KPa.ToPSI(KPa.FromKgfpCm2(pressure));
                            break;
                    }
                    break;

                case PressureUnit.InHg:
                    unit = inhg;
                    format = "{0:F0}";
                    switch (inputUnit)
                    {
                        case PressureUnit.KPa:
                            pressure = KPa.ToInHg(pressure);
                            break;
                        case PressureUnit.Bar:
                            pressure = Bar.ToInHg(pressure);
                            break;
                        case PressureUnit.PSI:
                            pressure = KPa.ToInHg(KPa.FromPSI(pressure));
                            break;
                        case PressureUnit.KgfpCm2:
                            pressure = KPa.ToInHg(KPa.FromKgfpCm2(pressure));
                            break;
                    }
                    break;

                case PressureUnit.KgfpCm2:
                    unit = kgfpcm2;
                    format = "{0:F1}";
                    switch (inputUnit)
                    {
                        case PressureUnit.KPa:
                            pressure = KPa.ToKgfpCm2(pressure);
                            break;
                        case PressureUnit.Bar:
                            pressure = Bar.ToKgfpCm2(pressure);
                            break;
                        case PressureUnit.PSI:
                            pressure = KPa.ToKgfpCm2(KPa.FromPSI(pressure));
                            break;
                        case PressureUnit.InHg:
                            pressure = KPa.ToKgfpCm2(KPa.FromInHg(pressure));
                            break;
                    }
                    break;
            }

            if(unitDisplayed)
                 format += " " + unit;

            return String.Format(format, pressure);
        }
    }

    /// <summary>
    /// Mass conversions from and to Kilograms
    /// </summary>
    public class Kg 
    {
        public static float FromLb(float l)     { return l / 2.20462f; }    // lb => Kg
        public static float   ToLb(float k)     { return k * 2.20462f; }    // Kg => lb
        public static float FromTUS(float t)    { return t * 907.1847f; }   // Tons (US) => Kg
        public static float   ToTUS(float k)    { return k / 907.1847f; }   // Kg => Tons (US)
        public static float FromTUK(float t)    { return t * 1016.047f; }   // Tons (UK) => Kg 
        public static float   ToTUK(float k)    { return k / 1016.047f; }   // kg => Tons (UK)
        public static float ToTonne(float k) { return k / 1000.0f; }   // kg => Tonnes (Metric)
        public static float FromTonne(float t) { return t * 1000.0f; }   // Tonnes (Metric) => Kg 
    }

    /// <summary>
    /// Force conversions from and to Newtons
    /// </summary>
    public class N 
    {
        public static float FromLbf(float l) { return l / 0.224808943871f; }    // lbf => Newtons
        public static float   ToLbf(float n) { return n * 0.224808943871f; }    // Newtons => lbf
    }

    /// <summary>
    /// Power conversions from and to Watts
    /// </summary>
    public class W 
    {
        public static float FromKW(float k) { return k * 1000f; } // kW => Watts
        public static float   ToKW(float w) { return w / 1000f; } // Watts => kW
        public static float FromHp(float h) { return h * 745.699872f; } // Hp => Watts
        public static float   ToHp(float w) { return w / 745.699872f; } // Watts => Hp
        public static float FromBTUpS(float b) { return b * 1055.05585f; } // BTU/s => Watts
        public static float   ToBTUpS(float w) { return w / 1055.05585f; } // Watts => BTU/s
    }

    /// <summary>
    /// Stiffness conversions from and to Newtons/metre
    /// </summary>
    public class NpM 
    {
    }

    /// <summary>
    /// Resistance conversions from and to Newtons/metre/sec
    /// </summary>
    public class NpMpS
    {
    }

    /// <summary>
    /// Mass rate conversions from and to Kg/s
    /// </summary>
    public class KgpS
    {
        public static float FromLbpH(float l) { return l / 7936.64144f; }  // lb/h => Kg/s
        public static float ToLbpH(float k) { return k * 7936.64144f; }  // Kg/s => lb/h
    }

    /// <summary>
    /// Area conversions from and to m^2
    /// </summary>
    public class Me2
    {
        public static float FromFt2(float f) { return f / 10.764f; } // ft^2 => m^2
        public static float   ToFt2(float m) { return m * 10.764f; } // m^2 => ft^2
        public static float FromIn2(float f) { return f / 1550.0031f; } // In^2 => m^2
        public static float   ToIn2(float m) { return m * 1550.0031f; } // m^2 => In^2
    }

    /// <summary>
    /// Volume conversions from and to m^3
    /// </summary>
    public class Me3
    {
        public static float FromFt3(float f) { return f / 35.3146665722f; }    // ft^3 => m^3
        public static float   ToFt3(float m) { return m * 35.3146665722f; }    // m^3 => ft^3
        public static float FromIn3(float i) { return i / 61023.7441f; }       // in^3 => m^3
        public static float   ToIn3(float m) { return m * 61023.7441f; }       // m^3 => in^3
    }

    /// <summary>
    /// Pressure conversions from and to kilopascals
    /// </summary>
    public class KPa
    {
        public static float     FromPSI(float p)    { return p * 6.89475729f; } // PSI => kPa
        public static float       ToPSI(float k)    { return k / 6.89475729f; } // kPa => PSI
        public static float    FromInHg(float i)    { return i * 3.386389f; }   // inHg => kPa
        public static float      ToInHg(float k)    { return k / 3.386389f; }   // kPa => inHg
        public static float     FromBar(float b)    { return b * 100.0f; }      // bar => kPa
        public static float       ToBar(float k)    { return k / 100.0f; }      // kPa => bar
        public static float FromKgfpCm2(float f)    { return f * 98.068059f; }  // kgf/cm2 => kPa
        public static float   ToKgfpCm2(float k)    { return k / 98.068059f; }  // kPa => kgf/cm2
    }

    /// <summary>
    /// Pressure conversions from and to bar
    /// </summary>
    public class Bar
    {
        public static float     FromKPa(float k)    { return k / 100.0f; }      // kPa => bar
        public static float       ToKPa(float b)    { return b * 100.0f; }      // bar => kPa
        public static float     FromPSI(float p)    { return p / 14.5037738f; } // PSI => bar
        public static float       ToPSI(float b)    { return b * 14.5037738f; } // bar => PSI
        public static float    FromInHg(float i)    { return i / 29.5333727f; } // inHg => bar
        public static float      ToInHg(float b)    { return b * 29.5333727f; } // bar => inHg
        public static float FromKgfpCm2(float f)    { return f / 1.0197f; }     // kgf/cm2 => bar
        public static float   ToKgfpCm2(float b)    { return b * 1.0197f; }     // bar => kgf/cm2
    }

    /// <summary>
    /// Pressure rate conversions from and to bar/s
    /// </summary>
    public class BarpS
    {
        public static float FromPSIpS(float p)  { return p / 14.5037738f; } // PSI/s => bar/s
        public static float   ToPSIpS(float b)  { return b * 14.5037738f; } // bar/s => PSI/s
    }

    /// <summary>
    /// Energy density conversions from and to kJ/Kg
    /// </summary>
    public class KJpKg
    {
        public static float FromBTUpLb(float b) { return b * 2.326f; }  // btu/lb => kj/kg
        public static float   ToBTUpLb(float k) { return k / 2.326f; }  // kj/kg => btu/lb
    }

    /// <summary>
    /// Liquid volume conversions from and to Litres
    /// </summary>
    public class L 
    {
        public static float FromGUK(float g)    { return g * 4.54609f; }    // UK gallon => litre
        public static float   ToGUK(float l)    { return l / 4.54609f; }    // litre => UK gallon
        public static float FromGUS(float g)    { return g * 3.78541f; }    // US gallon => litre
        public static float   ToGUS(float l)    { return l / 3.78541f; }    // litre => US gallon
    }

    /// <summary>
    /// Current conversions from and to Amps
    /// </summary>
    public class A
    {
    }

    /// <summary>
    /// Frequency conversions from and to Hz (revolutions/sec)
    /// </summary>
    public class pS 
    {
        public static float FrompM(float r)    { return r / 60f; }     // rev/min => rev/sec
        public static float   TopM(float h)    { return h * 60f; }     // rev/sec => rev/min
        public static float FrompH(float r)    { return r / 3600f; }   // rev/hr => rev/sec
        public static float   TopH(float h)    { return h * 3600f; }   // rev/sec => rev/hr
    }

    /// <summary>
    /// Time conversions from and to Seconds
    /// </summary>
    public class S
    {
        public static float FromM(float m)  { return m * 60f; }     // mins => secs
        public static float   ToM(float s)  { return s / 60f; }     // secs => mins
        public static float FromH(float h)  { return h * 3600f; }   // hours => secs
        public static float   ToH(float s)  { return s / 3600f; }   // secs => hours
    }

    /// <summary>
    /// Temperature conversions from and to Celsius
    /// </summary>
    public class C
    {
        public static float FromF(float f) { return (f - 32f) * 100f / 180f; }    // Fahrenheit => Celsius
        public static float   ToF(float c) { return (c * 180f / 100f) + 32f; }    // Celsius => Fahrenheit
        public static float FromK(float k) { return k - 273.15f; }   // Kelvin => Celsius
        public static float   ToK(float c) { return c + 273.15f; }   // Celsius => Kelvin
    }

    public class CompareTimes
    {
        static int eightHundredHours = 8 * 3600;
        static int sixteenHundredHours = 16 * 3600;

        public static int LatestTime(int time1, int time2)
        {
            if (time1 > sixteenHundredHours && time2 < eightHundredHours)
            {
                return (time2);
            }
            else if (time1 < eightHundredHours && time2 > sixteenHundredHours)
            {
                return (time1);
            }
            else if (time1 > time2)
            {
                return (time1);
            }
            return (time2);
        }

        public static int EarliestTime(int time1, int time2)
        {
            if (time1 > sixteenHundredHours && time2 < eightHundredHours)
            {
                return (time1);
            }
            else if (time1 < eightHundredHours && time2 > sixteenHundredHours)
            {
                return (time2);
            }
            else if (time1 > time2)
            {
                return (time2);
            }
            return (time1);
        }
    }
}
