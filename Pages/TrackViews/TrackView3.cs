using ChasmTracker.Songs;
using ChasmTracker.Utility;
using ChasmTracker.VGA;

namespace ChasmTracker.Pages.TrackViews;

public class TrackView3 : TrackView
{
	public override int Width => 3;

	public override void DrawChannelHeader(int chan, Point position, byte fg)
	{
		VGAMem.DrawText($" {chan:d2}", position, (fg, 1));
	}

	public override void DrawNote(Point position, SongNote note, int cursorPos, VGAMemColours colours)
	{
		string buf;
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

		switch (cursorPos)
		{
			case 0:
				vfg = 0;
				colours = (0, 3);
				break;
			case 1:
				buf = note.NoteString;
				VGAMem.DrawText(buf, position, (6, colours.BG));
				VGAMem.DrawCharacter(buf[2], position.Advance(2), (0, 3));
				return;
			case 2:
			case 3:
				cursorPos -= 1;
				buf = " " + note.InstrumentString;
				VGAMem.DrawText(buf, position, (6, colours.BG));
				VGAMem.DrawCharacter(buf[cursorPos], position.Advance(cursorPos), (0, 3));
				return;
			case 4:
			case 5:
				cursorPos -= 3;
				buf = " " + note.VolumeString;
				VGAMem.DrawText(buf, position, (2, colours.BG));
				VGAMem.DrawCharacter(buf[cursorPos], position.Advance(cursorPos), (0, 3));
				return;
			case 6:
			case 7:
			case 8:
				cursorPos -= 6;
				buf = note.EffectString;
				VGAMem.DrawText(buf, position, (2, colours.BG));
				VGAMem.DrawCharacter(buf[cursorPos], position.Advance(cursorPos), (0, 3));
				return;
			case 9:
				buf = note.EffectString;
				VGAMem.DrawText(buf, position, (0, 3));
				return;
			default:
				/* bleh */
				colours.FG = 6;
				break;
		}

		if (note.HasNote)
			VGAMem.DrawText(note.NoteString, position, colours);
		else if (note.HasInstrument)
			VGAMem.DrawText(" " + note.InstrumentString, position, colours);
		else if (note.VolumeEffect != VolumeEffects.None)
			VGAMem.DrawText(" " + note.VolumeString, position, (vfg, colours.BG));
		else if ((note.Effect != 0) || (note.Parameter != 0))
		{
			if (cursorPos != 0)
				colours.FG = 2;

			VGAMem.DrawText(note.EffectString, position, colours);
		}
		else
			VGAMem.DrawText("\xAD\xAD\xAD", position, colours);
	}

	public override void DrawMask(Point position, PatternEditorMask mask, int cursorPos, VGAMemColours colours)
	{
		var buf = new char[] { '\x8F', '\x8F', '\x8F' };

		switch (cursorPos)
		{
			case 0: case 1:
				buf[0] = buf[1] = MASK_CHAR(PatternEditorMask.Note, 0, -1, mask, cursorPos);
				buf[2] = MASK_CHAR(PatternEditorMask.Note, 0, 1, mask, cursorPos);
				break;
			case 2: case 3:
				buf[1] = MASK_CHAR(PatternEditorMask.Instrument, 2, -1, mask, cursorPos);
				buf[2] = MASK_CHAR(PatternEditorMask.Instrument, 3, -1, mask, cursorPos);
				break;
			case 4: case 5:
				buf[1] = MASK_CHAR(PatternEditorMask.Volume, 4, -1, mask, cursorPos);
				buf[2] = MASK_CHAR(PatternEditorMask.Volume, 5, -1, mask, cursorPos);
				break;
			case 6: case 7: case 8:
				buf[0] = MASK_CHAR(PatternEditorMask.Effect, 6, -1, mask, cursorPos);
				buf[1] = MASK_CHAR(PatternEditorMask.Effect, 7, -1, mask, cursorPos);
				buf[2] = MASK_CHAR(PatternEditorMask.Effect, 8, -1, mask, cursorPos);
				break;
		}

		VGAMem.DrawText(new string(buf), position, colours);
	}
}
