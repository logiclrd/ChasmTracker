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
