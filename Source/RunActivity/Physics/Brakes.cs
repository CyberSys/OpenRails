﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MSTS;
using System.IO;

namespace ORTS
{
    public abstract class BrakeSystem
    {
        public float BrakeLine1PressurePSI = 90;     // main trainline pressure at this car
        public float BrakeLine2PressurePSI = 0;     // extra line for dual line systems
        public float BrakeLine3PressurePSI = 0;     // extra line just in case

        public abstract void AISetPercent(float percent);

        public abstract string GetStatus(int detailLevel);

        public abstract void Save(BinaryWriter outf);

        public abstract void Restore( BinaryReader inf );

        public abstract void Initialize(bool handbrakeOn);
        public abstract void SetHandbrakePercent(float percent);
        public abstract void SetRetainer(RetainerSetting setting);
    }

    public enum RetainerSetting { Exhaust, HighPressure, LowPressure, SlowDirect };

    public abstract class MSTSBrakeSystem: BrakeSystem
    {

        public abstract void Parse(string lowercasetoken, STFReader f);

        public abstract void Update(float elapsedClockSeconds);

        public abstract void Increase();

        public abstract void Decrease();

        public abstract void InitializeFromCopy(BrakeSystem copy);

    }

    public class AirSinglePipe : MSTSBrakeSystem
    {
        float MaxHandbrakeForceN = 0;
        float MaxBrakeForceN = 89e3f;
        float BrakePercent = 0;  // simplistic system
        TrainCar Car;
        float HandbrakePercent = 0;
        float CylPressurePSI = 64;
        float AutoCylPressurePSI = 64;
        float AuxResPressurePSI = 64;
        float EmergResPressurePSI = 64;
        float MaxCylPressurePSI = 64;
        float AuxCylVolumeRatio = 2.5f;
        float AuxBrakeLineVolumeRatio = 3.1f;
        float RetainerPressureThresholdPSI = 0;
        float ReleaseRate = 1.86f;
        float MaxReleaseRate = 1.86f;
        float MaxApplicationRate = .9f;
        float MaxAuxilaryChargingRate = 1.684f;
        float EmergResChargingRate = 1.684f;
        float EmergAuxVolumeRatio = 1.4f;
        public enum ValveState { Lap, Apply, Release, Emergency };
        ValveState TripleValveState = ValveState.Lap;

        public AirSinglePipe( TrainCar car )
        {
            Car = car;
        }

        public override void InitializeFromCopy(BrakeSystem copy)
        {
            AirSinglePipe thiscopy = (AirSinglePipe)copy;
            MaxHandbrakeForceN = thiscopy.MaxHandbrakeForceN;
            MaxBrakeForceN = thiscopy.MaxBrakeForceN;
            MaxCylPressurePSI = thiscopy.MaxCylPressurePSI;
            AuxCylVolumeRatio = thiscopy.AuxCylVolumeRatio;
            AuxBrakeLineVolumeRatio = thiscopy.AuxBrakeLineVolumeRatio;
            RetainerPressureThresholdPSI = thiscopy.RetainerPressureThresholdPSI;
            ReleaseRate = thiscopy.ReleaseRate;
            MaxReleaseRate = thiscopy.MaxReleaseRate;
            MaxApplicationRate = thiscopy.MaxApplicationRate;
            MaxAuxilaryChargingRate = thiscopy.MaxAuxilaryChargingRate;
            EmergResChargingRate = thiscopy.EmergResChargingRate;
            EmergAuxVolumeRatio = thiscopy.EmergAuxVolumeRatio;
        }

        public override string GetStatus(int detailLevel)
        {
            if (BrakeLine1PressurePSI < 0)
                return "";
            string s = "";
            if (detailLevel > 0)
                s = s + string.Format("BC {0:F0} ",CylPressurePSI);
            s = s + string.Format("BP {0:F0}", BrakeLine1PressurePSI);
            if (detailLevel > 1)
                s = s + string.Format(" AR {0:F0} ER {1:F0} State {2}",AuxResPressurePSI, EmergResPressurePSI, TripleValveState);
            if (detailLevel > 0 && HandbrakePercent > 0)
                s = s + string.Format(" handbrake {0:F0}%", HandbrakePercent);
            return s;
        }

        public override void Parse(string lowercasetoken, STFReader f)
        {
            switch (lowercasetoken)
            {
                case "wagon(maxhandbrakeforce": MaxHandbrakeForceN = f.ReadFloatBlock(); break;
                case "wagon(maxbrakeforce": MaxBrakeForceN = f.ReadFloatBlock(); break;
                case "wagon(brakecylinderpressureformaxbrakebrakeforce": MaxCylPressurePSI = AutoCylPressurePSI = f.ReadFloatBlock(); break;
                case "wagon(triplevalveratio": AuxCylVolumeRatio = f.ReadFloatBlock(); break;
                case "wagon(maxreleaserate": MaxReleaseRate = ReleaseRate = f.ReadFloatBlock(); break;
                case "wagon(maxapplicationrate": MaxApplicationRate = f.ReadFloatBlock(); break;
                case "wagon(maxauxilarychargingrate": MaxAuxilaryChargingRate = f.ReadFloatBlock(); break;
                case "wagon(emergencyreschargingrate": EmergResChargingRate = f.ReadFloatBlock(); break;
                case "wagon(emergencyresvolumemultiplier": EmergAuxVolumeRatio = f.ReadFloatBlock(); break;
            }
        }

        public override void Save(BinaryWriter outf)
        {
            outf.Write(BrakeLine1PressurePSI);
            outf.Write(BrakeLine2PressurePSI);
            outf.Write(BrakeLine3PressurePSI);
            outf.Write(BrakePercent);
            outf.Write(HandbrakePercent);
            outf.Write(ReleaseRate);
            outf.Write(RetainerPressureThresholdPSI);
            outf.Write(AutoCylPressurePSI);
            outf.Write(AuxResPressurePSI);
            outf.Write(EmergResPressurePSI);
            outf.Write((int)TripleValveState);
        }

        public override void Restore(BinaryReader inf)
        {
            BrakeLine1PressurePSI = inf.ReadSingle();
            BrakeLine2PressurePSI = inf.ReadSingle();
            BrakeLine3PressurePSI = inf.ReadSingle();
            BrakePercent = inf.ReadSingle();
            HandbrakePercent = inf.ReadSingle();
            ReleaseRate = inf.ReadSingle();
            RetainerPressureThresholdPSI = inf.ReadSingle();
            AutoCylPressurePSI = inf.ReadSingle();
            AuxResPressurePSI = inf.ReadSingle();
            EmergResPressurePSI = inf.ReadSingle();
            TripleValveState = (ValveState)inf.ReadInt32();
        }

        public override void Initialize(bool handbrakeOn)
        {
            AuxResPressurePSI = EmergResPressurePSI = BrakeLine1PressurePSI;
            AutoCylPressurePSI = (BrakeLine2PressurePSI - BrakeLine1PressurePSI) * AuxCylVolumeRatio;
            if (AutoCylPressurePSI > MaxCylPressurePSI)
                AutoCylPressurePSI = MaxCylPressurePSI;
            TripleValveState = ValveState.Lap;
            HandbrakePercent = handbrakeOn ? 100 : 0;
            //Console.WriteLine("initb {0} {1}", AuxResPressurePSI, AutoCylPressurePSI);
        }
        public override void Update(float elapsedClockSeconds)
        {
            if (BrakeLine1PressurePSI < AuxResPressurePSI - 10)
                TripleValveState = ValveState.Emergency;
            else if (BrakeLine1PressurePSI > AuxResPressurePSI + 1)
                TripleValveState = ValveState.Release;
            else if (TripleValveState == ValveState.Emergency && BrakeLine1PressurePSI > AuxResPressurePSI)
                TripleValveState = ValveState.Release;
            else if (TripleValveState != ValveState.Emergency && BrakeLine1PressurePSI < AuxResPressurePSI - 1)
                TripleValveState = ValveState.Apply;
            else if (TripleValveState == ValveState.Apply && BrakeLine1PressurePSI >= AuxResPressurePSI)
                TripleValveState = ValveState.Lap;
            if (TripleValveState == ValveState.Apply || TripleValveState == ValveState.Emergency)
            {
                float dp = elapsedClockSeconds * MaxApplicationRate;
                if (AuxResPressurePSI - dp < AutoCylPressurePSI + dp * AuxCylVolumeRatio)
                    dp = (AuxResPressurePSI - AutoCylPressurePSI) / (1 + AuxCylVolumeRatio);
                if (BrakeLine1PressurePSI > AuxResPressurePSI - dp)
                {
                    dp = AuxResPressurePSI - BrakeLine1PressurePSI;
                    TripleValveState = ValveState.Lap;
                }
                AuxResPressurePSI -= dp;
                AutoCylPressurePSI += dp * AuxCylVolumeRatio;
                if (TripleValveState == ValveState.Emergency)
                {
                    dp = elapsedClockSeconds * MaxApplicationRate;
                    if (EmergResPressurePSI - dp < AuxResPressurePSI + dp * EmergAuxVolumeRatio)
                        dp = (EmergResPressurePSI - AuxResPressurePSI) / (1 + EmergAuxVolumeRatio);
                    EmergResPressurePSI -= dp;
                    AuxResPressurePSI += dp * EmergAuxVolumeRatio;
                }
            }
            if (TripleValveState == ValveState.Release)
            {
                if (AutoCylPressurePSI > RetainerPressureThresholdPSI)
                {
                    AutoCylPressurePSI -= elapsedClockSeconds * ReleaseRate;
                    if (AutoCylPressurePSI < RetainerPressureThresholdPSI)
                        AutoCylPressurePSI = RetainerPressureThresholdPSI;
                }
                if (AuxResPressurePSI < EmergResPressurePSI && AuxResPressurePSI < BrakeLine1PressurePSI)
                {
                    float dp = elapsedClockSeconds * EmergResChargingRate;
                    if (EmergResPressurePSI - dp < AuxResPressurePSI + dp * EmergAuxVolumeRatio)
                        dp = (EmergResPressurePSI - AuxResPressurePSI) / (1 + EmergAuxVolumeRatio);
                    if (BrakeLine1PressurePSI < AuxResPressurePSI + dp * EmergAuxVolumeRatio)
                        dp = (BrakeLine1PressurePSI - AuxResPressurePSI) / EmergAuxVolumeRatio;
                    EmergResPressurePSI -= dp;
                    AuxResPressurePSI += dp * EmergAuxVolumeRatio;
                }
                if (AuxResPressurePSI > EmergResPressurePSI)
                {
                    float dp = elapsedClockSeconds * EmergResChargingRate;
                    if (EmergResPressurePSI - dp > AuxResPressurePSI + dp * EmergAuxVolumeRatio)
                        dp = (EmergResPressurePSI - AuxResPressurePSI) / (1 + EmergAuxVolumeRatio);
                    EmergResPressurePSI += dp;
                    AuxResPressurePSI -= dp * EmergAuxVolumeRatio;
                }
                if (AuxResPressurePSI < BrakeLine1PressurePSI)
                {
                    float dp = elapsedClockSeconds * MaxAuxilaryChargingRate;
                    if (AuxResPressurePSI + dp > BrakeLine1PressurePSI - dp * AuxBrakeLineVolumeRatio)
                        dp = (BrakeLine1PressurePSI - AuxResPressurePSI) / (1 + AuxBrakeLineVolumeRatio);
                    AuxResPressurePSI += dp;
                    BrakeLine1PressurePSI -= dp * AuxBrakeLineVolumeRatio;
                }
            }
            if (BrakeLine3PressurePSI >= 1000)
            {
                BrakeLine3PressurePSI -= 1000;
                AutoCylPressurePSI -= 4 * elapsedClockSeconds;
            }
            if (AutoCylPressurePSI < 0)
                AutoCylPressurePSI = 0;
            if (AutoCylPressurePSI < BrakeLine3PressurePSI)
                CylPressurePSI = BrakeLine3PressurePSI;
            else
                CylPressurePSI = AutoCylPressurePSI;
            float f = MaxBrakeForceN * CylPressurePSI / MaxCylPressurePSI;
            if (f < MaxHandbrakeForceN * HandbrakePercent / 100)
                f = MaxHandbrakeForceN * HandbrakePercent / 100;
            Car.FrictionForceN += f;
        }

        public override void SetRetainer(RetainerSetting setting)
        {
            switch (setting)
            {
                case RetainerSetting.Exhaust:
                    RetainerPressureThresholdPSI = 0;
                    ReleaseRate = MaxReleaseRate;
                    break;
                case RetainerSetting.HighPressure:
                    RetainerPressureThresholdPSI = 20;
                    ReleaseRate = (50 - 20) / 90f;
                    break;
                case RetainerSetting.LowPressure:
                    RetainerPressureThresholdPSI = 10;
                    ReleaseRate = (50 - 10) / 60f;
                    break;
                case RetainerSetting.SlowDirect:
                    RetainerPressureThresholdPSI = 0;
                    ReleaseRate = (50 - 10) / 86f;
                    break;
            }
        }

        public override void SetHandbrakePercent(float percent)
        {
            if (percent < 0) percent = 0;
            if (percent > 100) percent = 100;
            HandbrakePercent = percent;
        }

        public override void Increase()
        {
            AISetPercent(BrakePercent + 10);
        }

        public override void Decrease()
        {
            AISetPercent(BrakePercent - 10);
        }

        public override void AISetPercent(float percent)
        {
            if (percent < 0) percent = 0;
            if (percent > 100) percent = 100;
            BrakePercent = percent;
            Car.Train.BrakeLine1PressurePSI = 90 - 26 * BrakePercent / 100;
        }
    }

    public class VacuumSinglePipe : MSTSBrakeSystem
    {
        float MaxHandbrakeForceN = 0;
        float MaxBrakeForceN = 89e3f;
        float MaxPressurePSI = 21;
        float BrakePercent = 0;  // simplistic system
        TrainCar Car;

        public VacuumSinglePipe( TrainCar car )
        {
            Car = car;
        }

        public override void InitializeFromCopy(BrakeSystem copy)
        {
            VacuumSinglePipe thiscopy = (VacuumSinglePipe)copy;
            MaxBrakeForceN = thiscopy.MaxBrakeForceN;
            MaxHandbrakeForceN = thiscopy.MaxHandbrakeForceN;
            MaxPressurePSI = thiscopy.MaxPressurePSI;
        }

        public override string GetStatus(int detailLevel)
        {
            return string.Format( "{0:F0}", BrakeLine1PressurePSI);
        }

        public override void Parse(string lowercasetoken, STFReader f)
        {
            switch (lowercasetoken)
            {
                case "wagon(maxhandbrakeforce": MaxHandbrakeForceN = f.ReadFloatBlock(); break;
                case "wagon(maxbrakeforce": MaxBrakeForceN = f.ReadFloatBlock(); break;
                case "wagon(brakecylinderpressureformaxbrakebrakeforce": MaxPressurePSI = f.ReadFloatBlock(); break;
            }
        }

        public override void Save(BinaryWriter outf)
        {
            outf.Write(BrakeLine1PressurePSI);
            outf.Write(BrakeLine2PressurePSI);
            outf.Write(BrakeLine3PressurePSI);
            outf.Write(BrakePercent);
        }

        public override void Restore(BinaryReader inf)
        {
            BrakeLine1PressurePSI = inf.ReadSingle();
            BrakeLine2PressurePSI = inf.ReadSingle();
            BrakeLine3PressurePSI = inf.ReadSingle();
            BrakePercent = inf.ReadSingle();
        }

        public override void Initialize(bool handbrakeOn)
        {
        }
        public override void Update(float elapsedClockSeconds)
        {
            if (BrakeLine1PressurePSI < 0)
                return; // pipes not connected
            // Unrealistic temporary code
            float brakePercent = 100 * (1 - BrakeLine1PressurePSI / MaxPressurePSI);
            if (brakePercent > 100) brakePercent = 100;
            if (brakePercent < 0) brakePercent = 0;
            Car.FrictionForceN += MaxBrakeForceN * brakePercent/100f; 
        }

        public override void SetHandbrakePercent(float percent)
        {
            // TODO
        }
        public override void SetRetainer(RetainerSetting setting)
        {
        }

        public override void Increase()
        {
            AISetPercent(BrakePercent + 10);
        }

        public override void Decrease()
        {
            AISetPercent(BrakePercent - 10);
        }

        public override void AISetPercent(float percent)
        {
            if (percent < 0) percent = 0;
            if (percent > 100) percent = 100;
            BrakePercent = percent;
            Car.Train.BrakeLine1PressurePSI = MaxPressurePSI * (1 - BrakePercent / 100);
        }
    }
}

