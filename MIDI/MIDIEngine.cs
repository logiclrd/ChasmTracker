using System;
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

	public static int PitchDepth = 12;
	public static int Amplification = 100;
	public static int C5Note = 60;

	public static int[] LastBendHit = new int[Constants.MaxChannels];

	public static bool HandleEvent(Event @event)
	{
		// TODO
		return false;
	}
}
