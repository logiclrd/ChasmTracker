using System;

using System.IO;

namespace ChasmTracker.DiskOutput;

public class MultiWrite
{
	public int[] Buffer = new int[Constants.MixBufferSize];

	public DiskWriter? Sink;

	public bool IsUsed = false;

	/* Conveniently, this has the same prototype as disko_write :) */
	public void Write(Span<byte> buf) => Sink?.Write(buf);
	/* this is optimization for channels that haven't had any data yet
	(nothing to convert/write, just seek ahead in the data stream) */
	public void Silence(int bytes) => Sink?.Silence(bytes);
}
