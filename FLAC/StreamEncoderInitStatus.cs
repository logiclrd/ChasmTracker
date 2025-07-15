namespace libFLAC;

/** Possible return values for the FLAC__stream_encoder_init_*() functions.
 */
public enum StreamEncoderInitStatus
{

	OK = 0,
	/**< Initialization was successful. */

	EncoderError,
	/**< General failure to set up encoder; call FLAC__stream_encoder_get_state() for cause. */

	UnsupportedContainer,
	/**< The library was not compiled with support for the given container
	 * format.
	 */

	InvalidCallbacks,
	/**< A required callback was not supplied. */

	InvalidNumberOfChannels,
	/**< The encoder has an invalid setting for number of channels. */

	InvalidBitsPerSample,
	/**< The encoder has an invalid setting for bits-per-sample.
	 * FLAC supports 4-32 bps.
	 */

	InvalidSampleRate,
	/**< The encoder has an invalid setting for the input sample rate. */

	InvalidBlockSize,
	/**< The encoder has an invalid setting for the block size. */

	InvalidMaxLPCOrder,
	/**< The encoder has an invalid setting for the maximum LPC order. */

	InvalidQlpCoeffPrecision,
	/**< The encoder has an invalid setting for the precision of the quantized linear predictor coefficients. */

	BlockSizeTooSmallForLPCOrder,
	/**< The specified block size is less than the maximum LPC order. */

	NotStreamable,
	/**< The encoder is bound to the <A HREF="https://xiph.org/flac/format.html#subset">Subset</A> but other settings violate it. */

	InvalidMetadata,
	/**< The metadata input to the encoder is invalid, in one of the following ways:
	 * - FLAC__stream_encoder_set_metadata() was called with a null pointer but a block count > 0
	 * - One of the metadata blocks contains an undefined type
	 * - It contains an illegal CUESHEET as checked by FLAC__format_cuesheet_is_legal()
	 * - It contains an illegal SEEKTABLE as checked by FLAC__format_seektable_is_legal()
	 * - It contains more than one SEEKTABLE block or more than one VORBIS_COMMENT block
	 */

	AlreadyInitialized
	/**< FLAC__stream_encoder_init_*() was called when the encoder was
	 * already initialized, usually because
	 * FLAC__stream_encoder_finish() was not called.
	 */

}