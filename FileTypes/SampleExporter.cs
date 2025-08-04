using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ChasmTracker.FileTypes;

public abstract class SampleExporter : FileConverter
{
	public virtual bool IsMulti => false;

	public abstract bool ExportHead(Stream fp, int bits, int channels, int rate);
	public abstract bool ExportSilence(Stream fp, int bytes);
	public abstract bool ExportBody(Stream fp, Span<byte> data);
	public abstract bool ExportTail(Stream fp);

	public static IEnumerable<SampleExporter> EnumerateImplementations()
		=> EnumerateImplementationsOfType<SampleExporter>();
	public static SampleExporter? FindImplementation(string label)
		=> EnumerateImplementationsOfType<SampleExporter>(false).FirstOrDefault(t => t.Label == label);
}