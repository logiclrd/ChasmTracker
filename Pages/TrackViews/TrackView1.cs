namespace ChasmTracker.Pages.TrackViews;

using ChasmTracker.Songs;
using ChasmTracker.Utility;
using ChasmTracker.VGA;

public class TrackView1 : TrackView
{
	public override int Width => 1;

	public override void DrawChannelHeader(int chan, Point position, byte fg)
	{
		char ch1 = unchecked((char)('0' + chan / 10));
		char ch2 = unchecked((char)('0' + chan % 10));

		VGAMem.DrawHalfWidthCharacters(ch1, ch2, position, (fg, 1), (fg, 1));
	}

	void DrawEffect(Point position, SongNote note, int cursorPos, VGAMemColours colours)
	{
		var colours1 = colours;
		var colours2 = colours;

		switch (cursorPos)
		{
			case 0:
				break;
			case 6:
				colours = (0, 3);
				break;
			case 7:
				colours1 = (0, 3);
				break;
			case 8:
				colours2 = (0, 3);
				break;
			default:
				colours.FG = 2;
				break;
		}

		if ((cursorPos == 7) || (cursorPos == 8) || ((note.Effect == 0) && (note.Parameter != 0)))
			VGAMem.DrawHalfWidthCharacters(HexDigit(note.Parameter >> 4),
				HexDigit(note.Parameter & 0xF),
				position.Advance(1), colours1, colours2);
		else
			VGAMem.DrawCharacter(note.EffectChar, position, colours);
	}

	public override void DrawNote(Point position, SongNote note, int cursorPos, VGAMemColours colours)
	{
		string buf;

		switch (cursorPos)
		{
			case 0:
				colours = (0, 3);
				if (note.NoteIsNote)
				{
					buf = note.NoteStringShort;
					VGAMem.DrawHalfWidthCharacters(buf[0], buf[1], position, colours, colours);
					return;
				}
				break;
			case 1:
				buf = note.NoteStringShort;
				VGAMem.DrawHalfWidthCharacters(buf[0], buf[1], position, colours, (0, 3));
				return;
			case 2:
			case 3:
				cursorPos -= 2;

				buf = note.HasInstrument ? note.NoteString : "\xAD\xAD";

				if (cursorPos == 0)
					VGAMem.DrawHalfWidthCharacters(buf[0], buf[1], position, (0, 3), colours);
				else
					VGAMem.DrawHalfWidthCharacters(buf[0], buf[1], position, colours, (0, 3));
				return;
			case 4:
			case 5:
				cursorPos -= 4;

				buf = note.VolumeString;

				colours.FG = (note.VolumeEffect == VolumeEffects.Panning) ? (byte)1 : (byte)2;

				if (cursorPos == 0)
					VGAMem.DrawHalfWidthCharacters(buf[0], buf[1], position, (0, 3), colours);
				else
					VGAMem.DrawHalfWidthCharacters(buf[0], buf[1], position, colours, (0, 3));
				return;
			case 9:
				cursorPos = 6;
				goto case 6;
			// fall through
			case 6:
			case 7:
			case 8:
				DrawEffect(position, note, cursorPos, colours);
				return;
		}

		if (note.HasNote)
			VGAMem.DrawCharacter(note.NoteStringShort[0], position, colours);
		else if (note.HasInstrument)
		{
			buf = note.InstrumentString;
			VGAMem.DrawHalfWidthCharacters(buf[0], buf[1], position, colours, colours);
		}
		else if (note.VolumeEffect != VolumeEffects.None)
		{
			if (cursorPos != 0)
				colours.FG = (note.VolumeEffect == VolumeEffects.Panning) ? (byte)1 : (byte)2;

			buf = note.VolumeString;

			VGAMem.DrawHalfWidthCharacters(buf[0], buf[1], position, colours, colours);
		}
		else if ((note.Effect != 0) || (note.Parameter != 0))
			DrawEffect(position, note, cursorPos, colours);
		else
			VGAMem.DrawCharacter('\xAD', position, colours);
	}

	public override void DrawMask(Point position, PatternEditorMask mask, int cursorPos, VGAMemColours colours)
	{
		char c = '\x8F';

		switch (cursorPos)
		{
			case 0: case 1:
				c = MASK_CHAR(PatternEditorMask.Note, 0, 1, mask, cursorPos);
				break;
			case 2: case 3:
				c = MASK_CHAR(PatternEditorMask.Instrument, 2, 3, mask, cursorPos);
				break;
			case 4: case 5:
				c = MASK_CHAR(PatternEditorMask.Volume, 4, 5, mask, cursorPos);
				break;
			case 6:
				c = MASK_CHAR(PatternEditorMask.Effect, 6, -1, mask, cursorPos);
				break;
			case 7: case 8:
				c = MASK_CHAR(PatternEditorMask.Effect, 7, 8, mask, cursorPos);
				break;
		}

		VGAMem.DrawCharacter(c, position, colours);
	}
}
