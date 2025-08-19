using System;
using System.IO;
using System.Text;

namespace ChasmTracker.FileTypes.Exporters;

/* wav is like aiff's ret**ded cousin */
public class WAV : SampleExporter
{
	public override string Label => "WAV";
	public override string Description => "WAV";
	public override string Extension => ".wav";

	public override int SortOrder => 1;

	long _fileStartOffset;
	long _dataSizeOffset; // seek position for writing data size (in bytes)
	long _numBytes; // how many bytes have been written
	int _bps; // bytes per sample
	int _bpf; // bytes per frame

	public override bool ExportHead(Stream fp, int bits, int channels, int rate)
	{
		_fileStartOffset = fp.Position;

		_bpf = WAVFile.WriteHeader(fp, bits, channels, rate, 0, out _dataSizeOffset);
		_bps = _bpf / channels;

		_numBytes = 0;

		return true;
	}

	public override bool ExportBody(Stream fp, Span<byte> data)
	{
		if ((_bpf == 0) || (_bps == 0))
			throw new Exception("Write data is not set, call ExportHead before ExportBody");

		try
		{
			SampleFileConverter.WritePCM(fp, data, _bpf, _bps, false, "AIFF");
		}
		catch
		{
			return false;
		}

		_numBytes += data.Length;

		return true;
	}

	static byte[] Zeroes = new byte[65536];

	public override bool ExportSilence(Stream fp, int bytes)
	{
		while (bytes > Zeroes.Length)
		{
			fp.Write(Zeroes, 0, Zeroes.Length);
			_numBytes += Zeroes.Length;
			bytes -= Zeroes.Length;
		}

		fp.Write(Zeroes, 0, bytes);
		_numBytes += bytes;

		return true;
	}

	public override bool ExportTail(Stream fp)
	{
		WAVFile.WriteLISTChunk(fp, null);

		var writer = new BinaryWriter(fp, Encoding.ASCII, leaveOpen: true);

		/* fix the length in the file header */
		long ul = fp.Position - _fileStartOffset - 8;
		fp.Position = _fileStartOffset + 4;
		writer.Write((int)ul);
		writer.Flush();

		/* write the other lengths */
		fp.Position = _dataSizeOffset;
		writer.Write((int)_numBytes);
		writer.Flush();

		return true;
	}
}
