﻿// COPYRIGHT 2014 by the Open Rails project.
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
using System.IO;

using ORTS.Common;

namespace ORTS.TrackViewer.Drawing
{
    public class DrawWorldTiles
    {
        // for each index=TileX, a list containing start and stop tileZ's
        // in many cases each list will contain only two elements. But in the case of holes, it might be more.
        private Dictionary<int, List<int>> worldTileRanges;
        public DrawWorldTiles()
        {
        }

        /// <summary>
        /// From the given route (in terms of its path) we can find the directory with the world files
        /// From the names of these file we determine the tile location, and store these locations for later drawing
        /// </summary>
        /// <param name="routePath"></param>
        public void SetRoute(string routePath) 
        {
            worldTileRanges = new Dictionary<int, List<int>>();
            Dictionary<int, List<int>> worldTiles = new Dictionary<int, List<int>>();

            // First find all world tiles
            string WFilePath = routePath + @"\WORLD\";
            foreach (var fileName in Directory.GetFiles(WFilePath, "*.w"))
            {
                // Parse the tile location out of the filename
                int p = fileName.ToUpper().LastIndexOf("\\WORLD\\W");
                int TileX = int.Parse(fileName.Substring(p + 8, 7));
                int TileZ = int.Parse(fileName.Substring(p + 15, 7));

                if (!worldTiles.ContainsKey(TileX)) worldTiles[TileX] = new List<int>();

                worldTiles[TileX].Add(TileZ);
            }

            //now make ranges out of it. For each available TileX a range is given by a minimum and maximum value of 
            //TileZ such that all tiles from the minimum till/including the maximum are available
            //multiple ranges can occur for a single tileX.
            
            foreach (int TileX in worldTiles.Keys)
            {
                worldTileRanges[TileX] = new List<int>();
                int currentTileZ = worldTiles[TileX][0];
                worldTileRanges[TileX].Add(currentTileZ);
                for (int i = 1; i < worldTiles[TileX].Count(); i++)
                {
                    int nextTileZ = worldTiles[TileX][i];
                    if (nextTileZ != currentTileZ + 1)
                    {
                        worldTileRanges[TileX].Add(currentTileZ);
                        worldTileRanges[TileX].Add(nextTileZ);
                    }
                    currentTileZ = nextTileZ;
                }
                worldTileRanges[TileX].Add(currentTileZ);
            }
        }

        /// <summary>
        /// Draw the various tiles (meaning simply drawing rectangles.
        /// </summary>
        /// <param name="drawArea">Area to draw upon</param>
        public void Draw(DrawArea drawArea)
        {
            if (!ORTS.TrackViewer.Properties.Settings.Default.showWorldTiles) return;
            foreach (int TileX in worldTileRanges.Keys)
            {
                for (int i = 0; i < worldTileRanges[TileX].Count; i += 2)
                {
                    int TileZstart = worldTileRanges[TileX][i];
                    int TileZstop = worldTileRanges[TileX][i+1];
                    WorldLocation bot = new WorldLocation(TileX, TileZstart, 0, 0, -1024);
                    WorldLocation top = new WorldLocation(TileX, TileZstop, 0, 0, 1024);
                    drawArea.DrawLineAlways(2048, DrawColors.colorsNormal["tile"], bot, top);
                }
            }
        }
    }
}
