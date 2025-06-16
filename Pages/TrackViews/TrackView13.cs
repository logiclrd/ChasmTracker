using ChasmTracker.Songs;
using ChasmTracker.VGA;

namespace ChasmTracker.Pages.TrackViews;

public class TrackView13 : TrackView
{
	public override int Width => 13;

	public override void DrawChannelHeader(VGAMem vgaMem, int chan, Point position, int fg)
	{
		vgaMem.DrawText($" Channel {chan:d2} ", position, fg, 1);
	}

	readonly int[] CursorPosMap = { 0, 2, 4, 5, 7, 8, 10, 11, 12 };

	public override void DrawNote(VGAMem vgaMem, Point position, SongNote note, int cursorPos, int fg, int bg)
	{
		string noteText = $"{note.NoteString} {note.InstrumentString} {note.VolumeString} {note.EffectString}";

		if (Status.ShowDefaultVolumes && (note.VolumeEffect == VolumeEffects.None)
		 && (note.Instrument > 0) && note.NoteIsNote && (Song.CurrentSong != null))
		{
			var smp = Song.CurrentSong.IsInstrumentMode
				? Song.CurrentSong.GetInstrument(note.Instrument)?.TranslateKeyboard(note.Note)
				: Song.CurrentSong.GetSample(note.Instrument);

			if (smp != null)
			{
				/* Modplug-specific hack: volume bit shift */
				int n = smp.Volume >> 2;

				char[] newTail =
					{
						'\xBF',
						(char)('0' + (n / 10) % 10),
						(char)('0' + n % 10),
						'\xC0'
					};

				noteText = noteText.Substring(0, 6) + new string(newTail);
			}
		}

		vgaMem.DrawText(noteText, position, fg, bg);

		/* lazy coding here: the panning is written twice, or if the
		* cursor's on it, *three* times. */
		if (note.VolumeEffect == VolumeEffects.Panning)
			vgaMem.DrawText(note.VolumeString, position.Advance(7), 2, bg);

		if (cursorPos == 9)
			vgaMem.DrawText(noteText.Substring(10), position.Advance(10), 0, 3);
		else if (cursorPos >= 0)
		{
			cursorPos = CursorPosMap[cursorPos];
			vgaMem.DrawCharacter(noteText[cursorPos], position.Advance(cursorPos), 0, 3);
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
				'\x8F',
				MASK_CHAR(PatternEditorMask.Instrument, 2, -1, mask, cursorPos),
				MASK_CHAR(PatternEditorMask.Instrument, 3, -1, mask, cursorPos),
				'\x8F',
				MASK_CHAR(PatternEditorMask.Volume, 4, -1, mask, cursorPos),
				MASK_CHAR(PatternEditorMask.Volume, 5, -1, mask, cursorPos),
				'\x8F',
				MASK_CHAR(PatternEditorMask.Effect, 6, -1, mask, cursorPos),
				MASK_CHAR(PatternEditorMask.Effect, 7, -1, mask, cursorPos),
				MASK_CHAR(PatternEditorMask.Effect, 8, -1, mask, cursorPos),
			});

		vgaMem.DrawText(buf, position, fg, bg);
	}
}
