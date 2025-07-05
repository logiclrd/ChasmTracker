namespace ChasmTracker.MIDI.Drivers;

using ChasmTracker.MIDI;

public class JackMIDIPort : MIDIPort
{
	public static bool Setup()
	{
		// TODO
		return false;
	}

	public override string Name => throw new System.NotImplementedException();
	public override string Provider => throw new System.NotImplementedException();
}