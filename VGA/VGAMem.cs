using System;

namespace ChasmTracker.VGA;

public class VGAMem
{
	public const int DefaultForeground = 3;

	public void DrawText(string text, Point position, int fg, int bg)
	{
		// TODO
	}

	public void DrawText(string text, int offset, Point position, int fg, int bg)
	{
		// TODO
	}

	public void DrawFillChars(Point s, Point e, int fg, int bg)
	{
		// TODO
	}

	public void DrawTextLen(string text, int len, Point position, int fg, int bg)
	{
		// TODO
	}

	public void DrawTextLen(string text, int offset, int len, Point position, int fg, int bg)
	{
		// TODO
	}

	public void DrawTextBIOSLen(string text, int len, Point position, int fg, int bg)
	{
		// TODO
	}

	public void DrawTextBIOSLen(string text, int offset, int len, Point position, int fg, int bg)
	{
		// TODO
	}

	public void DrawBox(Point topLeft, Point bottomRight, BoxTypes flags)
	{
		// TODO
	}

	public void DrawCharacter(char ch, Point position, int fg, int bg)
	{
		// TODO
	}

	public void DrawHalfWidthCharacters(char c1, char c2, Point position, int fg1, int bg1, int fg2, int bg2)
	{
		// TODO
	}

	public void DrawThumbBar(Point position, int width, int minimum, int maximum, int value, bool isSelected)
	{
		// TODO
	}

	public void ApplyOverlay(Overlay overlay)
	{
		// TODO
	}

	public void Scan8(int y, IntPtr pixels, ref ChannelData tpal, int[] mouseLine, int[] mouseLineMask)
	{
		// TODO
	}

	public void Scan16(int y, IntPtr pixels, ref ChannelData tpal, int[] mouseLine, int[] mouseLineMask)
	{
		// TODO
	}

	public void Scan32(int y, IntPtr pixels, ref ChannelData tpal, int[] mouseLine, int[] mouseLineMask)
	{
		// TODO
	}
}
