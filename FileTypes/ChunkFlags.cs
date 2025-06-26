namespace ChasmTracker.FileTypes;

public enum ChunkFlags
{
	SizeLittleEndian = 0b01, /* for RIFF */
	Aligned = 0b10, /* are the structures word aligned? */
}
