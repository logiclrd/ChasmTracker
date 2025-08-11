using System;

namespace ChasmTracker.FM;

public abstract class FMDriver
{
	public abstract void ShutDown();

	public abstract void ResetChip();

	public abstract bool Write(int a, int v);
	public abstract byte Read(int a);

	public abstract bool TimerOver(int c);
	public abstract void UpdateMulti(Memory<short>?[] buffers, uint[] vuMax);

	public event OPLTimerHandler? TimerHandler;
	public event OPLIRQHandler? IRQHandler;
	public event OPLUpdateHandler? UpdateHandler;

	protected void OnTimer(int timer, double period)
	{
		TimerHandler?.Invoke(timer, period);
	}

	protected void OnIRQ(bool irq)
	{
		IRQHandler?.Invoke(irq);
	}

	protected void OnUpdate(int minIntervalMicroseconds)
	{
		UpdateHandler?.Invoke(minIntervalMicroseconds);
	}
}
