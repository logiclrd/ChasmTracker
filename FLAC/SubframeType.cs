namespace libFLAC;

/** An enumeration of the available subframe types. */
public enum SubframeType
{
	Constant = 0, /**< constant signal */
	Verbatim = 1, /**< uncompressed signal */
	Fixed = 2, /**< fixed polynomial prediction */
	LPC = 3 /**< linear prediction */
}
