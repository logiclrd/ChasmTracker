using System.IO;

namespace ChasmTracker.VGA;

using SkiaSharp;

public class Image
{
	public Size Size;
	public uint[] PixelData;

	public uint this[int x, int y]
	{
		get => PixelData[x + y * Size.Width];
		set => PixelData[x + y * Size.Width] = value;
	}

	public Image(int width, int height)
		: this(new Size(width, height))
	{
	}

	public Image(Size size)
	{
		Size = size;
		PixelData = new uint[size.Width * size.Height];
	}

	public static Image LoadFrom(Stream stream)
	{
		var bits = SKBitmap.Decode(stream);

		if (bits.ColorType != SKColorType.Rgba8888)
		{
			var converted = new SKBitmap(bits.Width, bits.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);

			bits.CopyTo(converted);

			bits = converted;
		}

		var ret = new Image(bits.Width, bits.Height);

		var sourcePixels = bits.Pixels;

		for (int i = 0; i < ret.PixelData.Length; i++)
			ret.PixelData[i] = (uint)sourcePixels[i];

		return ret;
	}
}
