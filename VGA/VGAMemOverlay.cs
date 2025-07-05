using System;

namespace ChasmTracker.VGA;

using ChasmTracker.Utility;

/* ---------------------------------------------------------------------------
 * drawing overlays; can draw practically anything given the length
 * and height are a multiple of 8 (i.e. the size of a single character) */
public class VGAMemOverlay
{
	public Point TopLeft; /* in character cells... */
	public Point BottomRight;
	public Size Size; /* in pixels */

	public Span<byte> Q => VGAMem.Overlay.Slice(_q);
	public int Skip => _skip;

	int _q; /* offset to points inside ovl */
	int _skip; /* (Constants.NativeScreenWidth - width) (this is stupid and needs to go away) */

	public VGAMemOverlay(Point topLeft, Point bottomRight)
	{
		TopLeft = topLeft;
		BottomRight = bottomRight;
		Size = bottomRight - topLeft;

		_q = topLeft.X * 8 + topLeft.Y * 8 * Constants.NativeScreenWidth;
		_skip = 640 - Size.Width;
	}

	public byte this[int x, int y]
	{
		get => VGAMem.Overlay[_q + x + y * Constants.NativeScreenWidth];
		set => VGAMem.Overlay[_q + x + y * Constants.NativeScreenWidth] = value;
	}

	public byte this[Point pt]
	{
		get => this[pt.X, pt.Y];
		set => this[pt.X, pt.Y] = value;
	}

	public void Clear(byte colour)
	{
		for (int y = 0; y < Size.Height; y++)
			for (int x = 0; x < Size.Width; x++)
				this[x, y] = colour;
	}

	public void DrawLineV(int x, int ys, int ye, byte colour)
	{
		if (ys > ye)
			(ys, ye) = (ye, ys);

		for (int y = ys; y <= ye; y++)
			this[x, y] = colour;
	}

	public void DrawLineH(int xs, int xe, int y, byte colour)
	{
		if (xs > xe)
			(xs, xe) = (xe, xs);

		for (int x = xs; x <= xe; x++)
			this[x, y] = colour;
	}

	public void DrawLine(Point s, Point e, byte colour)
	{
		int dx = e.X - s.X;

		if (dx == 0)
		{
			DrawLineV(s.X, s.Y, e.Y, colour);
			return;
		}

		int dy = e.Y - s.Y;

		if (dy == 0)
		{
			DrawLineH(s.X, e.X, e.Y, colour);
			return;
		}

		int ax = Math.Abs(dx) << 1;
		int sx = Math.Sign(dx);

		int ay = Math.Abs(dy) << 1;
		int sy = Math.Sign(dy);

		int x = s.X;
		int y = s.Y;

		if (ax > ay)
		{
			/* x dominant */
			int d = ay - (ax >> 1);

			while (true)
			{
				this[x, y] = colour;

				if (x == e.X)
					break;

				if (d >= 0)
				{
					y += sy;
					d -= ax;
				}

				x += sx;
				d += ay;
			}
		}
		else
		{
			/* y dominant */
			int d = ax - (ay >> 1);

			while (true)
			{
				this[x, y] = colour;

				if (y == e.Y)
					break;

				if (d >= 0)
				{
					x += sx;
					d -= ay;
				}

				y += sy;
				d += ax;
			}
		}
	}
}
