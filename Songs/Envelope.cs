using System.Collections.Generic;

namespace ChasmTracker.Songs;

public class Envelope
{
	public readonly List<EnvelopeNode> Nodes = new List<EnvelopeNode>();

	public int LoopStart;
	public int LoopEnd;
	public int SustainStart;
	public int SustainEnd;
}
