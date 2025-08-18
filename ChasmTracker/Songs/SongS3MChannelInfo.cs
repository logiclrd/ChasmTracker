namespace ChasmTracker.Songs;

using ChasmTracker.Utility;

public struct SongS3MChannelInfo
{
	public byte Note;    // Which note is playing in this channel (0 = nothing)
	public byte Patch;   // Which patch was programmed on this channel (&0x80 = percussion)
	public byte Bank;    // Which bank was programmed on this channel
	public sbyte Panning;       // Which pan level was last selected
	public sbyte Channel;      // Which MIDI channel was allocated for this channel. -1 = none
	public uint PreferredChannelMask; // Which MIDI channel was preferred

	public void Reset()
	{
		Note                 = 0;
		Patch                = 0;
		Bank                 = 0;
		Panning              = 0;
		Channel              = -1;
		PreferredChannelMask = 0xFFFFFFFF;
	}

	public bool IsActive => (Note != 0) && (Channel >= 0);
	public bool IsPercussion => Patch.HasBitSet(0x80) || PreferredChannelMask.HasBitSet(1 << 9);
}