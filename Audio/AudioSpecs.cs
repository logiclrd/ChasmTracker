using System;

namespace ChasmTracker.Audio;

/* 8-bit audio is always unsigned; everything else is signed. */
public class AudioSpecs
{
	public int Frequency; /* sample rate */
	public int Bits; /* one of [8, 16, 32], always system byte order */
	public int Channels; /* channels */
	public ushort BufferSizeSamples; /* buffer size in samples */

	public Action<Memory<byte>>? Callback;
}
