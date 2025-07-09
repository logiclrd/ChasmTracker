using System.Diagnostics.CodeAnalysis;

namespace ChasmTracker.Songs;

public struct EnvelopeNode
{
	public int Tick;
	public byte Value;

	public EnvelopeNode()
	{
	}

	public EnvelopeNode(int tick, byte value)
	{
		Tick = tick;
		Value = value;
	}

	public EnvelopeNode(int tick, int value)
	{
		Tick = tick;
		Value = (byte)value;
	}

	public static implicit operator EnvelopeNode((int Tick, byte Value) tuple)
		=> new EnvelopeNode(tuple.Tick, tuple.Value);
	public static implicit operator EnvelopeNode((int Tick, int Value) tuple)
		=> new EnvelopeNode(tuple.Tick, tuple.Value);

	public static bool operator ==(EnvelopeNode left, EnvelopeNode right)
	{
		return (left.Tick == right.Tick) && (left.Value == right.Value);
	}

	public static bool operator !=(EnvelopeNode left, EnvelopeNode right)
	{
		return (left.Tick != right.Tick) || (left.Value != right.Value);
	}

	public override bool Equals([NotNullWhen(true)] object? obj)
	{
		if (obj is EnvelopeNode otherNode)
			return this == otherNode;
		else
			return false;
	}

	public override int GetHashCode()
	{
		return Tick ^ Value;
	}
}