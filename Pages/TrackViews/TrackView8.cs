using ChasmTracker.Songs;
using ChasmTracker.VGA;

namespace ChasmTracker.Pages.TrackViews;

public class TrackView8 : TrackView
{
	public override int Width => 8;

	public override void DrawChannelHeader(VGAMem vgaMem, int chan, Point position, int fg)
	{
		vgaMem.DrawText($"  {chan:d2}  ", position, fg, 1);
	}

	public override void DrawNote(VGAMem vgaMem, Point position, SongNote note, int cursorPos, int fg, int bg)
	{
		string noteBuf = note.NoteString;

		vgaMem.DrawText(noteBuf, position, fg, bg);

		if ((note.VolumeParameter != 0) || (note.VolumeEffect != 0))
		{
			string volumeBuf = note.VolumeString;

			vgaMem.DrawText(volumeBuf, position.Advance(3), (note.VolumeEffect == VolumeEffects.Panning) ? 1 : 2, bg);
		}
		else
		{
			vgaMem.DrawCharacter('\0', position.Advance(3), fg, bg);
			vgaMem.DrawCharacter('\0', position.Advance(4), fg, bg);
		}

		string effectBuf = note.EffectString;

		vgaMem.DrawText(effectBuf, position.Advance(5), fg, bg);
	}

	public override void DrawMask(VGAMem vgaMem, Point position, PatternEditorMask mask, int cursorPos, int fg, int bg)
	{
		// Unused. The 8-column track view is only used on the Info page.
	}
}
