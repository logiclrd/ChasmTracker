namespace ChasmTracker.Pages.InfoWindows;

using ChasmTracker.Pages.TrackViews;
using ChasmTracker.Songs;
using ChasmTracker.Utility;
using ChasmTracker.VGA;

public class TrackView2Window : TrackViewWindowBase
{
	public override string ConfigurationID => "track36";

	public TrackView2Window(int windowType, Shared<int> selectedChannel, int height, int firstChannel)
		: base(windowType, selectedChannel, height, firstChannel, new TrackView2())
	{
	}

	protected override int ChannelWidth => 2;
	protected override bool Separator => false;

	protected override void DrawChannelHeader(int chan, int column, int @base, byte fg)
	{
		VGAMem.DrawText(chan.ToString("d2"), new Point(column, @base), (fg, 1));
	}
}
