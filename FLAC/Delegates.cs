using System;
using System.Runtime.InteropServices;

namespace libFLAC;

/** Signature for the read callback.
 *
 *  A function pointer matching this signature must be passed to
 *  FLAC__stream_decoder_init*_stream(). The supplied function will be
 *  called when the decoder needs more input data.  The address of the
 *  buffer to be filled is supplied, along with the number of bytes the
 *  buffer can hold.  The callback may choose to supply less data and
 *  modify the byte count but must be careful not to overflow the buffer.
 *  The callback then returns a status code chosen from
 *  FLAC__StreamDecoderReadStatus.
 *
 * Here is an example of a read callback for stdio streams:
 * \code
 * FLAC__StreamDecoderReadStatus read_cb(const FLAC__StreamDecoder *decoder, FLAC__byte buffer[], size_t *bytes, void *client_data)
 * {
 *   FILE *file = ((MyClientData*)client_data)->file;
 *   if(*bytes > 0) {
 *     *bytes = fread(buffer, sizeof(FLAC__byte), *bytes, file);
 *     if(ferror(file))
 *       return FLAC__STREAM_DECODER_READ_STATUS_ABORT;
 *     else if(*bytes == 0)
 *       return FLAC__STREAM_DECODER_READ_STATUS_END_OF_STREAM;
 *     else
 *       return FLAC__STREAM_DECODER_READ_STATUS_CONTINUE;
 *   }
 *   else
 *     return FLAC__STREAM_DECODER_READ_STATUS_ABORT;
 * }
 * \endcode
 *
 * \note In general, FLAC__StreamDecoder functions which change the
 * state should not be called on the \a decoder while in the callback.
 *
 * \param  decoder  The decoder instance calling the callback.
 * \param  buffer   A pointer to a location for the callee to store
 *                  data to be decoded.
 * \param  bytes    A pointer to the size of the buffer.  On entry
 *                  to the callback, it contains the maximum number
 *                  of bytes that may be stored in \a buffer.  The
 *                  callee must set it to the actual number of bytes
 *                  stored (0 in case of error or end-of-stream) before
 *                  returning.
 * \param  client_data  The callee's client data set through
 *                      FLAC__stream_decoder_init_*().
 * \retval FLAC__StreamDecoderReadStatus
 *    The callee's return status.  Note that the callback should return
 *    \c FLAC__STREAM_DECODER_READ_STATUS_END_OF_STREAM if and only if
 *    zero bytes were read and there is no more data to be read.
 */
public delegate StreamDecoderReadStatus StreamDecoderReadCallback(IntPtr decoder, IntPtr bufferPtr, ref UIntPtr bytes, IntPtr clientData);

/** Signature for the seek callback.
 *
 *  A function pointer matching this signature may be passed to
 *  FLAC__stream_decoder_init*_stream().  The supplied function will be
 *  called when the decoder needs to seek the input stream.  The decoder
 *  will pass the absolute byte offset to seek to, 0 meaning the
 *  beginning of the stream.
 *
 * Here is an example of a seek callback for stdio streams:
 * \code
 * FLAC__StreamDecoderSeekStatus seek_cb(const FLAC__StreamDecoder *decoder, FLAC__uint64 absolute_byte_offset, void *client_data)
 * {
 *   FILE *file = ((MyClientData*)client_data)->file;
 *   if(file == stdin)
 *     return FLAC__STREAM_DECODER_SEEK_STATUS_UNSUPPORTED;
 *   else if(fseeko(file, (off_t)absolute_byte_offset, SEEK_SET) < 0)
 *     return FLAC__STREAM_DECODER_SEEK_STATUS_ERROR;
 *   else
 *     return FLAC__STREAM_DECODER_SEEK_STATUS_OK;
 * }
 * \endcode
 *
 * \note In general, FLAC__StreamDecoder functions which change the
 * state should not be called on the \a decoder while in the callback.
 *
 * \param  decoder  The decoder instance calling the callback.
 * \param  absolute_byte_offset  The offset from the beginning of the stream
 *                               to seek to.
 * \param  client_data  The callee's client data set through
 *                      FLAC__stream_decoder_init_*().
 * \retval FLAC__StreamDecoderSeekStatus
 *    The callee's return status.
 */
public delegate StreamDecoderSeekStatus StreamDecoderSeekCallback(IntPtr decoder, long absoluteByteOffset, IntPtr clientData);

/** Signature for the tell callback.
 *
 *  A function pointer matching this signature may be passed to
 *  FLAC__stream_decoder_init*_stream().  The supplied function will be
 *  called when the decoder wants to know the current position of the
 *  stream.  The callback should return the byte offset from the
 *  beginning of the stream.
 *
 * Here is an example of a tell callback for stdio streams:
 * \code
 * FLAC__StreamDecoderTellStatus tell_cb(const FLAC__StreamDecoder *decoder, FLAC__uint64 *absolute_byte_offset, void *client_data)
 * {
 *   FILE *file = ((MyClientData*)client_data)->file;
 *   off_t pos;
 *   if(file == stdin)
 *     return FLAC__STREAM_DECODER_TELL_STATUS_UNSUPPORTED;
 *   else if((pos = ftello(file)) < 0)
 *     return FLAC__STREAM_DECODER_TELL_STATUS_ERROR;
 *   else {
 *     *absolute_byte_offset = (FLAC__uint64)pos;
 *     return FLAC__STREAM_DECODER_TELL_STATUS_OK;
 *   }
 * }
 * \endcode
 *
 * \note In general, FLAC__StreamDecoder functions which change the
 * state should not be called on the \a decoder while in the callback.
 *
 * \param  decoder  The decoder instance calling the callback.
 * \param  absolute_byte_offset  A pointer to storage for the current offset
 *                               from the beginning of the stream.
 * \param  client_data  The callee's client data set through
 *                      FLAC__stream_decoder_init_*().
 * \retval FLAC__StreamDecoderTellStatus
 *    The callee's return status.
 */

public delegate StreamDecoderTellStatus StreamDecoderTellCallback(IntPtr decoder, out long absoluteByteOffset, IntPtr clientData);
/** Signature for the length callback.
 *
 *  A function pointer matching this signature may be passed to
 *  FLAC__stream_decoder_init*_stream().  The supplied function will be
 *  called when the decoder wants to know the total length of the stream
 *  in bytes.
 *
 * Here is an example of a length callback for stdio streams:
 * \code
 * FLAC__StreamDecoderLengthStatus length_cb(const FLAC__StreamDecoder *decoder, FLAC__uint64 *stream_length, void *client_data)
 * {
 *   FILE *file = ((MyClientData*)client_data)->file;
 *   struct stat filestats;
 *
 *   if(file == stdin)
 *     return FLAC__STREAM_DECODER_LENGTH_STATUS_UNSUPPORTED;
 *   else if(fstat(fileno(file), &filestats) != 0)
 *     return FLAC__STREAM_DECODER_LENGTH_STATUS_ERROR;
 *   else {
 *     *stream_length = (FLAC__uint64)filestats.st_size;
 *     return FLAC__STREAM_DECODER_LENGTH_STATUS_OK;
 *   }
 * }
 * \endcode
 *
 * \note In general, FLAC__StreamDecoder functions which change the
 * state should not be called on the \a decoder while in the callback.
 *
 * \param  decoder  The decoder instance calling the callback.
 * \param  stream_length  A pointer to storage for the length of the stream
 *                        in bytes.
 * \param  client_data  The callee's client data set through
 *                      FLAC__stream_decoder_init_*().
 * \retval FLAC__StreamDecoderLengthStatus
 *    The callee's return status.
 */
public delegate StreamDecoderLengthStatus StreamDecoderLengthCallback(IntPtr decoder, out long streamLength, IntPtr clientData);

/** Signature for the EOF callback.
 *
 *  A function pointer matching this signature may be passed to
 *  FLAC__stream_decoder_init*_stream().  The supplied function will be
 *  called when the decoder needs to know if the end of the stream has
 *  been reached.
 *
 * Here is an example of a EOF callback for stdio streams:
 * FLAC__bool eof_cb(const FLAC__StreamDecoder *decoder, void *client_data)
 * \code
 * {
 *   FILE *file = ((MyClientData*)client_data)->file;
 *   return feof(file)? true : false;
 * }
 * \endcode
 *
 * \note In general, FLAC__StreamDecoder functions which change the
 * state should not be called on the \a decoder while in the callback.
 *
 * \param  decoder  The decoder instance calling the callback.
 * \param  client_data  The callee's client data set through
 *                      FLAC__stream_decoder_init_*().
 * \retval FLAC__bool
 *    \c true if the currently at the end of the stream, else \c false.
 */
public delegate bool StreamDecoderEofCallback(IntPtr decoder, IntPtr clientData);

/** Signature for the write callback.
 *
 *  A function pointer matching this signature must be passed to one of
 *  the FLAC__stream_decoder_init_*() functions.
 *  The supplied function will be called when the decoder has decoded a
 *  single audio frame.  The decoder will pass the frame metadata as well
 *  as an array of pointers (one for each channel) pointing to the
 *  decoded audio.
 *
 * \note In general, FLAC__StreamDecoder functions which change the
 * state should not be called on the \a decoder while in the callback.
 *
 * \param  decoder  The decoder instance calling the callback.
 * \param  frame    The description of the decoded frame.  See
 *                  FLAC__Frame.
 * \param  buffer   An array of pointers to decoded channels of data.
 *                  Each pointer will point to an array of signed
 *                  samples of length \a frame->header.blocksize.
 *                  Channels will be ordered according to the FLAC
 *                  specification; see the documentation for the
 *                  <A HREF="https://xiph.org/flac/format.html#frame_header">frame header</A>.
 * \param  client_data  The callee's client data set through
 *                      FLAC__stream_decoder_init_*().
 * \retval FLAC__StreamDecoderWriteStatus
 *    The callee's return status.
 */
public delegate StreamDecoderWriteStatus StreamDecoderWriteCallback(IntPtr decoder, Frame frame, [MarshalAs(UnmanagedType.LPArray, SizeConst = FLACConstants.MaxChannels)] IntPtr[] buffer, IntPtr clientData);

/** Signature for the metadata callback.
 *
 *  A function pointer matching this signature must be passed to one of
 *  the FLAC__stream_decoder_init_*() functions.
 *  The supplied function will be called when the decoder has decoded a
 *  metadata block.  In a valid FLAC file there will always be one
 *  \c STREAMINFO block, followed by zero or more other metadata blocks.
 *  These will be supplied by the decoder in the same order as they
 *  appear in the stream and always before the first audio frame (i.e.
 *  write callback).  The metadata block that is passed in must not be
 *  modified, and it doesn't live beyond the callback, so you should make
 *  a copy of it with FLAC__metadata_object_clone() if you will need it
 *  elsewhere.  Since metadata blocks can potentially be large, by
 *  default the decoder only calls the metadata callback for the
 *  \c STREAMINFO block; you can instruct the decoder to pass or filter
 *  other blocks with FLAC__stream_decoder_set_metadata_*() calls.
 *
 * \note In general, FLAC__StreamDecoder functions which change the
 * state should not be called on the \a decoder while in the callback.
 *
 * \param  decoder  The decoder instance calling the callback.
 * \param  metadata The decoded metadata block.
 * \param  client_data  The callee's client data set through
 *                      FLAC__stream_decoder_init_*().
 */
public delegate void StreamDecoderMetadataCallback(IntPtr decoder, /* const FLAC__StreamMetadata * */ IntPtr metadata, IntPtr clientData);

/** Signature for the error callback.
 *
 *  A function pointer matching this signature must be passed to one of
 *  the FLAC__stream_decoder_init_*() functions.
 *  The supplied function will be called whenever an error occurs during
 *  decoding.
 *
 * \note In general, FLAC__StreamDecoder functions which change the
 * state should not be called on the \a decoder while in the callback.
 *
 * \param  decoder  The decoder instance calling the callback.
 * \param  status   The error encountered by the decoder.
 * \param  client_data  The callee's client data set through
 *                      FLAC__stream_decoder_init_*().
 */
public delegate void StreamDecoderErrorCallback(IntPtr decoder, StreamDecoderErrorStatus status, IntPtr clientData);

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

/** Signature for the read callback.
 *
 *  A function pointer matching this signature must be passed to
 *  FLAC__stream_encoder_init_ogg_stream() if seeking is supported.
 *  The supplied function will be called when the encoder needs to read back
 *  encoded data.  This happens during the metadata callback, when the encoder
 *  has to read, modify, and rewrite the metadata (e.g. seekpoints) gathered
 *  while encoding.  The address of the buffer to be filled is supplied, along
 *  with the number of bytes the buffer can hold.  The callback may choose to
 *  supply less data and modify the byte count but must be careful not to
 *  overflow the buffer.  The callback then returns a status code chosen from
 *  FLAC__StreamEncoderReadStatus.
 *
 * Here is an example of a read callback for stdio streams:
 * \code
 * FLAC__StreamEncoderReadStatus read_cb(const FLAC__StreamEncoder *encoder, FLAC__byte buffer[], size_t *bytes, void *client_data)
 * {
 *   FILE *file = ((MyClientData*)client_data)->file;
 *   if(*bytes > 0) {
 *     *bytes = fread(buffer, sizeof(FLAC__byte), *bytes, file);
 *     if(ferror(file))
 *       return FLAC__STREAM_ENCODER_READ_STATUS_ABORT;
 *     else if(*bytes == 0)
 *       return FLAC__STREAM_ENCODER_READ_STATUS_END_OF_STREAM;
 *     else
 *       return FLAC__STREAM_ENCODER_READ_STATUS_CONTINUE;
 *   }
 *   else
 *     return FLAC__STREAM_ENCODER_READ_STATUS_ABORT;
 * }
 * \endcode
 *
 * \note In general, FLAC__StreamEncoder functions which change the
 * state should not be called on the \a encoder while in the callback.
 *
 * \param  encoder  The encoder instance calling the callback.
 * \param  buffer   A pointer to a location for the callee to store
 *                  data to be encoded.
 * \param  bytes    A pointer to the size of the buffer.  On entry
 *                  to the callback, it contains the maximum number
 *                  of bytes that may be stored in \a buffer.  The
 *                  callee must set it to the actual number of bytes
 *                  stored (0 in case of error or end-of-stream) before
 *                  returning.
 * \param  client_data  The callee's client data set through
 *                      FLAC__stream_encoder_set_client_data().
 * \retval FLAC__StreamEncoderReadStatus
 *    The callee's return status.
 */
public delegate StreamEncoderReadStatus StreamEncoderReadCallback(IntPtr encoder, IntPtr bufferPtr, ref UIntPtr bytes, IntPtr clientData);

/** Signature for the write callback.
 *
 *  A function pointer matching this signature must be passed to
 *  FLAC__stream_encoder_init*_stream().  The supplied function will be called
 *  by the encoder anytime there is raw encoded data ready to write.  It may
 *  include metadata mixed with encoded audio frames and the data is not
 *  guaranteed to be aligned on frame or metadata block boundaries.
 *
 *  The only duty of the callback is to write out the \a bytes worth of data
 *  in \a buffer to the current position in the output stream.  The arguments
 *  \a samples and \a current_frame are purely informational.  If \a samples
 *  is greater than \c 0, then \a current_frame will hold the current frame
 *  number that is being written; otherwise it indicates that the write
 *  callback is being called to write metadata.
 *
 * \note
 * Unlike when writing to native FLAC, when writing to Ogg FLAC the
 * write callback will be called twice when writing each audio
 * frame; once for the page header, and once for the page body.
 * When writing the page header, the \a samples argument to the
 * write callback will be \c 0.
 *
 * \note In general, FLAC__StreamEncoder functions which change the
 * state should not be called on the \a encoder while in the callback.
 *
 * \param  encoder  The encoder instance calling the callback.
 * \param  buffer   An array of encoded data of length \a bytes.
 * \param  bytes    The byte length of \a buffer.
 * \param  samples  The number of samples encoded by \a buffer.
 *                  \c 0 has a special meaning; see above.
 * \param  current_frame  The number of the current frame being encoded.
 * \param  client_data  The callee's client data set through
 *                      FLAC__stream_encoder_init_*().
 * \retval FLAC__StreamEncoderWriteStatus
 *    The callee's return status.
 */
public delegate StreamEncoderWriteStatus StreamEncoderWriteCallback(IntPtr encoder, IntPtr bufferPtr, UIntPtr bytes, uint samples, uint currentFrame, IntPtr clientData);

/** Signature for the seek callback.
 *
 *  A function pointer matching this signature may be passed to
 *  FLAC__stream_encoder_init*_stream().  The supplied function will be called
 *  when the encoder needs to seek the output stream.  The encoder will pass
 *  the absolute byte offset to seek to, 0 meaning the beginning of the stream.
 *
 * Here is an example of a seek callback for stdio streams:
 * \code
 * FLAC__StreamEncoderSeekStatus seek_cb(const FLAC__StreamEncoder *encoder, FLAC__uint64 absolute_byte_offset, void *client_data)
 * {
 *   FILE *file = ((MyClientData*)client_data)->file;
 *   if(file == stdin)
 *     return FLAC__STREAM_ENCODER_SEEK_STATUS_UNSUPPORTED;
 *   else if(fseeko(file, (off_t)absolute_byte_offset, SEEK_SET) < 0)
 *     return FLAC__STREAM_ENCODER_SEEK_STATUS_ERROR;
 *   else
 *     return FLAC__STREAM_ENCODER_SEEK_STATUS_OK;
 * }
 * \endcode
 *
 * \note In general, FLAC__StreamEncoder functions which change the
 * state should not be called on the \a encoder while in the callback.
 *
 * \param  encoder  The encoder instance calling the callback.
 * \param  absolute_byte_offset  The offset from the beginning of the stream
 *                               to seek to.
 * \param  client_data  The callee's client data set through
 *                      FLAC__stream_encoder_init_*().
 * \retval FLAC__StreamEncoderSeekStatus
 *    The callee's return status.
 */
public delegate StreamEncoderSeekStatus StreamEncoderSeekCallback(IntPtr encoder, long absoluteByteOffset, IntPtr clientData);

/** Signature for the tell callback.
 *
 *  A function pointer matching this signature may be passed to
 *  FLAC__stream_encoder_init*_stream().  The supplied function will be called
 *  when the encoder needs to know the current position of the output stream.
 *
 * \warning
 * The callback must return the true current byte offset of the output to
 * which the encoder is writing.  If you are buffering the output, make
 * sure and take this into account.  If you are writing directly to a
 * FILE* from your write callback, ftell() is sufficient.  If you are
 * writing directly to a file descriptor from your write callback, you
 * can use lseek(fd, SEEK_CUR, 0).  The encoder may later seek back to
 * these points to rewrite metadata after encoding.
 *
 * Here is an example of a tell callback for stdio streams:
 * \code
 * FLAC__StreamEncoderTellStatus tell_cb(const FLAC__StreamEncoder *encoder, FLAC__uint64 *absolute_byte_offset, void *client_data)
 * {
 *   FILE *file = ((MyClientData*)client_data)->file;
 *   off_t pos;
 *   if(file == stdin)
 *     return FLAC__STREAM_ENCODER_TELL_STATUS_UNSUPPORTED;
 *   else if((pos = ftello(file)) < 0)
 *     return FLAC__STREAM_ENCODER_TELL_STATUS_ERROR;
 *   else {
 *     *absolute_byte_offset = (FLAC__uint64)pos;
 *     return FLAC__STREAM_ENCODER_TELL_STATUS_OK;
 *   }
 * }
 * \endcode
 *
 * \note In general, FLAC__StreamEncoder functions which change the
 * state should not be called on the \a encoder while in the callback.
 *
 * \param  encoder  The encoder instance calling the callback.
 * \param  absolute_byte_offset  The address at which to store the current
 *                               position of the output.
 * \param  client_data  The callee's client data set through
 *                      FLAC__stream_encoder_init_*().
 * \retval FLAC__StreamEncoderTellStatus
 *    The callee's return status.
 */
public delegate StreamEncoderTellStatus StreamEncoderTellCallback(IntPtr encoder, out long absoluteByteOffset, IntPtr clientData);

/** Signature for the metadata callback.
 *
 *  A function pointer matching this signature may be passed to
 *  FLAC__stream_encoder_init*_stream().  The supplied function will be called
 *  once at the end of encoding with the populated STREAMINFO structure.  This
 *  is so the client can seek back to the beginning of the file and write the
 *  STREAMINFO block with the correct statistics after encoding (like
 *  minimum/maximum frame size and total samples).
 *
 * \note In general, FLAC__StreamEncoder functions which change the
 * state should not be called on the \a encoder while in the callback.
 *
 * \param  encoder      The encoder instance calling the callback.
 * \param  metadata     The final populated STREAMINFO block.
 * \param  client_data  The callee's client data set through
 *                      FLAC__stream_encoder_init_*().
 */
public delegate void StreamEncoderMetadataCallback(IntPtr encoder, StreamMetadata metadata, IntPtr clientData);

/** Signature for the progress callback.
 *
 *  A function pointer matching this signature may be passed to
 *  FLAC__stream_encoder_init*_file() or FLAC__stream_encoder_init*_FILE().
 *  The supplied function will be called when the encoder has finished
 *  writing a frame.  The \c total_frames_estimate argument to the
 *  callback will be based on the value from
 *  FLAC__stream_encoder_set_total_samples_estimate().
 *
 * \note In general, FLAC__StreamEncoder functions which change the
 * state should not be called on the \a encoder while in the callback.
 *
 * \param  encoder          The encoder instance calling the callback.
 * \param  bytes_written    Bytes written so far.
 * \param  samples_written  Samples written so far.
 * \param  frames_written   Frames written so far.
 * \param  total_frames_estimate  The estimate of the total number of
 *                                frames to be written.
 * \param  client_data      The callee's client data set through
 *                          FLAC__stream_encoder_init_*().
 */
public delegate void StreamEncoderProgressCallback(IntPtr encoder, long bytesWritten, long samplesWritten, int framesWritten, int totalFrames_estimate, IntPtr clientData);
