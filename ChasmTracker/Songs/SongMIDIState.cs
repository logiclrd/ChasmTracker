using ChasmTracker.MIDI;

namespace ChasmTracker.Songs;

public class SongMIDIState
{
	public byte Volume;   // Which volume has been configured for this channel
	public byte Patch;    // What is the latest patch configured on this channel
	public byte Bank;     // What is the latest bank configured on this channel
	public int Bend;      // The latest pitchbend on this channel
	public sbyte Panning; // Latest pan

	public void Reset()
	{
		Volume  = 255;
		Patch   = 255;
		Bank    = 255;
		Bend    = GeneralMIDI.PitchBendCentre;
		Panning = 0;
	}

	public bool KnowsSomething => Patch != 255;
}
