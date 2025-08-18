namespace ChasmTracker.Pages.InfoWindows;

using ChasmTracker.Pages.TrackViews;
using ChasmTracker.Songs;
using ChasmTracker.Utility;
using ChasmTracker.VGA;

public class TrackView8Window : TrackViewWindowBase
{
	public override string ConfigurationID => "track8";

	public TrackView8Window(int windowType, Shared<int> selectedChannel, int height, int firstChannel)
		: base(windowType, selectedChannel, height, firstChannel, new TrackView8())
	{
	}

	protected override int ChannelWidth => 8;
	protected override bool Separator => true;

	protected override void DrawChannelHeader(int chan, int column, int @base, byte fg)
	{
		VGAMem.DrawCharacter(0, new Point(column + 1, @base), (1, 1));
		VGAMem.DrawCharacter(0, new Point(column + 2, @base), (1, 1));
		VGAMem.DrawText(chan.ToString("d2"), new Point(column + 3, @base), (fg, 1));
		VGAMem.DrawCharacter(0, new Point(column + 5, @base), (1, 1));
		VGAMem.DrawCharacter(0, new Point(column + 6, @base), (1, 1));
	}
}
