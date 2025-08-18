using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using libFLAC;

namespace ChasmTracker.FileTypes;

public class FLACEncoder : IDisposable
{
	public int Channels;
	public int Bits;
	public IntPtr Encoder;
	public int BytesPerFrame;

	public Stream? InputStream;
	public Stream? OutputStream;

	public static bool IsWorking
	{
		get
		{
			// Only return true if the FLAC library seems to be working.
			try
			{
				NativeMethods.FLAC__stream_encoder_delete(NativeMethods.FLAC__stream_encoder_new());
				return true;
			}
			catch
			{
				return false;
			}
		}
	}

	public bool Initialize(int bits, int channels, int rate, long estimateNumSamples)
	{
		if (!InitializeSaveHead(bits, channels, rate, estimateNumSamples))
			return false;
		if (!InitializeSaveTail())
			return false;

		return true;
	}

	public bool InitializeSaveHead(int bits, int channels, int rate, long estimateNumSamples)
	{
		Channels = channels;
		Bits = bits;
		BytesPerFrame = Bits * Channels / 8;

		Encoder = NativeMethods.FLAC__stream_encoder_new();

		if (Encoder == IntPtr.Zero)
			return false;

		if (!NativeMethods.FLAC__stream_encoder_set_channels(Encoder, channels))
			return false;

		if (!NativeMethods.FLAC__stream_encoder_set_bits_per_sample(Encoder, bits))
			return false;

		if (rate > FLACConstants.MaxSampleRate)
			rate = FLACConstants.MaxSampleRate;

		// FLAC only supports 10 Hz granularity for frequencies above 65535 Hz if the streamable subset is chosen, and only a maximum frequency of 655350 Hz.
		if (!NativeMethods.FLAC__format_sample_rate_is_subset(rate))
			NativeMethods.FLAC__stream_encoder_set_streamable_subset(Encoder, false);

		if (!NativeMethods.FLAC__stream_encoder_set_sample_rate(Encoder, rate))
			return false;

		if (!NativeMethods.FLAC__stream_encoder_set_compression_level(Encoder, 5))
			return false;

		if (!NativeMethods.FLAC__stream_encoder_set_total_samples_estimate(Encoder, estimateNumSamples))
			return false;

		if (!NativeMethods.FLAC__stream_encoder_set_verify(Encoder, false))
			return false;

		return true;
	}

	public bool InitializeSaveTail()
	{
		var initStatus = NativeMethods.FLAC__stream_encoder_init_stream(
			Encoder,
			OnWrite,
			OnSeek,
			OnTell,
			null, /* metadata callback */
			IntPtr.Zero /* client data */
		);

		if (initStatus != StreamEncoderInitStatus.OK)
		{
			Log.Append(4, "ERROR: initializing FLAC encoder: " + initStatus.ToString());
			Console.Error.WriteLine("ERROR: initializing FLAC encoder: " + initStatus.ToString());
			return false;
		}

		return true;
	}

	StreamEncoderWriteStatus OnWrite(IntPtr encoder, IntPtr bufferPtr, UIntPtr bytes, uint samples, uint currentFrame, IntPtr clientData)
	{
		try
		{
			unsafe
			{
				OutputStream!.Write(new Span<byte>((void*)bufferPtr, (int)bytes));
				return StreamEncoderWriteStatus.OK;
			}
		}
		catch
		{
			return StreamEncoderWriteStatus.FatalError;
		}
	}

	StreamEncoderSeekStatus OnSeek(IntPtr encoder, long absoluteByteOffset, IntPtr clientData)
	{
		try
		{
			if (!OutputStream!.CanSeek)
				return StreamEncoderSeekStatus.Unsupported;

			OutputStream.Position = absoluteByteOffset;
			return StreamEncoderSeekStatus.OK;
		}
		catch
		{
			return StreamEncoderSeekStatus.Error;
		}
	}

	StreamEncoderTellStatus OnTell(IntPtr encoder, out long absoluteByteOffset, IntPtr clientData)
	{
		try
		{
			absoluteByteOffset = OutputStream!.Position;
			return StreamEncoderTellStatus.OK;
		}
		catch
		{
			absoluteByteOffset = -1;
			return StreamEncoderTellStatus.Error;
		}
	}

	public bool SetMetadata(IEnumerable<IntPtr> metadata)
	{
		IntPtr[] ptrs = metadata.ToArray();

		return NativeMethods.FLAC__stream_encoder_set_metadata(Encoder, ptrs, ptrs.Length);
	}

	int[] sampleBuffer = new int[65536];

	public bool EmitSampleData(Span<byte> data)
	{
		int bytesPerSample = Bits / 8;

		int sampleCount = data.Length / bytesPerSample;

		if (sampleCount >= sampleBuffer.Length)
			sampleBuffer = new int[sampleCount * 2];

		/* 8-bit/16-bit/32-bit PCM -> 32-bit PCM */
		switch (bytesPerSample)
		{
			case 1:
				for (int i = 0; i < sampleCount; i++)
					sampleBuffer[i] = data[i];
				break;
			case 2:
				for (int i = 0; i < sampleCount; i++)
					sampleBuffer[i] = BitConverter.ToInt16(data.Slice(i * 2, 2));
				break;
			case 3:
				for (int i = 0; i < sampleCount; i++)
				{
					uint u = unchecked((uint)(
						(data[i * 3 + 2] << 24) |
						(data[i * 3 + 1] << 16) |
						(data[i * 3 + 0] << 8)));

					int s = unchecked((int)u);

					sampleBuffer[i] = s >> 8;
				}
				break;
			case 4:
				for (int i = 0; i < sampleCount; i++)
					sampleBuffer[i] = BitConverter.ToInt32(data.Slice(i * 4, 4));
				break;
			default:
				throw new Exception("unknown bytesPerSample value: " + bytesPerSample);
		}

		return NativeMethods.FLAC__stream_encoder_process_interleaved(Encoder, sampleBuffer, sampleCount / Channels);
	}

	public bool EmitSampleData(Span<sbyte> data)
	{
		int bytesPerSample = Bits / 8;

		int sampleCount = data.Length / bytesPerSample;

		if (sampleCount >= sampleBuffer.Length)
			sampleBuffer = new int[sampleCount * 2];

		byte[] conversionBuffer = new byte[4];

		/* 8-bit/16-bit/32-bit PCM -> 32-bit PCM */
		switch (bytesPerSample)
		{
			case 1:
				for (int i = 0; i < sampleCount; i++)
					sampleBuffer[i] = unchecked((byte)data[i]);
				break;
			case 2:
				for (int i = 0; i < sampleCount; i++)
				{
					conversionBuffer[0] = unchecked((byte)data[i + i]);
					conversionBuffer[1] = unchecked((byte)data[i + i + 1]);

					sampleBuffer[i] = BitConverter.ToInt16(conversionBuffer, 0);
				}
				break;
			case 4:
				for (int i = 0; i < sampleCount; i++)
				{
					conversionBuffer[0] = unchecked((byte)data[i * 4]);
					conversionBuffer[1] = unchecked((byte)data[i * 4 + 1]);
					conversionBuffer[2] = unchecked((byte)data[i * 4 + 2]);
					conversionBuffer[3] = unchecked((byte)data[i * 4 + 3]);

					sampleBuffer[i] = BitConverter.ToInt32(conversionBuffer, 0);
				}
				break;
			default:
				throw new Exception("unknown bytesPerSample value: " + bytesPerSample);
		}

		return NativeMethods.FLAC__stream_encoder_process_interleaved(Encoder, sampleBuffer, sampleCount / Channels);
	}

	public bool EmitSampleData(Span<short> data)
	{
		int bytesPerSample = Bits / 8;

		int sampleCount = data.Length / bytesPerSample;

		if (sampleCount >= sampleBuffer.Length)
			sampleBuffer = new int[sampleCount * 2];

		/* 8-bit/16-bit/32-bit PCM -> 32-bit PCM */
		switch (bytesPerSample)
		{
			case 1:
				throw new Exception("Internal error: sample format mismatch");
			case 2:
				for (int i = 0; i < sampleCount; i++)
					sampleBuffer[i] = data[i];
				break;
			case 4:
				// I don't think this ever actually happens??
				for (int i = 0; i < sampleCount; i++)
				{
					int lo = data[i + i];
					int hi = data[i + i + 1];

					sampleBuffer[i] = lo | (hi << 16);
				}
				break;
			default:
				throw new Exception("unknown bytesPerSample value: " + bytesPerSample);
		}

		return NativeMethods.FLAC__stream_encoder_process_interleaved(Encoder, sampleBuffer, sampleCount / Channels);
	}

	public StreamEncoderState GetState()
	{
		return NativeMethods.FLAC__stream_encoder_get_state(Encoder);
	}

	public bool Finish()
	{
		if (!NativeMethods.FLAC__stream_encoder_finish(Encoder))
			return false;

		return true;
	}

	public void Dispose()
	{
		if (Encoder != IntPtr.Zero)
		{
			NativeMethods.FLAC__stream_encoder_delete(Encoder);
			Encoder = IntPtr.Zero;
		}
	}
}
