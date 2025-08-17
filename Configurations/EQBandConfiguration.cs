using ChasmTracker.Playback;

namespace ChasmTracker.Configurations;

public class EQBandConfiguration : ConfigurationSection
{
	int _bandIndex;

	EQBandConfiguration() { _bandIndex = 0; }

	protected override ConfigurationSection CreatePristine() => new EQBandConfiguration();

	EQBand GetEQBand()
	{
		if (_bandIndex == 0)
			return new EQBand();
		else
			return AudioSettings.EQBands[_bandIndex];
	}

	public EQBandConfiguration(int bandIndex)
	{
		Index = bandIndex;
		_bandIndex = bandIndex;
	}

	[ConfigurationKey("freq")]
	public int FrequencyIndex;
	public int Gain;

	public int Frequency => 120 + (((_bandIndex * 128) * FrequencyIndex) * (AudioPlayback.MixFrequency / 128) / 1024);

	public override void PrepareToSave()
	{
		var eqBand = GetEQBand();

		FrequencyIndex = eqBand.FrequencyIndex;
		Gain = eqBand.Gain;
	}
}