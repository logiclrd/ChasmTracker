using System.Collections.Generic;

namespace ChasmTracker.Songs;

public class Envelope
{
	public readonly List<EnvelopeNode> Nodes = new List<EnvelopeNode>();

	public int LoopStart;
	public int LoopEnd;
	public int SustainStart;
	public int SustainEnd;

	public Envelope()
	{
	}

	public Envelope(int defaultValue)
	{
		Nodes.Add(new EnvelopeNode(0, defaultValue));
		Nodes.Add(new EnvelopeNode(100, defaultValue));
	}

	public static bool IsNullOrBlank(Envelope? env, int value)
	{
		if (env == null)
			return true;

		return env.IsBlank(value);
	}

	public bool IsBlank(int value)
	{
		return
			Nodes.Count == 2 &&
			LoopStart == 0 &&
			LoopEnd == 0 &&
			SustainStart == 0 &&
			SustainEnd == 0 &&
			Nodes[0].Tick == 0 &&
			Nodes[1].Tick == 100 &&
			Nodes[0].Value == value &&
			Nodes[1].Value == value;
	}
}
