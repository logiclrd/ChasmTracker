using System;

namespace ChasmTracker.Songs;

using ChasmTracker.Configurations;
using ChasmTracker.Pages;
using ChasmTracker.Utility;

public struct SongNote
{
	public byte Note;
	public byte Instrument;
	public byte VolumeEffectByte;
	public byte VolumeParameter;
	public byte EffectByte;
	public byte Parameter;

	public bool IsBlank
		=> (Note | Instrument | VolumeEffectByte | VolumeParameter | EffectByte | Parameter) == 0;

	public void SetNoteNote(int a, int b)
	{
		if (a > 0 && a < 250)
		{
			a += b;
			if (a <= 0 || a >= 250) a = 0;
		}

		Note = unchecked((byte)a);
	}

	public Effects Effect
	{
		get => (Effects)EffectByte;
		set => EffectByte = (byte)value;
	}

	public VolumeEffects VolumeEffect
	{
		get => (VolumeEffects)VolumeEffectByte;
		set => VolumeEffectByte = (byte)value;
	}

	// kbd_sharp_flat_state
	public static AccidentalsMode AccidentalsMode;

	// kbd_sharp_flat_toggle
	public static void ToggleAccidentalsMode(AccidentalsMode? newMode)
	{
		if (newMode == null)
		{
			switch (AccidentalsMode)
			{
				case AccidentalsMode.Flats: newMode = AccidentalsMode.Sharps; break;
				case AccidentalsMode.Sharps: newMode = AccidentalsMode.Flats; break;
			}
		}

		switch (newMode)
		{
			default:
			case AccidentalsMode.Sharps:
				Status.FlashText("Displaying accidentals as sharps (#)");
				AccidentalsMode = AccidentalsMode.Sharps;
				break;
			case AccidentalsMode.Flats:
				Status.FlashText("Displaying accidentals as flats (b)");
				AccidentalsMode = AccidentalsMode.Flats;
				break;
		}
	}

	static SongNote()
	{
		Configuration.RegisterConfigurable(new GeneralConfigurationThunk());
	}

	class GeneralConfigurationThunk : IConfigurable<GeneralConfiguration>
	{
		public void SaveConfiguration(GeneralConfiguration config) => SongNote.SaveConfiguration(config);
		public void LoadConfiguration(GeneralConfiguration config) => SongNote.LoadConfiguration(config);
	}

	static void LoadConfiguration(GeneralConfiguration config)
	{
		ToggleAccidentalsMode(config.AccidentalsAsFlats ? AccidentalsMode.Flats : AccidentalsMode.Sharps);
	}

	static void SaveConfiguration(GeneralConfiguration config)
	{
		config.AccidentalsAsFlats = (AccidentalsMode == AccidentalsMode.Flats);
	}

	public string NoteString => GetNoteString(Note);

	public static string GetNoteString(int note)
	{
		if ((note > 120)
			&& !(note == SpecialNotes.NoteCut
			|| note == SpecialNotes.NoteOff
			|| note == SpecialNotes.NoteFade))
		{
			Log.Append(4, "Note {0} out of range", note);
			return "???";
		}

		switch (note)
		{
			case SpecialNotes.None: return "\xAD\xAD\xAD";
			case SpecialNotes.NoteCut: return "\x5E\x5E\x5E";
			case SpecialNotes.NoteOff: return "\xCD\xCD\xCD";
			case SpecialNotes.NoteFade: return "\x7E\x7E\x7E";
		}

		int noteBase0 = note - 1;

		int octave = 1 + noteBase0 / 12;

		if (AccidentalsMode == AccidentalsMode.Sharps)
		{
			switch (noteBase0 % 12)
			{
				case 0: return "C-" + octave;
				case 1: return "C#" + octave;
				case 2: return "D-" + octave;
				case 3: return "D#" + octave;
				case 4: return "E-" + octave;
				case 5: return "F-" + octave;
				case 6: return "F#" + octave;
				case 7: return "G-" + octave;
				case 8: return "G#" + octave;
				case 9: return "A-" + octave;
				case 10: return "A#" + octave;
				case 11: return "B-" + octave;
			}
		}
		else
		{
			switch (noteBase0 % 12)
			{
				case 0: return "C-" + octave;
				case 1: return "Db" + octave;
				case 2: return "D-" + octave;
				case 3: return "Eb" + octave;
				case 4: return "E-" + octave;
				case 5: return "F-" + octave;
				case 6: return "Gb" + octave;
				case 7: return "G-" + octave;
				case 8: return "Ab" + octave;
				case 9: return "A-" + octave;
				case 10: return "Bb" + octave;
				case 11: return "B-" + octave;
			}
		}

		return "???";
	}

	public string NoteStringShort
	{
		get
		{
			if ((Note > 120)
				&& !(Note == SpecialNotes.NoteCut
				|| Note == SpecialNotes.NoteOff
				|| Note == SpecialNotes.NoteFade))
			{
				Log.Append(4, "Note {0} out of range", Note);
				return "??";
			}

			switch (Note)
			{
				case SpecialNotes.None: return "\xAD\xAD";
				case SpecialNotes.NoteCut: return "\x5E\x5E";
				case SpecialNotes.NoteOff: return "\xCD\xCD";
				case SpecialNotes.NoteFade: return "\x7E\x7E";
			}

			int noteBase0 = Note - 1;

			int octave = 1 + noteBase0 / 12;

			if (AccidentalsMode == AccidentalsMode.Sharps)
			{
				switch (noteBase0 % 12)
				{
					case 0: return "c" + octave;
					case 1: return "C" + octave;
					case 2: return "d" + octave;
					case 3: return "D" + octave;
					case 4: return "e" + octave;
					case 5: return "f" + octave;
					case 6: return "F" + octave;
					case 7: return "g" + octave;
					case 8: return "G" + octave;
					case 9: return "a" + octave;
					case 10: return "A" + octave;
					case 11: return "b" + octave;
				}
			}
			else
			{
				switch (noteBase0 % 12)
				{
					case 0: return "C" + octave;
					case 1: return "d" + octave;
					case 2: return "D" + octave;
					case 3: return "e" + octave;
					case 4: return "E" + octave;
					case 5: return "F" + octave;
					case 6: return "g" + octave;
					case 7: return "G" + octave;
					case 8: return "a" + octave;
					case 9: return "A" + octave;
					case 10: return "b" + octave;
					case 11: return "B" + octave;
				}
			}

			return "??";
		}
	}

	public string VolumeString
	{
		get
		{
			const string Commands = "...CDAB$H<>GFE";

			if (VolumeEffectByte > 13)
			{
				Log.Append(4, "get_volume_string: volume effect {0} out of range", VolumeEffect);
				return "??";
			}

			switch ((VolumeEffects)VolumeEffect)
			{
				case VolumeEffects.None:
					return "\xAD\xAD";
				case VolumeEffects.Volume:
				case VolumeEffects.Panning:
					/* Yeah, a bit confusing :)
					 * The display stuff makes the distinction here with
					 * a different color for panning. */
					return VolumeParameter.ToString("d2");
				default:
					return Commands[(int)VolumeEffect] + VolumeParameter.ToString("X1");
			}
		}
	}

	public bool HasInstrument => Instrument != 0;

	public string InstrumentString => Instrument.ToString99();

	const string EffectChars = ".JFEGHLKRXODB!CQATI?SMNVW$UY?P&Z()?";

	public char EffectChar => GetEffectChar(EffectByte);

	public static char GetEffectChar(byte effectByte)
	{
		if (effectByte > 34)
		{
			Log.Append(4, "GetEffectChar: effect {0} out of range", (Effects)effectByte);
			return '?';
		}

		return EffectChars[effectByte];
	}

	public string EffectString
	{
		get
		{
			return EffectChar + Parameter.ToString("X2");
		}
	}

	public static SongNote Empty => new SongNote();

	public static SongNote BareNote(byte note)
	{
		var ret = new SongNote();

		ret.Note = note;

		return ret;
	}

	public bool HasNote => Note > 0;
	public bool NoteIsNote => IsNote(Note);
	public bool NoteIsControl => IsControl(Note);
	public bool NoteIsInvalid => IsInvalid(Note);

	public static bool IsNote(int note)
	{
		return (note > SpecialNotes.None) && (note <= SpecialNotes.Last); // anything playable - C-0 to B-9
	}

	public static bool IsControl(int note)
	{
		return (note > SpecialNotes.Last); // not a note, but non-empty
	}

	public static bool IsInvalid(int note)
	{
		return (note > SpecialNotes.Last) && (note < SpecialNotes.NoteCut) && (note != SpecialNotes.NoteFade); // ???
	}

	/* --------------------------------------------------------------------------------------------------------- */
	/* note/freq conversion functions */

	public static int NoteFromFrequency(int frequency, int c5speed = 8363)
	{
		int n;

		if (frequency == 0)
			return 0;

		n = 1;

		int cFrequency = FrequencyFromNote(n, c5speed);

		while (cFrequency + cFrequency < frequency)
		{
			cFrequency += cFrequency;
			n += 12;
		}

		for (; n <= 120; n++)
		{
			/* Essentially, this is just doing a note_to_frequency(n, 8363), but with less
			computation since there's no c5speed to deal with. */
			if (frequency <= FrequencyFromNote(n + 1, c5speed))
				return n + 1;
		}

		return 120;
	}

	public static int FrequencyFromNote(int note, int c5speed = 8363)
	{
		if ((note == 0) || (note > 0xF0))
			return 0;

		note--;

		long product = c5speed * (long)Tables.LinearSlideUpTable[(note % 12) * 16];

		product <<= note / 12;

		return (int)(product >> 21);
	}

	public static int TransposeToFrequency(int transp, int ftune)
	{
		return (int)(8363.0 * Math.Pow(2.0, (transp * 128.0 + ftune) / 1536.0));
	}

	public static int FrequencyToTranspose(int freq)
	{
		return (int)(1536.0 * (Math.Log(freq / 8363.0) / Math.Log(2)));
	}

	public static int CalculateHalfTone(int hz, int rel)
	{
		return (int)Math.Round(Math.Pow(2.0, rel / 12.0) * hz);
	}

	byte MODPeriodToNote(int period)
	{
		if (period != 0)
		{
			for (int n = 0; n <= SpecialNotes.None; n++)
				if (period >= (32 * Tables.PeriodTable[n % 12] >> (n / 12 + 2)))
					return (byte)(n + 1);
		}

		return SpecialNotes.None;
	}

	// load a .mod-style 4-byte packed note
	public void ImportMODNote(byte[] noteBytes, bool importEffect = true)
	{
		Note = MODPeriodToNote(((noteBytes[0] & 0xf) << 8) + noteBytes[1]);
		Instrument = (byte)((noteBytes[0] & 0xf0) + (noteBytes[2] >> 4));
		VolumeEffect = VolumeEffects.None;
		VolumeParameter = 0;
		EffectByte = (byte)(noteBytes[2] & 0xf);
		Parameter = noteBytes[3];

		if (importEffect)
			ImportMODEffect(EffectByte, Parameter, false);
	}

	public void ImportMODEffect(byte modEffect, byte modParam, bool fromXM)
	{
		var effect = Effects.None;

		// strip no-op effect commands that have memory in IT but not MOD/XM.
		// arpeggio is safe since it's handled in the next switch.
		if ((modParam == 0) || (modEffect == 0x0E && !modParam.HasAnyBitSet(0xF)))
		{
			switch (modEffect)
			{
				case 0x01:
				case 0x02:
				case 0x0A:
					if (!fromXM) modEffect = 0;
					break;
				case 0x0E:
					switch (modParam & 0xF0)
					{
						case 0x10:
						case 0x20:
						case 0xA0:
						case 0xB0:
							if (fromXM) break;
							goto case 0x90;
						case 0x90:
							modEffect = modParam = 0;
							break;
					}
					break;
			}
		}

		switch (modEffect)
		{
			case 0x00:      if (modParam != 0) effect = Effects.Arpeggio; break;
			case 0x01:      effect = Effects.PortamentoUp; break;
			case 0x02:      effect = Effects.PortamentoDown; break;
			case 0x03:      effect = Effects.TonePortamento; break;
			case 0x04:      effect = Effects.Vibrato; break;
			case 0x05:      effect = Effects.TonePortamentoVolume; if (modParam.HasAnyBitSet(0xF0)) modParam &= 0xF0; break;
			case 0x06:      effect = Effects.VibratoVolume; if (modParam.HasAnyBitSet(0xF0)) modParam &= 0xF0; break;
			case 0x07:      effect = Effects.Tremolo; break;
			case 0x08:      effect = Effects.Panning; break;
			case 0x09:      effect = Effects.Offset; break;
			case 0x0A:
				effect = Effects.VolumeSlide;
				if (modParam.HasAnyBitSet(0xF0))
					modParam &= 0xF0;
				else
					modParam &= 0x0F;

				// IT does D0F/DF0 on the first tick, while MOD/XM does not.
				// This is very noticeable in e.g. Dubmood's "FFF keygen intro"
				// where the chords play much shorter than in FT2.
				// So, compensate by reducing to D0E/DE0. Hopefully this
				// doesn't make other mods sound bad in comparison.

				if (modParam == 0xF0) modParam = 0xE0;
				else if (modParam == 0x0F) modParam = 0x0E;

				break;
			case 0x0B:      effect = Effects.PositionJump; break;
			case 0x0C:
				if (fromXM)
					effect = Effects.Volume;
				else
				{
					VolumeEffect = VolumeEffects.Volume;
					VolumeParameter = modParam.Clamp(0, 64);
					modEffect = modParam = 0;
				}
				break;
			case 0x0D:      effect = Effects.PatternBreak; modParam = (byte)(((modParam >> 4) * 10) + (modParam & 0x0F)); break;
			case 0x0E:
				effect = Effects.Special;
				switch (modParam & 0xF0)
				{
					case 0x10: effect = Effects.PortamentoUp; modParam |= 0xF0; break;
					case 0x20: effect = Effects.PortamentoDown; modParam |= 0xF0; break;
					case 0x30: modParam = (byte)((modParam & 0x0F) | 0x10); break;
					case 0x40: modParam = (byte)((modParam & 0x0F) | 0x30); break;
					case 0x50: modParam = (byte)((modParam & 0x0F) | 0x20); break;
					case 0x60: modParam = (byte)((modParam & 0x0F) | 0xB0); break;
					case 0x70: modParam = (byte)((modParam & 0x0F) | 0x40); break;
					case 0x90: effect = Effects.Retrigger; modParam &= 0x0F; break;
					case 0xA0:
						effect = Effects.VolumeSlide;
						if (modParam.HasAnyBitSet(0x0F))
							modParam = (byte)((modParam << 4) | 0x0F);
						else
							modParam = 0;
						break;
					case 0xB0:
						effect = Effects.VolumeSlide;
						if (modParam.HasAnyBitSet(0x0F))
							modParam = (byte)(0xF0 | Math.Min(modParam & 0x0F, 0x0E));
						else
							modParam = 0;
						break;
				}
				break;
			case 0x0F:
				// FT2 processes 0x20 as Txx; ST3 loads it as Axx
				effect = (modParam < (fromXM ? 0x20 : 0x21)) ? Effects.Speed : Effects.Tempo;
				break;
			// Extension for XM extended effects
			case 'G' - 55:
				effect = Effects.GlobalVolume;
				modParam = (byte)Math.Min(modParam << 1, 0x80);
				break;
			case 'H' - 55:
				effect = Effects.GlobalVolumeSlide;
				//if (param & 0xF0) param &= 0xF0;
				modParam = (byte)(Math.Min((modParam & 0xf0) << 1, 0xf0) | Math.Min((modParam & 0xf) << 1, 0xf));
				break;
			case 'K' - 55:  effect = Effects.KeyOff; break;
			case 'L' - 55:  effect = Effects.SetEnvelopePosition; break;
			case 'M' - 55:  effect = Effects.ChannelVolume; break;
			case 'N' - 55:  effect = Effects.ChannelVolumeSlide; break;
			case 'P' - 55:
				effect = Effects.PanningSlide;
				// ft2 does Pxx backwards! skjdfjksdfkjsdfjk
				if (modParam.HasAnyBitSet(0xF0))
					modParam >>= 4;
				else
					modParam = (byte)((modParam & 0xf) << 4);
				break;
			case 'R' - 55:  effect = Effects.Retrigger; break;
			case 'T' - 55:  effect = Effects.Tremor; break;
			case 'X' - 55:
				switch (modParam & 0xf0)
				{
					case 0x10:
						effect = Effects.PortamentoUp;
						modParam = (byte)(0xe0 | (modParam & 0xf));
						break;
					case 0x20:
						effect = Effects.PortamentoDown;
						modParam = (byte)(0xe0 | (modParam & 0xf));
						break;
					case 0x50:
					case 0x60:
					case 0x70:
					case 0x90:
					case 0xa0:
						// ModPlug Tracker extensions
						effect = Effects.Special;
						break;
					default:
						modEffect = modParam = 0;
						break;
				}
				break;
			case 'Y' - 55:  effect = Effects.Panbrello; break;
			case 'Z' - 55:  effect = Effects.MIDI;     break;
			case '[' - 55:
				// FT2 shows this weird effect as -xx, and it can even be inserted
				// by typing "-", although it doesn't appear to do anything.
			default:        modEffect = 0; break;
		}

		Effect = effect;
		Parameter = modParam;
	}

	public void SwapEffects()
	{
		(VolumeEffectByte, EffectByte) = (EffectByte, VolumeEffectByte);
		(VolumeParameter, Parameter) = (Parameter, VolumeParameter);
	}
}
