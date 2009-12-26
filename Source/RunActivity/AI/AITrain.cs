﻿/* AI
 * 
 * Contains code to initialize and control AI trains.
 * Currently, AI trains are created at startup and moved down 1000 meters to make them
 * invisible.  This is done so the rendering code can discover the model it needs to draw.
 * 
 * 
/// COPYRIGHT 2009 by the Open Rails project.
/// This code is provided to enable you to contribute improvements to the open rails program.  
/// Use of the code for any other purpose or distribution of the code to anyone else
/// is prohibited without specific written permission from admin@openrails.org.
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Microsoft.Xna.Framework;
using MSTS;

namespace ORTS
{
    public class AITrain : Train
    {
        public int UiD;
        public AIPath Path = null;
        public AIPathNode RearNode = null;      // path node behind rear of train
        public AIPathNode NextStopNode = null;  // next path node train should stop at
        public AIPathNode AuthEndNode = null;   // end of authorized movement, set by dispatcher 
        public AIPathNode AuthSidingNode = null;// start of siding, take siding if not null
        public float NextStopDistanceM = 0;  // distance to next stop node
        public float NextStopTimeS = 0;      // seconds to next stop
        public float MaxDecelMpSS = 1;  // maximum decelleration
        public float MaxAccelMpSS = .5f;// maximum accelleration
        public float MaxSpeedMpS = 10;  // maximum speed
        public double WaitUntil = 0;    // clock time to wait for before next update
        public AI AI;

        public AITrain(int uid, AI ai, AIPath path)
        {
            UiD = uid;
            AI = ai;
            Path = path;
            NextStopNode = Path.FirstNode;
            RearNode = Path.FirstNode;
        }

        /// <summary>
        /// Update function for a single AI train.
        /// Performs stop processing if a planned stop is made.
        /// Then moves the train, calculates target accelleration and adjusts the controls.
        /// </summary>
        public void AIUpdate( float elapsedClockSeconds, double clockTime)
        {
            if (WaitUntil > clockTime)
                return;
            if (SpeedMpS <= 0 && NextStopDistanceM < .3)
            {
                //Console.WriteLine("stop {0} {1} {2}", NextStopDistanceM, SpeedMpS, NextStopNode.Type);
                SpeedMpS = 0;
                TrainThrottlePercent = 0;
                TrainBrakePercent = 100;
                if (WaitUntil == 0 && HandleNodeAction(NextStopNode, clockTime))
                    return;
                if (NextStopNode.NextMainNode == null && NextStopNode.NextSidingNode == null)
                {
                    NextStopNode = null;
                    return;
                }
                if (NextStopNode == AuthEndNode && !AI.Dispatcher.RequestAuth(this))
                {
                    WaitUntil = clockTime + 60;
                    return;
                }
                if (NextStopNode.IsFacingPoint)
                    Path.AlignSwitch(NextStopNode.JunctionIndex, GetTVNIndex(NextStopNode));
                else
                {
                    AIPathNode prevNode = FindPrevNode(NextStopNode);
                    Path.AlignSwitch(NextStopNode.JunctionIndex, GetTVNIndex(prevNode));
                }
                //Console.WriteLine("auth {0} {1}", AuthEndNode.ID, (AuthSidingNode == null ? "null" : AuthSidingNode.ID.ToString()));
                NextStopNode = FindStopNode(NextStopNode);
                if (NextStopNode == null)
                    return;
                //Console.WriteLine("nextstop {0} {1} {2}", NextStopNode.ID, NextStopNode.Type, NextStopNode.IsFacingPoint);
                WorldLocation wl = NextStopNode.Location;
                if (TrainDirectionForward)
                    NextStopDistanceM = FrontTDBTraveller.DistanceTo(wl.TileX, wl.TileZ, wl.Location.X, wl.Location.Y, wl.Location.Z);
                else
                {
                    TDBTraveller traveller = new TDBTraveller(RearTDBTraveller);
                    traveller.ReverseDirection();
                    NextStopDistanceM = traveller.DistanceTo(wl.TileX, wl.TileZ, wl.Location.X, wl.Location.Y, wl.Location.Z);
                }
                //Console.WriteLine("nextstopdist {0} {1} {2} {3}", NextStopDistanceM, FrontTDBTraveller.Direction, RearTDBTraveller.Direction,
                //    Math.Sqrt(WorldLocation.DistanceSquared(wl,FrontTDBTraveller.WorldLocation)));
                if (NextStopDistanceM < 0 && HandleNodeAction(NextStopNode, clockTime))
                    return;
                NextStopDistanceM -= 1;
                if (NextStopNode.IsFacingPoint == false && NextStopNode.JunctionIndex >= 0)
                {
                    TrackNode tn = Path.TrackDB.TrackNodes[NextStopNode.JunctionIndex];
                    float clearance = 40;
                    if (tn != null && tn.TrJunctionNode != null)
                    {
                        TrackShape shape = Path.TSectionDat.TrackShapes.Get(tn.TrJunctionNode.ShapeIndex);
                        if (shape != null)
                            clearance= 1.5f * (float)shape.ClearanceDistance;
                    }
                    NextStopDistanceM -= clearance;
                }
                //Console.WriteLine("nextstopdist {0}", NextStopDistanceM);
                if (NextStopDistanceM < 0)
                    NextStopDistanceM = 0;
            }
            WaitUntil = 0;
            float timeS = 0;
            
            float prevSpeedMpS = SpeedMpS;
            base.Update( elapsedClockSeconds );
            float dir = TrainDirectionForward ? 1 : -1;
            float distanceM = dir * SpeedMpS * timeS;
            NextStopDistanceM -= distanceM;
            float targetMpSS = CalcAccelMpSS();
            //Console.WriteLine("update {0} {1} {2}", NextStopDistanceM, SpeedMpS, targetMpSS);
            if (timeS > 0)
                AdjustControls(targetMpSS, dir * (SpeedMpS - prevSpeedMpS) / timeS, dir * timeS);
        }

        /// <summary>
        /// Finds the next path node the train should stop at.
        /// </summary>
        private AIPathNode FindStopNode(AIPathNode node)
        {
            if (node.NextMainNode == null && node.NextSidingNode == null)
                return null;
            while (node != AuthEndNode)
            {
                AIPathNode prevNode = node;
                node = GetNextNode(node);
                if (node == null)
                    return node;
                switch (node.Type)
                {
                    case AIPathNodeType.Stop:
                    case AIPathNodeType.Reverse:
                    case AIPathNodeType.Couple:
                    case AIPathNodeType.Uncouple:
                        return node;
                    default:
                        break;
                }
                if (node.IsFacingPoint && !Path.SwitchIsAligned(node.JunctionIndex, GetTVNIndex(node)))
                    return node;
                if (!node.IsFacingPoint && !Path.SwitchIsAligned(node.JunctionIndex, GetTVNIndex(prevNode)))
                    return node;
            }
            return node;
        }

        /// <summary>
        /// Finds the path node before target.
        /// </summary>
        private AIPathNode FindPrevNode(AIPathNode target)
        {
            AIPathNode node1 = RearNode;
            while (node1 != target)
            {
                AIPathNode node2= GetNextNode(node1);
                if (node2 == target)
                    return node1;
                node1 = node2;
            }
            return node1;
        }

        /// <summary>
        /// Performs any special processing based on the type of node.
        /// Returns true if an action was performed.
        /// </summary>
        private bool HandleNodeAction(AIPathNode node, double clockTime)
        {
            switch (node.Type)
            {
                case AIPathNodeType.Stop:
                    WaitUntil = clockTime + node.WaitTimeS;
                    return true;
                case AIPathNodeType.Reverse:
                    TrainDirectionForward = !TrainDirectionForward;
                    WaitUntil = clockTime + 5;
                    return true;
                case AIPathNodeType.Couple:
                    // couple onto the train we should have just hit
                    // TODO add code elsewhere to find train etc.
                    WaitUntil = clockTime + node.WaitTimeS;
                    return true;
                case AIPathNodeType.Uncouple:
                    if (node.NCars > 0 && node.NCars < Cars.Count - 1)
                        AI.Simulator.UncoupleBehind(Cars[node.NCars - 1]);
                    else if (-node.NCars > 0 && -node.NCars < Cars.Count-1)
                    {
                        int n = -node.NCars;
                        // TODO add code to uncouple front end of train and keep rear
                    }
                    // uncouple train
                    WaitUntil = clockTime + node.WaitTimeS;
                    return true;
                default:
                    break;
            }
            return false;
        }

        public AIPathNode GetNextNode(AIPathNode node)
        {
            if (node == AuthSidingNode || node.NextMainNode == null)
                return node.NextSidingNode;
            else
                return node.NextMainNode;
        }
        public int GetTVNIndex(AIPathNode node)
        {
            if (node == AuthSidingNode || node.NextMainNode == null)
                return node.NextSidingTVNIndex;
            else
                return node.NextMainTVNIndex;
        }

        /// <summary>
        /// Calculated the desired acceleration given the distance to the next stop.
        /// </summary>
        private float CalcAccelMpSS()
        {
            float targetMpS = MaxSpeedMpS;
            float stopDistanceM = NextStopDistanceM;
            // adjust stopDistanceM to account for signals etc.
            float maxSpeedSq = targetMpS * targetMpS;
            if (maxSpeedSq > 2 * stopDistanceM * MaxDecelMpSS)
                maxSpeedSq = 2 * stopDistanceM * MaxDecelMpSS;
            float minSpeedSq = .96f * maxSpeedSq;
            if (NextStopTimeS > 0)
            {
                float ssq = (stopDistanceM + maxSpeedSq / (2 * MaxDecelMpSS)) / NextStopTimeS;
                ssq = ssq * ssq;
                if (minSpeedSq > ssq)
                {
                    minSpeedSq = ssq;
                    if (maxSpeedSq > 1.4f * ssq)
                        maxSpeedSq = 1.4f * ssq;
                }
            }
            if (minSpeedSq > (stopDistanceM - .2f) * MaxDecelMpSS)
                minSpeedSq = (stopDistanceM - .2f) * MaxDecelMpSS;
            if (minSpeedSq < 0)
                minSpeedSq = 0;
            float speedSq = SpeedMpS * SpeedMpS;
            if (speedSq > maxSpeedSq && stopDistanceM>0 && 2*stopDistanceM*MaxDecelMpSS<speedSq)
                return -.5f*speedSq/stopDistanceM;
            else if (speedSq > maxSpeedSq)
                return -MaxDecelMpSS;
            else if (speedSq < minSpeedSq)
                return MaxAccelMpSS;
            else
                return 0;
        }

        /// <summary>
        /// Adjusts the train's throttle and brake controls to try to achieve the
        /// desired acceleration.  If the desired acceleration cannot be achieved
        /// using the controls, it is simply added to the speed.
        /// Adjusting the controls is the easiest way to get sound effects etc. to work right.
        /// </summary>
        private void AdjustControls(float targetMpSS, float measMpSS, float timeS)
        {
            if (NextStopDistanceM < 0)
            {
                TrainThrottlePercent = 0;
                TrainBrakePercent = 100;
            }
            if (targetMpSS < 0 && measMpSS > targetMpSS)
            {
                if (TrainThrottlePercent > 0)
                {
                    TrainThrottlePercent -= 10;
                    if (TrainThrottlePercent < 0)
                        TrainThrottlePercent = 0;
                }
                else if (TrainBrakePercent < 100)
                {
                    TrainBrakePercent += 10;
                    if (TrainBrakePercent > 100)
                        TrainBrakePercent = 100;
                }
                else
                {
                    SpeedMpS += timeS * (targetMpSS - measMpSS);
                    //Console.WriteLine("extra {0} {1} {2}", SpeedMpS, targetMpSS, measMpSS);
                }
                //Console.WriteLine("down {0} {1}", TrainThrottlePercent, TrainBrakePercent);
            }
            if (targetMpSS > 0 && measMpSS < targetMpSS)
            {
                if (TrainBrakePercent > 0)
                {
                    TrainBrakePercent -= 10;
                    if (TrainBrakePercent < 0)
                        TrainBrakePercent = 0;
                }
                else if (TrainThrottlePercent < 100)
                {
                    TrainThrottlePercent += 10;
                    if (TrainThrottlePercent > 100)
                        TrainThrottlePercent = 100;
                }
                else
                {
                    SpeedMpS += timeS * (targetMpSS - measMpSS);
                    //Console.WriteLine("extra {0} {1} {2}", SpeedMpS, targetMpSS, measMpSS);
                }
                //Console.WriteLine("up {0} {1}", TrainThrottlePercent, TrainBrakePercent);
            }
        }

        /// <summary>
        /// Called by dispatcher to set movement authorization for train.
        /// end is the path node the train is allowed to move just short of.
        /// if siding is not null the train should enter the siding at the specified node.
        /// In the future the dispatcher might change the authorization while a train is moving.
        /// </summary>
        public bool SetAuthorization(AIPathNode end, AIPathNode siding)
        {
            bool result = AuthEndNode != end;
            AuthEndNode = end;
            AuthSidingNode = siding;
            return result;
        }
    }
}
