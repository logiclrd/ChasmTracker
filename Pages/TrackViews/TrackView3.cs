using ChasmTracker.Songs;
using ChasmTracker.VGA;

namespace ChasmTracker.Pages.TrackViews;

public class TrackView3 : TrackView
{
	public override int Width => 3;

	public override void DrawChannelHeader(VGAMem vgaMem, int chan, Point position, int fg)
	{
		vgaMem.DrawText($" {chan:d2}", position, fg, 1);
	}

	public override void DrawNote(VGAMem vgaMem, Point position, SongNote note, int cursorPos, int fg, int bg)
	{
		string buf;
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

		switch (cursorPos)
		{
			case 0:
				vfg = fg = 0;
				bg = 3;
				break;
			case 1:
				buf = note.NoteString;
				vgaMem.DrawText(buf, position, 6, bg);
				vgaMem.DrawCharacter(buf[2], position.Advance(2), 0, 3);
				return;
			case 2:
			case 3:
				cursorPos -= 1;
				buf = " " + note.InstrumentString;
				vgaMem.DrawText(buf, position, 6, bg);
				vgaMem.DrawCharacter(buf[cursorPos], position.Advance(cursorPos), 0, 3);
				return;
			case 4:
			case 5:
				cursorPos -= 3;
				buf = " " + note.VolumeString;
				vgaMem.DrawText(buf, position, 2, bg);
				vgaMem.DrawCharacter(buf[cursorPos], position.Advance(cursorPos), 0, 3);
				return;
			case 6:
			case 7:
			case 8:
				cursorPos -= 6;
				buf = note.EffectString;
				vgaMem.DrawText(buf, position, 2, bg);
				vgaMem.DrawCharacter(buf[cursorPos], position.Advance(cursorPos), 0, 3);
				return;
			case 9:
				buf = note.EffectString;
				vgaMem.DrawText(buf, position, 0, 3);
				return;
			default:
				/* bleh */
				fg = 6;
				break;
		}

		if (note.HasNote)
			vgaMem.DrawText(note.NoteString, position, fg, bg);
		else if (note.HasInstrument)
			vgaMem.DrawText(" " + note.InstrumentString, position, fg, bg);
		else if (note.VolumeEffect != VolumeEffects.None)
			vgaMem.DrawText(" " + note.VolumeString, position, vfg, bg);
		else if ((note.Effect != 0) || (note.Parameter != 0))
		{
			if (cursorPos != 0)
				fg = 2;

			vgaMem.DrawText(note.EffectString, position, fg, bg);
		}
		else
			vgaMem.DrawText("\xAD\xAD\xAD", position, fg, bg);
	}

	public override void DrawMask(VGAMem vgaMem, Point position, MaskFields mask, int cursorPos, int fg, int bg)
	{
		var buf = new char[] { '\x8F', '\x8F', '\x8F' };

		switch (cursorPos)
		{
			case 0: case 1:
				buf[0] = buf[1] = MASK_CHAR(MaskFields.Note, 0, -1, mask, cursorPos);
				buf[2] = MASK_CHAR(MaskFields.Note, 0, 1, mask, cursorPos);
				break;
			case 2: case 3:
				buf[1] = MASK_CHAR(MaskFields.Instrument, 2, -1, mask, cursorPos);
				buf[2] = MASK_CHAR(MaskFields.Instrument, 3, -1, mask, cursorPos);
				break;
			case 4: case 5:
				buf[1] = MASK_CHAR(MaskFields.Volume, 4, -1, mask, cursorPos);
				buf[2] = MASK_CHAR(MaskFields.Volume, 5, -1, mask, cursorPos);
				break;
			case 6: case 7: case 8:
				buf[0] = MASK_CHAR(MaskFields.Effect, 6, -1, mask, cursorPos);
				buf[1] = MASK_CHAR(MaskFields.Effect, 7, -1, mask, cursorPos);
				buf[2] = MASK_CHAR(MaskFields.Effect, 8, -1, mask, cursorPos);
				break;
		}

		vgaMem.DrawText(new string(buf), position, fg, bg);
	}
}
