using ChasmTracker.Audio;
using ChasmTracker.Playback;

namespace ChasmTracker.Configurations;

public class MixerConfiguration : ConfigurationSection
{
	public int ChannelLimit = AudioPlayback.DefaultChannelLimit;
	public SourceMode InterpolationMode = SourceMode.Linear;
	public bool NoRamping = false;
	public bool SurroundEffect = true;

	public override void PrepareToSave()
	{
		ChannelLimit = AudioSettings.ChannelLimit;
		InterpolationMode = AudioSettings.InterpolationMode;
		NoRamping = AudioSettings.NoRamping;

		// Say, what happened to the switch for this in the gui?
		SurroundEffect = AudioSettings.SurroundEffect;
	}
}
