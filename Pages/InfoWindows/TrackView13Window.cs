namespace ChasmTracker.Pages.InfoWindows;

using ChasmTracker.Pages.TrackViews;
using ChasmTracker.Utility;

public class TrackView13Window : TrackViewWindowBase
{
	public override string ConfigurationID => "track5";

	public TrackView13Window(int windowType, Shared<int> selectedChannel, int height, int firstChannel)
		: base(windowType, selectedChannel, height, firstChannel, new TrackView13())
	{
	}

	protected override int ChannelWidth => 13;
	protected override bool Separator => true;

	protected override void DrawChannelHeader(int chan, int column, int @base, byte fg)
	{
		TrackView.DrawChannelHeader(chan, new Point(column, @base), fg);
	}
}
