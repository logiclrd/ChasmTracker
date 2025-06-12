using ChasmTracker.Songs;
using ChasmTracker.VGA;

namespace ChasmTracker.Pages.TrackViews;

public class TrackView2 : TrackView
{
	public override int Width => 2;

	public override void DrawChannelHeader(VGAMem vgaMem, int chan, Point position, int fg)
	{
		vgaMem.DrawText(chan.ToString("d2"), position, fg, 1);
	}

	void DrawEffect(VGAMem vgaMem, Point position, SongNote note, int cursorPos, int bg)
	{
		int fg = 2, fg1 = 10, fg2 = 10, bg1 = bg, bg2 = bg;

		switch (cursorPos)
		{
			case 0:
				fg = fg1 = fg2 = 0;
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
			case 9:
				fg = fg1 = fg2 = 0;
				bg = bg1 = bg2 = 3;
				break;
		}

		vgaMem.DrawCharacter(note.EffectChar, position, fg, bg);
		vgaMem.DrawHalfWidthCharacters(HexDigit(note.Parameter >> 4),
			HexDigit(note.Parameter & 0xF),
			position.Advance(1), fg1, bg1, fg2, bg2);
	}

	public override void DrawNote(VGAMem vgaMem, Point position, SongNote note, int cursorPos, int fg, int bg)
	{
		int vfg = 6;

		switch (note.VolumeEffect)
		{
			case VolumeEffects.Volume:
				vfg = 2;
				break;
			case VolumeEffects.Panning:
			case VolumeEffects.None:
				vfg = 1;
				break;
		}

		string buf = note.NoteString;

		switch (cursorPos)
		{
			case 0:
				vfg = fg = 0;
				bg = 3;
				/* FIXME Is this supposed to fallthrough here? */
				goto case 1;
			case 1: /* Mini-accidentals on 2-col. view */
				vgaMem.DrawCharacter(buf[0], position, fg, bg);
				// XXX cut-and-paste hackjob programming... this code should only exist in one place
				switch (buf[0])
				{
					case '^':
					case '~':
					case '\xCD': // note off
					case '\xAD': // dot (empty)
						if (cursorPos == 1)
							vgaMem.DrawCharacter(buf[1], position.Advance(1), 0, 3);
						else
							vgaMem.DrawCharacter(buf[1], position.Advance(1), fg, bg);
						break;
					default:
						vgaMem.DrawHalfWidthCharacters(buf[1], buf[2], position.Advance(1),
							fg, bg, cursorPos == 1 ? 0 : fg, cursorPos == 1 ? 3 : bg);
						break;
				}
				return;
				/*
				buf = note.NoteStringShort;
				vgaMem.DrawCharacter(buf[0], position, 6, bg);
				vgaMem.DrawCharacter(buf[1], position.Advance(1), 0, 3);
				return;
				*/
			case 2:
			case 3:
				cursorPos -= 2;

				buf = note.HasInstrument ? note.NoteString : "\xAD\xAD";
				vgaMem.DrawText(buf, position, 6, bg);
				vgaMem.DrawCharacter(buf[cursorPos], position.Advance(cursorPos), 0, 3);
				return;
			case 4:
			case 5:
				cursorPos -= 4;
				buf = note.VolumeString;
				vgaMem.DrawText(buf, position, vfg, bg);
				vgaMem.DrawCharacter(buf[cursorPos], position.Advance(cursorPos), 0, 3);
				return;
			case 6:
			case 7:
			case 8:
			case 9:
				DrawEffect(vgaMem, position, note, cursorPos, bg);
				return;
			default:
				/* bleh */
				fg = 6;
				break;
		}

		if (note.HasNote)
		{
			vgaMem.DrawCharacter(buf[0], position, 6, bg);

			switch (buf[0])
			{
				case '^':
				case '~':
				case '\xCD': // note off
				case '\xAD': // dot (empty)
					if (cursorPos == 1)
						vgaMem.DrawCharacter(buf[1], position.Advance(1), 0, 3);
					else
						vgaMem.DrawCharacter(buf[1], position.Advance(1), fg, bg);
					break;
				default:
					vgaMem.DrawHalfWidthCharacters(buf[1], buf[2], position.Advance(1),
						fg, bg, cursorPos == 1 ? 0 : fg, cursorPos == 1 ? 3 : bg);
					break;
			}
			/*
			vgaMem.DrawText(note.NoteString, position, fg, bg);
			*/
		}
		else if (note.HasInstrument)
			vgaMem.DrawText(note.InstrumentString, position, fg, bg);
		else if (note.VolumeEffect != VolumeEffects.None)
			vgaMem.DrawText(note.VolumeString, position, vfg, bg);
		else if ((note.Effect != 0) || (note.Parameter != 0))
			DrawEffect(vgaMem, position, note, cursorPos, bg);
		else
		{
			vgaMem.DrawCharacter('\xAD', position, fg, bg);
			vgaMem.DrawCharacter('\xAD', position.Advance(1), fg, bg);
		}
	}

	public override void DrawMask(VGAMem vgaMem, Point position, MaskFields mask, int cursorPos, int fg, int bg)
	{
		var buf = new char[] { '\x8F', '\x8F' };

		switch (cursorPos)
		{
			case 0: case 1:
				buf[0] = MASK_CHAR(MaskFields.Note, 0, -1, mask, cursorPos);
				buf[1] = MASK_CHAR(MaskFields.Note, 0, 1, mask, cursorPos);
				break;
			case 2: case 3:
				buf[0] = MASK_CHAR(MaskFields.Instrument, 2, -1, mask, cursorPos);
				buf[1] = MASK_CHAR(MaskFields.Instrument, 3, -1, mask, cursorPos);
				break;
			case 4: case 5:
				buf[0] = MASK_CHAR(MaskFields.Volume, 4, -1, mask, cursorPos);
				buf[1] = MASK_CHAR(MaskFields.Volume, 5, -1, mask, cursorPos);
				break;
			case 6: case 7: case 8:
				buf[0] = MASK_CHAR(MaskFields.Effect, 6, -1, mask, cursorPos);
				buf[1] = MASK_CHAR(MaskFields.Effect, 7, 8, mask, cursorPos);
				break;
		}

		vgaMem.DrawText(new string(buf), position, fg, bg);
	}
}
