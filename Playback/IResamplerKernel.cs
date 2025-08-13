using System;

public interface IResamplerKernel<TSample>
	where TSample : struct
{
	public void Resample(Span<TSample> oldBuf, Span<TSample> newBuf);
}