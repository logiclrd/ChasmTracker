using System;
using System.Runtime.InteropServices;

namespace libFLAC;

using ChasmTracker.Utility;

public static class NativeMethods
{
	[DllImport("FLAC")]
	public static extern IntPtr FLAC__stream_decoder_new();
	[DllImport("FLAC")]
	public static extern int FLAC__stream_decoder_set_metadata_respond_all(IntPtr decoder);
	[DllImport("FLAC")]
	public static extern StreamDecoderInitStatus FLAC__stream_decoder_init_stream(IntPtr decoder, StreamDecoderReadCallback read_callback, StreamDecoderSeekCallback seek_callback, StreamDecoderTellCallback tell_callback, StreamDecoderLengthCallback length_callback, StreamDecoderEofCallback eof_callback, StreamDecoderWriteCallback write_callback, StreamDecoderMetadataCallback metadata_callback, StreamDecoderErrorCallback error_callback, IntPtr client_data);
	[DllImport("FLAC")]
	public static extern bool FLAC__stream_decoder_process_until_end_of_metadata(IntPtr decoder);
	[DllImport("FLAC")]
	public static extern bool FLAC__stream_decoder_process_until_end_of_stream(IntPtr decoder);
	[DllImport("FLAC")]
	public static extern bool FLAC__stream_decoder_finish(IntPtr decoder);
	[DllImport("FLAC")]
	public static extern void FLAC__stream_decoder_delete(IntPtr decoder);
	[DllImport("FLAC")]
	public static extern StreamDecoderState FLAC__stream_decoder_get_state(IntPtr decoder);

	[DllImport("FLAC")]
	public static extern IntPtr FLAC__stream_encoder_new();
	[DllImport("FLAC")]
	public static extern bool FLAC__stream_encoder_set_channels(IntPtr encoder, int value);
	[DllImport("FLAC")]
	public static extern bool FLAC__stream_encoder_set_bits_per_sample(IntPtr encoder, int value);
	[DllImport("FLAC")]
	public static extern bool FLAC__stream_encoder_set_streamable_subset(IntPtr encoder, bool value);
	[DllImport("FLAC")]
	public static extern bool FLAC__stream_encoder_set_sample_rate(IntPtr encoder, int value);
	[DllImport("FLAC")]
	public static extern bool FLAC__stream_encoder_set_compression_level(IntPtr encoder, int value);
	[DllImport("FLAC")]
	public static extern bool FLAC__stream_encoder_set_total_samples_estimate(IntPtr encoder, long value);
	[DllImport("FLAC")]
	public static extern bool FLAC__stream_encoder_set_verify(IntPtr encoder, bool value);
	[DllImport("FLAC")]
	public static extern StreamEncoderInitStatus FLAC__stream_encoder_init_stream(IntPtr encoder, StreamEncoderWriteCallback write_callback, StreamEncoderSeekCallback seek_callback, StreamEncoderTellCallback tell_callback, StreamEncoderMetadataCallback? metadata_callback, IntPtr client_data);
	[DllImport("FLAC")]
	public static extern bool FLAC__stream_encoder_process_interleaved(IntPtr encoder, int[] buffer, int samples);
	[DllImport("FLAC")]
	public static extern bool FLAC__stream_encoder_finish(IntPtr encoder);
	[DllImport("FLAC")]
	public static extern void FLAC__stream_encoder_delete(IntPtr encoder);
	[DllImport("FLAC")]
	public static extern bool FLAC__stream_encoder_set_metadata(IntPtr encoder, IntPtr[] metadata, int num_blocks);
	[DllImport("FLAC")]
	public static extern bool FLAC__stream_encoder_set_metadata(IntPtr encoder, byte[][] metadata, int num_blocks);

	public static bool FLAC__stream_encoder_set_metadata(IntPtr encoder, StreamMetadata[] metadata, int num_blocks)
	{
		byte[][] serializedMetadata = new byte[metadata.Length][];

		for (int i = 0; i < metadata.Length; i++)
		{
			switch (metadata[i].Type)
			{
				case MetadataType.STREAMINFO:
				{
					var with = new StreamMetadata_with_StreamInfo() { StreamInfo = metadata[i].Data.StreamInfo };
					serializedMetadata[i] = StructureSerializer.MarshalToBytes(with);
					break;
				}
				case MetadataType.PADDING:
				{
					var with = new StreamMetadata_with_Padding() { Padding = metadata[i].Data.Padding };
					serializedMetadata[i] = StructureSerializer.MarshalToBytes(with);
					break;
				}
				case MetadataType.APPLICATION:
				{
					var with = new StreamMetadata_with_Application() { Application = metadata[i].Data.Application };
					serializedMetadata[i] = StructureSerializer.MarshalToBytes(with);
					break;
				}
				case MetadataType.SEEKTABLE:
				{
					var with = new StreamMetadata_with_SeekTable() { SeekTable = metadata[i].Data.SeekTable };
					serializedMetadata[i] = StructureSerializer.MarshalToBytes(with);
					break;
				}
				case MetadataType.VORBISCOMMENT:
				{
					var with = new StreamMetadata_with_VorbisComment() { VorbisComment = metadata[i].Data.VorbisComment };
					serializedMetadata[i] = StructureSerializer.MarshalToBytes(with);
					break;
				}
				case MetadataType.CUESHEET:
				{
					var with = new StreamMetadata_with_CueSheet() { CueSheet = metadata[i].Data.CueSheet };
					serializedMetadata[i] = StructureSerializer.MarshalToBytes(with);
					break;
				}
				case MetadataType.PICTURE:
				{
					var with = new StreamMetadata_with_Picture() { Picture = metadata[i].Data.Picture };
					serializedMetadata[i] = StructureSerializer.MarshalToBytes(with);
					break;
				}
				default: throw new NotSupportedException();
			}
		}

		return FLAC__stream_encoder_set_metadata(encoder, serializedMetadata, num_blocks);
	}

	[DllImport("FLAC")]
	public static extern StreamEncoderState FLAC__stream_encoder_get_state(IntPtr encoder);


	[DllImport("FLAC")]
	public static extern IntPtr FLAC__metadata_object_new(MetadataType type);
	[DllImport("FLAC")]
	public static extern void FLAC__metadata_object_delete(IntPtr @object);
	[DllImport("FLAC")]
	public static extern bool FLAC__metadata_object_application_set_data(IntPtr @object, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] byte[] data, int length, bool copy);
	[DllImport("FLAC")]
	public static extern bool FLAC__metadata_object_vorbiscomment_append_comment(IntPtr @object, VorbisCommentEntry entry, bool copy);

	[DllImport("FLAC")]
	public static extern bool FLAC__format_sample_rate_is_subset(int sample_rate);
}
