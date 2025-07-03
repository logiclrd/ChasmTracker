using System;
using System.IO;

namespace ChasmTracker.FileTypes;

public abstract class SampleExporter
{
	public abstract string Label { get; }
	public abstract string Description { get; }
	public abstract string Extension { get; }

	public virtual bool IsMulti => false;

	public abstract bool ExportHead(Stream fp, int bits, int channels, int rate, int length);
	public abstract bool ExportSilence(Stream fp, int bytes);
	public abstract bool ExportBody(Stream fp, Span<byte> data);
	public abstract bool ExportTail(Stream fp);
}