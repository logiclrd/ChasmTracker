namespace ChasmTracker.Events;

public class MIDINoteEvent : Event
{
	public int Status;
	public int Channel;
	public int Note;
	public int Velocity;
}
