namespace libFLAC;

/** Return values for the FLAC__StreamEncoder seek callback.
 */
public enum StreamEncoderSeekStatus
{

	OK,
	/**< The seek was OK and encoding can continue. */

	Error,
	/**< An unrecoverable error occurred. */

	Unsupported,
	/**< Client does not support seeking. */

}
