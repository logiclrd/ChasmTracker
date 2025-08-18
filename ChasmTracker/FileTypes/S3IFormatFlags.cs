using System;

namespace ChasmTracker.FileTypes;

[Flags]
public enum S3IFormatFlags : byte
{
	None = 0,

	Loop = 1,
	Stereo = 2,
	_16Bit = 4,
}