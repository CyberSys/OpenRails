﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MSTS.Parsers;
using ORTS.Scripting.Api;

namespace ORTS
{
    public class Pantographs
    {
        readonly MSTSWagon Wagon;

        public List<Pantograph> List = new List<Pantograph>();

        public Pantographs(MSTSWagon wagon)
        {
            Wagon = wagon;
        }

        public void Parse(string lowercasetoken, STFReader stf)
        {

        }

        public void Copy(Pantographs pantographs)
        {
            List.Clear();

            foreach (Pantograph pantograph in pantographs.List)
            {
                List.Add(new Pantograph(Wagon));
                List.Last().Copy(pantograph);
            }
        }

        public void Initialize()
        {
            foreach (Pantograph pantograph in List)
            {
                pantograph.Initialize();
            }
        }

        public void Update(float elapsedClockSeconds)
        {
            foreach (Pantograph pantograph in List)
            {
                pantograph.Update(elapsedClockSeconds);
            }
        }

        public void HandleEvent(PowerSupplyEvent evt)
        {
            foreach (Pantograph pantograph in List)
            {
                pantograph.HandleEvent(evt);
            }
        }

        public void HandleEvent(PowerSupplyEvent evt, int id)
        {
            if (id <= List.Count)
            {
                List[id - 1].HandleEvent(evt);
            }
        }

        #region ListManipulation

        public void Add(Pantograph pantograph)
        {
            List.Add(pantograph);
        }

        public int Count { get { return List.Count; } }

        public Pantograph this[int i]
        {
            get { return List[i - 1]; }
            set { List[i - 1] = value; }
        }

        #endregion

        public PantographState State
        {
            get
            {
                PantographState state = PantographState.Down;

                foreach (Pantograph pantograph in List)
                {
                    if (pantograph.State > state)
                        state = pantograph.State;
                }

                return state;
            }
        }
    }

    public class Pantograph
    {
        readonly MSTSWagon Wagon;

        public PantographState State { get; private set; }
        public float DelayS { get; private set; }
        public float TimeS { get; private set; }
        public bool CommandUp {
            get
            {
                bool value;

                switch (State)
                {
                    default:
                    case PantographState.Down:
                    case PantographState.Lowering:
                        value = false;
                        break;

                    case PantographState.Up:
                    case PantographState.Raising:
                        value = true;
                        break;
                }

                return value;
            }
        }

        public Pantograph(MSTSWagon wagon)
        {
            Wagon = wagon;

            State = PantographState.Down;
            DelayS = 0;
            TimeS = 0;
        }

        public void Parse(string lowercasetoken, STFReader stf)
        {

        }

        public void Copy(Pantograph pantograph)
        {
            DelayS = pantograph.DelayS;
            TimeS = pantograph.TimeS;
        }

        public void Initialize()
        {

        }

        public void Update(float elapsedClockSeconds)
        {
            switch (State)
            {
                case PantographState.Lowering:
                    TimeS -= elapsedClockSeconds;

                    if (TimeS < 0)
                    {
                        TimeS = 0;
                        State = PantographState.Down;
                    }
                    break;

                case PantographState.Raising:
                    TimeS += elapsedClockSeconds;

                    if (TimeS > DelayS)
                    {
                        TimeS = DelayS;
                        State = PantographState.Up;
                    }
                    break;
            }
        }

        public void HandleEvent(PowerSupplyEvent evt)
        {
            switch (evt)
            {
                case PowerSupplyEvent.LowerPantograph:
                    if (State == PantographState.Up || State == PantographState.Raising)
                    {
                        State = PantographState.Lowering;
                    }
                    break;

                case PowerSupplyEvent.RaisePantograph:
                    if (State == PantographState.Down || State == PantographState.Lowering)
                    {
                        State = PantographState.Raising;
                    }
                    break;
            }
        }
    }
}
