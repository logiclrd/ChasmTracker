namespace ChasmTracker.Events;

public class MIDIControllerEvent : Event
{
	public int Value;
	public int Channel;
	public int Param;
}
