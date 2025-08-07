using ChasmTracker.Playback;

namespace ChasmTracker.Configurations;

public class EQBandConfiguration : ConfigurationSection
{
	EQBand eqBand;

	EQBandConfiguration() { eqBand = new EQBand(); }

	protected override ConfigurationSection CreatePristine() => new EQBandConfiguration();

	public EQBandConfiguration(int bandIndex)
	{
		eqBand = AudioSettings.EQBands[bandIndex];
	}

	[ConfigurationKey("freq")]
	public int Frequency;
	public int Gain;

	public override void PrepareToSave()
	{
		Frequency = eqBand.Frequency;
		Gain = eqBand.Gain;
	}
}