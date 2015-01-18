﻿// COPYRIGHT 2009, 2010, 2011, 2012, 2013 by the Open Rails project.
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

/* STEAM LOCOMOTIVE CLASSES
 * 
 * The Locomotive is represented by two classes:
 *  MSTSDieselLocomotiveSimulator - defines the behaviour, ie physics, motion, power generated etc
 *  MSTSDieselLocomotiveViewer - defines the appearance in a 3D viewer. The viewer doesn't
 *  get attached to the car until it comes into viewing range.
 *  
 * Both these classes derive from corresponding classes for a basic locomotive
 *  LocomotiveSimulator - provides for movement, basic controls etc
 *  LocomotiveViewer - provides basic animation for running gear, wipers, etc
 * 
 */
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using Microsoft.Xna.Framework;
using MSTS.Formats;
using MSTS.Parsers;
using ORTS.Common;
using ORTS.Viewer3D;

namespace ORTS
{
    ///////////////////////////////////////////////////
    ///   SIMULATION BEHAVIOUR
    ///////////////////////////////////////////////////

    /// <summary>
    /// Adds physics and control for a steam locomotive
    /// </summary>
    public class MSTSSteamLocomotive: MSTSLocomotive
    {
        //Configure a default cutoff controller
        //If none is specified, this will be used, otherwise those values will be overwritten
        public MSTSNotchController CutoffController = new MSTSNotchController(-0.9f, 0.9f, 0.1f);
        public MSTSNotchController Injector1Controller = new MSTSNotchController(0, 1, 0.1f);
        public MSTSNotchController Injector2Controller = new MSTSNotchController(0, 1, 0.1f);
        public MSTSNotchController BlowerController = new MSTSNotchController(0, 1, 0.1f);
        public MSTSNotchController DamperController = new MSTSNotchController(0, 1, 0.1f);
        public MSTSNotchController FiringRateController = new MSTSNotchController(0, 1, 0.1f);
        public MSTSNotchController FireboxDoorController = new MSTSNotchController(0, 1, 0.1f);
        public MSTSNotchController FuelController = new MSTSNotchController(0, 1, 0.01f); // Could be coal, wood, oil or even peat !
        public MSTSNotchController WaterController = new MSTSNotchController(0, 1, 0.01f);

        public bool Injector1IsOn;
        public bool Injector2IsOn;
        public bool CylinderCocksAreOpen;
        bool FiringIsManual;
        bool BlowerIsOn = false;
        bool BoilerIsPriming = false;
        bool WaterIsExhausted = false;
        bool CoalIsExhausted = false;
        bool FireIsExhausted = false;
        bool FuelBoost = false;
        bool FuelBoostReset = false;
        bool StokerIsMechanical = false;
        bool HotStart; // Determine whether locomotive is started in hot or cold state - selectable option in Options TAB
        bool BoilerHeat = false;
        bool HasSuperheater = false;
        bool safety2IsOn = false; // Safety valve #2 is on and opertaing
        bool safety3IsOn = false; // Safety valve #3 is on and opertaing
        bool safety4IsOn = false; // Safety valve #4 is on and opertaing
        bool IsGearedSteamLoco = false; // Indicates that it is a geared locomotive
        bool IsFixGeared = false;
        bool IsSelectGeared = false; 
        bool IsLocoSlip = false; 	// locomotive is slipping

        float PulseTracker;
        int NextPulse = 1;

        // state variables
        float BoilerHeatBTU;        // total heat in water and steam in boiler - lb/s * SteamHeat(BTU/lb)
        float MaxBoilerHeatBTU;   // Boiler heat at max output and pressure, etc
        float MaxBoilerHeatPressurePSI; // Boiler Pressure for calculating max boiler pressure, includes safety valve pressure
        float PreviousBoilerHeatInBTU; // Hold previous boiler heat value
        float BoilerStartkW;        // calculate starting boilerkW
        float MaxBoilerHeatInBTUpS = 0.1f; // Remember the BoilerHeat value equivalent to Max Boiler Heat
        float MaxBoilerPressHeatBTU;  // Boiler heat at max boiler pressure
        float baseStartTempK;     // Starting water temp
        float StartBoilerHeatBTU;
        float FiringSteamUsageRateLBpS;   // rate if excessive usage
        float BoilerHeatRatio = 1.0f;   // Boiler heat ratio, if boiler heat exceeds, normal boiler pressure boiler heat
        float MaxBoilerHeatRatio = 1.0f;   // Max Boiler heat ratio, if boiler heat exceeds, safety boiler pressure boiler heat
        SmoothedData BoilerHeatSmoothBTU = new SmoothedData(60);       // total heat in water and steam in boiler - lb/s * SteamHeat(BTU/lb)
        float BoilerMassLB;         // total mass of water and steam in boiler

        float BoilerKW;                 // power of boiler
        float MaxBoilerKW;              // power of boiler at full performance
        float BoilerSteamHeatBTUpLB;    // Steam Heat based on current boiler pressure
        float BoilerWaterHeatBTUpLB;    // Water Heat based on current boiler pressure
        float BoilerSteamDensityLBpFT3; // Steam Density based on current boiler pressure
        float BoilerWaterDensityLBpFT3; // Water Density based on current boiler pressure
        float BoilerWaterTempK; 
        float FuelBurnRateKGpS;
        float FuelFeedRateKGpS;
        float DesiredChange;     // Amount of change to increase fire mass, clamped to range 0.0 - 1.0
        public float CylinderSteamUsageLBpS;
        public float NewCylinderSteamUsageLBpS;
        public float BlowerSteamUsageLBpS;
        public float BoilerPressurePSI;     // Gauge pressure - what the engineer sees.
 
        float WaterFraction;        // fraction of boiler volume occupied by water
        public float EvaporationLBpS;          // steam generation rate
        public float FireMassKG;      // Mass of coal currently on grate area
        public float FireRatio;
        float FlueTempK = 775;      // Initial FlueTemp (best @ 475)
        float MaxFlueTempK;         // FlueTemp at full boiler performance
        public bool SafetyIsOn;
        public readonly SmoothedData SmokeColor = new SmoothedData(2);

        // eng file configuration parameters

        float MaxBoilerPressurePSI = 180f;  // maximum boiler pressure, safety valve setting
        float BoilerVolumeFT3;      // total space in boiler that can hold water and steam
        int NumCylinders = 2;
        float CylinderStrokeM;
        float CylinderDiameterM;
        float MaxBoilerOutputLBpH;  // maximum boiler steam generation rate
        float IdealFireMassKG;      // Target fire mass
        float MaxFireMassKG;        // Max possible fire mass
        float MaxFiringRateKGpS;              // Max rate at which fireman or stoker can can feed coal into fire
        float GrateLimitLBpS = 140.0f;       // Max combustion rate of the grate, once this is reached, no more steam is produced.
        float PreviousFireHeatTxfKW;    // Capture max FireHeat value before Grate limit is exceeded.
        float GrateCombustionRateLBpFt2;     // Grate combustion rate, ie how many lbs coal burnt per sq ft grate area.
        float ORTSMaxFiringRateKGpS;          // OR equivalent of above
        float DisplayMaxFiringRateKGpS;     // Display value of MaxFiringRate
        public float SafetyValveUsageLBpS;
        float SafetyValveDropPSI = 4.0f;      // Pressure drop before Safety valve turns off, normally around 4 psi - First safety valve normally operates between MaxBoilerPressure, and MaxBoilerPressure - 4, ie Max Boiler = 200, cutoff = 196.
        float EvaporationAreaM2;
        float SuperheatAreaM2 = 0.0f;      // Heating area of superheater
        float SuperheatKFactor = 11.7f;     // Factor used to calculate superheat temperature - guesstimate
        float SuperheatRefTempF;            // Superheat temperature in deg Fahrenheit, based upon the heating area.
        float SuperheatTempRatio;          // A ratio used to calculate the superheat - based on the ratio of superheat (using heat area) to "known" curve. 
        float CurrentSuperheatTeampF;      // current value of superheating based upon boiler steam output
        float SuperheatVolumeRatio;   // Approximate ratio of Superheated steam to saturated steam at same pressure
        float FuelCalorificKJpKG = 33400;
        float ManBlowerMultiplier = 20.0f; // Blower Multipler for Manual firing
        float ShovelMassKG = 6;
//        float BurnRateMultiplier = 1.0f; // Used to vary the rate at which fuels burns at - used as a player customisation factor.
        float HeatRatio = 0.001f;        // Ratio to control burn rate - based on ratio of heat in vs heat out
        float PressureRatio = 0.01f;    // Ratio to control burn rate - based upon boiler pressure
        float BurnRateRawKGpS;           // Raw combustion (burn) rate
        SmoothedData FuelRateStoker = new SmoothedData(15); // Stoker is more responsive and only takes x seconds to fully react to changing needs.
        SmoothedData FuelRate = new SmoothedData(45); // Automatic fireman takes x seconds to fully react to changing needs.
        SmoothedData BurnRateSmoothKGpS = new SmoothedData(120); // Changes in BurnRate take x seconds to fully react to changing needs.
        float FuelRateSmooth = 0.0f;     // Smoothed Fuel Rate
        
        // precomputed values
        float CylinderSweptVolumeFT3pFT;     // Volume of steam Cylinder
        float BlowerSteamUsageFactor;
        float InjectorFlowRateLBpS;
        Interpolator BackPressureIHPtoAtmPSI;             // back pressure in cylinders given usage
        Interpolator CylinderSteamDensityPSItoLBpFT3;   // steam density in cylinders given pressure (could be super heated)
        Interpolator SteamDensityPSItoLBpFT3;   // saturated steam density given pressure
        Interpolator WaterDensityPSItoLBpFT3;   // water density given pressure
        Interpolator SteamHeatPSItoBTUpLB;      // total heat in saturated steam given pressure
        Interpolator WaterHeatPSItoBTUpLB;      // total heat in water given pressure
        Interpolator HeatToPressureBTUpLBtoPSI; // pressure given total heat in water (inverse of WaterHeat)
        Interpolator BurnRateLBpStoKGpS;        // fuel burn rate given steam usage - units in kg/s
        Interpolator PressureToTemperaturePSItoF;
        Interpolator InjDelWaterTempMinPressureFtoPSI; // Injector Delivery Water Temp - Minimum Capacity
        Interpolator InjDelWaterTempMaxPressureFtoPSI; // Injector Delivery Water Temp - Maximum Capacity
        Interpolator InjWaterFedSteamPressureFtoPSI; // Injector Water Lbs of water per lb steam used
        Interpolator InjCapMinFactorX; // Injector Water Table to determin min capacity - max/min
        Interpolator Injector09FlowratePSItoUKGpM;  // Flowrate of 09mm injector in gpm based on boiler pressure        
        Interpolator Injector10FlowratePSItoUKGpM;  // Flowrate of 10mm injector in gpm based on boiler pressure
        Interpolator Injector11FlowratePSItoUKGpM;  // Flowrate of 11mm injector in gpm based on boiler pressure
        Interpolator Injector13FlowratePSItoUKGpM;  // Flowrate of 13mm injector in gpm based on boiler pressure 
        Interpolator Injector14FlowratePSItoUKGpM;  // Flowrate of 14mm injector in gpm based on boiler pressure         
        Interpolator Injector15FlowratePSItoUKGpM;  // Flowrate of 15mm injector in gpm based on boiler pressure                       
        Interpolator SpecificHeatKtoKJpKGpK;        // table for specific heat capacity of water at temp of water
        Interpolator SaturationPressureKtoPSI;      // Saturated pressure of steam (psi) @ water temperature (K)
        Interpolator BoilerEfficiencyGrateAreaLBpFT2toX;      //  Table to determine boiler efficiency based upon lbs of coal per sq ft of Grate Area
        Interpolator BoilerEfficiency;  // boiler efficiency given steam usage
        Interpolator WaterTempFtoPSI;  // Table to convert water temp to pressure
        Interpolator CylinderCondensationFractionX;  // Table to find the cylinder condensation fraction per cutoff for the cylinder - saturated steam
        Interpolator SuperheatTempLimitXtoDegF;  // Table to find Super heat temp required to prevent cylinder condensation - Ref Elseco Superheater manual
        Interpolator SuperheatTempLbpHtoDegF;  // Table to find Super heat temp per lbs of steam to cylinder - from BTC Test Results for Std 8
        Interpolator InitialPressureDropRatioRpMtoX; // Allowance for wire-drawing - ie drop in initial pressure (cutoff) as speed increases
        Interpolator SteamChestPressureDropRatioRpMtoX; // Allowance for pressure drop in Steam chest pressure compared to Boiler Pressure
        
        Interpolator SaturatedSpeedFactorSpeedDropFtpMintoX; // Allowance for drop in TE for a saturated locomotive due to piston speed limitations
        Interpolator SuperheatedSpeedFactorSpeedDropFtpMintoX; // Allowance for drop in TE for a superheated locomotive due to piston speed limitations

        Interpolator NewBurnRateSteamToCoalLbspH; // Combustion rate of steam generated per hour to Dry Coal per hour

        Interpolator2D CutoffInitialPressureDropRatioUpper;  // Upper limit of the pressure drop from initial pressure to cut-off pressure
        Interpolator2D CutoffInitialPressureDropRatioLower;  // Lower limit of the pressure drop from initial pressure to cut-off pressure

  #region Additional steam properties
        const float SpecificHeatCoalKJpKGpK = 1.26f; // specific heat of coal - kJ/kg/K
        const float SteamVaporSpecVolumeAt100DegC1BarM3pKG = 1.696f;
        float WaterHeatBTUpFT3;             // Water heat in btu/ft3
        bool FusiblePlugIsBlown = false;    // Fusible plug blown, due to lack of water in the boiler
        bool LocoIsOilBurner = false;       // Used to identify if loco is oil burner
        float GrateAreaM2;                  // Grate Area in SqM
        float IdealFireDepthIN = 7.0f;      // Assume standard coal coverage of grate = 7 inches.
        float FuelDensityKGpM3 = 864.5f;    // Anthracite Coal : 50 - 58 (lb/ft3), 800 - 929 (kg/m3)
        float DamperFactorManual = 1.0f;    // factor to control draft through fire when locomotive is running in Manual mode
        const float WaterLBpUKG = 10.0f;    // lbs of water in 1 gal (uk)
        public float MaxTenderCoalMassKG;          // Maximum read from Eng File
        public float MaxTenderWaterMassKG;         // Maximum read from Eng file
        public float TenderCoalMassKG              // Decreased by firing and increased by refilling
        {
            get { return FuelController.CurrentValue * MaxTenderCoalMassKG; }
            set { FuelController.CurrentValue = value / MaxTenderCoalMassKG; }
        }
        public float TenderWaterVolumeUKG          // Decreased by running injectors and increased by refilling
        {
            get { return WaterController.CurrentValue * Kg.ToLb(MaxTenderWaterMassKG) / WaterLBpUKG; }
            set { WaterController.CurrentValue = value / (Kg.ToLb(MaxTenderWaterMassKG) / WaterLBpUKG); }
        }
        float DamperBurnEffect;             // Effect of the Damper control
        float Injector1Fraction = 0.0f;     // Fraction (0-1) of injector 1 flow from Fireman controller or AI
        float Injector2Fraction = 0.0f;     // Fraction (0-1) of injector  of injector 2 flow from Fireman controller or AI
        float SafetyValveStartPSI = 0.1f;   // Set safety valve to just over max pressure - allows for safety valve not to operate in AI firing
        float InjectorBoilerInputLB = 0.0f; // Input into boiler from injectors
        const float WaterDensityAt100DegC1BarKGpM3 = 954.8f;

        // Air Compressor Characteristics - assume 9.5in x 10in Compressor operating at 120 strokes per min.          
        float CompCylDiaIN = 9.5f;
        float CompCylStrokeIN = 10.0f;
        float CompStrokespM = 120.0f;
        float CompSteamUsageLBpS = 0.0f;
        const float BTUpHtoKJpS = 0.000293071f;     // Convert BTU/s to Kj/s
        float BoilerHeatTransferCoeffWpM2K = 45.0f; // Heat Transfer of locomotive boiler 45 Wm2K
        float TotalSteamUsageLBpS;                  // Running total for complete current steam usage
        float GeneratorSteamUsageLBpS = 1.0f;       // Generator Steam Usage
        float RadiationSteamLossLBpS = 2.5f;        // Steam loss due to radiation losses
        float BlowerBurnEffect;                     // Effect of Blower on burning rate
        float FlueTempDiffK;                        // Current difference in flue temp at current firing and steam usage rates.
        float FireHeatTxfKW;                        // Current heat generated by the locomotive fire
        float HeatMaterialThicknessFactor = 1.0f;   // Material thickness for convection heat transfer
        float TheoreticalMaxSteamOutputLBpS;        // Max boiler output based upon Output = EvapArea x 15 ( lbs steam per evap area)

        // Water model - locomotive boilers require water level to be maintained above the firebox crown sheet
        // This model is a crude representation of a water gauge based on a generic boiler and 8" water gauge
        // Based on a scaled drawing following water fraction levels have been used - crown sheet = 0.7, min water level = 0.73, max water level = 0.89
        float WaterMinLevel = 0.7f;         // min level before we blow the fusible plug
        float WaterMinLevelSafe = 0.75f;    // min level which you would normally want for safety
        float WaterMaxLevel = 0.91f;        // max level above which we start priming
        float WaterMaxLevelSafe = 0.90f;    // max level below which we stop priming
        float WaterGlassMaxLevel = 0.89f;   // max height of water gauge as a fraction of boiler level
        float WaterGlassMinLevel = 0.73f;   // min height of water gauge as a fraction of boiler level
        float WaterGlassLengthIN = 8.0f;    // nominal length of water gauge
        float WaterGlassLevelIN;            // Water glass level in inches
        float MEPFactor = 0.7f;             // Factor to determine the MEP
        float GrateAreaDesignFactor = 500.0f;   // Design factor for determining Grate Area
        float EvapAreaDesignFactor = 10.0f;     // Design factor for determining Evaporation Area

        float SpecificHeatWaterKJpKGpC; // Specific Heat Capacity of water in boiler (from Interpolator table) kJ/kG per deg C
        float WaterTempIN;              // Input to water Temp Integrator.
        float WaterTempNewK;            // Boiler Water Temp (Kelvin) - for testing purposes
        float BkW_Diff;                 // Net Energy into boiler after steam loads taken.
        float WaterVolL;                // Actual volume of water in bolier (litres)
        float BoilerHeatOutBTUpS = 0.0f;// heat out of boiler in BTU
        float BoilerHeatInBTUpS = 0.0f; // heat into boiler in BTU
        float InjCylEquivSizeIN;        // Calculate the equivalent cylinder size for purpose of sizing the injector.
        float InjectorSize;             // size of injector installed on boiler

        // Values from previous iteration to use in UpdateFiring() and show in HUD
        float PreviousBoilerHeatOutBTUpS = 0.0f;
        public float PreviousTotalSteamUsageLBpS;
        float Injector1WaterDelTempF;   // Injector 1 water delivery temperature - F
        float Injector2WaterDelTempF;   // Injector 1 water delivery temperature - F
        float Injector1TempFraction;    // Find the fraction above the min temp of water delivery
        float Injector2TempFraction;    // Find the fraction above the min temp of water delivery
        float Injector1WaterTempPressurePSI;  // Pressure equivalent of water delivery temp
        float Injector2WaterTempPressurePSI;  // Pressure equivalent of water delivery temp
        float MaxInject1SteamUsedLbpS;  // Max steam injected into boiler when injector operating at full value - Injector 1
        float MaxInject2SteamUsedLbpS;  // Max steam injected into boiler when injector operating at full value - Injector 2
        float ActInject1SteamUsedLbpS;  // Act steam injected into boiler when injector operating at current value - Injector 1
        float ActInject2SteamUsedLbpS;  // Act steam injected into boiler when injector operating at current value - Injector 2   
        float Inject1SteamHeatLossBTU;  // heat loss due to steam usage from boiler for injector operation - Injector 1     
        float Inject2SteamHeatLossBTU;  // heat loss due to steam usage from boiler for injector operation - Injector 2
        float Inject1WaterHeatLossBTU;  // heat loss due to water injected into the boiler for injector operation - Injector 1   
        float Inject2WaterHeatLossBTU;  // heat loss due to water injected into the boiler for injector operation - Injector 1                        

        // Derating factors for motive force 
        float BoilerPrimingDeratingFactor = 0.1f;   // Factor if boiler is priming
        float OneAtmospherePSI = 14.696f;      // Atmospheric Pressure
        
        float SuperheaterFactor = 1.0f;               // Currently 2 values respected: 0.0 for no superheat (default), > 1.0 for typical superheat
        float SuperheaterSteamUsageFactor = 1.0f;       // Below 1.0, reduces steam usage due to superheater
        float Stoker = 0.0f;                // Currently 2 values respected: 0.0 for no mechanical stoker (default), = 1.0 for typical mechanical stoker
        float StokerMaxUsage = 0.01f;       // Max steam usage of stoker - 1% of max boiler output
        float StokerMinUsage = 0.005f;      // Min Steam usage - just to keep motor ticking over - 0.5% of max boiler output
        float StokerSteamUsageLBpS;         // Current steam usage of stoker
        const float BoilerKWtoBHP = 0.101942f;  // Convert Boiler kW to Boiler HP, note different to HP.
        float MaxTheoreticalFiringRateKgpS;     // Max firing rate that fireman can sustain for short periods
        float FuelBoostOnTimerS = 0.01f;    // Timer to allow fuel boosting for a short while
        float FuelBoostResetTimerS = 0.01f; // Timer to rest fuel boosting for a while
        float TimeFuelBoostOnS = 300.0f;    // Time to allow fuel boosting to go on for 
        float TimeFuelBoostResetS = 1200.0f;// Time to wait before next fuel boost
        float throttle;
        float SpeedEquivMpS = 27.0f;          // Equvalent speed of 60mph in mps (27m/s) - used for damper control

// Cylinder related parameters
        float CutoffPressureDropRatio;  // Ratio of Cutoff Pressure to Initial Pressure
        float CylinderPressureAtmPSI;
        float BackPressureAtmPSI;
        float CutoffPressureAtmPSI;

        float CylinderAdmissionWorkInLbs; // Work done during steam admission into cylinder
        float CylinderExhaustOpenFactor; // Point on cylinder stroke when exhaust valve opens.
        float CylinderCompressionCloseFactor; // Point on cylinder stroke when compression valve closes - assumed reciporical of exhaust opens.
        float CylinderPreAdmissionOpenFactor = 0.05f; // Point on cylinder stroke when pre-admission valve opens
        float CylinderExhaustPressureAtmPSI;       // Pressure when exhaust valve opens
        float CylinderPreCompressionPressureAtmPSI;       // Pressure when exhaust valve closes
        float CylinderPreAdmissionPressureAtmPSI;    // Pressure after compression occurs and steam admission starts
        float CylinderExpansionWorkInLbs; // Work done during expansion stage of cylinder
        float CylinderReleaseWorkInLbs;   // Work done during release stage of cylinder
        float CylinderCompressionWorkInLbs; // Work done during compression stage of cylinder
        float CylinderPreAdmissionWorkInLbs; // Work done during PreAdmission stage of cylinder
        float CylinderExhaustWorkInLbs; // Work done during Exhaust stage of cylinder

        float MeanEffectivePressurePSI;         // Mean effective pressure
        float RatioOfExpansion;             // Ratio of expansion
        float CylinderClearancePC = 0.09f;    // Assume cylinder clearance of 8% of the piston displacement for saturated locomotives and 9% for superheated locomotive - default to saturated locomotive value
        float CylinderPortOpeningFactor;   // Model the size of the steam port opening in the cylinder - set to 0.085 as default, if no ENG file value added
        float CylinderPortOpeningUpper = 0.12f; // Set upper limit for Cylinder port opening
        float CylinderPortOpeningLower = 0.05f; // Set lower limit for Cylinder port opening
        float CylinderPistonShaftFt3;   // Volume taken up by the cylinder piston shaft
        float CylinderPistonShaftDiaIn = 3.5f; // Assume cylinder piston shaft to be 3.5 inches
        float CylinderPistonAreaFt2;    // Area of the piston in the cylinder
        float SteamChestPressurePSI;    // Pressure in steam chest - input to cylinder
        float InitialPressureAtmPSI;
        
        const int CylStrokesPerCycle = 2;  // each cylinder does 2 strokes for every wheel rotation, within each stroke
        float CylinderEfficiencyRate = 1.0f; // Factor to vary the output power of the cylinder without changing steam usage - used as a player customisation factor.
        public float CylCockSteamUsageLBpS = 0.0f; // Cylinder Cock Steam Usage
        float CylCockDiaIN = 0.5f;          // Steam Cylinder Cock orifice size
        float CylCockPressReduceFactor;     // Factor to reduce cylinder pressure by if cocks open
        
        float DrvWheelRevRpS;       // number of revolutions of the drive wheel per minute based upon speed.
        float PistonSpeedFtpM;      // Piston speed of locomotive
        float IndicatedHorsePowerHP;   // Indicated Horse Power (IHP), theoretical power of the locomotive, it doesn't take into account the losses due to friction, etc. Typically output HP will be 70 - 90% of the IHP
        float DrawbarHorsePowerHP;  // Drawbar Horse Power  (DHP), maximum power available at the wheels.
        float DrawBarPullLbsF;      // Drawbar pull in lbf
        float BoilerEvapRateLbspFt2;  // Sets the evaporation rate for the boiler is used to multiple boiler evaporation area by - used as a player customisation factor.

        float SpeedFactor;      // Speed factor - factor to reduce TE due to speed increase - American locomotive company
        
        public float MaxTractiveEffortLbf;     // Maximum theoritical tractive effort for locomotive
        float DisplayTractiveEffortLbsF; // Value of Tractive eefort to display in HUD
        float CriticalSpeedTractiveEffortLbf;  // Speed at which the piston speed reaches it maximum recommended value
        float StartTractiveEffortN = 0.0f;      // Record starting tractive effort
        float TractiveEffortLbsF;           // Current sim calculated tractive effort
        const float TractiveEffortFactor = 0.85f;  // factor for calculating Theoretical Tractive Effort
        
        float MaxLocoSpeedMpH;      // Speed of loco when max performance reached
        float MaxPistonSpeedFtpM;   // Piston speed @ max performance for the locomotive
        float MaxIndicatedHorsePowerHP; // IHP @ max performance for the locomotive
        float absSpeedMpS;
        
        float cutoff;
        public float DrvWheelWeightKg; // weight on locomotive drive wheel, includes drag factor
        float NumSafetyValves;  // Number of safety valves fitted to locomotive - typically 1 to 4
        float SafetyValveSizeIn;    // Size of the safety value - all will be the same size.
        float SafetyValveSizeDiaIn2; // Area of the safety valve - impacts steam discharge rate - is the space when the valve lifts
        float MaxSafetyValveDischargeLbspS; // Steam discharge rate of all safety valves combined.
        float SafetyValveUsage1LBpS; // Usage rate for safety valve #1
        float SafetyValveUsage2LBpS; // Usage rate for safety valve #2
        float SafetyValveUsage3LBpS; // Usage rate for safety valve #3
        float SafetyValveUsage4LBpS; // Usage rate for safety valve #4
        float MaxSteamGearPistonRateFtpM;   // Max piston rate for a geared locomotive, such as a Shay
        float SteamGearRatio;   // Gear ratio for a geared locomotive, such as a Shay  
        float SteamGearRatioLow;   // Gear ratio for a geared locomotive, such as a Shay
        float SteamGearRatioHigh;   // Gear ratio for a two speed geared locomotive, such as a Climax
        float LowMaxGearedSpeedMpS;  // Max speed of the geared locomotive - Low Gear
        float HighMaxGearedSpeedMpS; // Max speed of the geared locomotive - High Gear
        float MotiveForceGearRatio; // mulitplication factor to be used in calculating motive force etc, when a geared locomotive.
        float SteamGearPosition = 0.0f; // Position of Gears if set
        

       float TangentialCrankWheelForceLbf; 		// Tangential force on wheel
       float StaticWheelFrictionForceLbf; 		// Static force on wheel due to adhesion	
       float PistonForceLbf;    // Max force exerted by piston.
       float FrictionCoeff; // Co-efficient of friction
       float TangentialWheelTreadForceLbf; // Tangential force at the wheel tread.
       float WheelWeightLbs; // Weight per locomotive drive wheel

  #endregion 

  #region Variables for visual effects (steam, smoke)

        public readonly SmoothedData StackSteamVelocityMpS = new SmoothedData(2);
        public float StackSteamVolumeM3pS;
        public float CylindersSteamVelocityMpS;
        public float CylindersSteamVolumeM3pS;
        public float SafetyValvesSteamVolumeM3pS;

  #endregion

        public MSTSSteamLocomotive(Simulator simulator, string wagFile)
            : base(simulator, wagFile)
        {
        }

        /// <summary>
        /// Sets the coal level to maximum.
        /// </summary>
        public void RefillTenderWithCoal()
        {
            FuelController.CurrentValue = 1.0f;
        }

        /// <summary>
        /// Sets the water level to maximum.
        /// </summary>
        public void RefillTenderWithWater()
        {
            WaterController.CurrentValue = 1.0f;
        } 
       
        private bool ZeroError(float v, string name)
        {
            if (v > 0)
                return false;
            Trace.TraceWarning("Steam engine value {1} must be defined and greater than zero in {0}", WagFilePath, name);
            return true;
        }

        /// <summary>
        /// Parse the wag file parameters required for the simulator and viewer classes
        /// </summary>
        public override void Parse(string lowercasetoken, STFReader stf)
        {
            switch (lowercasetoken)
            {
	            case "engine(numcylinders": NumCylinders = stf.ReadIntBlock(null); break;
                case "engine(cylinderstroke": CylinderStrokeM = stf.ReadFloatBlock(STFReader.UNITS.Distance, null); break;
                case "engine(cylinderdiameter": CylinderDiameterM = stf.ReadFloatBlock(STFReader.UNITS.Distance, null); break;
                case "engine(ortscylinderexhaustopen": CylinderExhaustOpenFactor = stf.ReadFloatBlock(STFReader.UNITS.None, null); break;
                case "engine(ortscylinderportopening": CylinderPortOpeningFactor = stf.ReadFloatBlock(STFReader.UNITS.None, null); break;
                case "engine(boilervolume": BoilerVolumeFT3 = stf.ReadFloatBlock(STFReader.UNITS.VolumeDefaultFT3, null); break;
                case "engine(maxboilerpressure": MaxBoilerPressurePSI = stf.ReadFloatBlock(STFReader.UNITS.PressureDefaultPSI, null); break;
                case "engine(shovelcoalmass": ShovelMassKG = stf.ReadFloatBlock(STFReader.UNITS.Mass, null); break;
                case "engine(maxtendercoalmass": MaxTenderCoalMassKG = stf.ReadFloatBlock(STFReader.UNITS.Mass, null); break;
                case "engine(maxtenderwatermass": MaxTenderWaterMassKG = stf.ReadFloatBlock(STFReader.UNITS.Mass, null); break;
                case "engine(steamfiremanmaxpossiblefiringrate": MaxFiringRateKGpS = stf.ReadFloatBlock(STFReader.UNITS.MassRateDefaultLBpH, null) / 2.2046f / 3600; break;
                case "engine(steamfiremanismechanicalstoker": Stoker = stf.ReadFloatBlock(STFReader.UNITS.None, null); break;
                case "engine(ortssteamfiremanmaxpossiblefiringrate": ORTSMaxFiringRateKGpS = stf.ReadFloatBlock(STFReader.UNITS.MassRateDefaultLBpH, null) / 2.2046f / 3600; break;
                case "engine(enginecontrollers(cutoff": CutoffController.Parse(stf); break;
                case "engine(enginecontrollers(injector1water": Injector1Controller.Parse(stf); break;
                case "engine(enginecontrollers(injector2water": Injector2Controller.Parse(stf); break;
                case "engine(enginecontrollers(blower": BlowerController.Parse(stf); break;
                case "engine(enginecontrollers(dampersfront": DamperController.Parse(stf); break;
                case "engine(enginecontrollers(shovel": FiringRateController.Parse(stf); break;
                case "engine(enginecontrollers(firedoor": FireboxDoorController.Parse(stf); break;
                case "engine(effects(steamspecialeffects": ParseEffects(lowercasetoken, stf); break;
                case "engine(ortsgratearea": GrateAreaM2 = stf.ReadFloatBlock(STFReader.UNITS.AreaDefaultFT2, null); break;
                case "engine(superheater": SuperheaterFactor = stf.ReadFloatBlock(STFReader.UNITS.None, null); break;
                case "engine(ortsevaporationarea": EvaporationAreaM2 = stf.ReadFloatBlock(STFReader.UNITS.AreaDefaultFT2, null); break;
                case "engine(ortssuperheatarea": SuperheatAreaM2 = stf.ReadFloatBlock(STFReader.UNITS.AreaDefaultFT2, null); break;
                case "engine(ortsfuelcalorific": FuelCalorificKJpKG = stf.ReadFloatBlock(STFReader.UNITS.EnergyDensity, null); break;
            //    case "engine(ortsburnratemultiplier": BurnRateMultiplier = stf.ReadFloatBlock(STFReader.UNITS.None, null); break;
                case "engine(ortsboilerevaporationrate": BoilerEvapRateLbspFt2 = stf.ReadFloatBlock(STFReader.UNITS.None, null); break;
                case "engine(ortscylinderefficiencyrate": CylinderEfficiencyRate = stf.ReadFloatBlock(STFReader.UNITS.None, null); break;
                case "engine(ortscylinderinitialpressuredrop": InitialPressureDropRatioRpMtoX = new Interpolator(stf); break;
                case "engine(ortscylinderbackpressure": BackPressureIHPtoAtmPSI = new Interpolator(stf); break;
                case "engine(ortsburnrate": NewBurnRateSteamToCoalLbspH = new Interpolator(stf); break;
                case "engine(ortsboilerefficiency": BoilerEfficiencyGrateAreaLBpFT2toX = new Interpolator(stf); break;
                case "engine(ortsdrivewheelweight": DrvWheelWeightKg = stf.ReadFloatBlock(STFReader.UNITS.Mass, null); break;
                case "engine(ortssteamgearratio": 
                    stf.MustMatch("(");
                    SteamGearRatioLow = stf.ReadFloat(STFReader.UNITS.None, null);
                    SteamGearRatioHigh = stf.ReadFloat(STFReader.UNITS.None, null);
                    stf.SkipRestOfBlock();
                    break;
                case "engine(ortssteammaxgearpistonrate": MaxSteamGearPistonRateFtpM = stf.ReadFloatBlock(STFReader.UNITS.None, null); break;
                case "engine(ortssteamgeartype":
                    stf.MustMatch("(");
                    string typeString = stf.ReadString();
                    IsFixGeared = String.Compare(typeString, "Fixed") == 0;
                    IsSelectGeared = String.Compare(typeString, "Select") == 0;
                    break;
                default: base.Parse(lowercasetoken, stf); break;
            }
        }

        /// <summary>
        /// This initializer is called when we are making a new copy of a car already
        /// loaded in memory.  We use this one to speed up loading by eliminating the
        /// need to parse the wag file multiple times.
        /// NOTE:  you must initialize all the same variables as you parsed above
        /// </summary>
        public override void Copy(MSTSWagon copy)
        {
            base.Copy(copy);  // each derived level initializes its own variables

            MSTSSteamLocomotive locoCopy = (MSTSSteamLocomotive)copy;
            NumCylinders = locoCopy.NumCylinders;
            CylinderStrokeM = locoCopy.CylinderStrokeM;
            CylinderDiameterM = locoCopy.CylinderDiameterM;
            CylinderExhaustOpenFactor = locoCopy.CylinderExhaustOpenFactor;
            CylinderPortOpeningFactor = locoCopy.CylinderPortOpeningFactor;
            BoilerVolumeFT3 = locoCopy.BoilerVolumeFT3;
            MaxBoilerPressurePSI = locoCopy.MaxBoilerPressurePSI; 
            ShovelMassKG = locoCopy.ShovelMassKG;
            MaxTenderCoalMassKG = locoCopy.MaxTenderCoalMassKG;
            MaxTenderWaterMassKG = locoCopy.MaxTenderWaterMassKG;
            MaxFiringRateKGpS = locoCopy.MaxFiringRateKGpS;
            Stoker = locoCopy.Stoker;
            ORTSMaxFiringRateKGpS = locoCopy.ORTSMaxFiringRateKGpS;
            CutoffController = (MSTSNotchController)locoCopy.CutoffController.Clone();
            Injector1Controller = (MSTSNotchController)locoCopy.Injector1Controller.Clone();
            Injector2Controller = (MSTSNotchController)locoCopy.Injector2Controller.Clone();
            BlowerController = (MSTSNotchController)locoCopy.BlowerController.Clone();
            DamperController = (MSTSNotchController)locoCopy.DamperController.Clone();
            FiringRateController = (MSTSNotchController)locoCopy.FiringRateController.Clone();
            FireboxDoorController = (MSTSNotchController)locoCopy.FireboxDoorController.Clone();
            GrateAreaM2 = locoCopy.GrateAreaM2;
            SuperheaterFactor = locoCopy.SuperheaterFactor;
            EvaporationAreaM2 = locoCopy.EvaporationAreaM2;
            SuperheatAreaM2 = locoCopy.SuperheatAreaM2;
            FuelCalorificKJpKG = locoCopy.FuelCalorificKJpKG;
        //    BurnRateMultiplier = locoCopy.BurnRateMultiplier;
            BoilerEvapRateLbspFt2 = locoCopy.BoilerEvapRateLbspFt2;
            CylinderEfficiencyRate = locoCopy.CylinderEfficiencyRate;
            InitialPressureDropRatioRpMtoX = new Interpolator(locoCopy.InitialPressureDropRatioRpMtoX);
            BackPressureIHPtoAtmPSI = new Interpolator(locoCopy.BackPressureIHPtoAtmPSI);
            NewBurnRateSteamToCoalLbspH = new Interpolator(locoCopy.NewBurnRateSteamToCoalLbspH);
            BoilerEfficiency = locoCopy.BoilerEfficiency;
            DrvWheelWeightKg = locoCopy.DrvWheelWeightKg;
            SteamGearRatioLow = locoCopy.SteamGearRatioLow;
            SteamGearRatioHigh = locoCopy.SteamGearRatioHigh;
            MaxSteamGearPistonRateFtpM = locoCopy.MaxSteamGearPistonRateFtpM;
        }

        /// <summary>
        /// We are saving the game.  Save anything that we'll need to restore the 
        /// status later.
        /// </summary>
        public override void Save(BinaryWriter outf)
        {
            outf.Write(BoilerHeatOutBTUpS);
            outf.Write(BoilerHeatInBTUpS); 
            outf.Write(TenderCoalMassKG);
            outf.Write(TenderWaterVolumeUKG);
            outf.Write(CylinderSteamUsageLBpS);
            outf.Write(BoilerHeatBTU);
            outf.Write(BoilerMassLB);
            outf.Write(BoilerPressurePSI);
            outf.Write(WaterFraction);
            outf.Write(EvaporationLBpS);
            outf.Write(FireMassKG);
            outf.Write(FlueTempK);
            outf.Write(SteamGearPosition);
            ControllerFactory.Save(CutoffController, outf);
            ControllerFactory.Save(Injector1Controller, outf);
            ControllerFactory.Save(Injector2Controller, outf);
            ControllerFactory.Save(BlowerController, outf);
            ControllerFactory.Save(DamperController, outf);
            ControllerFactory.Save(FireboxDoorController, outf);
            ControllerFactory.Save(FiringRateController, outf);
            base.Save(outf);
        }

        /// <summary>
        /// We are restoring a saved game.  The TrainCar class has already
        /// been initialized.   Restore the game state.
        /// </summary>
		public override void Restore(BinaryReader inf)
        {
            BoilerHeatOutBTUpS = inf.ReadSingle();
            BoilerHeatInBTUpS = inf.ReadSingle(); 
            TenderCoalMassKG = inf.ReadSingle();
            TenderWaterVolumeUKG = inf.ReadSingle();
            CylinderSteamUsageLBpS = inf.ReadSingle();
            BoilerHeatBTU = inf.ReadSingle();
            BoilerMassLB = inf.ReadSingle();
            BoilerPressurePSI = inf.ReadSingle();
            WaterFraction = inf.ReadSingle();
            EvaporationLBpS = inf.ReadSingle();
            FireMassKG = inf.ReadSingle();
            FlueTempK = inf.ReadSingle();
            SteamGearPosition = inf.ReadSingle();
            ControllerFactory.Restore(CutoffController, inf);
            ControllerFactory.Restore(Injector1Controller, inf);
            ControllerFactory.Restore(Injector2Controller, inf);
            ControllerFactory.Restore(BlowerController, inf);
            ControllerFactory.Restore(DamperController, inf);
            ControllerFactory.Restore(FireboxDoorController, inf);
            ControllerFactory.Restore(FiringRateController, inf);
            base.Restore(inf);
        }

        public override void Initialize()
        {
            base.Initialize();

            if (NumCylinders < 0 && ZeroError(NumCylinders, "NumCylinders"))
                NumCylinders = 0;
            if (ZeroError(CylinderDiameterM, "CylinderDiammeter"))
                CylinderDiameterM = 1;
            if (ZeroError(CylinderStrokeM, "CylinderStroke"))
                CylinderStrokeM = 1;
            if (ZeroError(DriverWheelRadiusM, "WheelRadius"))
                DriverWheelRadiusM = 1;
            if (ZeroError(MaxBoilerPressurePSI, "MaxBoilerPressure"))
                MaxBoilerPressurePSI = 1;
            if (ZeroError(BoilerVolumeFT3, "BoilerVolume"))
                BoilerVolumeFT3 = 1;

            #region Initialise additional steam properties

            SteamDensityPSItoLBpFT3 = SteamTable.SteamDensityInterpolatorPSItoLBpFT3();
            WaterDensityPSItoLBpFT3 = SteamTable.WaterDensityInterpolatorPSItoLBpFT3();
            SteamHeatPSItoBTUpLB = SteamTable.SteamHeatInterpolatorPSItoBTUpLB();
            WaterHeatPSItoBTUpLB = SteamTable.WaterHeatInterpolatorPSItoBTUpLB();
            CylinderSteamDensityPSItoLBpFT3 = SteamTable.SteamDensityInterpolatorPSItoLBpFT3();
            HeatToPressureBTUpLBtoPSI = SteamTable.WaterHeatToPressureInterpolatorBTUpLBtoPSI();
            PressureToTemperaturePSItoF = SteamTable.PressureToTemperatureInterpolatorPSItoF();
            Injector09FlowratePSItoUKGpM = SteamTable.Injector09FlowrateInterpolatorPSItoUKGpM();
            Injector10FlowratePSItoUKGpM = SteamTable.Injector10FlowrateInterpolatorPSItoUKGpM();
            Injector11FlowratePSItoUKGpM = SteamTable.Injector11FlowrateInterpolatorPSItoUKGpM();
            Injector13FlowratePSItoUKGpM = SteamTable.Injector13FlowrateInterpolatorPSItoUKGpM();
            Injector14FlowratePSItoUKGpM = SteamTable.Injector14FlowrateInterpolatorPSItoUKGpM();
            Injector15FlowratePSItoUKGpM = SteamTable.Injector15FlowrateInterpolatorPSItoUKGpM();
            InjDelWaterTempMinPressureFtoPSI = SteamTable.InjDelWaterTempMinPressureInterpolatorFtoPSI();
            InjDelWaterTempMaxPressureFtoPSI = SteamTable.InjDelWaterTempMaxPressureInterpolatorFtoPSI();
            InjWaterFedSteamPressureFtoPSI = SteamTable.InjWaterFedSteamPressureInterpolatorFtoPSI();
            InjCapMinFactorX = SteamTable.InjCapMinFactorInterpolatorX();
            WaterTempFtoPSI = SteamTable.TemperatureToPressureInterpolatorFtoPSI();
            SpecificHeatKtoKJpKGpK = SteamTable.SpecificHeatInterpolatorKtoKJpKGpK();
            SaturationPressureKtoPSI = SteamTable.SaturationPressureInterpolatorKtoPSI();

            CylinderCondensationFractionX = SteamTable.CylinderCondensationFractionInterpolatorX();
            SuperheatTempLimitXtoDegF = SteamTable.SuperheatTempLimitInterpolatorXtoDegF();
            SuperheatTempLbpHtoDegF = SteamTable.SuperheatTempInterpolatorLbpHtoDegF();
            SteamChestPressureDropRatioRpMtoX = SteamTable.SteamChestPressureDropRatioInterpolatorRpMtoX();
            
            SaturatedSpeedFactorSpeedDropFtpMintoX = SteamTable.SaturatedSpeedFactorSpeedDropFtpMintoX();
            SuperheatedSpeedFactorSpeedDropFtpMintoX = SteamTable.SuperheatedSpeedFactorSpeedDropFtpMintoX();

            CutoffInitialPressureDropRatioUpper = SteamTable.CutoffInitialPressureUpper();
            CutoffInitialPressureDropRatioLower = SteamTable.CutoffInitialPressureLower();

            // Assign default steam table values if table not in ENG file
            if (BoilerEfficiencyGrateAreaLBpFT2toX == null)
            {
                BoilerEfficiencyGrateAreaLBpFT2toX = SteamTable.BoilerEfficiencyGrateAreaInterpolatorLbstoX();
                Trace.TraceInformation("BoilerEfficiencyGrateAreaLBpFT2toX - default information read from SteamTables");
            }

            // Assign default steam table values if table not in ENG file
            if (InitialPressureDropRatioRpMtoX == null)
            {
                InitialPressureDropRatioRpMtoX = SteamTable.InitialPressureDropRatioInterpolatorRpMtoX();
                Trace.TraceInformation("InitialPressureDropRatioRpMtoX - default information read from SteamTables");
            }

            // Assign default steam table values if table not in ENG file
            if (NewBurnRateSteamToCoalLbspH == null)
            {
                NewBurnRateSteamToCoalLbspH = SteamTable.NewBurnRateSteamToCoalLbspH();
                Trace.TraceInformation("BurnRateSteamToCoalLbspH - default information read from SteamTables");
            }

            RefillTenderWithCoal();
            RefillTenderWithWater();

            // Computed Values
            // Read alternative OR Value for calculation of Ideal Fire Mass
            if (GrateAreaM2 == 0)  // Calculate Grate Area if not present in ENG file
            {
                float MinGrateAreaSizeSqFt = 6.0f;
                GrateAreaM2 = Me2.FromFt2(((NumCylinders / 2.0f) * (Me.ToIn(CylinderDiameterM) * Me.ToIn(CylinderDiameterM) * Me.ToIn(CylinderStrokeM)) * MEPFactor * MaxBoilerPressurePSI) / (Me.ToIn(DriverWheelRadiusM * 2.0f) * GrateAreaDesignFactor));
                GrateAreaM2 = MathHelper.Clamp(GrateAreaM2, Me2.FromFt2(MinGrateAreaSizeSqFt), GrateAreaM2); // Clamp grate area to a minimum value of 6 sq ft
                IdealFireMassKG = GrateAreaM2 * Me.FromIn(IdealFireDepthIN) * FuelDensityKGpM3;
                Trace.TraceWarning("Grate Area not found in ENG file and has been set to {0} m^2", GrateAreaM2); // Advise player that Grate Area is missing from ENG file
            }
            else
                if (LocoIsOilBurner)
                    IdealFireMassKG = GrateAreaM2 * 720.0f * 0.08333f * 0.02382f * 1.293f;  // Check this formula as conversion factors maybe incorrect, also grate area is now in SqM
                else
                    IdealFireMassKG = GrateAreaM2 * Me.FromIn(IdealFireDepthIN) * FuelDensityKGpM3;
            if (MaxFireMassKG == 0) // If not specified, assume twice as much as ideal. 
                // Scale FIREBOX control to show FireMassKG as fraction of MaxFireMassKG.
                MaxFireMassKG = 2 * IdealFireMassKG;

            float baseTempK = C.ToK(C.FromF(PressureToTemperaturePSItoF[MaxBoilerPressurePSI]));
            if (EvaporationAreaM2 == 0)        // If evaporation Area is not in ENG file then synthesize a value
            {
                EvaporationAreaM2 = Me2.FromFt2(((NumCylinders / 2.0f) * (Me.ToIn(CylinderDiameterM) * Me.ToIn(CylinderDiameterM) * Me.ToIn(CylinderStrokeM)) * MEPFactor * MaxBoilerPressurePSI) / (Me.ToIn(DriverWheelRadiusM * 2.0f) * EvapAreaDesignFactor));
                Trace.TraceWarning("Evaporation Area not found in ENG file and has been set to {0} m^2", EvaporationAreaM2); // Advise player that Evaporation Area is missing from ENG file
            }

            CylinderSteamUsageLBpS = 1.0f;  // Set to 1 to ensure that there are no divide by zero errors
            WaterFraction = 0.9f;

            if (BoilerEvapRateLbspFt2 == 0) // If boiler evaporation rate is not in ENG file then set a default value
            {
                BoilerEvapRateLbspFt2 = 15.0f; // Default rate for evaporation rate. Assume a default rate of 15 lbs/sqft of evaporation area
            }
            BoilerEvapRateLbspFt2 = MathHelper.Clamp(BoilerEvapRateLbspFt2, 10.0f, 15.0f); // Clamp BoilerEvap Rate to between 10 & 15
            TheoreticalMaxSteamOutputLBpS = pS.FrompH(Me2.ToFt2(EvaporationAreaM2) * BoilerEvapRateLbspFt2); // set max boiler theoretical steam output

       //     Trace.TraceInformation("Evap Area {0} Evap Rate {1} Max {2}", Me2.ToFt2(EvaporationAreaM2), BoilerEvapRateLbspFt2, TheoreticalMaxSteamOutputLBpS);

            float BoilerVolumeCheck = Me2.ToFt2(EvaporationAreaM2) / BoilerVolumeFT3;    //Calculate the Boiler Volume Check value.
            if (BoilerVolumeCheck > 15) // If boiler volume is not in ENG file or less then a viable figure (ie high ratio figure), then set to a default value
            {
                BoilerVolumeFT3 = Me2.ToFt2(EvaporationAreaM2) / 8.3f; // Default rate for evaporation rate. Assume a default ratio of evaporation area * 1/8.3
                Trace.TraceWarning("Boiler Volume not found in ENG file, or doesn't appear to be a valid figure, and has been set to {0} Ft^3", BoilerVolumeFT3); // Advise player that Boiler Volume is missing from or incorrect in ENG file
            }

            MaxBoilerHeatPressurePSI = MaxBoilerPressurePSI + SafetyValveStartPSI + 5.0f; // set locomotive maximum boiler pressure to calculate max heat, allow for safety valve + a bit
            MaxBoilerPressHeatBTU = WaterFraction * BoilerVolumeFT3 * WaterDensityPSItoLBpFT3[MaxBoilerHeatPressurePSI] * WaterHeatPSItoBTUpLB[MaxBoilerHeatPressurePSI] + (1 - WaterFraction) * BoilerVolumeFT3 * SteamDensityPSItoLBpFT3[MaxBoilerHeatPressurePSI] * SteamHeatPSItoBTUpLB[MaxBoilerHeatPressurePSI];  // calculate the maximum possible heat in the boiler, assuming safety valve and a small margin
            MaxBoilerHeatBTU = WaterFraction * BoilerVolumeFT3 * WaterDensityPSItoLBpFT3[MaxBoilerPressurePSI] * WaterHeatPSItoBTUpLB[MaxBoilerPressurePSI] + (1 - WaterFraction) * BoilerVolumeFT3 * SteamDensityPSItoLBpFT3[MaxBoilerPressurePSI] * SteamHeatPSItoBTUpLB[MaxBoilerPressurePSI];  // calculate the maximum possible heat in the boiler

            MaxBoilerKW = Kg.FromLb(TheoreticalMaxSteamOutputLBpS) * W.ToKW(W.FromBTUpS(SteamHeatPSItoBTUpLB[MaxBoilerPressurePSI]));
            MaxFlueTempK = (MaxBoilerKW / (W.ToKW(BoilerHeatTransferCoeffWpM2K) * EvaporationAreaM2 * HeatMaterialThicknessFactor)) + baseTempK;
                    
            // Determine if Superheater in use
            if (SuperheatAreaM2 == 0) // If super heating area not specified
            {
                if (SuperheaterFactor > 1.0 || SuperheaterFactor == 1.0) // check if MSTS value, then set superheating
                {
                    HasSuperheater = true;
                    SuperheatRefTempF = 200.0f; // Assume a superheating temp of 250degF
                    SuperheatTempRatio = SuperheatRefTempF / SuperheatTempLbpHtoDegF[pS.TopH(TheoreticalMaxSteamOutputLBpS)];
                    SuperheatAreaM2 = Me2.FromFt2((SuperheatRefTempF * pS.TopH(TheoreticalMaxSteamOutputLBpS)) / (C.ToF(C.FromK(MaxFlueTempK)) * SuperheatKFactor)); // Back calculate Superheat area for display purposes only.
                    CylinderClearancePC = 0.09f;
                    
                }
                else
                {
                    HasSuperheater = false;
                    SuperheatRefTempF = 0.0f;
                }
            }
            else  // if OR value implies a superheater is present then calculate
            {

                HasSuperheater = true;

                // Calculate superheat steam reference temperature based upon heating area of superheater
                // SuperTemp = (SuperHeatArea x HeatTransmissionCoeff * (MeanGasTemp - MeanSteamTemp)) / (SteamQuantity * MeanSpecificSteamHeat)
                // Formula has been simplified as follows: SuperTemp = (SuperHeatArea x FlueTempK x SFactor) / SteamQuantity
                // SFactor is a "loose reprentation" =  (HeatTransmissionCoeff / MeanSpecificSteamHeat) - Av figure calculate by comparing a number of "known" units for superheat.
                SuperheatRefTempF = (Me2.ToFt2(SuperheatAreaM2) * C.ToF(C.FromK(MaxFlueTempK)) * SuperheatKFactor) / pS.TopH(TheoreticalMaxSteamOutputLBpS);
                SuperheatTempRatio = SuperheatRefTempF / SuperheatTempLbpHtoDegF[pS.TopH(TheoreticalMaxSteamOutputLBpS)];    // calculate a ratio figure for known value against reference curve. 
                CylinderClearancePC = 0.09f;
            }

            // Assign default steam table values if table not in ENG file 
            // Back pressure increases with the speed of the locomotive, as cylinder finds it harder to exhaust all the steam.

            if (BackPressureIHPtoAtmPSI == null)
            {
                if (HasSuperheater)
                {
                    BackPressureIHPtoAtmPSI = SteamTable.BackpressureSuperIHPtoPSI();
                    Trace.TraceInformation("BackPressureIHPtoAtmPSI (Superheated) - default information read from SteamTables");
                }
                else
                {
                    BackPressureIHPtoAtmPSI = SteamTable.BackpressureSatIHPtoPSI();
                    Trace.TraceInformation("BackPressureIHPtoAtmPSI (Saturated) - default information read from SteamTables");
                }
            }

            // Determine if Cylinder Port Opening  Factor has been set
            if (CylinderPortOpeningFactor == 0)
            {
                CylinderPortOpeningFactor = 0.085f; // Set as default if not specified
            }
            CylinderPortOpeningFactor = MathHelper.Clamp(CylinderPortOpeningFactor, 0.05f, 0.12f); // Clamp Cylinder Port Opening Factor to between 0.05 & 0.12 so that tables are not exceeded   
            
            // Initialise exhaust opening point on cylinder stroke, and its reciprocal compression close factor
            if (CylinderExhaustOpenFactor == 0)
            {
                CylinderExhaustOpenFactor = 0.9f; // If no value in ENG file set to default
                CylinderCompressionCloseFactor = 1.0f - CylinderExhaustOpenFactor; // Point on cylinder stroke when compression valve closes - assumed reciporical of exhaust opens.
                if (CutoffController.MaximumValue > CylinderExhaustOpenFactor )
                {
                    Trace.TraceWarning("Maximum Cutoff {0} value is greater then CylinderExhaustOpenFactor {1}", CutoffController.MaximumValue, CylinderExhaustOpenFactor); // provide warning if exhaust port is likely to open before maximum allowed cutoff value is reached.
                }
            }
            else
            {
                if (CutoffController.MaximumValue > CylinderExhaustOpenFactor)
                {
                    CylinderExhaustOpenFactor = CutoffController.MaximumValue + 0.05f; // Ensure exhaust valve opening is always higher then specificed maximum cutoff value
                    Trace.TraceWarning("Maximum Cutoff {0} value is greater then CylinderExhaustOpenFactor {1}, automatically adjusted", CutoffController.MaximumValue, CylinderExhaustOpenFactor); // provide warning if exhaust port is likely to open before maximum allowed cutoff value is reached.
                }
                CylinderExhaustOpenFactor = MathHelper.Clamp(CylinderExhaustOpenFactor, 0.1f, 0.95f); // Clamp Cylinder Exhaust Port Opening Factor to between 0.1 & 0.95 so that tables are not exceeded   
                CylinderCompressionCloseFactor = 1.0f - CylinderExhaustOpenFactor; // Point on cylinder stroke when compression valve closes - assumed reciporical of exhaust opens.
            }

            // Determine whether to start locomotive in Hot or Cold State
            HotStart = Simulator.Settings.HotStart;

            // Determine whether it is a geared locomotive & Initialise the values

           if(IsSelectGeared)
            {
               // Check for ENG file values
                if ( MaxSteamGearPistonRateFtpM == 0)
                {
                    MaxSteamGearPistonRateFtpM = 500.0f;
                    Trace.TraceWarning("MaxSteamGearPistonRateRpM not found in ENG file, or doesn't appear to be a valid figure, and has been set to default value");
                }
                if ( SteamGearRatioLow == 0)
                {
                    SteamGearRatioLow = 5.0f;
                    Trace.TraceWarning("SteamGearRatioLow not found in ENG file, or doesn't appear to be a valid figure, and has been set to default value");
                }
                if ( SteamGearRatioHigh == 0)
                {
                    SteamGearRatioHigh = 2.0f;
                    Trace.TraceWarning("SteamGearRatioHigh not found in ENG file, or doesn't appear to be a valid figure, and has been set to default value");
                }

               IsGearedSteamLoco = true;    // set flag for geared locomotive  
                MotiveForceGearRatio = 0.0f; // assume in neutral gear as starting position
                SteamGearRatio = 0.0f;   // assume in neutral gear as starting position
                // Calculate maximum locomotive speed - based upon the number of revs for the drive shaft, geared to wheel shaft, and then circumference of drive wheel
                // Max Geared speed = ((MaxPistonSpeed / Gear Ratio) x DrvWheelCircumference) / Feet in mile - miles per min
                LowMaxGearedSpeedMpS = MpS.ToMpH(Me.FromFt(pS.FrompM(MaxSteamGearPistonRateFtpM / SteamGearRatioLow))) * 2.0f * MathHelper.Pi * DriverWheelRadiusM / (2.0f * CylinderStrokeM);
                HighMaxGearedSpeedMpS = MpS.ToMpH(Me.FromFt(pS.FrompM(MaxSteamGearPistonRateFtpM / SteamGearRatioHigh))) * 2.0f * MathHelper.Pi * DriverWheelRadiusM / (2.0f * CylinderStrokeM);
           }
            else if (IsFixGeared)
            {
               // Check for ENG file values
                if ( MaxSteamGearPistonRateFtpM == 0)
                {
                    MaxSteamGearPistonRateFtpM = 700.0f; // Assume same value as standard steam locomotive
                    Trace.TraceWarning("MaxSteamGearPistonRateRpM not found in ENG file, or doesn't appear to be a valid figure, and has been set to default value");
                }
                if ( SteamGearRatioLow == 0)
                {
                    SteamGearRatioLow = 5.0f;
                    Trace.TraceWarning("SteamGearRatioLow not found in ENG file, or doesn't appear to be a valid figure, and has been set to default value");
                }
                IsGearedSteamLoco = true;    // set flag for geared locomotive
                MotiveForceGearRatio = SteamGearRatioLow;
                SteamGearRatio = SteamGearRatioLow;
                // Calculate maximum locomotive speed - based upon the number of revs for the drive shaft, geared to wheel shaft, and then circumference of drive wheel
                // Max Geared speed = ((MaxPistonSpeedFt/m / Gear Ratio) x DrvWheelCircumference) / Feet in mile - miles per min
                LowMaxGearedSpeedMpS = pS.FrompM(MaxSteamGearPistonRateFtpM / SteamGearRatio * MathHelper.Pi * DriverWheelRadiusM * 2.0f);
            }
            else
            {
                IsGearedSteamLoco = false;    // set flag for non-geared locomotive
                MotiveForceGearRatio = 1.0f;  // set gear ratio to default, as not a geared locomotive
                SteamGearRatio = 1.0f;     // set gear ratio to default, as not a geared locomotive
            }

            // Calculate maximum power of the locomotive, based upon the maximum IHP
            // Maximum IHP will occur at different (piston) speed for saturated locomotives and superheated based upon the wheel revolution. Typically saturated locomotive produce maximum power @ a piston speed of 700 ft/min , and superheated will occur @ 1000ft/min
            // Set values for piston speed

	        if ( HasSuperheater)
	        {
                MaxPistonSpeedFtpM = 1000.0f; // if superheated locomotive
                SpeedFactor = SuperheatedSpeedFactorSpeedDropFtpMintoX[MaxPistonSpeedFtpM];
   	        }
	        else if (IsGearedSteamLoco)
	        {
                MaxPistonSpeedFtpM = MaxSteamGearPistonRateFtpM;  // if geared locomotive
                SpeedFactor = SaturatedSpeedFactorSpeedDropFtpMintoX[MaxPistonSpeedFtpM];   // Assume the same as saturated locomotive for time being.
	        }
	        else
	        {
                MaxPistonSpeedFtpM = 700.0f;  // if saturated locomotive
                SpeedFactor = SaturatedSpeedFactorSpeedDropFtpMintoX[MaxPistonSpeedFtpM];
	        }

           // Calculate max velocity of the locomotive based upon above piston speed
          if (SteamGearRatio == 0)
          {
             MaxLocoSpeedMpH = 0.0f;
          }
          else
          {
            MaxLocoSpeedMpH = MpS.ToMpH(Me.FromFt(pS.FrompM(MaxPistonSpeedFtpM / SteamGearRatio))) * 2.0f * MathHelper.Pi * DriverWheelRadiusM / (2.0f * CylinderStrokeM);
          }

          // Check Cylinder efficiency rate to see if set - allows user to improve cylinder performance and reduce losses

          if (CylinderEfficiencyRate == 0)
          {
              CylinderEfficiencyRate = 1.0f; // If no cylinder efficiency rate in the ENG file set to mormal (1.0)
          }

       //   CylinderEfficiencyRate = MathHelper.Clamp(CylinderEfficiencyRate, 0.6f, 1.2f); // Clamp Cylinder Efficiency Rate to between 0.6 & 1.2           
          MaxTractiveEffortLbf = (NumCylinders / 2.0f) * (Me.ToIn(CylinderDiameterM) * Me.ToIn(CylinderDiameterM) * Me.ToIn(CylinderStrokeM) / (2 * Me.ToIn(DriverWheelRadiusM))) * MaxBoilerPressurePSI * TractiveEffortFactor * MotiveForceGearRatio * CylinderEfficiencyRate;

            // Max IHP = (Max TE x Speed) / 375.0, use a factor of 0.85 to calculate max TE
            MaxIndicatedHorsePowerHP = SpeedFactor * (MaxTractiveEffortLbf * MaxLocoSpeedMpH) / 375.0f;

           // If DrvWheelWeight is not in ENG file, then calculate from Factor of Adhesion(FoA) = DrvWheelWeight / Start (Max) Tractive Effort, assume FoA = 4.2

            if (DrvWheelWeightKg == 0) // if DrvWheelWeightKg not in ENG file.
            {
                const float FactorofAdhesion = 4.2f; // Assume a typical factor of adhesion
                DrvWheelWeightKg = Kg.FromLb(FactorofAdhesion * MaxTractiveEffortLbf); // calculate Drive wheel weight if not in ENG file
            }

            // Calculate "critical" speed of locomotive to determine limit of max IHP
            CriticalSpeedTractiveEffortLbf = (MaxTractiveEffortLbf * CylinderEfficiencyRate) * SpeedFactor;

            #endregion

            // Cylinder Steam Usage = Cylinder Volume * Cutoff * No of Cylinder Strokes (based on loco speed, ie, distance travelled in period / Circumference of Drive Wheels)
            // SweptVolumeToTravelRatioFT3pFT is used to calculate the Cylinder Steam Usage Rate (see below)
            // SweptVolumeToTravelRatioFT3pFT = strokes_per_cycle * no_of_cylinders * pi*CylRad^2 * stroke_length / 2*pi*WheelRad
            // "pi"s cancel out

            // Cylinder piston shaft volume needs to be calculated and deducted from sweptvolume - assume diameter of the cylinder minus one-half of the piston-rod area. Let us assume that the latter is 3 square inches
            CylinderPistonShaftFt3 = Me2.ToFt2(Me2.FromIn2(((float)Math.PI * (CylinderPistonShaftDiaIn / 2.0f) * (CylinderPistonShaftDiaIn / 2.0f)) / 2.0f));
            CylinderPistonAreaFt2 = Me2.ToFt2(MathHelper.Pi * CylinderDiameterM * CylinderDiameterM / 4.0f);
            CylinderSweptVolumeFT3pFT = ((CylinderPistonAreaFt2 * Me.ToFt(CylinderStrokeM)) - CylinderPistonShaftFt3);

            // Cylinder Steam Usage	= SweptVolumeToTravelRatioFT3pFT x cutoff x {(speed x (SteamDensity (CylPress) - SteamDensity (CylBackPress)) 
            // lbs/s                = ft3/ft                                  x   ft/s  x  lbs/ft3

            // This is to model falling boiler efficiency as the combustion increases, based on a "crude" model, to be REDONE?
  //          if (BoilerEfficiency == null)
   //         {
   //             BoilerEfficiency = new Interpolator(4);
   //             BoilerEfficiency[0] = .82f;
   //             BoilerEfficiency[(1 - .82f) / .35f] = .82f;
   //             BoilerEfficiency[(1 - .4f) / .35f] = .4f;
   //             BoilerEfficiency[1 / .35f] = .4f;
   //         }

            // Based on the EvapArea, this section calculates the maximum boiler output in lbs/s, and also calculates the theoretical burn rate in kg/s to support it.
            // BurnRate creates a table with an x-axis of steam production in lb/s, and a y-axis calculating the coal burnt to support this production rate in lb/s.
   //         BurnRateLBpStoKGpS = new Interpolator(27);
   //         for (int i = 0; i < 27; i++)
   //         {
   //             float x = .1f * i;
   //             float y = x;
   //             if (y < .02)
   //                 y = .02f;
   //             else if (y > 2.5f)
    //                y = 2.5f;
    //            BurnRateLBpStoKGpS[x] = y / BoilerEfficiency[x]; // To increase burnrate to compensate for a loss of energy, ie this is amount of coal that would need to compensate for the boiler efficiency
    //        }
    //        float sy = (MaxFlueTempK - baseTempK) * W.ToKW(BoilerHeatTransferCoeffWpM2K) * EvaporationAreaM2 / HeatMaterialThicknessFactor; // Boiler kWs
    //        float sx = sy / (W.ToKW(W.FromBTUpS(SteamHeatPSItoBTUpLB[MaxBoilerPressurePSI])));  // BoilerkW / (SteamHeat- in kJ)?
    //        BurnRateLBpStoKGpS.ScaleX(sx);  // Steam in lbs
   //         BurnRateLBpStoKGpS.ScaleY(sy / FuelCalorificKJpKG); // Original Formula - FuelBurnt KG = BoilerkW / FuelCalorific - Convert to equivalent kgs of coal
     //       BoilerEfficiency.ScaleX(sx); // Boiler Efficiency x axis - Steam in lbs
     //       MaxBoilerOutputLBpH = Kg.ToLb(pS.TopH(sx));
    //        BurnRateLBpStoKGpS.ScaleY(BurnRateMultiplier);

            MaxBoilerOutputLBpH = pS.TopH(TheoreticalMaxSteamOutputLBpS);

            // Temp to see input 

        //    Trace.TraceInformation("Max Boiler {0} Theoretical {1}", MaxBoilerOutputLBpH, TheoreticalMaxSteamOutputLBpS);

          //  Trace.TraceInformation(" full  - BurnRate {0} pH {1} ps @ SteamRate {2} Max Out {3}", NewBurnRateSteamToCoalLbspH[pS.TopH(TheoreticalMaxSteamOutputLBpS)], pS.FrompH(NewBurnRateSteamToCoalLbspH[pS.TopH(TheoreticalMaxSteamOutputLBpS)]), TheoreticalMaxSteamOutputLBpS, pS.TopH(TheoreticalMaxSteamOutputLBpS));
     //       Trace.TraceInformation("0.5 - BurnRate {0} ps {1} pH @ SteamRate {2} Max Out {3}", NewBurnRateSteamToCoalLbspH[TheoreticalMaxSteamOutputLBpS / 2.0f], pS.TopH(NewBurnRateSteamToCoalLbspH[TheoreticalMaxSteamOutputLBpS / 2.0f]), TheoreticalMaxSteamOutputLBpS / 2.0f, pS.TopH(TheoreticalMaxSteamOutputLBpS / 2.0f));
     //       Trace.TraceInformation("0.25 - BurnRate {0} ps {1} pH @ SteamRate {2} Max Out {3}", NewBurnRateSteamToCoalLbspH[TheoreticalMaxSteamOutputLBpS / 4.0f], pS.TopH(NewBurnRateSteamToCoalLbspH[TheoreticalMaxSteamOutputLBpS / 4.0f]), TheoreticalMaxSteamOutputLBpS / 4.0f, pS.TopH(TheoreticalMaxSteamOutputLBpS / 4.0f));
           
                // Calculate the maximum boiler heat input based on the steam generation rate
        //        MaxBoilerHeatInBTUpS = W.ToBTUpS(W.FromKW(BurnRateLBpStoKGpS[TheoreticalMaxSteamOutputLBpS] * FuelCalorificKJpKG * BoilerEfficiency[TheoreticalMaxSteamOutputLBpS]));


            float MaxCombustionRateKgpS = pS.FrompH(Kg.FromLb(NewBurnRateSteamToCoalLbspH[pS.TopH(TheoreticalMaxSteamOutputLBpS)]));

       //     Trace.TraceInformation("Burn {0}  Cal {1}", MaxCombustionRateKgpS, FuelCalorificKJpKG, );

            // Calculate the maximum boiler heat input based on the steam generation rate
         //   MaxBoilerHeatInBTUpS = W.ToBTUpS(W.FromKW(BurnRateLBpStoKGpS[TheoreticalMaxSteamOutputLBpS] * FuelCalorificKJpKG * BoilerEfficiency[TheoreticalMaxSteamOutputLBpS]));
           
            MaxBoilerHeatInBTUpS = W.ToBTUpS(W.FromKW(MaxCombustionRateKgpS * FuelCalorificKJpKG * BoilerEfficiencyGrateAreaLBpFT2toX[pS.TopH(Kg.ToLb(MaxCombustionRateKgpS)) / GrateAreaM2]));


            #region Initialise Locomotive in a Hot or Cold Start Condition

            if (HotStart)
            {
                // Hot Start - set so that FlueTemp is at maximum, boilerpressure slightly below max
                BoilerPressurePSI = MaxBoilerPressurePSI - 5.0f;
                baseStartTempK = C.ToK(C.FromF(PressureToTemperaturePSItoF[BoilerPressurePSI]));
                BoilerStartkW = Kg.FromLb((BoilerPressurePSI / MaxBoilerPressurePSI) * TheoreticalMaxSteamOutputLBpS) * W.ToKW(W.FromBTUpS(SteamHeatPSItoBTUpLB[BoilerPressurePSI])); // Given pressure is slightly less then max, this figure should be slightly less, ie reduce TheoreticalMaxSteamOutputLBpS, for the time being assume a ratio of bp to MaxBP
                FlueTempK = (BoilerStartkW / (W.ToKW(BoilerHeatTransferCoeffWpM2K) * EvaporationAreaM2 * HeatMaterialThicknessFactor)) + baseStartTempK;
                BoilerMassLB = WaterFraction * BoilerVolumeFT3 * WaterDensityPSItoLBpFT3[BoilerPressurePSI] + (1 - WaterFraction) * BoilerVolumeFT3 * SteamDensityPSItoLBpFT3[BoilerPressurePSI];
                BoilerHeatBTU = WaterFraction * BoilerVolumeFT3 * WaterDensityPSItoLBpFT3[BoilerPressurePSI] * WaterHeatPSItoBTUpLB[BoilerPressurePSI] + (1 - WaterFraction) * BoilerVolumeFT3 * SteamDensityPSItoLBpFT3[BoilerPressurePSI] * SteamHeatPSItoBTUpLB[BoilerPressurePSI];
                StartBoilerHeatBTU = BoilerHeatBTU;
            }
            else
            {
                // Cold Start - as per current
                BoilerPressurePSI = MaxBoilerPressurePSI * 0.66f; // Allow for cold start - start at 66% of max boiler pressure - check pressure value given heat in boiler????
                baseStartTempK = C.ToK(C.FromF(PressureToTemperaturePSItoF[BoilerPressurePSI]));
                BoilerStartkW = Kg.FromLb((BoilerPressurePSI / MaxBoilerPressurePSI) * TheoreticalMaxSteamOutputLBpS) * W.ToKW(W.FromBTUpS(SteamHeatPSItoBTUpLB[BoilerPressurePSI]));
                FlueTempK = (BoilerStartkW / (W.ToKW(BoilerHeatTransferCoeffWpM2K) * EvaporationAreaM2 * HeatMaterialThicknessFactor)) + baseStartTempK;
                BoilerMassLB = WaterFraction * BoilerVolumeFT3 * WaterDensityPSItoLBpFT3[BoilerPressurePSI] + (1 - WaterFraction) * BoilerVolumeFT3 * SteamDensityPSItoLBpFT3[BoilerPressurePSI];
                BoilerHeatBTU = WaterFraction * BoilerVolumeFT3 * WaterDensityPSItoLBpFT3[BoilerPressurePSI] * WaterHeatPSItoBTUpLB[BoilerPressurePSI] + (1 - WaterFraction) * BoilerVolumeFT3 * SteamDensityPSItoLBpFT3[BoilerPressurePSI] * SteamHeatPSItoBTUpLB[BoilerPressurePSI];
            }

            DamperFactorManual = TheoreticalMaxSteamOutputLBpS / SpeedEquivMpS; // Calculate a factor for damper control that will vary with speed.
            BlowerSteamUsageFactor = .04f * MaxBoilerOutputLBpH / 3600 / MaxBoilerPressurePSI;
            WaterTempNewK = C.ToK(C.FromF(PressureToTemperaturePSItoF[BoilerPressurePSI])); // Initialise new boiler pressure
            FireMassKG = IdealFireMassKG;
            if (MaxFiringRateKGpS == 0)
                MaxFiringRateKGpS = 180 * MaxBoilerOutputLBpH / 775 / 3600 / 2.2046f;

            if (ORTSMaxFiringRateKGpS != 0)
                MaxFiringRateKGpS = ORTSMaxFiringRateKGpS; // If OR value present then use it 

            // Initialise Mechanical Stoker if present
            if (Stoker == 1.0f)
            {
                StokerIsMechanical = true;
                MaxFiringRateKGpS = 2 * MaxFiringRateKGpS; // Temp allowance for mechanical stoker
            }
            MaxTheoreticalFiringRateKgpS = MaxFiringRateKGpS * 1.33f; // allow the fireman to overfuel for short periods of time 
            #endregion

            ApplyBoilerPressure();

            AuxPowerOn = true;
        }

        /// <summary>
        /// Sets controler settings from other engine for cab switch
        /// </summary>
        /// <param name="other"></param>
        public override void CopyControllerSettings(TrainCar other)
        {
            base.CopyControllerSettings(other);
            if (CutoffController != null)
                CutoffController.SetValue(Train.MUReverserPercent / 100);
        }

                //================================================================================================//
        /// <summary>
        /// Set starting conditions  when initial speed > 0 
        /// 

        public override void InitializeMoving()
        {
            base.InitializeMoving();
            WheelSpeedMpS = SpeedMpS;
            DynamicBrakePercent = -1;
            CutoffController.SetValue(Train.MUReverserPercent / 100);
            ThrottleController.SetValue(Train.MUThrottlePercent / 100);
            HotStart = true;
        }
 
        // +++++++++++++++++++++ Main Simulation - Start ++++++++++++++++++++++++++++++++
        /// <summary>
        /// This is a periodic update to calculate physics 
        /// parameters and update the base class's MotiveForceN 
        /// and FrictionForceN values based on throttle settings
        /// etc for the locomotive.
        /// </summary>
        public override void Update(float elapsedClockSeconds)
        {
            PowerOn = true;
            UpdateControllers(elapsedClockSeconds);
            base.Update(elapsedClockSeconds);
            UpdateFX(elapsedClockSeconds);

#if INDIVIDUAL_CONTROL
			//this train is remote controlled, with mine as a helper, so I need to send the controlling information, but not the force.
			if (MultiPlayer.MPManager.IsMultiPlayer() && this.Train.TrainType == Train.TRAINTYPE.REMOTE && this == Program.Simulator.PlayerLocomotive)
			{
				if (CutoffController.UpdateValue != 0.0 || BlowerController.UpdateValue != 0.0 || DamperController.UpdateValue != 0.0 || FiringRateController.UpdateValue != 0.0 || Injector1Controller.UpdateValue != 0.0 || Injector2Controller.UpdateValue != 0.0)
				{
					controlUpdated = true;
				}
				Train.MUReverserPercent = CutoffController.Update(elapsedClockSeconds) * 100.0f;
				if (Train.MUReverserPercent >= 0)
					Train.MUDirection = Direction.Forward;
				else
					Train.MUDirection = Direction.Reverse;
				return; //done, will go back and send the message to the remote train controller
			}

			if (MultiPlayer.MPManager.IsMultiPlayer() && this.notificationReceived == true)
			{
				Train.MUReverserPercent = CutoffController.CurrentValue * 100.0f;
				if (Train.MUReverserPercent >= 0)
					Train.MUDirection = Direction.Forward;
				else
					Train.MUDirection = Direction.Reverse;
			}
#endif 
            throttle = ThrottlePercent / 100;
            cutoff = Math.Abs(Train.MUReverserPercent / 100);
            if (cutoff > CutoffController.MaximumValue) // Maximum value set in cutoff controller in ENG file
                cutoff = CutoffController.MaximumValue;   // limit to maximum value set in ENG file for locomotive
            float absSpeedMpS = Math.Abs(Train.SpeedMpS);
            if (absSpeedMpS > 2 && (Train.MUReverserPercent == 100 || Train.MUReverserPercent == -100))
            {   // AI cutoff adjustment logic, also used for steam MU'd with non-steam
                cutoff = throttle * CutoffController.MaximumValue * 2 / absSpeedMpS;
                float min = 0.2f;  // Figure originally set with ForceFactor2 table - not sure the significance at this time.
                if (cutoff < min)
                {
                    throttle = cutoff / min;
                    cutoff = min;
                }
                else
                    throttle = 1;
            }
            #region transfer energy
            UpdateTender(elapsedClockSeconds);
            UpdateFirebox(elapsedClockSeconds, absSpeedMpS);
            UpdateBoiler(elapsedClockSeconds);
            UpdateCylinders(elapsedClockSeconds, throttle, cutoff, absSpeedMpS);
            UpdateMotion(elapsedClockSeconds, cutoff, absSpeedMpS);
            UpdateMotiveForce(elapsedClockSeconds, 0, 0, 0);
            UpdateAuxiliaries(elapsedClockSeconds, absSpeedMpS);
            #endregion

            #region adjust state
            UpdateWaterGauge();
            UpdateInjectors(elapsedClockSeconds);
            UpdateFiring(absSpeedMpS);
            #endregion
        }

        /// <summary>
        /// Update variables related to audiovisual effects (sound, steam)
        /// </summary>
        private void UpdateFX(float elapsedClockSeconds)
        {
            // Bernoulli equations
            StackSteamVelocityMpS.Update(elapsedClockSeconds, (float)Math.Sqrt(KPa.FromPSI(CylinderExhaustPressureAtmPSI) * 1000 * 2 / WaterDensityAt100DegC1BarKGpM3));
            CylindersSteamVelocityMpS = (float)Math.Sqrt(KPa.FromPSI(CylinderPressureAtmPSI) * 1000 * 2 / WaterDensityAt100DegC1BarKGpM3);

            StackSteamVolumeM3pS = Kg.FromLb(CylinderSteamUsageLBpS + BlowerSteamUsageLBpS + RadiationSteamLossLBpS + CompSteamUsageLBpS + GeneratorSteamUsageLBpS) * SteamVaporSpecVolumeAt100DegC1BarM3pKG;
            CylindersSteamVolumeM3pS = (CylinderCocksAreOpen ? Kg.FromLb(CylCockSteamUsageLBpS) / NumCylinders * SteamVaporSpecVolumeAt100DegC1BarM3pKG : 0);
            SafetyValvesSteamVolumeM3pS = SafetyIsOn ? Kg.FromLb(SafetyValveUsageLBpS) * SteamVaporSpecVolumeAt100DegC1BarM3pKG : 0;

            SmokeColor.Update(elapsedClockSeconds, MathHelper.Clamp((RadiationSteamLossLBpS + BlowerBurnEffect + DamperBurnEffect) / PreviousTotalSteamUsageLBpS - 0.2f, 0.25f, 1));

            // Variable1 is proportional to angular speed, value of 10 means 1 rotation/second.
            Variable1 = (Simulator.UseAdvancedAdhesion && Train.IsPlayerDriven ? LocomotiveAxle.AxleSpeedMpS : SpeedMpS) / DriverWheelRadiusM / MathHelper.Pi * 5;
            Variable2 = MathHelper.Clamp((CylinderPressureAtmPSI - OneAtmospherePSI) / BoilerPressurePSI * 100f, 0, 100);
            Variable3 = FuelRateSmooth * 100;

            const int rotations = 2;
            const int fullLoop = 10 * rotations;
            int numPulses = NumCylinders * 2 * rotations;

            var dPulseTracker = Variable1 / fullLoop * numPulses * elapsedClockSeconds;
            PulseTracker += dPulseTracker;

            if (PulseTracker > (float)NextPulse - dPulseTracker / 2)
            {
                SignalEvent((Event)((int)Event.SteamPulse1 + NextPulse - 1));
                PulseTracker %= numPulses;
                NextPulse %= numPulses;
                NextPulse++;
            }
        }

        private void UpdateControllers(float elapsedClockSeconds)
        {
            if (this.IsLeadLocomotive())
            {
                Train.MUReverserPercent = CutoffController.Update(elapsedClockSeconds) * 100.0f;
                Direction = Train.MUReverserPercent >= 0 ? Direction.Forward : Direction.Reverse;
            }
            else
                CutoffController.Update(elapsedClockSeconds);

            if (CutoffController.UpdateValue != 0.0)
                // On a steam locomotive, the Reverser is the same as the Cut Off Control.
                switch (Direction)
                {
                    case Direction.Reverse: Simulator.Confirmer.ConfirmWithPerCent(CabControl.SteamLocomotiveReverser, Math.Abs(Train.MUReverserPercent), CabSetting.Off); break;
                    case Direction.N: Simulator.Confirmer.Confirm(CabControl.SteamLocomotiveReverser, CabSetting.Neutral); break;
                    case Direction.Forward: Simulator.Confirmer.ConfirmWithPerCent(CabControl.SteamLocomotiveReverser, Math.Abs(Train.MUReverserPercent), CabSetting.On); break;
                }
            if (IsPlayerTrain)
            {
                if (BlowerController.UpdateValue > 0.0)
                    Simulator.Confirmer.UpdateWithPerCent(CabControl.Blower, CabSetting.Increase, BlowerController.CurrentValue * 100);
                if (BlowerController.UpdateValue < 0.0)
                    Simulator.Confirmer.UpdateWithPerCent(CabControl.Blower, CabSetting.Decrease, BlowerController.CurrentValue * 100);
                if (DamperController.UpdateValue > 0.0)
                    Simulator.Confirmer.UpdateWithPerCent(CabControl.Damper, CabSetting.Increase, DamperController.CurrentValue * 100);
                if (DamperController.UpdateValue < 0.0)
                    Simulator.Confirmer.UpdateWithPerCent(CabControl.Damper, CabSetting.Decrease, DamperController.CurrentValue * 100);
                if (FireboxDoorController.UpdateValue > 0.0)
                    Simulator.Confirmer.UpdateWithPerCent(CabControl.FireboxDoor, CabSetting.Increase, FireboxDoorController.CurrentValue * 100);
                if (FireboxDoorController.UpdateValue < 0.0)
                    Simulator.Confirmer.UpdateWithPerCent(CabControl.FireboxDoor, CabSetting.Decrease, FireboxDoorController.CurrentValue * 100);
                if (FiringRateController.UpdateValue > 0.0)
                    Simulator.Confirmer.UpdateWithPerCent(CabControl.FiringRate, CabSetting.Increase, FiringRateController.CurrentValue * 100);
                if (FiringRateController.UpdateValue < 0.0)
                    Simulator.Confirmer.UpdateWithPerCent(CabControl.FiringRate, CabSetting.Decrease, FiringRateController.CurrentValue * 100);
            }

            Injector1Controller.Update(elapsedClockSeconds);
            if (IsPlayerTrain)
            {
                if (Injector1Controller.UpdateValue > 0.0)
                    Simulator.Confirmer.UpdateWithPerCent(CabControl.Injector1, CabSetting.Increase, Injector1Controller.CurrentValue * 100);
                if (Injector1Controller.UpdateValue < 0.0)
                    Simulator.Confirmer.UpdateWithPerCent(CabControl.Injector1, CabSetting.Decrease, Injector1Controller.CurrentValue * 100);
            }
            Injector2Controller.Update(elapsedClockSeconds);
            if (IsPlayerTrain)
            {
                if (Injector2Controller.UpdateValue > 0.0)
                    Simulator.Confirmer.UpdateWithPerCent(CabControl.Injector2, CabSetting.Increase, Injector2Controller.CurrentValue * 100);
                if (Injector2Controller.UpdateValue < 0.0)
                    Simulator.Confirmer.UpdateWithPerCent(CabControl.Injector2, CabSetting.Decrease, Injector2Controller.CurrentValue * 100);
            }

            BlowerController.Update(elapsedClockSeconds);
            if (IsPlayerTrain)
            {
                if (BlowerController.UpdateValue > 0.0)
                    Simulator.Confirmer.UpdateWithPerCent(CabControl.Blower, CabSetting.Increase, BlowerController.CurrentValue * 100);
                if (BlowerController.UpdateValue < 0.0)
                    Simulator.Confirmer.UpdateWithPerCent(CabControl.Blower, CabSetting.Decrease, BlowerController.CurrentValue * 100);
            }

            DamperController.Update(elapsedClockSeconds);
            if (IsPlayerTrain)
            {
                if (DamperController.UpdateValue > 0.0)
                    Simulator.Confirmer.UpdateWithPerCent(CabControl.Damper, CabSetting.Increase, DamperController.CurrentValue * 100);
                if (DamperController.UpdateValue < 0.0)
                    Simulator.Confirmer.UpdateWithPerCent(CabControl.Damper, CabSetting.Decrease, DamperController.CurrentValue * 100);
            }
            FiringRateController.Update(elapsedClockSeconds);
            if (IsPlayerTrain)
            {
                if (FiringRateController.UpdateValue > 0.0)
                    Simulator.Confirmer.UpdateWithPerCent(CabControl.FiringRate, CabSetting.Increase, FiringRateController.CurrentValue * 100);
                if (FiringRateController.UpdateValue < 0.0)
                    Simulator.Confirmer.UpdateWithPerCent(CabControl.FiringRate, CabSetting.Decrease, FiringRateController.CurrentValue * 100);
            }

            var oldFireboxDoorValue = FireboxDoorController.CurrentValue;
            if (IsPlayerTrain)
            {
                FireboxDoorController.Update(elapsedClockSeconds);
                if (FireboxDoorController.UpdateValue > 0.0)
                    Simulator.Confirmer.UpdateWithPerCent(CabControl.FireboxDoor, CabSetting.Increase, FireboxDoorController.CurrentValue * 100);
                if (FireboxDoorController.UpdateValue < 0.0)
                    Simulator.Confirmer.UpdateWithPerCent(CabControl.FireboxDoor, CabSetting.Decrease, FireboxDoorController.CurrentValue * 100);
                if (oldFireboxDoorValue == 0 && FireboxDoorController.CurrentValue > 0)
                    SignalEvent(Event.FireboxDoorOpen);
                else if (oldFireboxDoorValue > 0 && FireboxDoorController.CurrentValue == 0)
                    SignalEvent(Event.FireboxDoorClose);
            }

            FuelController.Update(elapsedClockSeconds);
            if (IsPlayerTrain)
            {
                if (FuelController.UpdateValue > 0.0)
                    Simulator.Confirmer.UpdateWithPerCent(CabControl.TenderCoal, CabSetting.Increase, FuelController.CurrentValue * 100);
            }

            WaterController.Update(elapsedClockSeconds);
            if (IsPlayerTrain)
            {
                if (WaterController.UpdateValue > 0.0)
                    Simulator.Confirmer.UpdateWithPerCent(CabControl.TenderWater, CabSetting.Increase, WaterController.CurrentValue * 100);
            }
        }

        private void UpdateTender(float elapsedClockSeconds)
        {
            TenderCoalMassKG -= elapsedClockSeconds * FuelBurnRateKGpS; // Current Tender coal mass determined by burn rate.
            TenderCoalMassKG = MathHelper.Clamp(TenderCoalMassKG, 0, MaxTenderCoalMassKG); // Clamp value so that it doesn't go out of bounds
            if (TenderCoalMassKG < 1.0)
            {
                if (!CoalIsExhausted)
                {
                    Simulator.Confirmer.Message(ConfirmLevel.Warning, Viewer3D.Viewer.Catalog.GetString("Tender coal supply is empty. Your loco will fail."));
                }
                CoalIsExhausted = true;
            }
            else
            {
                CoalIsExhausted = false;
            }
            TenderWaterVolumeUKG -= InjectorBoilerInputLB / WaterLBpUKG; // Current water volume determined by injector input rate
            TenderWaterVolumeUKG = MathHelper.Clamp(TenderWaterVolumeUKG, 0, (Kg.ToLb(MaxTenderWaterMassKG) / WaterLBpUKG)); // Clamp value so that it doesn't go out of bounds
            if (TenderWaterVolumeUKG < 1.0)
            {
                if (!WaterIsExhausted && IsPlayerTrain)
                {
                    Simulator.Confirmer.Message(ConfirmLevel.Warning, Viewer3D.Viewer.Catalog.GetString("Tender water supply is empty. Your loco will fail."));
                }
                WaterIsExhausted = true;
            }
            else
            {
                WaterIsExhausted = false;
            }
        }

        private void UpdateFirebox(float elapsedClockSeconds, float absSpeedMpS)
        {
            if (!FiringIsManual && !HotStart)  // if loco is started cold, and is not moving then the blower may be needed to heat loco up.
            {
                if (absSpeedMpS < 1.0f)    // locomotive is stationary then blower can heat fire
                {
                    BlowerIsOn = true;  // turn blower on if being used
                    BlowerSteamUsageLBpS = BlowerSteamUsageFactor * BlowerController.CurrentValue * BoilerPressurePSI;
                    BlowerBurnEffect = ManBlowerMultiplier * BlowerSteamUsageFactor * BlowerController.CurrentValue * BoilerPressurePSI;
                }
                else
                {
                    BlowerBurnEffect = 0.0f;
                    BlowerIsOn = false;
                }
            }
#region Combsution (burn) rate for locomotive
            // Adjust burn rates for firing in either manual or AI mode
            if (FiringIsManual)
            {
           //     BurnRateRawKGpS = BurnRateLBpStoKGpS[(RadiationSteamLossLBpS) + BlowerBurnEffect + DamperBurnEffect]; // Manual Firing - note steam usage due to safety valve, compressor and steam cock operation not included, as these are factored into firemans calculations, and will be adjusted for manually - Radiation loss divided by factor of 5.0 to reduce the base level - Manual fireman to compensate as appropriate.
                BurnRateRawKGpS = pS.FrompH(Kg.FromLb(NewBurnRateSteamToCoalLbspH[pS.TopH((RadiationSteamLossLBpS) + BlowerBurnEffect + DamperBurnEffect)])); // Manual Firing - note steam usage due to safety valve, compressor and steam cock operation not included, as these are factored into firemans calculations, and will be adjusted for manually - Radiation loss divided by factor of 5.0 to reduce the base level - Manual fireman to compensate as appropriate.
            }
            else
            {
                if (PreviousTotalSteamUsageLBpS > TheoreticalMaxSteamOutputLBpS)
                {
                    FiringSteamUsageRateLBpS = TheoreticalMaxSteamOutputLBpS; // hold usage rate if steam usage rate exceeds boiler max output
                }
                else
                {
                    FiringSteamUsageRateLBpS = PreviousTotalSteamUsageLBpS;
                }
                // burnrate will be the radiation loss @ rest & then related to heat usage as a factor of the maximum boiler output
            //    BurnRateRawKGpS = BurnRateLBpStoKGpS[(BlowerBurnEffect + HeatRatio * FiringSteamUsageRateLBpS * PressureRatio * BoilerHeatRatio * MaxBoilerHeatRatio)]; // Original

                BurnRateRawKGpS = pS.FrompH(Kg.FromLb(NewBurnRateSteamToCoalLbspH[pS.TopH((BlowerBurnEffect + HeatRatio * FiringSteamUsageRateLBpS * PressureRatio * BoilerHeatRatio * MaxBoilerHeatRatio))]));

                //  Limit burn rate in AI fireman to within acceptable range of Fireman firing rate
                      BurnRateRawKGpS = MathHelper.Clamp(BurnRateRawKGpS, 0.05f, MaxTheoreticalFiringRateKgpS); // Allow burnrate to max out at MaxTheoreticalFiringRateKgpS
            }

            FuelFeedRateKGpS = BurnRateRawKGpS;
            float MinimumFireLevelfactor = 0.05f; // factor representing the how low firemass has got compared to ideal firemass
            if (FireMassKG / IdealFireMassKG < MinimumFireLevelfactor) // If fire level drops too far 
            {
                BurnRateRawKGpS = 0.0f; // If fire is no longer effective set burn rate to zero, change later to allow graduate ramp down
                if (!FireIsExhausted)
                {
                    if (IsPlayerTrain)
                        Simulator.Confirmer.Message(ConfirmLevel.Warning, Viewer3D.Viewer.Catalog.GetString("Fire has dropped too far. Your loco will fail."));
                    FireIsExhausted = true; // fire has run out of fuel.
                }
            }
            // test for fusible plug
            if (FusiblePlugIsBlown)
            {
            BurnRateRawKGpS = 0.0f; // Drop fire due to melting of fusible plug and steam quenching fire, change later to allow graduate ramp down.
            }
            
            FireRatio = FireMassKG / IdealFireMassKG;
            if (absSpeedMpS == 0)
                BurnRateRawKGpS *= FireRatio * 0.2f; // reduce background burnrate if stationary
            else if (FireRatio < 1.0f)  // maximise burnrate when FireMass = IdealFireMass, else allow a reduction in efficiency
                BurnRateRawKGpS *= FireRatio;
            else
                BurnRateRawKGpS *= 2 - FireRatio;
            // <CJComment> Incorrect version commented out. Needs fixing. </CJComment>
            BurnRateSmoothKGpS.Update(elapsedClockSeconds, BurnRateRawKGpS);
           // BurnRateSmoothKGpS.Update(0.1f, BurnRateRawKGpS); // Smooth the burn rate
            FuelBurnRateKGpS = BurnRateSmoothKGpS.SmoothedValue;
            FuelBurnRateKGpS = MathHelper.Clamp(FuelBurnRateKGpS, 0, MaxFireMassKG); // clamp burnrate to maintain it within limits
#endregion


#region Firing (feeding fuel) Rate of locomotive

            if (FiringIsManual)
            {
                FuelRateSmooth = CoalIsExhausted ? 0 : FiringRateController.CurrentValue;
                FuelFeedRateKGpS = MaxFiringRateKGpS * FuelRateSmooth;
            }
            else if (elapsedClockSeconds > 0.001 && MaxFiringRateKGpS > 0.001)
            {
                // Automatic fireman, ish.
                DesiredChange = MathHelper.Clamp(((IdealFireMassKG - FireMassKG) + FuelBurnRateKGpS) / MaxFiringRateKGpS, 0.001f, 1);
                if (StokerIsMechanical) // if a stoker is fitted expect a quicker response to fuel feeding
                {
                    FuelRateStoker.Update(elapsedClockSeconds, DesiredChange); // faster fuel feed rate for stoker    
                    FuelRateSmooth = CoalIsExhausted ? 0 : FuelRateStoker.SmoothedValue; // If tender coal is empty stop fuelrate (feeding coal onto fire). 
                }
                else
                {
            //        float ShovelFireLevelFactor = 0.9f; // Fireman will only shovel if fire drops below 90% of ideal firemass
            //        if((FireMassKG / IdealFireMassKG ) < ShovelFireLevelFactor && FireMassKG < IdealFireMassKG)
           //         {
                    FuelRate.Update(elapsedClockSeconds, DesiredChange); // slower fuel feed rate for fireman
                    FuelRateSmooth = CoalIsExhausted ? 0 : FuelRate.SmoothedValue; // If tender coal is empty stop fuelrate (feeding coal onto fire).
           //         }
            //        else 
            //        {
             //       FuelRate.Update(elapsedClockSeconds, 0.01f); // set fuel feed rate for fireman to low value to stop firing until fire level drops low enough
           //         FuelRateSmooth = CoalIsExhausted ? 0 : FuelRate.SmoothedValue; // If tender coal is empty stop fuelrate (feeding coal onto fire).
           //         }
                }
                 
                float CurrentFireLevelfactor = 0.95f; // factor representing the how low firemass has got compared to ideal firemass  
                if ((FireMassKG / IdealFireMassKG ) < CurrentFireLevelfactor) // if firemass is falling too low shovel harder - set @ 85% - needs further refinement as this shouldn't be able to be maintained indefinitely
                {
                    if (FuelBoostOnTimerS < TimeFuelBoostOnS) // If fuel boost timer still has time available allow fuel boost
                    {
                        FuelBoostResetTimerS = 0.01f;     // Reset fuel reset (time out) timer to allow stop boosting for a period of time.
                        if (!FuelBoost)
                        {
                            FuelBoost = true; // boost shoveling 
                            if (!StokerIsMechanical && IsPlayerTrain)  // Don't display message if stoker in operation
                            {
                                Simulator.Confirmer.Message(ConfirmLevel.Warning, Viewer3D.Viewer.Catalog.GetString("FireMass is getting low. Your fireman will shovel faster, but don't wear him out."));
                            }
                        }
                    }
                }
                else if (FireMassKG >= IdealFireMassKG) // If firemass has returned to normal - turn boost off
                {
                    if (FuelBoost)
                    {
                        FuelBoost = false; // disable boost shoveling 
                      //  FuelBoostReset = false; // Reset boost timer
                        if (!StokerIsMechanical && IsPlayerTrain)  // Don't display message if stoker in operation
                        {
                            Simulator.Confirmer.Message(ConfirmLevel.Warning, Viewer3D.Viewer.Catalog.GetString("FireMass is back within limits. Your fireman will shovel as per normal."));
                        }
                    }
                }
                if (FuelBoost && !FuelBoostReset) // if fuel boost is still on, and hasn't been reset - needs further refinement as this shouldn't be able to be maintained indefinitely
                {
                    DisplayMaxFiringRateKGpS = MaxTheoreticalFiringRateKgpS; // Set display value with temporary higher shovelling level
                    FuelFeedRateKGpS = MaxTheoreticalFiringRateKgpS * FuelRateSmooth;  // At times of heavy burning allow AI fireman to overfuel
                    FuelBoostOnTimerS += elapsedClockSeconds; // Time how long to fuel boost for
                }
                else
                {
                    DisplayMaxFiringRateKGpS = MaxFiringRateKGpS; // Rest display max firing rate to new figure
                    FuelFeedRateKGpS = MaxFiringRateKGpS * FuelRateSmooth;
                }
            }
            // Calculate update to firemass as a result of adding fuel to the fire
            FireMassKG += elapsedClockSeconds * (FuelFeedRateKGpS - FuelBurnRateKGpS);
            FireMassKG = MathHelper.Clamp(FireMassKG, 0, MaxFireMassKG);
            GrateCombustionRateLBpFt2 = Kg.ToLb(FuelBurnRateKGpS) / Me2.ToFt2(GrateAreaM2); //coal burnt per sq ft grate area
            // Time Fuel Boost reset timer if all time has been used up on boost timer
            if (FuelBoostOnTimerS >= TimeFuelBoostOnS)
            {
                FuelBoostResetTimerS += elapsedClockSeconds; // Time how long to wait for next fuel boost
                FuelBoostReset = true;
            }
            if (FuelBoostResetTimerS > TimeFuelBoostResetS)
            {
                FuelBoostOnTimerS = 0.01f;     // Reset fuel boost timer to allow another boost if required.
                FuelBoostReset = false;
            }
#endregion            
        }

        private void UpdateBoiler(float elapsedClockSeconds)
        {
            absSpeedMpS = Math.Abs(Train.SpeedMpS);
            
            // Determine number and size of safety valves
            // Reference: Ashton's POP Safety valves catalogue
            // To calculate size use - Total diam of safety valve = 0.036 x ( H / (L x P), where H = heat area of boiler sq ft (not including superheater), L = valve lift (assume 0.1 in for Ashton valves), P = Abs pressure psi (gauge pressure + atmospheric)
            
            const float ValveSizeCalculationFactor = 0.036f;
            const float ValveLiftIn = 0.1f;
            float ValveSizeTotalDiaIn = ValveSizeCalculationFactor * ( Me2.ToFt2(EvaporationAreaM2) / (ValveLiftIn * (MaxBoilerPressurePSI + OneAtmospherePSI)));
              
            ValveSizeTotalDiaIn += 1.0f; // Add safety margin to align with Ashton size selection table
            
            // There will always be at least two safety valves to allow for a possible failure. There may be up to four fitted to a locomotive depending upon the size of the heating area. Therefore allow for combinations of 2x, 3x or 4x.
            // Common valve sizes are 2.5", 3", 3.5" and 4".
            
            // Test for 2x combinations
            float TestValveSizeTotalDiaIn = ValveSizeTotalDiaIn / 2.0f;
            if (TestValveSizeTotalDiaIn <= 4.0f)
            {
            NumSafetyValves = 2.0f;   // Assume that there are 2 safety valves
            if ( TestValveSizeTotalDiaIn <= 2.5)
            {
            SafetyValveSizeIn = 2.5f; // Safety valve is a 2.5" diameter unit
            }
            if ( TestValveSizeTotalDiaIn > 2.5 && TestValveSizeTotalDiaIn <= 3.0)
            {
            SafetyValveSizeIn = 3.0f; // Safety valve is a 3.0" diameter unit
            }
            if ( TestValveSizeTotalDiaIn > 3.0 && TestValveSizeTotalDiaIn <= 3.5)
            {
            SafetyValveSizeIn = 3.5f; // Safety valve is a 3.5" diameter unit
            }
            if ( TestValveSizeTotalDiaIn > 3.5 && TestValveSizeTotalDiaIn <= 4.0)
            {
            SafetyValveSizeIn = 4.0f; // Safety valve is a 4.0" diameter unit
            }
            }
            else
            {
            TestValveSizeTotalDiaIn = ValveSizeTotalDiaIn / 3.0f;
            // Test for 3x combinations
            if (TestValveSizeTotalDiaIn <= 4.0f)
            {
            NumSafetyValves = 3.0f;   // Assume that there are 3 safety valves
            if ( TestValveSizeTotalDiaIn <= 2.5)
            {
            SafetyValveSizeIn = 2.5f; // Safety valve is a 2.5" diameter unit
            }
            if ( TestValveSizeTotalDiaIn > 2.5 && TestValveSizeTotalDiaIn <= 3.0)
            {
            SafetyValveSizeIn = 3.0f; // Safety valve is a 3.0" diameter unit
            }
            if ( TestValveSizeTotalDiaIn > 3.0 && TestValveSizeTotalDiaIn <= 3.5)
            {
            SafetyValveSizeIn = 3.5f; // Safety valve is a 3.5" diameter unit
            }
            if ( TestValveSizeTotalDiaIn > 3.5 && TestValveSizeTotalDiaIn <= 4.0)
            {
            SafetyValveSizeIn = 4.0f; // Safety valve is a 4.0" diameter unit
            }
            }
            else
            {
            TestValveSizeTotalDiaIn = ValveSizeTotalDiaIn / 4.0f;
            // Test for 4x combinations
            if (TestValveSizeTotalDiaIn <= 4.0f)
            {
            NumSafetyValves = 4.0f;   // Assume that there are 4 safety valves
            if ( TestValveSizeTotalDiaIn <= 2.5)
            {
            SafetyValveSizeIn = 2.5f; // Safety valve is a 2.5" diameter unit
            }
            if ( TestValveSizeTotalDiaIn > 2.5 && TestValveSizeTotalDiaIn <= 3.0)
            {
            SafetyValveSizeIn = 3.0f; // Safety valve is a 3.0" diameter unit
            }
            if ( TestValveSizeTotalDiaIn > 3.0 && TestValveSizeTotalDiaIn <= 3.5)
            {
            SafetyValveSizeIn = 3.5f; // Safety valve is a 3.5" diameter unit
            }
            if ( TestValveSizeTotalDiaIn > 3.5 && TestValveSizeTotalDiaIn <= 4.0)
            {
            SafetyValveSizeIn = 4.0f; // Safety valve is a 4.0" diameter unit
            }
            }
            else
            {
            // Else set at maximum default value
            NumSafetyValves = 4.0f;   // Assume that there are 4 safety valves
            SafetyValveSizeIn = 4.0f; // Safety valve is a 4.0" diameter unit
            }
            }
            }
            
            // Steam Discharge Rates
            // Use Napier formula to calculate steam discharge rate through safety valve, ie Discharge (lb/s) = (Valve area * Abs Pressure) / 70
            // Set "valve area" of safety valve, based on reverse enginnered values of steam, valve area is determined by lift and the gap created 
            const float SafetyValveDischargeFactor = 70.0f;
            if (SafetyValveSizeIn == 2.5f)
            {
                SafetyValveSizeDiaIn2 = 0.610369021f;
            }
            else
            {
                if (SafetyValveSizeIn == 3.0f)
                {
                    SafetyValveSizeDiaIn2 = 0.799264656f;
                }
                else
                {
                    if (SafetyValveSizeIn == 3.5f)
                    {
                        SafetyValveSizeDiaIn2 = 0.932672199f;
                    }
                    else
                    {
                        if (SafetyValveSizeIn == 4.0f)
                        {
                            SafetyValveSizeDiaIn2 = 0.977534912f;
                        }
                    }
                }
            }
            
            // For display purposes calculate the maximum steam discharge with all safety valves open
            MaxSafetyValveDischargeLbspS = NumSafetyValves * (SafetyValveSizeDiaIn2 * (MaxBoilerPressurePSI + OneAtmospherePSI)) / SafetyValveDischargeFactor;
            // Set open pressure and close pressures for safety valves.
            float SafetyValveOpen1Psi = MaxBoilerPressurePSI;
            float SafetyValveClose1Psi = MaxBoilerPressurePSI - 4.0f;
            float SafetyValveOpen2Psi = MaxBoilerPressurePSI + 2.0f;
            float SafetyValveClose2Psi = MaxBoilerPressurePSI - 3.0f;
            float SafetyValveOpen3Psi = MaxBoilerPressurePSI + 4.0f;
            float SafetyValveClose3Psi = MaxBoilerPressurePSI - 2.0f;
            float SafetyValveOpen4Psi = MaxBoilerPressurePSI + 6.0f;
            float SafetyValveClose4Psi = MaxBoilerPressurePSI - 1.0f;
            
            // Safety Valve
            if (BoilerPressurePSI > MaxBoilerPressurePSI + SafetyValveStartPSI)
            {
                if (!SafetyIsOn)
                {
                    SignalEvent(Event.SteamSafetyValveOn);
                    SafetyIsOn = true;
                }
            }
            else if (BoilerPressurePSI < MaxBoilerPressurePSI - SafetyValveDropPSI)
            {
                if (SafetyIsOn)
                {
                    SignalEvent(Event.SteamSafetyValveOff);
                    SafetyIsOn = false;
                    SafetyValveUsage1LBpS = 0.0f; // if safety valve closed, then zero discharge rate
                }
            }
            if (SafetyIsOn)
            {
               // Determine how many safety valves are in operation and set Safety Valve discharge rate
                SafetyValveUsageLBpS = 0.0f;  // Set to zero initially
                
                // Calculate rate for safety valve 1
                SafetyValveUsage1LBpS = (SafetyValveSizeDiaIn2 * (BoilerPressurePSI + OneAtmospherePSI)) / SafetyValveDischargeFactor; // If safety valve is above open value then set rate
                               
                // Calculate rate for safety valve 2
                if (BoilerPressurePSI > SafetyValveOpen2Psi)
                {
                safety2IsOn = true; // turn safey 2 on
                SafetyValveUsage2LBpS = (SafetyValveSizeDiaIn2 * (BoilerPressurePSI + OneAtmospherePSI)) / SafetyValveDischargeFactor; // If safety valve is above open value then set rate
                }
                else
                {
                if (BoilerPressurePSI < SafetyValveClose1Psi)
                {
                safety2IsOn = false; // turn safey 2 off
                SafetyValveUsage2LBpS = 0.0f; // if safety valve closed, then zero discharge rate
                }
                else
                {
                if (safety2IsOn)
                {
                SafetyValveUsage2LBpS = (SafetyValveSizeDiaIn2 * (BoilerPressurePSI + OneAtmospherePSI)) / SafetyValveDischargeFactor; // If safety valve is between open and close values, set rate
                }
                else
                {
                SafetyValveUsage2LBpS = 0.0f; // if safety valve closed, then zero discharge rate
                }
                }
                }
                
                
                // Calculate rate for safety valve 3
                if (BoilerPressurePSI > SafetyValveOpen3Psi)
                {
                safety3IsOn = true; // turn safey 3 on
                SafetyValveUsage3LBpS = (SafetyValveSizeDiaIn2 * (BoilerPressurePSI + OneAtmospherePSI)) / SafetyValveDischargeFactor; // If safety valve is above open value then set rate
                }
                else
                {
                if (BoilerPressurePSI < SafetyValveClose3Psi)
                {
                safety3IsOn = false; // turn safey 3 off
                SafetyValveUsage3LBpS = 0.0f; // if safety valve closed, then zero discharge rate
                }
                else
                {
                if (safety3IsOn)
                {
                SafetyValveUsage3LBpS = (SafetyValveSizeDiaIn2 * (BoilerPressurePSI + OneAtmospherePSI)) / SafetyValveDischargeFactor; // If safety valve is between open and close values, set rate
                }
                else
                {
                SafetyValveUsage3LBpS = 0.0f; // if safety valve closed, then zero discharge rate
                }
                }
                }
                
                
                // Calculate rate for safety valve 4
                if (BoilerPressurePSI > SafetyValveOpen4Psi)
                {
                safety4IsOn = true; // turn safey 4 on
                SafetyValveUsage4LBpS = (SafetyValveSizeDiaIn2 * (BoilerPressurePSI + OneAtmospherePSI)) / SafetyValveDischargeFactor; // If safety valve is above open value then set rate
                }
                else
                {
                if (BoilerPressurePSI < SafetyValveClose4Psi)
                {
                safety4IsOn = false; // turn safey 4 off
                SafetyValveUsage4LBpS = 0.0f; // if safety valve closed, then zero discharge rate
                }
                else
                {
                if (safety4IsOn)
                {
                SafetyValveUsage4LBpS = (SafetyValveSizeDiaIn2 * (BoilerPressurePSI + OneAtmospherePSI)) / SafetyValveDischargeFactor; // If safety valve is between open and close values, set rate
                }
                else
                {
                SafetyValveUsage4LBpS = 0.0f; // if safety valve closed, then zero discharge rate
                }
                }
                }
                
                
                SafetyValveUsageLBpS = SafetyValveUsage1LBpS + SafetyValveUsage2LBpS + SafetyValveUsage3LBpS + SafetyValveUsage4LBpS;   // Sum all the safety valve discharge rates together
                BoilerMassLB -= elapsedClockSeconds * SafetyValveUsageLBpS;
                BoilerHeatBTU -= elapsedClockSeconds * SafetyValveUsageLBpS * (BoilerSteamHeatBTUpLB - BoilerWaterHeatBTUpLB); // Heat loss due to safety valve
                TotalSteamUsageLBpS += SafetyValveUsageLBpS;
                BoilerHeatOutBTUpS += SafetyValveUsageLBpS * (BoilerSteamHeatBTUpLB - BoilerWaterHeatBTUpLB); // Heat loss due to safety valve
            }
            else
            {
                SafetyValveUsageLBpS = 0.0f;
            }

            // Adjust blower impacts on heat and boiler mass
            if (BlowerIsOn)
            {
                BoilerMassLB -= elapsedClockSeconds * BlowerSteamUsageLBpS; // Reduce boiler mass to reflect steam usage by blower  
                BoilerHeatBTU -= elapsedClockSeconds * BlowerSteamUsageLBpS * (BoilerSteamHeatBTUpLB - BoilerWaterHeatBTUpLB);  // Reduce boiler Heat to reflect steam usage by blower
                BoilerHeatOutBTUpS += BlowerSteamUsageLBpS * (BoilerSteamHeatBTUpLB - BoilerWaterHeatBTUpLB);  // Reduce boiler Heat to reflect steam usage by blower
                TotalSteamUsageLBpS += BlowerSteamUsageLBpS;
            }
            BoilerWaterTempK = C.ToK(C.FromF(PressureToTemperaturePSItoF[BoilerPressurePSI]));

            if (FlueTempK < BoilerWaterTempK)
            {
                FlueTempK = BoilerWaterTempK + 10.0f;  // Ensure that flue temp is greater then water temp, so that you don't get negative steam production
            }
            // Heat transferred per unit time (W or J/s) = (Heat Txf Coeff (W/m2K) * Heat Txf Area (m2) * Temp Difference (K)) / Material Thickness - in this instance the material thickness is a means of increasing the boiler output - convection heat formula.
            // Heat transfer Coefficient for Locomotive Boiler = 45.0 Wm^2K            
            BoilerKW = (FlueTempK - BoilerWaterTempK) * W.ToKW(BoilerHeatTransferCoeffWpM2K) * EvaporationAreaM2 / HeatMaterialThicknessFactor;
            if (GrateCombustionRateLBpFt2 > GrateLimitLBpS)
            {
                FireHeatTxfKW = PreviousFireHeatTxfKW; // if greater then grate limit don't allow any more heat txf
            }
            else
            {
             //   FireHeatTxfKW = FuelBurnRateKGpS * FuelCalorificKJpKG * BoilerEfficiencyGrateAreaLBpFT2toX[Kg.ToLb(FuelBurnRateKGpS)] / (SpecificHeatCoalKJpKGpK * FireMassKG); // Current heat txf based on fire burning rate  
                FireHeatTxfKW = FuelBurnRateKGpS * FuelCalorificKJpKG * BoilerEfficiencyGrateAreaLBpFT2toX[(pS.TopH(Kg.ToLb(FuelBurnRateKGpS)) / Me2.ToFt2(GrateAreaM2))] / (SpecificHeatCoalKJpKGpK * FireMassKG); // Current heat txf based on fire burning rate 
            }

            PreviousFireHeatTxfKW = FireHeatTxfKW; // store the last value of FireHeatTxfKW
            
            FlueTempDiffK = ((BoilerHeatInBTUpS - BoilerHeatOutBTUpS) * BTUpHtoKJpS) / (W.ToKW(BoilerHeatTransferCoeffWpM2K) * EvaporationAreaM2); // calculate current FlueTempK difference, based upon heat input due to firing - heat taken out by boiler

            FlueTempK += elapsedClockSeconds * FlueTempDiffK; // Calculate increase or decrease in Flue Temp

            FlueTempK = MathHelper.Clamp(FlueTempK, 0, 3000.0f);    // Maximum firebox temp in Penn document = 1514 K.

            if (FusiblePlugIsBlown)
            {
                EvaporationLBpS = 0.0333f;   // if fusible plug is blown drop steam output of boiler.
            }
            else
            {
                // Steam Output (kg/h) = ( Boiler Rating (kW) * 3600 s/h ) / Energy added kJ/kg, Energy added = energy (at Boiler Pressure - Feedwater energy)
                // Allow a small increase if superheater is installed
                EvaporationLBpS = Kg.ToLb(BoilerKW / W.ToKW(W.FromBTUpS(BoilerSteamHeatBTUpLB)));  // convert kW,  1kW = 0.94781712 BTU/s - fudge factor required - 1.1
            }

            // Cap Steam Generation rate if excessive
            //EvaporationLBpS = MathHelper.Clamp(EvaporationLBpS, 0, TheoreticalMaxSteamOutputLBpS); // If steam generation is too high, then cap at max theoretical rule of thumb

            PreviousBoilerHeatInBTU = BoilerHeatBTU;

            if (!FiringIsManual)
            {

                if (BoilerHeatBTU > MaxBoilerHeatBTU) // Limit boiler heat to max value for the boiler
                {
                    BoilerHeat = true;
                    const float BoilerHeatFactor = 1.025f; // Increasing this factor will change the burn rate once boiler heat has been reached
                    float FactorPower = BoilerHeatBTU / (MaxBoilerHeatBTU / BoilerHeatFactor); 
                    BoilerHeatRatio = MaxBoilerHeatBTU / (float)Math.Pow(BoilerHeatBTU, FactorPower);
                }
                else
                {
                    BoilerHeat = false;
                    BoilerHeatRatio = 1.0f;
                }
                BoilerHeatRatio = MathHelper.Clamp(BoilerHeatRatio, 0.0f, 1.0f); // Keep Boiler Heat ratio within bounds
                if (BoilerHeatBTU > MaxBoilerPressHeatBTU)
                {
                    float FactorPower = BoilerHeatBTU / (MaxBoilerPressHeatBTU - MaxBoilerHeatBTU);
                    MaxBoilerHeatRatio = MaxBoilerHeatBTU / (float)Math.Pow(BoilerHeatBTU, FactorPower); 
                }
                else
                {
                    MaxBoilerHeatRatio = 1.0f;
                }
                MaxBoilerHeatRatio = MathHelper.Clamp(MaxBoilerHeatRatio, 0.0f, 1.0f); // Keep Max Boiler Heat ratio within bounds
            }

          //  BoilerHeatInBTUpS = W.ToBTUpS(W.FromKW(FuelCalorificKJpKG * FuelBurnRateKGpS)) * BoilerEfficiencyGrateAreaLBpFT2toX[Kg.ToLb(FuelBurnRateKGpS)];
          //  BoilerHeatBTU += elapsedClockSeconds * W.ToBTUpS(W.FromKW(FuelCalorificKJpKG * FuelBurnRateKGpS)) * BoilerEfficiencyGrateAreaLBpFT2toX[Kg.ToLb(FuelBurnRateKGpS)];
            BoilerHeatInBTUpS = W.ToBTUpS(W.FromKW(FuelCalorificKJpKG * FuelBurnRateKGpS)) * BoilerEfficiencyGrateAreaLBpFT2toX[(pS.TopH(Kg.ToLb(FuelBurnRateKGpS)) / Me2.ToFt2(GrateAreaM2))];
            BoilerHeatBTU += elapsedClockSeconds * W.ToBTUpS(W.FromKW(FuelCalorificKJpKG * FuelBurnRateKGpS)) * BoilerEfficiencyGrateAreaLBpFT2toX[(pS.TopH(Kg.ToLb(FuelBurnRateKGpS)) / Me2.ToFt2(GrateAreaM2))];

            // Basic steam radiation losses 
            RadiationSteamLossLBpS = pS.FrompM((absSpeedMpS == 0.0f) ?
                3.04f : // lb/min at rest 
                5.29f); // lb/min moving
            BoilerMassLB -= elapsedClockSeconds * RadiationSteamLossLBpS;
            BoilerHeatBTU -= elapsedClockSeconds * RadiationSteamLossLBpS * (BoilerSteamHeatBTUpLB - BoilerWaterHeatBTUpLB);
            TotalSteamUsageLBpS += RadiationSteamLossLBpS;
            BoilerHeatOutBTUpS += RadiationSteamLossLBpS * (BoilerSteamHeatBTUpLB - BoilerWaterHeatBTUpLB);

            // Recalculate the fraction of the boiler containing water (the rest contains saturated steam)
            // The derivation of the WaterFraction equation is not obvious, but starts from:
            // Mb = Mw + Ms, Vb = Vw + Vs and (Vb - Vw)/Vs = 1 where Mb is mass in boiler, Vw is the volume of water etc. and Vw/Vb is the WaterFraction.
            // We can say:
            //                Mw = Mb - Ms
            //                Mw = Mb - Ms x (Vb - Vw)/Vs
            //      Mw - MsVw/Vs = Mb - MsVb/Vs
            // Vw(Mw/Vw - Ms/Vs) = Vb(Mb/Vb - Ms/Vs)
            //             Vw/Vb = (Mb/Vb - Ms/Vs)/(Mw/Vw - Ms/Vs)
            // If density Dx = Mx/Vx, we can write:
            //             Vw/Vb = (Mb/Vb - Ds)/Dw - Ds)
            WaterFraction = ((BoilerMassLB / BoilerVolumeFT3) - BoilerSteamDensityLBpFT3) / (BoilerWaterDensityLBpFT3 - BoilerSteamDensityLBpFT3);
            
            // Update Boiler Heat based upon current Evaporation rate
            // Based on formula - BoilerCapacity (btu/h) = (SteamEnthalpy (btu/lb) - EnthalpyCondensate (btu/lb) ) x SteamEvaporated (lb/h) ?????
            // EnthalpyWater (btu/lb) = BoilerCapacity (btu/h) / SteamEvaporated (lb/h) + Enthalpysteam (btu/lb)  ?????

            //<CJComment> Incorrect statement commented out. Needs sorting. </CJComment>
            BoilerHeatSmoothBTU.Update(elapsedClockSeconds, BoilerHeatBTU);
        //    BoilerHeatSmoothBTU.Update(0.1f, BoilerHeatBTU);

            WaterHeatBTUpFT3 = (BoilerHeatSmoothBTU.Value / BoilerVolumeFT3 - (1 - WaterFraction) * BoilerSteamDensityLBpFT3 * BoilerSteamHeatBTUpLB) / (WaterFraction * BoilerWaterDensityLBpFT3);

            #region Boiler Pressure calculation
            // works on the principle that boiler pressure will go up or down based on the change in water temperature, which is impacted by the heat gain or loss to the boiler
            WaterVolL = WaterFraction * BoilerVolumeFT3 * 28.31f;   // idealy should be equal to water flow in and out. 1ft3 = 28.31 litres of water
            BkW_Diff = (((BoilerHeatInBTUpS - BoilerHeatOutBTUpS) * 3600) * 0.0002931f);            // Calculate difference in boiler rating, ie heat in - heat out - 1 BTU = 0.0002931 kWh, divide by 3600????
            SpecificHeatWaterKJpKGpC = SpecificHeatKtoKJpKGpK[WaterTempNewK] * WaterVolL;  // Spec Heat = kj/kg, litres = kgs of water
            WaterTempIN = BkW_Diff / SpecificHeatWaterKJpKGpC;   // calculate water temp variation
            WaterTempNewK += elapsedClockSeconds * WaterTempIN; // Calculate new water temp
            WaterTempNewK = MathHelper.Clamp(WaterTempNewK, 274.0f, 496.0f);
            if (FusiblePlugIsBlown)
            {
            BoilerPressurePSI = 5.0f; // Drop boiler pressure if fusible plug melts.
            }
            else
            {
            BoilerPressurePSI = SaturationPressureKtoPSI[WaterTempNewK]; // Gauge Pressure
            }
            
            if (!FiringIsManual)
            {
                if (BoilerHeat)
                {
                    PressureRatio = MathHelper.Clamp((MaxBoilerPressurePSI / BoilerPressurePSI), 0.01f, 0.99f); // Boiler pressure ratio to adjust burn rate, if maxboiler heat reached, then clamp ratio < 1.0
                }
                else
                {
                    PressureRatio = MathHelper.Clamp((MaxBoilerPressurePSI / BoilerPressurePSI), 0.01f, 1.2f); // Boiler pressure ratio to adjust burn rate
                }
            }
            #endregion

            if (!FiringIsManual && BoilerPressurePSI > MaxBoilerPressurePSI) // For AI fireman stop excessive pressure
            {
                BoilerPressurePSI = MaxBoilerPressurePSI;  // Check for AI firing
            }
            if (FiringIsManual && BoilerPressurePSI > MaxBoilerPressurePSI + 10) // For manual fireman stop excessive pressure
            {
                BoilerPressurePSI = MaxBoilerPressurePSI + 10.0f;  // Check for manual firing
            }

            ApplyBoilerPressure();
        }

        private void ApplyBoilerPressure()
        {
            BoilerSteamHeatBTUpLB = SteamHeatPSItoBTUpLB[BoilerPressurePSI];
            BoilerWaterHeatBTUpLB = WaterHeatPSItoBTUpLB[BoilerPressurePSI];
            BoilerSteamDensityLBpFT3 = SteamDensityPSItoLBpFT3[BoilerPressurePSI];
            BoilerWaterDensityLBpFT3 = WaterDensityPSItoLBpFT3[BoilerPressurePSI];

            // Save values for use in UpdateFiring() and HUD
            PreviousBoilerHeatOutBTUpS = BoilerHeatOutBTUpS;
            PreviousTotalSteamUsageLBpS = TotalSteamUsageLBpS;
            // Reset for next pass
            BoilerHeatOutBTUpS = 0.0f;
            TotalSteamUsageLBpS = 0.0f;
        }

        private void UpdateCylinders(float elapsedClockSeconds, float throttle, float cutoff, float absSpeedMpS)
        {
            // Calculate speed of locomotive in wheel rpm - used to determine changes in performance based upon speed.
            DrvWheelRevRpS = absSpeedMpS / (2.0f * MathHelper.Pi * DriverWheelRadiusM);
            // Determine if Superheater in use
            if (HasSuperheater)
            {
                CurrentSuperheatTeampF = SuperheatTempLbpHtoDegF[pS.TopH(CylinderSteamUsageLBpS)] * SuperheatTempRatio; // Calculate current superheat temp
                float DifferenceSuperheatTeampF = CurrentSuperheatTeampF - SuperheatTempLimitXtoDegF[cutoff]; // reduce superheat temp due to cylinder condensation
                SuperheatVolumeRatio = 1.0f + (0.0015f * DifferenceSuperheatTeampF); // Based on formula Vsup = Vsat ( 1 + 0.0015 Tsup) - Tsup temperature at superheated level
                // look ahead to see what impact superheat will have on cylinder usage
                float FutureCylinderSteamUsageLBpS = CylinderSteamUsageLBpS * 1.0f / SuperheatVolumeRatio; // Calculate potential future new cylinder steam usage
                float FutureSuperheatTeampF = SuperheatTempLbpHtoDegF[pS.TopH(FutureCylinderSteamUsageLBpS)] * SuperheatTempRatio; // Calculate potential future new superheat temp
                

                if (CurrentSuperheatTeampF > SuperheatTempLimitXtoDegF[cutoff])
                {
                  if (FutureSuperheatTeampF < SuperheatTempLimitXtoDegF[cutoff])
                  {
                  SuperheaterSteamUsageFactor = 1.0f; // Superheating has minimal impact as all heat has been lost in the steam, so no steam reduction is achieved, but condensation has stopped
                  }
                  else
                  {
                  SuperheaterSteamUsageFactor = 1.0f / SuperheatVolumeRatio; // set steam reduction based on Superheat Volume Ratio
                  }
                }
                else
                {
                    SuperheaterSteamUsageFactor = 1.0f + CylinderCondensationFractionX[cutoff]; // calculate steam usage factor for superheated steam locomotive when superheat tem is not high enough to stop condensation 
                }
            }
            else
            {
                SuperheaterSteamUsageFactor = 1.0f + CylinderCondensationFractionX[cutoff]; // calculate steam usage factor for saturated steam locomotive according to cylinder condensation fraction 
            }
 

            #region Calculation of Mean Effective Pressure of Cylinder using an Indicator Diagram type approach
            // Note all presurres in absolute for working on steam indicator diagram
            // Calculate Ratio of expansion, with cylinder clearance
            // R (ratio of Expansion) = (length of stroke to point of  exhaust + clearance) / (length of stroke to point of cut-off + clearance)
            // Expressed as a fraction of stroke R = (Exhaust point + c) / (cutoff + c)
            RatioOfExpansion = (CylinderExhaustOpenFactor + CylinderClearancePC) / (cutoff + CylinderClearancePC);
            // Absolute Mean Pressure = Ratio of Expansion
            SteamChestPressurePSI = (throttle * SteamChestPressureDropRatioRpMtoX[pS.TopM(DrvWheelRevRpS)] * BoilerPressurePSI); // pressure in cylinder steam chest - allowance for pressure drop between boiler and steam chest
           
            // Initial pressure will be decreased depending upon locomotive speed
            // This drop can be adjusted with a table in Eng File
           InitialPressureAtmPSI = ((throttle * BoilerPressurePSI) + OneAtmospherePSI) * InitialPressureDropRatioRpMtoX[pS.TopM(DrvWheelRevRpS)]; // This is the gauge pressure + atmospheric pressure to find the absolute pressure - pressure drop gas been allowed for as the steam goes into the cylinder through the opening in the steam chest port.
           
           BackPressureAtmPSI = BackPressureIHPtoAtmPSI[IndicatedHorsePowerHP]; 

            if(throttle < 0.02f)
            {
                InitialPressureAtmPSI = 0.0f;  // for sake of display zero pressure values if throttle is closed.
                BackPressureAtmPSI = 0.0f;
            }
                        
            // In driving the wheels steam does work in the cylinders. The amount of work can be calculated by a typical steam indicator diagram
            // Mean Effective Pressure (work) = average positive pressures - average negative pressures
            // Average Positive pressures = admission + expansion + release
            // Average Negative pressures = exhaust + compression + pre-admission

             // Calculate Cut-off Pressure
            float CutoffDropUpper = CutoffInitialPressureDropRatioUpper.Get(pS.TopM(DrvWheelRevRpS), cutoff);  // Get Cutoff Pressure to Initial pressure drop - upper limit
            float CutoffDropLower = CutoffInitialPressureDropRatioLower.Get(pS.TopM(DrvWheelRevRpS), cutoff);  // Get Cutoff Pressure to Initial pressure drop - lower limit

            // calculate value based upon setting of Cylinder port opening

            CutoffPressureDropRatio = (((CylinderPortOpeningFactor - CylinderPortOpeningLower) / (CylinderPortOpeningUpper - CylinderPortOpeningLower)) * (CutoffDropUpper - CutoffDropLower)) + CutoffDropLower;

            CutoffPressureAtmPSI = InitialPressureAtmPSI * CutoffPressureDropRatio;
              
    // Calculate Av Admission Work (inch pounds)
            // Av Admission work = Av (Initial Pressure + Cutoff Pressure) * length of Cylinder during cutoff
            float CylinderLengthAdmissionIn = Me.ToIn(CylinderStrokeM * ((cutoff + CylinderClearancePC) - CylinderClearancePC));
            CylinderAdmissionWorkInLbs = ((InitialPressureAtmPSI + CutoffPressureAtmPSI) / 2.0f) * CylinderLengthAdmissionIn;

    // Calculate Av Expansion Work (inch pounds)
            // Av pressure during expansion = Cutoff pressure x log (ratio of expansion) / (ratio of expansion - 1.0) 
            // Av Expansion work = Av pressure during expansion * length of Cylinder during expansion
            float CylinderLengthExpansionIn = Me.ToIn(CylinderStrokeM) * ((CylinderExhaustOpenFactor + CylinderClearancePC) - (cutoff + CylinderClearancePC));
            float AverageExpansionPressureAtmPSI = CutoffPressureAtmPSI * ((float)Math.Log(RatioOfExpansion) / (RatioOfExpansion - 1.0f));
            CylinderExpansionWorkInLbs = AverageExpansionPressureAtmPSI * CylinderLengthExpansionIn;

    // Calculate Av Release work (inch pounds)
            // Exhaust pressure = Cutoff Pressure x Cylinder Volume (at cutoff point) / cylinder volume (at release)
            // Av Release work = Av pressure during release * length of Cylinder during release
            CylinderExhaustPressureAtmPSI = (CutoffPressureAtmPSI) * (cutoff + CylinderClearancePC) / (CylinderExhaustOpenFactor + CylinderClearancePC);  // Check factor to calculate volume of cylinder for new volume at exhaust
            float CylinderLengthReleaseIn = Me.ToIn(CylinderStrokeM) * ((1.0f + CylinderClearancePC) - (CylinderExhaustOpenFactor + CylinderClearancePC)); // Full cylinder length is 1.0
            CylinderReleaseWorkInLbs = ((CylinderExhaustPressureAtmPSI + BackPressureAtmPSI) / 2.0f) * CylinderLengthReleaseIn;

    // Calculate Av Exhaust Work (inch pounds)
            // Av Exhaust work = Av pressure during exhaust * length of Cylinder during exhaust stroke
            CylinderExhaustWorkInLbs = BackPressureAtmPSI * Me.ToIn(CylinderStrokeM) * ((1.0f - CylinderCompressionCloseFactor) + CylinderClearancePC);
            
    // Calculate Av Compression Work (inch pounds)
          // Calculate pre-compression pressure based upon backpresure being equal to it, as steam should be exhausting
          // Ratio of compression = stroke during compression = stroke @ start of compression - stroke and end of compression
          // Av compression pressure = PreCompression Pressure x Ratio of Compression x log (Ratio of Compression) / (Ratio of Compression - 1.0)
          // Av Exhaust work = Av pressure during compression * length of Cylinder during compression stroke
           CylinderPreCompressionPressureAtmPSI = (BackPressureAtmPSI);
            float RatioOfCompression = (CylinderCompressionCloseFactor + CylinderClearancePC) / (CylinderPreAdmissionOpenFactor + CylinderClearancePC);
            float CylinderLengthCompressionIn = Me.ToIn(CylinderStrokeM) * ((CylinderCompressionCloseFactor + CylinderClearancePC) - (CylinderPreAdmissionOpenFactor + CylinderClearancePC));
            float AverageCompressionPressureAtmPSI = CylinderPreCompressionPressureAtmPSI * RatioOfCompression * ((float)Math.Log(RatioOfCompression) / (RatioOfCompression - 1.0f));
            CylinderCompressionWorkInLbs = AverageCompressionPressureAtmPSI * CylinderLengthCompressionIn;

       //     Trace.TraceInformation("Av Press {0} Length {1}", AverageCompressionPressureAtmPSI, CylinderLengthCompressionIn);
           
    // Calculate Av Pre-admission work (inch pounds)
            // PreAdmission pressure = PreCompression Pressure x Ratio of Compression x log (Ratio of Compression) / (Ratio of Compression - 1.0)
            // Av Pre-admission work = Av pressure during pre-admission * length of Cylinder during pre-admission stroke
            CylinderPreAdmissionPressureAtmPSI = CylinderPreCompressionPressureAtmPSI * (CylinderCompressionCloseFactor + CylinderClearancePC) / (CylinderPreAdmissionOpenFactor + CylinderClearancePC);  // Check factor to calculate volume of 
           CylinderPreAdmissionWorkInLbs = ((InitialPressureAtmPSI + CylinderPreAdmissionPressureAtmPSI) / 2.0f) * CylinderPreAdmissionOpenFactor * Me.ToIn(CylinderStrokeM);
            
           // Calculate total work in cylinder
            float TotalPositiveWorkInLbs = CylinderAdmissionWorkInLbs + CylinderExpansionWorkInLbs + CylinderReleaseWorkInLbs - CylinderExhaustWorkInLbs - CylinderCompressionWorkInLbs - CylinderPreAdmissionWorkInLbs;

            MeanEffectivePressurePSI = TotalPositiveWorkInLbs / Me.ToIn(CylinderStrokeM);
            MeanEffectivePressurePSI = MathHelper.Clamp(MeanEffectivePressurePSI, 0, MaxBoilerPressurePSI + OneAtmospherePSI); // Make sure that Cylinder pressure does not go negative
            
            #endregion

            //  MeanPressureStrokePSI = InitialPressurePSI * (cutoff + ((cutoff + CylinderClearancePC) * (float)Math.Log(RatioOfExpansion))) * CutoffPressureDropRatio;
 //           MeanPressureStrokePSI = InitialPressurePSI * (cutoff + ((cutoff + CylinderClearancePC) * (float)Math.Log(RatioOfExpansion)));//
            // mean pressure during stroke = ((absolute mean pressure + (clearance + cylstroke)) - (initial pressure + clearance)) / cylstroke
           // Mean effective pressure = cylpressure - backpressure
        
            
            // Cylinder pressure also reduced by steam vented through cylinder cocks.
            CylCockPressReduceFactor = 1.0f;

            if (CylinderCocksAreOpen) // Don't apply steam cocks derate until Cylinder steam usage starts to work
            {
                if (HasSuperheater)
                {
                    CylCockPressReduceFactor = ((CylinderSteamUsageLBpS / SuperheaterSteamUsageFactor) / ((CylinderSteamUsageLBpS / SuperheaterSteamUsageFactor) + CylCockSteamUsageLBpS)); // For superheated locomotives temp convert back to a saturated comparison for calculation of steam cock reduction factor.
                }
                else
                {
                    CylCockPressReduceFactor = (CylinderSteamUsageLBpS / (CylinderSteamUsageLBpS + CylCockSteamUsageLBpS)); // Saturated steam locomotive
                }
                CylinderPressureAtmPSI = CutoffPressureAtmPSI - (CutoffPressureAtmPSI * (1.0f - CylCockPressReduceFactor)); // Allow for pressure reduction due to Cylinder cocks being open.
            }
            else
            {
                CylinderPressureAtmPSI = CutoffPressureAtmPSI;
            }

            CylinderPressureAtmPSI = MathHelper.Clamp(CylinderPressureAtmPSI, 0, MaxBoilerPressurePSI + OneAtmospherePSI); // Make sure that Cylinder pressure does not go negative
          
          #region Calculation of Cylinder steam usage using an Indicator Diagram type approach
          // To calculate steam usage, Calculate amount of steam in cylinder 
          // Cylinder steam usage = steam volume (and weight) at start of release stage - steam remaining in cylinder after compression
          // This amount then should be corrected to allow for cylinder condensation in saturated locomotives or not in superheated locomotives
          
          float CylinderExhaustPressureGaugePSI = CylinderExhaustPressureAtmPSI - OneAtmospherePSI; // Convert to gauge pressure as steam tables are in gauge pressure
          float CylinderPreAdmissionPressureGaugePSI = CylinderPreAdmissionPressureAtmPSI - OneAtmospherePSI; // Convert to gauge pressure as steam tables are in gauge pressure  
          float CylinderVolumeReleaseFt3 = CylinderSweptVolumeFT3pFT * (CylinderExhaustOpenFactor + CylinderClearancePC); // Calculate volume of cylinder at start of release
          float CylinderReleaseSteamWeightLbs = CylinderVolumeReleaseFt3 * CylinderSteamDensityPSItoLBpFT3[CylinderExhaustPressureAtmPSI]; // Weight of steam in Cylinder at release
          float CylinderClearanceSteamVolumeFt3 = CylinderSweptVolumeFT3pFT * (CylinderPreAdmissionOpenFactor + CylinderClearancePC); // volume of the clearance area + area of steam at pre-admission
          float CylinderClearanceSteamWeightLbs = CylinderClearanceSteamVolumeFt3 * CylinderSteamDensityPSItoLBpFT3[CylinderPreAdmissionPressureAtmPSI]; // Weight of total steam remaining in the cylinder
          
          float CalculatedCylinderSteamUsageLBpS = NumCylinders * DrvWheelRevRpS * CylStrokesPerCycle * (CylinderReleaseSteamWeightLbs - CylinderClearanceSteamWeightLbs) * SuperheaterSteamUsageFactor;
          
          #endregion
          

            if (throttle < 0.01 && absSpeedMpS > 0.1) // If locomotive moving and throttle set to close, then reduce steam usage.
            {
                CalculatedCylinderSteamUsageLBpS = 0.3f; // Set steam usage to a small value if throttle is closed
            }
                                
            // usage calculated as moving average to minimize chance of oscillation.
            // Decrease steam usage by SuperheaterUsage factor to model superheater - very crude model - to be improved upon
            CylinderSteamUsageLBpS = (0.6f * CylinderSteamUsageLBpS + 0.4f * CalculatedCylinderSteamUsageLBpS);
            
//            NewCylinderSteamUsageLBpS = (0.6f * CylinderSteamUsageLBpS + 0.4f * CalculatedCylinderSteamUsageLBpS);

            BoilerMassLB -= elapsedClockSeconds * CylinderSteamUsageLBpS; //  Boiler mass will be reduced by cylinder steam usage
            BoilerHeatBTU -= elapsedClockSeconds * CylinderSteamUsageLBpS * (BoilerSteamHeatBTUpLB - BoilerWaterHeatBTUpLB); //  Boiler Heat will be reduced by heat required to replace the cylinder steam usage, ie create steam from hot water. 
            TotalSteamUsageLBpS += CylinderSteamUsageLBpS;
            BoilerHeatOutBTUpS += CylinderSteamUsageLBpS * (BoilerSteamHeatBTUpLB - BoilerWaterHeatBTUpLB);
        }

        private void UpdateMotion(float elapsedClockSeconds, float cutoff, float absSpeedMpS)
        {
           
           // This section updates the force calculations and maintains them at the current values.

           // Caculate the current piston speed - purely for display purposes at the moment 
           // Piston Speed (Ft p Min) = (Stroke length x 2) x (Ft in Mile x Train Speed (mph) / ( Circum of Drv Wheel x 60))
            PistonSpeedFtpM = Me.ToFt(pS.TopM(CylinderStrokeM * 2.0f * DrvWheelRevRpS)) * SteamGearRatio;
             
            TractiveEffortLbsF = (NumCylinders / 2.0f) * (Me.ToIn(CylinderDiameterM) * Me.ToIn(CylinderDiameterM) * Me.ToIn(CylinderStrokeM) / (2 * Me.ToIn(DriverWheelRadiusM))) * MeanEffectivePressurePSI * CylinderEfficiencyRate * MotiveForceGearRatio;
            TractiveEffortLbsF = MathHelper.Clamp(TractiveEffortLbsF, 0, TractiveEffortLbsF);
            DisplayTractiveEffortLbsF = TractiveEffortLbsF;
                      
            // Calculate IHP
            // IHP = (MEP x CylStroke(ft) x cylArea(sq in) x No Strokes (/min)) / 33000) - this is per cylinder
            IndicatedHorsePowerHP = NumCylinders * MotiveForceGearRatio * ((MeanEffectivePressurePSI * Me.ToFt(CylinderStrokeM) *  Me2.ToIn2(Me2.FromFt2(CylinderPistonAreaFt2)) * pS.TopM(DrvWheelRevRpS) * CylStrokesPerCycle / 33000.0f));
       
            DrawBarPullLbsF = -1.0f * N.ToLbf(CouplerForceU);
            DrawbarHorsePowerHP = -1.0f * (N.ToLbf(CouplerForceU) * Me.ToFt(absSpeedMpS)) / 550.0f;  // TE in this instance is a maximum, and not at the wheel???
                
            
            MotiveForceSmoothedN.Update(elapsedClockSeconds, MotiveForceN);
            if (float.IsNaN(MotiveForceN))
                MotiveForceN = 0;
            switch (this.Train.TrainType)
            {
                case Train.TRAINTYPE.AI:
                case Train.TRAINTYPE.AI_PLAYERHOSTING:
                case Train.TRAINTYPE.STATIC:
                    break;
                case Train.TRAINTYPE.PLAYER:
                case Train.TRAINTYPE.AI_PLAYERDRIVEN:
                case Train.TRAINTYPE.REMOTE:
                    LimitMotiveForce(elapsedClockSeconds);
                    break;
                default:
                    break;
            }

            if (absSpeedMpS == 0 && cutoff < 0.3f)
                MotiveForceN = 0;   // valves assumed to be closed

        }

        protected override void UpdateMotiveForce(float elapsedClockSeconds, float t, float currentSpeedMpS, float currentWheelSpeedMpS)
        {
            // Pass force and power information to MSTSLocomotive file by overriding corresponding method there

            // Set Max Power equal to max IHP
            MaxPowerW = W.FromHp(MaxIndicatedHorsePowerHP);

            // Set maximum force for the locomotive
            MaxForceN = N.FromLbf(MaxTractiveEffortLbf * CylinderEfficiencyRate);

            // Set Max Velocity of locomotive
            MaxSpeedMpS = Me.FromMi(pS.FrompH(MaxLocoSpeedMpH)); // Note this is not the true max velocity of the locomotive, but  the speed at which max HP is reached
                     
        // Set "current" motive force based upon the throttle, cylinders, steam pressure, etc	
            MotiveForceN = (Direction == Direction.Forward ? 1 : -1) * N.FromLbf(TractiveEffortLbsF);

            // On starting allow maximum motive force to be used
            if (absSpeedMpS < 1.0f && cutoff > 0.70f && throttle > 0.98f)
            {
                MotiveForceN = (Direction == Direction.Forward ? 1 : -1) * MaxForceN;
            }
                       
            // Based upon max IHP, limit motive force.
            if (PistonSpeedFtpM > MaxPistonSpeedFtpM || IndicatedHorsePowerHP > MaxIndicatedHorsePowerHP)
            {

                if (IndicatedHorsePowerHP >= MaxIndicatedHorsePowerHP)
                {
                    IndicatedHorsePowerHP = MaxIndicatedHorsePowerHP; // Set IHP to maximum value
                }
                // Calculate the speed factor for the locomotive, based upon piston speed    
                if ( HasSuperheater)
	        {
                SpeedFactor = SuperheatedSpeedFactorSpeedDropFtpMintoX[PistonSpeedFtpM];
	        }
	        else if (IsGearedSteamLoco)
	        {
                SpeedFactor = SaturatedSpeedFactorSpeedDropFtpMintoX[PistonSpeedFtpM];   // Assume the same as saturated locomotive for time being.
	        }
	        else
	        {
                SpeedFactor = SaturatedSpeedFactorSpeedDropFtpMintoX[PistonSpeedFtpM];	        
	        }
                
                if((TractiveEffortLbsF * CylinderEfficiencyRate) > CriticalSpeedTractiveEffortLbf)
                {
                    MotiveForceN = (Direction == Direction.Forward ? 1 : -1) * N.FromLbf(CriticalSpeedTractiveEffortLbf);
                    DisplayTractiveEffortLbsF = CriticalSpeedTractiveEffortLbf;
                }
            }

#region - Experimental Steam Slip Monitor

	// Based upon information presented in "Locomotive Operation" by Henderson
	// At its simplest slip occurs when the wheel tangential force exceeds the static frictional force
	// Static frictional force = weight on the locomotive driving wheels * frictional co-efficient
	// Tangential force = Effective force (Interia + Piston force) * Tangential factor (sin (crank angle) + (crank radius / connecting rod length) * sin (crank angle) * cos (crank angle))
	// Typically tangential force will be greater at starting then when the locomotive is at speed, as interia and reduce steam pressure will decrease the value. 
    // Thus we will only consider slip impacts at start of the locomotive


	// Assume set crank radius & connecting rod length 
	float CrankRadiusFt = 1.08f;        // Assume crank and rod lengths to give a 1:10 ratio - a reasonable av for steam locomotives?
	float ConnectRodLengthFt = 10.8f;

	// Starting tangential force - at starting piston force is based upon cutoff pressure  & interia = 0
	PistonForceLbf = Me2.ToIn2(Me2.FromFt2(CylinderPistonAreaFt2)) * InitialPressureAtmPSI; // Piston force is equal to pressure in piston and piston area

    // At starting, for 2 cylinder locomotive, maximum tangential force occurs at the following crank angles:
    // Backward - 45 deg & 135 deg, Forward - 135 deg & 45 deg. To calculate the maximum we only need to select one of these points
    // To calculate total tangential force we need to calculate the left and right hand side of the locomotive, LHS & RHS will be 90 deg apart
    float RadConvert = (float)Math.PI / 180.0f;  // Conversion of degs to radians
    float CrankAngleLeft;
    float CrankAngleRight;
    float CrankAngleMiddle;
    float TangentialCrankForceFactorLeft;
    float TangentialCrankForceFactorMiddle = 0.0f;
    float TangentialCrankForceFactorRight;

            
    if (NumCylinders == 3.0)
    {
     CrankAngleLeft = RadConvert * 30.0f;	// For 3 Cylinder locomotive, cranks are 120 deg apart, and maximum occurs @ 
     CrankAngleMiddle = RadConvert * 150.0f;	// 30, 150, 270 deg crank angles
     CrankAngleRight = RadConvert * 270.0f;
     TangentialCrankForceFactorLeft = ((float)Math.Sin(CrankAngleLeft) + ((CrankRadiusFt / ConnectRodLengthFt) * (float)Math.Sin(CrankAngleLeft) * (float)Math.Cos(CrankAngleLeft)));
     TangentialCrankForceFactorMiddle = ((float)Math.Sin(CrankAngleMiddle) + ((CrankRadiusFt / ConnectRodLengthFt) * (float)Math.Sin(CrankAngleMiddle) * (float)Math.Cos(CrankAngleMiddle)));
     TangentialCrankForceFactorRight = ((float)Math.Sin(CrankAngleRight) + ((CrankRadiusFt / ConnectRodLengthFt) * (float)Math.Sin(CrankAngleRight) * (float)Math.Cos(CrankAngleRight)));
    }
    else
    {
    CrankAngleLeft = RadConvert * 315.0f;	// For 2 Cylinder locomotive, cranks are 90 deg apart, and maximum occurs @ 
    CrankAngleRight = RadConvert * 45.0f;	// 315 & 45 deg crank angles
    TangentialCrankForceFactorLeft = ((float)Math.Sin(CrankAngleLeft) + ((CrankRadiusFt / ConnectRodLengthFt) * (float)Math.Sin(CrankAngleLeft) * (float)Math.Cos(CrankAngleLeft)));
    TangentialCrankForceFactorRight = ((float)Math.Sin(CrankAngleRight) + ((CrankRadiusFt / ConnectRodLengthFt) * (float)Math.Sin(CrankAngleRight) * (float)Math.Cos(CrankAngleRight)));
    TangentialCrankForceFactorMiddle = 0.0f;
    }


    TangentialCrankWheelForceLbf = Math.Abs(PistonForceLbf * TangentialCrankForceFactorLeft) + Math.Abs(PistonForceLbf * TangentialCrankForceFactorMiddle) + Math.Abs(PistonForceLbf * TangentialCrankForceFactorRight);

    // Calculate internal resistance - IR = 3.8 * diameter of cylinder^2 * stroke * dia of drivers (all in inches)

    float InternalResistance = 3.8f * Me.ToIn(CylinderDiameterM) * Me.ToIn(CylinderDiameterM) * Me.ToIn(CylinderStrokeM) / (Me.ToIn(DriverWheelRadiusM) * 2.0f);

     // To convert the force at the crank to the force at wheel tread = Crank Force * Cylinder Stroke / Diameter of Drive Wheel (inches) - internal friction should be deducted from this as well.

    TangentialWheelTreadForceLbf = (TangentialCrankWheelForceLbf * Me.ToIn(CylinderStrokeM) / (Me.ToIn(DriverWheelRadiusM) * 2.0f)) - InternalResistance;
    TangentialWheelTreadForceLbf = MathHelper.Clamp(TangentialWheelTreadForceLbf, 0, TangentialWheelTreadForceLbf);

    // Vertical thrust of the connecting rod will reduce or increase the effect of the adhesive weight of the locomotive
    // Vert Thrust = Piston Force * 3/4 * r/l * sin(crank angle)
    float VerticalThrustFactorLeft = 3.0f / 4.0f * (CrankRadiusFt / ConnectRodLengthFt) * (float)Math.Sin(CrankAngleLeft);
    float VerticalThrustFactorMiddle = 3.0f / 4.0f * (CrankRadiusFt / ConnectRodLengthFt) * (float)Math.Sin(CrankAngleLeft);
    float VerticalThrustFactorRight = 3.0f / 4.0f * (CrankRadiusFt / ConnectRodLengthFt) * (float)Math.Sin(CrankAngleRight);
    float VerticalThrustForceMiddle = 0.0f;

    float VerticalThrustForceLeft = PistonForceLbf * VerticalThrustFactorLeft;
    VerticalThrustForceMiddle = PistonForceLbf * VerticalThrustFactorMiddle;
    float VerticalThrustForceRight = PistonForceLbf * VerticalThrustFactorRight;

  // Determine weather conditions and friction coeff
  // Typical coefficients of friction
  // Sand ----  40% increase of friction coeff., sand on wet railes, tends to make adhesion as good as dry rails.
  // Normal, wght per wheel > 10,000lbs   == 0.35
  // Normal, wght per wheel < 10,000lbs   == 0.25
  // Damp or frosty rails   == 0.20
  //
  // Dynamic (kinetic) friction = 0.242  // dynamic friction at slow speed
    float DrvWheelNum = GetLocoNumWheels();
    WheelWeightLbs = Kg.ToLb(DrvWheelWeightKg / (DrvWheelNum * 2.0f)); // Calculate the weight per wheel
   

    if (Program.Simulator.Weather == WeatherType.Rain || Program.Simulator.Weather == WeatherType.Snow)
  {
    if (IsLocoSlip)   // If loco is slipping then coeff of friction will be decreased below static value.
    {
    FrictionCoeff = 0.20f;  // Wet track - dynamic friction
    }
    else
    {
    FrictionCoeff = 0.20f;  // Wet track - static friction
    }
  } 
  else
  {
    if (IsLocoSlip)    // If loco is slipping then coeff of friction will be decreased below static value.
    {
    FrictionCoeff = 0.242f;  // Dry track - dynamic friction
    }
    else
    {
        if (WheelWeightLbs < 10000)
        {
            FrictionCoeff = 0.25f;  // Dry track - static friction for vehicles with wheel weights less then 10,000lbs
        }
        else
        {
            FrictionCoeff = 0.35f;  // Dry track - static friction for vehicles with wheel weights greater then 10,000lbs
        }
    }
  }
   if ( Sander )
      {
      FrictionCoeff = 0.4f;  // Sand track
      }

	// Static Friction Force - adhesive factor increased by vertical thrust when travelling forward, and reduced by vertical thrust when travelling backwards

  if (Direction == Direction.Forward)
  {
      StaticWheelFrictionForceLbf = (Kg.ToLb(DrvWheelWeightKg) + Math.Abs(VerticalThrustForceLeft) + Math.Abs(VerticalThrustForceRight)) * FrictionCoeff;
  }
  else
  {
      StaticWheelFrictionForceLbf = (Kg.ToLb(DrvWheelWeightKg) - Math.Abs(VerticalThrustForceLeft) - Math.Abs(VerticalThrustForceRight)) * FrictionCoeff;
  }

    if (absSpeedMpS < 1.0)  // Test only when the locomotive is starting
    {
        if (!IsLocoSlip)
        {
            if (TangentialWheelTreadForceLbf > StaticWheelFrictionForceLbf)
            {
                IsLocoSlip = true; 	// locomotive is slipping
            }
        }
        else if (IsLocoSlip)
        {
            if (TangentialWheelTreadForceLbf < StaticWheelFrictionForceLbf)
            {
                IsLocoSlip = false; 	// locomotive is slipping
            }
        }
    }
    else
    {
        IsLocoSlip = false; 	// locomotive is slipping

    }

#endregion

            // Derate when priming is occurring.
            if (BoilerIsPriming)
                MotiveForceN *= BoilerPrimingDeratingFactor;
            // Find the maximum TE for debug i.e. @ start and full throttle
            if (absSpeedMpS < 1.0)
            {
                if (MotiveForceN > StartTractiveEffortN && MotiveForceN < MaxForceN)
                {
                    StartTractiveEffortN = MotiveForceN; // update to new maximum TE
                }
            }
        }

        protected override float GetSteamLocoMechFrictN()
        {
            // Calculate steam locomotive mechanical friction value, ie 20 (or 98.0667 metric) x DrvWheelWeight x Valve Factor, Assume VF = 1
                          
            const float MetricTonneFromKg = 1000.0f;    // Conversion factor to convert from kg to tonnes
            return 98.0667f * (DrvWheelWeightKg / MetricTonneFromKg);
        }

        private void UpdateAuxiliaries(float elapsedClockSeconds, float absSpeedMpS)
        {
            // Calculate Air Compressor steam Usage if turned on
            if (CompressorIsOn)
            {
                CompSteamUsageLBpS = Me3.ToFt3(Me3.FromIn3((float)Math.PI * (CompCylDiaIN / 2.0f) * (CompCylDiaIN / 2.0f) * CompCylStrokeIN * pS.FrompM(CompStrokespM))) * SteamDensityPSItoLBpFT3[BoilerPressurePSI];   // Calculate Compressor steam usage - equivalent to volume of compressor steam cylinder * steam denisty * cylinder strokes
                BoilerMassLB -= elapsedClockSeconds * CompSteamUsageLBpS; // Reduce boiler mass to reflect steam usage by compressor
                BoilerHeatBTU -= elapsedClockSeconds * CompSteamUsageLBpS * (BoilerSteamHeatBTUpLB - BoilerWaterHeatBTUpLB);  // Reduce boiler Heat to reflect steam usage by compressor
                BoilerHeatOutBTUpS += CompSteamUsageLBpS * (BoilerSteamHeatBTUpLB - BoilerWaterHeatBTUpLB);  // Reduce boiler Heat to reflect steam usage by compressor

                TotalSteamUsageLBpS += CompSteamUsageLBpS;
            }
            else
            {
                CompSteamUsageLBpS = 0.0f;    // Set steam usage to zero if compressor is turned off
            }
            // Calculate cylinder cock steam Usage if turned on
            // The cock steam usage will be assumed equivalent to a steam orifice
            // Steam Flow (lb/hr) = 24.24 x Press(Cylinder + Atmosphere(psi)) x CockDia^2 (in) - this needs to be multiplied by Num Cyls
            if (CylinderCocksAreOpen == true)
            {
                if (throttle > 0.02) // if regulator open
                {
                    CylCockSteamUsageLBpS = pS.FrompH(NumCylinders * (24.24f * (CylinderPressureAtmPSI) * CylCockDiaIN * CylCockDiaIN));
                    BoilerMassLB -= elapsedClockSeconds * CylCockSteamUsageLBpS; // Reduce boiler mass to reflect steam usage by cylinder steam cocks  
                    BoilerHeatBTU -= elapsedClockSeconds * CylCockSteamUsageLBpS * (BoilerSteamHeatBTUpLB - BoilerWaterHeatBTUpLB);  // Reduce boiler Heat to reflect steam usage by cylinder steam cocks
                    BoilerHeatOutBTUpS += CylCockSteamUsageLBpS * (BoilerSteamHeatBTUpLB - BoilerWaterHeatBTUpLB);  // Reduce boiler Heat to reflect steam usage by cylinder steam cocks                
                    TotalSteamUsageLBpS += CylCockSteamUsageLBpS;
                }
                else
                {
                    CylCockSteamUsageLBpS = 0.0f; // set usage to zero if regulator closed
                }
            }
            else
            {
                CylCockSteamUsageLBpS = 0.0f;       // set steam usage to zero if turned off
            }
            //<CJComment> What if there is no electricity generator? </CJComment>
            // Calculate Generator steam Usage if turned on
            // Assume generator kW = 350W for D50 Class locomotive
            if (absSpeedMpS > 2.0f) //  Turn generator on if moving
            {
                GeneratorSteamUsageLBpS = 0.0291666f; // Assume 105lb/hr steam usage for 500W generator
            //   GeneratorSteamUsageLbpS = (GeneratorSizekW * SteamkwToBTUpS) / steamHeatCurrentBTUpLb; // calculate Generator steam usage
                BoilerMassLB -= elapsedClockSeconds * GeneratorSteamUsageLBpS; // Reduce boiler mass to reflect steam usage by generator  
                BoilerHeatBTU -= elapsedClockSeconds * GeneratorSteamUsageLBpS * (BoilerSteamHeatBTUpLB - BoilerWaterHeatBTUpLB);  // Reduce boiler Heat to reflect steam usage by generator
                BoilerHeatOutBTUpS += GeneratorSteamUsageLBpS * (BoilerSteamHeatBTUpLB - BoilerWaterHeatBTUpLB);  // Reduce boiler Heat to reflect steam usage by generator
                TotalSteamUsageLBpS += GeneratorSteamUsageLBpS;
            }
            else
            {
                GeneratorSteamUsageLBpS = 0.0f;
            }
            if (StokerIsMechanical)
            {
                StokerSteamUsageLBpS = pS.FrompH(MaxBoilerOutputLBpH) * (StokerMinUsage + (StokerMaxUsage - StokerMinUsage) * FuelFeedRateKGpS / MaxFiringRateKGpS);  // Caluculate current steam usage based on fuel feed rates
                BoilerMassLB -= elapsedClockSeconds * StokerSteamUsageLBpS; // Reduce boiler mass to reflect steam usage by mechanical stoker  
                BoilerHeatBTU -= elapsedClockSeconds * StokerSteamUsageLBpS * (BoilerSteamHeatBTUpLB - BoilerWaterHeatBTUpLB);  // Reduce boiler Heat to reflect steam usage by mechanical stoker
                BoilerHeatOutBTUpS += StokerSteamUsageLBpS * (BoilerSteamHeatBTUpLB - BoilerWaterHeatBTUpLB);  // Reduce boiler Heat to reflect steam usage by mecahnical stoker
                TotalSteamUsageLBpS += StokerSteamUsageLBpS;
            }
            // Other Aux device usage??
        }

        private void UpdateWaterGauge()
        {
            WaterGlassLevelIN = ((WaterFraction - WaterGlassMinLevel) / (WaterGlassMaxLevel - WaterGlassMinLevel)) * WaterGlassLengthIN;
            WaterGlassLevelIN = MathHelper.Clamp(WaterGlassLevelIN, 0, WaterGlassLengthIN);

            if (WaterFraction < WaterMinLevel)
            {
                if (!FusiblePlugIsBlown)
                    Simulator.Confirmer.Message(ConfirmLevel.Warning, Viewer3D.Viewer.Catalog.GetString("Water level dropped too far. Plug has fused and loco has failed."));
                FusiblePlugIsBlown = true; // if water level has dropped, then fusible plug will blow , see "water model"
            }
            // Check for priming            
            if (WaterFraction >= WaterMaxLevel)
            {
                if (!BoilerIsPriming)
                    Simulator.Confirmer.Message(ConfirmLevel.Warning, Viewer3D.Viewer.Catalog.GetString("Boiler overfull and priming."));
                BoilerIsPriming = true;
            }
            else if (WaterFraction < WaterMaxLevelSafe)
            {
                if (BoilerIsPriming)
                    Simulator.Confirmer.Message(ConfirmLevel.Information, Viewer3D.Viewer.Catalog.GetString("Boiler no longer priming."));
                BoilerIsPriming = false;
            }
        }

        private void UpdateInjectors(float elapsedClockSeconds)
        {
            // Calculate size of injectors to suit cylinder size.
            InjCylEquivSizeIN = (NumCylinders / 2.0f) * Me.ToIn(CylinderDiameterM);

            // Based on equiv cyl size determine correct size injector
            if (InjCylEquivSizeIN <= 19.0)
            {
                InjectorFlowRateLBpS = pS.FrompM(Injector09FlowratePSItoUKGpM[BoilerPressurePSI]) * WaterLBpUKG; // 9mm Injector Flow rate 
                InjectorSize = 09.0f; // store size for display in HUD
            }
            else if (InjCylEquivSizeIN <= 24.0)
            {
                InjectorFlowRateLBpS = pS.FrompM(Injector10FlowratePSItoUKGpM[BoilerPressurePSI]) * WaterLBpUKG; // 10 mm Injector Flow rate 
                InjectorSize = 10.0f; // store size for display in HUD                
            }
            else if (InjCylEquivSizeIN <= 26.0)
            {
                InjectorFlowRateLBpS = pS.FrompM(Injector11FlowratePSItoUKGpM[BoilerPressurePSI]) * WaterLBpUKG; // 11 mm Injector Flow rate 
                InjectorSize = 11.0f; // store size for display in HUD                
            }
            else if (InjCylEquivSizeIN <= 28.0)
            {
                InjectorFlowRateLBpS = pS.FrompM(Injector13FlowratePSItoUKGpM[BoilerPressurePSI]) * WaterLBpUKG; // 13 mm Injector Flow rate 
                InjectorSize = 13.0f; // store size for display in HUD                
            }
            else if (InjCylEquivSizeIN <= 30.0)
            {
                InjectorFlowRateLBpS = pS.FrompM(Injector14FlowratePSItoUKGpM[BoilerPressurePSI]) * WaterLBpUKG; // 14 mm Injector Flow rate 
                InjectorSize = 14.0f; // store size for display in HUD                
            }
            else
            {
                InjectorFlowRateLBpS = pS.FrompM(Injector15FlowratePSItoUKGpM[BoilerPressurePSI]) * WaterLBpUKG; // 15 mm Injector Flow rate 
                InjectorSize = 15.0f; // store size for display in HUD                
            }
            if (WaterIsExhausted)
            {
                InjectorFlowRateLBpS = 0.0f; // If the tender water is empty, stop flow into boiler
            }

            InjectorBoilerInputLB = 0; // Used by UpdateTender() later in the cycle
            if (WaterIsExhausted)
            {
                // don't fill boiler with injectors
            }
            else
            {
                // Injectors to fill boiler   
                if (Injector1IsOn)
                {
                    // Calculate Injector 1 delivery water temp
                    if (Injector1Fraction < InjCapMinFactorX[BoilerPressurePSI])
                    {
                        Injector1WaterDelTempF = InjDelWaterTempMinPressureFtoPSI[BoilerPressurePSI]; // set water delivery temp to minimum value
                    }
                    else
                    {
                        Injector1TempFraction = (Injector1Fraction - InjCapMinFactorX[BoilerPressurePSI]) / (1 - InjCapMinFactorX[MaxBoilerPressurePSI]); // Find the fraction above minimum value
                        Injector1WaterDelTempF = InjDelWaterTempMinPressureFtoPSI[BoilerPressurePSI] - ((InjDelWaterTempMinPressureFtoPSI[BoilerPressurePSI] - InjDelWaterTempMaxPressureFtoPSI[BoilerPressurePSI]) * Injector1TempFraction);
                        Injector1WaterDelTempF = MathHelper.Clamp(Injector1WaterDelTempF, 65.0f, 500.0f);
                    }

                    Injector1WaterTempPressurePSI = WaterTempFtoPSI[Injector1WaterDelTempF]; // calculate the pressure of the delivery water

                    // Calculate amount of steam used to inject water
                    MaxInject1SteamUsedLbpS = InjWaterFedSteamPressureFtoPSI[BoilerPressurePSI];  // Maximum amount of steam used at boiler pressure
                    ActInject1SteamUsedLbpS = (Injector1Fraction * InjectorFlowRateLBpS) / MaxInject1SteamUsedLbpS; // Lbs of steam injected into boiler to inject water.

                    // Calculate heat loss for steam injection
                    Inject1SteamHeatLossBTU = ActInject1SteamUsedLbpS * (BoilerSteamHeatBTUpLB - WaterHeatPSItoBTUpLB[Injector1WaterTempPressurePSI]); // Calculate heat loss for injection steam, ie steam heat to water delivery temperature

                    // Calculate heat loss for water injected
                    Inject1WaterHeatLossBTU = Injector1Fraction * InjectorFlowRateLBpS * (BoilerWaterHeatBTUpLB - WaterHeatPSItoBTUpLB[Injector1WaterTempPressurePSI]); // Loss of boiler heat due to water injection - loss is the diff between steam and water Heat

                    // calculate Water steam heat based on injector water delivery temp
                    BoilerMassLB += elapsedClockSeconds * Injector1Fraction * InjectorFlowRateLBpS;   // Boiler Mass increase by Injector 1
                    BoilerHeatBTU -= elapsedClockSeconds * (Inject1WaterHeatLossBTU + Inject1SteamHeatLossBTU); // Total loss of boiler heat due to water injection - inject steam and water Heat   
                    InjectorBoilerInputLB += (elapsedClockSeconds * Injector1Fraction * InjectorFlowRateLBpS); // Keep track of water flow into boilers from Injector 1
                    BoilerHeatOutBTUpS += (Inject1WaterHeatLossBTU + Inject1SteamHeatLossBTU); // Total loss of boiler heat due to water injection - inject steam and water Heat
                }
                if (Injector2IsOn)
                {
                    // Calculate Injector 2 delivery water temp
                    if (Injector2Fraction < InjCapMinFactorX[BoilerPressurePSI])
                    {
                        Injector2WaterDelTempF = InjDelWaterTempMinPressureFtoPSI[BoilerPressurePSI]; // set water delivery temp to minimum value
                    }
                    else
                    {
                        Injector2TempFraction = (Injector2Fraction - InjCapMinFactorX[BoilerPressurePSI]) / (1 - InjCapMinFactorX[MaxBoilerPressurePSI]); // Find the fraction above minimum value
                        Injector2WaterDelTempF = InjDelWaterTempMinPressureFtoPSI[BoilerPressurePSI] - ((InjDelWaterTempMinPressureFtoPSI[BoilerPressurePSI] - InjDelWaterTempMaxPressureFtoPSI[BoilerPressurePSI]) * Injector2TempFraction);
                        Injector2WaterDelTempF = MathHelper.Clamp(Injector2WaterDelTempF, 65.0f, 500.0f);
                    }
                    Injector2WaterTempPressurePSI = WaterTempFtoPSI[Injector2WaterDelTempF]; // calculate the pressure of the delivery water

                    // Calculate amount of steam used to inject water
                    MaxInject2SteamUsedLbpS = InjWaterFedSteamPressureFtoPSI[BoilerPressurePSI];  // Maximum amount of steam used at boiler pressure
                    ActInject2SteamUsedLbpS = (Injector2Fraction * InjectorFlowRateLBpS) / MaxInject2SteamUsedLbpS; // Lbs of steam injected into boiler to inject water.

                    // Calculate heat loss for steam injection
                    Inject2SteamHeatLossBTU = ActInject2SteamUsedLbpS * (BoilerSteamHeatBTUpLB - WaterHeatPSItoBTUpLB[Injector2WaterTempPressurePSI]); // Calculate heat loss for injection steam, ie steam heat to water delivery temperature

                    // Calculate heat loss for water injected
                    Inject2WaterHeatLossBTU = Injector2Fraction * InjectorFlowRateLBpS * (BoilerWaterHeatBTUpLB - WaterHeatPSItoBTUpLB[Injector2WaterTempPressurePSI]); // Loss of boiler heat due to water injection - loss is the diff between steam and water Heat

                    // calculate Water steam heat based on injector water delivery temp
                    BoilerMassLB += elapsedClockSeconds * Injector2Fraction * InjectorFlowRateLBpS;   // Boiler Mass increase by Injector 1
                    BoilerHeatBTU -= elapsedClockSeconds * (Inject2WaterHeatLossBTU + Inject2SteamHeatLossBTU); // Total loss of boiler heat due to water injection - inject steam and water Heat   
                    InjectorBoilerInputLB += (elapsedClockSeconds * Injector2Fraction * InjectorFlowRateLBpS); // Keep track of water flow into boilers from Injector 1
                    BoilerHeatOutBTUpS += (Inject2WaterHeatLossBTU + Inject2SteamHeatLossBTU); // Total loss of boiler heat due to water injection - inject steam and water Heat
                }
            }
        }

        private void UpdateFiring(float absSpeedMpS)
        {
            if (FiringIsManual)
            {
                // Test to see if blower has been manually activiated.
                if (BlowerController.CurrentValue > 0.0f)
                {
                    BlowerIsOn = true;  // turn blower on if being used
                    BlowerSteamUsageLBpS = BlowerSteamUsageFactor * BlowerController.CurrentValue * BoilerPressurePSI;
                    BlowerBurnEffect = ManBlowerMultiplier * BlowerSteamUsageFactor * BlowerController.CurrentValue * BoilerPressurePSI;
                }
                else
                {
                    BlowerIsOn = false;  // turn blower off if not being used
                    BlowerSteamUsageLBpS = 0.0f;
                    BlowerBurnEffect = 0.0f;
                }
                if (Injector1IsOn)
                {
                    Injector1Fraction = Injector1Controller.CurrentValue;
                }
                if (Injector2IsOn)
                {
                    Injector2Fraction = Injector2Controller.CurrentValue;
                }
            }
            else

            #region AI Fireman
            {
                // Injectors
                // Injectors normally not on when stationary?
                // Injector water delivery heat decreases with the capacity of the injectors, therefore cycle injectors on evenly across both.
                if (WaterGlassLevelIN > 7.99)        // turn injectors off if water level in boiler greater then 8.0, to stop cycling
                {
                    Injector1IsOn = false;
                    Injector1Fraction = 0.0f;
                    Injector2IsOn = false;
                    Injector2Fraction = 0.0f;
                }
                else if (WaterGlassLevelIN <= 7.0 & WaterGlassLevelIN > 6.75)  // turn injector 1 on 20% if water level in boiler drops below 7.0
                {
                    Injector1IsOn = true;
                    Injector1Fraction = 0.2f;
                    Injector2IsOn = false;
                    Injector2Fraction = 0.0f;
                }
                else if (WaterGlassLevelIN <= 6.75 & WaterGlassLevelIN > 6.5) 
                {
                    Injector1IsOn = true;
                    Injector1Fraction = 0.2f;
                    Injector2IsOn = true;
                    Injector2Fraction = 0.2f;
                }
                else if (WaterGlassLevelIN <= 6.5 & WaterGlassLevelIN > 6.25)
                {
                    Injector1IsOn = true;
                    Injector1Fraction = 0.4f;
                    Injector2IsOn = true;
                    Injector2Fraction = 0.2f;
                }
                else if (WaterGlassLevelIN <= 6.25 & WaterGlassLevelIN > 6.0)
                {
                    Injector1IsOn = true;
                    Injector1Fraction = 0.4f;
                    Injector2IsOn = true;
                    Injector2Fraction = 0.4f;
                }
                else if (WaterGlassLevelIN <= 6.0 & WaterGlassLevelIN > 5.75)
                {
                    Injector1IsOn = true;
                    Injector1Fraction = 0.6f;
                    Injector2IsOn = true;
                    Injector2Fraction = 0.4f;
                }
                else if (BoilerPressurePSI > (MaxBoilerPressurePSI - 10.0))  // If boiler pressure is not too low then turn on injector 2
                {
                    if (WaterGlassLevelIN <= 5.75 & WaterGlassLevelIN > 5.5)
                    {
                        Injector1IsOn = true;
                        Injector1Fraction = 0.6f;
                        Injector2IsOn = true;
                        Injector2Fraction = 0.6f;
                    }
                    else if (WaterGlassLevelIN <= 5.5 & WaterGlassLevelIN > 5.25)
                    {
                        Injector1IsOn = true;
                        Injector1Fraction = 0.8f;
                        Injector2IsOn = true;
                        Injector2Fraction = 0.6f;
                    }
                    else if (WaterGlassLevelIN <= 5.25 & WaterGlassLevelIN > 5.0)
                    {
                        Injector1IsOn = true;
                        Injector1Fraction = 0.8f;
                        Injector2IsOn = true;
                        Injector2Fraction = 0.8f;
                    }
                    else if (WaterGlassLevelIN <= 5.0 & WaterGlassLevelIN > 4.75)
                    {
                        Injector1IsOn = true;
                        Injector1Fraction = 1.0f;
                        Injector2IsOn = true;
                        Injector2Fraction = 0.8f;
                    }
                    else if (WaterGlassLevelIN <= 4.75 & WaterGlassLevelIN > 4.5)
                    {
                        Injector1IsOn = true;
                        Injector1Fraction = 1.0f;
                        Injector2IsOn = true;
                        Injector2Fraction = 1.0f;
                    }
                }

                // Put sound triggers in for the injectors in AI Fireman mode
                SignalEvent(Injector1IsOn ? Event.SteamEjector1On : Event.SteamEjector1Off); // hook for sound trigger
                SignalEvent(Injector2IsOn ? Event.SteamEjector2On : Event.SteamEjector2Off); // hook for sound trigger

            }
            #endregion

            // Damper - need to be calculated in AI fireman case too, to determine smoke color
            if (absSpeedMpS < 1.0f)    // locomotive is stationary then damper will have no effect
            {
                DamperBurnEffect = 0.0f;
            }
            else
            {
                DamperBurnEffect = DamperController.CurrentValue * absSpeedMpS * DamperFactorManual; // Damper value for manual firing - related to damper setting and increased speed
            }
            DamperBurnEffect = MathHelper.Clamp(DamperBurnEffect, 0.0f, TheoreticalMaxSteamOutputLBpS); // set damper maximum to the max generation rate
            
            // Determine Heat Ratio - for calculating burn rate

            if (BoilerHeat)
            {
                if (EvaporationLBpS > TotalSteamUsageLBpS)
                {
                    HeatRatio = MathHelper.Clamp(((BoilerHeatOutBTUpS / BoilerHeatInBTUpS) * (TotalSteamUsageLBpS / EvaporationLBpS)), 0.1f, 1.0f);  // Factor to determine how hard to drive burn rate, based on steam gen & usage rate, in AI mode only, clamp < 1 if max boiler heat reached.
                }
                else
                {
                    HeatRatio = MathHelper.Clamp(((BoilerHeatOutBTUpS / BoilerHeatInBTUpS) * (EvaporationLBpS / TotalSteamUsageLBpS)), 0.1f, 1.0f);  // Factor to determine how hard to drive burn rate, based on steam gen & usage rate, in AI mode only, clamp < 1 if max boiler heat reached.
                }
            }
            else
            {
                HeatRatio = MathHelper.Clamp((BoilerHeatOutBTUpS / BoilerHeatInBTUpS), 0.1f, 1.3f);  // Factor to determine how hard to drive burn rate, based on steam gen & usage rate, in AI mode only
            }
        }

    // +++++++++++++++ Main Simulation - End +++++++++++++++++++++

        public override float GetDataOf(CabViewControl cvc)
        {
            float data;

            switch (cvc.ControlType)
            {
                case CABViewControlTypes.WHISTLE:
                    data = Horn ? 1 : 0;
                    break;
                case CABViewControlTypes.REGULATOR:
                    data = ThrottlePercent / 100f;
                    break;
                case CABViewControlTypes.BOILER_WATER:
                    data = WaterFraction;
                    break;
                case CABViewControlTypes.TENDER_WATER:
                    data = TenderWaterVolumeUKG; // Looks like default locomotives need an absolute UK gallons value
                    break;
                case CABViewControlTypes.STEAM_PR:
                    data = ConvertFromPSI(cvc, BoilerPressurePSI);
                    break;
                case CABViewControlTypes.STEAMCHEST_PR:
                    data = ConvertFromPSI(cvc, SteamChestPressurePSI);
                    break;
                case CABViewControlTypes.CUTOFF:
                case CABViewControlTypes.REVERSER_PLATE:
                    data = Train.MUReverserPercent / 100f;
                    break;
                case CABViewControlTypes.CYL_COCKS:
                    data = CylinderCocksAreOpen ? 1 : 0;
                    break;
                case CABViewControlTypes.BLOWER:
                    data = BlowerController.CurrentValue;
                    break;
                case CABViewControlTypes.DAMPERS_FRONT:
                    data = DamperController.CurrentValue;
                    break;
                case CABViewControlTypes.FIREBOX:
                    data = FireMassKG / MaxFireMassKG;
                    break;
                case CABViewControlTypes.FIREHOLE:
                    data = FireboxDoorController.CurrentValue;
                    break;
                case CABViewControlTypes.WATER_INJECTOR1:
                    data = Injector1Controller.CurrentValue;
                    break;
                case CABViewControlTypes.WATER_INJECTOR2:
                    data = Injector2Controller.CurrentValue;
                    break;
                case CABViewControlTypes.STEAM_INJ1:
                    data = Injector1IsOn ? 1 : 0;
                    break;
                case CABViewControlTypes.STEAM_INJ2:
                    data = Injector2IsOn ? 1 : 0;
                    break;
                default:
                    data = base.GetDataOf(cvc);
                    break;
            }
            return data;
        }

        public override string GetStatus()
        {
            var boilerPressurePercent = BoilerPressurePSI / MaxBoilerPressurePSI;
            var boilerPressureSafety = boilerPressurePercent <= 0.25 ? "!!!" : boilerPressurePercent <= 0.5 ? "???" : "";
            var waterGlassPercent = (WaterFraction - WaterMinLevel) / (WaterMaxLevel - WaterMinLevel);
            var boilerWaterSafety = WaterFraction < WaterMinLevel || WaterFraction > WaterMaxLevel ? "!!!" : WaterFraction < WaterMinLevelSafe || WaterFraction > WaterMaxLevelSafe ? "???" : "";
            var coalPercent = TenderCoalMassKG / MaxTenderCoalMassKG;
            var waterPercent = TenderWaterVolumeUKG / (Kg.ToLb(MaxTenderWaterMassKG) / WaterLBpUKG);
            var fuelSafety = CoalIsExhausted || WaterIsExhausted ? "!!!" : coalPercent <= 0.105 || waterPercent <= 0.105 ? "???" : "";
            var status = new StringBuilder();

            if (IsFixGeared)
                status.AppendFormat("{0} = 1 ({1:F2})\n", Viewer.Catalog.GetString("Fixed gear"), SteamGearRatio);
            else if (IsSelectGeared)
                status.AppendFormat("{0} = {2} ({1:F2})\n", Viewer.Catalog.GetString("Gear"),
                    SteamGearRatio, SteamGearPosition == 0 ? Viewer.Catalog.GetParticularString("Gear", "N") : SteamGearPosition.ToString());

            status.AppendFormat("{0} = {1}/{2}\n", Viewer.Catalog.GetString("Steam usage"), FormatStrings.FormatMass(pS.TopH(Kg.FromLb(PreviousTotalSteamUsageLBpS)), PressureUnit != PressureUnit.PSI), FormatStrings.h);
            status.AppendFormat("{0}{2} = {1}{2}\n", Viewer.Catalog.GetString("Boiler pressure"), FormatStrings.FormatPressure(BoilerPressurePSI, PressureUnit.PSI, PressureUnit, true), boilerPressureSafety);
            status.AppendFormat("{0}{2} = {1:F0}% {3}{2}\n", Viewer.Catalog.GetString("Boiler water level"), 100 * waterGlassPercent, boilerWaterSafety, FiringIsManual ? Viewer.Catalog.GetString("(safe range)") : "");

            if (FiringIsManual)
            {
                status.AppendFormat("{0}{3} = {2:F0}% {1}{3}\n", Viewer.Catalog.GetString("Boiler water level"), Viewer.Catalog.GetString("(absolute)"), WaterFraction * 100, boilerWaterSafety);
                if (IdealFireMassKG > 0)
                    status.AppendFormat("{0} = {1:F0}%\n", Viewer.Catalog.GetString("Fire mass"), FireMassKG / IdealFireMassKG * 100);
                else
                    status.AppendFormat("{0} = {1:F0}%\n", Viewer.Catalog.GetString("Fire ratio"), FireRatio * 100);
            }

            status.AppendFormat("{0}{5} = {3:F0}% {1}, {4:F0}% {2}{5}\n", Viewer.Catalog.GetString("Fuel levels"), Viewer.Catalog.GetString("coal"), Viewer.Catalog.GetString("water"), 100 * coalPercent, 100 * waterPercent, fuelSafety);

            return status.ToString();
        }

        public override string GetDebugStatus()
        {
            var status = new StringBuilder(base.GetDebugStatus());

            status.AppendFormat("\n\n\t\t === Key Inputs === \t\t{0:N0} lb/h\n",
            pS.TopH(EvaporationLBpS));

            status.AppendFormat("Input:\tEvap\t{0:N0} ft^2\tGrate\t{1:N0} ft^2\tBoil.\t{2:N0} ft^3\tSup\t{3:N0} ft^2\tFuel Cal.\t{4:N0} btu/lb\n",
                Me2.ToFt2(EvaporationAreaM2),
                Me2.ToFt2(GrateAreaM2),
                BoilerVolumeFT3,
                Me2.ToFt2(SuperheatAreaM2),
                KJpKg.ToBTUpLb(FuelCalorificKJpKG));

            status.AppendFormat("Adj:\tCyl Eff\t{0:N1}\tCyl Exh\t{1:N2}\tPort Open\t{2:N2}\n",
                CylinderEfficiencyRate,
                CylinderExhaustOpenFactor,
                CylinderPortOpeningFactor);

            status.AppendFormat("\n\t\t === Steam Production === \t\t{0:N0} lb/h\n",
            pS.TopH(EvaporationLBpS));

            status.AppendFormat("Boiler:\tPower\t{0:N0} bhp\tMass\t{1:N0} lb\tOut.\t{2:N0} lb/h\t\tBoiler Eff\t{3:N2}\n",
                BoilerKW * BoilerKWtoBHP,
                BoilerMassLB,
                MaxBoilerOutputLBpH,
                BoilerEfficiencyGrateAreaLBpFT2toX[(pS.TopH(Kg.ToLb(FuelBurnRateKGpS)) / Me2.ToFt2(GrateAreaM2))]);

            status.AppendFormat("Heat:\tIn\t{0:N0} btu\tOut\t{1:N0} btu\tSteam\t{2:N0} btu/lb\t\tWater\t{3:N0} btu/lb\tand\t{4:N0} btu/ft^3\t\tHeat\t{5:N0} btu\t\tMax\t{6:N0} btu\n",
                BoilerHeatInBTUpS,
                PreviousBoilerHeatOutBTUpS,
                BoilerSteamHeatBTUpLB,
                BoilerWaterHeatBTUpLB,
                WaterHeatBTUpFT3,
                BoilerHeatSmoothBTU.Value,
                MaxBoilerHeatBTU);

            status.AppendFormat("Temp.:\tFlue\t{0:N0} F\tWater\t{1:N0} F\tS Ratio\t{2:N2}\t\tMaxSuper\t{3:N0} F\t\tCurSuper\t{4:N0} F\tSup Fact\t{5:N2}\n",
                C.ToF(C.FromK(FlueTempK)),
                C.ToF(C.FromK(BoilerWaterTempK)),
                SuperheatVolumeRatio,
                SuperheatRefTempF,
                CurrentSuperheatTeampF,
                SuperheaterSteamUsageFactor);

            status.AppendFormat("\n\t\t === Steam Usage === \t\t{0:N0} lb/h\n",
                pS.TopH(PreviousTotalSteamUsageLBpS));

            status.AppendFormat("Usage.:\tCyl.\t{0:N0} lb/h\tBlower\t{1:N0} lb/h\tRad.\t{2:N0} lb/h\tComp.\t{3:N0} lb/h\tSafety\t{4:N0} lb/h\tCock\t{5:N0} lb/h\tGen.\t{6:N0} lb/h\tStoke\t{7:N0} lb/h\n",
            pS.TopH(CylinderSteamUsageLBpS),
            pS.TopH(BlowerSteamUsageLBpS),
            pS.TopH(RadiationSteamLossLBpS),
            pS.TopH(CompSteamUsageLBpS),
            pS.TopH(SafetyValveUsageLBpS),
            pS.TopH(CylCockSteamUsageLBpS),
            pS.TopH(GeneratorSteamUsageLBpS),
            pS.TopH(StokerSteamUsageLBpS));

            status.AppendFormat("Press.:\tChest\t{0:N0} psi\tInit\t{1:N0} apsi\tCutoff\t{2:N0} apsi\tExhaust\t{3:N0} apsi\tBack\t{4:N0} apsi\tPreComp\t{5:N0} apsi\tPreAdm\t{6:N0} apsi\tMEP\t{7:N0} apsi\tMax Safe\t{8:N0} lb/h ({9} x {10:N1})\n",
            SteamChestPressurePSI,
            InitialPressureAtmPSI,
            CutoffPressureAtmPSI,
            CylinderExhaustPressureAtmPSI,
            BackPressureAtmPSI,
            CylinderPreCompressionPressureAtmPSI,
            CylinderPreAdmissionPressureAtmPSI,
            MeanEffectivePressurePSI,
            pS.TopH(MaxSafetyValveDischargeLbspS),
            NumSafetyValves,
            SafetyValveSizeIn);

           status.AppendFormat("Status.:\tSafety\t{0}\tPlug\t{1}\tPrime\t{2}\tBoil. Heat\t{3}\tSuper\t{4}\tGear\t{5}\n",
                SafetyIsOn,
                FusiblePlugIsBlown,
                BoilerIsPriming,
                BoilerHeat,
                HasSuperheater,
                IsGearedSteamLoco);

            status.AppendFormat("\n\t\t === Fireman === \n");
            status.AppendFormat("Fire:\tIdeal\t{0:N0} lb\t\tFire\t{1:N0} lb\t\tMax Fire\t{2:N0} lb/h\t\tFuel\t{3:N0} lb/h\t\tBurn\t{4:N0} lb/h\t\tComb\t{5:N1} lbs/ft2\n",
                Kg.ToLb(IdealFireMassKG),
                Kg.ToLb(FireMassKG),
                pS.TopH(Kg.ToLb(DisplayMaxFiringRateKGpS)),
                pS.TopH(Kg.ToLb(FuelFeedRateKGpS)),
                pS.TopH(Kg.ToLb(FuelBurnRateKGpS)),
                (pS.TopH(GrateCombustionRateLBpFt2)));

            status.AppendFormat("Injector:\tMax\t{0:N0} gal(uk)/h\t\t({1:N0}mm)\tInj. 1\t{2:N0} gal(uk)/h\t\ttemp\t{3:N0} F\t\tInj. 2\t{4:N0} gal(uk)/h\t\ttemp 2\t{5:N0} F\n",
                pS.TopH(InjectorFlowRateLBpS) / WaterLBpUKG,
                InjectorSize,
                Injector1Fraction * pS.TopH(InjectorFlowRateLBpS) / WaterLBpUKG,
                Injector1WaterDelTempF,
                Injector2Fraction * pS.TopH(InjectorFlowRateLBpS) / WaterLBpUKG,
                Injector2WaterDelTempF);

            status.AppendFormat("Tender:\tCoal\t{0:N0} lb\t{1:N0}%\tWater\t{2:N0} gal(uk)\t\t{3:F0}%\n",
                Kg.ToLb(TenderCoalMassKG),
                (TenderCoalMassKG / MaxTenderCoalMassKG) * 100,
                TenderWaterVolumeUKG,
                (TenderWaterVolumeUKG / (Kg.ToLb(MaxTenderWaterMassKG) / WaterLBpUKG)) * 100);

            status.AppendFormat("Status.:\tCoalOut\t{0}\t\tWaterOut\t{1}\tFireOut\t{2}\tStoker\t{3}\tBoost\t{4}\tB Reset\t{5}\n",
                CoalIsExhausted,
                WaterIsExhausted,
                FireIsExhausted,
                StokerIsMechanical,
                FuelBoost,
                FuelBoostReset);

            status.AppendFormat("\n\t\t === Performance === \n");
            status.AppendFormat("Power:\tMaxIHP\t{0:N0} hp\tIHP\t{1:N0} hp\tDHP\t{2:N0} hp\n",
                MaxIndicatedHorsePowerHP,
                IndicatedHorsePowerHP,
                DrawbarHorsePowerHP);

            status.AppendFormat("Force:\tTheo. TE\t{0:N0}\tStart TE\t{1:N0} lbf\tTE\t{2:N0} lbf\tDraw\t{3:N0} lbf\tCritic\t{4:N0} lbf\tCrit Speed {5:N1} mph\n",
                MaxTractiveEffortLbf,
                N.ToLbf(StartTractiveEffortN),
                DisplayTractiveEffortLbsF,
                DrawBarPullLbsF,
                CriticalSpeedTractiveEffortLbf,
                MaxLocoSpeedMpH);

            status.AppendFormat("Move:\tPiston\t{0:N0}ft/m\tSp. Fact.\t{1:N3}\tDrv\t{2:N0} rpm\tMF- Gear {3:N2}\n",
                PistonSpeedFtpM,
                SpeedFactor,
                pS.TopM(DrvWheelRevRpS),
                MotiveForceGearRatio);

 	status.AppendFormat("\n\t\t\t === Experimental - Slip Monitor === \n");
    status.AppendFormat("Slip:\tPiston\t{0:N0}\tTang(c)\t{1:N0}lbf\tTang(t)\t{2:N0}lbf\tStatic\t{3:N0}lbf\tCoeff\t{4:N2}\tSlip\t{5}\tWhWght \t{6:N0} lbs\n",
                PistonForceLbf,
                TangentialCrankWheelForceLbf,
                TangentialWheelTreadForceLbf,
                StaticWheelFrictionForceLbf,
                FrictionCoeff,
                IsLocoSlip,
               WheelWeightLbs);

            return status.ToString();
        }

        // Gear Box

        public void SteamStartGearBoxIncrease()
        {
            if (IsSelectGeared)
            {
                if (throttle == 0)   // only change gears if throttle is at zero
                {
                    if (SteamGearPosition < 2.0f) // Maximum number of gears is two
                    {
                        SteamGearPosition += 1.0f;
                        Simulator.Confirmer.ConfirmWithPerCent(CabControl.GearBox, CabSetting.Increase, SteamGearPosition);
                        if (SteamGearPosition == 0.0)
                        {
                            // Re -initialise the following for the new gear setting - set to zero as in neutral speed
                            MotiveForceGearRatio = 0.0f;
                            MaxLocoSpeedMpH = 0.0f;
                            SteamGearRatio = 0.0f;
                            MaxTractiveEffortLbf = 0.0f;
                            MaxIndicatedHorsePowerHP = 0.0f;

                        }
                        else if (SteamGearPosition == 1.0)
                        {
                            // Re -initialise the following for the new gear setting
                            MotiveForceGearRatio = SteamGearRatioLow;
                            MaxLocoSpeedMpH = MpS.ToMpH(LowMaxGearedSpeedMpS);
                            SteamGearRatio = SteamGearRatioLow;

                            MaxTractiveEffortLbf = (NumCylinders / 2.0f) * (Me.ToIn(CylinderDiameterM) * Me.ToIn(CylinderDiameterM) * Me.ToIn(CylinderStrokeM) / (2 * Me.ToIn(DriverWheelRadiusM))) * MaxBoilerPressurePSI * TractiveEffortFactor * MotiveForceGearRatio;

                            // Max IHP = (Max TE x Speed) / 375.0, use a factor of 0.85 to calculate max TE
                            MaxIndicatedHorsePowerHP = SpeedFactor * (MaxTractiveEffortLbf * MaxLocoSpeedMpH) / 375.0f;
                        }
                        else if (SteamGearPosition == 2.0)
                        {
                            // Re -initialise the following for the new gear setting
                            MotiveForceGearRatio = SteamGearRatioHigh;
                            MaxLocoSpeedMpH = MpS.ToMpH(HighMaxGearedSpeedMpS);
                            SteamGearRatio = SteamGearRatioHigh;

                            MaxTractiveEffortLbf = (NumCylinders / 2.0f) * (Me.ToIn(CylinderDiameterM) * Me.ToIn(CylinderDiameterM) * Me.ToIn(CylinderStrokeM) / (2 * Me.ToIn(DriverWheelRadiusM))) * MaxBoilerPressurePSI * TractiveEffortFactor * MotiveForceGearRatio;

                            // Max IHP = (Max TE x Speed) / 375.0, use a factor of 0.85 to calculate max TE
                            MaxIndicatedHorsePowerHP = SpeedFactor * (MaxTractiveEffortLbf * MaxLocoSpeedMpH) / 375.0f;
                        }
                    }
                }
                else
                {
                    Simulator.Confirmer.Message(ConfirmLevel.Warning, Viewer3D.Viewer.Catalog.GetString("Gears can't be changed unless throttle is at zero."));

                }
            }
        }

        public void SteamStopGearBoxIncrease()
        {
           
        }

        public void SteamStartGearBoxDecrease()
        {
            if (IsSelectGeared)
            {
                if (throttle == 0)  // only change gears if throttle is at zero
                {
                    if (SteamGearPosition > 0.0f) // Gear number can't go below zero
                    {
                        SteamGearPosition -= 1.0f;
                        Simulator.Confirmer.ConfirmWithPerCent(CabControl.GearBox, CabSetting.Increase, SteamGearPosition);
                        if (SteamGearPosition == 1.0)
                        {

                            // Re -initialise the following for the new gear setting
                            MotiveForceGearRatio = SteamGearRatioLow;
                            MaxLocoSpeedMpH = MpS.ToMpH(LowMaxGearedSpeedMpS);
                            SteamGearRatio = SteamGearRatioLow;
                            MaxTractiveEffortLbf = (NumCylinders / 2.0f) * (Me.ToIn(CylinderDiameterM) * Me.ToIn(CylinderDiameterM) * Me.ToIn(CylinderStrokeM) / (2 * Me.ToIn(DriverWheelRadiusM))) * MaxBoilerPressurePSI * TractiveEffortFactor * MotiveForceGearRatio;

                            // Max IHP = (Max TE x Speed) / 375.0, use a factor of 0.85 to calculate max TE
                            MaxIndicatedHorsePowerHP = SpeedFactor * (MaxTractiveEffortLbf * MaxLocoSpeedMpH) / 375.0f;

                        }
                        else if (SteamGearPosition == 0.0)
                        {
                            // Re -initialise the following for the new gear setting - set to zero as in neutral speed
                            MotiveForceGearRatio = 0.0f;
                            MaxLocoSpeedMpH = 0.0f;
                            SteamGearRatio = 0.0f;
                            MaxTractiveEffortLbf = 0.0f;
                            MaxIndicatedHorsePowerHP = 0.0f;
                        }
                    }
                }
                else
                {
                    Simulator.Confirmer.Message(ConfirmLevel.Warning, Viewer3D.Viewer.Catalog.GetString("Gears can't be changed unless throttle is at zero."));

                }
            }
        }

        public void SteamStopGearBoxDecrease()
        {
            
        }

        //Gear Box
        
        
        public override void StartReverseIncrease( float? target ) {
            CutoffController.StartIncrease( target );
            CutoffController.CommandStartTime = Simulator.ClockTime;
            switch (Direction)
            {
                case Direction.Reverse: Simulator.Confirmer.ConfirmWithPerCent(CabControl.SteamLocomotiveReverser, Math.Abs(Train.MUReverserPercent), CabSetting.Off); break;
                case Direction.N: Simulator.Confirmer.Confirm(CabControl.SteamLocomotiveReverser, CabSetting.Neutral); break;
                case Direction.Forward: Simulator.Confirmer.ConfirmWithPerCent(CabControl.SteamLocomotiveReverser, Math.Abs(Train.MUReverserPercent), CabSetting.On); break;
            }
            SignalEvent(Event.ReverserChange);
        }

        public void StopReverseIncrease() {
            CutoffController.StopIncrease();
            new ContinuousReverserCommand(Simulator.Confirmer.Viewer.Log, true, CutoffController.CurrentValue, CutoffController.CommandStartTime);
        }

        public override void StartReverseDecrease( float? target ) {
            CutoffController.StartDecrease( target );
            CutoffController.CommandStartTime = Simulator.ClockTime;
            switch (Direction)
            {
                case Direction.Reverse: Simulator.Confirmer.ConfirmWithPerCent(CabControl.SteamLocomotiveReverser, Math.Abs(Train.MUReverserPercent), CabSetting.Off); break;
                case Direction.N: Simulator.Confirmer.Confirm(CabControl.SteamLocomotiveReverser, CabSetting.Neutral); break;
                case Direction.Forward: Simulator.Confirmer.ConfirmWithPerCent(CabControl.SteamLocomotiveReverser, Math.Abs(Train.MUReverserPercent), CabSetting.On); break;
            }
            SignalEvent(Event.ReverserChange);
        }

        public void StopReverseDecrease() {
            CutoffController.StopDecrease();
            new ContinuousReverserCommand(Simulator.Confirmer.Viewer.Log, false, CutoffController.CurrentValue, CutoffController.CommandStartTime);
        }

        public void ReverserChangeTo( bool isForward, float? target ) {
            if( isForward ) {
                if( target > CutoffController.CurrentValue ) {
                    StartReverseIncrease( target );
                }
            } else {
                if( target < CutoffController.CurrentValue ) {
                    StartReverseDecrease( target );
                }
            }
        }

        public void SetCutoffValue(float value)
        {
            var controller = CutoffController;
            var oldValue = controller.IntermediateValue;
            var change = controller.SetValue(value);
            if (change != 0)
            {
                new ContinuousReverserCommand(Simulator.Confirmer.Viewer.Log, change > 0, controller.CurrentValue, Simulator.GameTime);
                SignalEvent(Event.ReverserChange);
            }
            if (oldValue != controller.IntermediateValue)
                Simulator.Confirmer.UpdateWithPerCent(CabControl.SteamLocomotiveReverser, oldValue < controller.IntermediateValue ? CabSetting.Increase : CabSetting.Decrease, controller.CurrentValue * 100);
        }

        public void SetCutoffPercent(float percent)
        {
            Train.MUReverserPercent = CutoffController.SetPercent(percent);
            Direction = Train.MUReverserPercent >= 0 ? Direction.Forward : Direction.Reverse;
        }

        public void StartInjector1Increase( float? target ) {
            Injector1Controller.CommandStartTime = Simulator.ClockTime;
            if (IsPlayerTrain) 
                Simulator.Confirmer.ConfirmWithPerCent(CabControl.Injector1, CabSetting.Increase, Injector1Controller.CurrentValue * 100);
            Injector1Controller.StartIncrease( target );
        }

        public void StopInjector1Increase() {
            Injector1Controller.StopIncrease();
            new ContinuousInjectorCommand(Simulator.Confirmer.Viewer.Log, 1, true, Injector1Controller.CurrentValue, Injector1Controller.CommandStartTime);
        }

        public void StartInjector1Decrease( float? target ) {
            if (IsPlayerTrain) 
                Simulator.Confirmer.ConfirmWithPerCent(CabControl.Injector1, CabSetting.Decrease, Injector1Controller.CurrentValue * 100);
            Injector1Controller.StartDecrease( target );
            Injector1Controller.CommandStartTime = Simulator.ClockTime;
        }

        public void StopInjector1Decrease() {
            Injector1Controller.StopDecrease();
            new ContinuousInjectorCommand(Simulator.Confirmer.Viewer.Log, 1, false, Injector1Controller.CurrentValue, Injector1Controller.CommandStartTime);
        }
        
        public void Injector1ChangeTo(bool increase, float? target)
        {
            if (increase)
            {
                if (target > Injector1Controller.CurrentValue)
                {
                    StartInjector1Increase(target);
                }
            }
            else
            {
                if (target < Injector1Controller.CurrentValue)
                {
                    StartInjector1Decrease(target);
                }
            }
        }

        public void SetInjector1Value(float value)
        {
            var controller = Injector1Controller;
            var oldValue = controller.IntermediateValue;
            var change = controller.SetValue(value);
            if (change != 0)
            {
                new ContinuousInjectorCommand(Simulator.Confirmer.Viewer.Log, 1, change > 0, controller.CurrentValue, Simulator.GameTime);
            }
            if (oldValue != controller.IntermediateValue)
                Simulator.Confirmer.UpdateWithPerCent(CabControl.Injector1, oldValue < controller.IntermediateValue ? CabSetting.Increase : CabSetting.Decrease, controller.CurrentValue * 100);
        }

        public void StartInjector2Increase(float? target)
        {
            Injector2Controller.CommandStartTime = Simulator.ClockTime;
            if (IsPlayerTrain) 
                Simulator.Confirmer.ConfirmWithPerCent(CabControl.Injector2, CabSetting.Increase, Injector2Controller.CurrentValue * 100);
            Injector2Controller.StartIncrease( target );
        }

        public void StopInjector2Increase() {
            Injector2Controller.StopIncrease();
            new ContinuousInjectorCommand(Simulator.Confirmer.Viewer.Log, 2, true, Injector2Controller.CurrentValue, Injector2Controller.CommandStartTime);
        }

        public void StartInjector2Decrease( float? target ) {
            Injector2Controller.CommandStartTime = Simulator.ClockTime;
            if (IsPlayerTrain) 
                Simulator.Confirmer.ConfirmWithPerCent(CabControl.Injector2, CabSetting.Decrease, Injector2Controller.CurrentValue * 100);
            Injector2Controller.StartDecrease( target );
        }

        public void StopInjector2Decrease() {
            Injector2Controller.StopDecrease();
            new ContinuousInjectorCommand(Simulator.Confirmer.Viewer.Log, 2, false, Injector2Controller.CurrentValue, Injector2Controller.CommandStartTime);
        }

        public void Injector2ChangeTo( bool increase, float? target ) {
            if( increase ) {
                if( target > Injector2Controller.CurrentValue ) {
                    StartInjector2Increase( target );
                }
            } else {
                if( target < Injector2Controller.CurrentValue ) {
                    StartInjector2Decrease( target );
                }
            }
        }

        public void SetInjector2Value(float value)
        {
            var controller = Injector2Controller;
            var oldValue = controller.IntermediateValue;
            var change = controller.SetValue(value);
            if (change != 0)
            {
                new ContinuousInjectorCommand(Simulator.Confirmer.Viewer.Log, 2, change > 0, controller.CurrentValue, Simulator.GameTime);
            }
            if (oldValue != controller.IntermediateValue)
                Simulator.Confirmer.UpdateWithPerCent(CabControl.Injector2, oldValue < controller.IntermediateValue ? CabSetting.Increase : CabSetting.Decrease, controller.CurrentValue * 100);
        }

        public void StartBlowerIncrease(float? target)
        {
            BlowerController.CommandStartTime = Simulator.ClockTime;
            if (IsPlayerTrain)
                Simulator.Confirmer.ConfirmWithPerCent( CabControl.Blower, CabSetting.Increase, BlowerController.CurrentValue * 100 );
            BlowerController.StartIncrease( target );
            SignalEvent(Event.BlowerChange);
        }
        public void StopBlowerIncrease() {
            BlowerController.StopIncrease();
            if (IsPlayerTrain)
                new ContinuousBlowerCommand(Simulator.Confirmer.Viewer.Log, true, BlowerController.CurrentValue, BlowerController.CommandStartTime);
        }
        public void StartBlowerDecrease( float? target ) {
            BlowerController.CommandStartTime = Simulator.ClockTime;
            if (IsPlayerTrain)
                Simulator.Confirmer.ConfirmWithPerCent( CabControl.Blower, CabSetting.Decrease, BlowerController.CurrentValue * 100 );
            BlowerController.StartDecrease( target );
            SignalEvent(Event.BlowerChange);
        }
        public void StopBlowerDecrease() {
            BlowerController.StopDecrease();
            if (IsPlayerTrain)
                new ContinuousBlowerCommand(Simulator.Confirmer.Viewer.Log, false, BlowerController.CurrentValue, BlowerController.CommandStartTime);
        }

        public void BlowerChangeTo( bool increase, float? target ) {
            if( increase ) {
                if( target > BlowerController.CurrentValue ) {
                    StartBlowerIncrease( target );
                }
            } else {
                if( target < BlowerController.CurrentValue ) {
                    StartBlowerDecrease( target );
                }
            }
        }

        public void SetBlowerValue(float value)
        {
            var controller = BlowerController;
            var oldValue = controller.IntermediateValue;
            var change = controller.SetValue(value);
            if (change != 0)
            {
                new ContinuousBlowerCommand(Simulator.Confirmer.Viewer.Log, change > 0, controller.CurrentValue, Simulator.GameTime);
                SignalEvent(Event.BlowerChange);
            }
            if (oldValue != controller.IntermediateValue)
                Simulator.Confirmer.UpdateWithPerCent(CabControl.Blower, oldValue < controller.IntermediateValue ? CabSetting.Increase : CabSetting.Decrease, controller.CurrentValue * 100);
        }

        public void StartDamperIncrease(float? target)
        {
            DamperController.CommandStartTime = Simulator.ClockTime;
            if (IsPlayerTrain)
                Simulator.Confirmer.ConfirmWithPerCent( CabControl.Damper, CabSetting.Increase, DamperController.CurrentValue * 100 );
            DamperController.StartIncrease( target );
            SignalEvent(Event.DamperChange);
        }
        public void StopDamperIncrease() {
            DamperController.StopIncrease();
            if (IsPlayerTrain) 
                new ContinuousDamperCommand(Simulator.Confirmer.Viewer.Log, true, DamperController.CurrentValue, DamperController.CommandStartTime);
        }
        public void StartDamperDecrease( float? target ) {
            DamperController.CommandStartTime = Simulator.ClockTime;
            if (IsPlayerTrain)
                Simulator.Confirmer.ConfirmWithPerCent( CabControl.Damper, CabSetting.Decrease, DamperController.CurrentValue * 100 );
            DamperController.StartDecrease( target );
            SignalEvent(Event.DamperChange);
        }
        public void StopDamperDecrease() {
            DamperController.StopDecrease();
            if (IsPlayerTrain) 
                new ContinuousDamperCommand(Simulator.Confirmer.Viewer.Log, false, DamperController.CurrentValue, DamperController.CommandStartTime);
        }

        public void DamperChangeTo( bool increase, float? target ) {
            if( increase ) {
                if( target > DamperController.CurrentValue ) {
                    StartDamperIncrease( target );
                }
            } else {
                if( target < DamperController.CurrentValue ) {
                    StartDamperDecrease( target );
                }
            }
        }

        public void SetDamperValue(float value)
        {
            var controller = DamperController;
            var oldValue = controller.IntermediateValue;
            var change = controller.SetValue(value);
            if (change != 0)
            {
                new ContinuousDamperCommand(Simulator.Confirmer.Viewer.Log, change > 0, controller.CurrentValue, Simulator.GameTime);
                SignalEvent(Event.DamperChange);
            }
            if (oldValue != controller.IntermediateValue)
                Simulator.Confirmer.UpdateWithPerCent(CabControl.Damper, oldValue < controller.IntermediateValue ? CabSetting.Increase : CabSetting.Decrease, controller.CurrentValue * 100);
        }

        public void StartFireboxDoorIncrease(float? target)
        {
            FireboxDoorController.CommandStartTime = Simulator.ClockTime;
            if (IsPlayerTrain)
                Simulator.Confirmer.ConfirmWithPerCent(CabControl.FireboxDoor, CabSetting.Increase, FireboxDoorController.CurrentValue * 100);
            FireboxDoorController.StartIncrease(target);
            SignalEvent(Event.FireboxDoorChange);
        }
        public void StopFireboxDoorIncrease()
        {
            FireboxDoorController.StopIncrease();
            if (IsPlayerTrain) 
                new ContinuousFireboxDoorCommand(Simulator.Confirmer.Viewer.Log, true, FireboxDoorController.CurrentValue, FireboxDoorController.CommandStartTime);
        }
        public void StartFireboxDoorDecrease(float? target)
        {
            FireboxDoorController.CommandStartTime = Simulator.ClockTime;
            if (IsPlayerTrain) 
                Simulator.Confirmer.ConfirmWithPerCent(CabControl.FireboxDoor, CabSetting.Decrease, FireboxDoorController.CurrentValue * 100);
            FireboxDoorController.StartDecrease(target);
            SignalEvent(Event.FireboxDoorChange);
        }
        public void StopFireboxDoorDecrease()
        {
            FireboxDoorController.StopDecrease();
            if (IsPlayerTrain) 
                new ContinuousFireboxDoorCommand(Simulator.Confirmer.Viewer.Log, false, FireboxDoorController.CurrentValue, FireboxDoorController.CommandStartTime);
        }

        public void FireboxDoorChangeTo(bool increase, float? target)
        {
            if (increase)
            {
                if (target > FireboxDoorController.CurrentValue)
                {
                    StartFireboxDoorIncrease(target);
                }
            }
            else
            {
                if (target < FireboxDoorController.CurrentValue)
                {
                    StartFireboxDoorDecrease(target);
                }
            }
        }

        public void SetFireboxDoorValue(float value)
        {
            var controller = FireboxDoorController;
            var oldValue = controller.IntermediateValue;
            var change = controller.SetValue(value);
            if (change != 0)
            {
                new ContinuousFireboxDoorCommand(Simulator.Confirmer.Viewer.Log, change > 0, controller.CurrentValue, Simulator.GameTime);
                SignalEvent(Event.FireboxDoorChange);
            }
            if (oldValue != controller.IntermediateValue)
                Simulator.Confirmer.UpdateWithPerCent(CabControl.FireboxDoor, oldValue < controller.IntermediateValue ? CabSetting.Increase : CabSetting.Decrease, controller.CurrentValue * 100);
        }

        public void StartFiringRateIncrease(float? target)
        {
            FiringRateController.CommandStartTime = Simulator.ClockTime;
            if (IsPlayerTrain) 
                Simulator.Confirmer.ConfirmWithPerCent(CabControl.FiringRate, FiringRateController.CurrentValue * 100);
            FiringRateController.StartIncrease( target );
        }
        public void StopFiringRateIncrease() {
            FiringRateController.StopIncrease();
            if (IsPlayerTrain) 
                new ContinuousFiringRateCommand(Simulator.Confirmer.Viewer.Log, true, FiringRateController.CurrentValue, FiringRateController.CommandStartTime);
        }
        public void StartFiringRateDecrease( float? target ) {
            FiringRateController.CommandStartTime = Simulator.ClockTime;
            if (IsPlayerTrain) 
                Simulator.Confirmer.ConfirmWithPerCent(CabControl.FiringRate, FiringRateController.CurrentValue * 100);
            FiringRateController.StartDecrease( target );
        }
        public void StopFiringRateDecrease() {
            FiringRateController.StopDecrease();
            if (IsPlayerTrain) 
                new ContinuousFiringRateCommand(Simulator.Confirmer.Viewer.Log, false, FiringRateController.CurrentValue, FiringRateController.CommandStartTime);
        }

        public void FiringRateChangeTo( bool increase, float? target ) {
            if( increase ) {
                if( target > FiringRateController.CurrentValue ) {
                    StartFiringRateIncrease( target );
                }
            } else {
                if( target < FiringRateController.CurrentValue ) {
                    StartFiringRateDecrease( target );
                }
            }
        }

        public void FireShovelfull()
        {
            FireMassKG+= ShovelMassKG;
            if (IsPlayerTrain) 
                Simulator.Confirmer.Confirm(CabControl.FireShovelfull, CabSetting.On);
            // Make a black puff of smoke
            SmokeColor.Update(1, 0);
        }

        public void ToggleCylinderCocks()
        {
            CylinderCocksAreOpen = !CylinderCocksAreOpen;
            SignalEvent(Event.CylinderCocksToggle);
            if (IsPlayerTrain) 
                Simulator.Confirmer.Confirm(CabControl.CylinderCocks, CylinderCocksAreOpen ? CabSetting.On : CabSetting.Off);
        }

        public void ToggleInjector1()
        {
            if (!FiringIsManual)
                return;
            Injector1IsOn = !Injector1IsOn;
            SignalEvent(Injector1IsOn ? Event.SteamEjector1On : Event.SteamEjector1Off); // hook for sound trigger
            if (IsPlayerTrain)
                Simulator.Confirmer.Confirm(CabControl.Injector1, Injector1IsOn ? CabSetting.On : CabSetting.Off);
        }

        public void ToggleInjector2()
        {
            if (!FiringIsManual)
                return;
            Injector2IsOn = !Injector2IsOn;
            SignalEvent(Injector2IsOn ? Event.SteamEjector2On : Event.SteamEjector2Off); // hook for sound trigger
            if (IsPlayerTrain)
                Simulator.Confirmer.Confirm(CabControl.Injector2, Injector2IsOn ? CabSetting.On : CabSetting.Off);
        }

        public void ToggleManualFiring()
        {
            FiringIsManual = !FiringIsManual;
        }

        /// <summary>
        /// Returns the controller which refills from the matching pickup point.
        /// </summary>
        /// <param name="type">Pickup type</param>
        /// <returns>Matching controller or null</returns>
        public override MSTSNotchController GetRefillController(uint type)
        {
            if (type == (uint)PickupType.FuelCoal) return FuelController;
            if (type == (uint)PickupType.FuelWater) return WaterController;
            return null;
        }

        /// <summary>
        /// Sets coal and water supplies to full immediately.
        /// Provided in case route lacks pickup points for coal and especially water.
        /// </summary>
        public override void RefillImmediately()
        {
            RefillTenderWithCoal();
            RefillTenderWithWater();
        }

        /// <summary>
        /// Returns the fraction of coal or water already in tender.
        /// </summary>
        /// <param name="pickupType">Pickup type</param>
        /// <returns>0.0 to 1.0. If type is unknown, returns 0.0</returns>
        public override float GetFilledFraction(uint pickupType)
        {
            if (pickupType == (uint)PickupType.FuelWater)
            {
                return WaterController.CurrentValue;
            }
            if (pickupType == (uint)PickupType.FuelCoal)
            {
                return FuelController.CurrentValue;
            }
            return 0f;
        }

		public void GetLocoInfo(ref float CC, ref float BC, ref float DC, ref float FC, ref float I1, ref float I2)
		{
			CC = CutoffController.CurrentValue;
			BC = BlowerController.CurrentValue;
			DC = DamperController.CurrentValue;
			FC = FiringRateController.CurrentValue;
			I1 = Injector1Controller.CurrentValue;
			I2 = Injector2Controller.CurrentValue;
		}

		public void SetLocoInfo(float CC, float BC, float DC, float FC, float I1, float I2)
		{
			CutoffController.CurrentValue = CC;
			CutoffController.UpdateValue = 0.0f;
			BlowerController.CurrentValue = BC;
			BlowerController.UpdateValue = 0.0f;
			DamperController.CurrentValue = DC;
			DamperController.UpdateValue = 0.0f;
			FiringRateController.CurrentValue = FC;
			FiringRateController.UpdateValue = 0.0f;
			Injector1Controller.CurrentValue = I1;
			Injector1Controller.UpdateValue = 0.0f;
			Injector2Controller.CurrentValue = I2;
			Injector2Controller.UpdateValue = 0.0f;
		}

        public override void SwitchToPlayerControl()
        {
            if (Train.MUReverserPercent == 100)
            {
                Train.MUReverserPercent = 25;
                if ((Flipped ^ UsingRearCab)) CutoffController.SetValue(-0.25f);
                else CutoffController.SetValue(0.25f);

            }
            else if (Train.MUReverserPercent == -100)
            {
                Train.MUReverserPercent = -25;
                if ((Flipped ^ UsingRearCab)) CutoffController.SetValue(0.25f);
                else CutoffController.SetValue(-0.25f);

            }
            base.SwitchToPlayerControl();
        }

        public override void SwitchToAutopilotControl()
        {
            if (Train.MUDirection == Direction.Forward)
            {
                Train.MUReverserPercent = 100;
            }
            else if (Train.MUDirection == Direction.Reverse)
            {
                Train.MUReverserPercent = -100;
            }
            base.SwitchToAutopilotControl();
        }

    } // class SteamLocomotive
}
