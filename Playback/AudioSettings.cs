using ChasmTracker.Configurations;
using ChasmTracker.Pages;
using ChasmTracker.Utility;

namespace ChasmTracker.Playback;

public static class AudioSettings
{
	public static int SampleRate;
	public static int Bits;
	public static int Channels;
	public static int BufferSize;

	public class Master
	{
		public static int Left;
		public static int Right;
	}

	public static int ChannelLimit;
	public static SourceMode InterpolationMode;
	public static bool NoRamping;
	public static bool SurroundEffect;

	public static EQBand[] EQBands = new EQBand[4];

	static AudioSettings()
	{
		Configuration.RegisterConfigurable(new AudioConfigurationThunk());
		Configuration.RegisterConfigurable(new MixerConfigurationThunk());
		Configuration.RegisterConfigurable(new EQBandConfigurationThunk());
	}

	class AudioConfigurationThunk : IConfigurable<AudioConfiguration>
	{
		public void SaveConfiguration(AudioConfiguration config) => AudioSettings.SaveConfiguration(config);
		public void LoadConfiguration(AudioConfiguration config) => AudioSettings.LoadConfiguration(config);
	}

	class MixerConfigurationThunk : IConfigurable<MixerConfiguration>
	{
		public void SaveConfiguration(MixerConfiguration config) => AudioSettings.SaveConfiguration(config);
		public void LoadConfiguration(MixerConfiguration config) => AudioSettings.LoadConfiguration(config);
	}

	class EQBandConfigurationThunk : IConfigurable<EQBandConfiguration>
	{
		public void SaveConfiguration(EQBandConfiguration config) => AudioSettings.SaveConfiguration(config);
		public void LoadConfiguration(EQBandConfiguration config) => AudioSettings.LoadConfiguration(config);
	}

	public static void LoadConfiguration(AudioConfiguration config)
	{
		SampleRate = config.SampleRate;
		Bits = config.Bits;
		Channels = config.Channels;
		BufferSize = config.BufferSize;

		Master.Left = config.MasterLeft;
		Master.Right = config.MasterRight;

		if ((Channels != 1) && (Channels != 2))
			Channels = 2;
		if ((Bits != 8) && (Bits != 16) && (Bits != 32))
			Bits = 16;
	}

	public static void SaveConfiguration(AudioConfiguration config)
	{
		config.SampleRate = SampleRate;
		config.Bits = Bits;
		config.Channels = Channels;
		config.BufferSize = BufferSize;

		config.MasterLeft = Master.Left;
		config.MasterRight = Master.Right;
	}

	public static void LoadConfiguration(MixerConfiguration config)
	{
		ChannelLimit = config.ChannelLimit.Clamp(4, Constants.MaxVoices);
		InterpolationMode = config.InterpolationMode.Clamp();
		NoRamping = config.NoRamping;
		SurroundEffect = config.SurroundEffect;
	}

	public static void SaveConfiguration(MixerConfiguration config)
	{
		config.ChannelLimit = ChannelLimit;
		config.InterpolationMode = InterpolationMode;
		config.NoRamping = NoRamping;

		// Say, what happened to the switch for this in the gui?
		config.SurroundEffect = SurroundEffect;
	}

	public static void LoadConfiguration(EQBandConfiguration config)
	{
		if ((config.Index < 0) || (config.Index >= EQBands.Length))
			return;

		var band = EQBands[config.Index];

		band.Frequency = config.Frequency;
		band.Gain = config.Gain;
	}

	public static void SaveConfiguration(EQBandConfiguration config)
	{
		if ((config.Index < 0) || (config.Index >= EQBands.Length))
			return;

		var band = EQBands[config.Index];

		config.Frequency = band.Frequency;
		config.Gain = band.Gain;
	}
}
