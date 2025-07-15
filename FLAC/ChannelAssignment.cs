namespace libFLAC;

/** An enumeration of the available channel assignments. */
public enum ChannelAssignment
{
	Independent = 0, /**< independent channels */
	LeftSide = 1, /**< left+side stereo */
	RightSide = 2, /**< right+side stereo */
	MidSide = 3 /**< mid+side stereo */
}
