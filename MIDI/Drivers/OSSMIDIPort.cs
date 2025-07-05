namespace ChasmTracker.MIDI.Drivers;

using ChasmTracker.MIDI;

public class OSSMIDIPort : MIDIPort
{
	string _name = "";

	public override string Name => _name;
	public override string Provider => "Win32MM";

	public static bool Setup()
	{
		//Prefer ALSA MIDI over OSS, but do not enable both since ALSA's OSS emulation can cause conflicts
		if (!ALSAMIDIPort.Setup())
		{
		}
		return false;
	}
}