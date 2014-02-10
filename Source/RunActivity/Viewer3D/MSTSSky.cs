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

// This file is the responsibility of the 3D & Environment Team. 

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ORTS.Processes;

namespace ORTS.Viewer3D
{
    #region MSTSSkyConstants
    static class MSTSSkyConstants
    {
        // Sky dome constants
        public const int skyRadius = 6000;
        public const int skySides = 24;
        public static bool IsNight = false;
    }

    #endregion

    #region MSTSSkyDrawer
    public class MSTSSkyDrawer
    {

        Viewer MSTSSkyViewer;
        Material MSTSSkyMaterial;

        // Classes reqiring instantiation
        public MSTSSkyMesh MSTSSkyMesh;
        WorldLatLon mstsskyworldLoc; // Access to latitude and longitude calcs (MSTS routes only)
        SunMoonPos MSTSSkyVectors;

		int mstsskyseasonType; //still need to remember it as MP now can change it.
        #region Class variables
        // Latitude of current route in radians. -pi/2 = south pole, 0 = equator, pi/2 = north pole.
        // Longitude of current route in radians. -pi = west of prime, 0 = prime, pi = east of prime.
        public double mstsskylatitude, mstsskylongitude;
        // Date of activity

        public ORTS.Viewer3D.SkyDrawer.Date date;
  
        // Size of the sun- and moon-position lookup table arrays.
        // Must be an integral divisor of 1440 (which is the number of minutes in a day).
        private int maxSteps = 72;
        private double mstsskyoldClockTime;
        private int step1, step2;
        // Phase of the moon
        public int mstsskymoonPhase;
        // Wind speed and direction
        public float mstsskywindSpeed;
        public float mstsskywindDirection;
        // Overcast level
        public float mstsskyovercastFactor;
        // Fog distance
        public float mstsskyfogDistance;
        public bool isNight = false;

        public List<string> SkyLayers = new List<string>();

        // These arrays and vectors define the position of the sun and moon in the world
        Vector3[] mstsskysolarPosArray = new Vector3[72];
        Vector3[] mstsskylunarPosArray = new Vector3[72];
        public Vector3 mstsskysolarDirection;
        public Vector3 mstsskylunarDirection;
        #endregion

        #region Constructor
        /// <summary>
        /// SkyDrawer constructor
        /// </summary>
        public MSTSSkyDrawer(Viewer viewer)
        {
            MSTSSkyViewer = viewer;
            MSTSSkyMaterial = viewer.MaterialManager.Load("MSTSSky");
            // Instantiate classes
            MSTSSkyMesh = new MSTSSkyMesh( MSTSSkyViewer.RenderProcess);
            MSTSSkyVectors = new SunMoonPos();

            //viewer.World.MSTSSky.MSTSSkyMaterial.Viewer.MaterialManager.sunDirection.Y < 0
            // Set default values
            mstsskyseasonType = (int)MSTSSkyViewer.Simulator.Season;
            date.ordinalDate = 82 + mstsskyseasonType * 91;
            // TODO: Set the following three externally from ORTS route files (future)
            date.month = 1 + date.ordinalDate / 30;
            date.day = 21;
            date.year = 2010;
            // Default wind speed and direction
            mstsskywindSpeed = 5.0f; // m/s (approx 11 mph)
            mstsskywindDirection = 4.7f; // radians (approx 270 deg, i.e. westerly)
       }
        #endregion

        /// <summary>
        /// Used to update information affecting the SkyMesh
        /// </summary>
        public void PrepareFrame(RenderFrame frame, ElapsedTime elapsedTime)
        {
			if (mstsskyseasonType != (int)MSTSSkyViewer.Simulator.Season)
			{
				mstsskyseasonType = (int)MSTSSkyViewer.Simulator.Season;
				date.ordinalDate = 82 + mstsskyseasonType * 91;
				// TODO: Set the following three externally from ORTS route files (future)
				date.month = 1 + date.ordinalDate / 30;
				date.day = 21;
				date.year = 2010;
			}
            // Adjust dome position so the bottom edge is not visible
			Vector3 ViewerXNAPosition = new Vector3(MSTSSkyViewer.Camera.Location.X, MSTSSkyViewer.Camera.Location.Y - 100, -MSTSSkyViewer.Camera.Location.Z);
            Matrix XNASkyWorldLocation = Matrix.CreateTranslation(ViewerXNAPosition);

            if (mstsskyworldLoc == null)
            {
                // First time around, initialize the following items:
                mstsskyworldLoc = new WorldLatLon();
                mstsskyoldClockTime = MSTSSkyViewer.Simulator.ClockTime % 86400;
                while (mstsskyoldClockTime < 0) mstsskyoldClockTime += 86400;
                step1 = step2 = (int)(mstsskyoldClockTime / 1200);
                step2++;
                // Get the current latitude and longitude coordinates
                mstsskyworldLoc.ConvertWTC(MSTSSkyViewer.Camera.TileX, MSTSSkyViewer.Camera.TileZ, MSTSSkyViewer.Camera.Location, ref mstsskylatitude, ref mstsskylongitude);
                // Fill in the sun- and moon-position lookup tables
                for (int i = 0; i < maxSteps; i++)
                {
                    mstsskysolarPosArray[i] = SunMoonPos.SolarAngle(mstsskylatitude, mstsskylongitude, ((float)i / maxSteps), date);
                    mstsskylunarPosArray[i] = SunMoonPos.LunarAngle(mstsskylatitude, mstsskylongitude, ((float)i / maxSteps), date);
                }
                // Phase of the moon is generated at random
                Random random = new Random();
                mstsskymoonPhase = random.Next(8);
                if (mstsskymoonPhase == 6 && date.ordinalDate > 45 && date.ordinalDate < 330)
                    mstsskymoonPhase = 3; // Moon dog only occurs in winter
                // Overcast factor: 0.0=almost no clouds; 0.1=wispy clouds; 1.0=total overcast
                mstsskyovercastFactor = MSTSSkyViewer.World.WeatherControl.overcastFactor;
                mstsskyfogDistance = MSTSSkyViewer.World.WeatherControl.fogDistance;
            }

			if (MultiPlayer.MPManager.IsClient() && MultiPlayer.MPManager.Instance().weatherChanged)
			{
				//received message about weather change
				if ( MultiPlayer.MPManager.Instance().overCast >= 0)
				{
					mstsskyovercastFactor = MultiPlayer.MPManager.Instance().overCast;
				}
                //received message about weather change
                if (MultiPlayer.MPManager.Instance().newFog > 0)
                {
                    mstsskyfogDistance = MultiPlayer.MPManager.Instance().newFog;
                }
                try
                {
                    if (MultiPlayer.MPManager.Instance().overCast >= 0 || MultiPlayer.MPManager.Instance().newFog > 0) 
                    {
                        MultiPlayer.MPManager.Instance().weatherChanged = false;
                        MultiPlayer.MPManager.Instance().overCast = -1 ;
                        MultiPlayer.MPManager.Instance().newFog = -1 ;
                    }
                }
                catch { }

            }

////////////////////// T E M P O R A R Y ///////////////////////////

            // The following keyboard commands are used for viewing sky and weather effects in "demo" mode.
            // Control- and Control+ for overcast, Shift- and Shift+ for fog and - and + for time.

            // Don't let multiplayer clients adjust the weather.
            if (!MultiPlayer.MPManager.IsClient())
            {
                // Overcast ranges from 0 (completely clear) to 1 (completely overcast).
                if (UserInput.IsDown(UserCommands.DebugOvercastIncrease)) mstsskyovercastFactor = MathHelper.Clamp(mstsskyovercastFactor + elapsedTime.RealSeconds / 10, 0, 1);
                if (UserInput.IsDown(UserCommands.DebugOvercastDecrease)) mstsskyovercastFactor = MathHelper.Clamp(mstsskyovercastFactor - elapsedTime.RealSeconds / 10, 0, 1);
                // Fog ranges from 10m (can't see anything) to 100km (clear arctic conditions).
                if (UserInput.IsDown(UserCommands.DebugFogIncrease)) mstsskyfogDistance = MathHelper.Clamp(mstsskyfogDistance - elapsedTime.RealSeconds * mstsskyfogDistance, 10, 100000);
                if (UserInput.IsDown(UserCommands.DebugFogDecrease)) mstsskyfogDistance = MathHelper.Clamp(mstsskyfogDistance + elapsedTime.RealSeconds * mstsskyfogDistance, 10, 100000);
            }
            // Don't let clock shift if multiplayer.
            if (!MultiPlayer.MPManager.IsMultiPlayer())
            {
                // Shift the clock forwards or backwards at 1h-per-second.
                if (UserInput.IsDown(UserCommands.DebugClockForwards)) MSTSSkyViewer.Simulator.ClockTime += elapsedTime.RealSeconds * 3600;
                if (UserInput.IsDown(UserCommands.DebugClockBackwards)) MSTSSkyViewer.Simulator.ClockTime -= elapsedTime.RealSeconds * 3600;
                if (MSTSSkyViewer.World.Precipitation != null && (UserInput.IsDown(UserCommands.DebugClockForwards) || UserInput.IsDown(UserCommands.DebugClockBackwards))) MSTSSkyViewer.World.Precipitation.Reset();
            }
            // Server needs to notify clients of weather changes.
            if (MultiPlayer.MPManager.IsServer())
            {
                if (UserInput.IsReleased(UserCommands.DebugOvercastIncrease) || UserInput.IsReleased(UserCommands.DebugOvercastDecrease) || UserInput.IsReleased(UserCommands.DebugFogIncrease) || UserInput.IsReleased(UserCommands.DebugFogDecrease))
                {
                    MultiPlayer.MPManager.Instance().SetEnvInfo(mstsskyovercastFactor, mstsskyfogDistance);
                    MultiPlayer.MPManager.Notify((new MultiPlayer.MSGWeather(-1, mstsskyovercastFactor, mstsskyfogDistance, -1)).ToString());
                }
            }

////////////////////////////////////////////////////////////////////

            // Current solar and lunar position are calculated by interpolation in the lookup arrays.
            // Using the Lerp() function, so need to calculate the in-between differential
            float diff = (float)(MSTSSkyViewer.Simulator.ClockTime - mstsskyoldClockTime) / 1200;
            // The rest of this increments/decrements the array indices and checks for overshoot/undershoot.
            if (MSTSSkyViewer.Simulator.ClockTime >= (mstsskyoldClockTime + 1200)) // Plus key, or normal forward in time
            {
                step1++;
                step2++;
                mstsskyoldClockTime = MSTSSkyViewer.Simulator.ClockTime;
                diff = 0;
                if (step1 == maxSteps - 1) // Midnight. Value is 71 for maxSteps = 72
                {
                    step2 = 0;
                }
                if (step1 == maxSteps) // Midnight.
                {
                    step1 = 0;
                }
            }
            if (MSTSSkyViewer.Simulator.ClockTime <= (mstsskyoldClockTime - 1200)) // Minus key
            {
                step1--;
                step2--;
                mstsskyoldClockTime = MSTSSkyViewer.Simulator.ClockTime;
                diff = 0;
                if (step1 < 0) // Midnight.
                {
                    step1 = 71;
                }
                if (step2 < 0) // Midnight.
                {
                    step2 = 71;
                }
            }
            

            mstsskysolarDirection.X = MathHelper.Lerp(mstsskysolarPosArray[step1].X, mstsskysolarPosArray[step2].X, diff);
            mstsskysolarDirection.Y = MathHelper.Lerp(mstsskysolarPosArray[step1].Y, mstsskysolarPosArray[step2].Y, diff);
            mstsskysolarDirection.Z = MathHelper.Lerp(mstsskysolarPosArray[step1].Z, mstsskysolarPosArray[step2].Z, diff);
            mstsskylunarDirection.X = MathHelper.Lerp(mstsskylunarPosArray[step1].X, mstsskylunarPosArray[step2].X, diff);
            mstsskylunarDirection.Y = MathHelper.Lerp(mstsskylunarPosArray[step1].Y, mstsskylunarPosArray[step2].Y, diff);
            mstsskylunarDirection.Z = MathHelper.Lerp(mstsskylunarPosArray[step1].Z, mstsskylunarPosArray[step2].Z, diff);

            if (mstsskysolarDirection.Y < -0.09f )
            {
                MSTSSkyConstants.IsNight = true;
                MSTSSkyMesh.ResetMSTSSkyMesh(MSTSSkyViewer.RenderProcess);              
            }
            else
            {
                MSTSSkyConstants.IsNight = false;
                MSTSSkyMesh.ResetMSTSSkyMesh(MSTSSkyViewer.RenderProcess);
            }
            frame.AddPrimitive(MSTSSkyMaterial, MSTSSkyMesh, RenderPrimitiveGroup.Sky, ref XNASkyWorldLocation);
        }

        [CallOnThread("Loader")]
        internal void Mark()
        {
            MSTSSkyMaterial.Mark();
        }
    }
    #endregion

    #region MSTSSkyMesh
    public class MSTSSkyMesh: RenderPrimitive 
    {
        private VertexBuffer SkyVertexBuffer;
        private static VertexDeclaration SkyVertexDeclaration;
        private static IndexBuffer SkyIndexBuffer;
        private static int SkyVertexStride;  // in bytes
        public int drawIndex;

        VertexPositionNormalTexture[] vertexList;
        private static short[] triangleListIndices; // Trilist buffer.

        // Sky dome geometry is based on two global variables: the radius and the number of sides
        public int mstsskyRadius = MSTSSkyConstants.skyRadius;
        private static int mstsskySides = MSTSSkyConstants.skySides;
        public int mstscloudDomeRadiusDiff = 600; // Amount by which cloud dome radius is smaller than sky dome
        // skyLevels: Used for iterating vertically through the "levels" of the hemisphere polygon
        private static int mstsskyLevels = ((MSTSSkyConstants.skySides / 4) - 1);
        // Number of vertices in the sky hemisphere. (each dome = 145 for 24-sided sky dome: 24 x 6 + 1)
        // plus four more for the moon quad
        private static int numVertices = 4 + 2 * (int)((Math.Pow(mstsskySides, 2) / 4) + 1);
        // Number of point indices (each dome = 792 for 24 sides: 5 levels of 24 triangle pairs each
        // plus 24 triangles at the zenith)
        // plus six more for the moon quad
        private static short indexCount = 6 + 2 * ((MSTSSkyConstants.skySides * 2 * 3 * ((MSTSSkyConstants.skySides / 4) - 1)) + 3 * MSTSSkyConstants.skySides);

        /// <summary>
        /// Constructor.
        /// </summary>

        public MSTSSkyMesh(RenderProcess renderProcess)
        {
            var tileFactor = 1;
            if ( MSTSSkyConstants.IsNight == true) 
                    tileFactor = 8;
                else
                    tileFactor = 1;
            // Initialize the vertex and point-index buffers
            vertexList = new VertexPositionNormalTexture[numVertices];
            triangleListIndices = new short[indexCount];

            // Sky dome
            MSTSSkyDomeVertexList(0, mstsskyRadius, 1.0f, tileFactor);
            MSTSSkyDomeTriangleList(0, 0);
            // Cloud dome
            //MSTSSkyDomeVertexList((numVertices - 4) / 2, mstsskyRadius - mstscloudDomeRadiusDiff, 0.4f, 1);
            //MSTSSkyDomeTriangleList((short)((indexCount - 6) / 2), 1);
            // Moon quad
            MoonLists(numVertices - 5, indexCount - 6);//(144, 792);
            // Meshes have now been assembled, so put everything into vertex and index buffers
            InitializeVertexBuffers(renderProcess.GraphicsDevice);
            
        }
        public void ResetMSTSSkyMesh(RenderProcess renderProcess)
        {
            var tileFactor = 1;
            if (MSTSSkyConstants.IsNight == true)
                tileFactor = 8;
            else
                tileFactor = 1;

            vertexList = new VertexPositionNormalTexture[numVertices];
            triangleListIndices = new short[indexCount];

            // Sky dome
            MSTSSkyDomeVertexList(0, mstsskyRadius, 1.0f, tileFactor);
            MSTSSkyDomeTriangleList(0, 0);
            // Cloud dome
            //MSTSSkyDomeVertexList((numVertices - 4) / 2, mstsskyRadius - mstscloudDomeRadiusDiff, 0.4f, 1);
            //MSTSSkyDomeTriangleList((short)((indexCount - 6) / 2), 1);
            // Moon quad
            MoonLists(numVertices - 5, indexCount - 6);//(144, 792);
            // Meshes have now been assembled, so put everything into vertex and index buffers
            InitializeVertexBuffers(renderProcess.GraphicsDevice);

        }

        public override void Draw(GraphicsDevice graphicsDevice)
        {
            graphicsDevice.VertexDeclaration = SkyVertexDeclaration;
            graphicsDevice.Vertices[0].SetSource(SkyVertexBuffer, 0, SkyVertexStride);
            graphicsDevice.Indices = SkyIndexBuffer;

            switch (drawIndex)
            {
                case 1: // Sky dome
                    graphicsDevice.DrawIndexedPrimitives(
                        PrimitiveType.TriangleList,
                        0,
                        0,
                        (numVertices - 4) / 2,
                        0,
                        (indexCount - 6) / 6);
                    break;
                case 2: // Moon
                    graphicsDevice.DrawIndexedPrimitives(
                    PrimitiveType.TriangleList,
                    0,
                    numVertices - 4,
                    4,
                    indexCount - 6,
                    2);
                    break;
                case 3: // Clouds Dome
                    graphicsDevice.DrawIndexedPrimitives(
                        PrimitiveType.TriangleList,
                        0,
                        (numVertices - 4) / 2,
                        (numVertices - 4) / 2,
                        (indexCount - 6) / 2,
                        (indexCount - 6) / 6);
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// Creates the vertex list for each sky dome.
        /// </summary>
        /// <param name="index">The starting vertex number</param>
        /// <param name="radius">The radius of the dome</param>
        /// <param name="oblate">The amount the dome is flattened</param>
        private void MSTSSkyDomeVertexList(int index, int radius, float oblate, float texturetiling)
        {
            int vertexIndex = index;
            // for each vertex
            for (int i = 0; i < (mstsskySides / 4); i++) // (=6 for 24 sides)
                for (int j = 0; j < mstsskySides; j++) // (=24 for top overlay)
                {
                    // The "oblate" factor is used to flatten the dome to an ellipsoid. Used for the inner (cloud)
                    // dome only. Gives the clouds a flatter appearance.
                    float y = (float)Math.Sin(MathHelper.ToRadians((360 / mstsskySides) * i)) * radius * oblate;
                    float yRadius = radius * (float)Math.Cos(MathHelper.ToRadians((360 / mstsskySides) * i));
                    float x = (float)Math.Cos(MathHelper.ToRadians((360 / mstsskySides) * (mstsskySides - j))) * yRadius;
                    float z = (float)Math.Sin(MathHelper.ToRadians((360 / mstsskySides) * (mstsskySides - j))) * yRadius;

                    // UV coordinates - top overlay
                    float uvRadius;
                    uvRadius = 0.5f - (float)(0.5f * i) / (mstsskySides / 4);
                    float uv_u = 0.5f - ((float)Math.Cos(MathHelper.ToRadians((360 / mstsskySides) * (mstsskySides - j))) * uvRadius);
                    float uv_v = 0.5f - ((float)Math.Sin(MathHelper.ToRadians((360 / mstsskySides) * (mstsskySides - j))) * uvRadius);

                    // Store the position, texture coordinates and normal (normalized position vector) for the current vertex
                    vertexList[vertexIndex].Position = new Vector3(x, y, z);
                    vertexList[vertexIndex].TextureCoordinate = new Vector2(uv_u * texturetiling , uv_v * texturetiling);  ///MSTS Sky is tiled, need to add that in.
                    vertexList[vertexIndex].Normal = Vector3.Normalize(new Vector3(x, y, z));
                    vertexIndex++;
                }
            // Single vertex at zenith

            vertexList[vertexIndex].Position = new Vector3(0, radius, 0);
            vertexList[vertexIndex].Normal = new Vector3(0, 1, 0);
            vertexList[vertexIndex].TextureCoordinate = new Vector2(0.5f, 0.5f); // (top overlay)
        }

        /// <summary>
        /// Creates the triangle index list for each dome.
        /// </summary>
        /// <param name="index">The starting triangle index number</param>
        /// <param name="pass">A multiplier used to arrive at the starting vertex number</param>
        static void MSTSSkyDomeTriangleList(short index, short pass)
        {
            // ----------------------------------------------------------------------
            // 24-sided sky dome mesh is built like this:        48 49 50
            // Triangles are wound couterclockwise          71 o--o--o--o
            // because we're looking at the inner              | /|\ | /|
            // side of the hemisphere. Each time               |/ | \|/ |
            // we circle around to the start point          47 o--o--o--o 26
            // on the mesh we have to reset the                |\ | /|\ |
            // vertex number back to the beginning.            | \|/ | \|
            // Using WAC's sw,se,nw,ne coordinate    nw ne  23 o--o--o--o 
            // convention.-->                        sw se        0  1  2
            // ----------------------------------------------------------------------
            short iIndex = index;
            short baseVert = (short)(pass * (short)((numVertices - 4) / 2));
            for (int i = 0; i < mstsskyLevels; i++) // (=5 for 24 sides)
                for (int j = 0; j < mstsskySides; j++) // (=24 for 24 sides)
                {
                    // Vertex indices, beginning in the southwest corner
                    short sw = (short)(baseVert + (j + i * (mstsskySides)));
                    short nw = (short)(sw + mstsskySides); // top overlay mapping
                    short ne = (short)(nw + 1);

                    short se = (short)(sw + 1);

                    if (((i & 1) == (j & 1)))  // triangles alternate
                    {
                        triangleListIndices[iIndex++] = sw;
                        triangleListIndices[iIndex++] = ((ne - baseVert) % mstsskySides == 0) ? (short)(ne - mstsskySides) : ne;
                        triangleListIndices[iIndex++] = nw;
                        triangleListIndices[iIndex++] = sw;
                        triangleListIndices[iIndex++] = ((se - baseVert) % mstsskySides == 0) ? (short)(se - mstsskySides) : se;
                        triangleListIndices[iIndex++] = ((ne - baseVert) % mstsskySides == 0) ? (short)(ne - mstsskySides) : ne;
                    }
                    else
                    {
                        triangleListIndices[iIndex++] = sw;
                        triangleListIndices[iIndex++] = ((se - baseVert) % mstsskySides == 0) ? (short)(se - mstsskySides) : se;
                        triangleListIndices[iIndex++] = nw;
                        triangleListIndices[iIndex++] = ((se - baseVert) % mstsskySides == 0) ? (short)(se - mstsskySides) : se;
                        triangleListIndices[iIndex++] = ((ne - baseVert) % mstsskySides == 0) ? (short)(ne - mstsskySides) : ne;
                        triangleListIndices[iIndex++] = nw;
                    }
                }
            //Zenith triangles (=24 for 24 sides)
            for (int i = 0; i < mstsskySides; i++)
            {
                short sw = (short)(baseVert + (((mstsskySides) * mstsskyLevels) + i));
                short se = (short)(sw + 1);

                triangleListIndices[iIndex++] = sw;
                triangleListIndices[iIndex++] = ((se - baseVert) % mstsskySides == 0) ? (short)(se - mstsskySides) : se;
                triangleListIndices[iIndex++] = (short)(baseVert + (short)((numVertices - 5) / 2)); // The zenith
            }
        }

        /// <summary>
        /// Creates the moon vertex and triangle index lists.
        /// <param name="vertexIndex">The starting vertex number</param>
        /// <param name="iIndex">The starting triangle index number</param>
        /// </summary>
        private void MoonLists(int vertexIndex, int iIndex)
        {
            // Moon vertices
            for (int i = 0; i < 2; i++)
                for (int j = 0; j < 2; j++)
                {
                    vertexIndex++;
                    vertexList[vertexIndex].Position = new Vector3(i, j, 0);
                    vertexList[vertexIndex].Normal = new Vector3(0, 0, 1);
                    vertexList[vertexIndex].TextureCoordinate = new Vector2(i, j);
                }

            // Moon indices - clockwise winding
            short msw = (short)(numVertices - 4);
            short mnw = (short)(msw + 1);
            short mse = (short)(mnw + 1);
            short mne = (short)(mse + 1);
            triangleListIndices[iIndex++] = msw;
            triangleListIndices[iIndex++] = mnw;
            triangleListIndices[iIndex++] = mse;
            triangleListIndices[iIndex++] = mse;
            triangleListIndices[iIndex++] = mnw;
            triangleListIndices[iIndex++] = mne;
        }

        /// <summary>
        /// Initializes the sky dome, cloud dome and moon vertex and triangle index list buffers.
        /// </summary>
        private void InitializeVertexBuffers(GraphicsDevice graphicsDevice)
        {
            if (SkyVertexDeclaration == null)
            {
                SkyVertexDeclaration = new VertexDeclaration(graphicsDevice, VertexPositionNormalTexture.VertexElements);
                SkyVertexStride = VertexPositionNormalTexture.SizeInBytes;
            }
            // Initialize the vertex and index buffers, allocating memory for each vertex and index
            SkyVertexBuffer = new VertexBuffer(graphicsDevice, VertexPositionNormalTexture.SizeInBytes * vertexList.Length, BufferUsage.WriteOnly);
            SkyVertexBuffer.SetData(vertexList);
            if (SkyIndexBuffer == null)
            {
                SkyIndexBuffer = new IndexBuffer(graphicsDevice, typeof(short), indexCount, BufferUsage.WriteOnly);
                SkyIndexBuffer.SetData(triangleListIndices);
            }
        }

    } // SkyMesh
    #endregion
}
