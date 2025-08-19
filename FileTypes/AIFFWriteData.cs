namespace ChasmTracker.FileTypes;

public class AIFFWriteData
{
	public long StartOffset;
	public long COMMFramesOffset; // seek position for writing header data
	public long SSNDSizeOffset; // seek position for writing header data
	public long NumBytes; // how many bytes have been written
	public int BytesPerSample; // bytes per sample
	public bool BigEndian; // should be byteswapped?
	public int BytesPerFrame; // bytes per frame
}
