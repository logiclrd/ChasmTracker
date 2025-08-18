namespace ChasmTracker.Pages.TrackViews;

using ChasmTracker.Songs;
using ChasmTracker.Utility;
using ChasmTracker.VGA;

public class TrackView2 : TrackView
{
	public override int Width => 2;

	public override void DrawChannelHeader(int chan, Point position, byte fg)
	{
		VGAMem.DrawText(chan.ToString("d2"), position, (fg, 1));
	}

	void DrawEffect(Point position, ref SongNote note, int cursorPos, byte bg)
	{
		VGAMemColours colours = (2, bg);
		VGAMemColours colours1 = (10, bg);
		VGAMemColours colours2 = (10, bg);

		switch (cursorPos)
		{
			case 0:
				colours.FG = colours1.FG = colours2.FG = 0;
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
			case 9:
				colours = (0, 3);
				colours1 = (0, 3);
				colours2 = (0, 3);
				break;
		}

		VGAMem.DrawCharacter(note.EffectChar, position, colours);
		VGAMem.DrawHalfWidthCharacters(HexDigit(note.Parameter >> 4),
			HexDigit(note.Parameter & 0xF),
			position.Advance(1), colours1, colours2);
	}

	public override void DrawNote(Point position, ref SongNote note, int cursorPos, VGAMemColours colours)
	{
		byte vfg = 6;

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
				vfg = 0;
				colours = (0, 3);
				/* FIXME Is this supposed to fallthrough here? */
				goto case 1;
			case 1: /* Mini-accidentals on 2-col. view */
				VGAMem.DrawCharacter(buf[0], position, colours);
				// XXX cut-and-paste hackjob programming... this code should only exist in one place
				switch (buf[0])
				{
					case '^':
					case '~':
					case '\xCD': // note off
					case '\xAD': // dot (empty)
						if (cursorPos == 1)
							VGAMem.DrawCharacter(buf[1], position.Advance(1), (0, 3));
						else
							VGAMem.DrawCharacter(buf[1], position.Advance(1), colours);
						break;
					default:
						VGAMem.DrawHalfWidthCharacters(buf[1], buf[2], position.Advance(1),
							colours, cursorPos == 1 ? (0, 3) : colours);
						break;
				}
				return;
				/*
				buf = note.NoteStringShort;
				VGAMem.DrawCharacter(buf[0], position, (6, bg));
				VGAMem.DrawCharacter(buf[1], position.Advance(1), (0, 3));
				return;
				*/
			case 2:
			case 3:
				cursorPos -= 2;

				buf = note.HasInstrument ? note.NoteString : "\xAD\xAD";
				VGAMem.DrawText(buf, position, (6, colours.BG));
				VGAMem.DrawCharacter(buf[cursorPos], position.Advance(cursorPos), (0, 3));
				return;
			case 4:
			case 5:
				cursorPos -= 4;
				buf = note.VolumeString;
				VGAMem.DrawText(buf, position, (vfg, colours.BG));
				VGAMem.DrawCharacter(buf[cursorPos], position.Advance(cursorPos), (0, 3));
				return;
			case 6:
			case 7:
			case 8:
			case 9:
				DrawEffect(position, ref note, cursorPos, colours.BG);
				return;
			default:
				/* bleh */
				colours.FG = 6;
				break;
		}

		if (note.HasNote)
		{
			VGAMem.DrawCharacter(buf[0], position, (6, colours.BG));

			switch (buf[0])
			{
				case '^':
				case '~':
				case '\xCD': // note off
				case '\xAD': // dot (empty)
					if (cursorPos == 1)
						VGAMem.DrawCharacter(buf[1], position.Advance(1), (0, 3));
					else
						VGAMem.DrawCharacter(buf[1], position.Advance(1), colours);
					break;
				default:
					VGAMem.DrawHalfWidthCharacters(buf[1], buf[2], position.Advance(1),
						colours, cursorPos == 1 ? (0, 3) : colours);
					break;
			}
			/*
			VGAMem.DrawText(note.NoteString, position, fg, bg);
			*/
		}
		else if (note.HasInstrument)
			VGAMem.DrawText(note.InstrumentString, position, colours);
		else if (note.VolumeEffect != VolumeEffects.None)
			VGAMem.DrawText(note.VolumeString, position, (vfg, colours.BG));
		else if ((note.Effect != 0) || (note.Parameter != 0))
			DrawEffect(position, ref note, cursorPos, colours.BG);
		else
		{
			VGAMem.DrawCharacter('\xAD', position, colours);
			VGAMem.DrawCharacter('\xAD', position.Advance(1), colours);
		}
	}

	public override void DrawMask(Point position, PatternEditorMask mask, int cursorPos, VGAMemColours colours)
	{
		var buf = new char[] { '\x8F', '\x8F' };

		switch (cursorPos)
		{
			case 0: case 1:
				buf[0] = MASK_CHAR(PatternEditorMask.Note, 0, -1, mask, cursorPos);
				buf[1] = MASK_CHAR(PatternEditorMask.Note, 0, 1, mask, cursorPos);
				break;
			case 2: case 3:
				buf[0] = MASK_CHAR(PatternEditorMask.Instrument, 2, -1, mask, cursorPos);
				buf[1] = MASK_CHAR(PatternEditorMask.Instrument, 3, -1, mask, cursorPos);
				break;
			case 4: case 5:
				buf[0] = MASK_CHAR(PatternEditorMask.Volume, 4, -1, mask, cursorPos);
				buf[1] = MASK_CHAR(PatternEditorMask.Volume, 5, -1, mask, cursorPos);
				break;
			case 6: case 7: case 8:
				buf[0] = MASK_CHAR(PatternEditorMask.Effect, 6, -1, mask, cursorPos);
				buf[1] = MASK_CHAR(PatternEditorMask.Effect, 7, 8, mask, cursorPos);
				break;
		}

		VGAMem.DrawText(new string(buf), position, colours);
	}
}
