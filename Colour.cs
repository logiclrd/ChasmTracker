namespace ChasmTracker;

public struct Colour
{
	public byte R, G, B;

	public void ToYUV(out int y, out int u, out int v)
	{
		// YCbCr
		y = (int)( 0.257 * R + 0.504 * G + 0.098 * B +  16);
		u = (int)(-0.148 * R - 0.291 * G + 0.439 * B + 128);
		v = (int)( 0.439 * R - 0.368 * G - 0.071 * B + 128);
	}
}
