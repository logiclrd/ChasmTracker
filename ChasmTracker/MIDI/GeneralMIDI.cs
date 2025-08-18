using System;
using System.Diagnostics.CodeAnalysis;

namespace ChasmTracker.MIDI;

using ChasmTracker.Playback;
using ChasmTracker.Songs;
using ChasmTracker.Utility;

/* This is a wrapper which converts S3M style thinking
 * into MIDI style thinking. */

public static class GeneralMIDI
{
	public const int LinearMIDIVolume = 1;
	public const int PitchBendCentre = 0x2000;

	// The range of bending equivalent to 1 semitone.
	// 0x2000 is the value used in TiMiDity++.
	// In this module, we prefer a full range of octave, to support a reasonable
	// range of pitch-bends used in tracker modules, and we reprogram the MIDI
	// synthesizer to support that range. So we specify it as such:
	public const int SemitoneBendDepth = 0x2000 / 12;

	enum ChannelHandlingMode
	{
		AlwaysHonour = 0,
		TryHonour = 1,
		Ignore = 2,
	}

	static ChannelHandlingMode PreferredChannelHandlingMode = ChannelHandlingMode.AlwaysHonour;

	/* GENERAL MIDI (GM) COMMANDS:
	8x       1000xxxx     nn vv         Note off (key is released)
							nn=note number
							vv=velocity

	9x       1001xxxx     nn vv         Note on (key is pressed)
							nn=note number
							vv=velocity

	Ax       1010xxxx     nn vv         Key after-touch
							nn=note number
							vv=velocity

	Bx       1011xxxx     cc vv         Control Change
							cc=controller number
							vv=new value

	Cx       1100xxxx     pp            Program (patch) change
							pp=new program number

	Dx       1101xxxx     cc            Channel after-touch
							cc=channel number

	Ex       1110xxxx     bb tt         Pitch wheel change (2000h is normal
								or no change)
							bb=bottom (least sig) 7 bits of value
							tt=top (most sig) 7 bits of value

	About the controllers... In AWE32 they are:
			0=Bank select               7=Master volume     11=Expression(volume?)
			1=Modulation Wheel(Vibrato)10=Pan Position      64=Sustain Pedal
			6=Data Entry MSB           38=Data Entry LSB    91=Effects Depth(Reverb)
		120=All Sound Off           123=All Notes Off     93=Chorus Depth
		100=RPN # LSB       101=RPN # MSB
		 98=NRPN # LSB       99=NRPN # MSB

			1=Vibrato, 121=reset vibrato,bend

			To set RPNs (registered parameters):
				control 101 <- param number MSB
				control 100 <- param number LSB
				control   6 <- value number MSB
			 <control  38 <- value number LSB> optional
			For NRPNs, the procedure is the same, but you use 98,99 instead of 100,101.

				 param 0 = pitch bend sensitivity
				 param 1 = finetuning
				 param 2 = coarse tuning
				 param 3 = tuning program select
				 param 4 = tuning bank select
				 param 0x4080 = reset (value omitted)

			References:
				 - SoundBlaster AWE32 documentation
				 - http://www.philrees.co.uk/nrpnq.htm
	*/

	static void MPU_SendCommand(Song csf, Span<byte> buf, int c)
	{
		if (buf.Length == 0)
			return;

		csf.MIDISend(buf, c, fake: false);
	}

	[ThreadStatic]
	static byte[]? s_buffer;

	[MemberNotNull(nameof(s_buffer))]
	static void EnsureBuffer(int size)
	{
		if ((s_buffer == null) || (s_buffer.Length < size))
			s_buffer = new byte[size * 2];
	}

	static void MPU_Ctrl(Song csf, int c, int i, int v)
	{
		if (!Status.Flags.HasAllFlags(StatusFlags.MIDILikeTracker))
			return;

		EnsureBuffer(3);

		s_buffer[0] = (byte)(0xB0 + c);
		s_buffer[1] = (byte)i;
		s_buffer[2] = (byte)v;

		MPU_SendCommand(csf, s_buffer.Slice(0, 3), c);
	}


	static void MPU_Patch(Song csf, int c, int p)
	{
		if (!Status.Flags.HasAllFlags(StatusFlags.MIDILikeTracker))
			return;

		EnsureBuffer(2);

		s_buffer[0] = (byte)(0xC0 + c);
		s_buffer[1] = (byte)p;

		MPU_SendCommand(csf, s_buffer.Slice(0, 2), c);
	}


	static void MPU_Bend(Song csf, int c, int w)
	{
		if (!Status.Flags.HasAllFlags(StatusFlags.MIDILikeTracker))
			return;

		EnsureBuffer(3);

		s_buffer[0] = (byte)(0xE0 + c);
		s_buffer[1] = (byte)(w & 127);
		s_buffer[2] = (byte)(w >> 7);

		MPU_SendCommand(csf, s_buffer.Slice(0, 3), c);
	}


	static void MPU_NoteOn(Song csf, int c, int k, int v)
	{
		if (!Status.Flags.HasAllFlags(StatusFlags.MIDILikeTracker))
			return;

		EnsureBuffer(3);

		s_buffer[0] = (byte)(0x90 + c);
		s_buffer[1] = (byte)k;
		s_buffer[2] = (byte)v;

		MPU_SendCommand(csf, s_buffer.Slice(0, 3), c);
	}


	static void MPU_NoteOff(Song csf, int c, int k, int v)
	{
		if (!Status.Flags.HasAllFlags(StatusFlags.MIDILikeTracker))
			return;

		if (csf.MIDIRunningStatus == 0x90 + c)
		{
			// send a zero-velocity keyoff instead for optimization
			MPU_NoteOn(csf, c, k, 0);
		}
		else
		{
			EnsureBuffer(3);

			s_buffer[0] = (byte)(0x80 + c);
			s_buffer[1] = (byte)k;
			s_buffer[2] = (byte)v;

			MPU_SendCommand(csf, s_buffer.Slice(0, 3), c);
		}
	}


	static void MPU_SendPN(Song csf, int ch,
						int portindex,
						int param, uint valuehi, uint valuelo)
	{
		MPU_Ctrl(csf, ch, portindex+1, param>>7);
		MPU_Ctrl(csf, ch, portindex+0, param & 0x80);

		if (param != 0x4080)
		{
			MPU_Ctrl(csf, ch, 6, unchecked((int)valuehi));

			if (valuelo != 0)
				MPU_Ctrl(csf, ch, 38, unchecked((int)valuelo));
		}
	}

	static void MPU_SendNRPN(Song csf, int ch, int param, uint valuehi, uint valuelo)
		=> MPU_SendPN(csf, ch, 98, param, valuehi, valuelo);
	static void MPU_SendRPN(Song csf, int ch, int param, uint valuehi, uint valuelo)
		=> MPU_SendPN(csf, ch, 100, param, valuehi, valuelo);

	static void MPU_ResetPN(Song csf, int ch)
		=> MPU_SendRPN(csf, ch, 0x4080, 0, 0);

	static void MSI_SetVolume(Song csf, SongMIDIState msi, int c, uint newvol)
	{
		if (msi.Volume != newvol)
		{
			msi.Volume = (byte)newvol;
			MPU_Ctrl(csf, c, 7, (int)newvol);
		}
	}


	static void MSI_SetPatchAndBank(Song csf, SongMIDIState msi, int c, int p, int b)
	{
		if (msi.Bank != b) {
			msi.Bank = (byte)b;
			MPU_Ctrl(csf, c, 0, b);
		}

		if (msi.Patch != p) {
			msi.Patch = (byte)p;
			MPU_Patch(csf, c, p);
		}
	}


	static void MSI_SetPitchBend(Song csf, SongMIDIState msi, int c, int value)
	{
		if (msi.Bend != value)
		{
			msi.Bend = value;
			MPU_Bend(csf, c, value);
		}
	}


	static void MSI_SetPanning(Song csf, SongMIDIState msi, int c, int value)
	{
		if (msi.Panning != value)
		{
			msi.Panning = (sbyte)value;
			MPU_Ctrl(csf, c, 10, unchecked((byte)(value + 128)) / 2);
		}
	}

	static byte Volume(byte vol) // Converts the volume
	{
		/* Converts volume in range 0..127 to range 0..127 with clamping */
		return Math.Min(vol, (byte)127);
	}


	static sbyte AllocateMelodyChannel(Song csf, int c, int patch, int bank, int key, uint preferredChannelMask)
	{
		/* Returns a MIDI channel number on
		* which this key can be played safely.
		*
		* Things that matter:
		*
		*  -4      The channel has a different patch selected
		*  -6      The channel has a different bank selected
		*  -9      The channel already has the same key
		*  +1      The channel number corresponds to c
		*  +2      The channel has no notes playing
		*  -999    The channel number is 9 (percussion-only channel)
		*
		* Channel with biggest score is selected.
		*
		*/
		bool[] badChannels = new bool[Constants.MaxMIDIChannels];  // channels having the same key playing
		bool[] usedChannels = new bool[Constants.MaxMIDIChannels]; // channels having something playing

		for (uint a = 0; a < Constants.MaxVoices; ++a)
		{
			if (csf.MIDIS3MChannels[a].IsActive &&
					!csf.MIDIS3MChannels[a].IsPercussion)
			{
				//Console.Error.WriteLine("S3M[{0}] active at {1}\n", a, csf.MIDIS3MChannels[a].Channel);
				usedChannels[csf.MIDIS3MChannels[a].Channel] = true; // channel is active

				if (csf.MIDIS3MChannels[a].Note == key)
					badChannels[csf.MIDIS3MChannels[a].Channel] = true; // ...with the same key
			}
		}

		sbyte bestMIDIChannel = unchecked((sbyte)(c % Constants.MaxMIDIChannels));
		int bestScore = -999;

		for (sbyte mc = 0; mc < Constants.MaxMIDIChannels; ++mc)
		{
			if (mc == 9)
				continue; // percussion channel is never chosen for melody.

			int score = 0;

			if (PreferredChannelHandlingMode != ChannelHandlingMode.TryHonour &&
					csf.MIDIChannels[mc].KnowsSomething)
			{
				if (csf.MIDIChannels[mc].Patch != patch) score -= 4; // different patch
				if (csf.MIDIChannels[mc].Bank  !=  bank) score -= 6; // different bank
			}

			if (PreferredChannelHandlingMode == ChannelHandlingMode.TryHonour)
			{
				if (preferredChannelMask.HasBitSet(1 << mc))
					score += 1; // same channel number
			}
			else if (PreferredChannelHandlingMode == ChannelHandlingMode.AlwaysHonour)
			{
				// disallow channels that are not allowed
				if (preferredChannelMask >= 0x10000) {
					if (mc != c % Constants.MaxMIDIChannels)
						continue;
				}
				else if (!preferredChannelMask.HasBitSet(1 << mc))
					continue;
			}
			else {
				if (c == mc)
					score += 1; // same channel number
			}

			if (badChannels[mc])
				score -= 9; // has same key on

			if (!usedChannels[mc])
				score += 2; // channel is unused

			//Console.Error.WriteLine("score {0} for channel {1}", score, mc);
			if (score > bestScore)
			{
				bestScore = score;
				bestMIDIChannel = mc;
			}
		}

		//Console.Error.WriteLine("BEST SCORE {0} FOR CHANNEL {1}\n", bestScore, bestMIDIChannel);
		return bestMIDIChannel;
	}


	public static void Patch(Song csf, int c, byte p, uint preferredChannelMask)
	{
		if (c < 0 || ((uint) c) >= Constants.MaxVoices)
			return;

		csf.MIDIS3MChannels[c].Patch         = p; // No actual data is sent.
		csf.MIDIS3MChannels[c].PreferredChannelMask = preferredChannelMask;
	}


	public static void Bank(Song csf, int c, byte b)
	{
		if (c < 0 || ((uint) c) >= Constants.MaxVoices)
			return;

		csf.MIDIS3MChannels[c].Bank = b; // No actual data is sent yet.
	}


	public static void Touch(Song csf, int c, byte vol)
	{
		if ((c < 0) || (c >= Constants.MaxVoices))
			return;

		/* This function must only be called when
		* a key has been played on the channel. */
		if (!csf.MIDIS3MChannels[c].IsActive)
			return;

		int mc = csf.MIDIS3MChannels[c].Channel;
		MSI_SetVolume(csf, csf.MIDIChannels[mc], mc, Volume(vol));
	}


	public static void KeyOn(Song csf, int c, byte key, byte vol)
	{
		if ((c < 0) || (c >= Constants.MaxVoices))
			return;

		KeyOff(csf, c); // Ensure the previous key on this channel is off.

		if (csf.MIDIS3MChannels[c].IsActive)
			return; // be sure the channel is deactivated.

#if GM_DEBUG
		Console.Error.WriteLine("GM_KeyOn({0}, {1}, {2})", c, key, vol);
#endif

		if (csf.MIDIS3MChannels[c].IsPercussion)
		{
			// Percussion always uses channel 9.
			byte percu = key;

			if (csf.MIDIS3MChannels[c].Patch.HasBitSet(0x80))
				percu = (byte)(csf.MIDIS3MChannels[c].Patch - 128);

			int mc = csf.MIDIS3MChannels[c].Channel = 9;
			// Percussion can have different banks too
			MSI_SetPatchAndBank(csf, csf.MIDIChannels[mc], mc, csf.MIDIS3MChannels[c].Patch, csf.MIDIS3MChannels[c].Bank);
			MSI_SetPanning(csf, csf.MIDIChannels[mc], mc, csf.MIDIS3MChannels[c].Panning);
			MSI_SetVolume(csf, csf.MIDIChannels[mc], mc, Volume(vol));
			csf.MIDIS3MChannels[c].Note = key;
			MPU_NoteOn(csf, mc, csf.MIDIS3MChannels[c].Note = percu, 127);
		}
		else
		{
			// Allocate a MIDI channel for this key.
			// Note: If you need to transpone the key, do it before allocating the channel.

			int mc = csf.MIDIS3MChannels[c].Channel = AllocateMelodyChannel(
				csf, c, csf.MIDIS3MChannels[c].Patch, csf.MIDIS3MChannels[c].Bank,
				key, csf.MIDIS3MChannels[c].PreferredChannelMask);

			MSI_SetPatchAndBank(csf, csf.MIDIChannels[mc], mc, csf.MIDIS3MChannels[c].Patch, csf.MIDIS3MChannels[c].Bank);
			MSI_SetVolume(csf, csf.MIDIChannels[mc], mc, Volume(vol));

			csf.MIDIS3MChannels[c].Note = key;

			MPU_NoteOn(csf, mc, csf.MIDIS3MChannels[c].Note, 127);
			MSI_SetPanning(csf, csf.MIDIChannels[mc], mc, csf.MIDIS3MChannels[c].Panning);
		}
	}

	public static void KeyOff(Song csf, int c)
	{
		if ((c < 0) || (c >= Constants.MaxVoices))
			return;

		if (!csf.MIDIS3MChannels[c].IsActive)
			return; // nothing to do

#if GM_DEBUG
		Console.Error.WriteLine("GM_KeyOff({0})", c);
#endif

		int mc = csf.MIDIS3MChannels[c].Channel;

		MPU_NoteOff(csf, mc, csf.MIDIS3MChannels[c].Note, 0);
		csf.MIDIS3MChannels[c].Channel = -1;
		csf.MIDIS3MChannels[c].Note = 0;
		csf.MIDIS3MChannels[c].Panning  = 0;
		// Don't reset the pitch bend, it will make sustains sound bad
	}


	public static void Bend(Song csf, int c, int count)
	{
		if ((c < 0) || (c >= Constants.MaxVoices))
			return;

		/* I hope nobody tries to bend hi-hat or something like that :-) */
		/* 1998-10-03 01:50 Apparently that can happen too...
			For example in the last pattern of urq.mod there's
			a hit of a heavy plate, which is followed by a J0A
			0.5 seconds thereafter for the same channel.
			Unfortunately MIDI cannot do that. Drum plate
			sizes can rarely be adjusted while playing. -Bisqwit
			However, we don't stop anyone from trying...
		*/

		if (csf.MIDIS3MChannels[c].IsActive)
		{
			int mc = csf.MIDIS3MChannels[c].Channel;
			MSI_SetPitchBend(csf, csf.MIDIChannels[mc], mc, count);
		}
	}


	public static void Reset(Song csf, bool quitting)
	{
#if GM_DEBUG
		csf.MIDIResetting = true;
#endif
		//Console.Error.WriteLine("GeneralMIDI.Reset");

		for (int a = 0; a < Constants.MaxVoices; a++)
		{
			KeyOff(csf, a);
			//csf.MIDIS3MChannels[a].Patch = csf.MIDIS3MChannels[a].Bank = 0;
			//csf.MIDIS3MChannels[a].Panning = 0;
			csf.MIDIS3MChannels[a].Reset();
		}

		// How many semitones does it take to screw in the full 0x4000 bending range of lightbulbs?
		// We scale the number by 128, because the RPN allows for finetuning.
		int nSemitonesTimes128 = 128 * 0x2000 / SemitoneBendDepth;

		if (quitting)
		{
			// When quitting, we reprogram the pitch bend sensitivity into
			// the range of 1 semitone (TiMiDity++'s default, which is
			// probably a default on other devices as well), instead of
			// what we preferred for IT playback.
			nSemitonesTimes128 = 128;
		}

		for (int a = 0; a < Constants.MaxMIDIChannels; a++)
		{
			csf.MIDIChannels[a] = new SongMIDIState();

			// XXX
			// XXX Porting note:
			// XXX This might go wrong because the midi struct is already reset
			// XXX  by the constructor in the C++ version.
			// XXX
			MPU_Ctrl(csf, a, 120,  0);   // turn off all sounds
			MPU_Ctrl(csf, a, 123,  0);   // turn off all notes
			MPU_Ctrl(csf, a, 121, 0);    // reset vibrato, bend
			MSI_SetPanning(csf, csf.MIDIChannels[a], a, 0);           // reset pan position
			MSI_SetVolume(csf, csf.MIDIChannels[a], a, 127);      // set channel volume
			MSI_SetPitchBend(csf, csf.MIDIChannels[a], a, PitchBendCentre); // reset pitch bends

			csf.MIDIChannels[a].Reset();

			// Reprogram the pitch bending sensitivity to our desired depth.
			MPU_SendRPN(csf, a, 0, (uint)(nSemitonesTimes128 / 128),
					(uint)(nSemitonesTimes128 % 128));

			MPU_ResetPN(csf, a);
		}

#if GM_DEBUG
		csf.MIDIResetting = 0;
		Console.Error.WriteLine("-------------- GM_Reset completed --------------s");
#endif
	}


	public static void DPatch(Song csf, int ch, byte GM, byte bank, int preferredChannelMask)
	{
#if GM_DEBUG
		Console.Error.WriteLine("GM_DPatch({0}, {1:X2} @ {2})", ch, GM, bank);
#endif

		if (ch < 0 || ((uint)ch) >= Constants.MaxVoices)
			return;

		Bank(csf, ch, bank);
		Patch(csf, ch, GM, unchecked((uint)preferredChannelMask));
	}


	public static void Pan(Song csf, int c, sbyte val)
	{
		//Console.Error.WriteLine("GM_Pan({0},{1})\n", c,val);
		if (c < 0 || ((uint)c) >= Constants.MaxVoices)
			return;

		csf.MIDIS3MChannels[c].Panning = val;

		// If a note is playing, effect immediately.
		if (csf.MIDIS3MChannels[c].IsActive)
		{
			int mc = csf.MIDIS3MChannels[c].Channel;
			MSI_SetPanning(csf, csf.MIDIChannels[mc], mc, val);
		}
	}




	public static void SetFrequencyAndVolume(Song csf, int c, int Hertz, byte vol, MIDIBendMode bendMode, bool keyOff)
	{
#if GM_DEBUG
		Console.Error.WriteLine("GM_SetFrequencyAndVolume({0},{1},{2})", c,Hertz,vol);
#endif
		if (c < 0 || ((uint)c) >= Constants.MaxVoices)
			return;

		/*
		Figure out the note and bending corresponding to this Hertz reading.

		TiMiDity++ calculates its frequencies this way (equal temperament):
			freq(0<=i<128) := 440 * pow(2.0, (i - 69) / 12.0)
			bend_fine(0<=i<256) := pow(2.0, i/12.0/256)
			bend_coarse(0<=i<128) := pow(2.0, i/12.0)

		I suppose we can do the mathematical route.  -Bisqwit
					hertz = 440*pow(2, (midinote-69)/12)
				Maxima gives us (solve+expand):
					midinote = 12 * log(hertz/440) / log(2) + 69
				In other words:
					midinote = 12 * log2(hertz/440) + 69
				Or:
					midinote = 12 * log2(hertz/55) + 33 (but I prefer the above for clarity)

					(55 and 33 are related to 440 and 69 the following way:
						log2(440) = ~8.7
						440/8   = 55
						log2(8) = 3
						12 * 3  = 36
						69-36   = 33.
					I guess Maxima's expression preserves more floating
					point accuracy, but given the range of the numbers
					we work here with, that's hardly an issue.)
		*/
		double midinote = 69 + 12.0 * Math.Log2(Hertz / 440.0);

		// Reduce by a couple of octaves... Apparently the hertz
		// value that comes from SchismTracker is upscaled by some 2^5.
		midinote -= 12*5;

		byte note = csf.MIDIS3MChannels[c].Note; // what's playing on the channel right now?

		bool newNote = !csf.MIDIS3MChannels[c].IsActive;

		if (newNote && !keyOff)
		{
			// If the note is not active, activate it first.
			// Choose the nearest note to Hertz.
			note = (byte)(midinote + 0.5);

			// If we are expecting a bend exclusively in either direction,
			// prepare to utilize the full extent of available pitch bending.
			if (bendMode == MIDIBendMode.Down) note += (int)(0x2000 / SemitoneBendDepth);
			if (bendMode == MIDIBendMode.Up)   note -= (int)(0x2000 / SemitoneBendDepth);

			if (note < 1) note = 1;
			if (note > 127) note = 127;
			KeyOn(csf, c, note, vol);
		}

		if (csf.MIDIS3MChannels[c].IsPercussion) // give us a break, don't bend percussive instruments
		{
			double notediff = midinote-note; // The difference is our bend value
			int bend = (int)(notediff * SemitoneBendDepth) + PitchBendCentre;

			// Because the log2 calculation does not always give pure notes,
			// and in fact, gives a lot of variation, we reduce the bending
			// precision to 100 cents. This is accurate enough for almost
			// all purposes, but will significantly reduce the bend event load.
			//const int bend_artificial_inaccuracy = SemitoneBendDepth / 100;
			//bend = (bend / bend_artificial_inaccuracy) * bend_artificial_inaccuracy;

			// Clamp the bending value so that we won't break the protocol
			if(bend < 0) bend = 0;
			if(bend > 0x3FFF) bend = 0x3FFF;

			Bend(csf, c, bend);
		}

		if (vol < 0) vol = 0;
		else if (vol > 127) vol = 127;

		//if (!newNote)
		Touch(csf, c, vol);
	}

	public static void SendSongStartCode(Song csf)    { EnsureBuffer(1); s_buffer[0] = 0xFA; MPU_SendCommand(csf, s_buffer.Slice(0, 1), 0); csf.MIDILastSongCounter = 0; }
	public static void SendSongStopCode(Song csf)     { EnsureBuffer(1); s_buffer[0] = 0xFC; MPU_SendCommand(csf, s_buffer.Slice(0, 1), 0); csf.MIDILastSongCounter = 0; }
	public static void SendSongContinueCode(Song csf) { EnsureBuffer(1); s_buffer[0] = 0xFB; MPU_SendCommand(csf, s_buffer.Slice(0, 1), 0); csf.MIDILastSongCounter = 0; }
	public static void SendSongTickCode(Song csf)     { EnsureBuffer(1); s_buffer[0] = 0xF8; MPU_SendCommand(csf, s_buffer.Slice(0, 1), 0); }

	public static void SendSongPositionCode(Song csf, uint note16pos)
	{
		EnsureBuffer(3);

		s_buffer[0] = 0xF2;
		s_buffer[1] = unchecked((byte)(note16pos & 127));
		s_buffer[2] = unchecked((byte)((note16pos >> 7) & 127));

		MPU_SendCommand(csf, s_buffer, 0);
		csf.MIDILastSongCounter = 0.0;
	}


	public static void IncrementSongCounter(Song csf, int count)
	{
		/* We assume that one schism tick = one midi tick (24ppq).
		*
		* We also know that:
		*                   5 * mixingrate
		* Length of tick is -------------- samples
		*                     2 * cmdT
		*
		* where cmdT = last FX_TEMPO = current_tempo
		*/

		int TickLengthInSamplesHi = 5 * AudioPlayback.MixFrequency;
		int TickLengthInSamplesLo = 2 * Song.CurrentSong.CurrentTempo;

		double TickLengthInSamples = TickLengthInSamplesHi / (double) TickLengthInSamplesLo;

		/* TODO: Use fraction arithmetics instead (note: cmdA, cmdT may change any time) */

		csf.MIDILastSongCounter += count / TickLengthInSamples;

		int numTicks = (int)csf.MIDILastSongCounter;

		if (numTicks != 0)
		{
			for (int a = 0; a < numTicks; ++a)
				SendSongTickCode(csf);

			csf.MIDILastSongCounter -= numTicks;
		}
	}
}
