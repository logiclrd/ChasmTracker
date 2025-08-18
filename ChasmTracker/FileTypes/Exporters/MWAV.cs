namespace ChasmTracker.FileTypes.Exporters;

public class MWAV : WAV
{
	public override bool IsMulti => true;

	public override string Description => "WAV multi-write";

	public override int SortOrder => 2;
}