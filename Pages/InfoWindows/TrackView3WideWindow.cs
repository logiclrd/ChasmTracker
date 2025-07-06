namespace ChasmTracker.Pages.InfoWindows;

using ChasmTracker.Pages.TrackViews;
using ChasmTracker.Songs;
using ChasmTracker.Utility;
using ChasmTracker.VGA;

public class TrackView3WideWindow : TrackViewWindowBase
{
	public override string ConfigurationID => "track18";

	public TrackView3WideWindow(int windowType, Shared<int> selectedChannel, int height, int firstChannel)
		: base(windowType, selectedChannel, height, firstChannel, new TrackView3())
	{
	}

	protected override int ChannelWidth => 3;
	protected override bool Separator => true;

	protected override void DrawChannelHeader(int chan, int column, int @base, byte fg)
	{
		VGAMem.DrawText(chan.ToString("d2"), new Point(column + 1, @base), (fg, 1));
	}
}
