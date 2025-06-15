using ChasmTracker.Songs;
using ChasmTracker.VGA;

namespace ChasmTracker.Pages.TrackViews;

public class TrackView7 : TrackView
{
	public override int Width => 7;

	public override void DrawChannelHeader(VGAMem vgaMem, int chan, Point position, int fg)
	{
		vgaMem.DrawText($"Chnl {chan:d2}", position, fg, 1);
	}

	public override void DrawNote(VGAMem vgaMem, Point position, SongNote note, int cursorPos, int fg, int bg)
	{
		string noteBuf = note.NoteString;
		string instrumentBuf = note.HasInstrument ? note.InstrumentString : "\xAD\xAD";
		string volumeBuf = note.VolumeString;

		vgaMem.DrawText(noteBuf, position, 6, bg);

		int fg1, bg1, fg2, bg2;

		fg1 = fg2 = (note.HasInstrument ? 10 : 2);
		bg1 = bg2 = bg;

		switch (cursorPos)
		{
			case 0:
				vgaMem.DrawCharacter(noteBuf[0], position, 0, 3);
				break;
			case 1:
				vgaMem.DrawCharacter(noteBuf[2], position.Advance(2), 0, 3);
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

		vgaMem.DrawHalfWidthCharacters(instrumentBuf[0], instrumentBuf[1], position.Advance(3), fg1, bg1, fg2, bg2);

		/* volume */
		switch (note.VolumeEffect)
		{
			case VolumeEffects.None:
				fg1 = 6;
				break;
			case VolumeEffects.Panning:
				fg1 = 10;
				break;
			case VolumeEffects.TonePortamento:
			case VolumeEffects.VibratoSpeed:
			case VolumeEffects.VibratoDepth:
				fg1 = 6;
				break;
			default:
				fg1 = 12;
				break;
		}

		fg2 = fg1;
		bg1 = bg2 = bg;

		switch (cursorPos)
		{
			case 4:
				fg1 = 0;
				bg1 = 3;
				break;
			case 5:
				fg2 = 0;
				bg2 = 3;
				break;
		}

		vgaMem.DrawHalfWidthCharacters(volumeBuf[0], volumeBuf[1], position.Advance(4), fg1, bg1, fg2, bg2);

		/* effect value */
		fg1 = fg2 = 10;
		bg1 = bg2 = bg;

		switch (cursorPos)
		{
			case 7:
				fg1 = 0;
				bg1 = 3;
				break;
			case 8:
				fg2 = 0;
				bg2 = 3;
				break;
			case 9:
				fg1 = fg2 = 0;
				bg1 = bg2 = 3;
				cursorPos = 6; // hack
				break;
		}

		vgaMem.DrawHalfWidthCharacters(
			HexDigit(note.Parameter >> 4),
			HexDigit(note.Parameter & 0xF),
			position.Advance(6), fg1, bg1, fg2, bg2);

		/* effect */
		vgaMem.DrawCharacter(note.EffectChar, position.Advance(5),
			(cursorPos == 6) ? 0 : 2, (cursorPos == 6) ? 3 : bg);
	}

	public override void DrawMask(VGAMem vgaMem, Point position, PatternEditorMask mask, int cursorPos, int fg, int bg)
	{
		var buf = new string(
			new char[]
			{
				MASK_CHAR(PatternEditorMask.Note, 0, -1, mask, cursorPos),
				MASK_CHAR(PatternEditorMask.Note, 0, -1, mask, cursorPos),
				MASK_CHAR(PatternEditorMask.Note, 0, 1, mask, cursorPos),
				MASK_CHAR(PatternEditorMask.Instrument, 2, 3, mask, cursorPos),
				MASK_CHAR(PatternEditorMask.Volume, 4, 5, mask, cursorPos),
				MASK_CHAR(PatternEditorMask.Effect, 6, -1, mask, cursorPos),
				MASK_CHAR(PatternEditorMask.Effect, 7, 8, mask, cursorPos),
			});

		vgaMem.DrawText(buf, position, fg, bg);
	}
}
