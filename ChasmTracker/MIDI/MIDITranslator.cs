using System;
using System.Diagnostics.CodeAnalysis;

namespace ChasmTracker.MIDI;

using ChasmTracker.Playback;
using ChasmTracker.Songs;
using ChasmTracker.Utility;

public class MIDITranslator
{
	[ThreadStatic]
	static byte[]? s_buffer;

	[MemberNotNull(nameof(s_buffer))]
	static void EnsureBuffer(int size)
	{
		if ((s_buffer == null) || (s_buffer.Length < size))
			s_buffer = new byte[size * 2];
	}

	// Send exactly one MIDI message
	public static void MIDISend(Song csf, Span<byte> data, int nchan, bool fake)
	{
		ref var chan = ref csf.Voices[nchan];

		if (data.Length >= 1 && (data[0] == 0xFA || data[0] == 0xFC || data[0] == 0xFF))
		{
			// Start Song, Stop Song, MIDI Reset
			for (int c = 0; c < csf.Voices.Length; c++)
			{
				csf.Voices[c].Cutoff = 0x7F;
				csf.Voices[c].Resonance = 0x00;
			}
		}

		if (data.Length >= 4 && data[0] == 0xF0 && data[1] == 0xF0)
		{
			// impulse tracker filter control (mfg. 0xF0)
			switch (data[2])
			{
				case 0x00: // set cutoff
					if (data[3] < 0x80)
					{
						chan.Cutoff = data[3];
						csf.SetUpChannelFilter(ref chan, !chan.Flags.HasAllFlags(ChannelFlags.Filter), 256, AudioPlayback.MixFrequency);
					}
					break;
				case 0x01: // set resonance
					if (data[3] < 0x80)
					{
						chan.Resonance = data[3];
						csf.SetUpChannelFilter(ref chan, !chan.Flags.HasAllFlags(ChannelFlags.Filter), 256, AudioPlayback.MixFrequency);
					}
					break;
			}
		}
		else if (!fake && (csf.MIDISink != null))
		{
			/* okay, this is kind of how it works.
			we pass buffer_count as here because while
				1000 * ((8((buffer_size/2) - buffer_count)) / sample_rate)
			is the number of msec we need to delay by, libmodplug simply doesn't know
			what the buffer size is at this point so buffer_count simply has no
			frame of reference.

			fortunately, schism does and can complete this (tags: _schism_midi_out_raw )

			*/
			csf.MIDISink.OutRaw(csf, data, csf.BufferCount);
		}
	}

	public static void MIDIOutNote(Song csf, int chan, SongNoteRef? startingNote)
	{
		var mPtr = startingNote;

		if (!csf.Flags.HasAllFlags(SongFlags.InstrumentMode) || Status.Flags.HasAllFlags(StatusFlags.MIDILikeTracker))
			return;

		/*if(m)
		fprintf(stderr, "midi_out_note called (ch %d)note(%d)instr(%d)volcmd(%02X)cmd(%02X)vol(%02X)p(%02X)\n",
		chan, m.note, m.Instrument, m.voleffect, m.effect, m.volparam, m.param);
		else fprintf(stderr, "midi_out_note called (ch %d) m=%p\n", m);*/

		if (!csf.MIDIPlaying)
		{
			ProcessMIDIMacro(csf, 0, csf.MIDIConfig.Start, 0, 0, 0, 0); // START!
			csf.MIDIPlaying = true;
		}

		if (chan < 0)
			return;

		ref var c = ref csf.Voices[chan];

		chan %= Constants.MaxChannels;

		if (mPtr == null)
		{
			if (csf.MIDILastRowNumber != csf.Row) return;
			mPtr = csf.MIDILastRow[chan];
			if (mPtr == null) return;
		}
		else
		{
			csf.MIDILastRow[chan] = mPtr;
			csf.MIDILastRowNumber = csf.Row;
		}

		ref var m = ref mPtr.Get();

		var ins = csf.MIDIInsTracker[chan];

		if (m.Instrument > 0)
			ins = m.Instrument;

		var insPtr = csf.GetInstrumentSlotSafe(ins);

		if (insPtr == null)
			return; /* err...  almost certainly */

		int mc;

		if (insPtr.MIDIChannelMask >= 0x10000)
			mc = chan % 16;
		else
		{
			mc = 0;
			if(insPtr.MIDIChannelMask > 0)
				while (!insPtr.MIDIChannelMask.HasBitSet(1 << mc))
					++mc;
		}

		byte m_note = m.Note;

		int tc = csf.TickCount % csf.CurrentSpeed;

		if (m.Effect == Effects.Special)
		{
			switch (m.Parameter & 0xF0)
			{
				case 0xC0: /* note cut */
					if (tc == (m.Parameter & 15))
					{
						m_note = SpecialNotes.NoteCut;
					}
					else if (tc != 0)
						return;

					break;

				case 0xD0: /* note delay */
					if (tc != (m.Parameter & 15))
						return;

					break;

				default:
					if (tc != 0) return;
					break;
			}
		}
		else
		{
			if (tc != 0 && (startingNote == null))
				return;
		}

		int needNote = -1;
		int needVelocity = -1;

		if (m_note > SpecialNotes.Last)
		{
			if (csf.MIDINoteTracker[chan] != 0)
			{
				ProcessMIDIMacro(csf, chan, csf.MIDIConfig.NoteOff,
					0, csf.MIDINoteTracker[chan], 0, csf.MIDIInsTracker[chan]);
			}

			csf.MIDINoteTracker[chan] = 0;

			if (m.VolumeEffect != VolumeEffects.Volume)
				csf.MIDIVolTracker[chan] = 64;
			else
				csf.MIDIVolTracker[chan] = m.VolumeParameter;
		}
		else if ((m.Note == 0) && (m.VolumeEffect == VolumeEffects.Volume))
		{
			csf.MIDIVolTracker[chan] = m.VolumeParameter;
			needVelocity = csf.MIDIVolTracker[chan];
		}
		else if (m.Note != 0)
		{
			if (csf.MIDINoteTracker[chan] != 0)
			{
				ProcessMIDIMacro(csf, chan, csf.MIDIConfig.NoteOff,
					0, csf.MIDINoteTracker[chan], 0, csf.MIDIInsTracker[chan]);
			}

			/* this is all stupid */
			csf.MIDINoteTracker[chan] = m_note;

			if (m.VolumeEffect != VolumeEffects.Volume)
				csf.MIDIVolTracker[chan] = 64;
			else
				csf.MIDIVolTracker[chan] = m.VolumeParameter;

			needNote = csf.MIDINoteTracker[chan];
			needVelocity = csf.MIDIVolTracker[chan];
		}

		if (m.Instrument > 0)
			csf.MIDIInsTracker[chan] = ins;

		int mg = insPtr.MIDIProgram
			+ (MIDIEngine.Flags.HasAllFlags(MIDIFlags.BaseProgram1) ? 1 : 0);

		int mbl = insPtr.MIDIBank;
		int mbh = (insPtr.MIDIBank >> 7) & 127;

		if ((mbh > -1) && csf.MIDIWasBankHi[mc] != mbh)
		{
			EnsureBuffer(4);

			s_buffer[0] = unchecked((byte)(0xB0 | (mc & 15))); // controller
			s_buffer[1] = 0x00; // corse bank/select
			s_buffer[2] = unchecked((byte)mbh); // corse bank/select

			MIDISend(csf, s_buffer.Slice(0, 3), 0, false);
			csf.MIDIWasBankHi[mc] = mbh;
		}

		if ((mbl > -1) && csf.MIDIWasBankLo[mc] != mbl)
		{
			EnsureBuffer(4);

			s_buffer[0] = unchecked((byte)(0xB0 | (mc & 15))); // controller
			s_buffer[1] = 0x20; // fine bank/select
			s_buffer[2] = unchecked((byte)mbl); // fine bank/select

			MIDISend(csf, s_buffer.Slice(0, 3), 0, false);
			csf.MIDIWasBankLo[mc] = mbl;
		}

		if ((mg > -1) && csf.MIDIWasProgram[mc] != mg)
		{
			csf.MIDIWasProgram[mc] = mg;
			ProcessMIDIMacro(csf, chan, csf.MIDIConfig.SetProgram,
				mg, 0, 0, ins); // program change
		}

		if (c.Flags.HasAllFlags(ChannelFlags.Mute))
		{
			// don't send noteon events when muted
		}
		else if (needNote > 0)
		{
			if (needVelocity == -1) needVelocity = 64; // eh?

			needVelocity = (needVelocity*2).Clamp(0, 127);

			ProcessMIDIMacro(csf, chan, csf.MIDIConfig.NoteOn,
				0, needNote, needVelocity, ins); // noteon
		}
		else if (needVelocity > -1 && csf.MIDINoteTracker[chan] > 0)
		{
			needVelocity = (needVelocity*2).Clamp(0, 127);

			ProcessMIDIMacro(csf, chan, csf.MIDIConfig.SetVolume,
				needVelocity, csf.MIDINoteTracker[chan], needVelocity, ins); // volume-set
		}
	}

	public static void ProcessMIDIMacro(Song csf, int nChan, string? macro, int param,
				int note, int velocity, int useInstrumentNumber)
	{
		if (macro == null)
			return;

		/* this was all wrong. -mrsb */
		ref var chan = ref csf.Voices[nChan];

		var pEnv = (csf.Flags.HasAllFlags(SongFlags.InstrumentMode)
						&& chan.LastInstrumentNumber < Constants.MaxInstruments)
				? csf.GetInstrumentSlotSafe((useInstrumentNumber != 0) ? useInstrumentNumber : chan.LastInstrumentNumber)
				: null;

		EnsureBuffer(Constants.MaxMIDIMacro * 2);

		var outBuffer = s_buffer;

		int midiChannel;
		bool sawC, fakeMIDIChannel = false;
		int nybblePos = 0;
		int writePos = 0;

		sawC = false;
		if ((pEnv == null) || pEnv.MIDIChannelMask == 0)
		{
			/* okay, there _IS_ no real midi channel. forget this for now... */
			midiChannel = 15;
			fakeMIDIChannel = true;
		}
		else if (pEnv.MIDIChannelMask >= 0x10000)
			midiChannel = (nChan-1) % 16;
		else
		{
			midiChannel = 0;
			while (!pEnv.MIDIChannelMask.HasBitSet(1 << midiChannel))
				++midiChannel;
		}

		for (int readPos = 0; readPos < macro.Length; readPos++)
		{
			byte data = 0;

			bool isNybble = false;

			switch (macro[readPos])
			{
				case '0': case '1': case '2':
				case '3': case '4': case '5':
				case '6': case '7': case '8':
				case '9':
					data = (byte)(macro[readPos] - '0');
					isNybble = true;
					break;
				case 'A': case 'B': case 'C':
				case 'D': case 'E': case 'F':
					data = (byte)((macro[readPos] - 'A') + 0x0A);
					isNybble = true;
					break;
				case 'c':
					/* Channel */
					data = (byte)midiChannel;
					isNybble = true;
					sawC = true;
					break;
				case 'n':
					/* Note */
					data = (byte)(note - 1);
					break;
				case 'v':
					data = (byte)velocity.Clamp(0x01, 0x7F);
					break;

				case 'u':
				{
					/* Volume */

					/* FIXME: This is still wrong.
					 *
					 * Notable suspects:
					 *  flt-macro.it; second note doesn't play right
					 *     (filter value is half than it should be)
					 *  GlobalVolume-Macro.it; row 9's filter value should
					 *     be much less than it actually is
					 *
					 * I have no idea about whats up with either of them.
					 * I'm just saving that for another day though  ;) */
					int vol = (int)((chan.CalcVolume * csf.LastGlobalVolume)
						* (long)(chan.GlobalVolume * chan.LastInstrumentVolume)
						/ (1 << 26));

					/*
					Console.WriteLine("{0} = ({1} * {2}) * ({3} * {4}) / 1l << 26", vol,
						chan.CalcVolume, csf.LastGlobalVolume,
						chan.GlobalVolume, chan.LastInstrumentVolume);
					*/

					data = (byte)vol.Clamp(0x01, 0x7F);
					//Console.WriteLine(data);
					break;
				}
				case 'x':
					/* Panning */
					data = (byte)Math.Min(chan.Panning, 0x7F);
					break;
				case 'y':
					/* Final Panning */
					data = (byte)Math.Min(chan.FinalPanning, 0x7F);
					break;
				case 'a':
					/* MIDI Bank (high byte) */
					if ((pEnv != null) && pEnv.MIDIBank != -1)
						data = (byte)((pEnv.MIDIBank >> 7) & 0x7F);
					break;
				case 'b':
					/* MIDI Bank (low byte) */
					if ((pEnv != null) && pEnv.MIDIBank != -1)
						data = (byte)(pEnv.MIDIBank & 0x7F);
					break;
				case 'p':
					/* MIDI Program */
					if ((pEnv != null) && pEnv.MIDIProgram != -1)
						data = (byte)(pEnv.MIDIProgram & 0x7F);
					break;
				case 'z':
					/* Zxx Param */
					data = (byte)param;
					break;
				case 'h':
					/* Host channel */
					data = (byte)(nChan & 0x7F);
					break;
				case 'm':
					/* Loop direction (judging from the macro letter, this was supposed to be
						loop mode instead, but a wrong offset into the channel structure was used in IT.) */
					data = chan.Flags.HasAllFlags(ChannelFlags.PingPongFlag) ? (byte)1 : (byte)0;
					break;
				case 'o':
					/* OpenMPT test case ZxxSecrets.it:
						offsets are NOT clamped! also SAx doesn't count :) */
					data = (byte)((chan.MemOffset >> 8) & 0xFF);
					break;
				default:
					continue;
			}

			if (isNybble)
			{
				if (nybblePos == 0)
				{
					outBuffer[writePos] = data;
					nybblePos = 1;
				}
				else
				{
					outBuffer[writePos] = unchecked((byte)((outBuffer[writePos] << 4) | data));
					writePos++;
					nybblePos = 0;
				}
			}
			else
			{
				if (nybblePos == 1)
				{
					writePos++;
					nybblePos = 0;
				}

				outBuffer[writePos] = data;
				writePos++;
			}
		}

		if (nybblePos == 1)
		{
			// Finish current byte
			writePos++;
		}

		// Macro string has been parsed and translated, now send the message(s)...
		int sendPos = 0;
		byte runningStatus = 0;

		while (sendPos < writePos)
		{
			int sendLength = 0;
			if (outBuffer[sendPos] == 0xF0)
			{
				// SysEx start
				if ((writePos - sendPos >= 4) && outBuffer[sendPos + 1] == 0xF0)
				{
					// Internal macro, 4 bytes long
					sendLength = 4;
				}
				else
				{
					// SysEx message, find end of message
					for (int i = sendPos + 1; i < writePos; i++)
					{
						if (outBuffer[i] == 0xF7)
						{
							// Found end of SysEx message
							sendLength = i - sendPos + 1;
							break;
						}
					}

					if (sendLength == 0)
					{
						// Didn't find end, so "invent" end of SysEx message
						outBuffer[writePos++] = 0xF7;
						sendLength = writePos - sendPos;
					}
				}
			}
			else if (!outBuffer[sendPos].HasBitSet(0x80))
			{
				// Missing status byte? Try inserting running status
				if (runningStatus != 0)
				{
					sendPos--;
					outBuffer[sendPos] = runningStatus;
				}
				else
				{
					// No running status to re-use; skip this byte
					sendPos++;
				}

				continue;
			}
			else
			{
				// Other MIDI messages
				sendLength = Math.Min(MIDIEngine.GetEventLength(outBuffer[sendPos]), writePos - sendPos);
			}

			if (sendLength == 0)
				break;

			if (outBuffer[sendPos] < 0xF0)
				runningStatus = outBuffer[sendPos];

			MIDISend(csf, outBuffer.Slice(sendPos, sendLength), nChan, sawC && fakeMIDIChannel);
			sendPos += sendLength;
		}
	}
}
