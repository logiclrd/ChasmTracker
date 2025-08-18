using System;
using System.Linq;

namespace ChasmTracker.Playback;

using System.Runtime.CompilerServices;
using ChasmTracker.MIDI;
using ChasmTracker.Songs;
using ChasmTracker.Utility;

public static class SongRenderer
{
	// Volume ramp length, in 1/10 ms
	const int VolumeRampLength = 146; // 1.46ms = 64 samples at 44.1kHz

	// VU meter
	const int VUMeterDecay = 16;

	delegate int ConversionFunction(Span<byte> ptr, Span<int> buffer, int samples, int[] minSample, int[] maxSample);

	static Random s_rnd = new Random();

	// The volume we have here is in range 0..(63*255) (0..16065)
	// We should keep that range, but convert it into a logarithmic
	// one such that a change of 256*8 (2048) corresponds to a halving
	// of the volume.
	//   logvolume = 2^(linvolume / (4096/8)) * (4096/64)
	// However, because the resolution of MIDI volumes
	// is merely 128 units, we can use a lookup table.
	//
	// In this table, each value signifies the minimum value
	// that volume must be in order for the result to be
	// that table index.
	static readonly ushort[] GMvolTransition =
		{
			    0, 2031, 4039, 5214, 6048, 6694, 7222, 7669,
			 8056, 8397, 8702, 8978, 9230, 9462, 9677, 9877,
			10064,10239,10405,10562,10710,10852,10986,11115,
			11239,11357,11470,11580,11685,11787,11885,11980,
			12072,12161,12248,12332,12413,12493,12570,12645,
			12718,12790,12860,12928,12995,13060,13123,13186,
			13247,13306,13365,13422,13479,13534,13588,13641,
			13693,13745,13795,13844,13893,13941,13988,14034,
			14080,14125,14169,14213,14256,14298,14340,14381,
			14421,14461,14501,14540,14578,14616,14653,14690,
			14727,14763,14798,14833,14868,14902,14936,14970,
			15003,15035,15068,15100,15131,15163,15194,15224,
			15255,15285,15315,15344,15373,15402,15430,15459,
			15487,15514,15542,15569,15596,15623,15649,15675,
			15701,15727,15753,15778,15803,15828,15853,15877,
			15901,15925,15949,15973,15996,16020,16043,16065,
		};


	// We use binary search to find the right slot
	// with at most 7 comparisons.
	static int FindVolume(ushort vol)
	{
		int l = 0, r = 128;

		while (l < r)
		{
			int m = l + ((r - l) / 2);
			ushort p = GMvolTransition[m];

			if (p < vol)
				l = m + 1;
			else
				r = m;
		}

		return l;
	}


	static void Tremor(ref SongVoice chan, ref int vol)
	{
		if (chan.CountdownTremor.HasBitSet(128) && (chan.Length != 0))
		{
			if (chan.CountdownTremor == 128)
				chan.CountdownTremor = (unchecked((uint)chan.MemTremor) >> 4) | 192u;
			else if (chan.CountdownTremor == 192)
				chan.CountdownTremor = (unchecked((uint)chan.MemTremor) & 0xf) | 128u;
			else
				chan.CountdownTremor--;
		}

		if ((chan.CountdownTremor & 192) == 128)
			vol = 0;

		chan.Flags |= ChannelFlags.FastVolumeRamp;
	}

	static int Vibrato(Song csf, ref SongVoice chan, int frequency)
	{
		int vibpos = chan.VibratoPosition & 0xFF;
		int vdelta;

		switch (chan.VibratoType)
		{
			case VibratoType.Sine:
			default:
				vdelta = Tables.SineTable[vibpos];
				break;
			case VibratoType.RampDown:
				vdelta = Tables.RampDownTable[vibpos];
				break;
			case VibratoType.Square:
				vdelta = Tables.SquareTable[vibpos];
				break;
			case VibratoType.Random:
				vdelta = s_rnd.Next(128) - 64;
				break;
		}

		uint vdepth;

		if (csf.Flags.HasAllFlags(SongFlags.ITOldEffects))
		{
			vdepth = 5;
			vdelta = -vdelta; // yes, IT does vibrato backwards in old-effects mode. try it.
		}
		else
			vdepth = 6;

		vdelta = (vdelta * (int)chan.VibratoDepth) >> unchecked((int)vdepth);

		frequency = csf.EffectDoFrequencySlide(csf.Flags, frequency, vdelta, false);

		// handle on tick-N, or all ticks if not in old-effects mode
		if (!csf.Flags.HasAllFlags(SongFlags.FirstTick) || !csf.Flags.HasAllFlags(SongFlags.ITOldEffects))
			chan.VibratoPosition = (vibpos + 4 * chan.VibratoSpeed) & 0xFF;

		return frequency;
	}

	static int SampleVibrato(ref SongVoice chan, int frequency)
	{
		int vibpos = chan.AutoVibratoPosition & 0xFF;
		int vdelta, adepth = 1;

		var pIns = chan.Sample;

		if (pIns == null) return frequency;

		/*
		1) Mov AX, [SomeVariableNameRelatingToVibrato]
		2) Add AL, Rate
		3) AdC AH, 0
		4) AH contains the depth of the vibrato as a fine-linear slide.
		5) Mov [SomeVariableNameRelatingToVibrato], AX  ; For the next cycle.
		*/

		/* OpenMPT test case VibratoSweep0.it:
			don't calculate autovibrato if the speed is 0 */
		if (pIns.VibratoSpeed != 0) {
			adepth = chan.AutoVibratoDepth; // (1)
			adepth += pIns.VibratoRate & 0xff; // (2 & 3)
			/* need this cast -- if adepth is unsigned, large autovib will crash the mixer (why? I don't know!)
			but if vib_depth is changed to signed, that screws up other parts of the code. ugh. */
			adepth = Math.Min(adepth, (int)(pIns.VibratoDepth << 8));
			chan.AutoVibratoDepth = adepth; // (5)
			adepth >>= 8; // (4)

			chan.AutoVibratoPosition += pIns.VibratoSpeed;
		}

		switch(pIns.VibratoType)
		{
			case VibratoType.Sine:
			default:
				vdelta = Tables.SineTable[vibpos];
				break;
			case VibratoType.RampDown:
				vdelta = Tables.RampDownTable[vibpos];
				break;
			case VibratoType.Square:
				vdelta = Tables.SquareTable[vibpos];
				break;
			case VibratoType.Random:
				vdelta = s_rnd.Next(128) - 64;
				break;
		}

		vdelta = (vdelta * adepth) >> 6;

		int l = Math.Abs(vdelta);

		int[] linearSlideTable, fineLinearSlideTable;

		if (vdelta < 0)
		{
			linearSlideTable = Tables.LinearSlideUpTable;
			fineLinearSlideTable = Tables.FineLinearSlideUpTable;
		}
		else
		{
			linearSlideTable = Tables.LinearSlideDownTable;
			fineLinearSlideTable = Tables.FineLinearSlideDownTable;
		}

		if(l < 16)
			vdelta = (int)(frequency * (long)fineLinearSlideTable[l] / 0x10000 - frequency);
		else
			vdelta = (int)(frequency * (long)linearSlideTable[l >> 2] / 0x10000 - frequency);

		return frequency - vdelta;
	}


	static void ProcessVolumeEnvelope(ref SongVoice chan, ref int nVol)
	{
		var pEnv = chan.Instrument;

		if (pEnv == null) return;

		int vol = nVol;

		if ((chan.Flags.HasAllFlags(ChannelFlags.VolumeEnvelope) || pEnv.Flags.HasAllFlags(InstrumentFlags.VolumeEnvelope))
		 && (pEnv.VolumeEnvelope != null)
		 && pEnv.VolumeEnvelope.Nodes.Any())
		{
			int envPos = chan.VolumeEnvelopePosition - 1;
			int pt = pEnv.VolumeEnvelope.Nodes.Count - 1;

			if (chan.VolumeEnvelopePosition == 0)
				return;

			for (int i = 0; i < pEnv.VolumeEnvelope.Nodes.Count - 1; i++)
			{
				if (envPos <= pEnv.VolumeEnvelope.Nodes[i].Tick)
				{
					pt = i;
					break;
				}
			}

			int x2 = pEnv.VolumeEnvelope.Nodes[pt].Tick;
			int x1, envVol;

			if (envPos >= x2)
			{
				envVol = pEnv.VolumeEnvelope.Nodes[pt].Value << 2;
				x1 = x2;
			}
			else if (pt > 0)
			{
				envVol = pEnv.VolumeEnvelope.Nodes[pt-1].Value << 2;
				x1 = pEnv.VolumeEnvelope.Nodes[pt-1].Tick;
			}
			else
			{
				envVol = 0;
				x1 = 0;
			}

			if (envPos > x2)
				envPos = x2;

			if (x2 > x1 && envPos > x1)
				envVol += ((envPos - x1) * (((int)(pEnv.VolumeEnvelope.Nodes[pt].Value << 2)) - envVol)) / (x2 - x1);


			envVol = envVol.Clamp(0, 256);
			vol = (vol * envVol) >> 8;
		}

		nVol = vol;
	}

	static void ProcessPanningEnvelope(ref SongVoice chan)
	{
		var pEnv = chan.Instrument;

		if (pEnv == null) return;

		if ((chan.Flags.HasAllFlags(ChannelFlags.PanningEnvelope) || pEnv.Flags.HasAllFlags(InstrumentFlags.PanningEnvelope))
		 && (pEnv.PanningEnvelope != null)
		 && pEnv.PanningEnvelope.Nodes.Any())
		{
			int envPos = chan.PanningEnvelopePosition - 1;
			int pt = pEnv.PanningEnvelope.Nodes.Count - 1;

			if (chan.PanningEnvelopePosition == 0)
				return;

			for (int i = 0; i < pEnv.PanningEnvelope.Nodes.Count - 1; i++)
			{
				if (envPos <= pEnv.PanningEnvelope.Nodes[i].Tick)
				{
					pt = i;
					break;
				}
			}

			int x2 = pEnv.PanningEnvelope.Nodes[pt].Tick, y2 = pEnv.PanningEnvelope.Nodes[pt].Value;
			int x1, envPan;

			if (envPos >= x2)
			{
				envPan = y2;
				x1 = x2;
			}
			else if (pt > 0)
			{
				envPan = pEnv.PanningEnvelope.Nodes[pt - 1].Value;
				x1 = pEnv.PanningEnvelope.Nodes[pt - 1].Tick;
			}
			else
			{
				envPan = 128;
				x1 = 0;
			}

			if (x2 > x1 && envPos > x1) {
				envPan += ((envPos - x1) * (y2 - envPan)) / (x2 - x1);
			}

			envPan = envPan.Clamp(0, 64);

			int pan = chan.FinalPanning;

			if (pan >= 128) {
				pan += ((envPan - 32) * (256 - pan)) / 32;
			} else {
				pan += ((envPan - 32) * (pan)) / 32;
			}

			chan.FinalPanning = pan;
		}
	}


	static void ProcessInstrumentFade(ref SongVoice chan, ref int nVol)
	{
		var pEnv = chan.Instrument;
		int vol = nVol;

		if (pEnv == null) return;

		if (chan.Flags.HasAllFlags(ChannelFlags.NoteFade))
		{
			int fadeout = pEnv.FadeOut;

			if (fadeout > 0)
			{
				chan.FadeOutVolume -= fadeout << 1;

				if (chan.FadeOutVolume <= 0)
					chan.FadeOutVolume = 0;

				vol = (vol * chan.FadeOutVolume) >> 16;
			}
			else if (chan.FadeOutVolume == 0)
			{
				vol = 0;
			}
		}

		nVol = vol;
	}

	static void ProcessEnvelopes(ref SongVoice chan, ref int nVol)
	{
		// Volume Envelope
		ProcessVolumeEnvelope(ref chan, ref nVol);

		// Panning Envelope
		ProcessPanningEnvelope(ref chan);

		// FadeOut volume
		ProcessInstrumentFade(ref chan, ref nVol);
	}

	static void ProcessMIDIMacro(Song csf, int voiceNumber)
	{
		ref var chan = ref csf.Voices[voiceNumber];

		/* this is wrong; see OpenMPT's soundlib/Snd_fx.cpp:
		*
		*     This is "almost" how IT does it - apparently, IT seems to lag one row
		*     behind on global volume or channel volume changes.
		*
		* OpenMPT also doesn't entirely support IT's version of this macro, which is
		* just another demotivator for actually implementing it correctly *sigh* */

		if (csf.Flags.HasAnyFlag(SongFlags.FirstTick) && (chan.RowEffect == Effects.MIDI))
			csf.EffectMidiZxx(ref chan, voiceNumber);
	}

	static int Arpeggio(Song csf, ref SongVoice chan, int frequency)
	{
		int a = 0;

		int realTickCount = (csf.CurrentSpeed + csf.FrameDelay) - csf.TickCount;
		int tick = realTickCount % (csf.CurrentSpeed + csf.FrameDelay);

		switch (tick % 3)
		{
			case 1:
				a = chan.MemArpeggio >> 4;
				break;
			case 2:
				a = chan.MemArpeggio & 0xf;
				break;
		}

		if (a == 0)
			return frequency;

		return (int)(frequency * (long)Tables.LinearSlideUpTable[a * 16] / 65536);
	}

	static void PitchFilterEnvelope(ref SongVoice chan, ref int nEnvPitch, ref int nFrequency)
	{
		var pEnv = chan.Instrument;

		if (pEnv == null) return;

		if ((chan.Flags.HasAllFlags(ChannelFlags.PitchEnvelope) || pEnv.Flags.HasAnyFlag(InstrumentFlags.PitchEnvelope | InstrumentFlags.Filter))
		 && (pEnv.PitchEnvelope != null)
		 && pEnv.PitchEnvelope.Nodes.Any())
		{
			int envPos = chan.PitchEnvelopePosition - 1;
			int pt = pEnv.PitchEnvelope.Nodes.Count - 1;
			int frequency = nFrequency;
			int envPitch = nEnvPitch;

			if (chan.PitchEnvelopePosition == 0)
				return;

			for (int i = 0; i < pEnv.PitchEnvelope.Nodes.Count - 1; i++)
			{
				if (envPos <= pEnv.PitchEnvelope.Nodes[i].Tick)
				{
					pt = i;
					break;
				}
			}

			int x2 = pEnv.PitchEnvelope.Nodes[pt].Tick;
			int x1;

			if (envPos >= x2)
			{
				envPitch = (((int)pEnv.PitchEnvelope.Nodes[pt].Value) - 32) * 8;
				x1 = x2;
			}
			else if (pt > 0)
			{
				envPitch = (((int)pEnv.PitchEnvelope.Nodes[pt - 1].Value) - 32) * 8;
				x1 = pEnv.PitchEnvelope.Nodes[pt - 1].Tick;
			}
			else
			{
				envPitch = 0;
				x1 = 0;
			}

			if (envPos > x2)
				envPos = x2;

			if (x2 > x1 && envPos > x1)
			{
				int envpitchdest = (pEnv.PitchEnvelope.Nodes[pt].Value - 32) * 8;
				envPitch += ((envPos - x1) * (envpitchdest - envPitch)) / (x2 - x1);
			}

			// clamp to -255/255?
			envPitch = envPitch.Clamp(-256, 256);

			// Pitch Envelope
			if (!pEnv.Flags.HasAllFlags(InstrumentFlags.Filter))
			{
				int l = Math.Abs(envPitch);

				l = Math.Min(l, 255);

				int ratio = (envPitch < 0 ? Tables.LinearSlideDownTable : Tables.LinearSlideUpTable)[l];
				frequency = (int)(frequency * (long)ratio / 0x10000);
			}

			nFrequency = frequency;
			nEnvPitch = envPitch;
		}
	}


	static void ProcessEnvelope(ref SongVoice chan, SongInstrument? pEnv, Envelope? envelope,
							ref int position, ChannelFlags envFlag, InstrumentFlags loopFlag, InstrumentFlags susFlag,
							ChannelFlags fadeFlag)
	{
		if ((pEnv == null) || (envelope == null))
			return;

		int start = 0, end = 0x7fffffff;

		if (!chan.Flags.HasAllFlags(envFlag))
			return;

		/* OpenMPT test case EnvOffLength.it */
		if (pEnv.Flags.HasAllFlags(susFlag) && !chan.OldFlags.HasAllFlags(ChannelFlags.KeyOff))
		{
			start = envelope.Nodes[envelope.SustainStart].Tick;
			end = envelope.Nodes[envelope.SustainEnd].Tick + 1;
			fadeFlag = 0;
		}
		else if (pEnv.Flags.HasAllFlags(loopFlag))
		{
			start = envelope.Nodes[envelope.LoopStart].Tick;
			end = envelope.Nodes[envelope.LoopEnd].Tick + 1;
			fadeFlag = 0;
		}
		else
		{
			// End of envelope (?)
			start = end = envelope.Nodes.Last().Tick;
		}

		if (position >= end)
		{
			if ((fadeFlag != 0) && (envelope.Nodes.Last().Value == 0))
				chan.FadeOutVolume = chan.FinalVolume = 0;
			position = start;
			chan.Flags |= fadeFlag; // only relevant for volume envelope
		}

		position++;
	}

	static void IncrementEnvelopePositions(ref SongVoice chan)
	{
		var pEnv = chan.Instrument;

		if (pEnv == null) return;

		ProcessEnvelope(ref chan, pEnv, pEnv.VolumeEnvelope, ref chan.VolumeEnvelopePosition,
					ChannelFlags.VolumeEnvelope, InstrumentFlags.VolumeEnvelopeLoop, InstrumentFlags.VolumeEnvelopeSustain, ChannelFlags.NoteFade);
		ProcessEnvelope(ref chan, pEnv, pEnv.PanningEnvelope, ref chan.PanningEnvelopePosition,
					ChannelFlags.PanningEnvelope, InstrumentFlags.PanningEnvelopeLoop, InstrumentFlags.PanningEnvelopeSustain, 0);
		ProcessEnvelope(ref chan, pEnv, pEnv.PitchEnvelope, ref chan.PitchEnvelopePosition,
					ChannelFlags.PitchEnvelope, InstrumentFlags.PitchEnvelopeLoop, InstrumentFlags.PitchEnvelopeSustain, 0);
	}


	static bool UpdateSample(Song csf, ref SongVoice chan, int nChan, int masterVolume)
	{
		// Adjusting volumes
		if ((AudioPlayback.MixChannels < 2) || csf.Flags.HasAllFlags(SongFlags.NoStereo))
		{
			chan.RightVolumeNew = (chan.FinalVolume * masterVolume) >> 8;
			chan.LeftVolumeNew = chan.RightVolumeNew;
		}
		else if (chan.Flags.HasAllFlags(ChannelFlags.Surround) && !AudioPlayback.MixFlags.HasAllFlags(MixFlags.NoSurround))
		{
			chan.RightVolumeNew = (chan.FinalVolume * masterVolume) >> 8;
			chan.LeftVolumeNew = -chan.RightVolumeNew;
		}
		else
		{
			int pan = ((int) chan.FinalPanning) - 128;
			pan *= (int) csf.PanSeparation;
			pan /= 128;

			if ((csf.Flags.HasAllFlags(SongFlags.InstrumentMode))
			 && (chan.Instrument != null)
			 && (chan.Instrument.MIDIChannelMask > 0))
				GeneralMIDI.Pan(csf, nChan, unchecked((sbyte)pan));

			pan += 128;
			pan = pan.Clamp(0, 256);

			if (AudioPlayback.MixFlags.HasAllFlags(MixFlags.ReverseStereo))
				pan = 256 - pan;

			int realVol = (chan.FinalVolume * masterVolume) >> (8 - 1);

			chan.LeftVolumeNew  = (realVol * pan) >> 8;
			chan.RightVolumeNew = (realVol * (256 - pan)) >> 8;
		}

		// Clipping volumes
		if (chan.RightVolumeNew > 0xFFFF)
			chan.RightVolumeNew = 0xFFFF;

		if (chan.LeftVolumeNew  > 0xFFFF)
			chan.LeftVolumeNew  = 0xFFFF;

		// Check IDO
		chan.Flags &= ~(ChannelFlags.NoIDO | ChannelFlags.HQSource);

		switch (AudioPlayback.MixInterpolation)
		{
			case SourceMode.Nearest:
				chan.Flags |= ChannelFlags.NoIDO;
				break;
			case SourceMode.Linear:
				if (chan.Increment >= 0xFF00)
				{
					chan.Flags |= ChannelFlags.NoIDO;
					break;
				}
				goto default;
			default:
				if (chan.Increment == 0x10000)
					chan.Flags |= ChannelFlags.NoIDO;
				break;
		}

		chan.RightVolumeNew >>= Constants.MixingAttenuation;
		chan.LeftVolumeNew  >>= Constants.MixingAttenuation;
		chan.RightRamp =
		chan.LeftRamp  = 0;

		// Checking Ping-Pong Loops
		if (chan.Flags.HasAllFlags(ChannelFlags.PingPongFlag))
			chan.Increment = -chan.Increment;

		if (chan.Flags.HasAllFlags(ChannelFlags.Mute))
		{
			chan.LeftVolume = chan.RightVolume = 0;
		}
		else if (!AudioPlayback.MixFlags.HasAllFlags(MixFlags.NoRamping)
		      && chan.Flags.HasAllFlags(ChannelFlags.VolumeRamp)
		      && (chan.RightVolume != chan.RightVolumeNew || chan.LeftVolume != chan.LeftVolumeNew))
		{
			// Setting up volume ramp
			int rampLength = AudioPlayback.RampingSamples;
			int rightDelta = (chan.RightVolumeNew - chan.RightVolume) << Constants.VolumeRampPrecision;
			int leftDelta  = (chan.LeftVolumeNew  - chan.LeftVolume) << Constants.VolumeRampPrecision;

			switch (AudioPlayback.MixInterpolation)
			{
				case SourceMode.Spline:
				case SourceMode.Polyphase:
					/* XXX why only for spline/polyphase */
					if (((chan.RightVolume | chan.LeftVolume) != 0)
					 && ((chan.RightVolumeNew | chan.LeftVolumeNew) != 0)
					 && !chan.Flags.HasAllFlags(ChannelFlags.FastVolumeRamp))
					{
						rampLength = csf.BufferCount;

						int l = 1 << (Constants.VolumeRampPrecision - 1);
						int r = AudioPlayback.RampingSamples;

						rampLength = rampLength.Clamp(l, r);
					}

					break;
			}

			chan.RightRamp = rightDelta / rampLength;
			chan.LeftRamp = leftDelta / rampLength;
			chan.RightVolume = chan.RightVolumeNew - ((chan.RightRamp * rampLength) >> Constants.VolumeRampPrecision);
			chan.LeftVolume = chan.LeftVolumeNew - ((chan.LeftRamp * rampLength) >> Constants.VolumeRampPrecision);

			if ((chan.RightRamp | chan.LeftRamp) != 0)
				chan.RampLength = rampLength;
			else
			{
				chan.Flags &= ~ChannelFlags.VolumeRamp;
				chan.RightVolume = chan.RightVolumeNew;
				chan.LeftVolume  = chan.LeftVolumeNew;
			}
		} else {
			chan.Flags  &= ~ChannelFlags.VolumeRamp;
			chan.RightVolume = chan.RightVolumeNew;
			chan.LeftVolume  = chan.LeftVolumeNew;
		}

		chan.RightRampVolume = chan.RightVolume << Constants.VolumeRampPrecision;
		chan.LeftRampVolume = chan.LeftVolume << Constants.VolumeRampPrecision;

		// Adding the channel in the channel list
		csf.VoiceMix[csf.NumVoices++] = nChan;

		if (csf.NumVoices >= Constants.MaxVoices)
			return false;

		return true;
	}


	// XXX Rename this
	//Ranges:
	// chan_num = 0..(Constants.MaxChannels - 1)
	// freq = frequency in Hertz
	// vol = 0..16384
	// chan.InstrumentVolume = 0..64  (corresponds to the sample global volume and instrument global volume)
	static void GenerateKey(Song csf, ref SongVoice chan, int chanNum, int freq, int vol)
	{
		if (chan.Flags.HasAllFlags(ChannelFlags.Mute))
		{
			// don't do anything
			return;
		}

		if (csf.Flags.HasAllFlags(SongFlags.InstrumentMode)
		 && (chan.Instrument != null)
		 && (chan.Instrument.MIDIChannelMask > 0))
		{
			var bendMode = MIDIBendMode.Normal;

			/* TODO: If we're expecting a large bend exclusively
			* in either direction, update BendMode to indicate so.
			* This can be used to extend the range of MIDI pitch bending.
			*/

			int volume = vol;

			if (chan.Flags.HasAllFlags(ChannelFlags.AdLib) && volume > 0)
			{
				// find_volume translates volume from range 0..16384 to range 0..127. But why with that method?
				volume = FindVolume((ushort)volume) * chan.InstrumentVolume / 64;
			}
			else
			{
				// This gives a value in the range 0..127.
				volume = volume * chan.InstrumentVolume / 8192;
			}

			GeneralMIDI.SetFrequencyAndVolume(csf, chanNum, freq, (byte)volume, bendMode, chan.Flags.HasAllFlags(ChannelFlags.KeyOff));
		}

		if (chan.Flags.HasAllFlags(ChannelFlags.AdLib))
		{
			// Scaling is needed to get a frequency that matches with ST3 notes.
			// 8363 is st3s middle C sample rate. 261.625 is the Hertz for middle C in a tempered scale (A4 = 440)
			//Also, note that to be true to ST3, the frequencies should be quantized, like using the glissando control.

			// OPL_Patch is called in csf_process_effects, from csf_read_note or ProcessTick, before calling this method.
			int oplmilliHertz = (int)(freq * 261625L / 8363);

			csf.OPLHertzTouch(chanNum, oplmilliHertz, chan.Flags.HasAllFlags(ChannelFlags.KeyOff));

			// ST32 ignores global & master volume in adlib mode, guess we should do the same -Bisqwit
			// This gives a value in the range 0..63.
			// log_appendf(2,"vol: %d, voiceinsvol: %d", vol , chan.InstrumentVolume);
			csf.OPLTouch(chanNum, vol * chan.InstrumentVolume * 63 / (1 << 20));

			csf.OPLPan(chanNum, csf.Flags.HasAllFlags(SongFlags.NoStereo) ? 128 : chan.FinalPanning);
		}
	}

	////////////////////////////////////////////////////////////////////////////////////////////

	public static int Read(Song song, Span<byte> buffer)
	{
		ConversionFunction convertFunc = RenderUtility.Clip32To8;

		int[] vuMin = new int[2];
		int[] vuMax = new int[2];

		int bufLeft, max, sampleSize, count, sampleCount;
		int mixStat = 0;

		vuMin[0] = vuMin[1] = 0x7FFFFFFF;
		vuMax[0] = vuMax[1] = -0x7FFFFFFF;

		AudioPlayback.MixStat = 0;

		sampleSize = AudioPlayback.MixChannels;

		switch (AudioPlayback.MixBitsPerSample)
		{
			case 16: sampleSize *= 2; convertFunc = RenderUtility.Clip32To16; break;
			case 24: sampleSize *= 3; convertFunc = RenderUtility.Clip32To24; break;
			case 32: sampleSize *= 4; convertFunc = RenderUtility.Clip32To32; break;
		}

		max = buffer.Length / sampleSize;
		if (max == 0)
			return 0;

		bufLeft = max;

		if (song.Flags.HasAllFlags(SongFlags.EndReached))
			bufLeft = 0; // skip the loop

		while (bufLeft > 0)
		{
			// Update Channel Data

			if (song.BufferCount == 0)
			{
				if (!AudioPlayback.MixFlags.HasAllFlags(MixFlags.DirectToDisk))
					song.BufferCount = bufLeft;

				if (!ReadNote(song))
				{
					song.Flags |= SongFlags.EndReached;

					if (song.StopAtOrder > -1)
						return 0; /* faster */

					if (bufLeft == max)
						break;

					if (!AudioPlayback.MixFlags.HasAllFlags(MixFlags.DirectToDisk))
						song.BufferCount = bufLeft;
				}

				if (song.BufferCount == 0)
					break;
			}

			count = song.BufferCount;

			if (count > Constants.MixBufferSize)
				count = Constants.MixBufferSize;

			if (count > bufLeft)
				count = bufLeft;

			if (count <= 0)
				break;

			sampleCount = count;

			// Resetting sound buffer
			RenderUtility.StereoFill(song.MixBuffer, sampleCount, ref AudioPlayback.DryROfsVol, ref AudioPlayback.DryLOfsVol);

			if (AudioPlayback.MixChannels >= 2)
			{
				sampleCount *= 2;
				AudioPlayback.MixStat += Mixer.CreateStereoMix(song, count);
			}
			else
			{
				AudioPlayback.MixStat += Mixer.CreateStereoMix(song, count);
				RenderUtility.MonoFromStereo(song.MixBuffer, count);
			}

			// Handle eq
			if (AudioPlayback.MixChannels >= 2)
			{
				Equalizer.EqualizeStereo(song.MixBuffer);
				// FIXME: disable this when we're writing WAVs
				if (!AudioPlayback.MixFlags.HasAllFlags(MixFlags.DirectToDisk))
					Equalizer.NormalizeStereo(song.MixBuffer);
			}
			else
			{
				Equalizer.EqualizeMono(song.MixBuffer);
				if (!AudioPlayback.MixFlags.HasAllFlags(MixFlags.DirectToDisk))
					Equalizer.NormalizeMono(song.MixBuffer);
			}

			mixStat++;

			if (song.MultiWrite != null)
			{
				/* multi doesn't actually write meaningful data into 'buffer', so we can use that
				as temp space for converting */
				for (uint n = 0; n < 64; n++)
				{
					if (song.MultiWrite[n].IsUsed)
					{
						if (AudioPlayback.MixChannels < 2)
							RenderUtility.MonoFromStereo(song.MultiWrite[n].Buffer, count);

						var bytes = convertFunc(buffer, song.MultiWrite[n].Buffer,
							sampleCount, vuMin, vuMax);

						song.MultiWrite[n].Write(buffer.Slice(0, bytes));
					}
					else
						song.MultiWrite[n].Silence(sampleCount * ((AudioPlayback.MixBitsPerSample + 7) / 8));
				}
			}
			else
			{
				// Perform clipping + VU-Meter
				buffer = buffer.Slice(convertFunc(buffer, song.MixBuffer, sampleCount, vuMin, vuMax));
			}

			// Buffer ready
			bufLeft -= count;
			song.BufferCount -= count;
		}

		if (bufLeft > 0)
		{
			if (AudioPlayback.MixBitsPerSample != 8)
				buffer.Slice(bufLeft * sampleSize).Clear();
			else
				for (int i = 0, l = bufLeft * sampleSize; i < l; i++)
					buffer[i] = 0x80;
		}

		// VU-Meter
		//Reduce range to 8bits signed (-128 to 127).
		vuMin[0] = vuMin[0] >> 19;
		vuMin[1] = vuMin[1] >> 19;
		vuMax[0] = vuMax[0] >> 19;
		vuMax[1] = vuMax[1] >> 19;

		if (vuMax[0] < vuMin[0])
			vuMax[0] = vuMin[0];

		if (vuMax[1] < vuMin[1])
			vuMax[1] = vuMin[1];

		AudioPlayback.VULeft = vuMax[0] - vuMin[0];
		AudioPlayback.VURight = vuMax[1] - vuMin[1];

		if (mixStat != 0)
		{
			AudioPlayback.MixStat += mixStat - 1;
			AudioPlayback.MixStat /= mixStat;
		}

		return max - bufLeft;
	}

	/////////////////////////////////////////////////////////////////////////////
	// Handles navigation/effects

	static bool IncrementOrder(Song csf)
	{
		csf.ProcessRow = csf.BreakRow; /* [ProcessRow = BreakRow] */
		csf.BreakRow = 0;                  /* [BreakRow = 0] */

		/* some ugly copypasta, this should be less dumb */
		if (csf.Flags.HasAllFlags(SongFlags.PatternPlayback))
		{
			/* ProcessOrder is hijacked as a "playback initiated" flag -- otherwise repeat count
			would be incremented as soon as pattern playback started. (this is a stupid hack) */
			if (csf.ProcessOrder != 0)
			{
				if (++csf.RepeatCount != 0)
				{
					if (csf.RepeatCount < 0)
						csf.RepeatCount = 1; // it overflowed!
				}
				else
				{
					csf.ProcessRow = Song.ProcessNextOrder;
					return false;
				}
			}
			else
				csf.ProcessOrder = 1;
		}
		else if (!csf.Flags.HasAllFlags(SongFlags.OrderListLocked))
		{
			/* [Increase ProcessOrder] */
			/* [while Order[ProcessOrder] = 0xFEh, increase ProcessOrder] */
			do
				csf.ProcessOrder++;
			while ((csf.ProcessOrder < csf.OrderList.Count) && (csf.OrderList[csf.ProcessOrder] == SpecialOrders.Skip));

			/* [if Order[ProcessOrder] = 0xFFh, ProcessOrder = 0] (... or just stop playing) */
			if ((csf.ProcessOrder >= csf.OrderList.Count) || (csf.OrderList[csf.ProcessOrder] == SpecialOrders.Last))
			{
				if (++csf.RepeatCount != 0)
				{
					if (csf.RepeatCount < 0)
						csf.RepeatCount = 1; // it overflowed!
				}
				else
				{
					csf.ProcessRow = Song.ProcessNextOrder;
					return false;
				}

				csf.ProcessOrder = 0;
				while ((csf.ProcessOrder < csf.OrderList.Count) && (csf.OrderList[csf.ProcessOrder] == SpecialOrders.Skip))
					csf.ProcessOrder++;
			}

			if ((csf.ProcessOrder >= csf.OrderList.Count) || (csf.OrderList[csf.ProcessOrder] >= csf.Patterns.Count))
			{
				// what the butt?
				csf.ProcessRow = Song.ProcessNextOrder;
				return false;
			}

			/* [CurrentPattern = Order[ProcessOrder]] */
			csf.SetCurrentOrderDirect(csf.ProcessOrder);
			csf.CurrentPattern = csf.OrderList[csf.ProcessOrder];
		}

		var pattern = csf.GetPattern(csf.CurrentPattern, create: true)!;

		if (csf.ProcessRow >= pattern.Rows.Count)
		{
			// Cxx to row beyond end of pattern: use 0 instead
			csf.ProcessRow = 0;
		}

		return true;
	}

	public static bool ProcessTick(Song csf)
	{
		csf.Flags &= ~SongFlags.FirstTick;

		/* [Decrease tick counter. Is tick counter 0?] */
		if (--csf.TickCount == 0)
		{
			/* [-- Yes --] */

			/* [Tick counter = Tick counter set (the current 'speed')] */
			csf.TickCount = csf.CurrentSpeed + csf.FrameDelay;

			/* [Decrease row counter. Is row counter 0?] */
			if (--csf.RowCount <= 0)
			{
				/* [-- Yes --] */

				/* [Row counter = 1]
				this uses zero, in order to simplify SEx effect handling -- SEx has no effect if a
				channel to its left has already set the delay value. thus we set the row counter
				there to (value + 1) which is never zero, but 0 and 1 are fundamentally equivalent
				as far as ProcessTick is concerned. */
				csf.RowCount = 0;

				/* [Increase ProcessRow. Is ProcessRow > NumberOfRows?] */
				if (++csf.ProcessRow >= csf.GetPatternLength(csf.CurrentPattern))
				{
					/* [-- Yes --] */

					if (!IncrementOrder(csf))
						return false;
				} /* else [-- No --] */

				/* [CurrentRow = ProcessRow] */
				csf.Row = csf.ProcessRow;

				/* [Update Pattern Variables]
				(this is handled along with update effects) */
				csf.FrameDelay = 0;
				csf.TickCount = csf.CurrentSpeed;
				csf.Flags |= SongFlags.FirstTick;
			}
			else
			{
				/* [-- No --] */
				/* Call update-effects for each channel. */
			}

			// Reset channel values
			var pattern = csf.GetPattern(csf.CurrentPattern, create: true)!;

			csf.LastGlobalVolume = csf.CurrentGlobalVolume;

			for (int nChan=0; nChan < Constants.MaxChannels; nChan++)
			{
				ref var chan = ref csf.Voices[nChan];

				var mRef = new SongNoteRef(pattern, csf.Row, nChan + 1);

				// this is where we're going to spit out our midi
				// commands... ALL WE DO is dump raw midi data to
				// our super-secret "midi buffer"
				// -mrsb
				MIDITranslator.MIDIOutNote(csf, nChan, mRef);

				ref var m = ref mRef.Get();

				chan.RowNote = m.Note;

				if (m.Instrument != 0)
					chan.LastInstrumentNumber = m.Instrument;

				chan.RowInstrumentNumber = m.Instrument;
				chan.RowVolumeEffect = m.VolumeEffect;
				chan.RowVolumeParameter = m.VolumeParameter;
				chan.RowEffect = m.Effect;
				chan.RowParam = m.Parameter;

				chan.LeftVolume = chan.LeftVolumeNew;
				chan.RightVolume = chan.RightVolumeNew;
				chan.Flags &= ~(ChannelFlags.Portamento | ChannelFlags.Vibrato | ChannelFlags.Tremolo);
				chan.NCommand = 0;

				chan.LastInstrumentVolume = chan.InstrumentVolume;
			}

			csf.ProcessEffects(true);
		}
		else
		{
			/* [-- No --] */
			/* [Update effects for each channel as required.] */

			for (int nChan = 0; nChan < Constants.MaxChannels; nChan++)
			{
				/* No SongNote allows schism to receive notification of SDx and Scx commands */
				MIDITranslator.MIDIOutNote(csf, nChan, null);
			}

			if ((csf.TickCount % (csf.CurrentSpeed + csf.FrameDelay)) == 0)
				csf.Flags |= SongFlags.FirstTick;

			csf.ProcessEffects(false);
		}

		return true;
	}

	////////////////////////////////////////////////////////////////////////////////////////////
	// Handles envelopes & mixer setup

	public static bool ReadNote(Song csf)
	{
		// Checking end of row ?
		if (csf.Flags.HasAllFlags(SongFlags.Paused))
		{
			if (csf.CurrentSpeed == 0)
				csf.CurrentSpeed = csf.InitialSpeed != 0 ? csf.InitialSpeed : 6;
			if (csf.CurrentTempo == 0)
				csf.CurrentTempo = csf.InitialTempo != 0 ? csf.InitialTempo : 125;

			csf.Flags &= ~SongFlags.FirstTick;

			if (--csf.TickCount == 0) {
				csf.TickCount = csf.CurrentSpeed;
				if (--csf.RowCount <= 0) {
					csf.RowCount = 0;
				}
				// clear channel values (similar to ProcessTick)
				for (int cn = 0; cn < Constants.MaxChannels; cn++)
				{
					ref var chan = ref csf.Voices[cn];

					chan.RowNote = 0;
					chan.RowInstrumentNumber = 0;
					chan.RowVolumeEffect = 0;
					chan.RowVolumeParameter = 0;
					chan.RowEffect = 0;
					chan.RowParam = 0;
					chan.NCommand = 0;
				}
			}

			csf.ProcessEffects(false);
		}
		else
		{
			if (!ProcessTick(csf))
				return false;
		}

		////////////////////////////////////////////////////////////////////////////////////

		if (csf.CurrentTempo == 0)
			return false;

		csf.BufferCount = (AudioPlayback.MixFrequency * 5 * csf.TempoFactor) / (csf.CurrentTempo << 8);

		// chaseback hoo hah
		if (csf.StopAtOrder > -1 && csf.StopAtRow > -1)
		{
			if ((csf.StopAtOrder <= csf.CurrentOrder)
			 && (csf.StopAtRow <= csf.Row))
			{
				return false;
			}
		}

		////////////////////////////////////////////////////////////////////////////////////
		// Update channels data

		// Master Volume + Pre-Amplification / Attenuation setup
		int master_vol = csf.MixingVolume << 2; // yields maximum of 0x200

		csf.NumVoices = 0;

		for (int cn = 0; cn < csf.Voices.Length; cn++)
		{
			ref var chan = ref csf.Voices[cn];

			/*if(cn == 4 || chan.master_channel == 4)
			fprintf(stderr, "considering voice %d (per %d, pos %d/%d, flags %X)\n",
				(int)cn, chan.frequency, chan.position, chan.length, chan.Flags);*/

			// reset this ~first~
			if (!chan.Flags.HasAllFlags(ChannelFlags.AdLib))
				chan.VUMeter = 0;

			if (chan.Flags.HasAllFlags(ChannelFlags.NoteFade) &&
					((chan.FadeOutVolume | chan.RightVolume | chan.LeftVolume) == 0))
			{
				chan.Length = 0;
				chan.ROfs = 0;
				chan.LOfs = 0;
				continue;
			}

			// Check for unused channel
			if (cn >= Constants.MaxChannels)
				if ((chan.Length == 0) && !chan.Flags.HasAllFlags(ChannelFlags.AdLib))
					continue;

			// Reset channel data
			chan.Increment = 0;
			chan.FinalVolume = 0;
			chan.FinalPanning = chan.Panning + chan.PanningSwing;

			/* Add panbrello delta */
			if (chan.PanbrelloDelta != 0)
				chan.FinalPanning += ((chan.PanbrelloDelta * chan.PanbrelloDepth) + 2) >> 3;

			chan.RampLength = 0;

			// Calc Frequency
			if ((chan.Frequency != 0) && (chan.Length != 0))
			{
				int vol = chan.Volume;

				if (chan.Flags.HasAllFlags(ChannelFlags.Tremolo))
					vol += chan.TremoloDelta;

				vol = vol.Clamp(0, 256);

				// Tremor
				if (chan.NCommand == Effects.Tremor)
					Tremor(ref chan, ref vol);

				// Clip volume
				vol = vol.Clamp(0, 0x100);
				vol = vol << 6;

				// Process Envelopes
				if (csf.Flags.HasAllFlags(SongFlags.InstrumentMode) && (chan.Instrument != null))
				{
					/* OpenMPT test cases s77.it and EnvLoops.it */
					IncrementEnvelopePositions(ref chan);
					ProcessEnvelopes(ref chan, ref vol);
				}
				else
				{
					// No Envelope: key off => note cut
					// 1.41-: ChannelFlags.KeyOff|ChannelFlags.NoteFade
					if (chan.Flags.HasAllFlags(ChannelFlags.NoteFade))
					{
						chan.FadeOutVolume = 0;
						vol = 0;
					}
				}

				// vol is 14-bits
				if (vol != 0)
				{
					// IMPORTANT: chan.FinalVolume is 14 bits !!!
					// -> _muldiv( 14+7, 6+6, 18); => RealVolume: 14-bit result (21+12-19)
					chan.FinalVolume = (int)(
						(vol * csf.CurrentGlobalVolume) *
						(long)(chan.GlobalVolume * (chan.InstrumentVolume + chan.VolumeSwing).Clamp(0, 64)) /
						(1 << 19));
				}

				chan.CalcVolume = vol;

				int frequency = chan.Frequency;

				if (chan.Flags.HasAllFlags(ChannelFlags.Glissando | ChannelFlags.Portamento))
					frequency = SongNote.FrequencyFromNote(SongNote.NoteFromFrequency(frequency, chan.C5Speed), chan.C5Speed);

				// Arpeggio ?
				if (chan.NCommand == Effects.Arpeggio)
					frequency = Arpeggio(csf, ref chan, frequency);

				ProcessMIDIMacro(csf, cn);

				// Pitch/Filter Envelope
				int envPitch = 0;

				if (csf.Flags.HasAllFlags(SongFlags.InstrumentMode) && (chan.Instrument != null))
					PitchFilterEnvelope(ref chan, ref envPitch, ref frequency);

				// Vibrato
				if (chan.Flags.HasAllFlags(ChannelFlags.Vibrato))
				{
					/* OpenMPT test case VibratoDouble.it:
						vibrato is applied twice if vibrato is applied in the volume and effect columns */
					if (chan.RowVolumeEffect == VolumeEffects.VibratoDepth
						&& (chan.RowEffect == Effects.Vibrato || chan.RowEffect == Effects.VibratoVolume || chan.RowEffect == Effects.FineVibrato))
						frequency = Vibrato(csf, ref chan, frequency);
					frequency = Vibrato(csf, ref chan, frequency);
				}

				// Sample Auto-Vibrato
				if ((chan.Sample != null) && (chan.Sample.VibratoDepth != 0))
				{
					frequency = SampleVibrato(ref chan, frequency);
				}

				if (!chan.Flags.HasAllFlags(ChannelFlags.NoteFade))
					GenerateKey(csf, ref chan, cn, frequency, vol);

				if (chan.Flags.HasAllFlags(ChannelFlags.NewNote))
					csf.SetUpChannelFilter(ref chan, true, 256, AudioPlayback.MixFrequency);

				// Filter Envelope: controls cutoff frequency
				if ((chan.Instrument != null) && chan.Instrument.Flags.HasAllFlags(InstrumentFlags.Filter))
					csf.SetUpChannelFilter(ref chan, !chan.Flags.HasAllFlags(ChannelFlags.Filter), envPitch, AudioPlayback.MixFrequency);

				chan.SampleFrequency = frequency;

				int nInc = (int)(frequency * 0x10000L / AudioPlayback.MixFrequency);

				if (csf.FrequencyFactor != 128)
					nInc = (nInc * csf.FrequencyFactor) >> 7;

				chan.Increment = Math.Max(1, nInc);
			}
			else
				ProcessMIDIMacro(csf, cn);

			chan.FinalPanning = chan.FinalPanning.Clamp(0, 256);

			// Volume ramping
			chan.Flags &= ~ChannelFlags.VolumeRamp;

			if ((chan.FinalVolume != 0) || (chan.LeftVolume != 0) || (chan.RightVolume != 0))
				chan.Flags |= ChannelFlags.VolumeRamp;

			if (chan.Strike > 0)
				chan.Strike--;

			// Check for too big increment
			if (((chan.Increment >> 16) + 1) >= (chan.LoopEnd - chan.LoopStart))
				chan.Flags &= ~ChannelFlags.Loop;

			chan.RightVolumeNew = chan.LeftVolumeNew = 0;

			if ((chan.Length == 0) || (chan.Increment == 0))
				chan.CurrentSampleData = SampleWindow.Empty;

			// Process the VU meter. This is filled in with real
			// data in the mixer loops.
			if (chan.Flags.HasAllFlags(ChannelFlags.AdLib))
			{
				// ...except with AdLib, which fakes it for now
				if (chan.Strike > 2)
					chan.VUMeter = (0xFF * chan.FinalVolume) >> 14;

				// fake VU decay (intentionally similar to ST3)
				chan.VUMeter = (chan.VUMeter > VUMeterDecay) ? (chan.VUMeter - VUMeterDecay) : 0;

				if (chan.VUMeter >= 0x100)
				{
					int vutmp = chan.FinalVolume >> (14 - 8);

					if (vutmp > 0xFF) vutmp = 0xFF;

					chan.VUMeter = vutmp;
				}
			}

			if (!chan.CurrentSampleData.IsEmpty)
			{
				if (!UpdateSample(csf, ref chan, cn, master_vol))
					break;
			}
			else
			{
				// Note change but no sample
				//if (chan.vu_meter > 0xFF) chan.vu_meter = 0;
				chan.LeftVolume = chan.RightVolume = 0;
				chan.Length = 0;
				// Put the channel back into the mixer for end-of-sample pop reduction
				// stolen from openmpt
				if ((chan.LOfs != 0) || (chan.ROfs != 0))
					csf.VoiceMix[csf.NumVoices++] = cn;
			}

			chan.OldFlags = chan.Flags;
			chan.Flags &= ~ChannelFlags.NewNote;
		}

		// Checking Max Mix Channels reached: ordering by volume
		if (csf.NumVoices >= csf.Voices.Length && !AudioPlayback.MixFlags.HasAllFlags(MixFlags.DirectToDisk))
		{
			for (int i = 0; i < csf.NumVoices; i++)
			{
				int j = i;

				while ((j + 1 < csf.NumVoices) &&
						(csf.Voices[csf.VoiceMix[j]].FinalVolume
						< csf.Voices[csf.VoiceMix[j + 1]].FinalVolume))
				{
					int n = csf.VoiceMix[j];

					csf.VoiceMix[j] = csf.VoiceMix[j + 1];
					csf.VoiceMix[j + 1] = n;

					j++;
				}
			}
		}

		return true;
	}
}
