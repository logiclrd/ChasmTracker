namespace ChasmTracker.Pages;

using ChasmTracker.VGA;
using ChasmTracker.Utility;
using ChasmTracker.Songs;
using ChasmTracker.Widgets;

public class OrderListPanningPage : OrderListPage
{
	PanBarWidget[] panBarChannelPanning = new PanBarWidget[Constants.MaxChannels];

	public OrderListPanningPage()
		: base(PageNumbers.OrderListPanning, "Order List and Panning (F11)", HelpTexts.OrderListPanning)
	{
		for (int n = 0; n < Constants.MaxChannels; n++)
		{
			int x = !n.HasBitSet(1) ? 20 : 54;
			int y = 15 + n / 2;

			panBarChannelPanning[n] = new PanBarWidget(new Point(x, y), n);
			panBarChannelPanning[n].Changed += UpdateValuesInSong;
		}

		Widgets.AddRange(panBarChannelPanning);

		LinkSelectedWidgetIndex(s_commonSelectedWidgetIndex);
	}

	public override void DrawConst()
	{
		CommonDrawConst();

		VGAMem.DrawText("L   M   R", new Point(31, 14), (0, 3));
		VGAMem.DrawText("L   M   R", new Point(65, 14), (0, 3));
	}

	void UpdateValuesInSong()
	{
		Status.Flags |= StatusFlags.SongNeedsSave;

		for (int n = 0; n < 64; n++)
		{
			ref var chn = ref Song.CurrentSong.Channels[n];

			/* yet another modplug hack here! */
			chn.Panning = panBarChannelPanning[n].Value * 4;

			if (panBarChannelPanning[n].IsSurround)
				chn.Flags |= ChannelFlags.Surround;
			else
				chn.Flags &= ~ChannelFlags.Surround;

			Song.CurrentSong.SetChannelMute(n, panBarChannelPanning[n].IsMuted);
		}
	}

	/* called when a channel is muted/unmuted by means other than the panning
	 * page (alt-f10 in the pattern editor, space on the info page...) */
	public void RecheckMutedChannels()
	{
		for (int n = 0; n < 64; n++)
			panBarChannelPanning[n].IsMuted = Song.CurrentSong.Channels[n].Flags.HasFlag(ChannelFlags.Mute);

		if (Status.CurrentPage is OrderListPanningPage)
			Status.Flags |= StatusFlags.NeedUpdate;
	}

	public override void NotifySongChanged()
	{
		for (int n = 0; n < 64; n++)
		{
			ref var chn = ref Song.CurrentSong.Channels[n];

			panBarChannelPanning[n].Value = chn.Panning / 4;
			panBarChannelPanning[n].IsSurround = chn.Flags.HasFlag(ChannelFlags.Surround);
			panBarChannelPanning[n].IsMuted = chn.Flags.HasFlag(ChannelFlags.Mute);
		}
	}

	public override void SetPage()
	{
		RecheckMutedChannels();
	}
}
