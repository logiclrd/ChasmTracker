using System.Runtime.InteropServices;

namespace libFLAC;

/** FLAC SEEKTABLE structure.  (c.f. <A HREF="https://xiph.org/flac/format.html#metadata_block_seektable">format specification</A>)
 *
 * \note From the format specification:
 * - The seek points must be sorted by ascending sample number.
 * - Each seek point's sample number must be the first sample of the
 *   target frame.
 * - Each seek point's sample number must be unique within the table.
 * - Existence of a SEEKTABLE block implies a correct setting of
 *   total_samples in the stream_info block.
 * - Behavior is undefined when more than one SEEKTABLE block is
 *   present in a stream.
 */
[StructLayout(LayoutKind.Sequential)]
public struct StreamMetadata_SeekTable
{
	public int NumPoints;
	[MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)]
	public SeekPoint[] Points;
}

/** SeekPoint structure used in SEEKTABLE blocks.  (c.f. <A HREF="https://xiph.org/flac/format.html#seekpoint">format specification</A>)
 */
[StructLayout(LayoutKind.Sequential)]
public struct SeekPoint
{
	long SampleNumber;
	/**<  The sample number of the target frame. */

	long StreamOffset;
	/**< The offset, in bytes, of the target frame with respect to
	 * beginning of the first frame. */

	int FrameSamples;
	/**< The number of samples in the target frame. */
}