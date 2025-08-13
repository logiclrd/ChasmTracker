using System;
using System.Runtime.CompilerServices;
using System.Xml.Serialization;
using ChasmTracker.Playback;
using ChasmTracker.Songs;
using ChasmTracker.Utility;
using ChasmTracker.VGA;

namespace ChasmTracker.Pages.InfoWindows;

public class SamplesInfoWindow : InfoWindow
{
	public override string ConfigurationID => "samples";

	Shared<bool> _velocityMode;
	Shared<bool> _instrumentNames;

	public SamplesInfoWindow(int windowType, Shared<int> selectedChannel, int height, int firstChannel, Shared<bool> velocityMode, Shared<bool> instrumentNames)
		: base(windowType, selectedChannel, height, firstChannel)
	{
		_velocityMode = velocityMode;
		_instrumentNames = instrumentNames;
	}

	public override int GetNumChannels() => Height - 2;

	public override bool UsesFirstRow => false;

	public override void Draw(int @base, int fullHeight, bool isActive)
	{
		VGAMem.DrawFillCharacters(new Point(5, @base + 1), new Point(28, @base + fullHeight - 2), (VGAMem.DefaultForeground, 0));
		VGAMem.DrawFillCharacters(new Point(31, @base + 1), new Point(61, @base + fullHeight - 2), (VGAMem.DefaultForeground, 0));

		VGAMem.DrawBox(new Point(4, @base), new Point(29, @base + fullHeight - 1), BoxTypes.Thick | BoxTypes.Inner | BoxTypes.Inset);
		VGAMem.DrawBox(new Point(30, @base), new Point(62, @base + fullHeight - 1), BoxTypes.Thick | BoxTypes.Inner | BoxTypes.Inset);

		if (Song.CurrentSong.IsStereo)
		{
			VGAMem.DrawFillCharacters(new Point(64, @base + 1), new Point(72, @base + fullHeight - 2), (VGAMem.DefaultForeground, 0));
			VGAMem.DrawBox(new Point(63, @base), new Point(73, @base + fullHeight - 1), BoxTypes.Thick | BoxTypes.Inner | BoxTypes.Inset);
		}
		else
			VGAMem.DrawFillCharacters(new Point(63, @base), new Point(73, @base + fullHeight), (VGAMem.DefaultForeground, 2));

		if (AudioPlayback.Mode == AudioPlaybackMode.Stopped)
		{
			for (int pos = @base + 1, c = FirstChannel; pos < @base + fullHeight - 1; pos++, c++)
			{
				ref var channel = ref Song.CurrentSong.Channels[c - 1];

				int fg;

				if (c == SelectedChannel)
					fg = channel.Flags.HasFlag(ChannelFlags.Mute) ? 6 : 3;
				else
				{
					if (channel.Flags.HasFlag(ChannelFlags.Mute))
						continue;

					fg = isActive ? 1 : 0;
				}

				VGAMem.DrawText(c.ToString("d2"), new Point(2, pos), (fg, 2));
			}

			return;
		}

		for (int pos = @base + 1, c = FirstChannel; pos < @base + fullHeight - 1; pos++, c++)
		{
			byte fg, fg2;

			ref var voice = ref Song.CurrentSong.Voices[c - 1];

			/* always draw the channel number */

			if (c == SelectedChannel)
			{
				fg = voice.Flags.HasFlag(ChannelFlags.Mute) ? (byte)6 : (byte)3;
				VGAMem.DrawText(c.ToString("d2"), new Point(2, pos), (fg, 2));
			}
			else if (!voice.Flags.HasFlag(ChannelFlags.Mute))
			{
				fg = isActive ? (byte)1 : (byte)0;
				VGAMem.DrawText(c.ToString("d2"), new Point(2, pos), (fg, 2));
			}

			if (!((voice.CurrentSampleData.Span != Memory<byte>.Empty.Span) && (voice.Length > 0)) && !voice.Flags.HasFlag(ChannelFlags.AdLib))
				continue;

			/* first box: vu meter */
			int vu;

			if (_velocityMode)
				vu = voice.FinalVolume >> 8;
			else
				vu = voice.VUMeter >> 2;

			if (voice.Flags.HasFlag(ChannelFlags.Mute))
			{
				fg = 1; fg2 = 2;
			}
			else
			{
				fg = 5; fg2 = 4;
			}

			VGAMem.DrawVUMeter(new Point(5, pos), 24, vu, fg, fg2);

			/* second box: sample number/name */
			int ins = Song.CurrentSong.GetInstrumentNumber(voice.Instrument);
			int smp;

			/* figuring out the sample number is an ugly hack... considering all the crap that's
			copied to the channel, i'm surprised that the sample and instrument numbers aren't
			in there somewhere... */
			if (voice.Sample != null)
				smp = Song.CurrentSong.GetSampleNumber(voice.Sample);
			else
				smp = ins = 0;

			if (smp < 0 || smp >= Song.CurrentSong.Samples.Count)
				smp = ins = 0; /* This sample is not in the sample array */

			if (smp != 0)
			{
				int n;

				VGAMem.DrawText(smp.ToString99(), new Point(31, pos), (6, 0));

				if (ins != 0)
				{
					VGAMem.DrawCharacter('/', new Point(33, pos), (6, 0));
					VGAMem.DrawText(ins.ToString99(), new Point(34, pos), (6, 0));
					n = 36;
				}
				else
					n = 33;

				if (voice.Volume == 0)
					fg = 4;
				else if (voice.Flags.HasAnyFlag(ChannelFlags.KeyOff | ChannelFlags.NoteFade))
					fg = 7;
				else
					fg = 6;

				VGAMem.DrawCharacter(':', new Point(n++, pos), (fg, 0));

				string ptr;

				if (_instrumentNames && voice.Instrument != null)
					ptr = voice.Instrument.Name ?? "";
				else
					ptr = Song.CurrentSong.Samples[smp]?.Name ?? "";

				VGAMem.DrawTextLen(ptr, 25, new Point(n, pos), (6, 0));
			}
			else if ((ins != 0) && (voice.Instrument != null) && (voice.Instrument.MIDIChannelMask != 0))
			{
				// XXX why? what?
				if (voice.Instrument.MIDIChannelMask >= 0x10000)
					VGAMem.DrawText((((c - 1) % 16) + 1).ToString("d2"), new Point(31, pos), (6, 0));
				else
				{
					int ch = 0;
					while (0 == (voice.Instrument.MIDIChannelMask & (1 << ch)))
						++ch;
					VGAMem.DrawText(ch.ToString("d2"), new Point(31, pos), (6, 0));
				}

				VGAMem.DrawCharacter('/', new Point(33, pos), (6, 0));
				VGAMem.DrawText(ins.ToString99(), new Point(34, pos), (6, 0));

				int n = 36;

				if (voice.Volume == 0)
					fg = 4;
				else if (voice.Flags.HasAnyFlag(ChannelFlags.KeyOff | ChannelFlags.NoteFade))
					fg = 7;
				else
					fg = 6;

				VGAMem.DrawCharacter(':', new Point(n++, pos), (fg, 0));
				VGAMem.DrawTextLen(voice.Instrument?.Name ?? "", 25, new Point(n, pos), (6, 0));
			}
			else
				continue;

			/* last box: panning. this one's much easier than the
			* other two, thankfully :) */
			if (Song.CurrentSong.IsStereo)
			{
				if (voice.Sample != null)
				{
					if (voice.Flags.HasFlag(ChannelFlags.Surround))
						VGAMem.DrawText("Surround", new Point(64, pos), (2, 0));
					else if (voice.FinalPanning >> 2 == 0)
						VGAMem.DrawText("Left", new Point(64, pos), (2, 0));
					else if ((voice.FinalPanning + 3) >> 2 == 64)
						VGAMem.DrawText("Right", new Point(68, pos), (2, 0));
					else
						VGAMem.DrawThumbBar(new Point(64, pos), 9, 0, 256, voice.FinalPanning, false);
				}
			}
		}
	}

	public override void Click(Point mousePosition)
	{
		SelectedChannel.Value = (mousePosition.Y + FirstChannel).Clamp(1, Constants.MaxChannels);
	}
}
