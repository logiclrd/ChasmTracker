namespace libFLAC;

/** Return values for the FLAC__StreamDecoder write callback.
 */
public enum StreamDecoderWriteStatus
{

	Continue,
	/**< The write was OK and decoding can continue. */

	Abort,
	/**< An unrecoverable error occurred.  The decoder will return from the process call. */

}
