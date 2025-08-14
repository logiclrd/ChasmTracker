namespace ChasmTracker.Pages;

using ChasmTracker.VGA;
using ChasmTracker.Utility;
using ChasmTracker.Songs;
using ChasmTracker.Widgets;

public class OrderListVolumesPage : OrderListPage
{
	ThumbBarWidget[] thumbBarChannelVolume = new ThumbBarWidget[Constants.MaxChannels];

	public OrderListVolumesPage()
		: base(PageNumbers.OrderListVolumes, "Order List and Channel Volume (F11)", HelpTexts.OrderListVolume)
	{
		for (int n = 0; n < Constants.MaxChannels; n++)
		{
			int x = !n.HasBitSet(1) ? 31 : 65;
			int y = 15 + n / 2;

			thumbBarChannelVolume[n] = new ThumbBarWidget(new Point(x, y), 9, 0, 64);
			thumbBarChannelVolume[n].Changed += UpdateValuesInSong;
		}

		AddWidgets(thumbBarChannelVolume);

		LinkSelectedWidgetIndex(s_commonSelectedWidgetIndex);
	}

	public override void DrawConst()
	{
		CommonDrawConst();

		VGAMem.DrawText(" Volumes ", new Point(31, 14), (0, 3));
		VGAMem.DrawText(" Volumes ", new Point(65, 14), (0, 3));

		for (int n = 1; n <= 32; n++)
		{
			int fg = 0;

			if (!Status.Flags.HasFlag(StatusFlags.ClassicMode))
			{
				if (ActiveWidgetContext?.SelectedWidgetIndex?.Value == n)
					fg = 3;
			}

			string buf = "Channel " + n.ToString("d2");

			VGAMem.DrawText(buf, new Point(20, 14 + n), (fg, 2));

			fg = 0;

			if (!Status.Flags.HasFlag(StatusFlags.ClassicMode))
			{
				if (ActiveWidgetContext?.SelectedWidgetIndex?.Value == n + 32)
					fg = 3;
			}

			buf = "Channel " + (n + 32).ToString("d2");

			VGAMem.DrawText(buf, new Point(54, 14 + n), (fg, 2));
		}
	}

	void UpdateValuesInSong()
	{
		Status.Flags |= StatusFlags.SongNeedsSave;

		for (int n = 0; n < 64; n++)
			Song.CurrentSong.Channels[n].Volume = thumbBarChannelVolume[n].Value;
	}

	public override void NotifySongChanged()
	{
		for (int n = 0; n < 64; n++)
		{
			ref var chn = ref Song.CurrentSong.Channels[n];

			thumbBarChannelVolume[n].Value = chn.Volume;
		}
	}
}
