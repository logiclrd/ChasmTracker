namespace ChasmTracker.MIDI.Drivers;

using ChasmTracker.MIDI;

public class Win32MMMIDIPort : MIDIPort
{
	string _name = "";

	public override string Name => _name;
	public override string Provider => "Win32MM";

	public static bool Setup()
	{
		// TODO
		return false;
	}
}