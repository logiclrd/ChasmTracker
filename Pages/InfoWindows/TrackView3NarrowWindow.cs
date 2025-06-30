namespace ChasmTracker.Pages.InfoWindows;

using ChasmTracker.Pages.TrackViews;
using ChasmTracker.Songs;
using ChasmTracker.Utility;
using ChasmTracker.VGA;

public class TrackView3NarrowWindow : TrackViewWindowBase
{
	public override string ConfigurationID => "track24";

	public TrackView3NarrowWindow(int windowType, Shared<int> selectedChannel, int height, int firstChannel)
		: base(windowType, selectedChannel, height, firstChannel, new TrackView3())
	{
	}

	protected override int ChannelWidth => 3;
	protected override bool Separator => false;

	protected override void DrawChannelHeader(int chan, int column, int @base, int fg)
	{
		VGAMem.DrawText(chan.ToString("d2"), new Point(column + 1, @base), (fg, 1));
	}
}
