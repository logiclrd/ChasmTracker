using System;

namespace ChasmTracker.Playback;

// Global Options (Renderer)
[Flags]
public enum MixFlags
{
	ReverseStereo        = 0x0001, // swap L/R audio channels
	//NoiseReduction       = 0x0002, // reduce hiss (do not use, it's just a simple low-pass filter)
	//AutomaticGainControl = 0x0004, // automatic gain control
	//NoResampling         = 0x0008, // force no resampling (uninterpolated)
	//HQResampler          = 0x0010, // cubic resampling
	//MegaBass             = 0x0020,
	//Surround             = 0x0040,
	//Reverb               = 0x0080,
	//EQ                   = 0x0100, // apply EQ (always on)
	//SoftPanning          = 0x0200,
	//UltraHQSourceMode    = 0x0400, // polyphase resampling (or FIR? I don't know)
	// Misc Flags (can safely be turned on or off)
	DirectToDisk         = 0x10000, // disk writer mode
	NoBackwardJumps      = 0x40000, // disallow Bxx jumps from going backward in the orderlist
	//MaxDefaultPanning    = 0x80000, // (no longer) Used by the MOD loader
	MuteChannelMode      = 0x100000, // Notes are not played on muted channels
	NoSurround           = 0x200000, // ignore S91
	//NoMixing             = 0x400000,
	NoRamping            = 0x800000, // don't apply ramping on volume change (causes clicks)
}
