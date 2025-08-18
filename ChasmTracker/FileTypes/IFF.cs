using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace ChasmTracker.FileTypes;

using ChasmTracker.Songs;
using ChasmTracker.Utility;

public class IFF
{
	[ThreadStatic]
	static byte[]? s_buffer;

	[MemberNotNull(nameof(s_buffer))]
	static void EnsureBuffer(int size)
	{
		if ((s_buffer == null) || (s_buffer.Length < size))
			s_buffer = new byte[size * 2];
	}

	public static IFFChunk? PeekChunk(Stream stream)
	{
		return PeekChunkEx(stream, ChunkFlags.Aligned);
	}

	public static IFFChunk? PeekChunkEx(Stream stream, ChunkFlags flags)
	{
		try
		{
			var chunk = new IFFChunk();

			chunk.ID = ByteSwap.Swap(stream.ReadStructure<uint>());
			chunk.Size = stream.ReadStructure<int>();

			if (!flags.HasAllFlags(ChunkFlags.SizeLittleEndian))
				chunk.Size = ByteSwap.Swap(chunk.Size);

			chunk.Offset = stream.Position;

			if (flags.HasAllFlags(ChunkFlags.Aligned))
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
	public static int Read(Stream stream, IFFChunk chunk, Memory<byte> data)
		=> Read(stream, chunk, data.Span);

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

	public static T ReadStructure<T>(Stream stream, IFFChunk chunk)
	{
		int structureSize = Marshal.SizeOf<T>();

		EnsureBuffer(structureSize);

		var slice = s_buffer.AsMemory(0, structureSize);

		int numRead = Read(stream, chunk, slice);

		if (numRead < structureSize)
			throw new Exception("Failed to read " + structureSize + " bytes from chunk of length " + chunk.Size + " at " + chunk.Offset);

		return StructureSerializer.MarshalFromBytes<T>(slice);
	}

	public static string ReadString(Stream stream, IFFChunk chunk, int length = -1, Encoding? encoding = null, bool nullTerminated = true)
	{
		encoding ??= Encoding.ASCII;

		if (length < 0)
			length = chunk.Size;
		if (length > chunk.Size)
			length = chunk.Size;

		EnsureBuffer(length);

		var slice = s_buffer.Slice(0, length);

		int numRead = Read(stream, chunk, slice);

		if (numRead < length)
			throw new Exception("Failed to read chunk of length " + length + " at " + chunk.Offset);

		if (nullTerminated)
		{
			int terminator = slice.IndexOf((byte)0);

			if (terminator >= 0)
				slice = slice.Slice(0, terminator);
		}

		return encoding.GetString(slice);
	}


	public static int Receive(Stream stream, IFFChunk chunk, Func<byte[], int, int>? callback)
	{
		var buffer = new byte[chunk.Size];

		int numRead = Read(stream, chunk, buffer.AsMemory());

		if (numRead == 0)
			return 0;

		return callback?.Invoke(buffer, numRead) ?? 0;
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

	public static bool ReadXtraChunk(Stream stream, SongSample smp)
	{
		try
		{
			int xtraFlags = stream.ReadStructure<int>();

			if ((xtraFlags & 0x20) != 0)
				smp.Flags |= SampleFlags.Panning;

			int pan = stream.ReadStructure<short>();

			pan = ((pan + 2) / 4) * 4; // round to nearest multiple of 4

			smp.Panning = Math.Min(pan, 256);

			int vol = stream.ReadStructure<short>();

			vol = ((vol + 2) / 4) * 4; // round to nearest multiple of 4

			smp.Volume = Math.Min(vol, 256);

			int gbv = stream.ReadStructure<short>();

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
			/* skip "samplerData" and "identifier" */
			stream.Position += 8;

			int type = stream.ReadStructure<int>();
			int start = stream.ReadStructure<int>();
			int end = stream.ReadStructure<int>();

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

	/* not sure if this really "fits" here; whatever, it's IFF-ish :) */
	public static void FillXtraChunk(SongSample smp, BinaryWriter xtraData)
	{
		/* Identifier :) */
		xtraData.WritePlain("xtra");

		/* always 16 bytes... */
		xtraData.Write(16);

		/* flags */
		xtraData.Write(smp.Flags.HasAllFlags(SampleFlags.Panning) ? 0x20 : 0);

		/* default pan -- 0..256 */
		xtraData.Write((short)smp.Panning);

		/* default volume -- 0..256 */
		xtraData.Write((short)smp.Volume);

		/* global volume -- 0..64 */
		xtraData.Write((short)smp.GlobalVolume);

		/* reserved (always zero) */
		xtraData.Write((short)0);

		/* autovibrato type */
		xtraData.Write((byte)smp.VibratoType);

		/* autovibrato sweep (speed) */
		xtraData.Write((byte)smp.VibratoSpeed);

		/* autovibrato depth */
		xtraData.Write((byte)smp.VibratoDepth);

		/* autovibrato rate */
		xtraData.Write((byte)smp.VibratoRate);

		/* after this can be a sample name and filename -- but it doesn't
		* matter for our use case */
	}

	static void FillSmplChunkLoop(BinaryWriter writer,
		int loopStart, int loopEnd, bool bidi)
	{
		/* The FOOLY COOLY Constant */
		writer.WritePlain("FLCL");

		writer.Write(bidi ? 1 : 0);
		writer.Write(loopStart);
		writer.Write(loopEnd - 1);

		/* no finetune ? */
		writer.Write(0);

		/* loop infinitely */
		writer.Write(0);
	}

	public static void FillSmplChunk(SongSample smp, BinaryWriter smplData)
	{
		bool writeLoop = smp.Flags.HasAllFlags(SampleFlags.Loop);
		bool writeSustainLoop = smp.Flags.HasAllFlags(SampleFlags.SustainLoop);

		/* legendary hackaround from OpenMPT:
		*
		* Since there are no "loop types" to distinguish between sustain and normal loops, OpenMPT assumes
		* that the first loop is a sustain loop if there are two loops. If we only want a sustain loop,
		* we will have to write a second bogus loop.
		*
		* As such, if there is a sustain loop, then there are _always_ two loops.
		*/
		int loopCount = writeSustainLoop ? 2 : writeLoop ? 1 : 0;

		int chunkLength = 36 + loopCount * 24;

		smplData.WritePlain("smpl");
		smplData.Write(chunkLength);

		/* manufacturer (zero == "no specific manufacturer") */
		smplData.Write(0);

		/* product (zero == "no specific manufacturer") */
		smplData.Write(0);

		/* period of one sample in nanoseconds */
		int period = unchecked((int)(1000000000L / smp.C5Speed));
		smplData.Write(period);

		/* MIDI unity note */
		smplData.Write(72);

		/* MIDI pitch fraction (???) -- I don't think this is relevant at all to us */
		smplData.Write(0);

		/* SMPTE format (?????????????) */
		smplData.Write(0);

		/* SMPTE offset */
		smplData.Write(0);

		/* finally... the sample loops */
		smplData.Write(loopCount);

		/* sampler-specific data (we have none) */
		smplData.Write(0);

		if (writeSustainLoop)
			FillSmplChunkLoop(smplData, smp.SustainStart, smp.SustainEnd, smp.Flags.HasAllFlags(SampleFlags.PingPongSustain));

		if (writeLoop)
			FillSmplChunkLoop(smplData, smp.LoopStart, smp.LoopEnd, smp.Flags.HasAllFlags(SampleFlags.PingPongLoop));
		else if (writeSustainLoop)
		{
			// Emit a dummy loop, because a loop count of 2 is the only way for
			// the sustain loop to be recognized as such when loading.
			FillSmplChunkLoop(smplData, 0, 0, false);
		}
	}
}
