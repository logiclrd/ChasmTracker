using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace ChasmTracker.FileTypes.Converters;

using ChasmTracker.FileSystem;
using ChasmTracker.Songs;
using ChasmTracker.Utility;

public class DSM : SongFileConverter
{
	public override string Label => "DSM";
	public override string Description => "Digital Sound Interface Kit";
	public override string Extension => ".dsm";

	class DSMChunkPattern
	{
		public byte[]? Data;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
	class DSMChunkSong
	{
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 28)]
		public string Title = "";
		public short Version;
		public short Flags;
		public int Pad;
		public short OrdNum, SmpNum, PatNum, ChannelNum;
		public byte GlobalVolume, MasterVolume, InitialSpeed, InitialTempo;
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
		public byte[] ChannelPan = Array.Empty<byte>();
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)]
		public byte[] Orders = Array.Empty<byte>();
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
	class DSMChunkInstrument
	{
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 13)]
		public string FileName = "";
		public DSMSampleFlags Flags;
		public byte Volume;
		public int Length, LoopStart, LoopEnd, AddressPtr;
		public short C5Speed, Period;
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 28)]
		public string Name = "";
	}

	class DSMProcessPatternData
	{
		public Pattern Pattern = Pattern.Empty;
		public int NumChannels;
		public int ChannelDoesNotMatch;
	}

	/* sample flags */
	enum DSMSampleFlags : short
	{
		LoopActive = 0x01,
		PCMSigned = 0x02,
		PCMPacked = 0x04,
		PCMDeltaEncoded = 0x40,
	};

	/* pattern byte flags/masks */
	enum DSMPatternFlags
	{
		NotePresent = 0x80,
		InstrumentPresent = 0x40,
		VolumePresent = 0x20,
		CommandPresent = 0x10,
		ChannelNumberMask = 0x0F,
	}

	const int ID_SONG = 0x534F4E47;
	const int ID_INST = 0x494E5354;
	const int ID_PATT = 0x50415454;

	public override bool FillExtendedData(Stream stream, FileReference fileReference)
	{
		long availableBytes = stream.Length - stream.Position;

		if (!(availableBytes > 40))
			return false;

		byte[] buffer = new byte[4];

		stream.ReadExactly(buffer);

		string riff = Encoding.ASCII.GetString(buffer);

		if (riff != "RIFF")
			return false;

		stream.ReadExactly(buffer);
		stream.ReadExactly(buffer);

		string dsmf = Encoding.ASCII.GetString(buffer);

		if (dsmf != "DSMF")
			return false;

		while (IFF.PeekChunkEx(stream, ChunkFlags.SizeLittleEndian) is IFFChunk chunk)
		{
			if (chunk.ID == ID_SONG)
			{
				/* we only need the title, really */
				byte[] title = new byte[28];

				IFF.Read(stream, chunk, title);

				fileReference.Title = title.ToStringZ();

				break;
			}
		}

		fileReference.Description = "Digital Sound Interface Kit";
		/*fileReference.Extension = "dsm";*/
		fileReference.Type = FileSystem.FileTypes.ModuleMOD;

		return true;
	}

	public override Song LoadSong(Stream stream, LoadFlags lflags)
	{
		var chunkSong = new DSMChunkSong();

		int ChunkSongRead(byte[] data, int length)
		{
			if (length < Marshal.SizeOf<DSMChunkSong>())
				throw new FormatException();

			var pin = GCHandle.Alloc(data, GCHandleType.Pinned);

			try
			{
				chunkSong = Marshal.PtrToStructure<DSMChunkSong>(pin.AddrOfPinnedObject()) ?? throw new FormatException();
			}
			finally
			{
				pin.Free();
			}

			return 1;
		}

		var chunkInstrument = new DSMChunkInstrument();

		int ChunkInstrumentRead(byte[] data, int length)
		{
			if (length < Marshal.SizeOf<DSMChunkInstrument>())
				throw new FormatException();

			var pin = GCHandle.Alloc(data, GCHandleType.Pinned);

			try
			{
				chunkInstrument = Marshal.PtrToStructure<DSMChunkInstrument>(pin.AddrOfPinnedObject()) ?? throw new FormatException();
			}
			finally
			{
				pin.Free();
			}

			return 1;
		}

		DSMProcessPatternData ppd = new DSMProcessPatternData();

		int ProcessPattern(byte[] data, int length)
		{
			/* grab the header length */
			int hdrLen = BitConverter.ToInt16(data, 0);

			if (length < hdrLen)
				throw new FormatException();

			/* reopen the memstream, but this time limit the size to the
			* chunk size or header length, whichever is smaller */
			var stream = new MemoryStream(data, 2, Math.Min(length - 2, hdrLen));

			int row = 0;

			while (row < 64)
			{
				var mask = (DSMPatternFlags)stream.ReadByte();

				if (mask == 0)
				{
					/* done with the row */
					row++;
					continue;
				}

				int channel = (int)(mask & DSMPatternFlags.ChannelNumberMask);

				if (channel > Constants.MaxChannels) /* whoops */
					throw new FormatException();

				if (channel > ppd.NumChannels) /* header doesn't match? warn. */
					ppd.ChannelDoesNotMatch = Math.Max(channel, ppd.ChannelDoesNotMatch);

				ref var note = ref ppd.Pattern.Rows[row][channel + 1];

				if (mask.HasFlag(DSMPatternFlags.NotePresent))
				{
					int c = stream.ReadByte();

					if (c <= 168)
						note.Note = (byte)(c + 12);
				}

				if (mask.HasFlag(DSMPatternFlags.InstrumentPresent))
					note.Instrument = (byte)stream.ReadByte();

				if (mask.HasFlag(DSMPatternFlags.VolumePresent))
				{
					/* volume */
					int param = stream.ReadByte();

					if (param != 0xFF)
					{
						note.VolumeEffect = VolumeEffects.Volume;
						note.VolumeParameter = (byte)Math.Min(param, 64);
					}
				}

				if (mask.HasFlag(DSMPatternFlags.CommandPresent))
				{
					note.ImportMODEffect(
						modEffect: (byte)stream.ReadByte(),
						modParam: (byte)stream.ReadByte(),
						fromXM: false);

					if (note.Effect == Effects.Panning)
					{
						if (note.Parameter <= 0x80)
							note.Parameter <<= 1;
						else if (note.Parameter == 0xA4)
						{
							note.Effect = Effects.Special;
							note.Parameter = 0x91;
						}
					}
				}
			}

			return 1;
		}

		var buffer = new byte[4];

		stream.ReadExactly(buffer);

		if (buffer.ToStringZ() != "RIFF")
			throw new NotSupportedException();

		stream.ReadExactly(buffer);
		stream.ReadExactly(buffer);

		if (buffer.ToStringZ() != "DSMF")
			throw new NotSupportedException();

		var song = new Song();
		byte[] channelPan = new byte[16];

		int s = 0, p = 0;
		int nOrd = 0, nSmp = 0, nPat = 0, nChn = 0;
		int channelDoesNotMatch = 0;
		int numSongHeaders = 0;

		while (IFF.PeekChunk(stream) is IFFChunk chunk)
		{
			switch (chunk.ID)
			{
				case ID_SONG:
				{
					IFF.Receive(stream, chunk, ChunkSongRead);

					nOrd = chunkSong.OrdNum;
					nSmp = chunkSong.SmpNum;
					nPat = chunkSong.PatNum;
					nChn = chunkSong.ChannelNum;

					if (nOrd > Constants.MaxOrders || nSmp > Constants.MaxSampleLength || nPat > Constants.MaxPathLength || nChn > Constants.MaxChannels)
						throw new NotSupportedException();

					song.InitialGlobalVolume = chunkSong.GlobalVolume << 1;
					song.MixingVolume = chunkSong.MasterVolume >> 1;
					song.InitialSpeed = chunkSong.InitialSpeed;
					song.InitialTempo = chunkSong.InitialTempo;

					song.Title = chunkSong.Title.TrimZ();

					chunkSong.ChannelPan.CopyTo(channelPan.AsMemory());

					for (int i = 0; i < chunkSong.Orders.Length; i++)
						song.OrderList.Add(chunkSong.Orders[i]);

					numSongHeaders++;

					break;
				}
				case ID_INST:
				{
					/* sanity check. it doesn't matter if nsmp isn't the real sample
					* count; the file isn't "tainted" because of it, so just print
					* a warning if the amount of samples loaded wasn't what was expected */
					if (s > Constants.MaxSamples)
						throw new NotSupportedException(); /* punt */

					if (!lflags.HasFlag(LoadFlags.NoSamples))
					{
						IFF.Receive(stream, chunk, ChunkInstrumentRead);

						/* samples internally start at index 1 */
						SongSample sample = song.EnsureSample(s + 1);

						SampleFormat flags = SampleFormat.LittleEndian | SampleFormat._8 | SampleFormat.Mono;

						if (chunkInstrument.Flags.HasFlag(DSMSampleFlags.LoopActive))
							sample.Flags |= SampleFlags.Loop;

						/* these are mutually exclusive (?) */
						if (chunkInstrument.Flags.HasFlag(DSMSampleFlags.PCMSigned))
							flags |= SampleFormat.PCMSigned;
						else if (chunkInstrument.Flags.HasFlag(DSMSampleFlags.PCMDeltaEncoded))
							flags |= SampleFormat.PCMDeltaEncoded;
						else
							flags |= SampleFormat.PCMUnsigned;

						sample.Name = chunkInstrument.Name.TrimZ();
						sample.FileName = chunkInstrument.FileName.TrimZ();

						sample.Length = chunkInstrument.Length;
						sample.LoopStart = chunkInstrument.LoopStart;
						sample.LoopEnd = chunkInstrument.LoopEnd;
						sample.C5Speed = chunkInstrument.C5Speed;
						sample.Volume = chunkInstrument.Volume * 4; // modplug

						IFF.ReadSample(stream, chunk, 64, sample, flags);
					}

					s++;

					break;
				}
				case ID_PATT:
				{
					if (p > Constants.MaxPatterns)
						throw new NotSupportedException(); /* punt */

					if (!lflags.HasFlag(LoadFlags.NoPatterns))
					{
						ppd.Pattern = new Pattern(length: 64);
						ppd.ChannelDoesNotMatch = channelDoesNotMatch;
						ppd.NumChannels = nChn;

						/* this is ultra-lame, and should be adapted to just
						* use slurp-functions :)
						*
						* on that note: it would be nice to have a slurp impl
						* that is literally just a range inside another slurp
						* hmmmm */
						IFF.Receive(stream, chunk, ProcessPattern);

						channelDoesNotMatch = ppd.ChannelDoesNotMatch;

						song.Patterns[p] = ppd.Pattern;
					}

					p++;
					break;
				}
				default:
					break;
			}
		}

		/* With our loader, it's possible that the song header wasn't even found to begin with.
		* This causes the order list and title to be empty and the global volume to remain as it was.
		* Make sure to notify the user if this ever actually happens. */
		if (numSongHeaders == 0)
			Log.Append(4, " WARNING: No SONG chunk found! (invalid DSM file??)");
		else if (numSongHeaders > 1)
			Log.Append(4, " WARNING: Multiple ({0}) SONG chunks found!", numSongHeaders);

		if (s != nSmp)
			Log.Append(4, " WARNING: # of samples ({0}) different than expected ({1})", s, nSmp);

		if (p != nPat)
			Log.Append(4, " WARNING: # of patterns ({0}) different than expected ({1})", p, nPat);

		if ((channelDoesNotMatch > 0) && (channelDoesNotMatch != nChn))
			Log.Append(4, " WARNING: # of channels ({0}) different than expected ({1})", channelDoesNotMatch, nChn);

		int n;

		for (n = 0; n < nChn; n++)
		{
			if (channelPan[n & 15] <= 0x80)
				song.Channels[n].Panning = channelPan[n & 15] << 1;
		}

		for (; n < Constants.MaxChannels; n++)
			song.Channels[n].Flags |= ChannelFlags.Mute;

		song.PanSeparation = 128;
		song.Flags = SongFlags.ITOldEffects | SongFlags.CompatibleGXX;

		song.TrackerID = "Digital Sound Interface Kit";

		return song;
	}
}
