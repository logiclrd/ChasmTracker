namespace ChasmTracker.FileTypes.Exporters;

public class MFLAC : FLAC
{
	public override bool IsMulti => true;

	public override string Description => "Free Lossless Audio Codec multi-write";

	public override int SortOrder => 6;
}