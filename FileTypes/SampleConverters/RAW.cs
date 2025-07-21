using ChasmTracker.FileTypes;

namespace Chasm.FileTypes.SampleConverters;

public class RAW : SampleFileConverter
{
	public override string Label => "RAW";
	public override string Description => "Raw";
	public override string Extension => ".raw";

	public override int SortOrder => 6;

	// TODO
}
