using System;
using System.IO;

namespace ChasmTracker.FileTypes.Exporters;

using ChasmTracker.Utility;

public class AIFF : SampleExporter
{
	public override string Label => "AIFF";
	public override string Description => "Audio IFF";
	public override string Extension => ".aiff";

	public override int SortOrder => 3;

	AIFFWriteData? _awd;

	public override bool ExportHead(Stream fp, int bits, int channels, int rate)
	{
		_awd = new AIFFWriteData();
		_awd.StartOffset = fp.Position;

		AIFFFile.WriteAIFFHeader(fp, bits, channels, rate, null, ~0, _awd);

		_awd.NumBytes = 0;
		_awd.BigEndian = (bits > 8);

		return true;
	}

	public override bool ExportBody(Stream fp, Span<byte> data)
	{
		if (_awd == null)
			throw new Exception("Write data is not set, call ExportHead before ExportBody");

		try
		{
			SampleFileConverter.WritePCM(fp, data, _awd.BytesPerFrame, _awd.BytesPerSample, _awd.BigEndian, "AIFF");
		}
		catch
		{
			return false;
		}

		_awd.NumBytes += data.Length;

		return true;
	}

	public override bool ExportSilence(Stream fp, int bytes)
	{
		_awd!.NumBytes += bytes;

		for (int i = 0; i < bytes; i++)
			fp.WriteByte(0);

		return true;
	}

	public override bool ExportTail(Stream fp)
	{
		/* fix the length in the file header */
		int fileDataLength = (int)(fp.Position - _awd!.StartOffset);

		fileDataLength = ByteSwap.Swap(fileDataLength);

		var writer = new BinaryWriter(fp);

		fp.Position = _awd.StartOffset + 4;

		writer.Write(fileDataLength);
		writer.Flush();

		/* write the other lengths */
		fp.Position = _awd.COMMFramesOffset;

		writer.Write(ByteSwap.Swap(_awd.NumBytes / _awd.BytesPerSample));
		writer.Flush();

		fp.Position = _awd.SSNDSizeOffset;

		writer.Write(ByteSwap.Swap(_awd.NumBytes + 8));
		writer.Flush();

		_awd = null;

		return true;
	}
}
