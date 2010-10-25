/// COPYRIGHT 2009 by the Open Rails project.
/// This code is provided to enable you to contribute improvements to the open rails program.  
/// Use of the code for any other purpose or distribution of the code to anyone else
/// is prohibited without specific written permission from admin@openrails.org.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using MSTSMath;

namespace MSTS
{
	// GLOBAL TSECTION DAT

	public class SectionCurve
	{
		public SectionCurve()
		{
			Radius = 0;
			Angle = 0;
		}
		public SectionCurve( STFReader f )
		{
			f.MustMatch("(");
            Radius = f.ReadFloat(STFReader.UNITS.Any, null);
            Angle = f.ReadFloat(STFReader.UNITS.Any, null);
			while( f.ReadItem() != ")" );  // MSTS seems to ignore extra params
		}
		public float Radius;	// meters
		public float Angle;	// degrees
	}

	public class SectionSize
	{
		public SectionSize()
		{
			Width = 1.5F;
			Length = 0;
		}
		public SectionSize( STFReader f )
		{
			f.MustMatch("(");
            Width = f.ReadFloat(STFReader.UNITS.Any, null);
            Length = f.ReadFloat(STFReader.UNITS.Any, null);
			while( f.ReadItem() != ")" );  // MSTS seems to ignore extra params
		}
		public float Width;
		public float Length;
	}
	
	public class TrackSection
	{
		public TrackSection()
		{
		}
		
		public TrackSection( STFReader f )
		{
			f.MustMatch("(");
            SectionIndex = f.ReadUInt(STFReader.UNITS.Any, null);
			string token = f.ReadItem();
			while( token != ")" )
			{
                if (token == "") throw new STFException(f, "Missing )");
				else if( 0 == String.Compare( token,"SectionSize",true ) )  SectionSize = new SectionSize( f );
				else if( 0 == String.Compare( token,"SectionCurve", true ) ) SectionCurve = new SectionCurve( f );
				else f.SkipBlock();
				token = f.ReadItem();
			}
			//if( SectionSize == null )
			//	throw( new STFError( f, "Missing SectionSize" ) );
			//  note- default TSECTION.DAT does have some missing sections

		}
		public uint SectionIndex;
		public SectionSize SectionSize;
		public SectionCurve SectionCurve;
	}
	
	public class RouteTrackSection: TrackSection
	{
		public RouteTrackSection( STFReader f )
		{
			f.MustMatch("(");
			f.MustMatch( "SectionCurve" );
			f.MustMatch("(");
			f.ReadItem(); // 0 or 1
			f.SkipRestOfBlock();
            SectionIndex = f.ReadUInt(STFReader.UNITS.Any, null);
			SectionSize = new SectionSize();
            float a = f.ReadFloat(STFReader.UNITS.Any, null);
            float b = f.ReadFloat(STFReader.UNITS.Any, null);
			if( b == 0 )
				// Its straight
			{
				SectionSize.Length = a;
			}
			else
				// its curved
			{
				SectionCurve = new SectionCurve();
				SectionCurve.Radius = b;
				SectionCurve.Angle = (float)M.Degrees( a );
			}
			f.SkipRestOfBlock();

		}
	}

	public class TrackSections: Dictionary<uint, TrackSection>
	{
		public TrackSections( STFReader f )
		{
			f.MustMatch("(");
            MaxSectionIndex = f.ReadUInt(STFReader.UNITS.Any, null);
			string token = f.ReadItem();
			while( token != ")" ) 
			{
                if (token == "") throw new STFException(f, "Missing )");
				else if (0 == String.Compare(token, "TrackSection", true)) {
					AddSection(f, new TrackSection(f));
				} else f.SkipBlock();
				token = f.ReadItem();
			}
		}

		public void AddRouteTrackSections( STFReader f )
		{
			f.MustMatch("(");
            MaxSectionIndex = f.ReadUInt(STFReader.UNITS.Any, null);
			string token = f.ReadItem();
			while( token != ")" ) 
			{
                if (token == "") throw new STFException(f, "Missing )");
				else if (0 == String.Compare(token, "TrackSection", true)) {
					AddSection(f, new RouteTrackSection(f));
				} else f.SkipBlock();
				token = f.ReadItem();
			}
		}
		private void AddSection(STFReader f, TrackSection section) {
			if (ContainsKey(section.SectionIndex)) {
				STFException.ReportError(f, "Duplicate SectionIndex of " + section.SectionIndex);
			}
			this[section.SectionIndex] = section;
		}

        public static int MissingTrackSectionWarnings = 0;

		public TrackSection Get( uint targetSectionIndex )
		{
			if (ContainsKey(targetSectionIndex))
				return this[targetSectionIndex];
			if (MissingTrackSectionWarnings++ < 5)
				Trace.TraceWarning("TDB references track section not listed in global or dynamic TSECTION.DAT: " + targetSectionIndex.ToString());
            return null;
		}
		public uint MaxSectionIndex;
	}
	public class SectionIdx
	{
		public SectionIdx( STFReader f )
		{
			f.MustMatch("(");
            NoSections = f.ReadUInt(STFReader.UNITS.Any, null);
            X = f.ReadDouble(STFReader.UNITS.Any, null);
            Y = f.ReadDouble(STFReader.UNITS.Any, null);
            Z = f.ReadDouble(STFReader.UNITS.Any, null);
            A = f.ReadDouble(STFReader.UNITS.Any, null);
			TrackSections = new uint[ NoSections ]; 
			for( int i = 0; i < NoSections; ++i )  
			{
       			string token = f.ReadItem();
                if( token == ")" ) 
                {
                    STFException.ReportError( f, "Missing track section" );
                    return;   // there are many TSECTION.DAT's with missing sections so we will accept this error
                }
				try
				{
					TrackSections[i] = uint.Parse(token);
				}
				catch (STFException error)
				{
					STFException.ReportError(f, error.Message);
				}
			}
			f.SkipRestOfBlock();
		}
		public uint NoSections;
		public double X,Y,Z;  // Offset
		public double A;  // Angular offset 
		public uint[] TrackSections;
	}
	
	public class TrackShape
	{
		public TrackShape( STFReader f )
		{
			f.MustMatch("(");
            ShapeIndex = f.ReadUInt(STFReader.UNITS.Any, null);
			string token = f.ReadItem();
			int nextPath = 0;
			while( token != ")" )
			{
                if (token == "") throw new STFException(f, "Missing )");
                else if (0 == String.Compare(token, "FileName", true)) FileName = f.ReadItemBlock(null);
				else if( 0 == String.Compare( token,"NumPaths", true ) ) 
				{
					NumPaths = f.ReadUIntBlock(STFReader.UNITS.Any, null);
					SectionIdxs = new SectionIdx[ NumPaths ];
				}
				else if( 0 == String.Compare( token,"MainRoute",true ) ) MainRoute = f.ReadUIntBlock(STFReader.UNITS.Any, null);
				else if( 0 == String.Compare( token,"ClearanceDist",true ) ) ClearanceDistance = f.ReadDoubleBlock(STFReader.UNITS.Any, null);
				else if( 0 == String.Compare( token,"SectionIdx",true ) ) SectionIdxs[ nextPath++ ] = new SectionIdx( f );
				else if( 0 == String.Compare( token,"TunnelShape",true ) ) TunnelShape = f.ReadBoolBlock(true);
				else if( 0 == String.Compare( token,"RoadShape",true ) ) RoadShape = f.ReadBoolBlock(true);
				else f.SkipBlock();
				token = f.ReadItem();
			}
			// TODO - this was removed since TrackShape( 183 ) is blank
			//if( FileName == null )	throw( new STFError( f, "Missing FileName" ) );
			//if( SectionIdxs == null )	throw( new STFError( f, "Missing SectionIdxs" ) );
			//if( NumPaths == 0 ) throw( new STFError( f, "No Paths in TrackShape" ) );
		}
		public uint ShapeIndex;
		public string FileName;
		public uint NumPaths = 0;
		public uint MainRoute = 0;
		public double ClearanceDistance = 0.0;
		public SectionIdx[] SectionIdxs;
		public bool TunnelShape = false;
		public bool RoadShape = false;
	}
	
	public class TrackShapes: ArrayList
	{
		public TrackShapes( STFReader f )
		{
			f.MustMatch("(");
            MaxShapeIndex = f.ReadUInt(STFReader.UNITS.Any, null);
			string token = f.ReadItem();
			while( token != ")" ) 
			{
                if (token == "") throw new STFException(f, "Missing )");
				else if( 0 == String.Compare( token,"TrackShape",true ) ) this.Add( new TrackShape(f) );
				else f.SkipBlock();
				token = f.ReadItem();
			}
		}
		public TrackShape Get( uint targetShapeIndex )
		{
			// TODO - do this better - linear search is slow
			for( int i = 0; i < this.Count; ++i )
				if( ((TrackShape)this[i]).ShapeIndex == targetShapeIndex )
				{
					return (TrackShape)this[i];
				}
			throw new InvalidDataException("ShapeIndex not found");
		}
		public uint MaxShapeIndex;
	}

	public class TSectionDatFile
	{
		public void AddRouteTSectionDatFile( string pathNameExt )
		{
            using (STFReader f = new STFReader(pathNameExt))
            {
                if (f.SIMISsignature != "SIMISA@@@@@@@@@@JINX0T0t______")
                {
                    Trace.TraceWarning("Ignoring invalid TSECTION.DAT in route folder.");
                    return;
                }
                string token = f.ReadItem();
                while (token != "") // EOF
                {
                    if (token == "(") throw new STFException(f, "Unexpected (");
                    else if (token == ")") throw new STFException(f, "Unexpected )");
                    else if (0 == String.Compare(token, "TrackSections", true)) TrackSections.AddRouteTrackSections(f);
                    // todo read in SectionIdx part of RouteTSectionDat
                    else f.SkipBlock();
                    token = f.ReadItem();
                }
            }
		}
		public TSectionDatFile( string filePath )
		{
            using (STFReader f = new STFReader(filePath))
            {
                string token = f.ReadItem();
                while (token != "") // EOF
                {
                    if (token == "(") throw new STFException(f, "Unexpected (");
                    else if (token == ")") throw new STFException(f, "Unexpected )");
                    else if (0 == String.Compare(token, "TrackSections", true)) TrackSections = new TrackSections(f);
                    else if (0 == String.Compare(token, "TrackShapes", true)) TrackShapes = new TrackShapes(f);
                    else f.SkipBlock();
                    token = f.ReadItem();
                }
                if (TrackSections == null) throw new STFException(f, "Missing TrackSections");
                if (TrackShapes == null) throw new STFException(f, "Missing TrackShapes");
            }
		}
		public TrackSections TrackSections;
		public TrackShapes TrackShapes;
	}


}
