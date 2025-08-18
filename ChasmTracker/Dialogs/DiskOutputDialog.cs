using ChasmTracker.DiskOutput;
using ChasmTracker.Playback;
using ChasmTracker.Utility;
using ChasmTracker.VGA;

namespace ChasmTracker.Dialogs;

public class DiskOutputDialog : Dialog
{
	public DiskWriter? ExportDS;
	public int EstimatedLength;
	public byte ProgressColour; // prgh

	public DiskOutputDialog(DiskWriter? exportDS, int estimatedLength)
		: base(new Point(22, 25), new Size(36, 8))
	{
		ExportDS = exportDS;
		EstimatedLength = estimatedLength;
	}

	public override void DrawConst()
	{
		if ((ExportDS == null) || ExportDS.IsDisposed)
		{
			/* what are we doing here?! */
			DestroyAll();
			Log.Append(4, "disk export dialog was eaten by a grue!");
			return;
		}

		int sec = ExportDS.Length / AudioPlayback.MixFrequency;
		int pos = ExportDS.Length * 64 / EstimatedLength;

		VGAMem.DrawText($"Exporting song...{sec / 60:#####0}:{sec % 60:00}", new Point(27, 27), (0, 2));
		VGAMem.DrawFillCharacters(new Point(24, 30), new Point(55, 30), (VGAMem.DefaultForeground, 0));
		VGAMem.DrawVUMeter(new Point(24, 30), 32, pos, ProgressColour, ProgressColour); // ugh
		VGAMem.DrawBox(new Point(23, 29), new Point(56, 31), BoxTypes.Thin | BoxTypes.Inner | BoxTypes.Inset);
	}
}
