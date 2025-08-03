using System.Collections.Generic;

namespace ChasmTracker.MIDI;

public abstract class MIDIProvider
{
	public object Sync = new object();
	public bool IsCancelled;

	public virtual int GetPortCount() { return 0; }

	public virtual bool SetUp() { return false; }
	public virtual void Poll() { }
	public virtual void Wake() { }
	public virtual void UnregisterAllPorts() { }
	public virtual void ShutDown() { }
}

public abstract class MIDIProvider<T> : MIDIProvider
	where T : MIDIPort
{
	public readonly List<T> Ports = new List<T>();

	public override int GetPortCount()
	{
		lock (Sync)
			return Ports.Count;
	}
}
