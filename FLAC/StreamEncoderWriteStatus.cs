namespace libFLAC;

/** Return values for the FLAC__StreamEncoder write callback.
 */
public enum StreamEncoderWriteStatus
{

	OK = 0,
	/**< The write was OK and encoding can continue. */

	FatalError,
	/**< An unrecoverable error occurred.  The encoder will return from the process call. */

}
