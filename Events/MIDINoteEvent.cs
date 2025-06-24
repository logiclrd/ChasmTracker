using ChasmTracker.MIDI;

namespace ChasmTracker.Events;

public class MIDINoteEvent : Event
{
	public MIDINote Status;
	public int Channel;
	public int Note;
	public int Velocity;
}
