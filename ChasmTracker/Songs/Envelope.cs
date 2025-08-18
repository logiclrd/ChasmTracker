using System.Collections.Generic;
using ChasmTracker.Utility;

namespace ChasmTracker.Songs;

public class Envelope
{
	public readonly ByReferenceList<EnvelopeNode> Nodes = new ByReferenceList<EnvelopeNode>();

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

	public Envelope Clone() => new Envelope().CopyFrom(this);

	public Envelope CopyFrom(Envelope other)
	{
		Nodes.Clear();
		Nodes.AddRange(other.Nodes);
		LoopStart = other.LoopStart;
		LoopEnd = other.LoopEnd;
		SustainStart = other.SustainStart;
		SustainEnd = other.SustainEnd;

		return this;
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
