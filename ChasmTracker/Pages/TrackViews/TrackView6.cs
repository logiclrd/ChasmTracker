using ChasmTracker.Songs;
using ChasmTracker.VGA;

namespace ChasmTracker.Pages.TrackViews;

using ChasmTracker.Utility;

public class TrackView6 : TrackView
{
	public override int Width => 6;

	public override void DrawChannelHeader(int chan, Point position, byte fg)
	{
		VGAMem.DrawText($"Chnl{chan:d2}", position, (fg, 1));
	}

	public override void DrawNote(Point position, ref SongNote note, int cursorPos, VGAMemColours colours)
	{
		string noteBuf;

#if USE_LOWERCASE_NOTES

		noteBuf = note.NoteStringShort;

		/* note & instrument */
		VGAMem.DrawText(noteBuf, position, 6, bg);

		switch (cursorPos)
		{
			case 0:
				VGAMem.DrawCharacter(noteBuf[0], position, (0, 3));
				break;
			case 1:
				VGAMem.DrawCharacter(noteBuf[1], position.Advance(1), y, (0, 3));
				break;
			case 2:
				fg1 = 0;
				bg1 = 3;
				break;
			case 3:
				fg2 = 0;
				bg2 = 3;
				break;
		}

#else

		noteBuf = note.NoteString;

		if (cursorPos == 0)
			VGAMem.DrawCharacter(noteBuf[0], position, (0, 3));
		else
			VGAMem.DrawCharacter(noteBuf[0], position, colours);

		switch (noteBuf[0])
		{
			case '^':
			case '~':
			case '\xCD': // note off
			case '\xAD': // dot (empty)
				if (cursorPos == 1)
					VGAMem.DrawCharacter(noteBuf[1], position.Advance(1), (0, 3));
				else
					VGAMem.DrawCharacter(noteBuf[1], position.Advance(1), colours);
				break;
			default:
				VGAMem.DrawHalfWidthCharacters(noteBuf[1], noteBuf[2], position.Advance(1),
					colours, cursorPos == 1 ? (0, 3) : colours);
				break;
		}

#endif

		string instrumentBuf = note.HasInstrument ? note.InstrumentString : "\xAD\xAD";

		VGAMemColours colours1, colours2;

		if (note.HasInstrument)
			colours1 = colours2 = (10, colours.BG);
		else
			colours1 = colours2 = (2, colours.BG);

		switch (cursorPos)
		{
			case 2:
				colours1 = (0, 3);
				break;
			case 3:
				colours2 = (0, 3);
				break;
		}

		VGAMem.DrawHalfWidthCharacters(instrumentBuf[0], instrumentBuf[1], position.Advance(2), colours1, colours2);

		/* volume */
		string volumeBuf = note.VolumeString;

		switch (note.VolumeEffect)
		{
			case VolumeEffects.None:
				colours1.FG = 6;
				break;
			case VolumeEffects.Panning:
				colours1.FG = 10;
				break;
			case VolumeEffects.TonePortamento:
			case VolumeEffects.VibratoSpeed:
			case VolumeEffects.VibratoDepth:
				colours1.FG = 6;
				break;
			default:
				colours1.FG = 12;
				break;
		}

		colours1.BG = colours.BG;
		colours2 = colours1;

		switch (cursorPos)
		{
			case 4:
				colours1 = (0, 3);
				break;
			case 5:
				colours2 = (0, 3);
				break;
		}

		VGAMem.DrawHalfWidthCharacters(volumeBuf[0], volumeBuf[1], position.Advance(3), colours1, colours2);

		/* effect value */
		colours1 = colours2 = (10, colours.BG);
		switch (cursorPos)
		{
			case 7:
				colours1 = (0, 3);
				break;
			case 8:
				colours2 = (0, 3);
				break;
			case 9:
				colours1 = colours2 = (0, 3);
				cursorPos = 6; // hack
				break;
		}

		VGAMem.DrawHalfWidthCharacters(
			HexDigit(note.Parameter >> 4),
			HexDigit(note.Parameter & 0xF),
			position.Advance(5), colours1, colours2);

		/* effect */
		if (cursorPos == 6)
			VGAMem.DrawCharacter(note.EffectChar, position.Advance(4), (0, 3));
		else
			VGAMem.DrawCharacter(note.EffectChar, position.Advance(4), (2, colours.BG));
	}

	public override void DrawMask(Point position, PatternEditorMask mask, int cursorPos, VGAMemColours colours)
	{
		var buf = new string(
			new char[]
			{
				MASK_CHAR(PatternEditorMask.Note, 0, -1, mask, cursorPos),
				MASK_CHAR(PatternEditorMask.Note, 0, 1, mask, cursorPos),
				MASK_CHAR(PatternEditorMask.Instrument, 2, 3, mask, cursorPos),
				MASK_CHAR(PatternEditorMask.Volume, 4, 5, mask, cursorPos),
				MASK_CHAR(PatternEditorMask.Effect, 6, -1, mask, cursorPos),
				MASK_CHAR(PatternEditorMask.Effect, 7, 8, mask, cursorPos),
			});

		VGAMem.DrawText(buf, position, colours);
	}
}
