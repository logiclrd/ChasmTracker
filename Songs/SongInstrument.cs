using System.Linq;

namespace ChasmTracker.Songs;

public class SongInstrument
{
	public int FadeOut;
	public InstrumentFlags Flags;
	public int GlobalVolume;
	public int Panning;
	public byte[] SampleMap = new byte[128];
	public byte[] NoteMap = new byte[128];
	public Envelope? VolumeEnvelope;
	public Envelope? PanningEnvelope;
	public Envelope? PitchEnvelope;
	public NewNoteActions NewNoteAction;
	public DuplicateCheckTypes DuplicateCheckTypes;
	public DuplicateCheckActions DuplicateCheckActions;
	public int PanningSwing;
	public int VolumeSwing;
	public int IFCutoff;
	public int IFResonance;
	public int MIDIBank; // TODO split this?
	public int MIDIProgram;
	public int MIDIChannelMask; // FIXME why is this a mask? why is a mask useful? does 2.15 use a mask?
	public int PitchPanSeparation;
	public int PitchPanCenter;
	public string? Name;
	public string? FileName;
	public bool IsPlayed; // for note playback dots

	public readonly Song Owner;

	public SongInstrument(Song owner)
	{
		Owner = owner;
	}

	public SongSample? TranslateKeyboard(int note, SongSample? def = default)
	{
		note = SampleMap[note - 1];

		if ((note > 0) && (note < Owner.Samples.Count))
			return Owner.Samples[note];
		else
			return def;
	}
}
