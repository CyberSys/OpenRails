﻿// COPYRIGHT 2013 by the Open Rails project.
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

/* AI
 * 
 * Contains code to initialize and control AI trains.
 * 
 */

// #define DEBUG_REPORTS
// #define DEBUG_CHECKTRAIN
// #define DEBUG_DEADLOCK
// #define DEBUG_EXTRAINFO
// #define DEBUG_TRACEINFO
// DEBUG flag for debug prints

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using MSTS.Formats;
using MSTS.Parsers;
using ORTS.Common;
using ORTS.Viewer3D.Popups;

namespace ORTS
{
    public class AITrain : Train
    {
        public int UiD;
        public AIPath Path;

        public float MaxDecelMpSSP = 1.0f;               // maximum decelleration
        public float MaxAccelMpSSP = 1.0f;               // maximum accelleration
        public float MaxDecelMpSSF = 0.8f;               // maximum decelleration
        public float MaxAccelMpSSF = 0.5f;               // maximum accelleration
        public float MaxDecelMpSS = 0.5f;                // maximum decelleration
        public float MaxAccelMpSS = 1.0f;                // maximum accelleration
        public float Efficiency = 1.0f;                  // train efficiency
        public float LastSpeedMpS;                       // previous speed
        public int Alpha10 = 10;                         // 10*alpha

        public bool PreUpdate;                           // pre update state
        public AIActionItem nextActionInfo;              // no next action
        public float NextStopDistanceM;                  // distance to next stop node
        public int? StartTime;                           // starting time
        public float MaxVelocityA = 30.0f;               // max velocity as set in .con file
        public Service_Definition ServiceDefinition;     // train's service definition in .act file

#if NEW_ACTION
        public List<AIAuxActions> AuxActions;          // Action To Do during activity, like WP
#endif

        public enum AI_MOVEMENT_STATE
        {
            AI_STATIC,
            INIT,
            STOPPED,
            STATION_STOP,
            BRAKING,
            ACCELERATING,
            FOLLOWING,
            RUNNING,
            APPROACHING_END_OF_PATH,
            STOPPED_EXISTING,
            INIT_ACTION,
            HANDLE_ACTION,
            END_ACTION//  SPA: used by new AIActionItem as Auxiliary
        }

        public AI_MOVEMENT_STATE MovementState = AI_MOVEMENT_STATE.INIT;  // actual movement state

        public enum AI_START_MOVEMENT
        {
            SIGNAL_CLEARED,
            SIGNAL_RESTRICTED,
            FOLLOW_TRAIN,
            END_STATION_STOP,
            NEW,
            PATH_ACTION
        }

        public AI AI;

        //  SPA:    Add public in order to be able to get these infos in new AIActionItems
        public static float keepDistanceStatTrainM_P = 10.0f;  // stay 10m behind stationary train (pass in station)
        public static float keepDistanceStatTrainM_F = 50.0f;  // stay 50m behind stationary train (freight or pass outside station)
        public static float followDistanceStatTrainM = 30.0f;  // min dist for starting to follow
        public static float keepDistanceMovingTrainM = 300.0f; // stay 300m behind moving train
        public static float creepSpeedMpS = 2.5f;              // speed for creeping up behind train or upto signal
        public static float maxFollowSpeedMpS = 15.0f;         // max. speed when following
        public static float hysterisMpS = 0.5f;                // speed hysteris value to avoid instability
        public static float clearingDistanceM = 30.0f;         // clear distance to stopping point
        public static float signalApproachDistanceM = 20.0f;   // final approach to signal

#if WITH_PATH_DEBUG
        //  Only for EnhancedActCompatibility
        public string currentAIState = "";
        public string currentAIStation = "";
        int countRequiredAction = 0;
        public AIActionItem savedActionInfo = null;              // no next action

#endif
        //================================================================================================//
        /// <summary>
        /// Constructor
        /// <\summary>

        public AITrain(Simulator simulator, Service_Definition sd, AI ai, AIPath path, float efficiency,
                string name, Traffic_Service_Definition trafficService, float maxVelocityA)
            : base(simulator)
        {
            ServiceDefinition = sd;
            UiD = ServiceDefinition.UiD;
            AI = ai;
            Path = path;
            TrainType = TRAINTYPE.AI_NOTSTARTED;
            StartTime = ServiceDefinition.Time;
            Efficiency = efficiency;
            Name = String.Copy(name);
            TrafficService = trafficService;
            MaxVelocityA = maxVelocityA;
#if NEW_ACTION
            AuxActions = new List<AIAuxActions>();
#endif
        }

        public AITrain(Simulator simulator)
            : base(simulator)
        {
            TrainType = TRAINTYPE.AI_NOTSTARTED;
#if NEW_ACTION
            AuxActions = new List<AIAuxActions>();
#endif

        }

        //================================================================================================//
        /// <summary>
        /// convert route and build station list
        /// <\summary>

        public void CreateRoute()
        {
            if (Path != null)
            {
                SetRoutePath(Path);
            }
            else
            {
                TrackCircuitSection thisSection = signalRef.TrackCircuitList[PresentPosition[1].TCSectionIndex];

                ValidRoute[0] = signalRef.BuildTempRoute(this, thisSection.Index, PresentPosition[1].TCOffset, PresentPosition[1].TCDirection, Length, true, true, false);
            }
        }

        //================================================================================================//
        /// <summary>
        /// convert route and build station list
        /// <\summary>

        public void CreateRoute(bool usePosition)
        {
            if (Path != null && !usePosition)
            {
                SetRoutePath(Path, signalRef);
            }
            else if (Path != null)
            {
                SetRoutePath(Path);
            }
            else if (usePosition)
            {
                TrackCircuitSection thisSection = signalRef.TrackCircuitList[PresentPosition[1].TCSectionIndex];

                ValidRoute[0] = signalRef.BuildTempRoute(this, thisSection.Index, PresentPosition[1].TCOffset, PresentPosition[1].TCDirection, Length, true, true, false);
            }
        }

        //================================================================================================//
        /// <summary>
        /// Restore
        /// <\summary>

        public AITrain(Simulator simulator, BinaryReader inf)
            : base(simulator, inf)
        {
            UiD = inf.ReadInt32();
            MaxDecelMpSS = inf.ReadSingle();
            MaxAccelMpSS = inf.ReadSingle();

            int startTimeValue = inf.ReadInt32();
            if (startTimeValue < 0)
            {
                StartTime = null;
            }
            else
            {
                StartTime = startTimeValue;
            }

            Alpha10 = inf.ReadInt32();

            MovementState = (AI_MOVEMENT_STATE)inf.ReadInt32();

            Efficiency = inf.ReadSingle();
            MaxVelocityA = inf.ReadSingle();
            int serviceListCount = inf.ReadInt32();
            if (serviceListCount > 0) RestoreServiceDefinition(inf, serviceListCount);

            // set signals and actions if train is active train
            bool activeTrain = true;

            if (TrainType == TRAINTYPE.AI_NOTSTARTED) activeTrain = false;
            if (TrainType == TRAINTYPE.AI_AUTOGENERATE) activeTrain = false;

            if (activeTrain)
            {
                if (MovementState == AI_MOVEMENT_STATE.AI_STATIC || MovementState == AI_MOVEMENT_STATE.INIT) activeTrain = false;
            }

            if (activeTrain)
            {
                InitializeSignals(true);
                ResetActions(true);
            }
#if NEW_ACTION
            AuxActions = new List<AIAuxActions>();
#endif

        }

        //================================================================================================//
        //
        // Restore of useful Service Items parameters
        //

        public void RestoreServiceDefinition(BinaryReader inf, int serviceLC )
        {
               ServiceDefinition = new Service_Definition();
           for (int iServiceList = 0; iServiceList < serviceLC; iServiceList++)
            {
                ServiceDefinition.ServiceList.Add(new Service_Item(inf.ReadSingle(), 0, 0.0f, inf.ReadInt32()));
 
            }
        }


        //================================================================================================//
        /// <summary>
        /// Save
        /// <\summary>

        public override void Save(BinaryWriter outf)
        {
            base.Save(outf);
            outf.Write(UiD);
            outf.Write(MaxDecelMpSS);
            outf.Write(MaxAccelMpSS);
            if (StartTime.HasValue)
            {
                outf.Write(StartTime.Value);
            }
            else
            {
                outf.Write(-1);
            }
            outf.Write(Alpha10);

            outf.Write((int)MovementState);
            outf.Write(Efficiency);
            outf.Write(MaxVelocityA);
            if (!Program.Simulator.TimetableMode && ServiceDefinition != null ) ServiceDefinition.Save(outf);
            else outf.Write(-1);
        }

 

        //================================================================================================//
        /// <summary>
        /// Clone
        /// <\summary>

        public AITrain AICopyTrain()
        {
            return ((AITrain)this.MemberwiseClone());
        }

        //================================================================================================//
        /// <summary>
        /// Post Init (override from Train)
        ///           perform all actions required to start
        /// </summary>

        public override bool PostInit()
        {

#if DEBUG_CHECKTRAIN
            if (Number == 2043)
            {
                CheckTrain = true;
            }
#endif
            // if train itself forms other train, check if train is to end at station (only if other train is not autogen and this train has SetStop set)

            if (Forms > 0 && SetStop)
            {
                AITrain nextTrain = AI.StartList.GetNotStartedTrainByNumber(Forms, false);

                if (nextTrain != null && nextTrain.StationStops != null && nextTrain.StationStops.Count > 0)
                {
                    TCSubpathRoute lastSubpath = TCRoute.TCRouteSubpaths[TCRoute.TCRouteSubpaths.Count - 1];
                    int lastSectionIndex = lastSubpath[lastSubpath.Count - 1].TCSectionIndex;

                    nextTrain.StationStops.Sort();
                    if (nextTrain.StationStops[0].PlatformItem.TCSectionIndex.Contains(lastSectionIndex))
                    {
                        StationStops = new List<StationStop>();
                        StationStop newStop = nextTrain.StationStops[0].CreateCopy();
                        newStop.ArrivalTime = StartTime.Value;
                        newStop.DepartTime = StartTime.Value;
                        newStop.arrivalDT = new DateTime((long)(StartTime.Value * Math.Pow(10, 7)));
                        newStop.departureDT = new DateTime((long)(StartTime.Value * Math.Pow(10, 7)));
                        newStop.RouteIndex = lastSubpath.GetRouteIndex(newStop.TCSectionIndex, 0);
                        newStop.SubrouteIndex = TCRoute.TCRouteSubpaths.Count - 1;
                        if (newStop.RouteIndex >= 0) StationStops.Add(newStop); // do not set stop if platform is not on route
                    }
                }
            }

            // check deadlocks

            CheckDeadlock(ValidRoute[0], Number);

            // set initial position and state

            bool atStation = false;
            bool validPosition = InitialTrainPlacement();     // Check track and if clear, set occupied

            if (validPosition)
            {
                if (IsFreight)
                {
                    MaxAccelMpSS = MaxAccelMpSSF;  // set freigth accel and decel
                    MaxDecelMpSS = MaxAccelMpSSF;
                }
                else
                {
                    MaxAccelMpSS = MaxAccelMpSSP;  // set passenger accel and decel
                    MaxDecelMpSS = MaxDecelMpSSP;
                    if (TrainMaxSpeedMpS > 40.0f)
                    {
                        MaxDecelMpSS = 1.5f * MaxDecelMpSSP;  // higher decel for high speed trains
                    }
                    if (TrainMaxSpeedMpS > 55.0f)
                    {
                        MaxDecelMpSS = 2.5f * MaxDecelMpSSP;  // higher decel for very high speed trains
                    }
                }

                BuildWaitingPointList(clearingDistanceM);
                BuildStationList(clearingDistanceM);

                StationStops.Sort();
                if (!atStation && StationStops.Count > 0 )
                {
                    if (! Program.Simulator.TimetableMode && Program.Simulator.Settings.EnhancedActCompatibility && MaxVelocityA > 0 &&
                        ServiceDefinition != null && ServiceDefinition.ServiceList.Count > 0)
                    {
                        // <CScomment> gets efficiency from .act file to override TrainMaxSpeedMpS computed from .srv efficiency
                        var sectionEfficiency = ServiceDefinition.ServiceList[0].Efficiency;
                        if (sectionEfficiency > 0)
                            TrainMaxSpeedMpS = Math.Min((float)Simulator.TRK.Tr_RouteFile.SpeedLimit, MaxVelocityA * sectionEfficiency);
                    }
                }

                InitializeSignals(false);           // Get signal information
                TCRoute.SetReversalOffset(Length);  // set reversal information for first subpath
                SetEndOfRouteAction();              // set action to ensure train stops at end of route

                // check if train starts at station stop
#if NEW_ACTION                                                                                                                                                              
                if (AuxActions.Count > 0)
                {
                    SetAuxAction();
                }
#endif
                if (StationStops.Count > 0)
                {
                    atStation = CheckInitialStation();
                }

                if (!atStation)
                {
                    if (StationStops.Count > 0)
                    { 
                               SetNextStationAction();               // set station details
                    }
                    MovementState = AI_MOVEMENT_STATE.INIT;   // start in STOPPED mode to collect info
                }
            }

            if (CheckTrain)
            {
                File.AppendAllText(@"C:\temp\checktrain.txt", "--------\n");
                File.AppendAllText(@"C:\temp\checktrain.txt", "Train : " + Number.ToString() + "\n");
                File.AppendAllText(@"C:\temp\checktrain.txt", "Name  : " + Name + "\n");
                File.AppendAllText(@"C:\temp\checktrain.txt", "Frght : " + IsFreight.ToString() + "\n");
                File.AppendAllText(@"C:\temp\checktrain.txt", "Length: " + Length.ToString() + "\n");
                File.AppendAllText(@"C:\temp\checktrain.txt", "MaxSpd: " + TrainMaxSpeedMpS.ToString() + "\n");
                File.AppendAllText(@"C:\temp\checktrain.txt", "Start : " + StartTime.Value.ToString() + "\n");
                File.AppendAllText(@"C:\temp\checktrain.txt", "State : " + MovementState.ToString() + "\n");
                File.AppendAllText(@"C:\temp\checktrain.txt", "Sttion: " + atStation.ToString() + "\n");
                File.AppendAllText(@"C:\temp\checktrain.txt", "ValPos: " + validPosition.ToString() + "\n");
            }

            return (validPosition);
        }

        //================================================================================================//
        /// <summary>
        /// Start train out of AI train due to 'formed' action
        /// </summary>
        /// <param name="otherTrain"></param>
        /// <returns></returns>

        public bool StartFromAITrain(AITrain otherTrain, int presentTime)
        {
            // set train type
            TrainType = TRAINTYPE.AI;

            // check if new train has route at present position of front of train
            int usedRefPosition = 0;
            int startPositionIndex = TCRoute.TCRouteSubpaths[0].GetRouteIndex(otherTrain.PresentPosition[usedRefPosition].TCSectionIndex, 0);

            // if not found, check for present rear position
            if (startPositionIndex < 0)
            {
                usedRefPosition = 1;
                startPositionIndex = TCRoute.TCRouteSubpaths[0].GetRouteIndex(otherTrain.PresentPosition[usedRefPosition].TCSectionIndex, 0);
            }

            // if not found - train cannot start out of other train as there is no valid route - let train start of its own
            if (startPositionIndex < 0)
            {
                FormedOf = -1;
                FormedOfType = FormCommand.None;
                return (false);
            }

            // copy consist information incl. max speed and type

            if (FormedOfType == FormCommand.TerminationFormed)
            {
                Cars.Clear();
                foreach (TrainCar car in otherTrain.Cars)
                {
                    Cars.Add(car);
                    car.Train = this;
                    car.CarID = this.Name.Split(':')[0];
                }
                IsFreight = otherTrain.IsFreight;
                Length = otherTrain.Length;
                MassKg = otherTrain.MassKg;
                LeadLocomotiveIndex = otherTrain.LeadLocomotiveIndex;
                TrainMaxSpeedMpS = otherTrain.TrainMaxSpeedMpS;

                FrontTDBTraveller = new Traveller(otherTrain.FrontTDBTraveller);
                RearTDBTraveller = new Traveller(otherTrain.RearTDBTraveller);

                // check if train reversal is required
                if (TCRoute.TCRouteSubpaths[0][startPositionIndex].Direction != otherTrain.PresentPosition[usedRefPosition].TCDirection)
                {
                    ReverseFormation(false);

                    // if reversal is required and units must be detached at start : reverse detached units position
                    if (DetachDetails.Count > 0)
                    {
                        for (int iDetach = DetachDetails.Count - 1; iDetach >= 0; iDetach--)
                        {
                            DetachInfo thisDetach = DetachDetails[iDetach];
                            if (thisDetach.DetachPosition == DetachInfo.DetachPositionInfo.atStart)
                            {
                                switch (thisDetach.DetachUnits)
                                {
                                    case DetachInfo.DetachUnitsInfo.allLeadingPower:
                                        thisDetach.DetachUnits = DetachInfo.DetachUnitsInfo.allTrailingPower;
                                        break;

                                    case DetachInfo.DetachUnitsInfo.allTrailingPower:
                                        thisDetach.DetachUnits = DetachInfo.DetachUnitsInfo.allLeadingPower;
                                        break;

                                    case DetachInfo.DetachUnitsInfo.unitsAtEnd:
                                        thisDetach.DetachUnits = DetachInfo.DetachUnitsInfo.unitsAtFront;
                                        break;

                                    case DetachInfo.DetachUnitsInfo.unitsAtFront:
                                        thisDetach.DetachUnits = DetachInfo.DetachUnitsInfo.unitsAtEnd;
                                        break;

                                    default:
                                        break;
                                }
                            }
                        }
                    }
                }
            }
            else if (FormedOfType == FormCommand.TerminationTriggered)
            {
                if (TCRoute.TCRouteSubpaths[0][startPositionIndex].Direction != otherTrain.PresentPosition[usedRefPosition].TCDirection)
                {
                    FrontTDBTraveller = new Traveller(otherTrain.RearTDBTraveller, Traveller.TravellerDirection.Backward);
                    RearTDBTraveller = new Traveller(otherTrain.FrontTDBTraveller, Traveller.TravellerDirection.Backward);
                }
                else
                {
                    FrontTDBTraveller = new Traveller(otherTrain.FrontTDBTraveller);
                    RearTDBTraveller = new Traveller(otherTrain.RearTDBTraveller);
                }
                CalculatePositionOfCars(0);
            }

            // set state
            MovementState = AI_MOVEMENT_STATE.AI_STATIC;
            // if no start time, set to now + 30
            if (!StartTime.HasValue)
                StartTime = presentTime + 30;
            InitialTrainPlacement();

            return (true);
        }

        //================================================================================================//
        /// <summary>
        /// Check initial station
        /// <\summary>

        public bool CheckInitialStation()
        {
            bool atStation = false;

            // get station details

            StationStop thisStation = StationStops[0];
            if (thisStation.SubrouteIndex != TCRoute.activeSubpath)
            {
                return (false);
            }

            if (thisStation.ActualStopType != StationStop.STOPTYPE.STATION_STOP)
            {
                return (false);
            }

            PlatformDetails thisPlatform = thisStation.PlatformItem;

            float platformBeginOffset = thisPlatform.TCOffset[0, thisStation.Direction];
            float platformEndOffset = thisPlatform.TCOffset[1, thisStation.Direction];
            int endSectionIndex = thisStation.Direction == 0 ?
                    thisPlatform.TCSectionIndex[thisPlatform.TCSectionIndex.Count - 1] :
                    thisPlatform.TCSectionIndex[0];
            int endSectionRouteIndex = ValidRoute[0].GetRouteIndex(endSectionIndex, 0);

            int beginSectionIndex = thisStation.Direction == 1 ?
                    thisPlatform.TCSectionIndex[thisPlatform.TCSectionIndex.Count - 1] :
                    thisPlatform.TCSectionIndex[0];
            int beginSectionRouteIndex = ValidRoute[0].GetRouteIndex(beginSectionIndex, 0);

            // check position

            float margin = 0.0f;
            if (AI.PreUpdate)
                margin = 2.0f * clearingDistanceM;  // allow margin in pre-update due to low update rate

            int stationIndex = ValidRoute[0].GetRouteIndex(thisStation.TCSectionIndex, PresentPosition[0].RouteListIndex);

            // if not found from front of train, try from rear of train (front may be beyond platform)
            if (stationIndex < 0)
            {
                stationIndex = ValidRoute[0].GetRouteIndex(thisStation.TCSectionIndex, PresentPosition[1].RouteListIndex);
            }
            if (Program.Simulator.TimetableMode || !Program.Simulator.Settings.EnhancedActCompatibility)
            { 
                // if rear is in platform, station is valid
                if (PresentPosition[1].RouteListIndex == stationIndex)
                {
                    atStation = true;
                }

                // if front is in platform and most of the train is as well, station is valid
                else if (PresentPosition[0].RouteListIndex == stationIndex &&
                        ((thisPlatform.Length - (platformBeginOffset - PresentPosition[0].TCOffset)) > (Length / 2)))
                {
                    atStation = true;
                }

                // if front is beyond platform and rear is not on route or before platform : train spans platform
                else if (PresentPosition[0].RouteListIndex > stationIndex && PresentPosition[1].RouteListIndex < stationIndex)
                {
                    atStation = true;
                }
            }
                // <CSComment> above first and third test don't work well at least in a real case each
            else
            {
                // if rear is in platform, station is valid
                 if (PresentPosition[1].RouteListIndex == stationIndex && PresentPosition[1].TCOffset >= platformBeginOffset)
                 {
                     atStation = true;
                 }
                 // if front is in platform and most of the train is as well, station is valid
                 else if (PresentPosition[0].RouteListIndex == stationIndex &&
                         ((thisPlatform.Length - (platformEndOffset - PresentPosition[0].TCOffset)) > (Length / 2)))
                 {
                     atStation = true;
                 }
                 // if front is beyond platform and rear is not on route or before platform : train spans platform
                 else if ((PresentPosition[0].RouteListIndex > stationIndex || (PresentPosition[0].RouteListIndex == stationIndex && PresentPosition[0].TCOffset >= platformEndOffset))
                     && (PresentPosition[1].RouteListIndex < stationIndex || (PresentPosition[1].RouteListIndex == stationIndex && PresentPosition[1].TCOffset <= platformBeginOffset)))
                 {
                     atStation = true;
                 }

            }

            // At station : set state, create action item

            if (atStation)
            {
                thisStation.ActualArrival = -1;
                thisStation.ActualDepart = -1;
                MovementState = AI_MOVEMENT_STATE.STATION_STOP;

                AIActionItem newAction = new AIActionItem(-10f, 0.0f, 0.0f, 0.0f, null, AIActionItem.AI_ACTION_TYPE.STATION_STOP);
                nextActionInfo = newAction;
                NextStopDistanceM = 0.0f;

#if DEBUG_REPORTS
                File.AppendAllText(@"C:\temp\printproc.txt", "Train " +
                     Number.ToString() + " initial at station " +
                     StationStops[0].PlatformItem.Name + "\n");
#endif
            }

            return (atStation);
        }

        //================================================================================================//
        /// <summary>
        /// Update
        /// Update function for a single AI train.
        /// </summary>

        public void AIUpdate(float elapsedClockSeconds, double clockTime, bool preUpdate)
        {
#if DEBUG_CHECKTRAIN
            if (Number == 2043)
            {
                CheckTrain = true;
            }
#endif
            
            PreUpdate = preUpdate;   // flag for pre-update phase
#if WITH_PATH_DEBUG
            int lastIndex = PreviousPosition[0].RouteListIndex;
            int presentIndex = PresentPosition[0].RouteListIndex;
            if (lastIndex != presentIndex || countRequiredAction != requiredActions.Count)
            {
                countRequiredAction = requiredActions.Count;
            }
            if (nextActionInfo != savedActionInfo)
            {
                savedActionInfo = nextActionInfo;
            }

#endif


            // Check if at stop point and stopped
            //          if ((NextStopDistanceM < actClearance) || (SpeedMpS <= 0 && MovementState == AI_MOVEMENT_STATE.STOPPED))
            if (MovementState == AI_MOVEMENT_STATE.STOPPED || MovementState == AI_MOVEMENT_STATE.STATION_STOP || MovementState == AI_MOVEMENT_STATE.AI_STATIC)
            {
                SpeedMpS = 0;
                foreach (TrainCar car in Cars)
                {
                    car.MotiveForceN = 0;
                    car.TotalForceN = 0;
                    car.SpeedMpS = 0;
                }

                AITrainThrottlePercent = 0;
                AITrainBrakePercent = 100;
            }

            // update position, route clearance and objects

            if (MovementState != AI_MOVEMENT_STATE.AI_STATIC)
            {
                if (!preUpdate)
                {
                    Update(elapsedClockSeconds);
                }
                else
                {
                    AIPreUpdate(elapsedClockSeconds);
                }

                // get through list of objects, determine necesarry actions

                CheckSignalObjects();

                // check if state still matches authority level

                if (MovementState != AI_MOVEMENT_STATE.INIT && ControlMode == TRAIN_CONTROL.AUTO_NODE && EndAuthorityType[0] != END_AUTHORITY.MAX_DISTANCE) // restricted authority
                {
                    CheckRequiredAction();
                }
#if BROL
                if (MovementState != AI_MOVEMENT_STATE.INIT && EndAuthorityType[0] != END_AUTHORITY.MAX_DISTANCE)
                {
                    CheckRequiredAuxAction();
                }

                if (requiredActions.Count == 0)
                    Trace.TraceInformation("No Actions");
#endif
                // check if reversal point reached and not yet activated - but station stop has preference over reversal point
                if ((nextActionInfo == null ||
                     (nextActionInfo.NextAction != AIActionItem.AI_ACTION_TYPE.STATION_STOP && nextActionInfo.NextAction != AIActionItem.AI_ACTION_TYPE.REVERSAL)) &&
                     TCRoute.ReversalInfo[TCRoute.activeSubpath].Valid )
                {
                    int reqSection = TCRoute.ReversalInfo[TCRoute.activeSubpath].SignalUsed ?
                        TCRoute.ReversalInfo[TCRoute.activeSubpath].LastSignalIndex :
                        TCRoute.ReversalInfo[TCRoute.activeSubpath].LastDivergeIndex;

                    if (reqSection >= 0 && PresentPosition[1].RouteListIndex >= reqSection && TCRoute.ReversalInfo[TCRoute.activeSubpath].ReversalActionInserted == false)
                    {
                        float reqDistance = SpeedMpS * SpeedMpS * MaxDecelMpSS;
                        float distanceToReversalPoint = 0;
                        reqDistance = nextActionInfo != null ? Math.Min(nextActionInfo.RequiredDistance, reqDistance) : reqDistance;
                        if (!Simulator.TimetableMode && Simulator.Settings.EnhancedActCompatibility )
                        {
                        distanceToReversalPoint = ComputeDistanceToReversalPoint();
                        // <CSComment: if compatibility flag on, the AI train runs up to the reverse point no matter how far it is from the diverging point.
                         
                            CreateTrainAction(TrainMaxSpeedMpS, 0.0f, distanceToReversalPoint, null, AIActionItem.AI_ACTION_TYPE.REVERSAL);
                            TCRoute.ReversalInfo[TCRoute.activeSubpath].ReversalActionInserted = true;
                        }
                        else
                        { 
                            nextActionInfo = new AIActionItem(reqDistance, 0.0f, 0.0f, PresentPosition[0].DistanceTravelledM, null, AIActionItem.AI_ACTION_TYPE.REVERSAL);
                            MovementState = AI_MOVEMENT_STATE.BRAKING;
                        }
 
                    }
                }
                // check if out of control - if so, remove

                if (ControlMode == TRAIN_CONTROL.OUT_OF_CONTROL)
                {
                    Trace.TraceInformation("Train {0} is removed for out of control, reason : {1}", Number, OutOfControlReason.ToString());
                    RemoveTrain();
                }
            }

            // switch on action depending on state

            int presentTime = Convert.ToInt32(Math.Floor(clockTime));

#if WITH_PATH_DEBUG
            currentAIStation = " ---";
#endif
#if NEW_ACTION
            //if (nextActionInfo != null && nextActionInfo.GetType().IsSubclassOf(typeof(AuxActionItem)))
            //{
            //    MovementState = nextActionInfo.UpdateAuxAction(this, presentTime, elapsedClockSeconds, MovementState);
            //}
            //else  SPA: Don't know yet
#endif
            {
                bool[] stillExist;

                switch (MovementState)
                {
                    case AI_MOVEMENT_STATE.AI_STATIC:
                        UpdateAIStaticState(presentTime);
                        break;
                    case AI_MOVEMENT_STATE.STOPPED:
#if NEW_ACTION
                        if (nextActionInfo != null && nextActionInfo.GetType().IsSubclassOf(typeof(AuxActionItem)))
                        {
                            MovementState = nextActionInfo.ProcessAction(this, presentTime, elapsedClockSeconds, MovementState);
                        }
                        else
#endif
                        {
                            stillExist = ProcessEndOfPath(presentTime);
                            if (stillExist[1]) UpdateStoppedState();
                        }
                        break;
                    case AI_MOVEMENT_STATE.INIT:
                        stillExist = ProcessEndOfPath(presentTime);
                        if (stillExist[1]) UpdateStoppedState();
                        break;
                    case AI_MOVEMENT_STATE.STATION_STOP:
                        UpdateStationState(presentTime);
                        break;
                    case AI_MOVEMENT_STATE.BRAKING:
                        UpdateBrakingState(elapsedClockSeconds, presentTime);
                        break;
                    case AI_MOVEMENT_STATE.APPROACHING_END_OF_PATH:
                        UpdateBrakingState(elapsedClockSeconds, presentTime);
                        break;
                    case AI_MOVEMENT_STATE.ACCELERATING:
                        UpdateAccelState(elapsedClockSeconds);
                        break;
                    case AI_MOVEMENT_STATE.FOLLOWING:
                        UpdateFollowingState(elapsedClockSeconds, presentTime);
                        break;
                    case AI_MOVEMENT_STATE.RUNNING:
                        UpdateRunningState(elapsedClockSeconds);
                        break;
                    case AI_MOVEMENT_STATE.STOPPED_EXISTING:
                        UpdateStoppedState();
                        break;
                        default:
#if NEW_ACTION
                        if(nextActionInfo != null && nextActionInfo.GetType().IsSubclassOf(typeof(AuxActionItem)))
                        {
                            MovementState = nextActionInfo.ProcessAction(this, presentTime, elapsedClockSeconds, MovementState);
                        }
#endif
                        break;

                }
            }
#if WITH_PATH_DEBUG
            //if (Simulator.Settings.EnhancedActCompatibility)
            {
                switch (MovementState)
                {
                    case AI_MOVEMENT_STATE.AI_STATIC:
                        currentAIState = "STATIC";
                        break;
                    case AI_MOVEMENT_STATE.STOPPED:
                        currentAIState = "STOPPED";
                        break;
                    case AI_MOVEMENT_STATE.INIT:
                        currentAIState = "INIT";
                        break;
                    case AI_MOVEMENT_STATE.STATION_STOP:
                        currentAIState = "STATION_STOP";
                        break;
                    case AI_MOVEMENT_STATE.BRAKING:
                        currentAIState = "BRAKING";
                        break;
                    case AI_MOVEMENT_STATE.APPROACHING_END_OF_PATH:
                        currentAIState = "APPROACHING_EOP";
                        break;
                    case AI_MOVEMENT_STATE.ACCELERATING:
                        currentAIState = "ACCELERATING";
                        break;
                    case AI_MOVEMENT_STATE.FOLLOWING:
                        currentAIState = "FOLLOWING";
                        break;
                    case AI_MOVEMENT_STATE.RUNNING:
                        currentAIState = "RUNNING";
                        break;
                    case AI_MOVEMENT_STATE.HANDLE_ACTION:
                        currentAIState = "HANDLE";
                        break;
                }
                if (nextActionInfo != null)
                {
                    switch (nextActionInfo.NextAction)
                    {
                        case AIActionItem.AI_ACTION_TYPE.STATION_STOP:
                            currentAIState = String.Concat(currentAIState, " to STOP");
                            break;
                        case AIActionItem.AI_ACTION_TYPE.SPEED_LIMIT:
                            currentAIState = String.Concat(currentAIState, " to SPEED_LIMIT");
                            break;
                        case AIActionItem.AI_ACTION_TYPE.SPEED_SIGNAL:
                            currentAIState = String.Concat(currentAIState, " to SPEED_SIGNAL");
                            break;
                        case AIActionItem.AI_ACTION_TYPE.SIGNAL_ASPECT_STOP:
                            currentAIState = String.Concat(currentAIState, " to SIGNAL_ASPECT_STOP");
                            break;
                        case AIActionItem.AI_ACTION_TYPE.SIGNAL_ASPECT_RESTRICTED:
                            currentAIState = String.Concat(currentAIState, " to SIGNAL_ASPECT_RESTRICTED");
                            break;
                        case AIActionItem.AI_ACTION_TYPE.END_OF_AUTHORITY:
                            currentAIState = String.Concat(currentAIState, " to END_OF_AUTHORITY");
                            break;
                        case AIActionItem.AI_ACTION_TYPE.TRAIN_AHEAD:
                            currentAIState = String.Concat(currentAIState, " to TRAIN_AHEAD");
                            break;
                        case AIActionItem.AI_ACTION_TYPE.END_OF_ROUTE:
                            currentAIState = String.Concat(currentAIState, " to END_OF_ROUTE");
                            break;
                        case AIActionItem.AI_ACTION_TYPE.REVERSAL:
                            currentAIState = String.Concat(currentAIState, " to REVERSAL");
                            break;
                        case AIActionItem.AI_ACTION_TYPE.AUX_ACTION:
                            currentAIState = String.Concat(currentAIState, " to AUX ACTION ");
                            break;
                        case AIActionItem.AI_ACTION_TYPE.NONE:
                            currentAIState = String.Concat(currentAIState, " to NONE ");
                            break;
                        default:
                            break;
                    }
                }
                else
                {
                    currentAIState = String.Concat(currentAIState, " to ???? ");
                }
                currentAIState = String.Concat(currentAIState, currentAIStation);
            }
#endif
            LastSpeedMpS = SpeedMpS;

            if (CheckTrain)
            {
                File.AppendAllText(@"C:\temp\checktrain.txt", "--------\n");
                File.AppendAllText(@"C:\temp\checktrain.txt",
                       "DistTrv: " + FormatStrings.FormatDistance(DistanceTravelledM, true) + "\n");
                File.AppendAllText(@"C:\temp\checktrain.txt",
                       "PresPos: " + PresentPosition[0].TCSectionIndex.ToString() + " + " +
                                     FormatStrings.FormatDistance(PresentPosition[0].TCOffset, true) + " : " +
                                     PresentPosition[0].RouteListIndex.ToString() + "\n");
                File.AppendAllText(@"C:\temp\checktrain.txt",
                       "Speed  : " + FormatStrings.FormatSpeed(SpeedMpS, true) + "\n");
                File.AppendAllText(@"C:\temp\checktrain.txt",
                               "Thrott : " + AITrainThrottlePercent.ToString() + " ; Brake : " + AITrainBrakePercent.ToString() + "\n");

                File.AppendAllText(@"C:\temp\checktrain.txt",
                       "Control: " + ControlMode.ToString() + "\n");

                if (ControlMode == TRAIN_CONTROL.AUTO_NODE)
                {
                    File.AppendAllText(@"C:\temp\checktrain.txt",
                       "Auth   : " + EndAuthorityType[0].ToString() + "\n");
                    File.AppendAllText(@"C:\temp\checktrain.txt",
                       "AuthDis: " + FormatStrings.FormatDistance(DistanceToEndNodeAuthorityM[0], true) + "\n");
                }

                File.AppendAllText(@"C:\temp\checktrain.txt",
                       "Movm   : " + MovementState.ToString() + "\n");

                if (NextSignalObject[0] != null)
                {
                    File.AppendAllText(@"C:\temp\checktrain.txt",
                       "NextSig: " + NextSignalObject[0].thisRef.ToString() + "\n");
                    File.AppendAllText(@"C:\temp\checktrain.txt",
                       "Section: " + NextSignalObject[0].TCReference.ToString() + "\n");
                }
                else
                {
                    File.AppendAllText(@"C:\temp\checktrain.txt",
                        "NextSig: null\n");
                }

                if (nextActionInfo != null)
                {
                    File.AppendAllText(@"C:\temp\checktrain.txt",
                       "Action : " + nextActionInfo.NextAction.ToString() + "\n");
                    File.AppendAllText(@"C:\temp\checktrain.txt",
                       "ActDist: " + FormatStrings.FormatDistance(nextActionInfo.ActivateDistanceM, true) + "\n");

                    if (nextActionInfo.NextAction == AIActionItem.AI_ACTION_TYPE.SIGNAL_ASPECT_STOP)
                    {
                        File.AppendAllText(@"C:\temp\checktrain.txt",
                           "NextSig: " + nextActionInfo.ActiveItem.ObjectDetails.thisRef.ToString() + "\n");
                        File.AppendAllText(@"C:\temp\checktrain.txt",
                           "Section: " + nextActionInfo.ActiveItem.ObjectDetails.TCReference.ToString() + "\n");
                        File.AppendAllText(@"C:\temp\checktrain.txt",
                           "DistTr : " + FormatStrings.FormatDistance(nextActionInfo.ActiveItem.distance_to_train, true) + "\n");
                    }
                }
                else
                {
                    File.AppendAllText(@"C:\temp\checktrain.txt",
                        "Action : null\n");
                }

                File.AppendAllText(@"C:\temp\checktrain.txt",
                       "StopDst: " + FormatStrings.FormatDistance(NextStopDistanceM, true) + "\n");

                File.AppendAllText(@"C:\temp\checktrain.txt", "\nDeadlock Info\n");
                foreach (KeyValuePair<int, List<Dictionary<int, int>>> thisDeadlock in DeadlockInfo)
                {
                    File.AppendAllText(@"C:\Temp\checktrain.txt", "Section : " + thisDeadlock.Key.ToString() + "\n");
                    foreach (Dictionary<int, int> actDeadlocks in thisDeadlock.Value)
                    {
                        foreach (KeyValuePair<int, int> actDeadlockInfo in actDeadlocks)
                        {
                            File.AppendAllText(@"C:\Temp\checktrain.txt", "  Other Train : " + actDeadlockInfo.Key.ToString() +
                                " - end Sector : " + actDeadlockInfo.Value.ToString() + "\n");
                        }
                    }
                    File.AppendAllText(@"C:\Temp\checktrain.txt", "\n");
                }
            }
#if WITH_PATH_DEBUG
            lastIndex = PreviousPosition[0].RouteListIndex;
            presentIndex = PresentPosition[0].RouteListIndex;
            if (lastIndex != presentIndex || countRequiredAction != requiredActions.Count)
            {
                countRequiredAction = requiredActions.Count;
            }
#endif

        }

        //================================================================================================//
        /// <summary>
        /// Update for pre-update state
        /// <\summary>

        public void AIPreUpdate(float elapsedClockSeconds)
        {

            // calculate delta speed and speed

            float deltaSpeedMpS = (0.01f * AITrainThrottlePercent * MaxAccelMpSS - 0.01f * AITrainBrakePercent * MaxDecelMpSS) *
                Efficiency * elapsedClockSeconds;
            if (AITrainBrakePercent > 0 && deltaSpeedMpS < 0 && Math.Abs(deltaSpeedMpS) > SpeedMpS)
            {
                deltaSpeedMpS = -SpeedMpS;
            }
            SpeedMpS = Math.Min(TrainMaxSpeedMpS, Math.Max(0.0f, SpeedMpS + deltaSpeedMpS));

            // calculate position

            float distanceM = SpeedMpS * elapsedClockSeconds;

            if (float.IsNaN(distanceM)) distanceM = 0;//sometimes it may become NaN, force it to be 0, so no move

            // force stop
            if (distanceM > NextStopDistanceM)
            {
#if DEBUG_REPORTS
                File.AppendAllText(@"C:\temp\printproc.txt", "Train " +
                    Number.ToString() + " forced stop : calculated " +
                    FormatStrings.FormatSpeed(SpeedMpS, true) + " > " +
                    FormatStrings.FormatDistance(distanceM, true) + " set to " +
                    "0.0 > " + FormatStrings.FormatDistance(NextStopDistanceM, true) + " at " +
                    FormatStrings.FormatDistance(DistanceTravelledM, true) + "\n");
#endif

                if (CheckTrain)
                {
                    File.AppendAllText(@"C:\temp\checktrain.txt", "Train " +
                        Number.ToString() + " forced stop : calculated " +
                        FormatStrings.FormatSpeed(SpeedMpS, true) + " > " +
                        FormatStrings.FormatDistance(distanceM, true) + " set to " +
                        "0.0 > " + FormatStrings.FormatDistance(NextStopDistanceM, true) + " at " +
                        FormatStrings.FormatDistance(DistanceTravelledM, true) + "\n");
                }

                distanceM = Math.Max(0.0f, NextStopDistanceM);
                SpeedMpS = 0;
            }

            // set speed and position

            foreach (TrainCar car in Cars)
            {
                if (car.Flipped)
                {
                    car.SpeedMpS = -SpeedMpS;
                }
                else
                {
                    car.SpeedMpS = SpeedMpS;
                }
            }

            CalculatePositionOfCars(distanceM);

            DistanceTravelledM += distanceM;

            // perform overall update

            if (ValidRoute != null)     // no actions required for static objects //
            {
                movedBackward = CheckBackwardClearance();                                       // check clearance at rear //
                UpdateTrainPosition();                                                          // position update         //              
                UpdateTrainPositionInformation();                                               // position linked info    //
                int SignalObjIndex = CheckSignalPassed(0, PresentPosition[0], PreviousPosition[0]);    // check if passed signal  //
                UpdateSectionState(movedBackward);                                              // update track occupation //
                ObtainRequiredActions(movedBackward);                                           // process Actions         //
                UpdateRouteClearanceAhead(SignalObjIndex, movedBackward, elapsedClockSeconds);  // update route clearance  //
                UpdateSignalState(movedBackward);                                               // update signal state     //
            }
        }

        //================================================================================================//
        /// <summary>
        /// change in authority state - check action
        /// <\summary>

        public void CheckRequiredAction()
        {
            // check if train ahead
            if (EndAuthorityType[0] == END_AUTHORITY.TRAIN_AHEAD)
            {
                if (MovementState != AI_MOVEMENT_STATE.STATION_STOP && MovementState != AI_MOVEMENT_STATE.STOPPED)
                {
                    MovementState = AI_MOVEMENT_STATE.FOLLOWING;  // start following
                }
            }
            else if (EndAuthorityType[0] == END_AUTHORITY.RESERVED_SWITCH || EndAuthorityType[0] == END_AUTHORITY.LOOP)
            {
                ResetActions(true);
                NextStopDistanceM = DistanceToEndNodeAuthorityM[0] - 2.0f * junctionOverlapM;
                CreateTrainAction(SpeedMpS, 0.0f, NextStopDistanceM, null,
                           AIActionItem.AI_ACTION_TYPE.END_OF_AUTHORITY);
            }
            // first handle outstanding actions
            else if (EndAuthorityType[0] == END_AUTHORITY.END_OF_PATH && 
                (nextActionInfo == null || nextActionInfo.NextAction == AIActionItem.AI_ACTION_TYPE.END_OF_ROUTE))
            {
                ResetActions(false);
                if (Simulator.TimetableMode || !Simulator.Settings.EnhancedActCompatibility || TCRoute.activeSubpath < TCRoute.TCRouteSubpaths.Count - 1)
                    NextStopDistanceM = DistanceToEndNodeAuthorityM[0] - clearingDistanceM;
                else NextStopDistanceM = ComputeDistanceToReversalPoint() - clearingDistanceM;
            }
        }

#if NEW_ACTION
        public void CheckRequiredAuxAction()
        {
            //nextActionInfo = null;

            if (AuxActions != null)
            {
                SetAuxAction();
            }
        }
#endif

        //================================================================================================//
        /// <summary>
        /// Check all signal objects
        /// <\summary>

        public void CheckSignalObjects()
        {
            if (CheckTrain)
            {
                File.AppendAllText(@"C:\temp\checktrain.txt", "Check Objects \n");
            }

            float validSpeed = AllowedMaxSpeedMpS;
            List<ObjectItemInfo> processedList = new List<ObjectItemInfo>();

            foreach (ObjectItemInfo thisInfo in SignalObjectItems)
            {

                // check speedlimit
                if (CheckTrain)
                {
                    File.AppendAllText(@"C:\temp\checktrain.txt",
                            "Item : " + thisInfo.ObjectType.ToString() + " at " +
                                        FormatStrings.FormatDistance(thisInfo.distance_to_train, true) +
                        " - processed : " + thisInfo.processed.ToString() + "\n");

                    if (thisInfo.ObjectType == ObjectItemInfo.ObjectItemType.Signal)
                    {
                        File.AppendAllText(@"C:\temp\checktrain.txt",
                                "  Signal State : " + thisInfo.signal_state.ToString() + "\n");
                    }
                }

                float setSpeed = IsFreight ? thisInfo.speed_freight : thisInfo.speed_passenger;
                if (setSpeed < validSpeed && setSpeed < AllowedMaxSpeedMpS && setSpeed > 0)
                {
                    if (!thisInfo.processed)
                    {
                        bool process_req = true;

                        if (ControlMode == TRAIN_CONTROL.AUTO_NODE &&
                                        thisInfo.distance_to_train > DistanceToEndNodeAuthorityM[0])
                        {
                            process_req = false;
                        }
                        else if (thisInfo.distance_to_train > signalApproachDistanceM ||
                                 (MovementState == AI_MOVEMENT_STATE.RUNNING && SpeedMpS > setSpeed) ||
                                  MovementState == AI_MOVEMENT_STATE.ACCELERATING)
                        {
                            process_req = true;
                        }
                        else
                        {
                            process_req = false;
                        }

                        if (process_req)
                        {
                            if (thisInfo.ObjectType == ObjectItemInfo.ObjectItemType.Speedlimit)
                            {
                                CreateTrainAction(validSpeed, setSpeed,
                                        thisInfo.distance_to_train, thisInfo, AIActionItem.AI_ACTION_TYPE.SPEED_LIMIT);
                            }
                            else
                            {
                                CreateTrainAction(validSpeed, setSpeed,
                                        thisInfo.distance_to_train, thisInfo, AIActionItem.AI_ACTION_TYPE.SPEED_SIGNAL);
                            }
                            processedList.Add(thisInfo);
                        }
                    }
                    validSpeed = setSpeed;
                }
                else if (setSpeed > 0)
                {
                    validSpeed = setSpeed;
                }

                // check signal state

                if (thisInfo.ObjectType == ObjectItemInfo.ObjectItemType.Signal &&
                        thisInfo.signal_state < MstsSignalAspect.APPROACH_1 &&
                        !thisInfo.processed)
                {
                    if (CheckTrain)
                    {
                        File.AppendAllText(@"C:\temp\checktrain.txt",
                                "Signal restricted\n");
                    }
                    if (!(ControlMode == TRAIN_CONTROL.AUTO_NODE &&
                                    thisInfo.distance_to_train > (DistanceToEndNodeAuthorityM[0] - clearingDistanceM)))
                    {
                        if (thisInfo.signal_state == MstsSignalAspect.STOP ||
                            thisInfo.ObjectDetails.enabledTrain != routedForward)
                        {
                            CreateTrainAction(validSpeed, 0.0f,
                                    thisInfo.distance_to_train, thisInfo,
                                    AIActionItem.AI_ACTION_TYPE.SIGNAL_ASPECT_STOP);
                            processedList.Add(thisInfo);
                            if (((thisInfo.distance_to_train - clearingDistanceM) < clearingDistanceM) &&
                                         (SpeedMpS > 0.0f || MovementState == AI_MOVEMENT_STATE.ACCELERATING))
                            {
                                AITrainBrakePercent = 100;
                                AITrainThrottlePercent = 0;
                                NextStopDistanceM = clearingDistanceM;
                            }
                        }
                        else if (thisInfo.distance_to_train > 2.0f * signalApproachDistanceM) // set restricted only if not close
                        {
                            CreateTrainAction(validSpeed, 0.0f,
                                    thisInfo.distance_to_train, thisInfo,
                                    AIActionItem.AI_ACTION_TYPE.SIGNAL_ASPECT_RESTRICTED);
                            processedList.Add(thisInfo);
                        }
                    }
                    else if (CheckTrain)
                    {
                        File.AppendAllText(@"C:\temp\checktrain.txt",
                                "Signal not set due to AUTO_NODE state \n");
                    }
                }
            }

            // set processed items - must be collected as item can be processed twice (speed and signal)

            foreach (ObjectItemInfo thisInfo in processedList)
            {
                thisInfo.processed = true;
            }
        }

        //================================================================================================//
        /// <summary>
        /// Check for next station
        /// <\summary>

        public void SetNextStationAction()
        {
            // check if station in this subpath

            int stationIndex = 0;
            StationStop thisStation = StationStops[stationIndex];
            while (thisStation.SubrouteIndex < TCRoute.activeSubpath) // station was in previous subpath
            {
                StationStops.RemoveAt(0);
                if (StationStops.Count == 0) // no more stations
                {
                       return;
                  }
                thisStation = StationStops[0];
            }

            if (thisStation.SubrouteIndex > TCRoute.activeSubpath)    // station is not in this subpath
            {
                return;
            }

            // get distance to station

            bool validStop = false;
            while (!validStop)
            {
                float[] distancesM = CalculateDistancesToNextStation(thisStation, TrainMaxSpeedMpS, false);
                if (distancesM[0] < 0f) // stop is not valid
                {
                    StationStops.RemoveAt(0);
                    if (StationStops.Count == 0)
                    {
                        return;  // no more stations - exit
                    }

                    thisStation = StationStops[0];
                    if (thisStation.SubrouteIndex > TCRoute.activeSubpath) return;  // station not in this subpath - exit
                }
                else
                {
                    validStop = true;
                    AIActionItem newAction = new AIActionItem(distancesM[1], 0.0f, distancesM[0], DistanceTravelledM,
                            null, AIActionItem.AI_ACTION_TYPE.STATION_STOP);

                    requiredActions.InsertAction(newAction);

#if DEBUG_REPORTS
                    if (StationStops[0].ActualStopType == StationStop.STOPTYPE.STATION_STOP)
                    {
                        File.AppendAllText(@"C:\temp\printproc.txt", "Insert for train " +
                            Number.ToString() + ", type STATION_STOP (" +
                            StationStops[0].PlatformItem.Name + "), at " +
                            FormatStrings.FormatDistance(distancesM[0], true) + ", trigger at " +
                            FormatStrings.FormatDistance(distancesM[1], true) + " (now at " +
                            FormatStrings.FormatDistance(PresentPosition[0].DistanceTravelledM, true) + ")\n");
            }
            else if (StationStops[0].ActualStopType == StationStop.STOPTYPE.WAITING_POINT)
            {
                File.AppendAllText(@"C:\temp\printproc.txt", "Insert for train " +
                            Number.ToString() + ", type WAITING_POINT (" +
                            FormatStrings.FormatDistance(distancesM[0], true) + ", trigger at " +
                            FormatStrings.FormatDistance(distancesM[1], true) + " (now at " +
                            FormatStrings.FormatDistance(PresentPosition[0].DistanceTravelledM, true) + ")\n");
                    }
#endif

                    if (CheckTrain)
                    {
                        if (StationStops[0].ActualStopType == StationStop.STOPTYPE.STATION_STOP)
                        {
                            File.AppendAllText(@"C:\temp\checktrain.txt", "Insert for train " +
                                    Number.ToString() + ", type STATION_STOP (" +
                                    StationStops[0].PlatformItem.Name + "), at " +
                                    FormatStrings.FormatDistance(distancesM[0], true) + ", trigger at " +
                                    FormatStrings.FormatDistance(distancesM[1], true) + " (now at " +
                                    FormatStrings.FormatDistance(PresentPosition[0].DistanceTravelledM, true) + ")\n");
                        }
                        else if (StationStops[0].ActualStopType == StationStop.STOPTYPE.WAITING_POINT)
                        {
                            File.AppendAllText(@"C:\temp\checktrain.txt", "Insert for train " +
                                        Number.ToString() + ", type WAITING_POINT (" +
                                        FormatStrings.FormatDistance(distancesM[0], true) + ", trigger at " +
                                        FormatStrings.FormatDistance(distancesM[1], true) + " (now at " +
                                        FormatStrings.FormatDistance(PresentPosition[0].DistanceTravelledM, true) + ")\n");
                        }
                    }
                }
            }
        }

        //================================================================================================//
        /// <summary>
        /// Calculate actual distance and trigger distance for next station
        /// <\summary>

        public float[] CalculateDistancesToNextStation(StationStop thisStation, float presentSpeedMpS, bool reschedule)
        {
            int thisSectionIndex = PresentPosition[0].TCSectionIndex;
            TrackCircuitSection thisSection = signalRef.TrackCircuitList[thisSectionIndex];
            float leftInSectionM = thisSection.Length - PresentPosition[0].TCOffset;

            // get station route index - if not found, return distances < 0

            int stationIndex0 = ValidRoute[0].GetRouteIndex(thisStation.TCSectionIndex, PresentPosition[0].RouteListIndex);
            int stationIndex1 = ValidRoute[0].GetRouteIndex(thisStation.TCSectionIndex, PresentPosition[1].RouteListIndex);

            float distanceToTrainM = -1f;
            if (stationIndex0 >= 0)
            {
                distanceToTrainM = ValidRoute[0].GetDistanceAlongRoute(PresentPosition[0].RouteListIndex,
                    leftInSectionM, stationIndex0, thisStation.StopOffset, true, signalRef);
            }
            // front of train is passed station but rear is not or train is stopped - return present position
            if (distanceToTrainM < 0f && MovementState == AI_MOVEMENT_STATE.STATION_STOP)
            {
                return (new float[2] { PresentPosition[0].DistanceTravelledM, 0.0f });
            }

            // if station not on route at all return negative values
            if (distanceToTrainM < 0f && stationIndex0 < 0 && stationIndex1 < 0)
            {
                return (new float[2] { -1f, -1f });
            }

            // if reschedule, use actual speed

            float activateDistanceTravelledM = PresentPosition[0].DistanceTravelledM + distanceToTrainM;
            float triggerDistanceM = 0.0f;

            if (reschedule)
            {
                float firstPartTime = 0.0f;
                float firstPartRangeM = 0.0f;
                float secndPartRangeM = 0.0f;
                float remainingRangeM = activateDistanceTravelledM - PresentPosition[0].DistanceTravelledM;

                firstPartTime = presentSpeedMpS / (0.25f * MaxDecelMpSS);
                firstPartRangeM = 0.25f * MaxDecelMpSS * (firstPartTime * firstPartTime);

                if (firstPartRangeM < remainingRangeM && SpeedMpS < TrainMaxSpeedMpS) // if distance left and not at max speed
                // split remaining distance based on relation between acceleration and deceleration
                {
                    secndPartRangeM = (remainingRangeM - firstPartRangeM) * (2.0f * MaxDecelMpSS) / (MaxDecelMpSS + MaxAccelMpSS);
                }

                triggerDistanceM = activateDistanceTravelledM - (firstPartRangeM + secndPartRangeM);
            }
            else

            // use maximum speed
            {
                float deltaTime = TrainMaxSpeedMpS / MaxDecelMpSS;
                float brakingDistanceM = (TrainMaxSpeedMpS * deltaTime) + (0.5f * MaxDecelMpSS * deltaTime * deltaTime);
                triggerDistanceM = activateDistanceTravelledM - brakingDistanceM;
            }

            float[] distancesM = new float[2];
            distancesM[0] = activateDistanceTravelledM;
            distancesM[1] = triggerDistanceM;

            return (distancesM);
        }

#if NEW_ACTION
        //================================================================================================//
        //  SPA:    Added for use with new AIActionItems
        /// <summary>
        /// Create Auxiliary Action
        /// <\summary>
        public void SetAuxAction()
        {
            if (AuxActions.Count <= 0)
                return;
            AIAuxActions thisAction = AuxActions[0];

            if (thisAction.SubrouteIndex > TCRoute.activeSubpath)
            {
                return;
            }

            bool validAction = false;
            while (!validAction)
            {
                float[] distancesM = thisAction.CalculateDistancesToNextAction(this, TrainMaxSpeedMpS, true);
                if (distancesM[0] < 0f)
                {
                    AuxActions.RemoveAt(0);
                    if (AuxActions.Count == 0)
                    {
                        return;
                    }

                    thisAction = AuxActions[0];
                    if (thisAction.SubrouteIndex > TCRoute.activeSubpath) return;
                }
                else
                {
                    validAction = true;
                    AIActionItem newAction = AuxActions[0].Handler(distancesM[1], 0.0f, distancesM[0], DistanceTravelledM);

                    requiredActions.InsertAction(newAction);
                }
            }
        }
        
#endif
        //================================================================================================//
        /// <summary>
        /// Override Switch to Signal control
        /// <\summary>

        public override void SwitchToSignalControl(SignalObject thisSignal)
        {
            base.SwitchToSignalControl(thisSignal);
            ResetActions(true);

            // check if any actions must be processed immediately

            ObtainRequiredActions(0);
        }

        //================================================================================================//
        /// <summary>
        /// Override Switch to Node control
        /// <\summary>

        public override void SwitchToNodeControl(int thisSectionIndex)
        {
            base.SwitchToNodeControl(thisSectionIndex);
            ResetActions(true);

            // check if any actions must be processed immediately

            ObtainRequiredActions(0);
        }

        //================================================================================================//
        /// <summary>
        /// Update AI Static state
        /// </summary>
        /// <param name="presentTime"></param>

        private void UpdateAIStaticState(int presentTime)
        {
            // start if start time is reached

            if (StartTime.HasValue && StartTime.Value < presentTime && TrainHasPower())
            {
                foreach (var car in Cars)
                {
                    if (car is MSTSLocomotive)
                    {
                        MSTSLocomotive loco = car as MSTSLocomotive;
                        loco.SetPower(true);
                    }
                }

                PostInit();
                return;
            }

            // check if anything needs be detached
            if (DetachDetails.Count > 0)
            {
                for (int iDetach = DetachDetails.Count - 1; iDetach >= 0; iDetach--)
                {
                    DetachInfo thisDetach = DetachDetails[iDetach];

                    bool validTime = !thisDetach.DetachTime.HasValue || thisDetach.DetachTime.Value < presentTime;
                    if (thisDetach.DetachPosition == DetachInfo.DetachPositionInfo.atStart && validTime)
                    {
                        thisDetach.Detach(this, presentTime);
                        DetachDetails.RemoveAt(iDetach);
                    }
                }
            }

            // switch off power for all engines
            foreach (var car in Cars)
            {
                if (car is MSTSLocomotive)
                {
                    MSTSLocomotive loco = car as MSTSLocomotive;
                    loco.SetPower(false);
                }
            }
        }

        //================================================================================================//
        /// <summary>
        /// Update train in stopped state
        /// <\summary>

        internal AITrain.AI_MOVEMENT_STATE UpdateStoppedState()      //  SPA:    Change private to internal, used by new AIActionItems
        {

            if (SpeedMpS > 0)   // if train still running force it to stop
            {
                SpeedMpS = 0;
                Update(0);   // stop the wheels from moving etc
                AITrainThrottlePercent = 0;
                AITrainBrakePercent = 100;
            }

            // check if train ahead - if so, determine speed and distance

            if (ControlMode == TRAIN_CONTROL.AUTO_NODE &&
                EndAuthorityType[0] == END_AUTHORITY.TRAIN_AHEAD)
            {

                // check if train ahead is in same section
                int sectionIndex = PresentPosition[0].TCSectionIndex;
                int startIndex = ValidRoute[0].GetRouteIndex(sectionIndex, 0);
                int endIndex = ValidRoute[0].GetRouteIndex(LastReservedSection[0], 0);

                TrackCircuitSection thisSection = signalRef.TrackCircuitList[sectionIndex];

                Dictionary<Train, float> trainInfo = thisSection.TestTrainAhead(this,
                                PresentPosition[0].TCOffset, PresentPosition[0].TCDirection);

                // search for train ahead in route sections
                for (int iIndex = startIndex + 1; iIndex <= endIndex && trainInfo.Count <= 0; iIndex++)
                {
                    thisSection = signalRef.TrackCircuitList[ValidRoute[0][iIndex].TCSectionIndex];
                    trainInfo = thisSection.TestTrainAhead(this, 0.0f, ValidRoute[0][iIndex].Direction);
                }

                if (trainInfo.Count <= 0)
                // train is in section beyond last reserved
                {
                    if (endIndex < ValidRoute[0].Count - 1)
                    {
                        thisSection = signalRef.TrackCircuitList[ValidRoute[0][endIndex + 1].TCSectionIndex];

                        trainInfo = thisSection.TestTrainAhead(this, 0.0f, ValidRoute[0][endIndex + 1].Direction);
                    }
                }

                if (trainInfo.Count > 0)  // found train
                {
                    foreach (KeyValuePair<Train, float> trainAhead in trainInfo) // always just one
                    {
                        Train OtherTrain = trainAhead.Key;
                        if (Math.Abs(OtherTrain.SpeedMpS) < 0.001f &&
                                    DistanceToEndNodeAuthorityM[0] > followDistanceStatTrainM)
                        {
                            // allow creeping closer
                            CreateTrainAction(creepSpeedMpS, 0.0f,
                                    DistanceToEndNodeAuthorityM[0], null, AIActionItem.AI_ACTION_TYPE.TRAIN_AHEAD);
                            MovementState = AI_MOVEMENT_STATE.FOLLOWING;
                            StartMoving(AI_START_MOVEMENT.FOLLOW_TRAIN);
                        }

                        else if (Math.Abs(OtherTrain.SpeedMpS) > 0 &&
                            DistanceToEndNodeAuthorityM[0] > keepDistanceMovingTrainM)
                        {
                            // train started moving
                            MovementState = AI_MOVEMENT_STATE.FOLLOWING;
                            StartMoving(AI_START_MOVEMENT.FOLLOW_TRAIN);
                        }
                    }
                }
                // if train not found, do nothing - state will change next update

            }

     // Other node mode : check distance ahead (path may have cleared)

            else if (ControlMode == TRAIN_CONTROL.AUTO_NODE &&
                        DistanceToEndNodeAuthorityM[0] > clearingDistanceM)
            {
                NextStopDistanceM = DistanceToEndNodeAuthorityM[0];
                StartMoving(AI_START_MOVEMENT.SIGNAL_CLEARED);
            }

    // signal node : check state of signal

            else if (ControlMode == TRAIN_CONTROL.AUTO_SIGNAL)
            {
                MstsSignalAspect nextAspect = MstsSignalAspect.UNKNOWN;
                // there is a next item and it is the next signal
                if (nextActionInfo != null && nextActionInfo.ActiveItem != null &&
                    nextActionInfo.ActiveItem.ObjectDetails == NextSignalObject[0])
                {
                    nextAspect = nextActionInfo.ActiveItem.ObjectDetails.this_sig_lr(MstsSignalFunction.NORMAL);
                }
                else
                {
                    nextAspect = GetNextSignalAspect(0);
                }

                if (NextSignalObject[0] == null) // no signal ahead so switch Node control
                {
                    SwitchToNodeControl(PresentPosition[0].TCSectionIndex);
                    NextStopDistanceM = DistanceToEndNodeAuthorityM[0];
                }

                else if (nextAspect > MstsSignalAspect.STOP &&
                        nextAspect < MstsSignalAspect.APPROACH_1)
                {
                    // check if any other signals within clearing distance
                    bool signalCleared = true;
                    bool withinDistance = true;

                    for (int iitem = 0; iitem <= SignalObjectItems.Count - 1 && withinDistance && signalCleared; iitem++)
                    {
                        ObjectItemInfo nextObject = SignalObjectItems[iitem];
                        if (nextObject.ObjectType == ObjectItemInfo.ObjectItemType.Signal)
                        {
                            if (nextObject.ObjectDetails != NextSignalObject[0]) // not signal we are waiting for
                            {
                                if (nextObject.distance_to_train > 2.0 * clearingDistanceM)
                                {
                                    withinDistance = false;  // signal is far enough ahead
                                }
                                else if (nextObject.signal_state == MstsSignalAspect.STOP)
                                {
                                    signalCleared = false;   // signal is not clear
                                    NextSignalObject[0].ForcePropagation = true;
                                }
                            }
                        }
                    }

                    if (signalCleared)
                    {
                        ResetActions(true);
                        NextStopDistanceM = 5000f; // clear to 5000m, will be updated if required
                        StartMoving(AI_START_MOVEMENT.SIGNAL_RESTRICTED);
                    }
                }
                else if (nextAspect >= MstsSignalAspect.APPROACH_1)
                {
                    // check if any other signals within clearing distance
                    bool signalCleared = true;
                    bool withinDistance = true;

                    for (int iitem = 0; iitem <= SignalObjectItems.Count - 1 && withinDistance && signalCleared; iitem++)
                    {
                        ObjectItemInfo nextObject = SignalObjectItems[iitem];
                        if (nextObject.ObjectType == ObjectItemInfo.ObjectItemType.Signal)
                        {
                            if (nextObject.ObjectDetails != NextSignalObject[0]) // not signal we are waiting for
                            {
                                if (nextObject.distance_to_train > 2.0 * clearingDistanceM)
                                {
                                    withinDistance = false;  // signal is far enough ahead
                                }
                                else if (nextObject.signal_state == MstsSignalAspect.STOP)
                                {
                                    signalCleared = false;   // signal is not clear
                                    NextSignalObject[0].ForcePropagation = true;
                                }
                            }
                        }
                    }

                    if (signalCleared)
                    {
                        ResetActions(true);
                        NextStopDistanceM = 5000f; // clear to 5000m, will be updated if required
                        StartMoving(AI_START_MOVEMENT.SIGNAL_CLEARED);
                    }
                }
                //else if (nextActionInfo != null &&
                // nextActionInfo.NextAction == AIActionItem.AI_ACTION_TYPE.STATION_STOP &&
                // StationStops[0].SubrouteIndex == TCRoute.activeSubpath &&
                // ValidRoute[0].GetRouteIndex(StationStops[0].TCSectionIndex, PresentPosition[0].RouteListIndex) <= PresentPosition[0].RouteListIndex)
                //// assume to be in station
                //{
                //    MovementState = AI_MOVEMENT_STATE.STATION_STOP;

                //    if (CheckTrain)
                //    {
                //        File.AppendAllText(@"C:\temp\checktrain.txt", "Train " + Number + " assumed to be in station : " +
                //            StationStops[0].PlatformItem.Name + "( present section = " + PresentPosition[0].TCSectionIndex +
                //            " ; station section = " + StationStops[0].TCSectionIndex + " )\n");
                //    }
                //}
                else if (nextActionInfo != null &&
                 nextActionInfo.NextAction == AIActionItem.AI_ACTION_TYPE.STATION_STOP)
                {
                    if (StationStops[0].SubrouteIndex == TCRoute.activeSubpath &&
                       ValidRoute[0].GetRouteIndex(StationStops[0].TCSectionIndex, PresentPosition[0].RouteListIndex) <= PresentPosition[0].RouteListIndex)
                    // assume to be in station
                    {
                        MovementState = AI_MOVEMENT_STATE.STATION_STOP;

                        if (CheckTrain)
                        {
                            File.AppendAllText(@"C:\temp\checktrain.txt", "Train " + Number + " assumed to be in station : " +
                                StationStops[0].PlatformItem.Name + "( present section = " + PresentPosition[0].TCSectionIndex +
                                " ; station section = " + StationStops[0].TCSectionIndex + " )\n");
                        }
                    }
                    else
                    // approaching next station
                    {
                        MovementState = AI_MOVEMENT_STATE.BRAKING;

                        if (CheckTrain)
                        {
                            File.AppendAllText(@"C:\temp\checktrain.txt", "Train " + Number + " departing from station stop to next stop : " +
                                StationStops[0].PlatformItem.Name + "( next section = " + PresentPosition[0].TCSectionIndex +
                                " ; station section = " + StationStops[0].TCSectionIndex + " )\n");
                        }
                    }
                }
#if NEW_ACTION
                else if (nextActionInfo != null &&
                    nextActionInfo.NextAction == AIActionItem.AI_ACTION_TYPE.AUX_ACTION)
                {
                    MovementState = AI_MOVEMENT_STATE.BRAKING;
                }
#endif
                else if (nextActionInfo == null || nextActionInfo.NextAction != AIActionItem.AI_ACTION_TYPE.SIGNAL_ASPECT_STOP)
                {
                    if (nextAspect != MstsSignalAspect.STOP)
                    {
                        MovementState = AI_MOVEMENT_STATE.RUNNING;
                        StartMoving(AI_START_MOVEMENT.SIGNAL_CLEARED);
                    }
                    else if (!Simulator.TimetableMode && Simulator.Settings.EnhancedActCompatibility)
                    {
                        //<CSComment: without this train would not start moving if there is a stop signal in front
                        if (NextSignalObject[0] != null)
                        {
                            var distanceSignaltoTrain = NextSignalObject[0].DistanceTo(FrontTDBTraveller);
                            float distanceToReversalPoint = 10000000f;
                            if (TCRoute.ReversalInfo[TCRoute.activeSubpath] != null && TCRoute.ReversalInfo[TCRoute.activeSubpath].Valid)
                            {
                                distanceToReversalPoint = ComputeDistanceToReversalPoint();
                            }
                            if (distanceSignaltoTrain >= 100.0f || (nextActionInfo != null && nextActionInfo.NextAction == AIActionItem.AI_ACTION_TYPE.REVERSAL 
                                && nextActionInfo.ActivateDistanceM - DistanceTravelledM > 10)|| 
                                distanceSignaltoTrain > distanceToReversalPoint)
                            {
                            MovementState = AI_MOVEMENT_STATE.RUNNING;
                                //>CSComment: better be sure the train will stop in front of signal
                            CreateTrainAction(0.0f, 0.0f, distanceSignaltoTrain, SignalObjectItems[0], AIActionItem.AI_ACTION_TYPE.SIGNAL_ASPECT_STOP);
                            Alpha10 = 10;
                                AITrainThrottlePercent = 25;
                                AdjustControlsBrakeOff();
                            }
                        }
                    }

#if DEBUG_REPORTS
                    File.AppendAllText(@"C:\temp\printproc.txt", "Train " +
                                Number.ToString() + " , forced to BRAKING from invalid stop (now at " +
                                FormatStrings.FormatDistance(PresentPosition[0].DistanceTravelledM, true) + ")\n");
#endif

                    if (CheckTrain)
                    {
                        File.AppendAllText(@"C:\temp\checktrain.txt", "Train " +
                                Number.ToString() + " , forced to BRAKING from invalid stop (now at " +
                                FormatStrings.FormatDistance(PresentPosition[0].DistanceTravelledM, true) + ")\n");
                    }
                }
            }
            return MovementState;
        }

        //================================================================================================//
        /// <summary>
        /// Train is at station
        /// <\summary>

        public void UpdateStationState(int presentTime)
        {
            StationStop thisStation = StationStops[0];
            bool removeStation = true;

            int eightHundredHours = 8 * 3600;
            int sixteenHundredHours = 16 * 3600;
            int actualdepart = thisStation.ActualDepart;

            // no arrival / departure time set : update times

            if (thisStation.ActualStopType == StationStop.STOPTYPE.STATION_STOP)
            {
                AtStation = true;
                 
                if (thisStation.ActualArrival < 0)
                {
                    thisStation.ActualArrival = presentTime;
                    thisStation.CalculateDepartTime(presentTime, this );
                    actualdepart = thisStation.ActualDepart;

#if DEBUG_REPORTS
                    DateTime baseDT = new DateTime();
                    DateTime arrTime = baseDT.AddSeconds(presentTime);

                    File.AppendAllText(@"C:\temp\printproc.txt", "Train " +
                         Number.ToString() + " arrives station " +
                         StationStops[0].PlatformItem.Name + " at " +
                         arrTime.ToString("HH:mm:ss") + "\n");
#endif
                    if (CheckTrain)
                    {
                        DateTime baseDTCT = new DateTime();
                        DateTime arrTimeCT = baseDTCT.AddSeconds(presentTime);
                        DateTime depTimeCT = baseDTCT.AddSeconds(thisStation.ActualDepart);

                        File.AppendAllText(@"C:\temp\checktrain.txt", "Train " +
                             Number.ToString() + " arrives station " +
                             StationStops[0].PlatformItem.Name + " at " +
                             arrTimeCT.ToString("HH:mm:ss") + " ; dep. at " +
                             depTimeCT.ToString("HH:mm:ss") + "\n");
                    }

                    // set reference arrival for any waiting connections
                    if (thisStation.ConnectionsWaiting.Count > 0)
                    {
                        foreach (int otherTrainNumber in thisStation.ConnectionsWaiting)
                        {
                            Train otherTrain = GetOtherTrainByNumber(otherTrainNumber);
                            if (otherTrain != null)
                            {
                                foreach (StationStop otherStop in otherTrain.StationStops)
                                {
                                    if (String.Compare(thisStation.PlatformItem.Name, otherStop.PlatformItem.Name) == 0)
                                    {
                                        if (otherStop.ConnectionsAwaited.ContainsKey(Number))
                                        {
                                            otherStop.ConnectionsAwaited.Remove(Number);
                                        }
                                        otherStop.ConnectionsAwaited.Add(Number, thisStation.ActualArrival);
                                    }
                                }
                            }
                        }
                    }
                }

                // check for connections
                if (thisStation.ConnectionsAwaited.Count > 0)
                {
                    int deptime = -1;
                    int needwait = -1;
                    needwait = ProcessConnections(thisStation, out deptime);

                    // if required to wait : exit
                    if (needwait >= 0)
                    {
                        return;
                    }

                    if (deptime >= 0)
                    {
                        actualdepart = CompareTimes.LatestTime(actualdepart, deptime);
                        thisStation.ActualDepart = actualdepart;
                    }
                }
            }
            // not yet time to depart - check if signal can be released

            int correctedTime = presentTime;

            if (actualdepart > sixteenHundredHours && presentTime < eightHundredHours) // should have departed before midnight
            {
                correctedTime = presentTime + (24 * 3600);
            }

            if (actualdepart < eightHundredHours && presentTime > sixteenHundredHours) // to depart after midnight
            {
                correctedTime = presentTime - 24 * 3600;
            }

#if WITH_PATH_DEBUG
            if (Simulator.Settings.EnhancedActCompatibility)
            {
                currentAIStation = " ---";
                switch (thisStation.ActualStopType)
                {
                    case StationStop.STOPTYPE.MANUAL_STOP:
                        currentAIStation = " Manual stop";
                        break;
                    case StationStop.STOPTYPE.SIDING_STOP:
                        currentAIStation = " Siding stop";
                        break;
                    case StationStop.STOPTYPE.STATION_STOP:
                        currentAIStation = " Station stop";
                        break;
                    case StationStop.STOPTYPE.WAITING_POINT:
                        currentAIStation = " Waiting Point";
                        break;
                    default:
                        currentAIStation = " ---";
                        break;
                }
                currentAIStation = String.Concat(currentAIStation, " ", actualdepart.ToString(), "?", correctedTime.ToString());
            }
#endif
            if (actualdepart > correctedTime)
            {
                if (thisStation.ActualStopType == StationStop.STOPTYPE.STATION_STOP &&
                    (actualdepart - 120 < correctedTime) &&
                     thisStation.HoldSignal)
                {
                    HoldingSignals.Remove(thisStation.ExitSignal);
                    var nextSignal = signalRef.SignalObjects[thisStation.ExitSignal];

                    if (CheckTrain)
                    {
                        File.AppendAllText(@"C:\temp\checktrain.txt", "Train " +
                             Number.ToString() + " clearing hold signal " + nextSignal.thisRef.ToString() + " at station " +
                             StationStops[0].PlatformItem.Name + "\n");
                    }

                    if (nextSignal.enabledTrain != null && nextSignal.enabledTrain.Train == this)
                    {
                        nextSignal.requestClearSignal(ValidRoute[0], routedForward, 0, false, null);// for AI always use direction 0
                    }
                    thisStation.HoldSignal = false;
                }
                return;
            }

            // depart

            thisStation.Passed = true;

            if (!Program.Simulator.TimetableMode && Program.Simulator.Settings.EnhancedActCompatibility && thisStation.ActualStopType == StationStop.STOPTYPE.STATION_STOP 
                && MaxVelocityA > 0 && ServiceDefinition != null && ServiceDefinition.ServiceList.Count > 0)
            // <CScomment> Recalculate TrainMaxSpeedMpS and AllowedMaxSpeedMpS
            {
               var actualServiceItemIdx = ServiceDefinition.ServiceList.FindIndex (si => si.PlatformStartID == thisStation.PlatformReference );
               if (actualServiceItemIdx >=0 && ServiceDefinition.ServiceList.Count >= actualServiceItemIdx + 2)
               {
                       var sectionEfficiency = ServiceDefinition.ServiceList[actualServiceItemIdx + 1].Efficiency;
                       if (sectionEfficiency > 0)
                       { 
                           TrainMaxSpeedMpS = Math.Min((float)Simulator.TRK.Tr_RouteFile.SpeedLimit, MaxVelocityA * sectionEfficiency);
                           RecalculateAllowedMaxSpeed();
                       }
               }
               else if (MaxVelocityA > 0 && Efficiency > 0)
               {
                   TrainMaxSpeedMpS = Math.Min((float)Simulator.TRK.Tr_RouteFile.SpeedLimit, MaxVelocityA * Efficiency);
                   RecalculateAllowedMaxSpeed();
               }
             }

            // first, check state of signal

            if (thisStation.ExitSignal >= 0 && thisStation.HoldSignal)
            {
                HoldingSignals.Remove(thisStation.ExitSignal);
                var nextSignal = signalRef.SignalObjects[thisStation.ExitSignal];

                // only request signal if in signal mode (train may be in node control)
                if (ControlMode == TRAIN_CONTROL.AUTO_SIGNAL)
                {
                    nextSignal.requestClearSignal(ValidRoute[0], routedForward, 0, false, null); // for AI always use direction 0
                }
            }

            // check if station is end of path

            bool[] endOfPath = ProcessEndOfPath(presentTime);

            if (endOfPath[0])
            {
                removeStation = false; // do not remove station from list - is done by path processing
            }
            // check if station has exit signal and this signal is at danger
            else if (thisStation.ExitSignal >= 0 && NextSignalObject[0] != null && NextSignalObject[0].thisRef == thisStation.ExitSignal)
            {
                MstsSignalAspect nextAspect = GetNextSignalAspect(0);
                if (nextAspect == MstsSignalAspect.STOP)
                {
                    return;  // do not depart if exit signal at danger
                }
            }

            // change state if train still exists
            if (endOfPath[1])
            {
                if (MovementState == AI_MOVEMENT_STATE.STATION_STOP)
                {
                    // if state is still station_stop and ready to depart - change to stop to check action
                    MovementState = (Simulator.TimetableMode || !Simulator.Settings.EnhancedActCompatibility)? AI_MOVEMENT_STATE.STOPPED : AI_MOVEMENT_STATE.STOPPED_EXISTING;   
                    AtStation = false;
                }

                Delay = TimeSpan.FromSeconds((presentTime - thisStation.DepartTime) % (24 * 3600));
            }

#if DEBUG_REPORTS
            DateTime baseDTd = new DateTime();
            DateTime depTime = baseDTd.AddSeconds(presentTime);

            if (thisStation.ActualStopType == StationStop.STOPTYPE.STATION_STOP)
            {
                File.AppendAllText(@"C:\temp\printproc.txt", "Train " +
                            Number.ToString() + " departs station " +
                            StationStops[0].PlatformItem.Name + " at " +
                            depTime.ToString("HH:mm:ss") + "\n");
            }
            else if (thisStation.ActualStopType == StationStop.STOPTYPE.WAITING_POINT)
            {
                File.AppendAllText(@"C:\temp\printproc.txt", "Train " +
                            Number.ToString() + " departs waiting point at " +
                            depTime.ToString("HH:mm:ss") + "\n");
            }

            if (thisStation.ExitSignal >= 0)
            {
                File.AppendAllText(@"C:\temp\printproc.txt", "Exit signal : " + thisStation.ExitSignal.ToString() + "\n");
                File.AppendAllText(@"C:\temp\printproc.txt", "Holding signals : \n");
                foreach (int thisSignal in HoldingSignals)
                {
                    File.AppendAllText(@"C:\temp\printproc.txt", "Signal : " + thisSignal.ToString() + "\n");
                }
                File.AppendAllText(@"C:\temp\printproc.txt", "\n");
            }
#endif

            if (CheckTrain)
            {
                DateTime baseDTdCT = new DateTime();
                DateTime depTimeCT = baseDTdCT.AddSeconds(presentTime);

                if (thisStation.ActualStopType == StationStop.STOPTYPE.STATION_STOP)
                {
                    File.AppendAllText(@"C:\temp\checktrain.txt", "Train " +
                                Number.ToString() + " departs station " +
                                thisStation.PlatformItem.Name + " at " +
                                depTimeCT.ToString("HH:mm:ss") + "\n");
                }
                else if (thisStation.ActualStopType == StationStop.STOPTYPE.WAITING_POINT)
                {
                    File.AppendAllText(@"C:\temp\checktrain.txt", "Train " +
                                Number.ToString() + " departs waiting point at " +
                                depTimeCT.ToString("HH:mm:ss") + "\n");
                }

                if (thisStation.ExitSignal >= 0)
                {
                    File.AppendAllText(@"C:\temp\checktrain.txt", "Exit signal : " + thisStation.ExitSignal.ToString() + "\n");
                    File.AppendAllText(@"C:\temp\checktrain.txt", "Holding signals : \n");
                    foreach (int thisSignal in HoldingSignals)
                    {
                        File.AppendAllText(@"C:\temp\checktrain.txt", "Signal : " + thisSignal.ToString() + "\n");
                    }
                    File.AppendAllText(@"C:\temp\checktrain.txt", "\n");
                }
            }

            if (removeStation)
                StationStops.RemoveAt(0);

            ResetActions(true);
        }

        //================================================================================================//
        /// <summary>
        /// Train is braking
        /// <\summary>

        public void UpdateBrakingState(float elapsedClockSeconds, int presentTime)
        {

            // check if action still required

            bool clearAction = false;
            float distanceToGoM = clearingDistanceM;

            if (nextActionInfo == null) // action has been reset - keep status quo
            {
                if (ControlMode == TRAIN_CONTROL.AUTO_NODE)  // node control : use control distance
                {
                    distanceToGoM = DistanceToEndNodeAuthorityM[0];

                    if (EndAuthorityType[0] == END_AUTHORITY.RESERVED_SWITCH)
                    {
                        distanceToGoM = DistanceToEndNodeAuthorityM[0] - 2.0f * junctionOverlapM;
                    }
                    else if (EndAuthorityType[0] == END_AUTHORITY.END_OF_PATH)
                    {
                        distanceToGoM = DistanceToEndNodeAuthorityM[0] - clearingDistanceM;
                    }

                    if (distanceToGoM <= 0)
                    {
                        if (SpeedMpS > 0)
                        {
                            if (CheckTrain)
                            {
                                File.AppendAllText(@"C:\temp\checktrain.txt",
                                                        "Brake mode - auto node - passed distance moving - set brakes\n");
                            }
                            AdjustControlsBrakeMore(MaxDecelMpSS, elapsedClockSeconds, 50);
                        }
                    }

                    if (distanceToGoM < clearingDistanceM && SpeedMpS <= 0)
                    {
                        if (CheckTrain)
                        {
                            File.AppendAllText(@"C:\temp\checktrain.txt",
                                                    "Brake mode - auto node - passed distance stopped - to stop state\n");
                        }
                        MovementState = AI_MOVEMENT_STATE.STOPPED;
                        return;
                    }
                }
                else // action cleared - set running or stopped
                {
                    if (SpeedMpS > 0)
                    {
                        MovementState = AI_MOVEMENT_STATE.RUNNING;
                        if (CheckTrain)
                        {
                            File.AppendAllText(@"C:\temp\checktrain.txt",
                                                    "Brake mode - auto node - action clear while moving - to running state\n");
                        }
                    }
                    else
                    {
                        MovementState = AI_MOVEMENT_STATE.STOPPED;
                        if (CheckTrain)
                        {
                            File.AppendAllText(@"C:\temp\checktrain.txt",
                                                    "Brake mode - auto node - action cleared while stopped - to stop state\n");
                        }
                    }
                    return;
                }

            }

                // check if speedlimit on signal is cleared

            else if (nextActionInfo.NextAction == AIActionItem.AI_ACTION_TYPE.SPEED_SIGNAL)
            {
                if (nextActionInfo.ActiveItem.actual_speed >= AllowedMaxSpeedMpS)
                {
                    clearAction = true;

#if DEBUG_REPORTS
                    File.AppendAllText(@"C:\temp\printproc.txt", "Train " +
                          Number.ToString() + " : signal " +
                          nextActionInfo.ActiveItem.ObjectDetails.thisRef.ToString() + " : speed : " +
                          FormatStrings.FormatSpeed(nextActionInfo.ActiveItem.actual_speed, true) + " >= limit : " +
                          FormatStrings.FormatSpeed(AllowedMaxSpeedMpS, true) + " at " +
                          FormatStrings.FormatDistance(nextActionInfo.ActivateDistanceM, true) + " (now at " +
                          FormatStrings.FormatDistance(PresentPosition[0].DistanceTravelledM, true) + " - " +
                          FormatStrings.FormatSpeed(SpeedMpS, true) + ")\n");
#endif
                    if (CheckTrain)
                    {
                        File.AppendAllText(@"C:\temp\checktrain.txt", "Train " +
                              Number.ToString() + " : signal " +
                              nextActionInfo.ActiveItem.ObjectDetails.thisRef.ToString() + " : speed : " +
                              FormatStrings.FormatSpeed(nextActionInfo.ActiveItem.actual_speed, true) + " >= limit : " +
                              FormatStrings.FormatSpeed(AllowedMaxSpeedMpS, true) + " at " +
                              FormatStrings.FormatDistance(nextActionInfo.ActivateDistanceM, true) + " (now at " +
                              FormatStrings.FormatDistance(PresentPosition[0].DistanceTravelledM, true) + " - " +
                              FormatStrings.FormatSpeed(SpeedMpS, true) + ")\n");
                    }
                }
                else if (nextActionInfo.ActiveItem.actual_speed < 0)
                {
                    clearAction = true;

#if DEBUG_REPORTS
                    File.AppendAllText(@"C:\temp\printproc.txt", "Train " +
                          Number.ToString() + " : signal " +
                          nextActionInfo.ActiveItem.ObjectDetails.thisRef.ToString() + " : speed : " +
                          FormatStrings.FormatSpeed(nextActionInfo.ActiveItem.actual_speed, true) + " cleared at " +
                          FormatStrings.FormatDistance(nextActionInfo.ActivateDistanceM, true) + " (now at " +
                          FormatStrings.FormatDistance(PresentPosition[0].DistanceTravelledM, true) + " - " +
                          FormatStrings.FormatSpeed(SpeedMpS, true) + ")\n");
#endif
                    if (CheckTrain)
                    {
                        File.AppendAllText(@"C:\temp\checktrain.txt", "Train " +
                              Number.ToString() + " : signal " +
                              nextActionInfo.ActiveItem.ObjectDetails.thisRef.ToString() + " : speed : " +
                              FormatStrings.FormatSpeed(nextActionInfo.ActiveItem.actual_speed, true) + " cleared at " +
                              FormatStrings.FormatDistance(nextActionInfo.ActivateDistanceM, true) + " (now at " +
                              FormatStrings.FormatDistance(PresentPosition[0].DistanceTravelledM, true) + " - " +
                              FormatStrings.FormatSpeed(SpeedMpS, true) + ")\n");
                    }
                }
            }

        // check if STOP signal cleared

            else if (nextActionInfo.NextAction == AIActionItem.AI_ACTION_TYPE.SIGNAL_ASPECT_STOP)
            {
                if (nextActionInfo.ActiveItem.signal_state >= MstsSignalAspect.APPROACH_1)
                {
                    clearAction = true;

#if DEBUG_REPORTS
                    File.AppendAllText(@"C:\temp\printproc.txt", "Train " +
                          Number.ToString() + " : signal " +
                          nextActionInfo.ActiveItem.ObjectDetails.thisRef.ToString() + " at " +
                          FormatStrings.FormatDistance(nextActionInfo.ActivateDistanceM, true) + " cleared (now at " +
                          FormatStrings.FormatDistance(PresentPosition[0].DistanceTravelledM, true) + " - " +
                          FormatStrings.FormatSpeed(SpeedMpS, true) + ")\n");
#endif
                    if (CheckTrain)
                    {
                        File.AppendAllText(@"C:\temp\checktrain.txt", "Train " +
                              Number.ToString() + " : signal " +
                              nextActionInfo.ActiveItem.ObjectDetails.thisRef.ToString() + " at " +
                              FormatStrings.FormatDistance(nextActionInfo.ActivateDistanceM, true) + " cleared (now at " +
                              FormatStrings.FormatDistance(PresentPosition[0].DistanceTravelledM, true) + " - " +
                              FormatStrings.FormatSpeed(SpeedMpS, true) + ")\n");
                    }
                }
                else if (nextActionInfo.ActiveItem.signal_state != MstsSignalAspect.STOP)
                {
                    nextActionInfo.NextAction = AIActionItem.AI_ACTION_TYPE.SIGNAL_ASPECT_RESTRICTED;
                    if ((nextActionInfo.ActivateDistanceM - PresentPosition[0].DistanceTravelledM) < signalApproachDistanceM)
                    {
                        clearAction = true;
#if DEBUG_REPORTS
                        File.AppendAllText(@"C:\temp\printproc.txt",
                          Number.ToString() + " : signal " +
                          nextActionInfo.ActiveItem.ObjectDetails.thisRef.ToString() + " at " +
                          FormatStrings.FormatDistance(nextActionInfo.ActivateDistanceM, true) + " cleared (now at " +
                          FormatStrings.FormatDistance(PresentPosition[0].DistanceTravelledM, true) + " - " +
                          FormatStrings.FormatSpeed(SpeedMpS, true) + ")\n");
#endif
                        if (CheckTrain)
                        {
                            File.AppendAllText(@"C:\temp\checktrain.txt",
                              Number.ToString() + " : signal " +
                              nextActionInfo.ActiveItem.ObjectDetails.thisRef.ToString() + " at " +
                              FormatStrings.FormatDistance(nextActionInfo.ActivateDistanceM, true) + " cleared (now at " +
                              FormatStrings.FormatDistance(PresentPosition[0].DistanceTravelledM, true) + " - " +
                              FormatStrings.FormatSpeed(SpeedMpS, true) + ")\n");
                        }
                    }
                }
            }

        // check if RESTRICTED signal cleared

            else if (nextActionInfo.NextAction == AIActionItem.AI_ACTION_TYPE.SIGNAL_ASPECT_RESTRICTED)
            {
                if (nextActionInfo.ActiveItem.signal_state >= MstsSignalAspect.APPROACH_1 ||
                (nextActionInfo.ActivateDistanceM - PresentPosition[0].DistanceTravelledM) < signalApproachDistanceM)
                {
                    clearAction = true;
#if DEBUG_REPORTS
                    File.AppendAllText(@"C:\temp\printproc.txt",
                      Number.ToString() + " : signal " +
                      nextActionInfo.ActiveItem.ObjectDetails.thisRef.ToString() + " at " +
                      FormatStrings.FormatDistance(nextActionInfo.ActivateDistanceM, true) + " cleared (now at " +
                      FormatStrings.FormatDistance(PresentPosition[0].DistanceTravelledM, true) + " - " +
                      FormatStrings.FormatSpeed(SpeedMpS, true) + ")\n");
#endif
                    if (CheckTrain)
                    {
                        File.AppendAllText(@"C:\temp\checktrain.txt",
                          Number.ToString() + " : signal " +
                          nextActionInfo.ActiveItem.ObjectDetails.thisRef.ToString() + " at " +
                          FormatStrings.FormatDistance(nextActionInfo.ActivateDistanceM, true) + " cleared (now at " +
                          FormatStrings.FormatDistance(PresentPosition[0].DistanceTravelledM, true) + " - " +
                          FormatStrings.FormatSpeed(SpeedMpS, true) + ")\n");
                    }
                }
            }

    // check if END_AUTHORITY extended

            else if (nextActionInfo.NextAction == AIActionItem.AI_ACTION_TYPE.END_OF_AUTHORITY)
            {
                nextActionInfo.ActivateDistanceM = DistanceToEndNodeAuthorityM[0] + DistanceTravelledM;
                if (EndAuthorityType[0] == END_AUTHORITY.MAX_DISTANCE)
                {
                    clearAction = true;
                }
            }

            else if (nextActionInfo.NextAction == AIActionItem.AI_ACTION_TYPE.SPEED_LIMIT)
            {
                if (nextActionInfo.RequiredSpeedMpS >= AllowedMaxSpeedMpS)
                {
                    clearAction = true;

#if DEBUG_REPORTS
                    File.AppendAllText(@"C:\temp\printproc.txt", "Train " +
                          Number.ToString() + " : speed : " +
                          FormatStrings.FormatSpeed(nextActionInfo.RequiredSpeedMpS, true) + " >= limit : " +
                          FormatStrings.FormatSpeed(AllowedMaxSpeedMpS, true) + " at " +
                          FormatStrings.FormatDistance(nextActionInfo.ActivateDistanceM, true) + " (now at " +
                          FormatStrings.FormatDistance(PresentPosition[0].DistanceTravelledM, true) + " - " +
                          FormatStrings.FormatSpeed(SpeedMpS, true) + ")\n");
#endif
                    if (CheckTrain)
                    {
                        File.AppendAllText(@"C:\temp\checktrain.txt", "Train " +
                              Number.ToString() + " : speed : " +
                              FormatStrings.FormatSpeed(nextActionInfo.RequiredSpeedMpS, true) + " >= limit : " +
                              FormatStrings.FormatSpeed(AllowedMaxSpeedMpS, true) + " at " +
                              FormatStrings.FormatDistance(nextActionInfo.ActivateDistanceM, true) + " (now at " +
                              FormatStrings.FormatDistance(PresentPosition[0].DistanceTravelledM, true) + " - " +
                              FormatStrings.FormatSpeed(SpeedMpS, true) + ")\n");
                    }

                }
                else if (nextActionInfo.ActiveItem.actual_speed != nextActionInfo.RequiredSpeedMpS)
                {
                    clearAction = true;

#if DEBUG_REPORTS
                    File.AppendAllText(@"C:\temp\printproc.txt", "Train " +
                          Number.ToString() + " : speed : " +
                          FormatStrings.FormatSpeed(nextActionInfo.RequiredSpeedMpS, true) + " changed to : " +
                          FormatStrings.FormatSpeed(nextActionInfo.ActiveItem.actual_speed, true) + " at " +
                          FormatStrings.FormatDistance(nextActionInfo.ActivateDistanceM, true) + " (now at " +
                          FormatStrings.FormatDistance(PresentPosition[0].DistanceTravelledM, true) + " - " +
                          FormatStrings.FormatSpeed(SpeedMpS, true) + ")\n");
#endif
                    if (CheckTrain)
                    {
                        File.AppendAllText(@"C:\temp\checktrain.txt", "Train " +
                              Number.ToString() + " : speed : " +
                              FormatStrings.FormatSpeed(nextActionInfo.RequiredSpeedMpS, true) + " changed to : " +
                              FormatStrings.FormatSpeed(nextActionInfo.ActiveItem.actual_speed, true) + " at " +
                              FormatStrings.FormatDistance(nextActionInfo.ActivateDistanceM, true) + " (now at " +
                              FormatStrings.FormatDistance(PresentPosition[0].DistanceTravelledM, true) + " - " +
                              FormatStrings.FormatSpeed(SpeedMpS, true) + ")\n");
                    }
                }
            }

            // action cleared - reset processed info for object items to determine next action
            // clear list of pending action to create new list

            if (clearAction)
            {
                ResetActions(true);
                MovementState = AI_MOVEMENT_STATE.RUNNING;
                Alpha10 = 10;
                if (SpeedMpS < AllowedMaxSpeedMpS - 3.0f * hysterisMpS)
                {
                    AdjustControlsBrakeOff();
                }
                return;
            }

            // check ideal speed

            float requiredSpeedMpS = 0;
            float creepDistanceM = 3.0f * signalApproachDistanceM;

            if (nextActionInfo != null)
            {
                requiredSpeedMpS = nextActionInfo.RequiredSpeedMpS;
                distanceToGoM = nextActionInfo.ActivateDistanceM - PresentPosition[0].DistanceTravelledM;

                if (nextActionInfo.ActiveItem != null)
                {
                    distanceToGoM = nextActionInfo.ActiveItem.distance_to_train - signalApproachDistanceM;
                }

                // check if stopped at station

                if (nextActionInfo.NextAction == AIActionItem.AI_ACTION_TYPE.STATION_STOP)
                {
                    NextStopDistanceM = distanceToGoM;
                    if (distanceToGoM <= 0.1f)
                    {
                        AdjustControlsBrakeMore(MaxDecelMpSS, elapsedClockSeconds, 100);
                        AITrainThrottlePercent = 0;

                        // train is stopped - set departure time

                        if (SpeedMpS == 0)
                        {
                            MovementState = AI_MOVEMENT_STATE.STATION_STOP;
                            StationStop thisStation = StationStops[0];

                            if (thisStation.ActualStopType == StationStop.STOPTYPE.STATION_STOP)
                            {
#if DEBUG_REPORTS
                                DateTime baseDT = new DateTime();
                                DateTime arrTime = baseDT.AddSeconds(presentTime);

                                File.AppendAllText(@"C:\temp\printproc.txt", "Train " +
                                     Number.ToString() + " arrives station " +
                                     StationStops[0].PlatformItem.Name + " at " +
                                     arrTime.ToString("HH:mm:ss") + "\n");
#endif
                                if (CheckTrain)
                                {
                                    DateTime baseDTCT = new DateTime();
                                    DateTime arrTimeCT = baseDTCT.AddSeconds(presentTime);

                                    File.AppendAllText(@"C:\temp\checktrain.txt", "Train " +
                                         Number.ToString() + " arrives station " +
                                         StationStops[0].PlatformItem.Name + " at " +
                                         arrTimeCT.ToString("HH:mm:ss") + "\n");
                                }
                            }
                            else if (thisStation.ActualStopType == StationStop.STOPTYPE.WAITING_POINT)
                            {
                                thisStation.ActualArrival = presentTime;

                                // delta time set
                                if (thisStation.DepartTime < 0)
                                {
                                    thisStation.ActualDepart = presentTime - thisStation.DepartTime; // depart time is negative!!
                                }
                                // actual time set
                                else
                                {
                                    thisStation.ActualDepart = thisStation.DepartTime;
                                }

#if DEBUG_REPORTS
                                DateTime baseDT = new DateTime();
                                DateTime arrTime = baseDT.AddSeconds(presentTime);

                                File.AppendAllText(@"C:\temp\printproc.txt", "Train " +
                                     Number.ToString() + " arrives waiting point at " +
                                     arrTime.ToString("HH:mm:ss") + "\n");
#endif
                                if (CheckTrain)
                                {
                                    DateTime baseDTCT = new DateTime();
                                    DateTime arrTimeCT = baseDTCT.AddSeconds(presentTime);

                                    File.AppendAllText(@"C:\temp\checktrain.txt", "Train " +
                                         Number.ToString() + " arrives waiting point at " +
                                         arrTimeCT.ToString("HH:mm:ss") + "\n");
                                }
                            }
                        }
                        return;
                    }
                }
#if NEW_ACTION
                else if(nextActionInfo.GetType().IsSubclassOf(typeof(AuxActionItem)))
                {
                    NextStopDistanceM = distanceToGoM;
                    MovementState = nextActionInfo.ProcessAction(this, presentTime, elapsedClockSeconds, MovementState);
                }
#endif
                // check speed reduction position reached

                else if (nextActionInfo.RequiredSpeedMpS > 0)
                {
                    if (distanceToGoM <= 0.0f)
                    {
                        AdjustControlsBrakeOff();
                        AllowedMaxSpeedMpS = nextActionInfo.RequiredSpeedMpS;
                        MovementState = AI_MOVEMENT_STATE.RUNNING;
                        Alpha10 = 10;
                        if (CheckTrain)
                        {
                            File.AppendAllText(@"C:\temp\checktrain.txt",
                                                    "Speed limit reached : " +
                                           "Speed : " + FormatStrings.FormatSpeed(SpeedMpS, true) +
                                           " ; Reqd : " + FormatStrings.FormatSpeed(nextActionInfo.RequiredSpeedMpS, true) + "\n");
                        }
                        ResetActions(true);
                        return;
                    }
                }

        // check if approaching reversal point

                else if (nextActionInfo.NextAction == AIActionItem.AI_ACTION_TYPE.REVERSAL)
                {
                    if (Math.Abs(SpeedMpS) < 0.01f && ! Simulator.Settings.EnhancedActCompatibility) MovementState = AI_MOVEMENT_STATE.STOPPED;
                    else if (Math.Abs(SpeedMpS) < 0.01f && nextActionInfo.ActivateDistanceM - DistanceTravelledM < 10.0f && 
                        Simulator.Settings.EnhancedActCompatibility && !Simulator.TimetableMode) MovementState = AI_MOVEMENT_STATE.STOPPED;
                }

                // check if stopped at signal

                else if (nextActionInfo.RequiredSpeedMpS == 0)
                {
                    NextStopDistanceM = distanceToGoM;
                    if (distanceToGoM < signalApproachDistanceM)
                    {
                        AdjustControlsBrakeMore(MaxDecelMpSS, elapsedClockSeconds, 50);
                        AITrainThrottlePercent = 0;
                        if (SpeedMpS == 0)
                        {
                            MovementState = AI_MOVEMENT_STATE.STOPPED;
                        }
                        else
                        {
                            AdjustControlsBrakeMore(MaxDecelMpSS, elapsedClockSeconds, 10);
                        }

                        if (CheckTrain)
                        {
                            File.AppendAllText(@"C:\temp\checktrain.txt",
                                                    "Signal Approach reached : " +
                                           "Speed : " + FormatStrings.FormatSpeed(SpeedMpS, true) + "\n");
                        }

                        // if approaching signal and at 0.25 of approach distance and still moving, force stop
                        if (distanceToGoM < (0.25 * signalApproachDistanceM) && SpeedMpS > 0 &&
                            nextActionInfo != null && nextActionInfo.NextAction == AIActionItem.AI_ACTION_TYPE.SIGNAL_ASPECT_STOP)
                        {

#if DEBUG_EXTRAINFO
                            Trace.TraceWarning("Forced stop for signal at danger for train {0} at speed {1}", Number, SpeedMpS);
#endif
                            if (CheckTrain)
                            {
                                File.AppendAllText(@"C:\temp\checktrain.txt",
                                                        "Signal forced stop : " +
                                               "Speed : " + FormatStrings.FormatSpeed(SpeedMpS, true) + "\n");
                            }

                            SpeedMpS = 0.0f;
                            foreach (TrainCar car in Cars)
                            {
                                car.SpeedMpS = SpeedMpS;
                            }
                        }

                        return;
                    }

                    if (nextActionInfo.NextAction == AIActionItem.AI_ACTION_TYPE.SIGNAL_ASPECT_RESTRICTED)
                    {
                        if (distanceToGoM < creepDistanceM)
                        {
                            requiredSpeedMpS = creepSpeedMpS;
                        }
                    }
                }
            }

            if (nextActionInfo != null && nextActionInfo.NextAction == AIActionItem.AI_ACTION_TYPE.STATION_STOP)
                creepDistanceM = 0.0f;
            if (nextActionInfo == null && requiredSpeedMpS == 0)
                creepDistanceM = clearingDistanceM;

            // keep speed within required speed band

            float lowestSpeedMpS = requiredSpeedMpS;

            if (requiredSpeedMpS == 0)
            {
                lowestSpeedMpS =
                    distanceToGoM < (3.0f * signalApproachDistanceM) ? (0.25f * creepSpeedMpS) : creepSpeedMpS;
            }
            else
            {
                lowestSpeedMpS = distanceToGoM < creepDistanceM ? requiredSpeedMpS :
                    Math.Max(creepSpeedMpS, requiredSpeedMpS);
            }

            lowestSpeedMpS = Math.Min(lowestSpeedMpS, AllowedMaxSpeedMpS);

            float maxPossSpeedMpS = distanceToGoM > 0 ? (float)Math.Sqrt(0.25f * MaxDecelMpSS * distanceToGoM) : 0.0f;
            float idealSpeedMpS = Math.Min(AllowedMaxSpeedMpS, Math.Max(maxPossSpeedMpS, lowestSpeedMpS));

            if (requiredSpeedMpS > 0)
            {
                maxPossSpeedMpS =
                        (float)Math.Sqrt(0.12f * MaxDecelMpSS * Math.Max(0.0f, distanceToGoM - (3.0f * signalApproachDistanceM)));
                idealSpeedMpS = Math.Min(AllowedMaxSpeedMpS, Math.Max(maxPossSpeedMpS + requiredSpeedMpS, lowestSpeedMpS)) -
                                    (2f * hysterisMpS);
            }

            float idealLowBandMpS = Math.Max(lowestSpeedMpS, idealSpeedMpS - (3f * hysterisMpS));
            float ideal3LowBandMpS = Math.Max(lowestSpeedMpS, idealSpeedMpS - (9f * hysterisMpS));
            float idealHighBandMpS = Math.Min(AllowedMaxSpeedMpS, Math.Max(lowestSpeedMpS, idealSpeedMpS + hysterisMpS));
            float ideal3HighBandMpS = Math.Min(AllowedMaxSpeedMpS, Math.Max(lowestSpeedMpS, idealSpeedMpS + (2f * hysterisMpS)));

            float deltaSpeedMpS = SpeedMpS - requiredSpeedMpS;
            float idealDecelMpSS = Math.Max((0.5f * MaxDecelMpSS), (deltaSpeedMpS * deltaSpeedMpS / (2.0f * distanceToGoM)));

            float lastDecelMpSS = elapsedClockSeconds > 0 ? ((SpeedMpS - LastSpeedMpS) / elapsedClockSeconds) : idealDecelMpSS;

            float preferredBrakingDistanceM = 2 * AllowedMaxSpeedMpS / (MaxDecelMpSS * MaxDecelMpSS);

            if (distanceToGoM < 0f)
            {
                idealSpeedMpS = requiredSpeedMpS;
                idealLowBandMpS = Math.Max(0.0f, idealSpeedMpS - hysterisMpS);
                idealHighBandMpS = idealSpeedMpS;
                idealDecelMpSS = MaxDecelMpSS;
            }

            if (CheckTrain)
            {
                File.AppendAllText(@"C:\temp\checktrain.txt",
                                        "Brake calculation details : \n");
                File.AppendAllText(@"C:\temp\checktrain.txt",
                               "     Actual: " + FormatStrings.FormatSpeed(SpeedMpS, true) + "\n");
                File.AppendAllText(@"C:\temp\checktrain.txt",
                               "     Allwd : " + FormatStrings.FormatSpeed(AllowedMaxSpeedMpS, true) + "\n");
                File.AppendAllText(@"C:\temp\checktrain.txt",
                               "     Reqd  : " + FormatStrings.FormatSpeed(requiredSpeedMpS, true) + "\n");
                File.AppendAllText(@"C:\temp\checktrain.txt",
                               "     Ideal : " + FormatStrings.FormatSpeed(idealSpeedMpS, true) + "\n");
                File.AppendAllText(@"C:\temp\checktrain.txt",
                               "     lowest: " + FormatStrings.FormatSpeed(lowestSpeedMpS, true) + "\n");
                File.AppendAllText(@"C:\temp\checktrain.txt",
                               "     3high : " + FormatStrings.FormatSpeed(ideal3HighBandMpS, true) + "\n");
                File.AppendAllText(@"C:\temp\checktrain.txt",
                               "     high  : " + FormatStrings.FormatSpeed(idealHighBandMpS, true) + "\n");
                File.AppendAllText(@"C:\temp\checktrain.txt",
                               "     low   : " + FormatStrings.FormatSpeed(idealLowBandMpS, true) + "\n");
                File.AppendAllText(@"C:\temp\checktrain.txt",
                               "     3low  : " + FormatStrings.FormatSpeed(ideal3LowBandMpS, true) + "\n");
                File.AppendAllText(@"C:\temp\checktrain.txt",
                               "     dist  : " + FormatStrings.FormatDistance(distanceToGoM, true) + "\n");
                File.AppendAllText(@"C:\temp\checktrain.txt",
                               "     A&B(S): " + AITrainThrottlePercent.ToString() + " - " + AITrainBrakePercent.ToString() + "\n");
            }

            // keep speed withing band 

            if (SpeedMpS > AllowedMaxSpeedMpS)
            {
                if (AITrainThrottlePercent > 0)
                {
                    AdjustControlsThrottleOff();
                }
                else
                {
                    AdjustControlsBrakeMore(MaxDecelMpSS, elapsedClockSeconds, 20);
                }

                AdjustControlsFixedSpeed(AllowedMaxSpeedMpS);
                Alpha10 = 5;
            }
            else if (SpeedMpS > requiredSpeedMpS && distanceToGoM < 0)
            {
                AdjustControlsBrakeMore(MaxDecelMpSS, elapsedClockSeconds, 50);
            }
            else if (SpeedMpS > ideal3HighBandMpS)
            {
                if (AITrainThrottlePercent > 0)
                {
                    AdjustControlsThrottleOff();
                }
                else if (AITrainBrakePercent < 50)
                {
                    AdjustControlsBrakeMore(2.0f * MaxDecelMpSS, elapsedClockSeconds, 10);
                    Alpha10 = 5;
                }
                else if (lastDecelMpSS < 0.5f * idealDecelMpSS || Alpha10 <= 0)
                {
                    AdjustControlsBrakeMore(2.0f * MaxDecelMpSS, elapsedClockSeconds, 50);
                    Alpha10 = 5;
                }
                Alpha10 = Alpha10 > 0 ? --Alpha10 : 0;
            }
            else if (SpeedMpS > idealHighBandMpS)
            {
                if (LastSpeedMpS > SpeedMpS)
                {
                    if (AITrainBrakePercent > 50)
                    {
                        AdjustControlsBrakeLess(0.3f * MaxDecelMpSS, elapsedClockSeconds, 10);
                    }
                    else if (AITrainBrakePercent > 0)
                    {
                        if (lastDecelMpSS > 1.5f * idealDecelMpSS)
                        {
                            AdjustControlsBrakeLess(0.3f * MaxDecelMpSS, elapsedClockSeconds, 2);
                        }
                        else if (Alpha10 <= 0)
                        {
                            AdjustControlsBrakeLess(0.3f * MaxDecelMpSS, elapsedClockSeconds, 2);
                            Alpha10 = 10;
                        }
                    }
                }
                else
                {
                    if (AITrainThrottlePercent > 50)
                    {
                        AdjustControlsAccelLess(MaxAccelMpSS, elapsedClockSeconds, 10);
                    }
                    else if (AITrainThrottlePercent > 20)
                    {
                        AdjustControlsAccelLess(MaxAccelMpSS, elapsedClockSeconds, 2);
                    }
                    else if (AITrainThrottlePercent > 0)
                    {
                        AdjustControlsThrottleOff();
                    }
                    else if (Alpha10 <= 0 || lastDecelMpSS < (0.5 * idealDecelMpSS))
                    {
                        AdjustControlsBrakeMore(MaxDecelMpSS, elapsedClockSeconds, 10);
                        Alpha10 = 10;
                    }

                }
                Alpha10 = Alpha10 > 0 ? --Alpha10 : 0;
            }
            else if (SpeedMpS > idealLowBandMpS)
            {
                if (LastSpeedMpS < SpeedMpS)
                {
                    if (AITrainThrottlePercent > 50)
                    {
                        AdjustControlsAccelLess(0.5f * MaxAccelMpSS, elapsedClockSeconds, 10);
                        Alpha10 = 5;
                    }
                }
                else
                {
                    if (AITrainBrakePercent > 50)
                    {
                        AdjustControlsBrakeLess(0.0f, elapsedClockSeconds, 20);
                    }
                    else if (AITrainBrakePercent > 0)
                    {
                        AdjustControlsBrakeOff();
                    }
                    else
                    {
                        AdjustControlsAccelMore(0.5f * MaxAccelMpSS, elapsedClockSeconds, 10);
                    }
                }
            }
            else if (SpeedMpS > ideal3LowBandMpS)
            {
                if (AITrainBrakePercent > 0)
                {
                    AdjustControlsBrakeLess(0.5f * MaxAccelMpSS, elapsedClockSeconds, 20);
                }
                else if (LastSpeedMpS > SpeedMpS)
                {
                    if (Alpha10 <= 0)
                    {
                        AdjustControlsAccelMore(0.5f * MaxAccelMpSS, elapsedClockSeconds, 10);
                        Alpha10 = 5;
                    }
                }
                Alpha10 = Alpha10 > 0 ? --Alpha10 : 0;
            }
            else if (SpeedMpS < requiredSpeedMpS)
            {
                AdjustControlsBrakeOff();
                if (((SpeedMpS - LastSpeedMpS) / elapsedClockSeconds) < 0.5f * MaxAccelMpSS)
                {
                    AdjustControlsAccelMore(0.5f * MaxAccelMpSS, elapsedClockSeconds, 20);
                }
                Alpha10 = 5;
            }
            else if (distanceToGoM > 4 * preferredBrakingDistanceM && SpeedMpS < idealLowBandMpS)
            {
                AdjustControlsBrakeOff();
                AdjustControlsAccelMore(0.5f * MaxAccelMpSS, elapsedClockSeconds, 20);
            }
            else if (distanceToGoM > preferredBrakingDistanceM && SpeedMpS < ideal3LowBandMpS)
            {
                AdjustControlsBrakeOff();
                AdjustControlsAccelMore(0.5f * MaxAccelMpSS, elapsedClockSeconds, 20);
            }
            else if (requiredSpeedMpS == 0 && distanceToGoM > creepDistanceM && SpeedMpS < creepSpeedMpS)
            {
                AdjustControlsBrakeOff();
                AdjustControlsAccelMore(0.5f * MaxAccelMpSS, elapsedClockSeconds, 20);
            }
            else if (requiredSpeedMpS == 0 && distanceToGoM > signalApproachDistanceM && SpeedMpS < creepSpeedMpS)
            {
                AdjustControlsBrakeOff();
                AdjustControlsAccelMore(0.25f * MaxAccelMpSS, elapsedClockSeconds, 10);
            }

            // in preupdate : avoid problems with overshoot due to low update rate
            // check if at present speed train would pass beyond end of authority
            if (PreUpdate)
            {
                if (requiredSpeedMpS == 0 && (elapsedClockSeconds * SpeedMpS) > distanceToGoM)
                {
                    SpeedMpS = (0.5f * SpeedMpS);
                }
            }

            if (CheckTrain)
            {
                File.AppendAllText(@"C:\temp\checktrain.txt",
                     "     A&B(E): " + AITrainThrottlePercent.ToString() + " - " + AITrainBrakePercent.ToString() + "\n");
            }
        }

        //================================================================================================//
        /// <summary>
        /// Train is accelerating
        /// <\summary>

        public void UpdateAccelState(float elapsedClockSeconds)
        {

            // check speed

            if (((SpeedMpS - LastSpeedMpS) / elapsedClockSeconds) < 0.5f * MaxAccelMpSS)
            {
                AdjustControlsAccelMore(Efficiency * 0.5f * MaxAccelMpSS, elapsedClockSeconds, 10);
            }

            if (SpeedMpS > (AllowedMaxSpeedMpS - ((9.0f - 6.0f * Efficiency) * hysterisMpS)))
            {
                AdjustControlsAccelLess(0.0f, elapsedClockSeconds, (int)(AITrainThrottlePercent * 0.5f));
                MovementState = AI_MOVEMENT_STATE.RUNNING;
                Alpha10 = 0;
            }
        }

        //================================================================================================//
        /// <summary>
        /// Train is following
        /// <\summary>

        public void UpdateFollowingState(float elapsedClockSeconds, int presentTime)
        {
            if (CheckTrain)
            {
                File.AppendAllText(@"C:\temp\checktrain.txt",
                                        "Update Train Ahead - now at : " +
                                        PresentPosition[0].TCSectionIndex.ToString() + " " +
                                        FormatStrings.FormatDistance(PresentPosition[0].TCOffset, true) +
                                        " ; speed : " + FormatStrings.FormatSpeed(SpeedMpS, true) + "\n");
            }

            if (ControlMode != TRAIN_CONTROL.AUTO_NODE || EndAuthorityType[0] != END_AUTHORITY.TRAIN_AHEAD) // train is gone
            {
                if (CheckTrain)
                {
                    File.AppendAllText(@"C:\temp\checktrain.txt", "Train ahead is cleared \n");
                }
                MovementState = AI_MOVEMENT_STATE.RUNNING;
                ResetActions(true);
            }
            else
            {
                // check if train is in sections ahead
                bool trainFound = false;
                bool lastSection = false;
                Dictionary<Train, float> trainInfo = null;
                int sectionIndex = -1;
                float accDistance = 0;

                for (int iSection = PresentPosition[0].RouteListIndex; iSection < ValidRoute[0].Count && !lastSection && !trainFound; iSection++)
                {
                    sectionIndex = ValidRoute[0][iSection].TCSectionIndex;
                    TrackCircuitSection thisSection = signalRef.TrackCircuitList[sectionIndex];

                    if (sectionIndex == PresentPosition[0].TCSectionIndex)
                    {
                        trainInfo = thisSection.TestTrainAhead(this,
                             PresentPosition[0].TCOffset, PresentPosition[0].TCDirection);
                        if (trainInfo.Count <= 0)
                            accDistance -= PresentPosition[0].TCOffset;  // compensate for offset
                    }
                    else
                    {
                        trainInfo = thisSection.TestTrainAhead(this,
                            0, ValidRoute[0][iSection].Direction);
                    }

                    trainFound = (trainInfo.Count > 0);
                    lastSection = (sectionIndex == LastReservedSection[0]);

                    if (!trainFound)
                    {
                        accDistance += thisSection.Length;
                    }

                    if (CheckTrain)
                    {
                        File.AppendAllText(@"C:\temp\checktrain.txt",
                                            "Train count in section " + sectionIndex.ToString() + " = " + trainInfo.Count.ToString() + "\n");
                    }
                }

                if (trainInfo == null || trainInfo.Count == 0) // try next section after last reserved
                {
                    if (sectionIndex == LastReservedSection[0])
                    {
                        int routeIndex = ValidRoute[0].GetRouteIndex(sectionIndex, PresentPosition[0].RouteListIndex);
                        if (routeIndex >= 0 && routeIndex <= (ValidRoute[0].Count - 1))
                        {
                            sectionIndex = ValidRoute[0][routeIndex + 1].TCSectionIndex;
                            TrackCircuitSection thisSection = signalRef.TrackCircuitList[sectionIndex];

                            trainInfo = thisSection.TestTrainAhead(this,
                                0, ValidRoute[0][routeIndex + 1].Direction);
                        }
                    }
                }

                if (trainInfo != null && trainInfo.Count > 0)  // found train
                {
                    foreach (KeyValuePair<Train, float> trainAhead in trainInfo) // always just one
                    {
                        Train OtherTrain = trainAhead.Key;
                        float distanceToTrain = trainAhead.Value + accDistance;

                        if (CheckTrain)
                        {
                            File.AppendAllText(@"C:\temp\checktrain.txt",
                                                    "Other train : " + OtherTrain.Number.ToString() + " at : " +
                                                    OtherTrain.PresentPosition[0].TCSectionIndex.ToString() + " " +
                                                    FormatStrings.FormatDistance(OtherTrain.PresentPosition[0].TCOffset, true) +
                                                    " ; speed : " + FormatStrings.FormatSpeed(OtherTrain.SpeedMpS, true) + "\n");
                            File.AppendAllText(@"C:\temp\checktrain.txt",
                                                            "DistAhd: " + FormatStrings.FormatDistance(DistanceToEndNodeAuthorityM[0], true) + "\n");
                        }

                        // update action info with new position

                        float keepDistanceTrainM = 0f;
                        bool attachToTrain = AttachTo == OtherTrain.Number;
                        // <CScomment> Make check when this train in same section of OtherTrain; if other train is static or this train is in last section, pass to passive coupling
                        if (!Simulator.TimetableMode && Simulator.Settings.EnhancedActCompatibility && OtherTrain.SpeedMpS == 0.0f)
                        {
                            var rearOrFront = ValidRoute[0][ValidRoute[0].Count - 1].Direction == 1 ? 0 : 1;
                             if   (PresentPosition[rearOrFront].TCSectionIndex == OtherTrain.PresentPosition[0].TCSectionIndex || 
                                PresentPosition[rearOrFront].TCSectionIndex == OtherTrain.PresentPosition[1].TCSectionIndex)
                            {
                                if (OtherTrain.TrainType == TRAINTYPE.STATIC || PresentPosition[0].TCSectionIndex ==
                                    TCRoute.TCRouteSubpaths[TCRoute.TCRouteSubpaths.Count - 1][TCRoute.TCRouteSubpaths[TCRoute.TCRouteSubpaths.Count - 1].Count - 1].TCSectionIndex)
                                { 
                                attachToTrain = true;
                                AttachTo = OtherTrain.Number;
                                }
                            }
                        }
                        if (OtherTrain.SpeedMpS != 0.0f)
                        {
                            keepDistanceTrainM = keepDistanceMovingTrainM;
                        }
                        else if (!attachToTrain)
                        {
                            keepDistanceTrainM = (OtherTrain.IsFreight || IsFreight) ? keepDistanceStatTrainM_F : keepDistanceStatTrainM_P;
                        }

                        if (nextActionInfo != null && nextActionInfo.NextAction == AIActionItem.AI_ACTION_TYPE.TRAIN_AHEAD)
                        {
                            NextStopDistanceM = distanceToTrain - keepDistanceTrainM;
                        }
                        else if (nextActionInfo != null)
                        {
                            //float deltaDistance = nextActionInfo.ActivateDistanceM - DistanceTravelledM;
                            //if (deltaDistance < distanceToTrain) MovementState = AI_MOVEMENT_STATE.BRAKING; // switch to normal braking to handle action
                            //NextStopDistanceM = Math.Min(deltaDistance, (distanceToTrain - keepDistanceTrainM));
                            float deltaDistance = nextActionInfo.ActivateDistanceM - DistanceTravelledM;
                            if (nextActionInfo.RequiredSpeedMpS > 0.0f)
                            {
                                NextStopDistanceM = distanceToTrain - keepDistanceTrainM;
                            }
                            else
                            {
                                NextStopDistanceM = Math.Min(deltaDistance, (distanceToTrain - keepDistanceTrainM));
                            }

                            if (deltaDistance < distanceToTrain) // perform to normal braking to handle action
                            {
                                UpdateBrakingState(elapsedClockSeconds, presentTime);
                                return;
                            }
                        }

                        // check distance and speed
                        if (OtherTrain.SpeedMpS == 0.0f)
                        {
                            float brakingDistance = SpeedMpS * SpeedMpS * 0.5f * (0.5f * MaxDecelMpSS);
                            float reqspeed = (float)Math.Sqrt(distanceToTrain * MaxDecelMpSS);

                            float maxspeed = Math.Max(reqspeed / 2, creepSpeedMpS); // allow continue at creepspeed
                            maxspeed = Math.Min(maxspeed, AllowedMaxSpeedMpS); // but never beyond valid speed limit

                            // set brake or acceleration as required

                            if (SpeedMpS > maxspeed)
                            {
                                AdjustControlsBrakeMore(0.5f * MaxAccelMpSS, elapsedClockSeconds, 10);
                            }
                            else if ((distanceToTrain - brakingDistance) > keepDistanceTrainM * 3.0f)
                            {
                                if (brakingDistance > distanceToTrain)
                                {
                                    AdjustControlsBrakeMore(0.5f * MaxAccelMpSS, elapsedClockSeconds, 10);
                                }
                                else if (SpeedMpS < maxspeed)
                                {
                                    AdjustControlsAccelMore(0.5f * MaxAccelMpSS, elapsedClockSeconds, 20);
                                }
                            }
                            else if ((distanceToTrain - brakingDistance) > keepDistanceTrainM)
                            {
                                if (SpeedMpS > maxspeed)
                                {
                                    AdjustControlsBrakeMore(0.5f * MaxAccelMpSS, elapsedClockSeconds, 50);
                                }
                                else if (SpeedMpS > 0.25f * maxspeed)
                                {
                                    AdjustControlsBrakeOff();
                                }
                                else
                                {
                                    AdjustControlsAccelMore(0.5f * MaxAccelMpSS, elapsedClockSeconds, 10);
                                }
                            }
                            else
                            {
                                float reqMinSpeedMpS = attachToTrain ? 0.5f * creepSpeedMpS : 0;
                                bool thisTrainFront;
                                bool otherTrainFront;

                                if (attachToTrain && CheckCouplePosition(OtherTrain, out thisTrainFront, out otherTrainFront))
                                {
                                    MovementState = AI_MOVEMENT_STATE.STOPPED;
                                    CoupleAI(OtherTrain, thisTrainFront, otherTrainFront);
                                }
                                else if ((SpeedMpS - reqMinSpeedMpS) > 0.1f)
                                {
                                    AdjustControlsBrakeMore(MaxDecelMpSS, elapsedClockSeconds, 50);

                                    // if too close, force stop or slow down if coupling
                                    if (distanceToTrain < 0.25 * keepDistanceTrainM)
                                    {
                                        foreach (TrainCar car in Cars)
                                        {
                                            car.SpeedMpS = car.Flipped ? -reqMinSpeedMpS : reqMinSpeedMpS;
                                        }
                                        SpeedMpS = reqMinSpeedMpS;
                                    }
                                }
                                else if (attachToTrain)
                                {
                                    AdjustControlsBrakeOff();
                                    if (SpeedMpS < 0.2 * creepSpeedMpS)
                                    {
                                        AdjustControlsAccelMore(0.2f * MaxAccelMpSSP, 0.0f, 20);
                                    }
                                }
                                else
                                {
                                    MovementState = AI_MOVEMENT_STATE.STOPPED;

                                    // check if stopped in next station
                                    // conditions : 
                                    // next action must be station stop
                                    // next station must be in this subroute
                                    // if next train is AI and that trains state is STATION_STOP, station must be ahead of present position
                                    // else this train must be in station section

                                    bool otherTrainInStation = false;

                                    if (OtherTrain.TrainType == TRAINTYPE.AI)
                                    {
                                        AITrain OtherAITrain = OtherTrain as AITrain;
                                        otherTrainInStation = (OtherAITrain.MovementState == AI_MOVEMENT_STATE.STATION_STOP);
                                    }

                                    bool thisTrainInStation = (nextActionInfo != null && nextActionInfo.NextAction == AIActionItem.AI_ACTION_TYPE.STATION_STOP);
                                    if (thisTrainInStation) thisTrainInStation = (StationStops[0].SubrouteIndex == TCRoute.activeSubpath);
                                    if (thisTrainInStation)
                                    {
                                        if (otherTrainInStation)
                                        {
                                            thisTrainInStation =
                                                (ValidRoute[0].GetRouteIndex(StationStops[0].TCSectionIndex, PresentPosition[0].RouteListIndex) >= PresentPosition[0].RouteListIndex);
                                        }
                                        else
                                        {
                                            thisTrainInStation =
                                                (ValidRoute[0].GetRouteIndex(StationStops[0].TCSectionIndex, PresentPosition[0].RouteListIndex) == PresentPosition[0].RouteListIndex);
                                        }
                                    }

                                    if (thisTrainInStation)
                                    {
                                        MovementState = AI_MOVEMENT_STATE.STATION_STOP;
                                        StationStop thisStation = StationStops[0];

                                        if (thisStation.ActualStopType == StationStop.STOPTYPE.STATION_STOP)
                                        {
#if DEBUG_REPORTS
                                            DateTime baseDT = new DateTime();
                                            DateTime arrTime = baseDT.AddSeconds(presentTime);

                                            File.AppendAllText(@"C:\temp\printproc.txt", "Train " +
                                                 Number.ToString() + " arrives station " +
                                                 StationStops[0].PlatformItem.Name + " at " +
                                                 arrTime.ToString("HH:mm:ss") + "\n");
#endif
                                            if (CheckTrain)
                                            {
                                                DateTime baseDTCT = new DateTime();
                                                DateTime arrTimeCT = baseDTCT.AddSeconds(presentTime);

                                                File.AppendAllText(@"C:\temp\checktrain.txt", "Train " +
                                                     Number.ToString() + " arrives station " +
                                                     StationStops[0].PlatformItem.Name + " at " +
                                                     arrTimeCT.ToString("HH:mm:ss") + "\n");
                                            }
                                        }
                                        else if (thisStation.ActualStopType == StationStop.STOPTYPE.WAITING_POINT)
                                        {
                                            thisStation.ActualArrival = presentTime;

                                            // delta time set
                                            if (thisStation.DepartTime < 0)
                                            {
                                                thisStation.ActualDepart = presentTime - thisStation.DepartTime; // depart time is negative!!
                                            }
                                            // actual time set
                                            else
                                            {
                                                thisStation.ActualDepart = thisStation.DepartTime;
                                            }

                                            // if waited behind other train, move remaining track sections to next subroute if required

                                            // scan sections in backward order
                                            TCSubpathRoute nextRoute = TCRoute.TCRouteSubpaths[TCRoute.activeSubpath + 1];

                                            for (int iIndex = ValidRoute[0].Count - 1; iIndex > PresentPosition[0].RouteListIndex; iIndex--)
                                            {
                                                int nextSectionIndex = ValidRoute[0][iIndex].TCSectionIndex;
                                                if (nextRoute.GetRouteIndex(nextSectionIndex, 0) <= 0)
                                                {
                                                    nextRoute.Insert(0, ValidRoute[0][iIndex]);
                                                }
                                                ValidRoute[0].RemoveAt(iIndex);
                                            }

#if DEBUG_REPORTS
                                            DateTime baseDT = new DateTime();
                                            DateTime arrTime = baseDT.AddSeconds(presentTime);

                                            File.AppendAllText(@"C:\temp\printproc.txt", "Train " +
                                                Number.ToString() + " arrives waiting point at " +
                                                arrTime.ToString("HH:mm:ss") + "\n");
#endif
                                            if (CheckTrain)
                                            {
                                                DateTime baseDTCT = new DateTime();
                                                DateTime arrTimeCT = baseDTCT.AddSeconds(presentTime);

                                                File.AppendAllText(@"C:\temp\checktrain.txt", "Train " +
                                                     Number.ToString() + " arrives waiting point at " +
                                                     arrTimeCT.ToString("HH:mm:ss") + "\n");
                                            }

                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            if (SpeedMpS > (OtherTrain.SpeedMpS + hysterisMpS) ||
                                SpeedMpS > (maxFollowSpeedMpS + hysterisMpS) ||
                                       distanceToTrain < (keepDistanceTrainM - clearingDistanceM))
                            {
                                AdjustControlsBrakeMore(0.5f * MaxAccelMpSS, elapsedClockSeconds, 10);
                            }
                            else if (SpeedMpS < (OtherTrain.SpeedMpS - hysterisMpS) &&
                                       SpeedMpS < maxFollowSpeedMpS &&
                                       distanceToTrain > (keepDistanceTrainM + clearingDistanceM))
                            {
                                AdjustControlsAccelMore(0.5f * MaxAccelMpSS, elapsedClockSeconds, 2);
                            }
                        }
                    }
                }

                // train not found - keep moving, state will change next update
            }
        }

        //================================================================================================//
        /// <summary>
        /// Train is running at required speed
        /// <\summary>

        public void UpdateRunningState(float elapsedClockSeconds)
        {

            float topBand = AllowedMaxSpeedMpS - ((1.5f - Efficiency) * hysterisMpS);
            float highBand = Math.Max(0.5f, AllowedMaxSpeedMpS - ((3.0f - 2.0f * Efficiency) * hysterisMpS));
            float lowBand = Math.Max(0.4f, AllowedMaxSpeedMpS - ((9.0f - 3.0f * Efficiency) * hysterisMpS));

            // check speed

            if (SpeedMpS > AllowedMaxSpeedMpS)
            {
                if (AITrainThrottlePercent > 0)
                {
                    AdjustControlsThrottleOff();
                }
                else
                {
                    AdjustControlsBrakeMore(MaxDecelMpSS, elapsedClockSeconds, 20);
                }

                AdjustControlsFixedSpeed(AllowedMaxSpeedMpS);
                Alpha10 = 5;
            }
            else if (SpeedMpS > topBand)
            {
                if (LastSpeedMpS > SpeedMpS)
                {
                    if (AITrainBrakePercent > 0)
                    {
                        AdjustControlsBrakeLess(0.5f * MaxDecelMpSS, elapsedClockSeconds, 10);
                    }
                }
                else
                {
                    if (AITrainThrottlePercent > 50)
                    {
                        AdjustControlsAccelLess(0.0f, elapsedClockSeconds, 20);
                    }
                    else if (AITrainThrottlePercent > 0)
                    {
                        if (Alpha10 <= 0)
                        {
                            AdjustControlsAccelLess(0.0f, elapsedClockSeconds, 2);
                            Alpha10 = 5;
                        }
                    }
                    else if (AITrainBrakePercent < 50)
                    {
                        AdjustControlsBrakeMore(0.0f, elapsedClockSeconds, 10);
                    }
                    else
                    {
                        AdjustControlsBrakeMore(MaxDecelMpSS, elapsedClockSeconds, 50);
                    }
                }
                Alpha10 = Alpha10 > 0 ? --Alpha10 : 0;
            }
            else if (SpeedMpS > highBand)
            {
                if (LastSpeedMpS > SpeedMpS)
                {
                    if (AITrainBrakePercent > 50)
                    {
                        AdjustControlsBrakeLess(0.3f * MaxDecelMpSS, elapsedClockSeconds, 10);
                    }
                    else if (AITrainBrakePercent > 0)
                    {
                        AdjustControlsBrakeLess(0.3f * MaxDecelMpSS, elapsedClockSeconds, 2);
                    }
                    else if (Alpha10 <= 0)
                    {
                        AdjustControlsAccelMore(0.3f * MaxAccelMpSS, elapsedClockSeconds, 2);
                        Alpha10 = 10;
                    }
                }
                else
                {
                    if (AITrainThrottlePercent > 50)
                    {
                        AdjustControlsAccelLess(0.3f * MaxAccelMpSS, elapsedClockSeconds, 20);
                    }
                    else if (Alpha10 <= 0 && AITrainThrottlePercent > 20)
                    {
                        AdjustControlsAccelLess(0.3f * MaxAccelMpSS, elapsedClockSeconds, 5);
                        Alpha10 = 10;
                    }
                    else if (Alpha10 <= 0 && AITrainThrottlePercent < 10)
                    {
                        AdjustControlsAccelMore(0.3f * MaxAccelMpSS, elapsedClockSeconds, 2);
                        Alpha10 = 10;
                    }
                }
                Alpha10 = Alpha10 > 0 ? --Alpha10 : 0;
            }
            else if (SpeedMpS > lowBand)
            {
                if (LastSpeedMpS < SpeedMpS)
                {
                    if (AITrainThrottlePercent > 50)
                    {
                        AdjustControlsAccelLess(0.5f * MaxAccelMpSS, elapsedClockSeconds, 10);
                    }
                }
                else
                {
                    if (AITrainBrakePercent > 50)
                    {
                        AdjustControlsBrakeLess(0.0f, elapsedClockSeconds, 20);
                    }
                    else if (AITrainBrakePercent > 0)
                    {
                        AdjustControlsBrakeOff();
                    }
                    else
                    {
                        AdjustControlsAccelMore(0.5f * MaxAccelMpSS, elapsedClockSeconds, 10);
                    }
                }
                Alpha10 = 0;
            }
            else
            {
                AdjustControlsBrakeOff();
                AdjustControlsAccelMore(0.5f * MaxAccelMpSS, elapsedClockSeconds, 20);
                Alpha10 = 0;
            }
        }

        //================================================================================================//
        /// <summary>
        /// Start Moving
        /// <\summary>

        public void StartMoving(AI_START_MOVEMENT reason)
        {
            // reset brakes, set throttle

            if (reason == AI_START_MOVEMENT.FOLLOW_TRAIN)
            {
                MovementState = AI_MOVEMENT_STATE.FOLLOWING;
                AITrainThrottlePercent = 25;
                AdjustControlsBrakeOff();
            }
            else if (ControlMode == TRAIN_CONTROL.AUTO_NODE && EndAuthorityType[0] == END_AUTHORITY.TRAIN_AHEAD)
            {
                MovementState = AI_MOVEMENT_STATE.FOLLOWING;
                AITrainThrottlePercent = 0;
            }
            else if (reason == AI_START_MOVEMENT.NEW)
            {
                MovementState = AI_MOVEMENT_STATE.STOPPED;
                AITrainThrottlePercent = 0;
            }
            else if (nextActionInfo != null)  // train has valid action, so start in BRAKE mode
            {
                MovementState = AI_MOVEMENT_STATE.BRAKING;
                Alpha10 = 10;
                AITrainThrottlePercent = 25;
                AdjustControlsBrakeOff();
            }
            else
            {
                MovementState = AI_MOVEMENT_STATE.ACCELERATING;
                Alpha10 = 10;
                AITrainThrottlePercent = 25;
                AdjustControlsBrakeOff();
            }

            if (FirstCar != null)
            {
                FirstCar.ThrottlePercent = AITrainThrottlePercent;
                FirstCar.BrakeSystem.AISetPercent(AITrainBrakePercent);
            }

        }

        //================================================================================================//
        /// <summary>
        /// Train control routines
        /// <\summary>

        public void AdjustControlsBrakeMore(float reqDecelMpSS, float timeS, int stepSize)
        {
            if (AITrainThrottlePercent > 0)
            {
                AITrainThrottlePercent = 0;
            }

            if (AITrainBrakePercent < 100)
            {
                AITrainBrakePercent += stepSize;
                if (AITrainBrakePercent > 100)
                    AITrainBrakePercent = 100;
            }
            else
            {
                float ds = timeS * (reqDecelMpSS);
                SpeedMpS = Math.Max(SpeedMpS - ds, 0); // avoid negative speeds
                foreach (TrainCar car in Cars)
                {
                    car.SpeedMpS = car.Flipped ? -SpeedMpS : SpeedMpS;
                }
            }

            if (FirstCar != null)
            {
                FirstCar.ThrottlePercent = AITrainThrottlePercent;
                FirstCar.BrakeSystem.AISetPercent(AITrainBrakePercent);
            }

        }

        public void AdjustControlsBrakeLess(float reqDecelMpSS, float timeS, int stepSize)
        {
            if (AITrainThrottlePercent > 0)
            {
                AITrainThrottlePercent = 0;
            }

            if (AITrainBrakePercent > 0)
            {
                AITrainBrakePercent -= stepSize;
                if (AITrainBrakePercent < 0)
                    AdjustControlsBrakeOff();
            }
            else
            {
                float ds = timeS * (reqDecelMpSS);
                SpeedMpS = SpeedMpS + ds; // avoid negative speeds
                foreach (TrainCar car in Cars)
                {
                    car.SpeedMpS = car.Flipped ? -SpeedMpS : SpeedMpS;
                }
            }

            if (FirstCar != null)
            {
                FirstCar.ThrottlePercent = AITrainThrottlePercent;
                FirstCar.BrakeSystem.AISetPercent(AITrainBrakePercent);
            }
        }

        public void AdjustControlsBrakeOff()
        {
            AITrainBrakePercent = 0;
            InitializeBrakes();

            if (FirstCar != null)
            {
                FirstCar.BrakeSystem.AISetPercent(AITrainBrakePercent);
            }
        }

        public void AdjustControlsBrakeFull()
        {
            AITrainThrottlePercent = 0;
            AITrainBrakePercent = 100;

            if (FirstCar != null)
            {
                FirstCar.BrakeSystem.AISetPercent(AITrainBrakePercent);
            }
        }

        public void AdjustControlsThrottleOff()
        {
            AITrainThrottlePercent = 0;

            if (FirstCar != null)
            {
                FirstCar.ThrottlePercent = AITrainThrottlePercent;
            }
        }

        public void AdjustControlsAccelMore(float reqAccelMpSS, float timeS, int stepSize)
        {
            if (AITrainBrakePercent > 0)
            {
                AdjustControlsBrakeOff();
            }

            if (AITrainThrottlePercent < 100)
            {
                AITrainThrottlePercent += stepSize;
                if (AITrainThrottlePercent > 100)
                    AITrainThrottlePercent = 100;
            }
            else if (LastSpeedMpS == 0 || LastSpeedMpS >= SpeedMpS)
            {
                float ds = timeS * (reqAccelMpSS);
                SpeedMpS = SpeedMpS + ds;
                foreach (TrainCar car in Cars)
                {
                    car.SpeedMpS = car.Flipped ? -SpeedMpS : SpeedMpS;
                }
            }

            if (FirstCar != null)
            {
                FirstCar.ThrottlePercent = AITrainThrottlePercent;
                FirstCar.BrakeSystem.AISetPercent(AITrainBrakePercent);
            }
        }


        public void AdjustControlsAccelLess(float reqAccelMpSS, float timeS, int stepSize)
        {
            if (AITrainBrakePercent > 0)
            {
                AdjustControlsBrakeOff();
            }

            if (AITrainThrottlePercent > 0)
            {
                AITrainThrottlePercent -= stepSize;
                if (AITrainThrottlePercent < 0)
                    AITrainThrottlePercent = 0;
            }
            else
            {
                float ds = timeS * (reqAccelMpSS);
                SpeedMpS = Math.Max(SpeedMpS - ds, 0); // avoid negative speeds
                foreach (TrainCar car in Cars)
                {
                    car.SpeedMpS = car.Flipped ? -SpeedMpS : SpeedMpS;
                }
            }

            if (FirstCar != null)
            {
                FirstCar.ThrottlePercent = AITrainThrottlePercent;
                FirstCar.BrakeSystem.AISetPercent(AITrainBrakePercent);
            }

        }

        public void AdjustControlsFixedSpeed(float reqSpeedMpS)
        {
            foreach (TrainCar car in Cars)
            {
                car.SpeedMpS = car.Flipped ? -reqSpeedMpS : reqSpeedMpS;
            }
        }

        //================================================================================================//
        /// <summary>
        /// Update AllowedMaxSpeedMps after station stop
        /// <\summary>
        /// 

        public void RecalculateAllowedMaxSpeed()
        {
            var allowedMaxSpeedPathMpS = Math.Min(allowedAbsoluteMaxSpeedSignalMpS, allowedAbsoluteMaxSpeedLimitMpS);
            AllowedMaxSpeedMpS = Math.Min(allowedMaxSpeedPathMpS, TrainMaxSpeedMpS);
        }

        //================================================================================================//
        /// <summary>
        /// Create waiting point list
        /// <\summary>

        public override void BuildWaitingPointList(float clearingDistanceM)
        {

            // loop through all waiting points - back to front as the processing affects the actual routepaths

#if NEW_ACTION
            for (int iWait = 0; iWait <= TCRoute.WaitingPoints.Count - 1; iWait++)
#else
            int prevSection = -1;
            int TCElmtSignalIdx = -1;
            for (int iWait = TCRoute.WaitingPoints.Count - 1; iWait >= 0; iWait--)
#endif
            {
                int[] waitingPoint = TCRoute.WaitingPoints[iWait];

                TCSubpathRoute thisRoute = TCRoute.TCRouteSubpaths[waitingPoint[0]];
                int routeIndex = thisRoute.GetRouteIndex(waitingPoint[1], 0);
                int lastIndex = routeIndex;

                // check if waiting point is in route - else give warning and skip
                if (routeIndex < 0)
                {
                    Trace.TraceInformation("Waiting point for train " + Number.ToString() + " is not on route - point removed");
                    continue;
                }
#if NEW_ACTION
                int direction = thisRoute[routeIndex].Direction;
#else

                // waiting point is in same section as previous - add time to previous point, remove this point
                if (waitingPoint[1] == prevSection)
                {
                    int[] prevWP = TCRoute.WaitingPoints[iWait + 1];
                    prevWP[2] += waitingPoint[2];
                    TCRoute.WaitingPoints.RemoveAt(iWait);
                    StationStops[StationStops.Count - 1].DepartTime = -prevWP[2];
                    StationStops[StationStops.Count - 1].ActualDepart = -prevWP[2];
                    Trace.TraceInformation("Waiting points for train " + Number.ToString() + " combined, total time set to " + prevWP[2].ToString());
                    continue;
                }

                // check if section has signal

                prevSection = waitingPoint[1];  // save

                int exitSignalReference = -1;
                float offset = 0.0f;
                bool endSectionFound = false;

                TrackCircuitSection thisSection = signalRef.TrackCircuitList[thisRoute[routeIndex].TCSectionIndex];
                TrackCircuitSection nextSection =
                    routeIndex < thisRoute.Count - 2 ? signalRef.TrackCircuitList[thisRoute[routeIndex + 1].TCSectionIndex] : null;

                int direction = thisRoute[routeIndex].Direction;
                
                if (TCElmtSignalIdx == -1)
                {
                    if (thisSection.EndSignals[direction] != null)
                    {
                        endSectionFound = true;
                        offset = thisSection.Length - clearingDistanceM - 1.0f; // 1 m short to force as first action
                        exitSignalReference = thisSection.EndSignals[direction] == null ? -1 : thisSection.EndSignals[direction].thisRef;
                        TCElmtSignalIdx = thisSection.Index;
                    }
                }

                // check if next section is junction

                else if (nextSection == null || nextSection.CircuitType != TrackCircuitSection.TrackCircuitType.Normal)
                {
                    endSectionFound = true;
                    offset = thisSection.Length - junctionOverlapM;
                }
                TCElmtSignalIdx = -1;
                // try and find next section with signal; if junction is found, stop search

                int nextIndex = routeIndex + 1;
                while (nextIndex < thisRoute.Count - 1 && !endSectionFound)
                {
                    nextSection = signalRef.TrackCircuitList[thisRoute[nextIndex].TCSectionIndex];
                    direction = thisRoute[nextIndex].Direction;

                    if (nextSection.EndSignals[direction] != null)
                    {
                        endSectionFound = true;
                        lastIndex = nextIndex;
                        offset = nextSection.Length - clearingDistanceM - 1.0f; // 1 m short to force as first action
                        exitSignalReference = thisSection.EndSignals[direction] == null ? -1 : thisSection.EndSignals[direction].thisRef;
                    }
                    else if (nextSection.CircuitType != TrackCircuitSection.TrackCircuitType.Normal)
                    {
                        endSectionFound = true;
                        lastIndex = nextIndex - 1;
                    }

                    if (!endSectionFound)
                    {
                        nextIndex++;
                        offset = nextSection.Length - junctionOverlapM;  // use this section length if next section is junction
                    }
                }
#endif
                // move sections beyond waiting point to next subroute

#if !NEW_ACTION
                TCSubpathRoute nextRoute = null;
                if ((waitingPoint[0] + 1) > (TCRoute.TCRouteSubpaths.Count - 1))
                {
                    nextRoute = new TCSubpathRoute();
                    TCRoute.TCRouteSubpaths.Add(nextRoute);
                    TCReversalInfo nextReversalPoint = new TCReversalInfo(); // also add dummy reversal info to match total number
                    TCRoute.ReversalInfo.Add(nextReversalPoint);
                    if (!Simulator.TimetableMode && Simulator.Settings.EnhancedActCompatibility)
                    {
                        TCRoute.ReversalInfo[TCRoute.ReversalInfo.Count - 1].ReversalIndex = TCRoute.ReversalInfo[TCRoute.ReversalInfo.Count - 2].ReversalIndex + 1;
                        TCRoute.ReversalInfo[TCRoute.ReversalInfo.Count - 1].ReversalSectionIndex = TCRoute.ReversalInfo[TCRoute.ReversalInfo.Count - 2].ReversalSectionIndex;
                        TCRoute.ReversalInfo[TCRoute.ReversalInfo.Count - 1].ReverseReversalOffset = TCRoute.ReversalInfo[TCRoute.ReversalInfo.Count - 2].ReverseReversalOffset;
                    }
                    TCRoute.LoopEnd.Add(-1); // also add dummy loop end
                }
                else
                {
                    nextRoute = TCRoute.TCRouteSubpaths[waitingPoint[0] + 1];
                }

                for (int iElement = thisRoute.Count - 1; iElement >= lastIndex + 1; iElement--)
                {
                    nextRoute.Insert(0, thisRoute[iElement]);
                    thisRoute.RemoveAt(iElement);
                }
                // repeat actual waiting section in next subroute (if not allready there)

                if (nextRoute.Count <= 0 || nextRoute[0].TCSectionIndex != thisRoute[thisRoute.Count - 1].TCSectionIndex)
                    nextRoute.Insert(0, thisRoute[thisRoute.Count - 1]);

                // build station stop

                bool HoldSignal = exitSignalReference >= 0;

                int DepartTime = waitingPoint[2] > 0 ? -waitingPoint[2] : waitingPoint[3];
#endif

#if NEW_ACTION
                AIActionWP action = new AIActionWP(waitingPoint[5], 0f, 0, thisRoute[lastIndex].TCSectionIndex, lastIndex, direction);
                action.SetDelay(waitingPoint[2]);
                AuxActions.Add(action);
#else

                StationStop thisStation = new StationStop(
                        -1,
                        null,
                        waitingPoint[0],
                        lastIndex,
                        thisRoute[lastIndex].TCSectionIndex,
                        direction,
                        exitSignalReference,
                        HoldSignal,
                        offset,
                        0,
                        DepartTime,
                        StationStop.STOPTYPE.WAITING_POINT);
                StationStops.Add(thisStation);
            }

            // adjust station stop indices for removed subpaths
            for (int i = 0; i < StationStops.Count; i++)
            {
                var WPcur = StationStops[i];
                for (int iTC = TCRoute.TCRouteSubpaths.Count - 1; iTC >= 0; iTC--)
                {
                    var tcRS = TCRoute.TCRouteSubpaths[iTC];
                    for (int iTCE = tcRS.Count - 1; iTCE >= 0; iTCE--)
                    {
                        var tcSR = tcRS[iTCE];
                        if (WPcur.TCSectionIndex == tcSR.TCSectionIndex)
                        {
                            WPcur.SubrouteIndex = iTC;
                            WPcur.RouteIndex = iTCE;
                        }
                    }
                }
            }

            // rebuild loop info
            // retest for loop ends

            for (int iLoop = TCRoute.TCRouteSubpaths.Count - 1; iLoop >= 0; iLoop--)
            {
                int loopSection = TCRoute.LoopEnd[iLoop];
                if (loopSection >= 0)
                {
                    // if no longer on this subpath, test if on any of the following subpaths
                    if (TCRoute.TCRouteSubpaths[iLoop].GetRouteIndex(loopSection, 0) < 0)
                    {
                        for (int iLoop2 = iLoop + 1; iLoop2 <= TCRoute.TCRouteSubpaths.Count - 1; iLoop2++)
                        {
                            if (TCRoute.TCRouteSubpaths[iLoop2].GetRouteIndex(loopSection, 0) >= 0)
                            {
                                if (iLoop2 <= TCRoute.TCRouteSubpaths.Count - 2 && TCRoute.TCRouteSubpaths[iLoop2 + 1].GetRouteIndex(loopSection, 0) >= 0) // must also be on next subpath
                                {
                                    TCRoute.LoopEnd[iLoop2] = loopSection;
                                    Trace.TraceInformation("Loop section " + loopSection + " moved to " + iLoop2 + "\n");
                                }
                            }
                        }

                        TCRoute.LoopEnd[iLoop] = -1;

                    }
                    else
                    {
                        if (iLoop > TCRoute.TCRouteSubpaths.Count - 2 || TCRoute.TCRouteSubpaths[iLoop + 1].GetRouteIndex(loopSection, 0) < 0) // check if also still on next subpath
                        {
                            TCRoute.LoopEnd[iLoop] = -1;
                        }
                    }
                }
#endif
            }
            
        }

        //================================================================================================//
        /// <summary>
        /// Initialize brakes for AI trains
        /// <\summary>

        public override void InitializeBrakes()
        {
            float maxPressurePSI = 90;
            float fullServPressurePSI = 64;
            BrakeLine3PressurePSI = BrakeLine4PressurePSI = 0;
            BrakeLine1PressurePSIorInHg = BrakeLine2PressurePSI = maxPressurePSI;
            foreach (TrainCar car in Cars)
            {
                car.BrakeSystem.Initialize(false, maxPressurePSI, fullServPressurePSI, true);
            }
        }

        //================================================================================================//
        /// <summary>
        /// Process end of path 
        /// returns :
        /// [0] : true : end of route, false : not end of route
        /// [1] : true : train still exists, false : train is removed and no longer exists
        /// <\summary>

        public bool[] ProcessEndOfPath(int presentTime)
        {
            bool[] returnValue = new bool[2] { false, true };

            int directionNow = ValidRoute[0][PresentPosition[0].RouteListIndex].Direction;
            int positionNow = ValidRoute[0][PresentPosition[0].RouteListIndex].TCSectionIndex;

            bool[] nextPart = UpdateRouteActions(0);

            if (!nextPart[0]) return (returnValue);   // not at end and not to attach to anything

            returnValue[0] = true; // end of path reached
            if (nextPart[1])   // next route available
            {
#if DEBUG_REPORTS
                File.AppendAllText(@"C:\temp\printproc.txt", "Train " +
                     Number.ToString() + " continued, part : " + TCRoute.activeSubpath.ToString() + "\n");
#endif
                if (CheckTrain)
                {
                    File.AppendAllText(@"C:\temp\checktrain.txt", "Train " +
                         Number.ToString() + " continued, part : " + TCRoute.activeSubpath.ToString() + "\n");
                }

                if (positionNow == PresentPosition[0].TCSectionIndex && directionNow != PresentPosition[0].TCDirection)
                {
                    ReverseFormation(false);

#if DEBUG_REPORTS
                    File.AppendAllText(@"C:\temp\printproc.txt", "Train " +
                         Number.ToString() + " reversed\n");
#endif
                }
                else if (positionNow == PresentPosition[1].TCSectionIndex && directionNow != PresentPosition[1].TCDirection)
                {
                    ReverseFormation(false);

#if DEBUG_REPORTS
                    File.AppendAllText(@"C:\temp\printproc.txt", "Train " +
                         Number.ToString() + " reversed\n");
#endif
                }

                if (CheckTrain)
                {
                    File.AppendAllText(@"C:\temp\checktrain.txt", "Train " +
                         Number.ToString() + " reversed\n");
                }

                // check if next station was on previous subpath - if so, move to this subpath

                if (StationStops.Count > 0)
                {
                    StationStop thisStation = StationStops[0];

                    if (thisStation.Passed)
                    {
                        StationStops.RemoveAt(0);
                    }
                    else if (thisStation.SubrouteIndex < TCRoute.activeSubpath)
                    {
                        thisStation.SubrouteIndex = TCRoute.activeSubpath;

                        if (ValidRoute[0].GetRouteIndex(thisStation.TCSectionIndex, 0) < 0) // station no longer on route
                        {
                            if (thisStation.ExitSignal >= 0 && thisStation.HoldSignal && HoldingSignals.Contains(thisStation.ExitSignal))
                            {
                                HoldingSignals.Remove(thisStation.ExitSignal);
                            }
                            StationStops.RemoveAt(0);
                        }
                    }
                }

                // reset to node control, also reset required actions

                SwitchToNodeControl(-1);
                ResetActions(true);

            }
            else
            {
                // check if train is to form new train
                // note : if formed train == 0, formed train is player train which requires different actions

                if (Forms > 0)
                {
                    // check if anything needs be detached
                    if (DetachDetails.Count > 0)
                    {
                        for (int iDetach = DetachDetails.Count - 1; iDetach >= 0; iDetach--)
                        {
                            DetachInfo thisDetach = DetachDetails[iDetach];
                            if (thisDetach.DetachPosition == DetachInfo.DetachPositionInfo.atEnd)
                            {
                                thisDetach.Detach(this, presentTime);
                                DetachDetails.RemoveAt(iDetach);
                            }
                        }
                    }

                    bool autogenStart = false;
                    // get train which is to be formed
                    AITrain formedTrain = AI.StartList.GetNotStartedTrainByNumber(Forms, true);

                    if (formedTrain == null)
                    {
                        formedTrain = Simulator.GetAutoGenTrainByNumber(Forms);
                        autogenStart = true;
                    }

                    // if found - start train
                    if (formedTrain != null)
                    {
                        // remove existing train
                        Forms = -1;
                        RemoveTrain();

                        // set details for new train from existing train
                        bool validFormed = formedTrain.StartFromAITrain(this, presentTime);

#if DEBUG_TRACEINFO
                        Trace.TraceInformation("{0} formed into {1}", Name, formedTrain.Name);
#endif

                        if (validFormed)
                        {
                            // start new train
                            if (!autogenStart)
                            {
                                AI.Simulator.StartReference.Remove(formedTrain.Number);
                            }
                            formedTrain.TrainType = Train.TRAINTYPE.AI;
                            MovementState = AI_MOVEMENT_STATE.AI_STATIC;
                            formedTrain.SetFormedOccupied();
                            AI.TrainsToAdd.Add(formedTrain);
                        }
                        else if (!autogenStart)
                        {
                            // reinstate as to be started (note : train is not yet removed from reference)
                            AI.StartList.InsertTrain(formedTrain);
                        }
                    }

                    returnValue[1] = false;
                    return (returnValue);
                }

                // check if train is to remain as static
                else if (FormsStatic)
                {
                    MovementState = AI_MOVEMENT_STATE.AI_STATIC;
                    ControlMode = TRAIN_CONTROL.UNDEFINED;
                    StartTime = null;  // set starttime to invalid
                    return (returnValue);
                }

#if DEBUG_REPORTS
                File.AppendAllText(@"C:\temp\printproc.txt", "Train " +
                     Number.ToString() + " removed\n");
#endif
                if (CheckTrain)
                {
                    File.AppendAllText(@"C:\temp\checktrain.txt", "Train " +
                         Number.ToString() + " removed\n");
                }
                RemoveTrain();
                returnValue[1] = false;
                return (returnValue);
            }
            return (returnValue);
        }

        public bool CheckCouplePosition(Train attachTrain, out bool thisTrainFront, out bool otherTrainFront)
        {
            thisTrainFront = true;
            otherTrainFront = true;

            Traveller usedTraveller = new Traveller(FrontTDBTraveller);
            int usePosition = 0;

            if (MUDirection == Direction.Reverse)
            {
                usedTraveller = new Traveller(RearTDBTraveller, Traveller.TravellerDirection.Backward); // use in direction of movement
                thisTrainFront = false;
                usePosition = 1;
            }

            Traveller otherTraveller = null;
            int useOtherPosition = 0;
            bool withinSection = true;

            // Check if train is in same section as other train, either for the other trains front or rear
            if (PresentPosition[usePosition].TCSectionIndex == attachTrain.PresentPosition[0].TCSectionIndex) // train in same section as front
            {
                withinSection = true;
            }
            else if (PresentPosition[usePosition].TCSectionIndex == attachTrain.PresentPosition[1].TCSectionIndex) // train in same section as rear
            {
                useOtherPosition = 1;
                withinSection = true;
            }

            if (!withinSection) // not yet in same section
            {
                return (false);
            }

            // test directions
            if (PresentPosition[usePosition].TCDirection == attachTrain.PresentPosition[useOtherPosition].TCDirection) // trains are in same direction
            {
                if (usePosition == 1)
                {
                    otherTraveller = new Traveller(attachTrain.FrontTDBTraveller);
                }
                else
                {
                    otherTraveller = new Traveller(attachTrain.RearTDBTraveller);
                    otherTrainFront = false;
                }
            }
            else
            {
                if (usePosition == 1)
                {
                    otherTraveller = new Traveller(attachTrain.RearTDBTraveller);
                    otherTrainFront = false;
                }
                else
                {
                    otherTraveller = new Traveller(attachTrain.FrontTDBTraveller);
                }
            }

            if (PreUpdate) return (true); // in pre-update, being in the same section is good enough

            // check distance to other train
            float dist = usedTraveller.OverlapDistanceM(otherTraveller, false);
            return (dist < 0.1f);
        }

        public void CoupleAI(Train attachTrain, bool thisTrainFront, bool attachTrainFront)
        {
            // stop train
            SpeedMpS = 0;
            AdjustControlsThrottleOff();
            physicsUpdate(0);

            // check on reverse formation
            if (thisTrainFront == attachTrainFront)
            {
                ReverseFormation(false);
            }

            var attachCar = Cars[0];

            // attach to front of waiting train
            if (attachTrainFront)
            {
                attachCar = Cars[Cars.Count - 1];
                for (int iCar = Cars.Count - 1; iCar >= 0; iCar--)
                {
                    var car = Cars[iCar];
                    car.Train = attachTrain;
                    car.CarID = String.Copy(attachTrain.Name);
                    attachTrain.Cars.Insert(0, car);
                }
                //<CSComment this should be a bug; now corrected only when Enhanced flag on
                if (!Simulator.TimetableMode && Simulator.Settings.EnhancedActCompatibility && attachTrain.LeadLocomotiveIndex >= 0)
                    attachTrain.LeadLocomotiveIndex += Cars.Count ;
            }
            // attach to rear of waiting train
            else
            {
                foreach (var car in Cars)
                {
                    car.Train = attachTrain;
                    car.CarID = String.Copy(attachTrain.Name);
                    attachTrain.Cars.Add(car);
                }
            }

            // remove cars from this train
            Cars.Clear();
            attachTrain.Length += Length;

            // recalculate position of formed train
            if (attachTrainFront)  // coupled to front, so rear position is still valid
            {
                attachTrain.CalculatePositionOfCars(0);
                DistanceTravelledM += Length;
            }
            else // coupled to rear so front position is still valid
            {
                attachTrain.RepositionRearTraveller();    // fix the rear traveller
                attachTrain.CalculatePositionOfCars(0);
            }

            // update positions train
            TrackNode tn = attachTrain.FrontTDBTraveller.TN;
            float offset = attachTrain.FrontTDBTraveller.TrackNodeOffset;
            int direction = (int)attachTrain.FrontTDBTraveller.Direction;

            attachTrain.PresentPosition[0].SetTCPosition(tn.TCCrossReference, offset, direction);
            attachTrain.PresentPosition[0].CopyTo(ref attachTrain.PreviousPosition[0]);

            attachTrain.DistanceTravelledM = 0.0f;

            tn = attachTrain.RearTDBTraveller.TN;
            offset = attachTrain.RearTDBTraveller.TrackNodeOffset;
            direction = (int)attachTrain.RearTDBTraveller.Direction;

            attachTrain.PresentPosition[1].SetTCPosition(tn.TCCrossReference, offset, direction);

            // remove train from track and clear actions
            attachTrain.RemoveFromTrack();
            attachTrain.ClearActiveSectionItems();

            // set new track sections occupied
            Train.TCSubpathRoute tempRouteTrain = signalRef.BuildTempRoute(attachTrain, attachTrain.PresentPosition[1].TCSectionIndex,
                attachTrain.PresentPosition[1].TCOffset, attachTrain.PresentPosition[1].TCDirection, attachTrain.Length, false, true, false);

            for (int iIndex = 0; iIndex < tempRouteTrain.Count; iIndex++)
            {
                TrackCircuitSection thisSection = signalRef.TrackCircuitList[tempRouteTrain[iIndex].TCSectionIndex];
                thisSection.SetOccupied(attachTrain.routedForward);
            }

            // set various items
            attachTrain.CheckFreight();
            attachCar.SignalEvent(Event.Couple);

            if (MovementState != AI_MOVEMENT_STATE.AI_STATIC)
            {
                InitializeSignals(true);
            }

            InitializeBrakes();
            attachTrain.physicsUpdate(0);   // stop the wheels from moving etc

            // remove original train
            RemoveTrain();
        }

        //================================================================================================//
        /// <summary>
        /// Remove train
        /// <\summary>

        public void RemoveTrain()
        {
            RemoveFromTrack();
            ClearDeadlocks();

            // if train was to form another train, ensure this other train is started by removing the formed link
            if (Forms >= 0)
            {
                AITrain formedTrain = AI.StartList.GetNotStartedTrainByNumber(Forms, true);
                if (formedTrain != null)
                {
                    formedTrain.FormedOf = -1;
                    formedTrain.FormedOfType = FormCommand.None;
                }
            }

#if DEBUG_DEADLOCK
            foreach (TrackCircuitSection thisSection in Simulator.Signals.TrackCircuitList)
            {
                if (thisSection.DeadlockTraps.Count > 0)
                {
                    File.AppendAllText(@"C:\Temp\deadlock.txt", "Section : " + thisSection.Index.ToString() + "\n");
                    foreach (KeyValuePair<int, List<int>> thisDeadlock in thisSection.DeadlockTraps)
                    {
                        File.AppendAllText(@"C:\Temp\deadlock.txt", "    Train : " + thisDeadlock.Key.ToString() + "\n");
                        File.AppendAllText(@"C:\Temp\deadlock.txt", "       With : " + "\n");
                        foreach (int otherTrain in thisDeadlock.Value)
                        {
                            File.AppendAllText(@"C:\Temp\deadlock.txt", "          " + otherTrain.ToString() + "\n");
                        }
                    }
                }
            }
#endif
            // remove train

            AI.TrainsToRemove.Add(this);
        }

        //================================================================================================//
        /// <summary>
        /// Insert action item
        /// <\summary>

        public void CreateTrainAction(float presentSpeedMpS, float reqSpeedMpS, float distanceToTrainM,
                ObjectItemInfo thisItem, AIActionItem.AI_ACTION_TYPE thisAction)
        {
            // if signal or speed limit take off clearing distance

            float activateDistanceTravelledM = PresentPosition[0].DistanceTravelledM + distanceToTrainM;
            if (thisItem != null)
            {
                activateDistanceTravelledM -= clearingDistanceM;
            }

            // calculate braking distance

            float firstPartTime = 0.0f;
            float firstPartRangeM = 0.0f;
            float secndPartRangeM = 0.0f;
            float remainingRangeM = activateDistanceTravelledM - PresentPosition[0].DistanceTravelledM;

            float triggerDistanceM = PresentPosition[0].DistanceTravelledM; // worst case

            // braking distance based on max speed - use 0.25 * MaxDecelMpSS as average deceleration (due to braking delay)
            // T = deltaV / A
            float fullPartTime = (AllowedMaxSpeedMpS - reqSpeedMpS) / (0.25f * MaxDecelMpSS);
            // R = 0.5 * Vstart * T + 0.5 * A * T**2 
            // 0.5 * Vstart is average speed over used time, 0.5 * Vstart * T is related distance covered , 0.5 A T**2 is distance covered to reduce speed
            float fullPartRangeM = (0.5f * 0.25f * MaxDecelMpSS * fullPartTime * fullPartTime) + ((AllowedMaxSpeedMpS - reqSpeedMpS) * 0.5f * fullPartTime);

            if (presentSpeedMpS > reqSpeedMpS)   // if present speed higher, brake distance is always required (same equation)
            {
                firstPartTime = (presentSpeedMpS - reqSpeedMpS) / (0.25f * MaxDecelMpSS);
                firstPartRangeM = (0.5f * 0.25f * MaxDecelMpSS * firstPartTime * firstPartTime) + ((presentSpeedMpS - reqSpeedMpS) * 0.5f * fullPartTime);
            }

            if (firstPartRangeM > remainingRangeM)
            {
                triggerDistanceM = activateDistanceTravelledM - firstPartRangeM;
            }

                // if brake from max speed is possible taking into account acc up to full speed, use it as braking distance
            else if (fullPartRangeM < (remainingRangeM - ((AllowedMaxSpeedMpS - reqSpeedMpS) * (AllowedMaxSpeedMpS - reqSpeedMpS) * 0.5 / MaxAccelMpSS)))
            {
                triggerDistanceM = activateDistanceTravelledM - fullPartRangeM;
            }

            // if distance from max speed is too long and from present speed too short and train not at max speed,
            // remaining distance calculation :
            // max. time to reach allowed max speed : Tacc = (Vmax - Vnow) / MaxAccel
            // max. time to reduce speed from max back to present speed : Tdec = (Vmax - Vnow) / 0.25 * MaxDecel
            // convered distance : R = Vnow*(Tacc + Tdec) + 0.5 * MaxAccel * Tacc**2 + 0.5 * 0*25 * MaxDecel * Tdec**2
            else
            {
                secndPartRangeM = 0;
                if (SpeedMpS < presentSpeedMpS)
                {
                    float Tacc = (presentSpeedMpS - SpeedMpS) / MaxAccelMpSS;
                    float Tdec = (presentSpeedMpS - SpeedMpS) / 0.25f * MaxDecelMpSS;
                    secndPartRangeM = (SpeedMpS * (Tacc + Tdec)) + (0.5f * MaxAccelMpSS * (Tacc * Tacc)) + (0.5f * 0.25f * MaxDecelMpSS * (Tdec * Tdec));
                }
                //<CSComment: here sometimes triggerDistanceM becomes negative.
                triggerDistanceM = activateDistanceTravelledM - (firstPartRangeM + secndPartRangeM);
            }

            // create and insert action

            AIActionItem newAction = new AIActionItem(triggerDistanceM, reqSpeedMpS, activateDistanceTravelledM,
                    DistanceTravelledM, thisItem, thisAction);

            requiredActions.InsertAction(newAction);

#if DEBUG_REPORTS
            if (thisItem != null && thisItem.ObjectType == ObjectItemInfo.ObjectItemType.Signal)
            {
                File.AppendAllText(@"C:\temp\printproc.txt", "Insert for train " +
                         Number.ToString() + ", type " +
                         thisAction.ToString() + " for signal " +
                         thisItem.ObjectDetails.thisRef.ToString() + ", at " +
                         FormatStrings.FormatDistance(activateDistanceTravelledM, true) + ", trigger at " +
                         FormatStrings.FormatDistance(triggerDistanceM, true) + " (now at " +
                         FormatStrings.FormatDistance(PresentPosition[0].DistanceTravelledM, true) + ")\n");
            }
            else
            {
                File.AppendAllText(@"C:\temp\printproc.txt", "Insert for train " +
                         Number.ToString() + ", type " +
                         thisAction.ToString() + " at " +
                         FormatStrings.FormatDistance(activateDistanceTravelledM, true) + ", trigger at " +
                         FormatStrings.FormatDistance(triggerDistanceM, true) + " (now at " +
                         FormatStrings.FormatDistance(PresentPosition[0].DistanceTravelledM, true) + ")\n");
            }
#endif
            if (CheckTrain)
            {
                if (thisItem != null && thisItem.ObjectType == ObjectItemInfo.ObjectItemType.Signal)
                {
                    File.AppendAllText(@"C:\temp\checktrain.txt", "Insert for train " +
                             Number.ToString() + ", type " +
                             thisAction.ToString() + " for signal " +
                             thisItem.ObjectDetails.thisRef.ToString() + ", at " +
                             FormatStrings.FormatDistance(activateDistanceTravelledM, true) + ", trigger at " +
                             FormatStrings.FormatDistance(triggerDistanceM, true) + " (now at " +
                             FormatStrings.FormatDistance(PresentPosition[0].DistanceTravelledM, true) + ")\n");
                }
                else
                {
                    File.AppendAllText(@"C:\temp\checktrain.txt", "Insert for train " +
                             Number.ToString() + ", type " +
                             thisAction.ToString() + " at " +
                             FormatStrings.FormatDistance(activateDistanceTravelledM, true) + ", trigger at " +
                             FormatStrings.FormatDistance(triggerDistanceM, true) + " (now at " +
                             FormatStrings.FormatDistance(PresentPosition[0].DistanceTravelledM, true) + ")\n");
                }
            }
        }

        //================================================================================================//
        /// <summary>
        /// Insert action item for end-of-route
        /// <\summary>

        public void SetEndOfRouteAction()
        {
            // remaining length first section

            TrackCircuitSection thisSection = signalRef.TrackCircuitList[PresentPosition[0].TCSectionIndex];
            float lengthToGoM = thisSection.Length - PresentPosition[0].TCOffset;
            if (Simulator.TimetableMode || !Simulator.Settings.EnhancedActCompatibility || TCRoute.activeSubpath < TCRoute.TCRouteSubpaths.Count - 1)
            {
                // go through all further sections

                for (int iElement = PresentPosition[0].RouteListIndex + 1; iElement < ValidRoute[0].Count; iElement++)
                {
                    TCRouteElement thisElement = ValidRoute[0][iElement];
                    thisSection = signalRef.TrackCircuitList[thisElement.TCSectionIndex];
                    lengthToGoM += thisSection.Length;
                }
            }
            else lengthToGoM = ComputeDistanceToReversalPoint();
            lengthToGoM -= 5.0f; // keep save distance from end

            // if last section does not end at signal at next section is switch, set back overlap to keep clear of switch
            // only do so for last subroute to avoid falling short of reversal points

            TCRouteElement lastElement = ValidRoute[0][ValidRoute[0].Count - 1];
            TrackCircuitSection lastSection = signalRef.TrackCircuitList[lastElement.TCSectionIndex];
            if (lastSection.EndSignals[lastElement.Direction] == null && TCRoute.activeSubpath == (TCRoute.TCRouteSubpaths.Count - 1) &&
                (Simulator.TimetableMode || !Simulator.Settings.EnhancedActCompatibility) )
            {
                int nextIndex = lastSection.Pins[lastElement.Direction, 0].Link;
                if (nextIndex >= 0)
                {
                    if (signalRef.TrackCircuitList[nextIndex].CircuitType == TrackCircuitSection.TrackCircuitType.Junction)
                    {
                        float lengthCorrection = Math.Max(Convert.ToSingle(signalRef.TrackCircuitList[nextIndex].Overlap), standardOverlapM);
                        if (lastSection.Length - 2 * lengthCorrection < Length) // make sure train fits
                        {
                            lengthCorrection = Math.Max(0.0f, (lastSection.Length - Length) / 2);
                        }
                        lengthToGoM -= lengthCorrection; // correct for stopping position
                    }
                }
            }

            CreateTrainAction(TrainMaxSpeedMpS, 0.0f, lengthToGoM, null,
                    AIActionItem.AI_ACTION_TYPE.END_OF_ROUTE);
            NextStopDistanceM = lengthToGoM;
        }

        //================================================================================================//
        /// <summary>
        /// Reset action list
        /// <\summary>

        public void ResetActions(bool setEndOfPath)
        {
#if DEBUG_REPORTS
            if (nextActionInfo != null)
            {
                File.AppendAllText(@"C:\temp\printproc.txt", "Reset all for train " +
                         Number.ToString() + ", type " +
                         nextActionInfo.NextAction.ToString() + ", at " +
                         FormatStrings.FormatDistance(nextActionInfo.ActivateDistanceM, true) + ", trigger at " +
                         FormatStrings.FormatDistance(nextActionInfo.RequiredDistance, true) + " (now at " +
                         FormatStrings.FormatDistance(PresentPosition[0].DistanceTravelledM, true) + " - " +
                         FormatStrings.FormatSpeed(SpeedMpS, true) + ")\n");
            }
            else
            {
                File.AppendAllText(@"C:\temp\printproc.txt", "Reset all for train " +
                         Number.ToString() + " (now at " +
                         FormatStrings.FormatDistance(PresentPosition[0].DistanceTravelledM, true) + " - " +
                         FormatStrings.FormatSpeed(SpeedMpS, true) + ")\n");
            }
#endif
            if (CheckTrain)
            {
                if (nextActionInfo != null)
                {
                    File.AppendAllText(@"C:\temp\checktrain.txt", "Reset all for train " +
                             Number.ToString() + ", type " +
                             nextActionInfo.NextAction.ToString() + ", at " +
                             FormatStrings.FormatDistance(nextActionInfo.ActivateDistanceM, true) + ", trigger at " +
                             FormatStrings.FormatDistance(nextActionInfo.RequiredDistance, true) + " (now at " +
                             FormatStrings.FormatDistance(PresentPosition[0].DistanceTravelledM, true) + " - " +
                             FormatStrings.FormatSpeed(SpeedMpS, true) + ")\n");
                }
                else
                {
                    File.AppendAllText(@"C:\temp\checktrain.txt", "Reset all for train " +
                             Number.ToString() + " (now at " +
                             FormatStrings.FormatDistance(PresentPosition[0].DistanceTravelledM, true) + " - " +
                             FormatStrings.FormatSpeed(SpeedMpS, true) + ")\n");
                }
            }

            nextActionInfo = null;
            foreach (ObjectItemInfo thisInfo in SignalObjectItems)
            {
                thisInfo.processed = false;
            }
            requiredActions.RemovePendingAIActionItems(false);

#if NEW_ACTION
            if (AuxActions != null)
            {
                SetAuxAction();
            }
#endif
            if (StationStops.Count > 0)
                SetNextStationAction();
            if (setEndOfPath)
            {
                SetEndOfRouteAction();
            }
            // to allow re-inserting of reversal action if necessary (enhanced compatibility flag on)
            if (TCRoute.ReversalInfo[TCRoute.activeSubpath].ReversalActionInserted == true)
                TCRoute.ReversalInfo[TCRoute.activeSubpath].ReversalActionInserted = false;

        }

        //================================================================================================//
        /// <summary>
        /// Perform stored actions
        /// <\summary>

        public override void PerformActions(List<DistanceTravelledItem> nowActions)
        {
            foreach (var thisAction in nowActions)
            {
                if (thisAction is ClearSectionItem)
                {
                    ClearOccupiedSection(thisAction as ClearSectionItem);
                }
                else if (thisAction is ActivateSpeedLimit)
                {
                    SetAIPendingSpeedLimit(thisAction as ActivateSpeedLimit);
                }
#if NEW_ACTION
                else if (thisAction is AIActionItem && !(thisAction is AuxActionItem))
                {
                    ProcessActionItem(thisAction as AIActionItem);
                }
                else if (thisAction is AuxActionItem)
                {
                    ((AuxActionItem)thisAction).ValidAction(this);
                }
#else
                else if (thisAction is AIActionItem)
                {
                    ProcessActionItem(thisAction as AIActionItem);
                }
#endif
            }
        }

        //================================================================================================//
        /// <summary>
        /// Set pending speed limits
        /// <\summary>

        public void SetAIPendingSpeedLimit(ActivateSpeedLimit speedInfo)
        {
            if (speedInfo.MaxSpeedMpSSignal > 0)
            {
                allowedMaxSpeedSignalMpS = speedInfo.MaxSpeedMpSSignal;
                AllowedMaxSpeedMpS = Math.Min(allowedMaxSpeedLimitMpS, speedInfo.MaxSpeedMpSSignal);
            }
            if (speedInfo.MaxSpeedMpSLimit > 0)
            {
                allowedMaxSpeedLimitMpS = speedInfo.MaxSpeedMpSLimit;
                AllowedMaxSpeedMpS = speedInfo.MaxSpeedMpSLimit;
            }
            // <CScomment> following statement should be valid in general, as it seems there was a bug here in the original SW
            if (!Program.Simulator.TimetableMode && Program.Simulator.Settings.EnhancedActCompatibility) AllowedMaxSpeedMpS = Math.Min(AllowedMaxSpeedMpS, TrainMaxSpeedMpS);

            if (MovementState == AI_MOVEMENT_STATE.RUNNING && SpeedMpS < AllowedMaxSpeedMpS - 2.0f * hysterisMpS)
            {
                MovementState = AI_MOVEMENT_STATE.ACCELERATING;
                Alpha10 = 10;
            }

#if DEBUG_REPORTS
            File.AppendAllText(@"C:\temp\printproc.txt", "Train " + Number + " Validated speedlimit : " +
               "Limit : " + allowedMaxSpeedLimitMpS.ToString() + " ; " +
               "Signal : " + allowedMaxSpeedSignalMpS.ToString() + " ; " +
               "Overall : " + AllowedMaxSpeedMpS.ToString() + "\n");

#endif

            // reset pending actions to recalculate braking distance

            ResetActions(true);
        }

        //================================================================================================//
        /// <summary>
        /// Process pending actions
        /// <\summary>

        public void ProcessActionItem(AIActionItem thisItem)
        {
            // normal actions

            bool actionValid = true;
            bool actionCleared = false;

#if DEBUG_REPORTS
            if (thisItem.ActiveItem != null && thisItem.ActiveItem.ObjectType == ObjectItemInfo.ObjectItemType.Signal)
            {
                File.AppendAllText(@"C:\temp\printproc.txt", "Activated for train " +
                         Number.ToString() + ", type " +
                         thisItem.NextAction.ToString() + " for signal " +
                         thisItem.ActiveItem.ObjectDetails.thisRef.ToString() + ", at " +
                         FormatStrings.FormatDistance(thisItem.ActivateDistanceM, true) + ", trigger at " +
                         FormatStrings.FormatDistance(thisItem.RequiredDistance, true) + " (now at " +
                         FormatStrings.FormatDistance(PresentPosition[0].DistanceTravelledM, true) + " - " +
                         FormatStrings.FormatSpeed(SpeedMpS, true) + ")\n");
            }
            else
            {
                File.AppendAllText(@"C:\temp\printproc.txt", "Activated for train " +
                         Number.ToString() + ", type " +
                         thisItem.NextAction.ToString() + " at " +
                         FormatStrings.FormatDistance(thisItem.ActivateDistanceM, true) + ", trigger at " +
                         FormatStrings.FormatDistance(thisItem.RequiredDistance, true) + " (now at " +
                         FormatStrings.FormatDistance(PresentPosition[0].DistanceTravelledM, true) + " - " +
                         FormatStrings.FormatSpeed(SpeedMpS, true) + ")\n");
            }
#endif
            if (CheckTrain)
            {
                if (thisItem.ActiveItem != null && thisItem.ActiveItem.ObjectType == ObjectItemInfo.ObjectItemType.Signal)
                {
                    File.AppendAllText(@"C:\temp\checktrain.txt", "Activated for train " +
                             Number.ToString() + ", type " +
                             thisItem.NextAction.ToString() + " for signal " +
                             thisItem.ActiveItem.ObjectDetails.thisRef.ToString() + ", at " +
                             FormatStrings.FormatDistance(thisItem.ActivateDistanceM, true) + ", trigger at " +
                             FormatStrings.FormatDistance(thisItem.RequiredDistance, true) + " (now at " +
                             FormatStrings.FormatDistance(PresentPosition[0].DistanceTravelledM, true) + " - " +
                             FormatStrings.FormatSpeed(SpeedMpS, true) + ")\n");
                }
                else
                {
                    File.AppendAllText(@"C:\temp\checktrain.txt", "Activated for train " +
                             Number.ToString() + ", type " +
                             thisItem.NextAction.ToString() + " at " +
                             FormatStrings.FormatDistance(thisItem.ActivateDistanceM, true) + ", trigger at " +
                             FormatStrings.FormatDistance(thisItem.RequiredDistance, true) + " (now at " +
                             FormatStrings.FormatDistance(PresentPosition[0].DistanceTravelledM, true) + " - " +
                             FormatStrings.FormatSpeed(SpeedMpS, true) + ")\n");
                }
            }

            // if signal speed, check if still set
 #region signal_check
            if (thisItem.NextAction == AIActionItem.AI_ACTION_TYPE.SPEED_SIGNAL)
            {
                if (thisItem.ActiveItem.actual_speed == AllowedMaxSpeedMpS)  // no longer valid
                {
                    actionValid = false;
                }
                else if (thisItem.ActiveItem.actual_speed != thisItem.RequiredSpeedMpS)
                {
                    actionValid = false;
                }
            }

            // if signal, check if not held for station stop (station stop comes first)

            else if (thisItem.NextAction == AIActionItem.AI_ACTION_TYPE.SIGNAL_ASPECT_STOP)
            {
                if (thisItem.ActiveItem.signal_state == MstsSignalAspect.STOP &&
                    thisItem.ActiveItem.ObjectDetails.holdState == SignalObject.HoldState.StationStop)
                {
                    actionValid = false;

#if DEBUG_REPORTS
                    File.AppendAllText(@"C:\temp\printproc.txt", "Train " +
                            Number.ToString() + " : signal " +
                            thisItem.ActiveItem.ObjectDetails.thisRef.ToString() + " at " +
                            FormatStrings.FormatDistance(thisItem.ActivateDistanceM, true) + " is held for station stop\n");
#endif
                    if (CheckTrain)
                    {
                        File.AppendAllText(@"C:\temp\checktrain.txt", "Train " +
                                Number.ToString() + " : signal " +
                                thisItem.ActiveItem.ObjectDetails.thisRef.ToString() + " at " +
                                FormatStrings.FormatDistance(thisItem.ActivateDistanceM, true) + " is held for station stop\n");
                    }

                }

            // check if cleared

                else if (thisItem.ActiveItem.signal_state >= MstsSignalAspect.APPROACH_1)
                {
                    actionValid = false;
                    actionCleared = true;

#if DEBUG_REPORTS
                    File.AppendAllText(@"C:\temp\printproc.txt", "Train " +
                            Number.ToString() + " : signal " +
                            thisItem.ActiveItem.ObjectDetails.thisRef.ToString() + " at " +
                            FormatStrings.FormatDistance(thisItem.ActivateDistanceM, true) + " cleared\n");
#endif
                    if (CheckTrain)
                    {
                        File.AppendAllText(@"C:\temp\checktrain.txt", "Train " +
                                Number.ToString() + " : signal " +
                                thisItem.ActiveItem.ObjectDetails.thisRef.ToString() + " at " +
                                FormatStrings.FormatDistance(thisItem.ActivateDistanceM, true) + " cleared\n");
                    }
                }

            // check if restricted

                else if (thisItem.ActiveItem.signal_state != MstsSignalAspect.STOP)
                {
                    thisItem.NextAction = AIActionItem.AI_ACTION_TYPE.SIGNAL_ASPECT_RESTRICTED;
                    if ((thisItem.ActivateDistanceM - PresentPosition[0].DistanceTravelledM) < signalApproachDistanceM)
                    {
                        actionValid = false;
                        actionCleared = true;

#if DEBUG_REPORTS
                        File.AppendAllText(@"C:\temp\printproc.txt", "Train " +
                            Number.ToString() + " : signal " +
                            thisItem.ActiveItem.ObjectDetails.thisRef.ToString() + " at " +
                            FormatStrings.FormatDistance(thisItem.ActivateDistanceM, true) + " set to RESTRICTED\n");
#endif
                        if (CheckTrain)
                        {
                            File.AppendAllText(@"C:\temp\checktrain.txt", "Train " +
                                Number.ToString() + " : signal " +
                                thisItem.ActiveItem.ObjectDetails.thisRef.ToString() + " at " +
                                FormatStrings.FormatDistance(thisItem.ActivateDistanceM, true) + " set to RESTRICTED\n");
                        }
                    }
                }

                // recalculate braking distance if train is running slow
                if (actionValid && SpeedMpS < creepSpeedMpS)
                {
                    float firstPartTime = 0.0f;
                    float firstPartRangeM = 0.0f;
                    float secndPartRangeM = 0.0f;
                    float remainingRangeM = thisItem.ActivateDistanceM - PresentPosition[0].DistanceTravelledM;

                    if (SpeedMpS > thisItem.RequiredSpeedMpS)   // if present speed higher, brake distance is always required
                    {
                        firstPartTime = (SpeedMpS - thisItem.RequiredSpeedMpS) / (0.25f * MaxDecelMpSS);
                        firstPartRangeM = 0.25f * MaxDecelMpSS * (firstPartTime * firstPartTime);
                    }

                    if (firstPartRangeM < remainingRangeM && SpeedMpS < TrainMaxSpeedMpS) // if distance left and not at max speed
                    // split remaining distance based on relation between acceleration and deceleration
                    {
                        secndPartRangeM = (remainingRangeM - firstPartRangeM) * (2.0f * MaxDecelMpSS) / (MaxDecelMpSS + MaxAccelMpSS);
                    }

                    float fullRangeM = firstPartRangeM + secndPartRangeM;
                    if (fullRangeM < remainingRangeM && remainingRangeM > 300.0f) // if range is shorter and train not too close, reschedule
                    {
                        actionValid = false;
                        thisItem.RequiredDistance = thisItem.ActivateDistanceM - fullRangeM;
                        requiredActions.InsertAction(thisItem);

#if DEBUG_REPORTS
                        File.AppendAllText(@"C:\temp\printproc.txt", "Rescheduled for train " +
                             Number.ToString() + ", type " +
                             thisItem.NextAction.ToString() + " for signal " +
                             thisItem.ActiveItem.ObjectDetails.thisRef.ToString() + ", at " +
                             FormatStrings.FormatDistance(thisItem.ActivateDistanceM, true) + ", trigger at " +
                             FormatStrings.FormatDistance(thisItem.RequiredDistance, true) + " (now at " +
                             FormatStrings.FormatDistance(PresentPosition[0].DistanceTravelledM, true) + " - " +
                             FormatStrings.FormatSpeed(SpeedMpS, true) + ")\n");
#endif
                        if (CheckTrain)
                        {
                            File.AppendAllText(@"C:\temp\checktrain.txt", "Rescheduled for train " +
                                 Number.ToString() + ", type " +
                                 thisItem.NextAction.ToString() + " for signal " +
                                 thisItem.ActiveItem.ObjectDetails.thisRef.ToString() + ", at " +
                                 FormatStrings.FormatDistance(thisItem.ActivateDistanceM, true) + ", trigger at " +
                                 FormatStrings.FormatDistance(thisItem.RequiredDistance, true) + " (now at " +
                                 FormatStrings.FormatDistance(PresentPosition[0].DistanceTravelledM, true) + " - " +
                                 FormatStrings.FormatSpeed(SpeedMpS, true) + ")\n");
                        }
                    }

                }
            }

    // if signal at RESTRICTED, check if not cleared

            else if (thisItem.NextAction == AIActionItem.AI_ACTION_TYPE.SIGNAL_ASPECT_RESTRICTED)
            {
                if (thisItem.ActiveItem.signal_state >= MstsSignalAspect.APPROACH_1 ||
                (thisItem.ActivateDistanceM - PresentPosition[0].DistanceTravelledM) < signalApproachDistanceM)
                {
                    actionValid = false;

#if DEBUG_REPORTS
                    File.AppendAllText(@"C:\temp\printproc.txt", "Train " +
                            Number.ToString() + " : signal " +
                            thisItem.ActiveItem.ObjectDetails.thisRef.ToString() + " at " +
                            FormatStrings.FormatDistance(thisItem.ActivateDistanceM, true) + " cleared\n");
#endif
                    if (CheckTrain)
                    {
                        File.AppendAllText(@"C:\temp\checktrain.txt", "Train " +
                                Number.ToString() + " : signal " +
                                thisItem.ActiveItem.ObjectDetails.thisRef.ToString() + " at " +
                                FormatStrings.FormatDistance(thisItem.ActivateDistanceM, true) + " cleared\n");
                    }
                }
            }

    // get station stop, recalculate with present speed if required

            else if (thisItem.NextAction == AIActionItem.AI_ACTION_TYPE.STATION_STOP)
            {
                float[] distancesM = CalculateDistancesToNextStation(StationStops[0], SpeedMpS, true);

                if (distancesM[1] - 300.0f > DistanceTravelledM) // trigger point more than 300m away
                {
                    actionValid = false;
                    thisItem.RequiredDistance = distancesM[1];
                    thisItem.ActivateDistanceM = distancesM[0];
                    requiredActions.InsertAction(thisItem);


#if DEBUG_REPORTS
                    File.AppendAllText(@"C:\temp\printproc.txt", "StationStop rescheduled for train " +
                        Number.ToString() + ", at " +
                        FormatStrings.FormatDistance(thisItem.ActivateDistanceM, true) + ", trigger at " +
                        FormatStrings.FormatDistance(thisItem.RequiredDistance, true) + " ( now at " +
                        FormatStrings.FormatDistance(PresentPosition[0].DistanceTravelledM, true) + " - " +
                        FormatStrings.FormatSpeed(SpeedMpS, true) + ")\n");
#endif
                    if (CheckTrain)
                    {
                        File.AppendAllText(@"C:\temp\checktrain.txt", "StationStop rescheduled for train " +
                            Number.ToString() + ", at " +
                            FormatStrings.FormatDistance(thisItem.ActivateDistanceM, true) + ", trigger at " +
                            FormatStrings.FormatDistance(thisItem.RequiredDistance, true) + " ( now at " +
                            FormatStrings.FormatDistance(PresentPosition[0].DistanceTravelledM, true) + " - " +
                            FormatStrings.FormatSpeed(SpeedMpS, true) + ")\n");
                    }
                }
            }

            EndProcessAction(actionValid, thisItem, actionCleared);
        }
#endregion

        //  SPA:    To be able to call it by AuxActionItems
        public void EndProcessAction (bool actionValid, AIActionItem thisItem, bool actionCleared)
        {
            // if still valid - check if at station and signal is exit signal
            // if so, use minimum distance of both items to ensure train stops in time for signal

            if (actionValid && nextActionInfo != null &&
                nextActionInfo.NextAction == AIActionItem.AI_ACTION_TYPE.STATION_STOP &&
                thisItem.NextAction == AIActionItem.AI_ACTION_TYPE.SIGNAL_ASPECT_STOP)
            {
                int signalIdent = thisItem.ActiveItem.ObjectDetails.thisRef;
                if (signalIdent == StationStops[0].ExitSignal)
                {
                    actionValid = false;
                    nextActionInfo.ActivateDistanceM = Math.Min(nextActionInfo.ActivateDistanceM, thisItem.ActivateDistanceM);
#if DEBUG_REPORTS
                    if (StationStops[0].ActualStopType == StationStop.STOPTYPE.STATION_STOP)
                    {
                        File.AppendAllText(@"C:\temp\checktrain.txt", "Rejected : Train " +
                             Number.ToString() + " : signal " +
                             signalIdent.ToString() + " is exit signal for " +
                             StationStops[0].PlatformItem.Name + "\n");
                    }
                    else if (StationStops[0].ActualStopType == StationStop.STOPTYPE.WAITING_POINT)
                    {
                        File.AppendAllText(@"C:\temp\checktrain.txt", "Rejected : Train " +
                             Number.ToString() + " : signal " +
                             signalIdent.ToString() + " is exit signal for Waiting Point \n");
                    }
#endif
                    if (CheckTrain)
                    {
                        if (StationStops[0].ActualStopType == StationStop.STOPTYPE.STATION_STOP)
                        {
                            File.AppendAllText(@"C:\temp\checktrain.txt", "Rejected : Train " +
                                 Number.ToString() + " : signal " +
                                 signalIdent.ToString() + " is exit signal for " +
                                 StationStops[0].PlatformItem.Name + "\n");
                        }
                        else if (StationStops[0].ActualStopType == StationStop.STOPTYPE.WAITING_POINT)
                        {
                            File.AppendAllText(@"C:\temp\checktrain.txt", "Rejected : Train " +
                                 Number.ToString() + " : signal " +
                                 signalIdent.ToString() + " is exit signal for Waiting Point \n");
                        }
                    }
                }
            }

            // if still valid - check if more severe as existing action

#region more_severe
            if (actionValid)
            {
                if (nextActionInfo != null)
                {
                    bool earlier = false;

                    if (thisItem.ActivateDistanceM < nextActionInfo.ActivateDistanceM)
                    {
                        if (thisItem.RequiredSpeedMpS <= nextActionInfo.RequiredSpeedMpS)
                        {
                            earlier = true;
                        }
                        else  // new requirement earlier with higher speed - check if enough braking distance remaining
                        {
                            float deltaTime = (thisItem.RequiredSpeedMpS - nextActionInfo.RequiredSpeedMpS) / MaxDecelMpSS;
                            float brakingDistanceM = (thisItem.RequiredSpeedMpS * deltaTime) + (0.5f * MaxDecelMpSS * deltaTime * deltaTime);

                            if (brakingDistanceM < (nextActionInfo.ActivateDistanceM - thisItem.ActivateDistanceM))
                            {
                                earlier = true;
                            }
                        }
                    }
                    else if (thisItem.RequiredSpeedMpS < nextActionInfo.RequiredSpeedMpS)
                    // new requirement further but with lower speed - check if enough braking distance left
                    {
                        float deltaTime = (nextActionInfo.RequiredSpeedMpS - thisItem.RequiredSpeedMpS) / MaxDecelMpSS;
                        float brakingDistanceM = (nextActionInfo.RequiredSpeedMpS * deltaTime) + (0.5f * MaxDecelMpSS * deltaTime * deltaTime);

                        if (brakingDistanceM > (thisItem.ActivateDistanceM - nextActionInfo.ActivateDistanceM))
                        {
                            earlier = true;
                        }
                    }

                    // if earlier : check if present action is station stop, new action is signal - if so, check is signal really in front of or behind station stop

                    if (earlier && thisItem.NextAction == AIActionItem.AI_ACTION_TYPE.SIGNAL_ASPECT_STOP &&
                                 nextActionInfo.NextAction == AIActionItem.AI_ACTION_TYPE.STATION_STOP)
                    {
                        float newposition = thisItem.ActivateDistanceM + 0.75f * clearingDistanceM; // correct with clearing distance - leave smaller gap
                        float actposition = nextActionInfo.ActivateDistanceM;

                        if (actposition < newposition) earlier = false;

                        if (!earlier && CheckTrain)
                        {
                            File.AppendAllText(@"C:\temp\checktrain.txt", "allowing minimum gap : " + newposition.ToString() + " and " + actposition.ToString() + "\n");
                        }
                    }

                    // if not earlier and station stop and present action is signal stop : check if signal is hold signal, if so set station stop
                    // set distance to signal if that is less than distance to platform to ensure trains stops at signal

                    if (!earlier && thisItem.NextAction == AIActionItem.AI_ACTION_TYPE.STATION_STOP &&
                               nextActionInfo.NextAction == AIActionItem.AI_ACTION_TYPE.SIGNAL_ASPECT_STOP)
                    {
                        if (HoldingSignals.Contains(nextActionInfo.ActiveItem.ObjectDetails.thisRef))
                        {
                            earlier = true;
                            thisItem.ActivateDistanceM = Math.Min(nextActionInfo.ActivateDistanceM, thisItem.ActivateDistanceM);
                        }
                    }

                    // reject if less severe (will be rescheduled if active item is cleared)

                    if (!earlier)
                    {
                        actionValid = false;

#if DEBUG_REPORTS
                        File.AppendAllText(@"C:\temp\printproc.txt", "Rejected : Train " +
                             Number.ToString() + " : this " +
                             FormatStrings.FormatSpeed(thisItem.RequiredSpeedMpS, true) + " at " +
                             FormatStrings.FormatDistance(thisItem.ActivateDistanceM, true) + ", active " +
                             FormatStrings.FormatSpeed(nextActionInfo.RequiredSpeedMpS, true) + " at " +
                             FormatStrings.FormatDistance(nextActionInfo.ActivateDistanceM, true) + "\n");
#endif
                        if (CheckTrain)
                        {
                            File.AppendAllText(@"C:\temp\checktrain.txt", "Rejected : Train " +
                                 Number.ToString() + " : this " +
                                 FormatStrings.FormatSpeed(thisItem.RequiredSpeedMpS, true) + " at " +
                                 FormatStrings.FormatDistance(thisItem.ActivateDistanceM, true) + ", active " +
                                 FormatStrings.FormatSpeed(nextActionInfo.RequiredSpeedMpS, true) + " at " +
                                 FormatStrings.FormatDistance(nextActionInfo.ActivateDistanceM, true) + "\n");
                        }
                    }
                    else
                    {
#if DEBUG_REPORTS
                        File.AppendAllText(@"C:\temp\printproc.txt", "Accepted : Train " +
                             Number.ToString() + " : this " +
                             FormatStrings.FormatSpeed(thisItem.RequiredSpeedMpS, true) + " at " +
                             FormatStrings.FormatDistance(thisItem.ActivateDistanceM, true) + ", active " +
                             FormatStrings.FormatSpeed(nextActionInfo.RequiredSpeedMpS, true) + " at " +
                             FormatStrings.FormatDistance(nextActionInfo.ActivateDistanceM, true) + "\n");
#endif
                        if (CheckTrain)
                        {
                            File.AppendAllText(@"C:\temp\checktrain.txt", "Accepted : Train " +
                                 Number.ToString() + " : this " +
                                 FormatStrings.FormatSpeed(thisItem.RequiredSpeedMpS, true) + " at " +
                                 FormatStrings.FormatDistance(thisItem.ActivateDistanceM, true) + ", active " +
                                 FormatStrings.FormatSpeed(nextActionInfo.RequiredSpeedMpS, true) + " at " +
                                 FormatStrings.FormatDistance(nextActionInfo.ActivateDistanceM, true) + "\n");
                        }
                    }
                }
            }
#endregion

            // if still valid, set as action, set state to braking if still running

            if (actionValid)
            {
#if DEBUG_REPORTS
                File.AppendAllText(@"C:\temp\printproc.txt", "Validated\n");
#endif
                if (CheckTrain)
                {
                    File.AppendAllText(@"C:\temp\checktrain.txt", "Validated\n");
                }
                nextActionInfo = thisItem;
                if (nextActionInfo.RequiredSpeedMpS == 0)
                {
                    NextStopDistanceM = thisItem.ActivateDistanceM - PresentPosition[0].DistanceTravelledM;
                    if (AI.PreUpdate)
                    {
                        AITrainBrakePercent = 100; // because of short reaction time
                        AITrainThrottlePercent = 0;
                    }
                }

#if DEBUG_REPORTS
                File.AppendAllText(@"C:\temp\printproc.txt", "Train " + Number.ToString() +
                " , Present state : " + MovementState.ToString() + "\n");

#endif

                if (MovementState != AI_MOVEMENT_STATE.STATION_STOP && MovementState != AI_MOVEMENT_STATE.STOPPED && MovementState != AI_MOVEMENT_STATE.HANDLE_ACTION)
                {
                    MovementState = AI_MOVEMENT_STATE.BRAKING;
                    Alpha10 = 10;
#if DEBUG_REPORTS
                    File.AppendAllText(@"C:\temp\printproc.txt", "Train " + Number.ToString() +
                    " , new state : " + MovementState.ToString() + "\n");
#endif
                    if (CheckTrain)
                    {
                        File.AppendAllText(@"C:\temp\checktrain.txt", "Train " + Number.ToString() +
                        " , new state : " + MovementState.ToString() + "\n");
                    }
                }
                else
                {
#if DEBUG_REPORTS
                    File.AppendAllText(@"C:\temp\printproc.txt", "Train " + Number.ToString() +
                    " , unchanged \n");
#endif
                    if (CheckTrain)
                    {
                        File.AppendAllText(@"C:\temp\checktrain.txt", "Train " + Number.ToString() +
                        " , unchanged \n");
                    }
                }
            }
            else
            {
#if DEBUG_REPORTS
                File.AppendAllText(@"C:\temp\printproc.txt", "Action Rejected\n");
#endif
                if (CheckTrain)
                {
                    File.AppendAllText(@"C:\temp\checktrain.txt", "Action Rejected\n");
                }
            }

            if (actionCleared)
            {
#if DEBUG_REPORTS
                File.AppendAllText(@"C:\temp\printproc.txt", "Action Cleared\n");
#endif
                if (CheckTrain)
                {
                    File.AppendAllText(@"C:\temp\checktrain.txt", "Action Cleared\n");
                }
                // reset actions - ensure next action is validated

                ResetActions(true);
            }
        }

        public bool TrainHasPower()
        {
            foreach (var car in Cars)
            {
                if (car is MSTSLocomotive)
                {
                    return (true);
                }
            }

            return (false);
        }

        //================================================================================================//
        //
        // Extra actions when alternative route is set
        //

        public override void SetAlternativeRoute_pathBased(int startElementIndex, int altRouteIndex, SignalObject nextSignal)
        {
            base.SetAlternativeRoute_pathBased(startElementIndex, altRouteIndex, nextSignal);

            // reset actions to recalculate distances

            ResetActions(true);
        }

        public override void SetAlternativeRoute_locationBased(int startSectionIndex, DeadlockInfo sectionDeadlockInfo, int usedPath, SignalObject nextSignal)
        {
            base.SetAlternativeRoute_locationBased(startSectionIndex, sectionDeadlockInfo, usedPath, nextSignal);

            // reset actions to recalculate distances

            ResetActions(true);
        }

        //================================================================================================//
        /// <summary>
        /// Add movement status to train status string
        /// Update the string for 'TextPageDispatcherInfo' in case of AI train.
        /// Modifiy fields 4, 5, 7, 8 & 11
        /// 4   AIMode :
        ///     INI     : AI is in INIT mode
        ///     STC     : AI is static
        ///     STP     : AI is Stopped
        ///     BRK     : AI Brakes
        ///     ACC     : AI do acceleration
        ///     FOL     : AI follows
        ///     RUN     : AI is running
        ///     EOP     : AI approch and of path
        ///     STA     : AI is on Station Stop
        ///     WTP     : AI is on Waiting Point
        /// 5   AI Data :
        ///     000&000     : for mode INI, BRK, ACC, FOL, RUN or EOP
        ///     HH:mm:ss    : for mode STA or WTP with actualDepart or DepartTime
        ///                 : for mode STC with Start Time Value
        ///     ..:..:..    : For other case
        /// 7   Next Action : 
        ///     SPDL    :   Speed limit
        ///     SIGL    :   Speed signal
        ///     STOP    :   Signal STOP
        ///     REST    :   Signal RESTRICTED
        ///     EOA     :   End Of Authority
        ///     STAT    :   Station Stop
        ///     TRAH    :   Train Ahead
        ///     EOR     :   End Of Route
        ///     NONE    :   None
        /// 8   Distance :
        ///     Distance to
        /// 11  Train Name
        /// <\summary>

        public String[] AddMovementState(String[] stateString, bool metric)
        {
            String[] retString = new String[stateString.Length];
            stateString.CopyTo(retString, 0);

            string movString = "";
            switch (MovementState)
            {
                case AI_MOVEMENT_STATE.INIT:
                    movString = "INI ";
                    break;
                case AI_MOVEMENT_STATE.AI_STATIC:
                    movString = "STC ";
                    break;
                case AI_MOVEMENT_STATE.STOPPED:
                    movString = "STP ";
                    break;
                case AI_MOVEMENT_STATE.STATION_STOP:
                    break;   // set below
                case AI_MOVEMENT_STATE.BRAKING:
                    movString = "BRK ";
                    break;
                case AI_MOVEMENT_STATE.ACCELERATING:
                    movString = "ACC ";
                    break;
                case AI_MOVEMENT_STATE.FOLLOWING:
                    movString = "FOL ";
                    break;
                case AI_MOVEMENT_STATE.RUNNING:
                    movString = "RUN ";
                    break;
                case AI_MOVEMENT_STATE.APPROACHING_END_OF_PATH:
                    movString = "EOP ";
                    break;
            }

            string abString = AITrainThrottlePercent.ToString("000");
            abString = String.Concat(abString, "&", AITrainBrakePercent.ToString("000"));

            if (MovementState == AI_MOVEMENT_STATE.STATION_STOP)
            {
                DateTime baseDT = new DateTime();
                if (StationStops[0].DepartTime > 0)
                {
                    DateTime depTime = baseDT.AddSeconds(StationStops[0].DepartTime);
                    abString = depTime.ToString("HH:mm:ss");
                }
                else if (StationStops[0].ActualDepart > 0)
                {
                    DateTime depTime = baseDT.AddSeconds(StationStops[0].ActualDepart);
                    abString = depTime.ToString("HH:mm:ss");
                }
                else
                {
                    abString = "..:..:..";
                }

                if (StationStops[0].ActualStopType == StationStop.STOPTYPE.STATION_STOP)
                {
                    movString = "STA";
                }
                else if (StationStops[0].ActualStopType == StationStop.STOPTYPE.WAITING_POINT)
                {
                    movString = "WTP";
                }
            }
            else if (MovementState == AI_MOVEMENT_STATE.AI_STATIC)
            {
                if (StartTime.HasValue)
                {
                    long startNSec = (long)(StartTime.Value * Math.Pow(10, 7));
                    DateTime startDT = new DateTime(startNSec);
                    abString = startDT.ToString("HH:mm:ss");
                }
                else
                {
                    abString = "--------";
                }
            }

            string nameString = Name.Substring(0, Math.Min(Name.Length, 7));

            string actString = "";

            if (nextActionInfo != null)
            {
                switch (nextActionInfo.NextAction)
                {
                    case AIActionItem.AI_ACTION_TYPE.SPEED_LIMIT:
                        actString = "SPDL";
                        break;
                    case AIActionItem.AI_ACTION_TYPE.SPEED_SIGNAL:
                        actString = "SIGL";
                        break;
                    case AIActionItem.AI_ACTION_TYPE.SIGNAL_ASPECT_STOP:
                        actString = "STOP";
                        break;
                    case AIActionItem.AI_ACTION_TYPE.SIGNAL_ASPECT_RESTRICTED:
                        actString = "REST";
                        break;
                    case AIActionItem.AI_ACTION_TYPE.END_OF_AUTHORITY:
                        actString = "EOA ";
                        break;
                    case AIActionItem.AI_ACTION_TYPE.STATION_STOP:
                        actString = "STAT";
                        break;
                    case AIActionItem.AI_ACTION_TYPE.TRAIN_AHEAD:
                        actString = "TRAH";
                        break;
                    case AIActionItem.AI_ACTION_TYPE.END_OF_ROUTE:
                        actString = "EOR ";
                        break;
                    case AIActionItem.AI_ACTION_TYPE.NONE:
                        actString = "NONE";
                        break;
                }

                retString[7] = String.Copy(actString);
                retString[8] = FormatStrings.FormatDistance(
                        nextActionInfo.ActivateDistanceM - PresentPosition[0].DistanceTravelledM, metric);

            }

            retString[4] = String.Copy(movString);
            retString[5] = String.Copy(abString);
            retString[11] = String.Copy(nameString);

            return (retString);
        }

#if WITH_PATH_DEBUG 
        //================================================================================================//
        /// <summary>
        /// AddPathInfo:  Used to construct a single line for path debug in HUD Windows
        /// <\summary>

        public String[] AddPathInfo(String[] stateString, bool metric)
        {
            String[] retString = this.TCRoute.GetTCRouteInfo(stateString, PresentPosition[0]);

            retString[1] = currentAIState;
            return (retString);
        }

        public String[] GetActionStatus(bool metric)
        {
            int iColumn = 0;

            string[] statusString = new string[2];

            //  "Train"
            statusString[0] = Number.ToString();
            iColumn++;

            //  "Action"
            statusString[1] = "Actions: ";
            foreach (var action in requiredActions)
            {
                statusString[1] = String.Concat(statusString[1], showActionInfo(action));
            }
            statusString[1] = String.Concat(statusString[1], "NextAction->", showActionInfo(nextActionInfo));
            return statusString;
        }

        String showActionInfo(Train.DistanceTravelledItem action)
        {
            string actionString = String.Empty;
            //actionString = string.Concat(actionString, "Actions:");
            if (action == null)
                return "";

            if (action.GetType() == typeof(ClearSectionItem))
            {
                ClearSectionItem TrainAction = action as ClearSectionItem;
                //actionString = String.Concat(actionString, " ClearSection(", action.RequiredDistance.ToString("F0"), "):");
                actionString = String.Concat(actionString, " CLR Section(", TrainAction.TrackSectionIndex, "):");
            }
            else if (action.GetType() == typeof(ActivateSpeedLimit))
            {
                actionString = String.Concat(actionString, " ASL(", action.RequiredDistance.ToString("F0"), "m):");
            }
#if NEW_ACTION
            else if (action.GetType() == typeof(AIActionItem) || action.GetType().IsSubclassOf(typeof(AuxActionItem)))
#else
            else if (action.GetType() == typeof(AIActionItem))
#endif
            {
                AIActionItem AIaction = action as AIActionItem;
                {
                    switch (AIaction.NextAction)
                    {
                        case AIActionItem.AI_ACTION_TYPE.END_OF_AUTHORITY:
                            actionString = String.Concat(actionString, " EOA(", NextStopDistanceM.ToString("F0"), "m):");
                            break;
                        case AIActionItem.AI_ACTION_TYPE.END_OF_ROUTE:
                            actionString = String.Concat(actionString, " EOR(", NextStopDistanceM.ToString("F0"), "m):");
                            break;
                        case AIActionItem.AI_ACTION_TYPE.REVERSAL:
                            actionString = String.Concat(actionString, " REV(", NextStopDistanceM.ToString("F0"), "m):");
                            break;
                        case AIActionItem.AI_ACTION_TYPE.SIGNAL_ASPECT_RESTRICTED:
                            actionString = String.Concat(actionString, " SAR(", NextStopDistanceM.ToString("F0"), "m):");
                            break;
                        case AIActionItem.AI_ACTION_TYPE.SIGNAL_ASPECT_STOP:
                            string infoSignal = "";
                            if (AIaction.ActiveItem.ObjectType == ObjectItemInfo.ObjectItemType.Signal)
                            {
                                infoSignal = AIaction.ActiveItem.signal_state.ToString();
                                infoSignal = String.Concat(infoSignal, ",", AIaction.ActiveItem.ObjectDetails.blockState.ToString());
                            }
                            actionString = String.Concat(actionString, " SAS(", infoSignal, "):");
                            break;
                        case AIActionItem.AI_ACTION_TYPE.SPEED_LIMIT:
                            actionString = String.Concat(actionString, " SL(", NextStopDistanceM.ToString("F0"), "m):");
                            break;
                        case AIActionItem.AI_ACTION_TYPE.SPEED_SIGNAL:
                            actionString = String.Concat(actionString, " Speed(", NextStopDistanceM.ToString("F0"), "m):");
                            break;
                        case AIActionItem.AI_ACTION_TYPE.STATION_STOP:
                            actionString = String.Concat(actionString, " StationStop(", NextStopDistanceM.ToString("F0"), "m):");
                            break;
                        case AIActionItem.AI_ACTION_TYPE.TRAIN_AHEAD:
                            float diff = AIaction.ActivateDistanceM - AIaction.InsertedDistanceM;
                            actionString = String.Concat(actionString, " TrainAhead(", NextStopDistanceM.ToString("F0"), "m):");
                            break;
                        case AIActionItem.AI_ACTION_TYPE.NONE:
                            actionString = String.Concat(actionString, " None(", NextStopDistanceM.ToString("F0"), "m):");
                            break;
#if NEW_ACTION
                        case AIActionItem.AI_ACTION_TYPE.AUX_ACTION:
                            string coord = String.Concat("X:", this.FrontTDBTraveller.X.ToString(), ", Z:", this.FrontTDBTraveller.Z.ToString());
                            actionString = String.Concat(actionString, AIaction.AsString(this), NextStopDistanceM.ToString("F0"), "m):", coord);
                            //actionString = String.Concat(actionString, " AUX(", NextStopDistanceM.ToString("F0"), "m):");
                            break;
#endif
                    }
                }
            }

            return (actionString);
        }
#endif
    }


    //================================================================================================//
    /// <summary>
    /// AIActionItem class : class to hold info on next restrictive action
    /// <\summary>


    public class AIActionItem : Train.DistanceTravelledItem
    {
        public float RequiredSpeedMpS;
        public float ActivateDistanceM;
        public float InsertedDistanceM;
        public ObjectItemInfo ActiveItem;

        public enum AI_ACTION_TYPE
        {
            SPEED_LIMIT,
            SPEED_SIGNAL,
            SIGNAL_ASPECT_STOP,
            SIGNAL_ASPECT_RESTRICTED,
            END_OF_AUTHORITY,
            STATION_STOP,
            TRAIN_AHEAD,
            END_OF_ROUTE,
            REVERSAL,
            AUX_ACTION,
            NONE
        }

        public AI_ACTION_TYPE NextAction = AI_ACTION_TYPE.NONE;

        //================================================================================================//
        /// <summary>
        /// constructor for AIActionItem
        /// </summary>

        public AIActionItem(float distance, float requiredSpeedMpS, float activateDistance, float insertedDistance,
            ObjectItemInfo thisItem, AI_ACTION_TYPE thisAction)
        {
            RequiredDistance = distance;
            RequiredSpeedMpS = requiredSpeedMpS;
            ActivateDistanceM = activateDistance;
            InsertedDistanceM = insertedDistance;
            ActiveItem = thisItem;
            NextAction = thisAction;

        }

        //================================================================================================//
        //
        // Restore
        //

        public AIActionItem(BinaryReader inf, Signals signalRef)
        {
            RequiredDistance = inf.ReadSingle();
            RequiredSpeedMpS = inf.ReadSingle();
            ActivateDistanceM = inf.ReadSingle();
            InsertedDistanceM = inf.ReadSingle();

            bool validActiveItem = inf.ReadBoolean();

            if (validActiveItem)
            {
                ActiveItem = RestoreActiveItem(inf, signalRef);
            }

            NextAction = (AI_ACTION_TYPE)inf.ReadInt32();
        }

        public static ObjectItemInfo RestoreActiveItem(BinaryReader inf, Signals signalRef)
        {

            ObjectItemInfo thisInfo = new ObjectItemInfo(ObjectItemInfo.ObjectItemFindState.None);

            thisInfo.ObjectType = (ObjectItemInfo.ObjectItemType)inf.ReadInt32();
            thisInfo.ObjectState = (ObjectItemInfo.ObjectItemFindState)inf.ReadInt32();

            int signalIndex = inf.ReadInt32();
            thisInfo.ObjectDetails = signalRef.SignalObjects[signalIndex];

            thisInfo.distance_found = inf.ReadSingle();
            thisInfo.distance_to_train = inf.ReadSingle();
            thisInfo.distance_to_object = inf.ReadSingle();

            thisInfo.speed_passenger = inf.ReadSingle();
            thisInfo.speed_freight = inf.ReadSingle();
            thisInfo.speed_flag = inf.ReadUInt32();
            thisInfo.actual_speed = inf.ReadSingle();

            thisInfo.processed = inf.ReadBoolean();

            thisInfo.signal_state = MstsSignalAspect.UNKNOWN;
            if (thisInfo.ObjectDetails.isSignal)
            {
                thisInfo.signal_state = thisInfo.ObjectDetails.this_sig_lr(MstsSignalFunction.NORMAL);
            }

            return (thisInfo);
        }

        //================================================================================================//
        //
        // Save
        //

        public void SaveItem(BinaryWriter outf)
        {
            outf.Write(RequiredSpeedMpS);
            outf.Write(ActivateDistanceM);
            outf.Write(InsertedDistanceM);

            if (ActiveItem == null)
            {
                outf.Write(false);
            }
            else
            {
                outf.Write(true);
                SaveActiveItem(outf, ActiveItem);
            }

            outf.Write((int)NextAction);
        }

        public static void SaveActiveItem(BinaryWriter outf, ObjectItemInfo ActiveItem)
        {
            outf.Write((int)ActiveItem.ObjectType);
            outf.Write((int)ActiveItem.ObjectState);

            outf.Write(ActiveItem.ObjectDetails.thisRef);

            outf.Write(ActiveItem.distance_found);
            outf.Write(ActiveItem.distance_to_train);
            outf.Write(ActiveItem.distance_to_object);

            outf.Write(ActiveItem.speed_passenger);
            outf.Write(ActiveItem.speed_freight);
            outf.Write(ActiveItem.speed_flag);
            outf.Write(ActiveItem.actual_speed);

            outf.Write(ActiveItem.processed);
        }
#if NEW_ACTION
        //================================================================================================//
        //
        //  Generic Handler for all derived class
        //
        public virtual bool ValidAction(AITrain thisTrain)
        {
            return false;
        }

        public virtual AITrain.AI_MOVEMENT_STATE InitAction(AITrain thisTrain, int presentTime, AITrain.AI_MOVEMENT_STATE movementState)
        {
            return movementState;
        }

        public virtual AITrain.AI_MOVEMENT_STATE EndAction(AITrain thisTrain, int presentTime, AITrain.AI_MOVEMENT_STATE movementState)
        {
            return movementState;
        }

        public virtual AITrain.AI_MOVEMENT_STATE HandleAction(AITrain thisTrain, int presentTime, AITrain.AI_MOVEMENT_STATE movementState)
        {
            return movementState;
        }

        public virtual AITrain.AI_MOVEMENT_STATE ProcessAction(AITrain thisTrain, int presentTime, float elapsedClockSeconds, AITrain.AI_MOVEMENT_STATE movementState)
        {
            return movementState;
        }

        public virtual string AsString(AITrain thisTrain)
        {
            return " ??(";
        }

#endif
    }

}
