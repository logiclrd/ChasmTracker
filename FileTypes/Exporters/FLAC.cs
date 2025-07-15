using System;
using System.IO;

namespace ChasmTracker.FileTypes.Exporters;

public class FLAC : SampleExporter
{
	public override string Label => "FLAC";
	public override string Description => "Free Lossless Audio Codec";
	public override string Extension => ".flac";

	FLACEncoder? _encoder;

	public override bool ExportHead(Stream fp, int bits, int channels, int rate, int length)
	{
		_encoder = new FLACEncoder();

		return _encoder.Initialize(fp, bits, channels, rate, 0);
	}

	public override bool ExportSilence(Stream fp, int bytes)
	{
		/* actually have to generate silence here */
		return ExportBody(fp, new byte[bytes]);
	}

	public override bool ExportBody(Stream fp, Span<byte> data)
	{
		return _encoder!.EmitSampleData(data);
	}

	public override bool ExportTail(Stream fp)
	{
		if (_encoder!.Finish())
		{
			_encoder = null;
			return true;
		}
		else
			return false;
	}
}
