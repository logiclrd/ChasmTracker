using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using ChasmTracker.Songs;
using ChasmTracker.Utility;
using libFLAC;

namespace ChasmTracker.FileTypes;

public class FLACDecoder : IDisposable
{
	public SongSample? Sample;

	public IntPtr Decoder;

	public int Bits; /* 8, 16, or 32 (24 gets rounded up) */
	public int Channels; /* channels in the stream */

	/* STREAM INFO */
	public int StreamBits; /* actual bitsize of the stream */
	public int Offset; /* used while decoding */

	public Stream? InputStream;
	public MemoryStream? OutputStream;

	byte[] _readBuffer = new byte[8192];
	bool _isEOF;

	public bool Initialize(Stream inputStream)
	{
		InputStream = inputStream;

		Decoder = NativeMethods.FLAC__stream_decoder_new();

		if (Decoder == IntPtr.Zero)
			return false;

		NativeMethods.FLAC__stream_decoder_set_metadata_respond_all(Decoder);

		var xx =
			NativeMethods.FLAC__stream_decoder_init_stream(
				Decoder,
				OnRead, OnSeek,
				OnTell, OnLength,
				OnEOF, OnWrite,
				OnMeta, OnError,
				IntPtr.Zero
			);

		if (xx != StreamDecoderInitStatus.OK)
			return false;

		return true;
	}

	StreamDecoderReadStatus OnRead(IntPtr decoder, IntPtr bufferPtr, ref UIntPtr bytes, IntPtr clientData)
	{
		if (bytes <= 0)
			return StreamDecoderReadStatus.Abort;
		if (InputStream == null)
			return StreamDecoderReadStatus.Abort;

		try
		{
			if (_readBuffer.Length < (int)bytes)
				_readBuffer = new byte[bytes * 2];

			int numRead = InputStream.Read(_readBuffer, 0, (int)bytes);

			Marshal.Copy(_readBuffer, 0, bufferPtr, numRead);

			bytes = (UIntPtr)numRead;

			if (numRead == 0)
			{
				_isEOF = true;
				return StreamDecoderReadStatus.EndOfStream;
			}
			else
				return StreamDecoderReadStatus.Continue;
		}
		catch
		{
			return StreamDecoderReadStatus.Abort;
		}
	}

	StreamDecoderSeekStatus OnSeek(IntPtr decoder, long absoluteByteOffset, IntPtr clientData)
	{
		if (InputStream == null)
			return StreamDecoderSeekStatus.Error;
		if (!InputStream.CanSeek)
			return StreamDecoderSeekStatus.Unsupported;

		try
		{
			InputStream.Position = absoluteByteOffset;

			_isEOF = (InputStream.Position < InputStream.Length);

			return StreamDecoderSeekStatus.OK;
		}
		catch
		{
			return StreamDecoderSeekStatus.Error;
		}
	}

	StreamDecoderTellStatus OnTell(IntPtr decoder, out long absoluteByteOffset, IntPtr clientData)
	{
		if (InputStream == null)
		{
			absoluteByteOffset = -1;
			return StreamDecoderTellStatus.Error;
		}

		try
		{
			absoluteByteOffset = InputStream.Position;
			return StreamDecoderTellStatus.OK;
		}
		catch
		{
			absoluteByteOffset = -1;
			return StreamDecoderTellStatus.Error;
		}
	}

	StreamDecoderLengthStatus OnLength(IntPtr decoder, out long streamLength, IntPtr clientData)
	{
		if (InputStream == null)
		{
			streamLength = -1;
			return StreamDecoderLengthStatus.Error;
		}

		try
		{
			streamLength = InputStream.Length;
			return StreamDecoderLengthStatus.OK;
		}
		catch
		{
			streamLength = -1;
			return StreamDecoderLengthStatus.Error;
		}
	}

	bool OnEOF(IntPtr decoder, IntPtr clientData)
	{
		return _isEOF;
	}

	StreamDecoderWriteStatus OnWrite(IntPtr decoder, Frame frame, [MarshalAs(UnmanagedType.LPArray, SizeConst = FLACConstants.MaxChannels)] IntPtr[] buffer, IntPtr clientData)
	{
		if ((Sample == null) || (OutputStream == null))
			return StreamDecoderWriteStatus.Abort;

		if (frame.Header.Channels < Channels)
			return StreamDecoderWriteStatus.Abort;

		/* invalid?; FIXME: this should probably make sure the total_samples
		* is less than the max sample constant thing */
		if ((Sample.Length == 0) || (Sample.Length > Constants.MaxSampleLength)
			|| Channels > 2
			|| Bits > 32)
			return StreamDecoderWriteStatus.Abort;

		if (frame.Header.FrameOrSampleNumber == 0)
			Offset = 0;

		byte[] sampleBuffer = new byte[Bits / 8];

		int bitShift = Bits - StreamBits;

		switch (Bits)
		{
			case 8:
				for (int i = 0; i < frame.Header.BlockSize; i++)
					for (int c = 0; c < Channels; c++)
					{
						int sample = Marshal.ReadInt32(buffer[c], i * 4);

						sample <<= bitShift;

						OutputStream.WriteByte(unchecked((byte)sample));
					}
				break;
			case 16:
				for (int i = 0; i < frame.Header.BlockSize; i++)
					for (int c = 0; c < Channels; c++)
					{
						int sample = Marshal.ReadInt32(buffer[c], i * 4);

						sample <<= bitShift;

						BitConverter.TryWriteBytes(sampleBuffer, unchecked((short)sample));

						OutputStream.Write(sampleBuffer);
					}
				break;
			case 32:
				for (int i = 0; i < frame.Header.BlockSize; i++)
					for (int c = 0; c < Channels; c++)
					{
						int sample = Marshal.ReadInt32(buffer[c], i * 4);

						sample <<= bitShift;

						BitConverter.TryWriteBytes(sampleBuffer, sample);

						OutputStream.Write(sampleBuffer);
					}
				break;
		}

		Offset += frame.Header.BlockSize * Channels;

		return StreamDecoderWriteStatus.Continue;
	}

	/* this should probably be elsewhere
	* note: maybe bswap.h and bshift.h should be merged to a bits.h */
	static int CeilPow2_32(int xx)
	{
		uint x = unchecked((uint)xx);

		/* from Bit Twiddling Hacks */
		x--;
		x |= x >> 1;
		x |= x >> 2;
		x |= x >> 4;
		x |= x >> 8;
		x |= x >> 16;
		x++;

		return unchecked((int)x);
	}

	unsafe void OnMeta(IntPtr decoder, /* const FLAC__StreamMetadata * */ IntPtr metadata, IntPtr clientData)
	{
		if (Sample == null)
			return;

		MetadataType type = (MetadataType)Marshal.ReadInt32(metadata);

		switch (type)
		{
			case MetadataType.STREAMINFO:
			{
				/* supposedly, this is always first */
				var streaminfo = Marshal.PtrToStructure<StreamMetadata_with_StreamInfo>(metadata)!.StreamInfo;

				/* copy */
				Sample.C5Speed = streaminfo.SampleRate;
				Sample.Length = (int)streaminfo.TotalSamples;

				StreamBits = streaminfo.BitsPerSample;
				Channels = streaminfo.Channels;

				Bits = CeilPow2_32(StreamBits);

				break;
			}
			case MetadataType.VORBISCOMMENT:
			{
				var vorbisComment = Marshal.PtrToStructure<StreamMetadata_with_VorbisComment>(metadata)!.VorbisComment;

				int loopStart = -1, loopLength = -1;

				for (int i = 0; i < vorbisComment.NumComments; i++)
				{
					string s = Marshal.PtrToStringAnsi(
						vorbisComment.Comments[i].Entry,
						vorbisComment.Comments[i].Length);

					int separator = s.IndexOf('=');

					if (separator < 0)
						continue;

					string parameterName = s.Substring(0, separator);
					string parameterValue = s.Substring(separator + 1);

					bool isIntParameter = int.TryParse(parameterValue, out int parameterIntValue);

					if (parameterName == "TITLE") Sample.Name = s.Substring(6);
					else if ((parameterName == "SAMPLERATE") && isIntParameter) Sample.C5Speed = parameterIntValue;
					else if ((parameterName == "LOOPSTART") && isIntParameter) loopStart = parameterIntValue;
					else if ((parameterName == "LOOPLENGTH") && isIntParameter) loopLength = parameterIntValue;
					else
					{
#if false
						Log.Append(5, " FLAC: unknown vorbis comment '" + s + "'");
#endif
					}
				}

				if (loopStart > 0 && loopLength > 1)
				{
					Sample.Flags |= SampleFlags.Loop;

					Sample.LoopStart = loopStart;
					Sample.LoopEnd = loopStart + loopLength;
				}

				break;
			}
			case MetadataType.APPLICATION:
			{
				var parsedMetadata = Marshal.PtrToStructure<StreamMetadata_with_Application>(metadata)!;
				var application = parsedMetadata.Application;

				/* All chunks we read have the application ID "riff" */
				if (application.ID.ToStringZ() != "riff")
					break;

				if (parsedMetadata.Length < 4)
					break;

				var appFp = new UnmanagedMemoryStream((byte*)application.Data, parsedMetadata.Length - 4);

				int chunkID, chunkLen;

				try
				{
					chunkID = appFp.ReadStructure<int>();
					chunkLen = appFp.ReadStructure<int>();
				}
				catch
				{
					break;
				}

				switch (chunkID)
				{
					case 0x61727478: /* "xtra" */
						IFF.ReadXtraChunk(appFp, Sample);
						break;
					case 0x6C706D73: /* "smpl" */
						IFF.ReadSmplChunk(appFp, Sample);
						break;
				}

				break;
			}
		}
	}

	void OnError(IntPtr decoder, StreamDecoderErrorStatus status, IntPtr clientData)
	{
		Console.WriteLine("FLAC ERROR: {0}", status);
	}

	public bool ProcessMetadata()
	{
		return NativeMethods.FLAC__stream_decoder_process_until_end_of_metadata(Decoder);
	}

	public bool ProcessEntireFile()
	{
		return NativeMethods.FLAC__stream_decoder_process_until_end_of_stream(Decoder);
	}

	public StreamDecoderState GetState()
	{
		return NativeMethods.FLAC__stream_decoder_get_state(Decoder);
	}

	public bool Finish()
	{
		if (!NativeMethods.FLAC__stream_decoder_finish(Decoder))
			return false;

		return true;
	}

	public void Dispose()
	{
		if (Decoder != IntPtr.Zero)
		{
			NativeMethods.FLAC__stream_decoder_delete(Decoder);
			Decoder = IntPtr.Zero;
		}
	}
}
