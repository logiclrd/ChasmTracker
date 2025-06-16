using ChasmTracker.Events;

namespace ChasmTracker.MIDI;

public class MIDIEngine
{
	/* configurable midi stuff */
	public static MIDIFlags Flags =
		MIDIFlags.TickQuantize |
		MIDIFlags.RecordNoteOff |
		MIDIFlags.RecordVelocity |
		MIDIFlags.RecordAftertouch |
		MIDIFlags.PitchBend;

	public int PitchDepth = 12;
	public int Amplification = 100;
	public int C5Note = 60;

	public static bool HandleEvent(Event @event)
	{
		// TODO
		return false;
	}
}
