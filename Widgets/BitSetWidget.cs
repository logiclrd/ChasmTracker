using ChasmTracker.VGA;

namespace ChasmTracker.Widgets;

public class BitSetWidget : Widget
{
	public int NumberOfBits;
	public int Value;
	public SharedInt CursorPosition;
	public string[] BitsOn;
	public string[] BitsOff;
	public char[] ActivationKeys;

	public BitSetWidget(Point position, int width, int nbits, string[] bitsOn, string[] bitsOff, SharedInt cursorPos)
		: base(position, width)
	{
		NumberOfBits = nbits;
		CursorPosition = cursorPos;
		BitsOn = bitsOn;
		BitsOff = bitsOff;

		ActivationKeys = new char[nbits];
	}

	/* In textentries, cursor=0,3; normal=2,0 */
	static int[] FGSelection =
		new int[]
		{
			2, /* not cursor, not set */
			3, /* not cursor, is  set */
			0, /* has cursor, not set */
			0  /* has cursor, is  set */
		};

	static int[] BGSelection =
		new int[]
		{
			0, /* not cursor, not set */
			0, /* not cursor, is  set */
			2, /* has cursor, not set */
			3  /* has cursor, is  set */
		};

	protected override void DrawWidget(VGAMem vgaMem, bool isSelected, int tfg, int tbg)
	{
		for (int n = 0; n < NumberOfBits; ++n)
		{
			bool set = (Value & (1 << n)) != 0;

			string label = set ? BitsOn[n] : BitsOff[n];

			char label_c1 = label[0];
			char label_c2 = (label.Length >= 1) ? label[1] : '\0';

			bool isFocused = isSelected && (n == CursorPosition);

			int fg = FGSelection[(set ? 1 : 0) + (isFocused ? 2 : 0)];
			int bg = BGSelection[(set ? 1 : 0) + (isFocused ? 2 : 0)];

			if (label_c2 != '\0')
				vgaMem.DrawHalfWidthCharacters(label_c1, label_c2, Position.Advance(n), fg, bg, fg, bg);
			else
				vgaMem.DrawCharacter(label_c1, Position.Advance(n), fg, bg);
		}
	}
}
