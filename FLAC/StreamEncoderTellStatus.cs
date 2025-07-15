namespace libFLAC;

/** Return values for the FLAC__StreamEncoder tell callback.
 */
public enum StreamEncoderTellStatus
{

	OK,
	/**< The tell was OK and encoding can continue. */

	Error,
	/**< An unrecoverable error occurred. */

	Unsupported,
	/**< Client does not support seeking. */

}