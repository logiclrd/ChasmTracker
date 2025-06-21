namespace ChasmTracker.Utility;

public struct Size
{
	public int Width;
	public int Height;

	public Size()
	{
	}

	public Size(int width)
	{
		Width = width;
		Height = 1;
	}

	public Size(int width, int height)
	{
		Width = width;
		Height = height;
	}

	public static Size operator /(Size size, int divisor)
		=> new Size(size.Width / divisor, size.Height / divisor);
}
