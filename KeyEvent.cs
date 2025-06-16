using ChasmTracker.Songs;

namespace ChasmTracker;

public struct KeyEvent
{
	public KeySym Sym; /* A keycode, can be Unicode */
	public KeySym OriginalSym; /* 'sym' from before key_translate warps it */
	public ScanCode ScanCode; /* Locale-independent key locations */
	public KeyMod Modifiers; /* current key modifiers */
	public string? Text; /* text input, if any. can be null */

	public KeyState State;
	public MouseState Mouse;
	public MouseButton MouseButton;

	public int MIDINote;
	public int MIDIChannel;
	public int MIDIVolume; /* -1 for not a midi key otherwise 0...128 */
	public int MIDIBend; /* normally 0; -8192 to +8192 */

	public Point StartPosition; /* start position (character) */
	public Point MousePosition; /* position of mouse (character) */
	public Point MousePositionFine; /* position of mouse (pixel) */
	public int MouseXHalfCharacter;

	public Point CharacterResolution; /* x/y resolution */

	public bool IsRepeat;
	public bool OnTarget;
	public bool IsSynthetic; /* 1 came from paste */
	public bool IsHandled;

	public int Character99Value
	{
		get
		{
			if (Modifiers.HasAnyFlag(KeyMod.ControlAlt))
				return -1;

			char c = char.ToLowerInvariant((char)Sym);

			if ((c >= 'h') && (c <= 'z'))
				return 10 + c - 'h';

			return HexValue;
		}
	}

	public int HexValue
	{
		get
		{
			if ((OriginalSym >= KeySym._0) && (OriginalSym <= KeySym._9))
				return OriginalSym - KeySym._0;

			if (Modifiers.HasFlag(KeyMod.Num))
			{
				if (OriginalSym == KeySym.KP_0)
					return 0;

				if ((OriginalSym >= KeySym.KP_1) && (OriginalSym <= KeySym.KP_9))
					return OriginalSym - KeySym.KP_1 + 1;
			}

			if ((OriginalSym >= KeySym.a) && (OriginalSym <= KeySym.f))
				return OriginalSym - KeySym.a + 10;

			return -1;
		}
	}

	/* --------------------------------------------------------------------- */

	/* return values:
	*      < 0 = invalid note
	*        0 = clear field ('.' in qwerty)
	*    1-120 = note
	* NOTE_CUT = cut ("^^^")
	* NOTE_OFF = off ("===")
	* NOTE_FADE = fade ("~~~")
	*         i haven't really decided on how to display this.
	*         and for you people who might say 'hey, IT doesn't do that':
	*         yes it does. read the documentation. it's not in the editor,
	*         but it's in the player. */
	public int NoteValue
	{
		get
		{
			if (!Modifiers.HasAnyFlag(KeyMod.ControlAlt))
				return -1;

			if (Sym == KeySym.KP_1 || k->sym == KeySym.KP_period)
			{
				if (!Modifiers.HasFlag(KeyMod.Num))
					return -1;
			}

			int note;

			switch (ScanCode)
			{
				case ScanCode.Grave:
					if (Modifiers.HasAnyFlag(KeyMod.Shift))
						return SpecialNotes.NoteFade;
					else
						return SpecialNotes.NoteOff;

				case ScanCode.NonUSHash: /* for delt */
				case ScanCode.KP_Hash:
					return SpecialNotes.NoteOff;

				case ScanCode._1:
					return SpecialNotes.NoteCut;

				case ScanCode.Period:
					return 0; /* clear */

				case ScanCode.Z: note = 1; break;
				case ScanCode.S: note = 2; break;
				case ScanCode.X: note = 3; break;
				case ScanCode.D: note = 4; break;
				case ScanCode.C: note = 5; break;
				case ScanCode.V: note = 6; break;
				case ScanCode.G: note = 7; break;
				case ScanCode.B: note = 8; break;
				case ScanCode.H: note = 9; break;
				case ScanCode.N: note = 10; break;
				case ScanCode.J: note = 11; break;
				case ScanCode.M: note = 12; break;

				case ScanCode.Q: note = 13; break;
				case ScanCode._2: note = 14; break;
				case ScanCode.W: note = 15; break;
				case ScanCode._3: note = 16; break;
				case ScanCode.E: note = 17; break;
				case ScanCode.R: note = 18; break;
				case ScanCode._5: note = 19; break;
				case ScanCode.T: note = 20; break;
				case ScanCode._6: note = 21; break;
				case ScanCode.Y: note = 22; break;
				case ScanCode._7: note = 23; break;
				case ScanCode.U: note = 24; break;
				case ScanCode.I: note = 25; break;
				case ScanCode._9: note = 26; break;
				case ScanCode.O: note = 27; break;
				case ScanCode._0: note = 28; break;
				case ScanCode.P: note = 29; break;

				default: return -1;
			}

			note += 12 * Keyboard.CurrentOctave;

			return note.Clamp(1, 120);
		}
	}

	public void Reset(Point startPosition)
	{
		Sym = default;
		ScanCode = default;
		Modifiers = default;
		Text = null;

		State = default;
		Mouse = default;
		MouseButton = default;

		MIDINote = -1;
		MIDIChannel = 0;
		MIDIVolume = -1;
		MIDIBend = 0;

		StartPosition = startPosition;
		MousePosition = default;
		MousePositionFine = default;
		MouseXHalfCharacter = 0;

		CharacterResolution = new Point(Constants.NativeScreenWidth / 80, Constants.NativeScreenHeight / 50); /* x/y resolution */

		IsRepeat = false;
		OnTarget = false;
		IsSynthetic = false;
		IsHandled = false;
	}
}
