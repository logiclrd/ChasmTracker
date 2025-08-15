using ChasmTracker.Songs;
using ChasmTracker.VGA;

namespace ChasmTracker.Pages.TrackViews;

using ChasmTracker.Utility;

public abstract class TrackView
{
	public abstract int Width { get; }

	public abstract void DrawChannelHeader(int chan, Point position, byte fg);
	public abstract void DrawNote(Point position, ref SongNote note, int cursorPos, VGAMemColours colours);
	public abstract void DrawMask(Point position, PatternEditorMask mask, int cursorPos, VGAMemColours colours);

	/* --------------------------------------------------------------------- */
	/* pattern edit mask indicators */

	/*
	atnote  (1)  cursor_pos == 0
		over  (2)  cursor_pos == pos
	masked  (4)  mask & MASK_whatever
	*/
	static readonly char[] MaskChars =
		{
			'\x8F', // 0
			'\x8F', // atnote
			'\xA9', // over
			'\xA9', // over && atnote
			'\xAA', // masked
			'\xA9', // masked && atnote
			'\xAB', // masked && over
			'\xAB', // masked && over && atnote
		};

	protected static char MASK_CHAR(PatternEditorMask field, int pos, int pos2, PatternEditorMask mask, int cursorPos)
		=> MaskChars
				[
					((cursorPos == 0) ? 1 : 0) |
					((cursorPos == pos) ? 2 : 0) |
					((cursorPos == pos2) ? 2 : 0) |
					(mask.HasAllFlags(field) ? 4 : 0)
				];

	protected char HexDigit(int n)
		=> "0123456789ABCDEF"[n & 15];
}
