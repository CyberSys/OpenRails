﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MSTS;
using Microsoft.Xna.Framework.Graphics;
using System.IO;
using System.Collections;

namespace ORTS
{
    public class DieselEngines : IEnumerable
    {
        /// <summary>
        /// A list of auxiliaries
        /// </summary>
        public List<DieselEngine> DEList = new List<DieselEngine>();

        /// <summary>
        /// Number of Auxiliaries on the list
        /// </summary>
        public int Count { get { return DEList.Count; } }

        /// <summary>
        /// Reference to the locomotive carrying the auxiliaries
        /// </summary>
        public readonly MSTSLocomotive Locomotive;

        /// <summary>
        /// not applicable, but still can be used
        /// </summary>
        public DieselEngines()
        {

        }

        /// <summary>
        /// Creates a set of auxiliaries connected to the locomotive
        /// </summary>
        /// <param name="loco">Host locomotive</param>
        public DieselEngines(MSTSLocomotive loco)
        {
            Locomotive = loco;
        }

        /// <summary>
        /// constructor from copy
        /// </summary>
        public DieselEngines(DieselEngines copy, MSTSLocomotive loco)
        {
            DEList = new List<DieselEngine>();
            foreach (DieselEngine de in copy.DEList)
            {
                DEList.Add(new DieselEngine(de));
            }
            Locomotive = loco;
        }

        /// <summary>
        /// Creates a set of auxiliaries connected to the locomotive, based on stf reader parameters 
        /// </summary>
        /// <param name="loco">Host locomotive</param>
        /// <param name="stf">Reference to the ENG file reader</param>
        public DieselEngines(MSTSLocomotive loco, STFReader stf)
        {
            Locomotive = loco;
            Parse(stf, loco);
        }


        public DieselEngine this[int i]
        {
            get { return DEList[i]; }
            set { DEList[i] = value; }
        }

        public void Add()
        {
            DEList.Add(new DieselEngine());
        }

        public void Add(DieselEngine de)
        {
            DEList.Add(de);
        }


        /// <summary>
        /// Parses all the parameters within the ENG file
        /// </summary>
        /// <param name="stf">eference to the ENG file reader</param>
        public void Parse(STFReader stf, MSTSLocomotive loco)
        {
            stf.MustMatch("(");
            int count = stf.ReadInt(STFReader.UNITS.None, 0);
            for (int i = 0; i < count; i++)
            {
                string setting = stf.ReadString().ToLower();
                if (setting == "diesel")
                {
                    DEList.Add(new DieselEngine());

                    DEList[i].Parse(stf, loco);
                }
                if (!DEList[i].IsInitialized)
                {
                    STFException.TraceWarning(stf, "Diesel engine model not found - loading MSTS format");
                    DEList[i].InitFromMSTS((MSTSDieselLocomotive)Locomotive);                    
                }
            }
        }

        public void Initialize(bool start)
        {
            foreach (DieselEngine de in DEList)
                de.Initialize(start);
        }

        /// <summary>
        /// Saves status of each auxiliary on the list
        /// </summary>
        /// <param name="outf"></param>
        public void Save(BinaryWriter outf)
        {
            outf.Write(DEList.Count);
            foreach (DieselEngine de in DEList)
                de.Save(outf);
        }

        /// <summary>
        /// Restores status of each auxiliary on the list
        /// </summary>
        /// <param name="inf"></param>
        public void Restore(BinaryReader inf)
        {
            int count = inf.ReadInt32();
            if (DEList.Count == 0)
            {
                for (int i = 0; i < count; i++)
                {
                    DEList.Add(new DieselEngine());
                    DEList[i].InitFromMSTS((MSTSDieselLocomotive)Locomotive);
                }
                
            }
            foreach (DieselEngine de in DEList)
                de.Restore(inf);
        }

        /// <summary>
        /// A summary of power of all the diesels
        /// </summary>
        public float PowerW
        {
            get
            {
                float temp = 0f;
                foreach (DieselEngine de in DEList)
                {
                    temp += de.OutputPowerW;
                }
                return temp;
            }
        }

        /// <summary>
        /// A summary of maximal power of all the diesels
        /// </summary>
        public float MaxPowerW
        {
            get
            {
                float temp = 0f;
                foreach (DieselEngine de in DEList)
                {
                    temp += de.MaxOutputPowerW;
                }
                return temp;
            }
        }

        /// <summary>
        /// A summary of fuel flow of all the auxiliaries
        /// </summary>
        public float DieselFlowLps
        {
            get
            {
                float temp = 0f;
                foreach (DieselEngine de in DEList)
                {
                    temp += de.DieselFlowLps;
                }
                return temp;
            }
        }

        public bool HasGearBox
        {
            get
            {
                bool temp = false;
                foreach (DieselEngine de in DEList)
                {
                    temp |= (de.GearBox != null);
                }
                return temp;
            }
        }

        public float MotiveForceN
        {
            get
            {
                float temp = 0;
                foreach (DieselEngine de in DEList)
                {
                    if(de.GearBox != null)
                        temp += de.GearBox.MotiveForceN;
                }
                return temp;
            }
        }

        /// <summary>
        /// Updates each auxiliary on the list
        /// </summary>
        /// <param name="elapsedClockSeconds">Time span within the simulation cycle</param>
        public void Update(float elapsedClockSeconds)
        {
            foreach (DieselEngine de in DEList)
            {
                de.Update(elapsedClockSeconds);
            }
        }

        /// <summary>
        /// Gets status of each auxiliary on the list
        /// </summary>
        /// <returns>string formated as one line for one auxiliary</returns>
        public string GetStatus()
        {
            var result = new StringBuilder();
            result.AppendLine("Diesel Engines:");
            foreach (DieselEngine de in DEList)
            {
                result.AppendLine(de.GetStatus());
            }
            return result.ToString();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return (IEnumerator)GetEnumerator();
        }

        public DieselEnum GetEnumerator()
        {
            return new DieselEnum(DEList.ToArray());
        }
    }

    public class DieselEnum : IEnumerator
    {
        public DieselEngine[] deList;

        // Enumerators are positioned before the first element
        // until the first MoveNext() call.
        int position = -1;

        public DieselEnum(DieselEngine[] list)
        {
            deList = list;
        }

        public bool MoveNext()
        {
            position++;
            return (position < deList.Length);
        }

        public void Reset()
        {
            position = -1;
        }

        object IEnumerator.Current
        {
            get
            {
                return Current;
            }
        }

        public DieselEngine Current
        {
            get
            {
                try
                {
                    return deList[position];
                }
                catch (IndexOutOfRangeException)
                {
                    throw new InvalidOperationException();
                }
            }
        }
    }

    public class DieselEngine
    {
        public enum Status
        {
            Stopped = 0,
            Starting = 1,
            Running = 2,
            Stopping = 3
        }

        public DieselEngine()
        {
        }

        public DieselEngine(DieselEngine copy)
        {
            IdleRPM = copy.IdleRPM;
            MaxRPM = copy.MaxRPM;
            ChangeUpRPMpS = copy.ChangeUpRPMpS;
            ChangeDownRPMpS = copy.ChangeDownRPMpS;
            RateOfChangeUpRPMpSS = copy.RateOfChangeUpRPMpSS;
            RateOfChangeDownRPMpSS = copy.RateOfChangeDownRPMpSS;
            MaximalPowerW = copy.MaximalPowerW;
            initLevel = copy.initLevel;
            DieselPowerTab = new Interpolator(copy.DieselPowerTab);
            DieselConsumptionTab = new Interpolator(copy.DieselConsumptionTab);
            locomotive = copy.locomotive;
        }

        float dRPM = 0;
        public float EngineRPMchangeRPMpS { get { return dRPM; } }

        public float RealRPM;

        public float StartingRPM;

        public float StartingConfirmationRPM;

        public GearBox GearBox = null;

        public MSTSLocomotive locomotive = null;

        int initLevel = 0;
        public bool IsInitialized { get { return initLevel >= 17; } }

        public Status EngineStatus = Status.Stopped;

        public float DemandedRPM;
        float demandedThrottlePercent;
        public float DemandedThrottlePercent { set { demandedThrottlePercent = value > 100f ? 100f : (value < 0 ? 0 : value); } get { return demandedThrottlePercent; } }
        public float IdleRPM;
        public float MaxRPM;

        public float ChangeUpRPMpS;
        public float ChangeDownRPMpS;
        public float RateOfChangeUpRPMpSS;
        public float RateOfChangeDownRPMpSS;

        public float MaximalPowerW;
        public float MaxOutputPowerW;
        public float OutputPowerW;
        public float ThrottlePercent { get { return OutputPowerW / MaximalPowerW * 100f; } }

        public float DieselUsedPerHourAtMaxPowerL = 1.0f;
        public float DieselUsedPerHourAtIdleL = 1.0f;
        public float DieselFlowLps = 0.0f;

        public Interpolator DieselPowerTab = null;
        public Interpolator DieselConsumptionTab = null;
        public Interpolator ThrottleRPMTab = null;

        public float ExhaustParticles = 10.0f;
        public Color ExhaustColor = Color.Gray;
        Color ExhaustSteadyColor = Color.Gray;
        Color ExhaustTransientColor = Color.Black;
        public float ExhaustDynamics = 1.5f;
        public float MaxExhaust = 100;
        public float IdleExhaust = 10;
        public float LoadPercent
        {
            get
            {
                return (MaxOutputPowerW <= 0f ? 0f : (OutputPowerW * 100f / MaxOutputPowerW)) ;
            }
        }

        public bool HasGearBox { get { return GearBox != null; } }

        public void Parse(string lowercasetoken, STFReader stf)
        {
            switch (lowercasetoken)
            {
                case "engine(orts(diesel(idlerpm": IdleRPM = stf.ReadFloatBlock(STFReader.UNITS.None, 600); initLevel++; break;
                case "engine(orts(diesel(maxrpm": MaxRPM = stf.ReadFloatBlock(STFReader.UNITS.None, 1800); initLevel++; break;
                case "engine(orts(diesel(startingrpm": StartingRPM = stf.ReadFloatBlock(STFReader.UNITS.None, 400); initLevel++; break;
                case "engine(orts(diesel(startingconfirmrpm": StartingConfirmationRPM = stf.ReadFloatBlock(STFReader.UNITS.None, 650); initLevel++; break;
                case "engine(orts(diesel(changeuprpmps": ChangeUpRPMpS = stf.ReadFloatBlock(STFReader.UNITS.None, 100); initLevel++; break;
                case "engine(orts(diesel(changedownrpmps": ChangeDownRPMpS = stf.ReadFloatBlock(STFReader.UNITS.None, 100); initLevel++; break;
                case "engine(orts(diesel(rateofchangeuprpmpss": RateOfChangeUpRPMpSS = stf.ReadFloatBlock(STFReader.UNITS.None, 100); initLevel++; break;
                case "engine(orts(diesel(rateofchangedownrpmpss": RateOfChangeDownRPMpSS = stf.ReadFloatBlock(STFReader.UNITS.None, 100); initLevel++; break;
                case "engine(orts(diesel(maximalpowerw": MaximalPowerW = stf.ReadFloatBlock(STFReader.UNITS.Power, 1); initLevel++; break;
                case "engine(orts(diesel(dieselpowertab": DieselPowerTab = new Interpolator(stf); initLevel++; break;
                case "engine(orts(diesel(dieselconsumptiontab": DieselConsumptionTab = new Interpolator(stf); initLevel++; break;
                case "engine(orts(diesel(throttlerpmtab": ThrottleRPMTab = new Interpolator(stf); initLevel++; break;
                case "engine(orts(diesel(idleexhaust": IdleExhaust = stf.ReadFloatBlock(STFReader.UNITS.None, 5); initLevel++; break;
                case "engine(orts(diesel(maxexhaust": MaxExhaust = stf.ReadFloatBlock(STFReader.UNITS.None, 50); initLevel++; break;
                case "engine(orts(diesel(exhaustdynamics": ExhaustDynamics = stf.ReadFloatBlock(STFReader.UNITS.None, 1); initLevel++; break;
                case "engine(orts(diesel(exhaustcolor": ExhaustSteadyColor.PackedValue = stf.ReadHexBlock(Color.Gray.PackedValue); initLevel++; break;
                case "engine(orts(diesel(exhausttransientcolor": ExhaustTransientColor.PackedValue = stf.ReadHexBlock(Color.Black.PackedValue); initLevel++; break;
                default: break;
            }
        }

        /// <summary>
        /// Parses parameters from the stf reader
        /// </summary>
        /// <param name="stf">Reference to the stf reader</param>
        public virtual void Parse(STFReader stf, MSTSLocomotive loco)
        {
            locomotive = loco;
            stf.MustMatch("(");
            bool end = false;
            while (!end)
            {
                string lowercasetoken = stf.ReadItem().ToLower();
                switch (lowercasetoken)
                {
                    case "idlerpm":         IdleRPM = stf.ReadFloatBlock(STFReader.UNITS.Power, 0); initLevel++; break;
                    case "maxrpm":          MaxRPM = stf.ReadFloatBlock(STFReader.UNITS.None, 0);initLevel++; break;
                    case "startingrpm":     StartingRPM = stf.ReadFloatBlock(STFReader.UNITS.None, 0);initLevel++; break;
                    case "startingconfirmrpm": StartingConfirmationRPM = stf.ReadFloatBlock(STFReader.UNITS.None, 0); initLevel++; break;
                    case "changeuprpmps":   ChangeUpRPMpS = stf.ReadFloatBlock(STFReader.UNITS.None, 0); initLevel++; break;
                    case "changedownrpmps": ChangeDownRPMpS = stf.ReadFloatBlock(STFReader.UNITS.None, 0); initLevel++; break;
                    case "rateofchangeuprpmpss": RateOfChangeUpRPMpSS = stf.ReadFloatBlock(STFReader.UNITS.None, 0);initLevel++; break;
                    case "rateofchangedownrpmpss": RateOfChangeDownRPMpSS = stf.ReadFloatBlock(STFReader.UNITS.None, 0);initLevel++; break;
                    case "maximalpowerw":   MaximalPowerW = stf.ReadFloatBlock(STFReader.UNITS.None, 0);initLevel++; break;
                    case "idleexhaust":     IdleExhaust = stf.ReadFloatBlock(STFReader.UNITS.None, 0); initLevel++; break;
                    case "maxexhaust":      MaxExhaust = stf.ReadFloatBlock(STFReader.UNITS.None, 0);initLevel++; break;
                    case "exhaustdynamics": ExhaustDynamics = stf.ReadFloatBlock(STFReader.UNITS.None, 0);initLevel++; break;
                    case "exhaustcolor":    ExhaustSteadyColor.PackedValue = stf.ReadHexBlock(Color.Gray.PackedValue);initLevel++; break;
                    case "exhausttransientcolor": ExhaustTransientColor.PackedValue = stf.ReadHexBlock(Color.Black.PackedValue);initLevel++; break;
                    case "dieselpowertab": DieselPowerTab = new Interpolator(stf);initLevel++; break;
                    case "dieselconsumptiontab": DieselConsumptionTab = new Interpolator(stf);initLevel++; break;
                    case "throttlerpmtab": ThrottleRPMTab = new Interpolator(stf); initLevel++; break;
                    default:
                        end = true;
                        break;
                }
            }
        }

        public void Initialize(bool start)
        {
            if (start)
            {
                RealRPM = IdleRPM;
                EngineStatus = Status.Running;
            }
        }


        public void Update(float elapsedClockSeconds)
        {
            if ((ThrottleRPMTab != null)&&(EngineStatus == Status.Running))
            {
                DemandedRPM = ThrottleRPMTab[demandedThrottlePercent];
            }

            if (GearBox != null)
            {
                if (RealRPM > 0)
                    GearBox.ShaftPercent = (RealRPM - GearBox.ShaftRPM) / RealRPM * 100f;
                else
                    GearBox.ShaftPercent = 100f;
                
                if (GearBox.CurrentGear != null)
                {
                    if (GearBox.IsShaftOn)
                        DemandedRPM = GearBox.ShaftRPM;
                }
            }

            ExhaustParticles = ((MaxExhaust - IdleExhaust) * ThrottlePercent / 100f + IdleExhaust);
            if (ExhaustParticles < 5.0f)
                ExhaustParticles = 5.0f;
            //Rate of change decission
            if (RealRPM < (DemandedRPM))
            {
                dRPM = (float)Math.Min(Math.Sqrt(2 * RateOfChangeUpRPMpSS * (DemandedRPM - RealRPM)), ChangeUpRPMpS);
                if (dRPM == ChangeUpRPMpS)
                {
                    ExhaustParticles *= ExhaustDynamics * MaxExhaust;
                    ExhaustColor = ExhaustTransientColor;
                }
                else
                {
                    ExhaustColor = ExhaustSteadyColor;
                }
            }
            else
            {
                if (RealRPM > (DemandedRPM))
                {
                    dRPM = (float)Math.Max(-Math.Sqrt(2 * RateOfChangeDownRPMpSS * (RealRPM - DemandedRPM)), -ChangeDownRPMpS);
                    ExhaustColor = ExhaustSteadyColor;
                    ExhaustParticles = 3.0f;
                }
                else
                {
                    dRPM = 0;
                    ExhaustColor = ExhaustSteadyColor;
                }
            }

            if ((OutputPowerW > MaxOutputPowerW)&&(EngineStatus == Status.Running))
                dRPM = (MaxOutputPowerW - OutputPowerW) / MaximalPowerW * RateOfChangeDownRPMpSS;

            RealRPM = Math.Max(RealRPM + dRPM * elapsedClockSeconds, 0);

            if (DieselPowerTab != null)
            {
                MaxOutputPowerW = DieselPowerTab[RealRPM] <= MaximalPowerW ? DieselPowerTab[RealRPM] : MaximalPowerW;
                MaxOutputPowerW = MaxOutputPowerW < 0f ? 0f : MaxOutputPowerW;
            }
            else
            {
                MaxOutputPowerW = (RealRPM - IdleRPM) / (MaxRPM - IdleRPM) * MaximalPowerW;
            }

            if (EngineStatus == Status.Starting)
            {
                if ((RealRPM > (0.9f * StartingRPM)) && (RealRPM < StartingRPM))
                {
                    DemandedRPM = 1.1f * StartingConfirmationRPM;
                    ExhaustColor = ExhaustTransientColor;
                    ExhaustParticles = (MaxExhaust - IdleExhaust) / (0.5f * StartingRPM - StartingRPM) * (RealRPM - 0.5f * StartingRPM) + IdleExhaust;
                }
                if ((RealRPM > StartingConfirmationRPM))// && (RealRPM < 0.9f * IdleRPM))
                    EngineStatus = Status.Running;
            }

            if ((EngineStatus != Status.Starting) && (RealRPM == 0f))
                EngineStatus = Status.Stopped;

            if ((EngineStatus == Status.Stopped) || (EngineStatus == Status.Stopping) || ((EngineStatus == Status.Starting) && (RealRPM < StartingRPM)))
            {
                ExhaustParticles = 0;
                DieselFlowLps = 0;
            }
            else
            {
                if (DieselConsumptionTab != null)
                {
                    if (ThrottlePercent == 0)
                        DieselFlowLps = DieselUsedPerHourAtIdleL / 3600.0f;
                    else
                        DieselFlowLps = DieselConsumptionTab[RealRPM] * MaxOutputPowerW / 3600.0f / 800.0f / 1000.0f;
                }
                else
                {
                    if (ThrottlePercent == 0)
                        DieselFlowLps = DieselUsedPerHourAtIdleL / 3600.0f;
                    else
                        DieselFlowLps = ((DieselUsedPerHourAtMaxPowerL - DieselUsedPerHourAtIdleL) * ThrottlePercent / 100f + DieselUsedPerHourAtIdleL) / 3600.0f;
                }
            }

            if (ExhaustParticles > 100f)
                ExhaustParticles = 100f;

            return;
        }

        public Status Start()
        {
            switch (EngineStatus)
            {
                case Status.Stopped:
                case Status.Stopping:
                    DemandedRPM = StartingRPM;
                    EngineStatus = Status.Starting;
                    break;
                default:
                    break;
            }
            return EngineStatus;
        }

        public Status Stop()
        {
            if (EngineStatus != Status.Stopped)
            {
                DemandedRPM = 0;
                EngineStatus = Status.Stopping;
                if (RealRPM <= 0)
                    EngineStatus = Status.Stopped;
            }
            return EngineStatus;
        }

        public string GetStatus()
        {
            var result = new StringBuilder();
            result.AppendFormat("Diesel engine = {0}\n", EngineStatus.ToString());
            result.AppendFormat("Diesel RPM r/d = {0:F0} / {1:F0}\n", RealRPM, DemandedRPM);
            result.AppendFormat("Diesel flow = {0:F1} L/h ({1:F1} gal/h)\n", DieselFlowLps * 3600.0f, DieselFlowLps * 3600.0f / 3.785f);
            result.AppendFormat("Diesel power = {0:F0} / {1:F0} kW\n", OutputPowerW / 1000f, MaxOutputPowerW / 1000f);
            result.AppendFormat("Diesel load = {0:F1} %\n", LoadPercent);
            if (GearBox != null)
            {
                result.AppendFormat("Current gear = {0} {1}\n", GearBox.CurrentGearIndex < 0 ? "N" : (GearBox.CurrentGearIndex + 1).ToString(), GearBox.GearBoxOperation == GearBoxOperation.Automatic ? "Automatic gear" : "");
                //if (GearBox.GearBoxOperation == GearBoxOperation.Manual)
                result.AppendFormat("Next gear = {0}, Shaft: {1:F0}%\n", GearBox.NextGearIndex < 0 ? "N" : (GearBox.NextGearIndex + 1).ToString(), GearBox.ShaftPercent);
                if(GearBox.CurrentGear != null)
                    result.AppendFormat("RealRPM = {0:F0} - min: {1:F0}, max: {2:F0}\n", RealRPM, MaxRPM * GearBox.CurrentGear.DownGearProportion, MaxRPM * GearBox.CurrentGear.UpGearProportion);
                result.AppendFormat("ShaftRPM = {0:F0} - {1}\n", GearBox.ShaftRPM, GearBox.GearedUp ? "UP" : (GearBox.GearedDown ? "DOWN" : "At gear"));

                if (GearBox.IsOverspeedError)
                    result.AppendLine("Gearbox Overspeed ERROR!!!");
                else
                    if (GearBox.IsOverspeedWarning)
                        result.AppendLine("Gearbox Overspeed Warning!");
                
            }
            return result.ToString();
        }

        public void Restore(BinaryReader inf)
        {
            EngineStatus = (Status)inf.ReadInt32();
            RealRPM = inf.ReadSingle();
            OutputPowerW = inf.ReadSingle();
            if (((MSTSDieselLocomotive)locomotive).GearBox != null)
            {
                if (!((MSTSDieselLocomotive)locomotive).GearBox.IsInitialized)
                    GearBox = null;
                else
                {
                    GearBox = new GearBox(((MSTSDieselLocomotive)locomotive).GearBox, this);
                    GearBox.Restore(inf);
                }
            }
        }

        public void Save(BinaryWriter outf)
        {
            outf.Write((int)EngineStatus);
            outf.Write(RealRPM);
            outf.Write(OutputPowerW);
            if (GearBox != null)
                GearBox.Save(outf);
        }

        public void InitFromMSTS(MSTSDieselLocomotive diesel)
        {
            IdleRPM = diesel.IdleRPM;
            MaxRPM = diesel.MaxRPM;
            StartingRPM = diesel.IdleRPM * 2.0f / 3.0f;
            StartingConfirmationRPM = diesel.IdleRPM * 1.1f;
            ChangeUpRPMpS = diesel.MaxRPMChangeRate;
            ChangeDownRPMpS = diesel.MaxRPMChangeRate;
            RateOfChangeUpRPMpSS = ChangeUpRPMpS;
            RateOfChangeDownRPMpSS = ChangeDownRPMpS;
            MaximalPowerW = diesel.MaxPowerW;
            DieselPowerTab = new Interpolator(new float[] { diesel.IdleRPM, diesel.MaxRPM }, new float[] { diesel.MaxPowerW * 0.05f, diesel.MaxPowerW });
            DieselConsumptionTab = new Interpolator(new float[] { diesel.IdleRPM, diesel.MaxRPM }, new float[] { diesel.DieselUsedPerHourAtIdleL, diesel.DieselUsedPerHourAtMaxPowerL });
            ThrottleRPMTab = new Interpolator(new float[] { 0, 100 }, new float[] { diesel.IdleRPM, diesel.MaxRPM });
            IdleExhaust = diesel.IdleExhaust;
            MaxExhaust = diesel.MaxExhaust;
            ExhaustDynamics = diesel.ExhaustDynamics;
            locomotive = diesel;
        }

    }
}
