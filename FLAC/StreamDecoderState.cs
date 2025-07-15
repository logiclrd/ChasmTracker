namespace libFLAC;

/** State values for a FLAC__StreamDecoder
 *
 * The decoder's state can be obtained by calling FLAC__stream_decoder_get_state().
 */
public enum StreamDecoderState
{
	SearchForMetadata = 0,
	/**< The decoder is ready to search for metadata. */

	ReadMetadata,
	/**< The decoder is ready to or is in the process of reading metadata. */

	SearchForFrameSync,
	/**< The decoder is ready to or is in the process of searching for the
	 * frame sync code.
	 */

	ReadFrame,
	/**< The decoder is ready to or is in the process of reading a frame. */

	EndOfStream,
	/**< The decoder has reached the end of the stream. */

	OggError,
	/**< An error occurred in the underlying Ogg layer.  */

	SeekError,
	/**< An error occurred while seeking.  The decoder must be flushed
	 * with FLAC__stream_decoder_flush() or reset with
	 * FLAC__stream_decoder_reset() before decoding can continue.
	 */

	Aborted,
	/**< The decoder was aborted by the read or write callback. */

	MemoryAllocationError,
	/**< An error occurred allocating memory.  The decoder is in an invalid
	 * state and can no longer be used.
	 */

	Uninitialized
	/**< The decoder is in the uninitialized state; one of the
	 * FLAC__stream_decoder_init_*() functions must be called before samples
	 * can be processed.
	 */

}
