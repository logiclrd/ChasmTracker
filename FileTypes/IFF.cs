using System;
using System.IO;

namespace ChasmTracker.FileTypes;

using ChasmTracker.Songs;
using ChasmTracker.Utility;

public class IFF
{
	public static IFFChunk? PeekChunk(Stream stream)
	{
		return PeekChunkEx(stream, ChunkFlags.Aligned);
	}

	public static IFFChunk? PeekChunkEx(Stream stream, ChunkFlags flags)
	{
		try
		{
			var chunk = new IFFChunk();

			byte[] buffer = new byte[4];

			stream.ReadExactly(buffer);

			chunk.ID = ByteSwap.Swap(Convert.ToUInt32(buffer));

			stream.ReadExactly(buffer);

			chunk.Size = Convert.ToInt32(buffer);

			if (!flags.HasFlag(ChunkFlags.SizeLittleEndian))
				chunk.Size = ByteSwap.Swap(chunk.Size);

			chunk.Offset = stream.Position;

			if (flags.HasFlag(ChunkFlags.Aligned))
				stream.Position += chunk.Size + (chunk.Size & 1);
			else
				stream.Position += chunk.Size;

			return chunk;
		}
		catch (IOException)
		{
			return null;
		}
	}

	/* returns the number of bytes read or zero on error */
	public static int Read(Stream stream, IFFChunk chunk, Span<byte> data)
	{
		long savedPosition = stream.Position;
		int returnValue = 0;

		try
		{
			stream.Position = chunk.Offset;

			if (data.Length > chunk.Size)
				data = data.Slice(0, chunk.Size);

			if (stream.Position + data.Length > stream.Length)
				data = data.Slice(0, (int)(stream.Length - stream.Position));

			stream.ReadExactly(data);

			returnValue = data.Length;
		}
		catch (IOException)
		{
			returnValue = 0;
		}
		finally
		{
			try
			{
				stream.Position = savedPosition;
			}
			catch
			{
				/* how ? */
				returnValue = 0;
			}
		}

		return returnValue;
	}

	public static int Receive(Stream stream, IFFChunk chunk, Func<byte[], int, int>? callback)
	{
		var buffer = new byte[chunk.Size];

		int numRead = Read(stream, chunk, buffer);

		if (numRead == 0)
			return 0;

		return callback?.Invoke(buffer, numRead, userData) ?? 0;
	}

	/* offset is the offset the sample is actually located in the chunk;
	 * this can be different depending on the file format... */
	public static int ReadSample(Stream stream, IFFChunk chunk, int offset, SongSample smp, SampleFormat flags)
	{
		long savedPosition = stream.Position;

		try
		{
			stream.Position = chunk.Offset + offset;

			return SampleFileConverter.ReadSample(smp, flags, stream);
		}
		finally
		{
			try
			{
				stream.Position = savedPosition;
			}
			catch { }
		}
	}

	/* ------------------------------------------------------------------------ */

	public static bool ReadXtraChunkData(Stream stream, SongSample smp)
	{
		try
		{
			byte[] buffer = new byte[4];

			stream.ReadExactly(buffer);

			int xtraFlags = Convert.ToInt32(buffer);

			if ((xtraFlags & 0x20) != 0)
				smp.Flags |= SampleFlags.Panning;

			buffer = new byte[2];

			stream.ReadExactly(buffer);

			int pan = Convert.ToInt16(buffer);

			pan = ((pan + 2) / 4) * 4; // round to nearest multiple of 4

			smp.Panning = Math.Min(pan, 256);

			stream.ReadExactly(buffer);

			int vol = Convert.ToInt16(buffer);

			vol = ((vol + 2) / 4) * 4; // round to nearest multiple of 4

			smp.Volume = Math.Min(vol, 256);

			stream.ReadExactly(buffer);

			int gbv = Convert.ToInt16(buffer);

			smp.GlobalVolume = Math.Min(gbv, 64);

			/* reserved chunk (always zero) */
			stream.Position += 2;

			smp.VibratoType = (VibratoType)stream.ReadByte();
			smp.VibratoSpeed = stream.ReadByte();
			smp.VibratoDepth = stream.ReadByte();
			smp.VibratoRate = stream.ReadByte();

			return true;
		}
		catch (IOException)
		{
			return false;
		}
	}

	public static bool ReadSmplChunkLoop(Stream stream, SampleFlags loopFlag, SampleFlags bidiFlag, ref int loopStart, ref int loopend, ref SampleFlags flags)
	{
		try
		{
			byte[] buffer = new byte[4];

			/* skip "samplerData" and "identifier" */
			stream.Position += 8;

			stream.ReadExactly(buffer);

			int type = Convert.ToInt32(buffer);

			stream.ReadExactly(buffer);

			int start = Convert.ToInt32(buffer);

			stream.ReadExactly(buffer);

			int end = Convert.ToInt32(buffer);

			/* loop is bogus? */
			if ((start | end) == 0)
				return false;

			if ((type < 0) || (type >= 2)) /* unsupported */
				return false;

			flags |= loopFlag;

			if (type == 1)
				flags |= bidiFlag;

			loopStart = start;
			loopend = end + 1;

			return true;
		}
		catch (IOException)
		{
			return false;
		}
	}

	public static bool ReadSmplChunk(Stream stream, SongSample smp)
	{
		try
		{
			stream.Position += 28;

			byte[] buffer = new byte[4];

			stream.ReadExactly(buffer);

			int numLoops = Convert.ToInt32(buffer);

			/* sustain loop */
			if (numLoops >= 2
				&& !ReadSmplChunkLoop(stream, SampleFlags.SustainLoop, SampleFlags.PingPongSustain, ref smp.SustainStart, ref smp.SustainEnd, ref smp.Flags))
				return false;

			if (numLoops >= 1
				&& !ReadSmplChunkLoop(stream, SampleFlags.Loop, SampleFlags.PingPongLoop, ref smp.LoopStart, ref smp.LoopEnd, ref smp.Flags))
				return false;

			return true;
		}
		catch (IOException)
		{
			return false;
		}
	}
}
