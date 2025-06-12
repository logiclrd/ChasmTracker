namespace ChasmTracker.VGA;

public class Overlay
{
	public Point TopLeft, BottomRight;
	public Image Image;

	public Overlay(Point topLeft, Point bottomRight, Image image)
	{
		TopLeft = topLeft;
		BottomRight = bottomRight;
		Image = image;
	}

	public Overlay(int x1, int y1, int x2, int y2, Image image)
		: this(new Point(x1, y1), new Point(x2, y2), image)
	{
	}

	public Overlay(Image image)
	{
		Image = image;
	}
}
