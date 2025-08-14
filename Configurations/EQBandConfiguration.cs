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
	public int Frequency;
	public int Gain;

	public override void PrepareToSave()
	{
		var eqBand = GetEQBand();

		Frequency = eqBand.Frequency;
		Gain = eqBand.Gain;
	}
}