namespace ChasmTracker.Songs;

public struct SongChannel
{
	public SongChannel() { }

	public int Panning = 128;
	public int Volume = 64;
	public ChannelFlags Flags;

	public bool IsMuted
	{
		get => Flags.HasFlag(ChannelFlags.Mute);
		set
		{
			if (value)
				Flags |= ChannelFlags.Mute;
			else
				Flags &= ~ChannelFlags.Mute;
		}
	}
}
