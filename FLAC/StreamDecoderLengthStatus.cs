namespace libFLAC;

/** Return values for the FLAC__StreamDecoder length callback.
 */
public enum StreamDecoderLengthStatus
{

	OK,
	/**< The length call was OK and decoding can continue. */

	Error,
	/**< An unrecoverable error occurred.  The decoder will return from the process call. */

	Unsupported,
	/**< Client does not support reporting the length. */

}