﻿/// COPYRIGHT 2010 by the Open Rails project.
/// This code is provided to enable you to contribute improvements to the open rails program.  
/// Use of the code for any other purpose or distribution of the code to anyone else
/// is prohibited without specific written permission from admin@openrails.org.

/// Author: James Ross

using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace ORTS.Popups
{
	public abstract class Window : RenderPrimitive
	{
		public static readonly Point DecorationSize = new Point(4 + 4, 4 + 16 + 5 + 4);
		public Matrix XNAWorld;
		protected WindowManager Owner;
		bool visible = false;
		Rectangle location = new Rectangle(0, 0, 100, 100);
		string Caption;
		ControlLayout WindowLayout;
		VertexBuffer WindowVertexBuffer;
		IndexBuffer WindowIndexBuffer;

		public Window(WindowManager owner, int width, int height, string caption)
		{
			Owner = owner;
			location = new Rectangle(0, 0, width, height);
			Caption = caption;
			Owner.Add(this);
			VisibilityChanged();
			LocationChanged();
			SizeChanged();
		}

		protected virtual void VisibilityChanged()
		{
		}

		protected virtual void LocationChanged()
		{
			XNAWorld = Matrix.CreateWorld(new Vector3(location.X, location.Y, 0), -Vector3.UnitZ, Vector3.UnitY);
		}

		protected virtual void SizeChanged()
		{
			Layout();
			WindowVertexBuffer = null;
		}

		internal virtual void ActiveChanged()
		{
		}

		public bool Visible
		{
			get
			{
				return visible;
			}
			set
			{
				if (visible != value)
				{
					visible = value;
					VisibilityChanged();
				}
			}
		}

		public Rectangle Location
		{
			get
			{
				return location;
			}
		}

		public void MoveTo(int x, int y)
		{
			x = (int)MathHelper.Clamp(x, 0, Owner.ScreenSize.X - location.Width);
			y = (int)MathHelper.Clamp(y, 0, Owner.ScreenSize.Y - location.Height);

			if ((location.X != x) || (location.Y != y))
			{
				location.X = x;
				location.Y = y;
				LocationChanged();
			}
		}

		public void MoveBy(int dx, int dy)
		{
			MoveTo(location.X + dx, location.Y + dy);
		}

		public void SizeTo(int width, int height)
		{
			if ((location.Width != width) || (location.Height != height))
			{
				location.Width = width;
				location.Height = height;
				MoveTo(location.X, location.Y);
				SizeChanged();
			}
		}

		public enum AlignAt
		{
			Start,
			Middle,
			End,
		};

		public void Align(AlignAt horizontal, AlignAt vertical)
		{
			MoveTo(horizontal == AlignAt.Start ? 0 : horizontal == AlignAt.Middle ? (Owner.ScreenSize.X - location.Width) / 2 : Owner.ScreenSize.X - location.Width,
				vertical == AlignAt.Start ? 0 : vertical == AlignAt.Middle ? (Owner.ScreenSize.Y - location.Height) / 2 : Owner.ScreenSize.Y - location.Height);
		}

		protected void Layout()
		{
			WindowLayout = new WindowControlLayout(this, location.Width, location.Height);
			Layout(WindowLayout);
		}

		protected virtual ControlLayout Layout(ControlLayout layout)
		{
			// Pad window by 4px, add caption and space between to content area.
			var content = layout.AddLayoutOffset(4, 4, 4, 4).AddLayoutVertical();
			content.Add(new Label(content.RemainingWidth, 16, Caption, LabelAlignment.Center));
			content.AddSpace(0, 5);
			return content;
		}

		public override void Draw(GraphicsDevice graphicsDevice)
		{
			if (WindowVertexBuffer == null)
			{
				// Edges/corners are 32px (1/4th image size).
				var vertexData = new[] {
					//  0  1  2  3
					new VertexPositionTexture(new Vector3(0 * location.Width + 00, 0 * location.Height + 00, 0), new Vector2(0.00f / 2, 0.00f)),
					new VertexPositionTexture(new Vector3(0 * location.Width + 32, 0 * location.Height + 00, 0), new Vector2(0.25f / 2, 0.00f)),
					new VertexPositionTexture(new Vector3(1 * location.Width - 32, 0 * location.Height + 00, 0), new Vector2(0.75f / 2, 0.00f)),
					new VertexPositionTexture(new Vector3(1 * location.Width - 00, 0 * location.Height + 00, 0), new Vector2(1.00f / 2, 0.00f)),
					//  4  5  6  7
					new VertexPositionTexture(new Vector3(0 * location.Width + 00, 0 * location.Height + 32, 0), new Vector2(0.00f / 2, 0.25f)),
					new VertexPositionTexture(new Vector3(0 * location.Width + 32, 0 * location.Height + 32, 0), new Vector2(0.25f / 2, 0.25f)),
					new VertexPositionTexture(new Vector3(1 * location.Width - 32, 0 * location.Height + 32, 0), new Vector2(0.75f / 2, 0.25f)),
					new VertexPositionTexture(new Vector3(1 * location.Width - 00, 0 * location.Height + 32, 0), new Vector2(1.00f / 2, 0.25f)),
					//  8  9 10 11
					new VertexPositionTexture(new Vector3(0 * location.Width + 00, 1 * location.Height - 32, 0), new Vector2(0.00f / 2, 0.75f)),
					new VertexPositionTexture(new Vector3(0 * location.Width + 32, 1 * location.Height - 32, 0), new Vector2(0.25f / 2, 0.75f)),
					new VertexPositionTexture(new Vector3(1 * location.Width - 32, 1 * location.Height - 32, 0), new Vector2(0.75f / 2, 0.75f)),
					new VertexPositionTexture(new Vector3(1 * location.Width - 00, 1 * location.Height - 32, 0), new Vector2(1.00f / 2, 0.75f)),
					// 12 13 14 15
					new VertexPositionTexture(new Vector3(0 * location.Width + 00, 1 * location.Height - 00, 0), new Vector2(0.00f / 2, 1.00f)),
					new VertexPositionTexture(new Vector3(0 * location.Width + 32, 1 * location.Height - 00, 0), new Vector2(0.25f / 2, 1.00f)),
					new VertexPositionTexture(new Vector3(1 * location.Width - 32, 1 * location.Height - 00, 0), new Vector2(0.75f / 2, 1.00f)),
					new VertexPositionTexture(new Vector3(1 * location.Width - 00, 1 * location.Height - 00, 0), new Vector2(1.00f / 2, 1.00f)),
				};
				WindowVertexBuffer = new VertexBuffer(graphicsDevice, typeof(VertexPositionTexture), vertexData.Length, BufferUsage.WriteOnly);
				WindowVertexBuffer.SetData(vertexData);
			}
			if (WindowIndexBuffer == null)
			{
				var indexData = new short[] {
					0, 4, 1, 5, 2, 6, 3, 7,
					11, 6, 10, 5, 9, 4, 8,
					12, 9, 13, 10, 14, 11, 15,
				};
				WindowIndexBuffer = new IndexBuffer(graphicsDevice, typeof(short), indexData.Length, BufferUsage.WriteOnly);
				WindowIndexBuffer.SetData(indexData);
			}

			graphicsDevice.VertexDeclaration = new VertexDeclaration(graphicsDevice, VertexPositionTexture.VertexElements);
			graphicsDevice.Vertices[0].SetSource(WindowVertexBuffer, 0, VertexPositionTexture.SizeInBytes);
			graphicsDevice.Indices = WindowIndexBuffer;
			graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleStrip, 0, 0, 16, 0, 20);
		}

		public void Draw(SpriteBatch spriteBatch)
		{
			WindowLayout.Draw(spriteBatch, Location.Location);
		}

		public void MouseDown()
		{
			WindowLayout.HandleMouseDown(new WindowMouseEvent(Owner, this));
		}

		public void MouseUp()
		{
			WindowLayout.HandleMouseUp(new WindowMouseEvent(Owner, this));
		}

		public void MouseMove()
		{
			WindowLayout.HandleMouseMove(new WindowMouseEvent(Owner, this));
		}

		public void HandleUserInput()
		{
			WindowLayout.HandleUserInput(new WindowMouseEvent(Owner, this));
		}
	}

	public class WindowMouseEvent
	{
		public readonly Point MousePosition;
		public readonly Point MouseDownPosition;
		public readonly Point MouseScreenPosition;
		public readonly Point MouseDownScreenPosition;

		public WindowMouseEvent(WindowManager windowManager, Window window)
		{
			MousePosition = new Point(UserInput.MouseState.X - window.Location.X, UserInput.MouseState.Y - window.Location.Y);
			MouseDownPosition = new Point(windowManager.MouseDownPosition.X - window.Location.X, windowManager.MouseDownPosition.Y - window.Location.Y);
			MouseScreenPosition = new Point(UserInput.MouseState.X, UserInput.MouseState.Y);
			MouseDownScreenPosition = windowManager.MouseDownPosition;
		}
	}

	class WindowControlLayout : ControlLayout
	{
		public readonly Window Window;

		public WindowControlLayout(Window window, int width, int height)
			: base(0, 0, width, height)
		{
			Window = window;
		}

		static readonly Point DragInvalid = new Point(-1, -1);
		Point DragWindowOffset;
		bool Dragging;

		internal override bool HandleMouseDown(WindowMouseEvent e)
		{
			DragWindowOffset = DragInvalid;
			if (base.HandleMouseDown(e))
				return true;
			DragWindowOffset = new Point(e.MouseDownScreenPosition.X - Window.Location.X, e.MouseDownScreenPosition.Y - Window.Location.Y);
			return true;
		}

		internal override bool HandleMouseUp(WindowMouseEvent e)
		{
			if (base.HandleMouseUp(e))
				return true;
			if (Dragging)
				Dragging = false;
			return true;
		}

		internal override bool HandleMouseMove(WindowMouseEvent e)
		{
			if (base.HandleMouseMove(e))
				return true;
			if (UserInput.IsMouseLeftButtonDown() && !Dragging && (DragWindowOffset != DragInvalid) && ((MathHelper.Distance(e.MouseScreenPosition.X, e.MouseDownScreenPosition.X) > WindowManager.DragMinimumDistance) || (MathHelper.Distance(e.MouseScreenPosition.Y, e.MouseDownScreenPosition.Y) > WindowManager.DragMinimumDistance)))
				Dragging = true;
			else if (UserInput.IsMouseLeftButtonDown() && Dragging)
				Window.MoveTo(e.MouseScreenPosition.X - DragWindowOffset.X, e.MouseScreenPosition.Y - DragWindowOffset.Y);
			return true;
		}

		internal override bool HandleUserInput(WindowMouseEvent e)
		{
			if (base.HandleUserInput(e))
				return true;
			return true;
		}
	}
}
