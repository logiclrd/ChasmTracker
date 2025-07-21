namespace ChasmTracker.FileTypes.Exporters;

public class MAIFF : AIFF
{
	public override bool IsMulti => true;

	public override string Description => "Audio IFF multi-write";

	public override int SortOrder => 4;
}