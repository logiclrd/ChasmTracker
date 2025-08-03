using System;
using System.Linq;

namespace ChasmTracker.VGA;

public class Palette
{
	public string Name;
	public int Index { get; private set; }
	public bool IsReadOnly => Index > 0;
	public byte[,] Colours = new byte[16, 3];

	public Palette(string name, byte[,] colours)
	{
		Name = name;
		Array.Copy(colours, this.Colours, Colours.Length);
	}

	/* this should be called only by the Palettes static constructor */
	public Palette SetIndex(int newIndex)
	{
		Index = newIndex;
		return this;
	}

	public byte this[int colour, int channel]
	{
		get => Colours[colour, channel];
		set => Colours[colour, channel] = value;
	}

	public (byte Red, byte Green, byte Blue) this[int colour] => (Colours[colour, 0], Colours[colour, 1], Colours[colour, 2]);

	static char[] PaletteTranslation = ".0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ_abcdefghijklmnopqrstuvwxyz".ToCharArray();

	public bool SetFromString(string strIn)
	{
		if (IsReadOnly)
			return false;

		// Remove bad characters from beginning (spaces etc.).
			int startIndex = strIn.IndexOfAny(PaletteTranslation);

		if (startIndex > 0)
			strIn = strIn.Substring(startIndex);

		if (strIn.Length < 48)
			return false;

		for (int i = 0; i < 48; i++)
			if (!PaletteTranslation.Contains(strIn[i]))
				return false;

		for (int n = 0; n < 16; n++)
			for (int c = 0; c < 3; c++)
				Colours[n, c] = unchecked((byte)(Array.IndexOf(PaletteTranslation, strIn[n * 3 + c])));

		return true;
	}

	public override string ToString()
	{
		char[] strOut = new char[48];

		for (int n = 0; n < 16; n++)
			for (int c = 0; c < 3; c++)
				strOut[n * 3 + c] = PaletteTranslation[Colours[n, c]];

		return new string(strOut);
	}

	public void Apply()
	{
		Video.SetPalette(VGAMem.CurrentPalette);

		/* is the "light" border color actually darker than the "dark" color? */
		int lightBorderIntensity = VGAMem.CurrentPalette[3, 0] + VGAMem.CurrentPalette[3, 1] + VGAMem.CurrentPalette[3, 2];
		int darkBorderIntensity = VGAMem.CurrentPalette[1, 0] + VGAMem.CurrentPalette[1, 1] + VGAMem.CurrentPalette[1, 2];

		if (darkBorderIntensity > lightBorderIntensity)
			Status.Flags |= StatusFlags.InvertedPalette;
		else
			Status.Flags &= ~StatusFlags.InvertedPalette;
	}
}
