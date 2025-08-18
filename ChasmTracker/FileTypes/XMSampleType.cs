using System;

namespace ChasmTracker.FileTypes;

[Flags]
public enum XMSampleType : byte
{
	Loop = 1,
	PingPongLoop = 2,
	LoopMask = Loop | PingPongLoop,
	_16Bit = 0x10,
	Stereo = 0x20,
}
