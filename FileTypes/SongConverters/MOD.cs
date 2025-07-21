namespace ChasmTracker.FileTypes.SongConverters;

public class MOD : SongFileConverter
{
	public override string Label => "MOD";
	public override string Description => "Amiga ProTracker";
	public override string Extension => ".mod";

	public override int SortOrder => 3;

	// TODO
}