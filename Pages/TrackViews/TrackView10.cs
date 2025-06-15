using ChasmTracker.Songs;
using ChasmTracker.VGA;

namespace ChasmTracker.Pages.TrackViews;

public class TrackView10 : TrackView
{
	public override int Width => 10;

	public override void DrawChannelHeader(VGAMem vgaMem, int chan, Point position, int fg)
	{
		vgaMem.DrawText($"Channel {chan:d2}", position, fg, 1);
	}

	public override void DrawNote(VGAMem vgaMem, Point position, SongNote note, int cursorPos, int fg, int bg)
	{
		string noteBuf = note.NoteString;
		string instrumentBuf = note.InstrumentString;
		string volumeBuf = note.VolumeString;
		string effectBuf = note.EffectString;

		vgaMem.DrawText(noteBuf, position, 6, bg);
		vgaMem.DrawText(instrumentBuf, position.Advance(3), note.HasInstrument ? 10 : 2, bg);
		vgaMem.DrawText(volumeBuf, position.Advance(5), (note.VolumeEffect == VolumeEffects.Panning) ? 2 : 6, bg);
		vgaMem.DrawText(effectBuf, position.Advance(7), 2, bg);

		if (cursorPos < 0)
			return;

		if (cursorPos > 0)
			cursorPos++;

		if (cursorPos == 10)
			vgaMem.DrawText(note.EffectString, position.Advance(7), 0, 3);
		else
		{
			char c = '\0';

			switch (cursorPos)
			{
				case 0: c = noteBuf[0]; break;
				case 2: c = noteBuf[2]; break;
				case 3: c = instrumentBuf[0]; break;
				case 4: c = instrumentBuf[1]; break;
				case 5: c = volumeBuf[0]; break;
				case 6: c = volumeBuf[1]; break;
				default: /* 7->9 */
					c = effectBuf[cursorPos - 7];
					break;
			}

			vgaMem.DrawCharacter(c, position.Advance(cursorPos), 0, 3);
		}
	}

	public override void DrawMask(VGAMem vgaMem, Point position, PatternEditorMask mask, int cursorPos, int fg, int bg)
	{
		var buf = new string(
			new char[]
			{
				MASK_CHAR(PatternEditorMask.Note, 0, -1, mask, cursorPos),
				MASK_CHAR(PatternEditorMask.Note, 0, -1, mask, cursorPos),
				MASK_CHAR(PatternEditorMask.Note, 0, 1, mask, cursorPos),
				MASK_CHAR(PatternEditorMask.Instrument, 2, -1, mask, cursorPos),
				MASK_CHAR(PatternEditorMask.Instrument, 3, -1, mask, cursorPos),
				MASK_CHAR(PatternEditorMask.Volume, 4, -1, mask, cursorPos),
				MASK_CHAR(PatternEditorMask.Volume, 5, -1, mask, cursorPos),
				MASK_CHAR(PatternEditorMask.Effect, 6, -1, mask, cursorPos),
				MASK_CHAR(PatternEditorMask.Effect, 7, -1, mask, cursorPos),
				MASK_CHAR(PatternEditorMask.Effect, 8, -1, mask, cursorPos),
			});

		vgaMem.DrawText(buf, position, fg, bg);
	}
}
