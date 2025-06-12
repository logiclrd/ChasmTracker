namespace ChasmTracker.Events;

public interface IButtonPressEvent
{
	bool IsPressEvent { get; }
	bool IsReleaseEvent { get; }

	public KeyState KeyState => IsPressEvent ? KeyState.Press : KeyState.Release;
}
