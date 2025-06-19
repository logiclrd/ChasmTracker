using System;
using System.ComponentModel;

namespace ChasmTracker;

public struct Point
{
	public int X;
	public int Y;

	public Point()
	{
	}

	public Point(int x, int y)
	{
		X = x;
		Y = y;
	}

	public Point Advance(int w, int h = 0)
	{
		return new Point(X + w, Y + h);
	}

	public Point Advance(Size size)
	{
		return new Point(X + size.Width, Y + size.Height);
	}

	public void Clamp(int w, int h)
	{
		if (X > w)
			X = w;
		if (Y > h)
			Y = h;
	}

	public static Point operator *(Point num, Size scale)
	{
		return new Point(num.X * scale.Width, num.Y * scale.Height);
	}

	public static Point operator /(Point num, Size den)
	{
		return new Point(num.X / den.Width, num.Y / den.Height);
	}

	public static Point operator /(Point num, Point den)
	{
		return new Point(num.X / den.X, num.Y / den.Y);
	}

	public static Size operator -(Point point, Point other)
	{
		return new Size(point.X - other.X, point.Y - other.Y);
	}

	public double DistanceTo(Point other)
	{
		double dx = other.X - X;
		double dy = other.Y - Y;

		return Math.Sqrt(dx * dx + dy * dy);
	}

	public static bool operator ==(Point a, Point b)
		=> (a.X == b.X) && (a.Y == b.Y);
	public static bool operator !=(Point a, Point b)
		=> (a.X != b.X) || (a.Y != b.Y);
}
