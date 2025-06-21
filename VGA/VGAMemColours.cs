using System.Data;

namespace ChasmTracker.VGA;

public struct VGAMemColours
{
	public byte FG; /* 0...15 */
	public byte BG; /* 0...15 */

	public VGAMemColours()
	{
	}

	public VGAMemColours(byte fg, byte bg)
	{
		FG = fg;
		BG = bg;
	}

	public static implicit operator VGAMemColours((byte FG, byte BG) colours)
		=> new VGAMemColours(colours.FG, colours.BG);

	public static implicit operator VGAMemColours((int FG, byte BG) colours)
		=> new VGAMemColours(unchecked((byte)colours.FG), colours.BG);

	public static implicit operator VGAMemColours((int FG, int BG) colours)
		=> new VGAMemColours(unchecked((byte)colours.FG), (byte)colours.BG);
}
