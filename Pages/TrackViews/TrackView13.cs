namespace ChasmTracker.Pages.TrackViews;

using ChasmTracker.Songs;
using ChasmTracker.Utility;
using ChasmTracker.VGA;

public class TrackView13 : TrackView
{
	public override int Width => 13;

	public override void DrawChannelHeader(int chan, Point position, byte fg)
	{
		VGAMem.DrawText($" Channel {chan:d2} ", position, (fg, 1));
	}

	readonly int[] CursorPosMap = { 0, 2, 4, 5, 7, 8, 10, 11, 12 };

	public override void DrawNote(Point position, SongNote note, int cursorPos, VGAMemColours colours)
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

		VGAMem.DrawText(noteText, position, colours);

		/* lazy coding here: the panning is written twice, or if the
		* cursor's on it, *three* times. */
		if (note.VolumeEffect == VolumeEffects.Panning)
			VGAMem.DrawText(note.VolumeString, position.Advance(7), (2, colours.BG));

		if (cursorPos == 9)
			VGAMem.DrawText(noteText.Substring(10), position.Advance(10), (0, 3));
		else if (cursorPos >= 0)
		{
			cursorPos = CursorPosMap[cursorPos];
			VGAMem.DrawCharacter(noteText[cursorPos], position.Advance(cursorPos), (0, 3));
		}
	}

	public override void DrawMask(Point position, PatternEditorMask mask, int cursorPos, VGAMemColours colours)
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

		VGAMem.DrawText(buf, position, colours);
	}
}
