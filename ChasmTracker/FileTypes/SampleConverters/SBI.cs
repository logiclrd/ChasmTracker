using System;
using System.IO;

namespace ChasmTracker.FileTypes.SampleConverters;

using ChasmTracker.FileSystem;
using ChasmTracker.Songs;
using ChasmTracker.Utility;

public class SBI : SampleFileConverter
{
	public override string Label => "SBI";
	public override string Description => "Sound Blaster";
	public override string Extension => ".sbi";

	public override bool CanSave => true;

	byte[] LoadData(Stream stream)
	{
		/* file format says 52 bytes, but the rest is just
		 * random padding we don't care about. */
		byte[] data = new byte[47];

		stream.ReadExactly(data);

		if (data.Slice(0, 4).ToString() != "SBI\x1a")
			throw new FormatException();

		return data;
	}

	public override bool FillExtendedData(Stream stream, FileReference file)
	{
		try
		{
			byte[] data = LoadData(stream);

			file.Description = "Sound Blaster Instrument";
			file.Title = data.Slice(4, 32).ToStringZ();
			file.Type = FileTypes.SampleExtended | FileTypes.InstrumentOther; //huh?

			return true;
		}
		catch
		{
			return false;
		}
	}

	public override SongSample LoadSample(Stream stream)
	{
		byte[] data = LoadData(stream);

		var smp = new SongSample();

		smp.Name = data.Slice(4, 32).ToStringZ();
		smp.AdLibBytes = data.Slice(36, 11).ToArray();
		smp.C5Speed = 8363;

		/* dumb hackaround that ought to someday be removed: */
		smp.Length = 1;
		smp.AllocateData();

		smp.Flags = SampleFlags.AdLib;

		return smp;
	}

	/* ---------------------------------------------------- */

	public override SaveResult SaveSample(SongSample sample, Stream stream)
	{
		if (!sample.Flags.HasAllFlags(SampleFlags.AdLib))
			return SaveResult.Unsupported;
		if ((sample.AdLibBytes == null) || (sample.AdLibBytes.Length < 11))
			return SaveResult.InternalError;

		/* magic bytes */
		stream.WriteString("SBI\x1a", 4);
		stream.Write(sample.Name.ToCP437(32));

		/* instrument settings */
		stream.Write(sample.AdLibBytes.Slice(0, 11));

		/* padding. many programs expect this to exist, but some
		* files have this data cut off for unknown reasons. */
		stream.WriteByte(0);
		stream.WriteByte(0);
		stream.WriteByte(0);
		stream.WriteByte(0);
		stream.WriteByte(0);

		/* that was easy! */
		return SaveResult.Success;
	}
}
