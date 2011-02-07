﻿// COPYRIGHT 2010, 2011 by the Open Rails project.
// This code is provided to enable you to contribute improvements to the open rails program.  
// Use of the code for any other purpose or distribution of the code to anyone else
// is prohibited without specific written permission from admin@openrails.org.

// Add DEBUG_WINDOW_ZORDER to project defines to record window visibility and z-order changes.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace ORTS.Popups
{
	public class WindowManager
	{
		public const SpriteBlendMode BeginSpriteBlendMode = SpriteBlendMode.AlphaBlend;
		public const SpriteSortMode BeginSpriteSortMode = SpriteSortMode.Immediate;
		public const SaveStateMode BeginSaveStateMode = SaveStateMode.SaveState;

		public static Texture2D WhiteTexture;
		public static Texture2D ScrollbarTexture;
		public static Texture2D LabelShadowTexture;

		public readonly Viewer3D Viewer;
		readonly List<Window> Windows = new List<Window>();
		readonly SpriteBatch SpriteBatch;
		Matrix XNAView = Matrix.Identity;
		Matrix XNAProjection = Matrix.Identity;
		internal Point ScreenSize = Point.Zero;
		ResolveTexture2D Screen;

		public WindowManager(Viewer3D viewer)
		{
			Viewer = viewer;
			SpriteBatch = new SpriteBatch(viewer.GraphicsDevice);
			ScreenChanged();

			if (WhiteTexture == null)
			{
				WhiteTexture = new Texture2D(viewer.GraphicsDevice, 1, 1, 1, TextureUsage.None, SurfaceFormat.Color);
				WhiteTexture.SetData(new[] { Color.White });
			}
			if (ScrollbarTexture == null)
				ScrollbarTexture = viewer.RenderProcess.Content.Load<Texture2D>("WindowScrollbar");
			if (LabelShadowTexture == null)
				LabelShadowTexture = viewer.RenderProcess.Content.Load<Texture2D>("WindowLabelShadow");
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
				window.MoveTo((ScreenSize.X - window.Location.Width) * window.Location.X / (oldScreenSize.X - window.Location.Width), (ScreenSize.Y - window.Location.Height) * window.Location.Y / (oldScreenSize.Y - window.Location.Height));
		}

		[CallOnThread("Render")]
		public void Draw(GraphicsDevice graphicsDevice)
		{
			// Nothing visible? Nothing more to do!
			if (Windows.All(w => !w.Visible))
				return;

			// Construct a view where (0, 0) is the top-left and (width, height) is
			// bottom-right, so that popups can act more like normal window things.
			XNAView = Matrix.CreateTranslation(-ScreenSize.X / 2, -ScreenSize.Y / 2, 0) *
				Matrix.CreateTranslation(-0.5f, -0.5f, 0) *
				Matrix.CreateScale(1, -1, 1);
			// Project into a flat view of the same size as the viewport.
			XNAProjection = Matrix.CreateOrthographic(ScreenSize.X, ScreenSize.Y, 0, 100);

			var material = Materials.PopupWindowMaterial;
			foreach (var window in VisibleWindows)
			{
				var xnaWorld = window.XNAWorld;

				if (Screen != null)
					graphicsDevice.ResolveBackBuffer(Screen);
				material.SetState(graphicsDevice, Screen);
				material.Render(graphicsDevice, window, ref xnaWorld, ref XNAView, ref XNAProjection);
				material.ResetState(graphicsDevice);

				SpriteBatch.Begin(BeginSpriteBlendMode, BeginSpriteSortMode, BeginSaveStateMode);
				window.Draw(SpriteBatch);
				SpriteBatch.End();
			}
		}

		internal void Add(Window window)
		{
			Windows.Add(window);
        }

		public bool HasVisiblePopupWindows()
		{
			return Windows.Any(w => w.Visible);
		}

		public IEnumerable<Window> VisibleWindows
		{
			get
			{
				return Windows.Where(w => w.Visible);
			}
		}

		public const int DragMinimumDistance = 2;

		Point mouseDownPosition;
		public Point MouseDownPosition { get { return mouseDownPosition; } }

		Window mouseActiveWindow;
		public Window MouseActiveWindow { get { return mouseActiveWindow; } }

		double LastUpdateRealTime;
		[CallOnThread("Updater")]
		public void HandleUserInput()
		{
			if (UserInput.IsMouseLeftButtonPressed())
			{
				mouseDownPosition = new Point(UserInput.MouseState.X, UserInput.MouseState.Y);
                mouseActiveWindow = VisibleWindows.LastOrDefault(w => w.Interactive && w.Location.Contains(mouseDownPosition));
                if ((mouseActiveWindow != null) && (mouseActiveWindow != Windows.Last()))
				{
					Windows.Remove(mouseActiveWindow);
					Windows.Add(mouseActiveWindow);
                    WriteWindowZOrder();
				}
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

        [Conditional("DEBUG_WINDOW_ZORDER")]
        internal void WriteWindowZOrder()
        {
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine("Windows: (bottom-to-top order, [V] = visible, [NI] = non-interactive)");
            Console.WriteLine("  Visible: {0}", String.Join(", ", VisibleWindows.Select(w => w.GetType().Name).ToArray()));
            Console.WriteLine("  All:     {0}", String.Join(", ", Windows.Select(w => String.Format("{0}{1}{2}", w.GetType().Name, w.Interactive ? "" : "[NI]", w.Visible ? "[V]" : "")).ToArray()));
            Console.WriteLine();
        }
	}
}
