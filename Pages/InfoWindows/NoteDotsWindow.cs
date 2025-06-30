using ChasmTracker.Playback;
using ChasmTracker.Songs;
using ChasmTracker.Utility;
using ChasmTracker.VGA;

namespace ChasmTracker.Pages.InfoWindows;

public class NoteDotsWindow : InfoWindow
{
	public override string ConfigurationID => "dots";

	Shared<bool> _velocityMode;

	public NoteDotsWindow(int windowType, Shared<int> selectedChannel, int height, int firstChannel, Shared<bool> velocityMode)
		: base(windowType, selectedChannel, height, firstChannel)
	{
		_velocityMode = velocityMode;
	}

	int[,] _dotField = new int[73, 36]; // f#2 -> f#8 = 73 columns

	public override int GetNumChannels() => Height - 2;

	public override bool UsesFirstRow => false;

	/* Yay it works, only took me forever and a day to get it right. */
	public override void Draw(int @base, int fullHeight, bool isActive)
	{
		VGAMem.DrawFillCharacters(new Point(5, @base + 1), new Point(77, @base + fullHeight - 2), (VGAMem.DefaultForeground, 0));
		VGAMem.DrawBox(new Point(4, @base), new Point(78, @base + fullHeight - 1), BoxTypes.Thick | BoxTypes.Inner | BoxTypes.Inset);

		for (int n = 0; n < Song.CurrentSong.Voices.Length; n++)
		{
			ref var voice = ref Song.CurrentSong.Voices[n];

			/* 31 = f#2, 103 = f#8. (i hope ;) */
			if ((voice.Sample == null) || (voice.Note > 31) || (voice.Note > 103))
				continue;

			int pos = (voice.MasterChannel != 0) ? voice.MasterChannel : (1 + n);
			if (pos < FirstChannel)
				continue;

			pos -= FirstChannel;
			if (pos > fullHeight - 1)
				continue;

			int fg = voice.Flags.HasFlag(ChannelFlags.Mute) ? 1 : (Song.CurrentSong.GetSampleNumber(voice.Sample) % 4 + 2);

			int v;

			if (_velocityMode || Status.Flags.HasFlag(StatusFlags.ClassicMode))
				v = (voice.FinalVolume + 2047) >> 11;
			else
				v = (voice.VUMeter + 31) >> 5;

			int d = _dotField[voice.Note - 31, pos];
			int dn = (v << 4) | fg;
			if (dn > d)
				_dotField[voice.Note - 31, pos] = dn;
		}

		for (int c = FirstChannel, pos = 0; pos < fullHeight - 2; pos++, c++)
		{
			int fg;

			for (int n = 0; n < 73; n++)
			{
				int d = (_dotField[n, pos] != 0) ? _dotField[n, pos] : 0x06;

				fg = d & 0xf;

				int v = d >> 4;

				VGAMem.DrawCharacter(unchecked((char)(v + 193)), new Point(n + 5, pos + @base + 1), (fg, 0));
			}

			if (c == SelectedChannel)
				fg = Song.CurrentSong.Channels[c - 1].IsMuted ? 6 : 3;
			else
			{
				if (Song.CurrentSong.Channels[c - 1].IsMuted)
					continue;

				fg = isActive ? 1 : 0;
			}

			VGAMem.DrawText(c.ToString("d2"), new Point(2, pos + @base + 1), (fg, 2));
		}
	}

	public override void Click(Point mousePosition)
	{
		SelectedChannel.Value = (mousePosition.Y + FirstChannel).Clamp(1, Constants.MaxChannels);
	}
}
