namespace ChasmTracker.Songs;

public class SongChannel
{
	public int Panning;
	public int Volume;
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
