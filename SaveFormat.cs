using System;

using ChasmTracker.FileTypes;

namespace ChasmTracker;

public class SaveFormat
{
	public string Label; // label for the button on the save page
	public string Name; // long name of format
	public string Extension;

	public Func<bool>? IsEnabled;

	public SongFileConverter? SongConverter;
	public InstrumentFileConverter? InstrumentConverter;
	// TODO: exporter functors

	public SaveFormat(string label, string name, string extension)
	{
		Label = label;
		Name = name;
		Extension = extension;
	}
}
