namespace ChasmTracker.Pages.InfoWindows;

using System.Threading.Channels;
using ChasmTracker.Pages.TrackViews;
using ChasmTracker.Songs;
using ChasmTracker.Utility;
using ChasmTracker.VGA;

public class TrackView1Window : TrackViewWindowBase
{
	public override string ConfigurationID => "track36";

	public TrackView1Window(int windowType, Shared<int> selectedChannel, int height, int firstChannel)
		: base(windowType, selectedChannel, height, firstChannel, new TrackView1())
	{
	}

	protected override int ChannelWidth => 1;
	protected override bool Separator => false;

	protected override int GetRightEdge()
	{
		/* IT draws nine more blank "channels" on the right */
		return Status.Flags.HasFlag(StatusFlags.ClassicMode) ? 73 : 64;
	}

	protected override void DrawChannelHeader(int chan, int column, int @base, int fg)
	{
		VGAMem.DrawHalfWidthCharacters("0123456789"[chan / 10], "0123456789"[chan % 10], new Point(column, @base), (fg, 1), (fg, 1));
	}

	public override void Draw(int @base, int fullHeight, bool isActive)
	{
		int right = GetRightEdge();

		VGAMem.DrawBox(new Point(4, @base), new Point(right, @base + fullHeight - 1), BoxTypes.Thick | BoxTypes.Inner | BoxTypes.Inset);

		int chan, column;

		for (chan = FirstChannel, column = 5; column + FullChannelWidth < right && chan <= Song.CurrentSong.Channels.Length; chan++, column += FullChannelWidth)
		{
			var channel = Song.CurrentSong.Channels[chan - 1];

			byte fg;

			if (channel.Flags.HasFlag(ChannelFlags.Mute))
				fg = (chan == SelectedChannel) ? (byte)6 : (byte)1;
			else
				fg = (chan == SelectedChannel) ? (byte)3 : (isActive ? (byte)2 : (byte)0);

			DrawChannelHeader(chan, column, @base, fg);
		}

		for (; column + FullChannelWidth < right; column++)
			VGAMem.DrawCharacter(0, new Point(column, @base), (1, 1));

		DrawTrackView(@base, fullHeight);
	}
}
