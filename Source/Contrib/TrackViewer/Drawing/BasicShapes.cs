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
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace ORTS.TrackViewer.Drawing
{
    /// <summary>
    /// This class contains all basic methods to draw on a spritebatch. This means drawing lines, drawing arcs, drawing textures.
    /// So no other class needs to draw directly. 
    /// This class also contains the creation of some textures like discs and rings, as well as the loading 
    /// of a number of needed textures. For the textures that are loaded also a highlight version is made, and they are mipmapped.
    /// </summary>
    static class BasicShapes
    {
        private static SpriteBatch spriteBatch;
        
        //size of a identifying feature in the texture (in pixels), so we can scale as needed
        private static Dictionary<string, float> textureScales = new Dictionary<string, float>();
        private static Dictionary<string, Vector2> textureOffsets = new Dictionary<string, Vector2>();
        private static Dictionary<string,Texture2D> textures = new Dictionary<string,Texture2D>();

        private static ORTS.Viewer3D.Popups.WindowTextManager TextManager;
        private static ORTS.Viewer3D.Popups.WindowTextFont itemfont;    // for items like platform, siding, ...
        private static ORTS.Viewer3D.Popups.WindowTextFont loadingfont; // for loading message


        /// <summary>
        /// Some initialization needed for actual drawing
        /// </summary>
        /// <param name="graphicsDevice"></param>
        /// <param name="spriteBatchIn"></param>
        public static void LoadContent(GraphicsDevice graphicsDevice, SpriteBatch spriteBatchIn, string contentPath)
        {
            spriteBatch = spriteBatchIn;
            textures["blankPixel"] = new Texture2D(graphicsDevice, 1, 1);
            textures["blankPixel"].SetData(new[] { Color.White });

            int diameter = 64; // Needs to be power of two for mipmapping
            int radius = (diameter - 2) / 2;
            textures["circle"] = CreateCircleTexture(graphicsDevice, diameter);
            textureScales["circle"] = radius;
            textureOffsets["circle"] = new Vector2(diameter/2, diameter/2);

            textures["disc"] = CreateDiscTexture(graphicsDevice, diameter);
            textureScales["disc"] = radius;
            textureOffsets["disc"] = new Vector2(diameter / 2, diameter / 2);

            textures["ring"] = CreateRingTexture(graphicsDevice, diameter);
            textureScales["ring"] = radius;
            textureOffsets["ring"] = new Vector2(diameter / 2, diameter / 2);

            textures["crossedRing"] = CreateCrossedRingTexture(graphicsDevice, diameter);
            textureScales["crossedRing"] = radius;
            textureOffsets["crossedRing"] = new Vector2(diameter / 2, diameter / 2);


            LoadAndHighlightTexture(graphicsDevice, contentPath, "signal", "Signal",12, 11);
            LoadAndHighlightTexture(graphicsDevice, contentPath, "hazard", "Hazard");

            LoadAndHighlightTexture(graphicsDevice, contentPath, "pathNormal", "pathNormal",31,31);
            LoadAndHighlightTexture(graphicsDevice, contentPath, "pathStart", "pathStart",31,31);
            LoadAndHighlightTexture(graphicsDevice, contentPath, "pathEnd", "pathEnd",31,31);
            LoadAndHighlightTexture(graphicsDevice, contentPath, "pathWait", "pathWait",31,31);
            LoadAndHighlightTexture(graphicsDevice, contentPath, "pathUncouple", "pathUncouple", 31, 31);
            LoadAndHighlightTexture(graphicsDevice, contentPath, "pathReverse", "pathReverse", 31, 31);
            LoadAndHighlightTexture(graphicsDevice, contentPath, "pathSiding", "pathSiding", 31, 31);

            // textures modified from http://www.iconsdb.com
            LoadAndHighlightTexture(graphicsDevice, contentPath, "sound", "Sound", 5, 5);
            LoadAndHighlightTexture(graphicsDevice, contentPath, "carspawner", "CarSpawner");
            LoadAndHighlightTexture(graphicsDevice, contentPath, "pickup", "Pickup");
            LoadAndHighlightTexture(graphicsDevice, contentPath, "platform", "Platform");
            LoadAndHighlightTexture(graphicsDevice, contentPath, "sound", "Sound");
            LoadAndHighlightTexture(graphicsDevice, contentPath, "playerTrain", "steamTrain",31,31);

            prepareArcDrawing();

            TextManager = new ORTS.Viewer3D.Popups.WindowTextManager();
            itemfont = TextManager.Get("Segoe UI", 10, System.Drawing.FontStyle.Regular, 0);
            loadingfont = TextManager.Get("Segoe UI", 10, System.Drawing.FontStyle.Regular, 0);
            
            
        }

        /// <summary>
        /// Update, because Textmanager only loads the needed characters when it knows it needs to print it.
        /// </summary>
        public static void Update(GraphicsDevice graphicsDevice)
        {
            TextManager.Load(graphicsDevice);
        }

        /// <summary>
        /// Create both a normal and a highlighted texture by loading, adding highlight and doing automipmap
        /// </summary>
        /// <param name="graphicsDevice">graphics device needed to create new textures</param>
        /// <param name="contentPath">main Game needed to load from file</param>
        /// <param name="textureName">the name by which we can find the texture back later</param>
        /// <param name="fileName">the name of the file (without extension)</param>
        private static void LoadAndHighlightTexture(GraphicsDevice graphicsDevice, string contentPath, string textureName, string fileName)
        {
            LoadAndHighlightTexture( graphicsDevice, contentPath, textureName, fileName, 0, 0);
        }
        private static void LoadAndHighlightTexture(GraphicsDevice graphicsDevice, string contentPath, string textureName, string fileName, int offsetX, int offsetY)
        {
            string fullFilename = System.IO.Path.Combine(contentPath, fileName +".png");
            Texture2D tempTexture;
            try
            {
                tempTexture = Texture2D.FromFile(graphicsDevice, fullFilename);
                //Texture2D tempTexture = contentPath.Content.Update<Texture2D>(fileName);
            }
            catch
            {
                tempTexture = textures["disc"];
            }
            textures[textureName] = ColorScaledTexture(tempTexture, graphicsDevice, 0); // No scaling, but it is adding automipmap
            textureScales[textureName] = textures[textureName].Width;
            textureOffsets[textureName] = new Vector2(offsetX, offsetY);

            string textureNameHighlight = textureName + "Highlight";
            textures[textureNameHighlight] = ColorScaledTexture(tempTexture, graphicsDevice, 80);
            textureScales[textureNameHighlight] = textures[textureNameHighlight].Width;
            textureOffsets[textureNameHighlight] = textureOffsets[textureName];

        }

        static Texture2D ColorScaledTexture(Texture2D texture, GraphicsDevice graphicsDevice, byte offset)
        {
            int pixelCount = texture.Width * texture.Height;
            Color[] pixels = new Color[pixelCount];
            texture.GetData<Color>(pixels);
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = DrawColors.Highlighted(pixels[i], offset);
            }

            Texture2D outTexture = new Texture2D(graphicsDevice, texture.Width, texture.Height, 0, TextureUsage.AutoGenerateMipMap, SurfaceFormat.Color);
            outTexture.SetData<Color>(pixels);
            return outTexture;
        }

        /// <summary>
        /// private method to create a texture2D containing a circle
        /// </summary>
        /// <param name="graphicsDevice"></param>
        /// <param name="radius">radius of the circle in pixels (diameter is 2*radius + 1)</param>
        /// <returns>The white texture</returns>
        private static Texture2D CreateCircleTexture(GraphicsDevice graphicsDevice, int outerRadius)
        {
            int radius = (outerRadius - 2)/2; // So circle doesn't go out of bounds
            Texture2D texture = new Texture2D(graphicsDevice, outerRadius, outerRadius,  0, TextureUsage.AutoGenerateMipMap, SurfaceFormat.Color);

            Color[] data = new Color[outerRadius * outerRadius];

            // Colour the entire texture transparent first.
            for (int i = 0; i < data.Length; i++)
                data[i] = Color.TransparentWhite;

            // Work out the minimum step necessary using trigonometry + sine approximation.
            double angleStep = 1f / radius;

            for (double angle = 0; angle < MathHelper.TwoPi; angle += angleStep)
            {
                // Use the parametric definition of a circle: http://en.wikipedia.org/wiki/Circle#Cartesian_coordinates
                int x = (int)Math.Round(radius + radius * Math.Cos(angle));
                int y = (int)Math.Round(radius + radius * Math.Sin(angle));

                data[y * outerRadius + x + 1] = Color.White;
            }

            texture.SetData(data);
            return texture;
        }

        /// <summary>
        /// private method to create a texture2D containing a disc (filled circle)
        /// </summary>
        /// <param name="graphicsDevice"></param>
        /// <param name="radius">radius of the circle in pixels (diameter is 2*radius + 1)</param>
        /// <returns>The white texture</returns>
        private static Texture2D CreateDiscTexture(GraphicsDevice graphicsDevice, int outerRadius)
        {
            int radius = (outerRadius - 1) / 2;
            Texture2D texture = new Texture2D(graphicsDevice, outerRadius, outerRadius, 0, TextureUsage.AutoGenerateMipMap, SurfaceFormat.Color);

            Color[] data = new Color[outerRadius * outerRadius];

            for (int x = -radius; x <= radius; x++)
            {
                for (int y = -radius; y <= radius; y++)
                {
                    int i = (x + radius) * outerRadius + (y + radius);
                    if (x * x + y * y <= radius * radius)
                    {
                        data[i] = Color.White;
                    }
                    else
                    {
                        data[i] = Color.TransparentWhite;
                    }
                }
            }

            texture.SetData(data);
            return texture;
        }
        
        /// <summary>
        /// private method to create a texture2D containing a ring (circle with thick border)
        /// </summary>
        /// <param name="graphicsDevice"></param>
        /// <param name="radius">radius of the circle in pixels (diameter is 2*radius + 1)</param>
        /// <returns>The white texture</returns>
        private static Texture2D CreateRingTexture(GraphicsDevice graphicsDevice, int outerRadius)
        {
            int radius = (outerRadius - 1) / 2;
            int innerRadius = (2 * radius) / 3;
            Texture2D texture = new Texture2D(graphicsDevice, outerRadius, outerRadius, 0, TextureUsage.AutoGenerateMipMap, SurfaceFormat.Color);

            Color[] data = new Color[outerRadius * outerRadius];

            for (int x = -radius; x <= radius; x++)
            {
                for (int y = -radius; y <= radius; y++)
                {
                    int i = (x + radius) * outerRadius + (y + radius);
                    int r2 = x * x + y * y;
                    if (r2 <= radius * radius &&  r2 > innerRadius * innerRadius)
                    {
                        data[i] = Color.White;
                    }
                    else
                    {
                        data[i] = Color.TransparentWhite;
                    }
                }
            }

            texture.SetData(data);
            return texture;
        }

        /// <summary>
        /// private method to create a texture2D containing a ring (circle with thick border), with a cross in the middle
        /// </summary>
        /// <param name="graphicsDevice"></param>
        /// <param name="radius">radius of the circle in pixels (diameter is 2*radius + 1)</param>
        /// <returns>The white texture</returns>
        private static Texture2D CreateCrossedRingTexture(GraphicsDevice graphicsDevice, int outerRadius)
        {
            int radius = (outerRadius - 1) / 2;
            int innerRadius = (3 * radius) / 4;
            int crossWidth = 5;
            Texture2D texture = new Texture2D(graphicsDevice, outerRadius, outerRadius, 0, TextureUsage.AutoGenerateMipMap, SurfaceFormat.Color);

            Color[] data = new Color[outerRadius * outerRadius];

            for (int x = -radius; x <= radius; x++)
            {
                for (int y = -radius; y <= radius; y++)
                {
                    int i = (x + radius) * outerRadius + (y + radius);
                    data[i] = Color.TransparentWhite; //default
                    int r2 = x * x + y * y;
                    if (r2 <= radius * radius)
                    {
                        if (r2 > innerRadius * innerRadius)
                        {   //ring
                            data[i] = Color.White;
                        }
                        if ((x - y) * (x - y) < crossWidth * crossWidth)
                        {   //part of cross lower-left to upper-right
                            data[i] = Color.White;
                        }
                        if ((x + y) * (x + y) < crossWidth * crossWidth)
                        {   //part of cross lower-right to upper-left
                            data[i] = Color.White;
                        }
                    }
                    
                }
            }

            texture.SetData(data);
            return texture;
        }


        /// <summary>
        /// Basic method to draw a line. Coordinates are in screen coordinates.
        /// <param name="width"> Width of the line to draw </param>
        /// <param name="color"> Color of the line</param>
        /// <param name="point1"> Vector to the first point of the line</param>
        /// <param name="point2"> Vector to the last point of the line</param>
        /// </summary>
        public static void DrawLine(float width, Color color, Vector2 point1, Vector2 point2)
        {
            float angle = (float)Math.Atan2(point2.Y - point1.Y, point2.X - point1.X);
            float length = Vector2.Distance(point1, point2);
            DrawLine(width, color, point1, length, angle);
        }

        /// <summary>
        /// Basic method to draw a line. Coordinates are in screen coordinates.
        /// </summary>
        /// <param name="width">Width of the line to draw </param>
        /// <param name="color">Color of the line</param>
        /// <param name="point">Vector to the first point of the line</param>
        /// <param name="length">Length of the line</param>
        /// <param name="angle">Angle (in down from horizontal) of where the line is pointing</param>
        public static void DrawLine(float width, Color color, Vector2 point, float length, float angle)
        {
            // offset to compensate for the width of the line
            Vector2 offset = new Vector2(width * (float) Math.Sin(angle)/2, -width * (float)Math.Cos(angle)/2);
            spriteBatch.Draw(textures["blankPixel"], point+offset, null, color,
                       angle, Vector2.Zero, new Vector2(length, width),
                       SpriteEffects.None, 0);
        }

        /// <summary>
        /// Basic (but not trivial) method to draw an arc. Coordinates are in screen coordinates.
        /// </summary>
        /// <param name="width">Width of the line to draw </param>
        /// <param name="color">Color of the line</param>
        /// <param name="point">Vector to the first point of the line</param>
        /// <param name="radius">Radius of the circle to which the arc belongs. Positive means curving left</param>
        /// <param name="angle">Angle (in down from horizontal) of where the line is pointing</param>
        /// <param name="arcDegrees">Number of degrees in the arc (360 would be full circle)</param>
        /// <param name="arcDegreesOffset">Instead of starting at 0 degrees in the circle, offset allows to start at a different position</param>
        public static void DrawArc(float width, Color color, Vector2 point, float radius, float angle, float arcDegrees, float arcDegreesOffset)
        {
            // Positive arcDegree means curving to the left, negative arcDegree means curving to the right
            int sign = (arcDegrees > 0) ? -1 : 1;
            arcDegrees = Math.Abs(arcDegrees);
            
            // We will draw an arc as a succession of straight lines. We do this in a way that reduces the amount
            // of goniometric calculations needed.
            // The idea is to start to find the center of the circle. The direction from center to origin is
            // 90 degrees different from angle
            Vector2 centerToPointDirection = sign * new Vector2(-(float)Math.Sin(angle), (float)Math.Cos(angle)); // unit vector
            Vector2 center = point - radius * centerToPointDirection; ;
            
            // To determine the amount of straight lines we need to calculate we first 
            // determine then lenght of the arc, and divide that by the maximum we allow;
            // All angles go in steps of minAngleDegree
            float arcLength = radius * MathHelper.ToRadians(arcDegrees);
            // We draw straight lines. The error in the middle of the line is: error = radius - radius*cos(alpha/2).
            // Here alpha is the angle drawn for a single arc-segment. Approximately error ~ radius * alpha^2/8.
            // The amount of pixels in the line is about L ~ radius * alpha => L ~ sqrt(8 * radius * error). 
            // We found that for thight curves, error can not be larger than half a pixel (otherwise it becomes visible)
            maxStraightPixels = (float)Math.Sqrt(4*radius);   
            float numberStraightLines = (float)Math.Ceiling(arcLength / maxStraightPixels);
            // amount of minAngleDegrees we need to cover: 
            int arcStepsRemaining = (int) (Math.Round(arcDegrees/minAngleDegree)); 
            // amount of minAngleDegrees we cover per straight line:
            int arcStepsPerLine = (int) (Math.Ceiling(arcDegrees/(minAngleDegree*numberStraightLines)));

            // Add offset in angles
            if (arcDegreesOffset != 0f)
            {
                angle += MathHelper.ToRadians(arcDegreesOffset);
                centerToPointDirection = sign * new Vector2(-(float)Math.Sin(angle), (float)Math.Cos(angle));
            }

            // All straight lines that we draw will be titled by half of the arc that is should cover.
            angle += -sign * arcStepsPerLine * minAngleRad / 2;

            // while we still have some arc steps to cover
            while (arcStepsRemaining > 0)
            {
                int arcSteps = Math.Min(arcStepsRemaining, arcStepsPerLine); //angle steps we cover in this line
                point = center + centerToPointDirection * (radius-sign*width/2);  // correct for width of line
                float length = radius * arcSteps * minAngleRad;
                
                spriteBatch.Draw(textures["blankPixel"], point, null, color,
                       angle, Vector2.Zero, new Vector2(length, width),
                       SpriteEffects.None, 0);

                // prepare for next straight line
                arcStepsRemaining -= arcSteps;

                if (arcStepsRemaining > 0)
                {
                    angle -= sign * arcSteps * minAngleRad;
                    //Rotate the centerToPointDirection, and calculate new point
                    centerToPointDirection = new Vector2(
                             cosTable[arcSteps] * centerToPointDirection.X + sign*sinTable[arcSteps] * centerToPointDirection.Y,
                       -sign*sinTable[arcSteps] * centerToPointDirection.X +      cosTable[arcSteps] * centerToPointDirection.Y
                        );
                }
            }
        }

        /// <summary>
        /// Draw a text string. 
        /// </summary>
        /// <param name="point">Screen location to use as top-left corner</param>
        /// <param name="color">Color of the text</param>
        /// <param name="message">The string to print</param>
        public static void DrawString(Vector2 point, Color color, string message )
        {
            // text is better readable when on integer locations
            Point intPoint = new Point((int)Math.Round(point.X), (int)Math.Round(point.Y));
            itemfont.Draw(spriteBatch, new Rectangle(), intPoint, message, ORTS.Viewer3D.Popups.LabelAlignment.Left, color);
        }
        public static void DrawStringLoading(Vector2 point, Color color, string message)
        {
            // text is better readable when on integer locations
            Point intPoint = new Point((int)Math.Round(point.X), (int)Math.Round(point.Y));
            loadingfont.Draw(spriteBatch, new Rectangle(), intPoint, message, ORTS.Viewer3D.Popups.LabelAlignment.Center, color);
        }


        /// <summary>
        /// Draw one of the (predefined) textures at the given location with the given angle
        /// </summary>
        /// <param name="point">Location where to draw</param>
        /// <param name="textureName">name by which the texture is internally known</param>
        /// <param name="angle">Angle used to rotate the texture</param>
        /// <param name="size">Size of the texture in pixels</param>
        public static void DrawTexture(Vector2 point, string textureName, float angle, float size, Color color)
        {
            float scaledSize = size/ textureScales[textureName];
            spriteBatch.Draw(textures[textureName], point, null, color, 
                angle, textureOffsets[textureName], new Vector2(scaledSize), SpriteEffects.None, 0);
        }

        /// <summary>
        /// Some preparation to be able to draw arcs more efficiently
        /// </summary>
        private static void prepareArcDrawing()
        {
            minAngleRad = MathHelper.ToRadians(minAngleDegree);
            arcTableSize = (int)(Math.Ceiling(maxAngleDegree/minAngleDegree)+1);
            cosTable = new float[arcTableSize];
            sinTable = new float[arcTableSize];
            for (int i = 0; i < arcTableSize; i++)
            {
                cosTable[i] = (float) Math.Cos(i * minAngleRad);
                sinTable[i] = (float) Math.Sin(i * minAngleRad);
            }
        }

        
        private static float minAngleDegree = 0.1f; // we do not care for angles smaller than 0.1 degrees
        private static float maxAngleDegree = 90; // allows for drawing up to 90 degree arcs
        private static float maxStraightPixels; // Maximum amount of pixels in a straight line part of an arc
        private static float minAngleRad; // minAngleDegree but in radians.
        private static int arcTableSize; // size of the table with precalculated values
        private static float[] cosTable; // table with precalculated Cosine values: cosTable[numberDrawn] = cos(numberDrawn * 0.1degrees)
        private static float[] sinTable; // similar
    }
}