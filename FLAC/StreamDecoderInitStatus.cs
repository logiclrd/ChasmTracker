namespace libFLAC;

/** Possible return values for the FLAC__stream_decoder_init_*() functions.
 */
public enum StreamDecoderInitStatus
{
	OK = 0,
	/**< Initialization was successful. */

	UnsupportedContainer,
	/**< The library was not compiled with support for the given container
	 * format.
	 */

	InvalidCallbacks,
	/**< A required callback was not supplied. */

	MemoryAllocationError,
	/**< An error occurred allocating memory. */

	ErrorOpeningFile,
	/**< fopen() failed in FLAC__stream_decoder_init_file() or
	 * FLAC__stream_decoder_init_ogg_file(). */

	AlreadyInitialized,
	/**< FLAC__stream_decoder_init_*() was called when the decoder was
	 * already initialized, usually because
	 * FLAC__stream_decoder_finish() was not called.
	 */
}
