﻿/// COPYRIGHT 2010 by the Open Rails project.
/// This code is provided to enable you to contribute improvements to the open rails program.  
/// Use of the code for any other purpose or distribution of the code to anyone else
/// is prohibited without specific written permission from admin@openrails.org.

/// Autor James Ross

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ORTS
{
	public abstract class PopupControl
	{
		public Rectangle Position;

		protected PopupControl(int x, int y, int width, int height)
		{
			Position = new Rectangle(x, y, width, height);
		}

		internal abstract void Draw(SpriteBatch spriteBatch, Point offset);
	}

	public class PopupSpacer : PopupControl
	{
		public PopupSpacer(int width, int height)
			: base(0, 0, width, height)
		{
		}

		internal override void Draw(SpriteBatch spriteBatch, Point offset)
		{
		}
	}

	public enum PopupLabelAlignment {
		Left,
		Center,
		Right,
	}

	public class PopupLabel : PopupControl
	{
		public string Text;
		public PopupLabelAlignment Align;

		public PopupLabel(int x, int y, int width, int height, string text, PopupLabelAlignment align)
			: base(x, y, width, height)
		{
			Text = text;
			Align = align;
		}

		public PopupLabel(int x, int y, int width, int height, string text)
			: this(x, y, width, height, text, PopupLabelAlignment.Left)
		{
		}

		public PopupLabel(int width, int height, string text, PopupLabelAlignment align)
			: this(0, 0, width, height, text, align)
		{
		}

		public PopupLabel(int width, int height, string text)
			: this(0, 0, width, height, text, PopupLabelAlignment.Left)
		{
		}

		internal override void Draw(SpriteBatch spriteBatch, Point offset)
		{
			var pos = new Vector2(offset.X + Position.X, offset.Y + Position.Y);
			if (Align != PopupLabelAlignment.Left)
			{
				var size = Materials.PopupWindowMaterial.DefaultFont.MeasureString(Text);
				if (Align == PopupLabelAlignment.Right)
					pos.X += Position.Width - size.X;
				else
					pos.X += (int)(Position.Width - size.X) / 2;
			}
			spriteBatch.DrawString(Materials.PopupWindowMaterial.DefaultFont, Text, pos, Color.White);
		}
	}

	public class PopupImage : PopupControl
	{
		public Texture2D Texture;

		public PopupImage(int x, int y, int width, int height, System.Drawing.Bitmap image)
			: base(x, y, width, height)
		{
			Image = image;
		}

		public PopupImage(int width, int height, System.Drawing.Bitmap image)
			: this(0, 0, width, height, image)
		{
		}

		public System.Drawing.Bitmap Image
		{
			set
			{
				//Texture = new Texture2D(null, Texture.Width, Texture.Height);
			}
		}

		internal override void Draw(SpriteBatch spriteBatch, Point offset)
		{
			var redTexture = new Texture2D(spriteBatch.GraphicsDevice, 1, 1, 1, TextureUsage.None, SurfaceFormat.Color);
			redTexture.SetData(new[] { Color.Red });
			spriteBatch.Draw(redTexture, new Rectangle(offset.X + Position.X, offset.Y + Position.Y, Position.Width, Position.Height), Color.White);
		}
	}

	public class PopupControlLayout : PopupControl
	{
		protected readonly List<PopupControl> Controls = new List<PopupControl>();

		public PopupControlLayout(int x, int y, int width, int height)
			: base(x, y, width, height)
		{
		}

		public virtual int RemainingWidth
		{
			get
			{
				return Position.Width;
			}
		}

		public virtual int RemainingHeight
		{
			get
			{
				return Position.Height;
			}
		}

		public virtual int CurrentLeft
		{
			get
			{
				return 0;
			}
		}

		public virtual int CurrentTop
		{
			get
			{
				return 0;
			}
		}

		T InternalAdd<T>(T control) where T : PopupControl
		{
			// Offset control by our location. Don't touch its size!
			control.Position.Location = new Point(control.Position.Location.X + Position.Location.X + CurrentLeft, control.Position.Location.Y + Position.Location.Y + CurrentTop);
			//Debug.WriteLine(String.Format("{0} added {1} at {2}", GetType().Name, control.GetType().Name, control.Position));
			Controls.Add(control);
			return control;
		}

		public void Add(PopupControl control)
		{
			InternalAdd(control);
		}

		public void AddSpace(int width, int height)
		{
			Add(new PopupSpacer(width, height));
		}

		public PopupControlLayoutOffset AddLayoutOffset(int left, int top, int right, int bottom)
		{
			return InternalAdd(new PopupControlLayoutOffset(RemainingWidth, RemainingHeight, left, top, right, bottom));
		}

		public PopupControlLayoutHorizontal AddLayoutHorizontal()
		{
			return AddLayoutHorizontal(RemainingHeight);
		}

		public PopupControlLayoutHorizontal AddLayoutHorizontal(int height)
		{
			return InternalAdd(new PopupControlLayoutHorizontal(RemainingWidth, height));
		}

		public PopupControlLayoutVertical AddLayoutVertical()
		{
			return AddLayoutVertical(RemainingWidth);
		}

		public PopupControlLayoutVertical AddLayoutVertical(int width)
		{
			return InternalAdd(new PopupControlLayoutVertical(width, RemainingHeight));
		}

		internal override void Draw(SpriteBatch spriteBatch, Point offset)
		{
			foreach (var control in Controls)
				control.Draw(spriteBatch, offset);
		}
	}

	public class PopupControlLayoutOffset : PopupControlLayout
	{
		readonly int PadLeft;
		readonly int PadTop;
		readonly int PadRight;
		readonly int PadBottom;

		public PopupControlLayoutOffset(int width, int height, int left, int top, int right, int bottom)
			: base(left, top, width - left - right, height - top - bottom)
		{
			PadLeft = left;
			PadTop = top;
			PadRight = right;
			PadBottom = bottom;
		}
	}

	public class PopupControlLayoutHorizontal : PopupControlLayout
	{
		public PopupControlLayoutHorizontal(int width, int height)
			: base(0, 0, width, height)
		{
		}

		public override int RemainingWidth
		{
			get
			{
				return base.RemainingWidth - CurrentLeft;
			}
		}

		public override int CurrentLeft
		{
			get
			{
				return Controls.Sum(c => c.Position.Width);
			}
		}
	}

	public class PopupControlLayoutVertical : PopupControlLayout
	{
		public PopupControlLayoutVertical(int width, int height)
			: base(0, 0, width, height)
		{
		}

		public override int RemainingHeight
		{
			get
			{
				return base.RemainingHeight - CurrentTop;
			}
		}

		public override int CurrentTop
		{
			get
			{
				return Controls.Sum(c => c.Position.Height);
			}
		}
	}
}
