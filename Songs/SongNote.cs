using System.Diagnostics.Metrics;
using System.Net.NetworkInformation;

namespace ChasmTracker.Songs;

public struct SongNote
{
	public byte Note;
	public byte Instrument;
	public byte VolumeEffectByte;
	public byte VolumeParameter;
	public byte EffectByte;
	public byte Parameter;

	public bool IsBlank
		=> (Note | Instrument | VolumeEffectByte | VolumeParameter | Effect | Parameter) == 0;

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

	public static AccidentalsMode AccidentalsMode;

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

			if (VolumeEffect > 13)
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
			if (Effect > 34)
			{
				Log.Append(4, "get_effect_char: effect {0} out of range", Effect);
				return '?';
			}

			return EffectChars[Effect];
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
}
