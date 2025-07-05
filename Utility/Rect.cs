namespace ChasmTracker.Utility;

public struct Rect
{
	public Point TopLeft;
	public Size Size;

	public Rect()
	{
	}

	public Rect(Point topLeft, Size size)
	{
		TopLeft = topLeft;
		Size = size;
	}

	public Rect(int x, int y, int width, int height)
		: this(new Point(x, y), new Size(width, height))
	{
	}

	public Point BottomRight => TopLeft.Advance(Size);

	public bool Contains(Point pt)
	{
		if (pt.X < TopLeft.X)
			return false;
		if (pt.Y < TopLeft.Y)
			return false;

		var bottomRight = BottomRight;

		if (pt.X >= bottomRight.X)
			return false;
		if (pt.Y >= bottomRight.Y)
			return false;

		return true;
	}
}
