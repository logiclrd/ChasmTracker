namespace ChasmTracker.Events;

public class KeyboardEvent : Event, IButtonPressEvent
{
	public KeyboardEventType EventType;

	public KeyState State;
	public bool IsRepeat;

	public KeySym Sym;
	public ScanCode ScanCode;
	public KeyMod Modifiers;
	public string? Text;

	public bool IsPressEvent => (EventType == KeyboardEventType.KeyDown);
	public bool IsReleaseEvent => (EventType == KeyboardEventType.KeyUp);
}
