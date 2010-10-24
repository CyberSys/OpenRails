﻿/// COPYRIGHT 2009 by the Open Rails project.
/// This code is provided to enable you to contribute improvements to the open rails program.  
/// Use of the code for any other purpose or distribution of the code to anyone else
/// is prohibited without specific written permission from admin@openrails.org.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MSTS
{
    public class TRKFile
    {
        public TRKFile(string filename)
        {
            using (STFReader f = new STFReader(filename))
            try
            {
                string token = f.ReadItem();
                while (token != "") // EOF
                {
                    if (token == "(") f.SkipBlock();
                    else if (0 == String.Compare(token, "Tr_RouteFile", true)) Tr_RouteFile = new Tr_RouteFile(f);
                    else if (0 == String.Compare(token, "_OpenRails", true)) ORTRKData = new ORTRKData(f);
                    else f.SkipBlock();
                    token = f.ReadItem();
                }
                if (Tr_RouteFile == null) throw new STFException(f, "Missing Tr_RouteFile");
            }
            finally
            {
                if (ORTRKData == null)
                    ORTRKData = new ORTRKData();
            }
        }
        public Tr_RouteFile Tr_RouteFile;
        public ORTRKData ORTRKData = null;
    }

    public class ORTRKData
    {
        public float MaxViewingDistance = float.MaxValue;  // disables local route override

        public ORTRKData()
        {
        }

        public ORTRKData(STFReader f)
        {
            f.MustMatch("(");
            while (!f.EndOfBlock())
                switch (f.ReadItem().ToLower())
                {
                    case "maxviewingdistance": MaxViewingDistance = f.ReadFloatBlock(); break;
                    case "(": f.SkipRestOfBlock(); break;
                }
        }
    }

    public class Tr_RouteFile
    {
        public Tr_RouteFile(STFReader f)
        {
            f.MustMatch("(");
            string token = f.ReadItem();
            while (token != ")")
            {
				if (token == "") throw new STFException(f, "Missing )");
				else if (0 == String.Compare(token, "RouteID", true)) RouteID = f.ReadStringBlock();
				else if (0 == String.Compare(token, "Name", true)) Name = f.ReadStringBlock();
				else if (0 == String.Compare(token, "FileName", true)) FileName = f.ReadStringBlock();
				else if (0 == String.Compare(token, "Description", true)) Description = f.ReadStringBlock();
                else if (0 == String.Compare(token, "MaxLineVoltage", true)) MaxLineVoltage = f.ReadDoubleBlock();
                else if (0 == String.Compare(token, "RouteStart", true) && RouteStart == null) RouteStart = new RouteStart(f); // take only the first - ignore any others
				else if (0 == String.Compare(token, "Environment", true)) Environment = new TRKEnvironment(f);
				else if (0 == String.Compare(token, "MilepostUnitsKilometers", true)) MilepostUnitsMetric = true;
				else f.SkipBlock();
                token = f.ReadItem();
            }
            if (RouteID == null) throw new STFException(f, "Missing RouteID");
            if (Name == null) throw new STFException(f, "Missing Name");
            if (Description == null) throw new STFException(f, "Missing Description");
            if (RouteStart == null) throw new STFException(f, "Missing RouteStart");
        }
        public string RouteID;  // ie JAPAN1  - used for TRK file and route folder name
        public string FileName; // ie OdakyuSE - used for MKR,RDB,REF,RIT,TDB,TIT
        public string Name;
        public string Description;
        public RouteStart RouteStart;
        public TRKEnvironment Environment;
		public bool MilepostUnitsMetric = false;
        public double MaxLineVoltage = 0;
    }


    public class RouteStart
    {
        public RouteStart(STFReader f)
        {
            f.MustMatch("(");
            WX = f.ReadDouble(STFReader.UNITS.Any, null);   // tilex
            WZ = f.ReadDouble(STFReader.UNITS.Any, null);   // tilez
            X = f.ReadDouble(STFReader.UNITS.Any, null);
            Z = f.ReadDouble(STFReader.UNITS.Any, null);
            while (f.ReadItem() != ")") ; // discard extra parameters - users frequently describe location here
        }
        public double WX, WZ, X, Z;
    }

    public class TRKEnvironment
    {
        string[] ENVFileNames = new string[12];

        public TRKEnvironment(STFReader f)
        {
            f.MustMatch("(");
            for( int i = 0; i < 12; ++i )
            {
                f.ReadItem();
                f.MustMatch("(");
                ENVFileNames[i] = f.ReadItem();
                f.MustMatch(")");
            }
            f.MustMatch(")");
        }

        public string ENVFileName( SeasonType seasonType, WeatherType weatherType )
        {
            int index = (int)seasonType * 3 + (int)weatherType;
            return ENVFileNames[index];
        }
    }

}
