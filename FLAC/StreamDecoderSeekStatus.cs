namespace libFLAC;

/** Return values for the FLAC__StreamDecoder seek callback.
 */
public enum StreamDecoderSeekStatus
{

	OK,
	/**< The seek was OK and decoding can continue. */

	Error,
	/**< An unrecoverable error occurred.  The decoder will return from the process call. */

	Unsupported,
	/**< Client does not support seeking. */

}
