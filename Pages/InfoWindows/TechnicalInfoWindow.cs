using System;

namespace ChasmTracker.Pages.InfoWindows;

using ChasmTracker.Songs;
using ChasmTracker.Utility;
using ChasmTracker.VGA;

public class TechnicalInfoWindow : InfoWindow
{
	public override string ConfigurationID => "tech";

	public TechnicalInfoWindow(int windowType, Shared<int> selectedChannel, int height, int firstChannel)
		: base(windowType, selectedChannel, height, firstChannel)
	{
	}

	public override int GetNumChannels() => Height - 2;

	public override bool UsesFirstRow => true;

	public override void Draw(int @base, int height, bool isActive)
	{
		/*
		FVl - 0-128, final calculated volume, taking everything into account:
			(sample volume, sample global volume, instrument volume, inst. global volume,
			volume envelope, volume swing, fadeout, channel volume, song global volume, effects (I/Q/R)
		Vl - 0-64, sample volume / volume column (also affected by I/Q/R)
		CV - 0-64, channel volume (M/N)
		SV - 0-64, sample global volume + inst global volume
		Fde - 0-512, HALF the fade
			(initially 1024, and subtracted by instrument fade value each tick when fading out)
		Pn - 0-64 (or "Su"), final channel panning
			+ pan swing + pitch/pan + current pan envelope value! + Yxx
			(note: suggests that Xxx panning is reduced to 64 values when it's applied?)
		PE - 0-64, pan envelope
			note: this value is not changed if pan env is turned off (e.g. with S79) -- so it's copied
		all of the above are still set to valid values in sample mode
		*/

		VGAMem.DrawFillCharacters(new Point(5, @base + 1), new Point(29, @base + Height - 2), (VGAMem.DefaultForeground, 0));
		VGAMem.DrawBox(new Point(4, @base), new Point(30, @base + Height - 1), BoxTypes.Thick | BoxTypes.Inner | BoxTypes.Inset);
		VGAMem.DrawText("Frequency", new Point(6, @base), (2, 1));
		VGAMem.DrawText("Position", new Point(17, @base), (2, 1));
		VGAMem.DrawText("Smp", new Point(27, @base), (2, 1));

		VGAMem.DrawFillCharacters(new Point(32, @base + 1), new Point(56, @base + Height - 2), (VGAMem.DefaultForeground, 0));
		VGAMem.DrawBox(new Point(31, @base), new Point(57, @base + Height - 1), BoxTypes.Thick | BoxTypes.Inner | BoxTypes.Inset);
		VGAMem.DrawText("FVl", new Point(32, @base), (2, 1));
		VGAMem.DrawText("Vl", new Point(36, @base), (2, 1));
		VGAMem.DrawText("CV", new Point(39, @base), (2, 1));
		VGAMem.DrawText("SV", new Point(42, @base), (2, 1));
		VGAMem.DrawText("VE", new Point(45, @base), (2, 1));
		VGAMem.DrawText("Fde", new Point(48, @base), (2, 1));
		VGAMem.DrawText("Pn", new Point(52, @base), (2, 1));
		VGAMem.DrawText("PE", new Point(55, @base), (2, 1));

		if (Song.CurrentSong.IsInstrumentMode)
		{
			VGAMem.DrawFillCharacters(new Point(59, @base + 1), new Point(65, @base + Height - 2), (VGAMem.DefaultForeground, 0));
			VGAMem.DrawBox(new Point(58, @base), new Point(66, @base + Height - 1), BoxTypes.Thick | BoxTypes.Inner | BoxTypes.Inset);
			VGAMem.DrawText("NNA", new Point(59, @base), (2, 1));
			VGAMem.DrawText("Tot", new Point(63, @base), (2, 1));
		}

		for (int pos = @base + 1, c = FirstChannel; pos < @base + Height - 1; pos++, c++)
		{
			ref var channel = ref Song.CurrentSong.Channels[c];
			ref var voice = ref Song.CurrentSong.Voices[c];

			int fg;

			if (c == SelectedChannel)
				fg = channel.Flags.HasAllFlags(ChannelFlags.Mute) ? 6 : 3;
			else
			{
				if (channel.Flags.HasAllFlags(ChannelFlags.Mute))
					fg = 2;
				else
					fg = isActive ? 1 : 0;
			}

			VGAMem.DrawText(c.ToString99(), new Point(2, pos), (fg, 2)); /* channel number */

			VGAMem.DrawCharacter(168, new Point(15, pos), (2, 0));
			VGAMem.DrawCharacter(168, new Point(26, pos), (2, 0));
			VGAMem.DrawCharacter(168, new Point(35, pos), (2, 0));
			VGAMem.DrawCharacter(168, new Point(38, pos), (2, 0));
			VGAMem.DrawCharacter(168, new Point(41, pos), (2, 0));
			VGAMem.DrawCharacter(168, new Point(44, pos), (2, 0));
			VGAMem.DrawCharacter(168, new Point(47, pos), (2, 0));
			VGAMem.DrawCharacter(168, new Point(51, pos), (2, 0));
			VGAMem.DrawCharacter(168, new Point(54, pos), (2, 0));

			if (Song.CurrentSong.IsInstrumentMode)
			{
				VGAMem.DrawText("---\xa8", new Point(59, pos), (2, 0)); /* will be overwritten if something's playing */

				/* count how many voices claim this channel */
				int tot = 0;

				for (int nv = 0, m = Song.CurrentSong.Voices.Length; nv < m; nv++)
				{
					ref var v = ref Song.CurrentSong.Voices[nv];

					if (v.MasterChannel == c && ((!v.CurrentSampleData.IsEmpty && (v.Length != 0)) || v.Flags.HasAllFlags(ChannelFlags.AdLib)))
						tot++;
				}

				if ((!voice.CurrentSampleData.IsEmpty && (voice.Length != 0)) || voice.Flags.HasAllFlags(ChannelFlags.AdLib))
					tot++;

				VGAMem.DrawText(tot.ToString("d3"), new Point(63, pos), (2, 0));
			}

			int smp;

			if ((!voice.CurrentSampleData.IsEmpty && (voice.Length != 0)) || voice.Flags.HasAllFlags(ChannelFlags.AdLib) && (voice.Sample != null))
			{
				// again with the hacks...
				smp = Song.CurrentSong.Samples.IndexOf(voice.Sample);
				if (smp < 0)
					continue;
			}
			else
				continue;

			// Frequency
			VGAMem.DrawText($"{voice.SampleFrequency,10}", new Point(5, pos), (2, 0));
			// Position
			VGAMem.DrawText($"{voice.Position,10}", new Point(16, pos), (2, 0));

			VGAMem.DrawText(smp.ToString("d3"), new Point(27, pos), (2, 0)); // Smp
			VGAMem.DrawText((voice.FinalVolume / 128).ToString("d3"), new Point(32, pos), (2, 0)); // FVl
			VGAMem.DrawText((voice.Volume >> 2).ToString("d2"), new Point(36, pos), (2, 0)); // Vl
			VGAMem.DrawText(voice.GlobalVolume.ToString("d2"), new Point(39, pos), (2, 0)); // CV
			VGAMem.DrawText(voice.Sample!.GlobalVolume.ToString("d2"), new Point(42, pos), (2, 0)); // SV
																																															// FIXME: VE means volume envelope. Also, voice.InstrumentVolume is actually sample global volume
			VGAMem.DrawText((voice.InstrumentVolume).ToString("d2"), new Point(45, pos), (2, 0)); // VE
			VGAMem.DrawText((voice.FadeOutVolume / 128).ToString("d3"), new Point(48, pos), (2, 0)); // Fde

			// Pn
			if (voice.Flags.HasAllFlags(ChannelFlags.Surround))
				VGAMem.DrawText("Su", new Point(52, pos), (2, 0));
			else
				VGAMem.DrawText((voice.Panning >> 2).ToString("d2"), new Point(52, pos), (2, 0));

			VGAMem.DrawText((voice.FinalPanning >> 2).ToString("d2"), new Point(55, pos), (2, 0)); // PE

			if (Song.CurrentSong.IsInstrumentMode)
			{
				string nnaStr;

				switch (voice.NewNoteAction)
				{
					case NewNoteActions.NoteCut: nnaStr = "Cut"; break;
					case NewNoteActions.Continue: nnaStr = "Con"; break;
					case NewNoteActions.NoteOff: nnaStr = "Off"; break;
					case NewNoteActions.NoteFade: nnaStr = "Fde"; break;
					default: nnaStr = "???"; break;
				}

				VGAMem.DrawText(nnaStr, new Point(59, pos), (2, 0));
			}
		}
	}

	public override void Click(Point mousePosition)
	{
		SelectedChannel.Value = (mousePosition.Y + FirstChannel - 1).Clamp(1, Constants.MaxChannels);
	}
}
