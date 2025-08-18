using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

using libFLAC;

namespace ChasmTracker.FileTypes.SampleConverters;

using ChasmTracker.FileSystem;
using ChasmTracker.Songs;
using ChasmTracker.Utility;

public class FLAC : SampleFileConverter
{
	public override string Label => "FLAC";
	public override string Description => "FLAC Audio File";
	public override string Extension => ".flac";

	public override bool IsEnabled => FLACEncoder.IsWorking;

	FLACDecoder Load(Stream inputStream, bool metaOnly)
	{
		inputStream.Position = 0; /* paranoia */

		byte[] magic = new byte[4];

		inputStream.ReadExactly(magic);

		if (magic.ToStringZ() != "fLaC")
			throw new Exception("Incorrect format");

		var decoder = new FLACDecoder();

		decoder.Sample = new SongSample();

		decoder.Initialize(inputStream);

		if (!(metaOnly ? decoder.ProcessMetadata() : decoder.ProcessEntireFile()))
			throw new Exception("Failed to read FLAC file: " + decoder.GetState());

		return decoder;
	}

	public override SongSample LoadSample(Stream stream)
	{
		using (var decoder = Load(stream, metaOnly: false))
		{
			var sample = decoder.Sample ?? throw new Exception("Internal error");

			sample.Volume = 64 * 4;
			sample.GlobalVolume = 64;

			/* libFLAC *always* returns signed */
			SampleFormat flags = SampleFormat.PCMSigned;

			// endianness, based on host system
			flags |= SampleFormat.LittleEndian;

			// channels
			flags |= (decoder.Channels == 2) ? SampleFormat.StereoInterleaved : SampleFormat.Mono;

			// bit width
			switch (decoder.Bits)
			{
				case 8: flags |= SampleFormat._8; break;
				case 16: flags |= SampleFormat._16; break;
				case 32: flags |= SampleFormat._32; break;
				default: /* csf_read_sample will fail */ break;
			}

			var outputStream = decoder.OutputStream ?? throw new Exception("Internal error: FLAC decoder has no OutputStream");

			outputStream.Position = 0;

			ReadSample(sample, flags, outputStream);

			return sample;
		}
	}

	public override bool FillExtendedData(Stream stream, FileReference file)
	{
		using (var decoder = Load(stream, metaOnly: true))
		{
			var sample = decoder.Sample;

			if (sample == null)
				return false;

			sample.Volume = 64 * 4;
			sample.GlobalVolume = 64;

			file.FillFromSample(sample);

			file.Description  = Description;
			file.Type         = FileTypes.SampleCompressed;
			file.SampleFileName = file.BaseName;

			return true;
		}
	}

	/* ------------------------------------------------------------------------ */
	/* Now onto the writing stuff */

	public override bool CanSave => true;

	/* need this because convering huge buffers in memory is KIND OF bad.
	* currently this is the same size as the buffer length in disko.c */
	const int SampleBufferLength = 65536;

	public override SaveResult SaveSample(SongSample sample, Stream stream)
	{
		if (sample.Flags.HasAllFlags(SampleFlags.AdLib))
			return SaveResult.Unsupported;

		/* metadata structures & the amount */
		List<IntPtr> allocatedMetadataPtrs = new List<IntPtr>();
		List<IntPtr> metadataPtrs = new List<IntPtr>();

		try
		{
			using (var encoder = new FLACEncoder())
			{
				bool result = encoder.InitializeSaveHead(
					sample.Flags.HasAllFlags(SampleFlags._16Bit) ? 16 : 8,
					sample.Flags.HasAllFlags(SampleFlags.Stereo) ? 2 : 1,
					sample.C5Speed,
					sample.Length);

				if (!result)
					return SaveResult.InternalError;

				/* okay, now we have to hijack the writedata, so we can set metadata */
				IntPtr metadata;

				metadata = NativeMethods.FLAC__metadata_object_new(MetadataType.APPLICATION);

				if (metadata != IntPtr.Zero)
				{
					allocatedMetadataPtrs.Add(metadata);

					int length = 24;
					byte[] xtra = new byte[24];

					//iff_fill_xtra_chunk(smp, xtra, &length);

					/* now shove it into the metadata */
					if (NativeMethods.FLAC__metadata_object_application_set_data(metadata, xtra, length, true))
					{
						var structure = Marshal.PtrToStructure<StreamMetadata_with_Application>(metadata)!;

						structure.Application.ID = Encoding.ASCII.GetBytes("riff");

						Marshal.StructureToPtr(structure, metadata, false);

						metadataPtrs.Add(metadata);
					}
				}

				metadata = NativeMethods.FLAC__metadata_object_new(MetadataType.APPLICATION);

				if (metadata != IntPtr.Zero)
				{
					allocatedMetadataPtrs.Add(metadata);

					int length = 92;
					byte[] smpl = new byte[92];

					//iff_fill_smpl_chunk(smp, smpl, &length);

					if (NativeMethods.FLAC__metadata_object_application_set_data(metadata, smpl, length, true))
					{
						var structure = Marshal.PtrToStructure<StreamMetadata_with_Application>(metadata)!;

						structure.Application.ID = Encoding.ASCII.GetBytes("riff");

						Marshal.StructureToPtr(structure, metadata, false);

						metadataPtrs.Add(metadata);
					}
				}

				/* this shouldn't be needed for export */
				metadata = NativeMethods.FLAC__metadata_object_new(MetadataType.VORBISCOMMENT);

				if (metadata != IntPtr.Zero)
				{
					allocatedMetadataPtrs.Add(metadata);

					if (AppendVorbisComment(metadata, "SAMPLERATE=48000")
					&& AppendVorbisComment(metadata, "TITLE=FLACTest"))
						metadataPtrs.Add(metadata);

					bool AppendVorbisComment(IntPtr metadataPtr, string comment)
					{
						byte[] bytes = Encoding.UTF8.GetBytes(comment);

						if (bytes.Length > 0)
						{
							IntPtr bytesMemory = IntPtr.Zero;
							try
							{
								bytesMemory = Marshal.AllocHGlobal(bytes.Length);

								Marshal.Copy(bytes, 0, bytesMemory, bytes.Length);

								VorbisCommentEntry e;

								e.Length = bytes.Length;
								e.Entry = bytesMemory;

								NativeMethods.FLAC__metadata_object_vorbiscomment_append_comment(metadataPtr, e, true);

								return true;
							}
							finally
							{
								if (bytesMemory != IntPtr.Zero)
									Marshal.FreeHGlobal(bytesMemory);
							}
						}

						return false;
					}
				}

				if (!encoder.SetMetadata(metadataPtrs))
				{
					Log.Append(4, " FLAC__stream_encoder_set_metadata: " + encoder.GetState());
					return SaveResult.InternalError;
				}

				if (!encoder.InitializeSaveTail())
					return SaveResult.InternalError;

				/* buffer this */
				long totalBytes = sample.Length * (sample.Flags.HasAllFlags(SampleFlags._16Bit) ? 2 : 1) * (sample.Flags.HasAllFlags(SampleFlags.Stereo) ? 2 : 1);
				for (int outputOffset = 0; outputOffset < totalBytes; outputOffset += SampleBufferLength)
				{
					int needed = (int)Math.Min(totalBytes - outputOffset, SampleBufferLength);

					bool succeeded =
						sample.Flags.HasAllFlags(SampleFlags._16Bit)
						? encoder.EmitSampleData(sample.Data16.Slice(outputOffset, needed))
						: encoder.EmitSampleData(sample.Data8.Slice(outputOffset, needed));

					if (!succeeded)
					{
						Log.Append(4, " fmt_flac_export_body: " + encoder.GetState());
						return SaveResult.InternalError;
					}
				}

				encoder.Finish();

				return SaveResult.Success;
			}
		}
		finally
		{
			/* cleanup the metadata crap */
			foreach (var ptr in allocatedMetadataPtrs)
				NativeMethods.FLAC__metadata_object_delete(ptr);
		}
	}
}
