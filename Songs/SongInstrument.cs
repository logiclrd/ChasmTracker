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
		Initialize();
	}

	public void Initialize()
	{
		if (!IsEmpty)
			return;

		FadeOut = default;
		Flags = default;
		NewNoteAction = default;
		DuplicateCheckTypes = default;
		DuplicateCheckActions = default;
		PanningSwing = default;
		VolumeSwing = default;
		IFCutoff = default;
		IFResonance = default;
		MIDIChannelMask = default;
		PitchPanSeparation = default;
		Name = default;
		FileName = default;
		IsPlayed = default;

		VolumeEnvelope = new Envelope(64);
		PanningEnvelope = new Envelope(32);
		PitchEnvelope = new Envelope(32);

		GlobalVolume = 128;
		Panning = 128;
		MIDIBank = -1;
		MIDIProgram = -1;
		PitchPanCenter = 60; // why does pitch/pan not use the same note values as everywhere else?!

		for (int n = 0; n < 128; n++)
		{
			SampleMap[n] = 0;
			NoteMap[n] = (byte)(n + 1);
		}
	}

	public void InitializeFromSample(int sampleNumber, SongSample sample)
	{
		if (!sample.HasData)
			return;

		InitializeFromSample(sampleNumber);

		// IT doesn't set instrument filenames unless loading an instrument from disk
		//FileName = sample.FileName;

		Name = sample.Name;
	}

	public void InitializeFromSample(int sampleNumber)
	{
		if (!IsEmpty)
			return;

		Initialize();

		for (int i = 0; i < SampleMap.Length; i++)
			SampleMap[i] = (byte)sampleNumber;
	}

	public SongSample? TranslateKeyboard(int note, SongSample? def = default)
	{
		note = SampleMap[note - 1];

		if ((note > 0) && (note < Owner.Samples.Count))
			return Owner.Samples[note];
		else
			return def;
	}

	public static bool IsNullOrEmpty(SongInstrument? ins)
	{
		if (ins == null)
			return true;

		return ins.IsEmpty;
	}

	public bool IsEmpty
	{
		get
		{
			for (int n = 0; n < SampleMap.Length; n++)
				if (SampleMap[n] != 0 || NoteMap[n] != (n + SpecialNotes.First))
					return false;

			return
				string.IsNullOrWhiteSpace(Name) &&
				string.IsNullOrEmpty(FileName) &&
				Flags == default && /* No envelopes, loop points, panning, or carry flags set */
				NewNoteAction == NewNoteActions.NoteCut &&
				DuplicateCheckTypes == DuplicateCheckTypes.None &&
				DuplicateCheckActions == DuplicateCheckActions.NoteCut &&
				Envelope.IsNullOrBlank(VolumeEnvelope, 64) &&
				GlobalVolume == 128 &&
				FadeOut == 0 &&
				VolumeSwing == 0 &&
				Envelope.IsNullOrBlank(PanningEnvelope, 32) &&
				Panning == 32 * 4 && //mphack
				PitchPanCenter == 60 && // C-5 (blah)
				PitchPanSeparation == 0 &&
				PanningSwing == 0 &&
				Envelope.IsNullOrBlank(PitchEnvelope, 32) &&
				IFCutoff == 0 &&
				IFResonance == 0 &&
				MIDIChannelMask == 0 &&
				MIDIProgram == -1 &&
				MIDIBank == -1;
		}
	}
}
