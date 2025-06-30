namespace ChasmTracker.Pages.InfoWindows;

using ChasmTracker.Pages.TrackViews;
using ChasmTracker.Songs;
using ChasmTracker.Utility;
using ChasmTracker.VGA;

public class TrackView7Window : TrackViewWindowBase
{
	public override string ConfigurationID => "track10";

	public TrackView7Window(int windowType, Shared<int> selectedChannel, int height, int firstChannel)
		: base(windowType, selectedChannel, height, firstChannel, new TrackView7())
	{
	}

	protected override int ChannelWidth => 7;
	protected override bool Separator => false;

	protected override void DrawChannelHeader(int chan, int column, int @base, int fg)
	{
		VGAMem.DrawCharacter(0, new Point(column + 0, @base), (1, 1));
		VGAMem.DrawCharacter(0, new Point(column + 1, @base), (1, 1));
		VGAMem.DrawText(chan.ToString("d2"), new Point(column + 2, @base), (fg, 1));
		VGAMem.DrawCharacter(0, new Point(column + 4, @base), (1, 1));
		VGAMem.DrawCharacter(0, new Point(column + 5, @base), (1, 1));
	}
}
