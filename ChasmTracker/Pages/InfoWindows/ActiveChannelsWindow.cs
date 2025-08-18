using ChasmTracker.Playback;
using ChasmTracker.Songs;
using ChasmTracker.Utility;
using ChasmTracker.VGA;

namespace ChasmTracker.Pages.InfoWindows;

public class ActiveChannelsWindow : InfoWindow
{
	public override string ConfigurationID => "global";

	public ActiveChannelsWindow(int windowType, Shared<int> selectedChannel, int height, int firstChannel)
		: base(windowType, selectedChannel, height, firstChannel)
	{
	}

	public override int GetNumChannels() => 0;

	public override bool UsesFirstRow => true;

	public override void Draw(int @base, int height, bool isActive)
	{
		int fg = (isActive ? 3 : 0);

		VGAMem.DrawText($"Active Channels: {AudioPlayback.PlayingChannels} ({AudioPlayback.MaxChannelsUsed})", new Point(2, @base), (fg, 2));
		VGAMem.DrawText($"Global Volume: {Song.CurrentSong.CurrentGlobalVolume}", new Point(4, @base + 1), (fg, 2));
	}

	public override void Click(Point mousePosition)
	{
		/* do nothing */
	}
}
