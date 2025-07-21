namespace ChasmTracker.FileTypes.SampleConverters;

public class S3I : SampleFileConverter
{
	public override string Label => "S3I";
	public override string Description => "Scream Tracker";
	public override string Extension => ".s3i";

	public override int SortOrder => 2;

	// TODO
}
