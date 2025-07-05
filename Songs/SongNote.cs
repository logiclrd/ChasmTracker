using System;

namespace ChasmTracker.Songs;

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

	public string NoteString
	{
		get
		{
			if ((Note > 120)
				&& !(Note == SpecialNotes.NoteCut
				|| Note == SpecialNotes.NoteOff
				|| Note == SpecialNotes.NoteFade))
			{
				Log.Append(4, "Note {0} out of range", Note);
				return "???";
			}

			switch (Note)
			{
				case SpecialNotes.None: return "\xAD\xAD\xAD";
				case SpecialNotes.NoteCut: return "\x5E\x5E\x5E";
				case SpecialNotes.NoteOff: return "\xCD\xCD\xCD";
				case SpecialNotes.NoteFade: return "\x7E\x7E\x7E";
			}

			int noteBase0 = Note - 1;

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

	public char EffectChar
	{
		get
		{
			if (EffectByte > 34)
			{
				Log.Append(4, "get_effect_char: effect {0} out of range", Effect);
				return '?';
			}

			return EffectChars[EffectByte];
		}
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
}
