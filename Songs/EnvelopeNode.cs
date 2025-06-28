namespace ChasmTracker.Songs;

public struct EnvelopeNode
{
	public int Tick;
	public byte Value;

	public EnvelopeNode()
	{
	}

	public EnvelopeNode(int tick, int value)
	{
		Tick = tick;
		Value = value;
	}
}