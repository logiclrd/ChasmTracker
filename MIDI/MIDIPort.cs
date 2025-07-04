using System;

namespace ChasmTracker.MIDI;

public abstract class MIDIPort
{
	public MIDIIO IO;
	public MIDIIO IOCap;
	public int Number;

	public byte[]? UserData;

	public abstract string Name { get; }
	public abstract string Provider { get; }

	public virtual bool CanDisable => false;
	public virtual bool CanSendNow => false;
	public virtual bool CanSendLater => false;
	public virtual bool CanDrain => false;

	public virtual void Poll() { }
	public virtual bool Enable() { return false; }
	public virtual bool Disable() { return false; }
	public virtual void SendNow(byte[] seq, int len, TimeSpan delay) { }
	public virtual void SendLater(byte[] seq, int len, TimeSpan delay) { }
	public virtual void Drain() { }

	public event Action<MIDIPort, byte[]>? Received;
}
