namespace ChasmTracker.FileTypes;

public class IFFChunk
{
	public uint ID; /* the ID, as a big endian integer. e.g. "wave" == 0x77617665u */
	public int Size;
	public long Offset; /* where in the file the data actually starts */
}
