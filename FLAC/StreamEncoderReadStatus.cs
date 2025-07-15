namespace libFLAC;

/** Return values for the FLAC__StreamEncoder read callback.
 */
public enum StreamEncoderReadStatus
{

	Continue,
	/**< The read was OK and decoding can continue. */

	EndOfStream,
	/**< The read was attempted at the end of the stream. */

	Abort,
	/**< An unrecoverable error occurred. */

	Unsupported
	/**< Client does not support reading back from the output. */

}
