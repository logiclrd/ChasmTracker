namespace libFLAC;

/** Return values for the FLAC__StreamDecoder tell callback.
 */
public enum StreamDecoderTellStatus
{

	OK,
	/**< The tell was OK and decoding can continue. */

	Error,
	/**< An unrecoverable error occurred.  The decoder will return from the process call. */

	Unsupported,
	/**< Client does not support telling the position. */

}
