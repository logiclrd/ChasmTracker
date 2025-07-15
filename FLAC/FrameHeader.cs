using System.Runtime.InteropServices;

namespace libFLAC;

[StructLayout(LayoutKind.Sequential)]
public class FrameHeader
{
	public int BlockSize;
	/**< The number of samples per subframe. */

	public int SampleRate;
	/**< The sample rate in Hz. */

	public int Channels;
	/**< The number of channels (== number of subframes). */

	public ChannelAssignment ChannelAssignment;
	/**< The channel assignment for the frame. */

	public int BitsPerSample;
	/**< The sample resolution. */

	public FrameNumberType NumberType;
	/**< The numbering scheme used for the frame.  As a convenience, the
	 * decoder will always convert a frame number to a sample number because
	 * the rules are complex. */

	public long FrameOrSampleNumber;

	/**< The frame number or sample number of first sample in frame;
	 * use the \a number_type value to determine which to use. */

	public byte CRC;
	/**< CRC-8 (polynomial = x^8 + x^2 + x^1 + x^0, initialized with 0)
	 * of the raw frame header bytes, meaning everything before the CRC byte
	 * including the sync code.
	 */
}
