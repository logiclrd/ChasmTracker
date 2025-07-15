namespace libFLAC;

/** State values for a FLAC__StreamEncoder.
 *
 * The encoder's state can be obtained by calling FLAC__stream_encoder_get_state().
 *
 * If the encoder gets into any other state besides \c FLAC__STREAM_ENCODER_OK
 * or \c FLAC__STREAM_ENCODER_UNINITIALIZED, it becomes invalid for encoding and
 * must be deleted with FLAC__stream_encoder_delete().
 */
public enum StreamEncoderState
{

	OK = 0,
	/**< The encoder is in the normal OK state and samples can be processed. */

	Uninitialized,
	/**< The encoder is in the uninitialized state; one of the
	 * FLAC__stream_encoder_init_*() functions must be called before samples
	 * can be processed.
	 */

	OGGError,
	/**< An error occurred in the underlying Ogg layer.  */

	VerifyDecoderError,
	/**< An error occurred in the underlying verify stream decoder;
	 * check FLAC__stream_encoder_get_verify_decoder_state().
	 */

	VerifyMismatchInAudioData,
	/**< The verify decoder detected a mismatch between the original
	 * audio signal and the decoded audio signal.
	 */

	ClientError,
	/**< One of the callbacks returned a fatal error. */

	IoError,
	/**< An I/O error occurred while opening/reading/writing a file.
	 * Check \c errno.
	 */

	FramingError,
	/**< An error occurred while writing the stream; usually, the
	 * write_callback returned an error.
	 */

	MemoryAllocationError
	/**< Memory allocation failed. */

}
