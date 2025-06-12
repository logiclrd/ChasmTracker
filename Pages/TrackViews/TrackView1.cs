using System.IO;
using ChasmTracker.Songs;
using ChasmTracker.VGA;

namespace ChasmTracker.Pages.TrackViews;

public class TrackView1 : TrackView
{
	public override int Width => 1;

	public override void DrawChannelHeader(VGAMem vgaMem, int chan, Point position, int fg)
	{
		char ch1 = unchecked((char)('0' + chan / 10));
		char ch2 = unchecked((char)('0' + chan % 10));

		vgaMem.DrawHalfWidthCharacters(ch1, ch2, position, fg, 1, fg, 1);
	}

	void DrawEffect(VGAMem vgaMem, Point position, SongNote note, int cursorPos, int fg, int bg)
	{
		int fg1 = fg, fg2 = fg, bg1 = bg, bg2 = bg;

		switch (cursorPos)
		{
			case 0:
				break;
			case 6:
				fg = 0;
				bg = 3;
				break;
			case 7:
				fg1 = 0;
				bg1 = 3;
				break;
			case 8:
				fg2 = 0;
				bg2 = 3;
				break;
			default:
				fg = 2;
				break;
		}

		if ((cursorPos == 7) || (cursorPos == 8) || ((note.Effect == 0) && (note.Parameter != 0)))
			vgaMem.DrawHalfWidthCharacters(HexDigit(note.Parameter >> 4),
				HexDigit(note.Parameter & 0xF),
				position.Advance(1), fg1, bg1, fg2, bg2);
		else
			vgaMem.DrawCharacter(note.EffectChar, position, fg, bg);
	}

	public override void DrawNote(VGAMem vgaMem, Point position, SongNote note, int cursorPos, int fg, int bg)
	{
		string buf;

		switch (cursorPos)
		{
			case 0:
				fg = 0;
				bg = 3;
				if (note.NoteIsNote)
				{
					buf = note.NoteStringShort;
					vgaMem.DrawHalfWidthCharacters(buf[0], buf[1], position, fg, bg, fg, bg);
					return;
				}
				break;
			case 1:
				buf = note.NoteStringShort;
				vgaMem.DrawHalfWidthCharacters(buf[0], buf[1], position, fg, bg, 0, 3);
				return;
			case 2:
			case 3:
				cursorPos -= 2;

				buf = note.HasInstrument ? note.NoteString : "\xAD\xAD";

				if (cursorPos == 0)
					vgaMem.DrawHalfWidthCharacters(buf[0], buf[1], position, 0, 3, fg, bg);
				else
					vgaMem.DrawHalfWidthCharacters(buf[0], buf[1], position, fg, bg, 0, 3);
				return;
			case 4:
			case 5:
				cursorPos -= 4;

				buf = note.VolumeString;

				fg = (note.VolumeEffect == VolumeEffects.Panning) ? 1 : 2;

				if (cursorPos == 0)
					vgaMem.DrawHalfWidthCharacters(buf[0], buf[1], position, 0, 3, fg, bg);
				else
					vgaMem.DrawHalfWidthCharacters(buf[0], buf[1], position, fg, bg, 0, 3);
				return;
			case 9:
				cursorPos = 6;
				goto case 6;
			// fall through
			case 6:
			case 7:
			case 8:
				DrawEffect(vgaMem, position, note, cursorPos, fg, bg);
				return;
		}

		if (note.HasNote)
			vgaMem.DrawCharacter(note.NoteStringShort[0], position, fg, bg);
		else if (note.HasInstrument)
		{
			buf = note.InstrumentString;
			vgaMem.DrawHalfWidthCharacters(buf[0], buf[1], position, fg, bg, fg, bg);
		}
		else if (note.VolumeEffect != VolumeEffects.None)
		{
			if (cursorPos != 0)
				fg = (note.VolumeEffect == VolumeEffects.Panning) ? 1 : 2;

			buf = note.VolumeString;

			vgaMem.DrawHalfWidthCharacters(buf[0], buf[1], position, fg, bg, fg, bg);
		}
		else if ((note.Effect != 0) || (note.Parameter != 0))
			DrawEffect(vgaMem, position, note, cursorPos, fg, bg);
		else
			vgaMem.DrawCharacter('\xAD', position, fg, bg);
	}

	public override void DrawMask(VGAMem vgaMem, Point position, MaskFields mask, int cursorPos, int fg, int bg)
	{
		char c = '\x8F';

		switch (cursorPos)
		{
			case 0: case 1:
				c = MASK_CHAR(MaskFields.Note, 0, 1, mask, cursorPos);
				break;
			case 2: case 3:
				c = MASK_CHAR(MaskFields.Instrument, 2, 3, mask, cursorPos);
				break;
			case 4: case 5:
				c = MASK_CHAR(MaskFields.Volume, 4, 5, mask, cursorPos);
				break;
			case 6:
				c = MASK_CHAR(MaskFields.Effect, 6, -1, mask, cursorPos);
				break;
			case 7: case 8:
				c = MASK_CHAR(MaskFields.Effect, 7, 8, mask, cursorPos);
				break;
		}

		vgaMem.DrawCharacter(c, position, fg, bg);
	}
}
