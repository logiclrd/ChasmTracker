namespace ChasmTracker.FileTypes.SongConverters;

public class S3M : SongFileConverter
{
	public override string Label => "S3M";
	public override string Description => "Scream Tracker 3";
	public override string Extension => ".s3m";

	public override int SortOrder => 2;

	// TODO
}
