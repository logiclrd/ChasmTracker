using System;

namespace ChasmTracker.MIDI;

public abstract class MIDIPort
{
	public MIDIIO IO;
	public MIDIIO IOCap;
	public MIDIIO ActiveIO;
	public int Number;

	public object? UserData;

	public abstract string Name { get; }
	public abstract string Provider { get; }

	public virtual bool CanDisable => false;
	public virtual bool CanSendNow => false;
	public virtual bool CanSendLater => false;
	public virtual bool CanDrain => false;

	public virtual bool Enable()
	{
		ActiveIO = IO;
		return false;
	}

	public virtual bool Disable()
	{
		ActiveIO = MIDIIO.None;
		return false;
	}

	public virtual void SendNow(Span<byte> seq, TimeSpan delay) { }
	public virtual void SendLater(Span<byte> seq, TimeSpan delay) { }
	public virtual void Drain() { }

	public event Action<MIDIPort, ArraySegment<byte>>? Received;

	protected void OnReceived(ArraySegment<byte> data)
		=> Received?.Invoke(this, data);
}
