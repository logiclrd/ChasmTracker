namespace ChasmTracker.Pages;

using ChasmTracker.Utility;
using ChasmTracker.VGA;

public class WaterfallPage : Page
{
	public WaterfallPage()
		: base(PageNumbers.Waterfall, "", HelpTexts.Global)
	{
		/* get the _whole_ display */
		_ovl = VGAMem.AllocateOverlay(new Point(0, 0), new Point(79, 49));
	}

	const int ScopeRows = 32;

	VGAMemOverlay _ovl;

	/* Convert the output of */
	static int DoBits(byte[] q, int qOffset, byte[] @in, int offset, int length, int y)
	{
		int i, c;
		for (i = 0; i < length; i++)
		{
			/* j is has range 0 to 128. Now use the upper side for drawing.*/
			c = 128 + @in[i + offset];
			if (c > 255) c = 255;
			q[qOffset] = (byte)c;
			qOffset += y;
		}
		return qOffset;
	}

	void DrawSlice(int x, int h, byte c)
	{
		int y = ((h>>2) % ScopeRows)+1;

		_ovl.DrawLine(
			new Point(x, Constants.NativeScreenHeight-y),
			new Point(x, Constants.NativeScreenHeight-1),
			c);
	}

	void ProcessVisualization()
	{
	}

	public override void DrawFull()
	{
		// TODO
	}

}
