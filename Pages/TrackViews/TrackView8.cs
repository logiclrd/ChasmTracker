using ChasmTracker.Songs;
using ChasmTracker.VGA;

namespace ChasmTracker.Pages.TrackViews;

public class TrackView8 : TrackView
{
	public override int Width => 8;

	public override void DrawChannelHeader(int chan, Point position, byte fg)
	{
		VGAMem.DrawText($"  {chan:d2}  ", position, (fg, 1));
	}

	public override void DrawNote(Point position, SongNote note, int cursorPos, VGAMemColours colours)
	{
		string noteBuf = note.NoteString;

		VGAMem.DrawText(noteBuf, position, colours);

		if ((note.VolumeParameter != 0) || (note.VolumeEffect != 0))
		{
			string volumeBuf = note.VolumeString;

			if (note.VolumeEffect == VolumeEffects.Panning)
				VGAMem.DrawText(volumeBuf, position.Advance(3), (1, colours.BG));
			else
				VGAMem.DrawText(volumeBuf, position.Advance(3), (2, colours.BG));
		}
		else
		{
			VGAMem.DrawCharacter('\0', position.Advance(3), colours);
			VGAMem.DrawCharacter('\0', position.Advance(4), colours);
		}

		string effectBuf = note.EffectString;

		VGAMem.DrawText(effectBuf, position.Advance(5), colours);
	}

	public override void DrawMask(Point position, PatternEditorMask mask, int cursorPos, VGAMemColours colours)
	{
		// Unused. The 8-column track view is only used on the Info page.
	}
}
