﻿// COPYRIGHT 2010, 2011, 2012, 2013 by the Open Rails project.
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

// Add DEBUG_WINDOW_ZORDER to project defines to record window visibility and z-order changes.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ORTS.Viewer3D;

namespace ORTS.Popups
{
	public class WindowManager : RenderPrimitive
	{
		public static Texture2D WhiteTexture;
		public static Texture2D ScrollbarTexture;
		public static Texture2D LabelShadowTexture;
        public static Texture2D PauseTexture;

        // This is all a bit of a hack, since SpriteBatch does not expose its own internal Flush() method. What we do
        // is draw with a different texture to anything else; the change of texture triggers an internal flush. The
        // texture is initialised to transparent black so although we draw it in a visible area, it will not actually
        // be visible on screen.
        static Texture2D FlushTexture;
        public static void Flush(SpriteBatch spriteBatch)
        {
            spriteBatch.Draw(FlushTexture, Vector2.Zero, Color.Black);
        }

		public readonly Viewer Viewer;
        public readonly WindowTextManager TextManager;
        public readonly WindowTextFont TextFontDefault;
        public readonly WindowTextFont TextFontDefaultOutlined;

        public readonly WindowTextFont TextFontSmall;
        public readonly WindowTextFont TextFontSmallOutlined;

        readonly Material WindowManagerMaterial;
        readonly PopupWindowMaterial PopupWindowMaterial;
        readonly List<Window> Windows = new List<Window>();
        Window[] WindowsZOrder = new Window[0];
        SpriteBatch SpriteBatch;
        Matrix Identity = Matrix.Identity;
        Matrix XNAView = Matrix.Identity;
		Matrix XNAProjection = Matrix.Identity;
        internal Point ScreenSize = new Point(10000, 10000); // Arbitrary but necessary.
		ResolveTexture2D Screen;

		public WindowManager(Viewer viewer)
		{
			Viewer = viewer;
            WindowManagerMaterial = new BasicBlendedMaterial(viewer, "WindowManager");
            PopupWindowMaterial = (PopupWindowMaterial)Viewer.MaterialManager.Load("PopupWindow");
            TextManager = new WindowTextManager();
            TextFontDefault = TextManager.Get("Arial", 10, System.Drawing.FontStyle.Regular);
            TextFontDefaultOutlined = TextManager.Get("Arial", 10, System.Drawing.FontStyle.Regular, 1);
            TextFontSmall = TextManager.Get("Arial", 8, System.Drawing.FontStyle.Regular);
            TextFontSmallOutlined = TextManager.Get("Arial", 8, System.Drawing.FontStyle.Regular, 1);

            SpriteBatch = new SpriteBatch(Viewer.GraphicsDevice);

            if (WhiteTexture == null)
            {
                WhiteTexture = new Texture2D(Viewer.GraphicsDevice, 1, 1, 1, TextureUsage.None, SurfaceFormat.Color);
                WhiteTexture.SetData(new[] { Color.White });
            }
            if (FlushTexture == null)
            {
                FlushTexture = new Texture2D(Viewer.GraphicsDevice, 1, 1, 1, TextureUsage.None, SurfaceFormat.Color);
                FlushTexture.SetData(new[] { Color.TransparentBlack });
            }
            if (ScrollbarTexture == null)
                // TODO: This should happen on the loader thread.
                ScrollbarTexture = Texture2D.FromFile(Viewer.RenderProcess.GraphicsDevice, System.IO.Path.Combine(Viewer.ContentPath, "WindowScrollbar.png"));
            if (LabelShadowTexture == null)
                // TODO: This should happen on the loader thread.
                LabelShadowTexture = Texture2D.FromFile(Viewer.RenderProcess.GraphicsDevice, System.IO.Path.Combine(Viewer.ContentPath, "WindowLabelShadow.png"));
            if (PauseTexture == null)
            {
                var size = 256;
                var background = new Color(Color.Black, 0.5f);
                var borderRadius = size / 7;
                var data = new Color[size * size * 2];
                // Rounded corner background.
                for (var y = 0; y < size; y++)
                    for (var x = 0; x < size; x++)
                        if ((x > borderRadius && x < size - borderRadius) || (y > borderRadius && y < size - borderRadius)
                            || (Math.Sqrt((x - borderRadius) * (x - borderRadius) + (y - borderRadius) * (y - borderRadius)) < borderRadius)
                            || (Math.Sqrt((x - size + borderRadius) * (x - size + borderRadius) + (y - borderRadius) * (y - borderRadius)) < borderRadius)
                            || (Math.Sqrt((x - borderRadius) * (x - borderRadius) + (y - size + borderRadius) * (y - size + borderRadius)) < borderRadius)
                            || (Math.Sqrt((x - size + borderRadius) * (x - size + borderRadius) + (y - size + borderRadius) * (y - size + borderRadius)) < borderRadius))
                            data[y * size + x] = background;
                Array.Copy(data, 0, data, size * size, size * size);
                // Play ">" symbol.
                for (var y = size / 7; y < size - size / 7; y++)
                {
                    for (var x = size / 7; x < size - size / 7 - 2 * Math.Abs(y - size / 2); x++)
                        data[y * size + x] = Color.White;
                }
                // Pause "||" symbol.
                for (var y = size + size / 7; y < 2 * size - size / 7; y++)
                {
                    for (var x = size * 2 / 7; x < size * 3 / 7; x++)
                        data[y * size + x] = Color.White;
                    for (var x = size * 4 / 7; x < size * 5 / 7; x++)
                        data[y * size + x] = Color.White;
                }
                PauseTexture = new Texture2D(Viewer.GraphicsDevice, size, size * 2, 1, TextureUsage.None, SurfaceFormat.Color);
                PauseTexture.SetData(data);
            }
        }

        public void Initialize()
        {
            ScreenChanged();
            UpdateTopMost();

            foreach (var window in Windows)
            {
                window.Initialize();
                window.Layout();
            }
        }

        [CallOnThread("Updater")]
        public void Save(BinaryWriter outf)
        {
            foreach (var window in Windows)
                window.Save(outf);
        }

        [CallOnThread("Render")]
        public void Restore(BinaryReader inf)
        {
            foreach (var window in Windows)
                window.Restore(inf);
        }

		[CallOnThread("Updater")]
		public void ScreenChanged()
		{
			var oldScreenSize = ScreenSize;
			ScreenSize = Viewer.DisplaySize;

			// Buffer for screen texture, also same size as viewport and using the backbuffer format.
			if (Viewer.Settings.WindowGlass)
			{
				if (Screen != null)
					Screen.Dispose();
				Screen = new ResolveTexture2D(Viewer.GraphicsDevice, ScreenSize.X, ScreenSize.Y, 1, Viewer.GraphicsDevice.PresentationParameters.BackBufferFormat);
			}

			// Reposition all the windows.
            foreach (var window in Windows)
            {
                if (oldScreenSize.X - window.Location.Width > 0 && oldScreenSize.Y - window.Location.Height > 0)
                    window.MoveTo((ScreenSize.X - window.Location.Width) * window.Location.X / (oldScreenSize.X - window.Location.Width), (ScreenSize.Y - window.Location.Height) * window.Location.Y / (oldScreenSize.Y - window.Location.Height));
                window.ScreenChanged();
            }
		}

        double LastPrepareRealTime;
        [CallOnThread("Updater")]
        public void PrepareFrame(RenderFrame frame, ElapsedTime elapsedTime)
        {
            var updateFull = false;
            if (Viewer.RealTime - LastPrepareRealTime >= 0.25)
            {
                updateFull = true;
                LastPrepareRealTime = Viewer.RealTime;
            }

            foreach (var window in VisibleWindows)
                window.PrepareFrame(frame, elapsedTime, updateFull);

            frame.AddPrimitive(WindowManagerMaterial, this, RenderPrimitiveGroup.Overlay, ref Identity);
        }

        [CallOnThread("Render")]
        public override void Draw(GraphicsDevice graphicsDevice)
		{
			// Nothing visible? Nothing more to do!
			if (!VisibleWindows.Any())
				return;

			// Construct a view where (0, 0) is the top-left and (width, height) is
			// bottom-right, so that popups can act more like normal window things.
			XNAView = Matrix.CreateTranslation(-ScreenSize.X / 2, -ScreenSize.Y / 2, 0) *
				Matrix.CreateTranslation(-0.5f, -0.5f, 0) *
				Matrix.CreateScale(1, -1, 1);
			// Project into a flat view of the same size as the viewport.
			XNAProjection = Matrix.CreateOrthographic(ScreenSize.X, ScreenSize.Y, 0, 100);

            var rs = graphicsDevice.RenderState;
			foreach (var window in VisibleWindows)
			{
				var xnaWorld = window.XNAWorld;

				if (Screen != null)
					graphicsDevice.ResolveBackBuffer(Screen);
                PopupWindowMaterial.SetState(graphicsDevice, Screen);
                PopupWindowMaterial.Render(graphicsDevice, window, ref xnaWorld, ref XNAView, ref XNAProjection);
                PopupWindowMaterial.ResetState(graphicsDevice);

                SpriteBatch.Begin(SpriteBlendMode.AlphaBlend, SpriteSortMode.Immediate, SaveStateMode.None);
				window.Draw(SpriteBatch);
				SpriteBatch.End();
			}
            // For performance, we call SpriteBatch.Begin() with SaveStateMode.None above, but we now need to restore
            // the state ourselves.
            rs.AlphaBlendEnable = false;
            rs.AlphaFunction = CompareFunction.Always;
            rs.AlphaTestEnable = false;
            rs.DepthBufferEnable = true;
            rs.DestinationBlend = Blend.Zero;
            rs.SourceBlend = Blend.One;
		}

		internal void Add(Window window)
		{
			Windows.Add(window);
            WindowsZOrder = Windows.Concat(new[] { window }).ToArray();
        }

		public bool HasVisiblePopupWindows()
		{
            return WindowsZOrder.Any(w => w.Visible);
		}

		public IEnumerable<Window> VisibleWindows
		{
			get
			{
                return WindowsZOrder.Where(w => w.Visible);
			}
		}

		public const int DragMinimumDistance = 2;

		Point mouseDownPosition;
		public Point MouseDownPosition { get { return mouseDownPosition; } }

		Window mouseActiveWindow;
		public Window MouseActiveWindow { get { return mouseActiveWindow; } }

		double LastUpdateRealTime;
		[CallOnThread("Updater")]
        public void HandleUserInput(ElapsedTime elapsedTime)
        {
			if (UserInput.IsMouseLeftButtonPressed())
			{
				mouseDownPosition = new Point(UserInput.MouseState.X, UserInput.MouseState.Y);
                mouseActiveWindow = VisibleWindows.LastOrDefault(w => w.Interactive && w.Location.Contains(mouseDownPosition));
                if ((mouseActiveWindow != null) && (mouseActiveWindow != WindowsZOrder.Last()))
                    BringWindowToTop(mouseActiveWindow);
			}

			if (mouseActiveWindow != null)
			{
				if (UserInput.IsMouseLeftButtonPressed())
					mouseActiveWindow.MouseDown();
				else if (UserInput.IsMouseLeftButtonReleased())
					mouseActiveWindow.MouseUp();

				if (UserInput.IsMouseMoved())
					mouseActiveWindow.MouseMove();

				if (Viewer.RealTime - LastUpdateRealTime >= 0.1)
				{
					LastUpdateRealTime = Viewer.RealTime;
					mouseActiveWindow.HandleUserInput();
				}

				if (UserInput.IsMouseLeftButtonReleased())
					mouseActiveWindow = null;
			}
		}

        public void BringWindowToTop(Window mouseActiveWindow)
        {
            WindowsZOrder = WindowsZOrder.Where(w => w != mouseActiveWindow).Concat(new[] { mouseActiveWindow }).ToArray();
            UpdateTopMost();
            WriteWindowZOrder();
        }

        void UpdateTopMost()
        {
            // Make sure all top-most windows sit above all normal windows.
            WindowsZOrder = WindowsZOrder.Where(w => !w.TopMost).Concat(WindowsZOrder.Where(w => w.TopMost)).ToArray();
        }

        [Conditional("DEBUG_WINDOW_ZORDER")]
        internal void WriteWindowZOrder()
        {
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine("Windows: (bottom-to-top order, [V] = visible, [NI] = non-interactive)");
            Console.WriteLine("  Visible: {0}", String.Join(", ", VisibleWindows.Select(w => w.GetType().Name).ToArray()));
            Console.WriteLine("  All:     {0}", String.Join(", ", WindowsZOrder.Select(w => String.Format("{0}{1}{2}", w.GetType().Name, w.Interactive ? "" : "[NI]", w.Visible ? "[V]" : "")).ToArray()));
            Console.WriteLine();
        }

        [CallOnThread("Loader")]
        public void Mark()
        {
            WindowManagerMaterial.Mark();
            PopupWindowMaterial.Mark();
        }

        [CallOnThread("Loader")]
        public void Load()
        {
            TextManager.Load(Viewer.GraphicsDevice);
        }
    }
}
