namespace ChasmTracker.Playback;

public class EQBandState
{
	public float A0, A1, A2, B1, B2;
	public float X1, X2, Y1, Y2;
	public float Gain, CentreFrequency;
	public bool IsEnabled;

	public EQBandState(float gain, float centreFrequency, bool isEnabled)
	{
		Gain = gain;
		CentreFrequency = centreFrequency;
		IsEnabled = isEnabled;
	}
}
