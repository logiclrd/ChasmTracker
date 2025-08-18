using System;

namespace ChasmTracker.Songs;

[Flags]
public enum SongFlags
{
	EmbedMIDIConfig             = 0x0001, // Embed MIDI macros (Shift-F1) in file
	//FastVolumeSlides          = 0x0002,
	ITOldEffects                = 0x0004, // Old Impulse Tracker effect implementations
  CompatibleGXX               = 0x0008, // "Compatible Gxx" (handle portamento more like other trackers)
  LinearSlides                = 0x0010, // Linear slides vs. Amiga slides
  PatternPlayback             = 0x0020, // Only playing current pattern
  //Step                      = 0x0040,
  Paused                      = 0x0080, // Playback paused (Shift-F8)
  // Fading                   = 0x0100,
  EndReached                  = 0x0200, // Song is finished (standalone keyjazz mode)
  // GlobalFade               = 0x0400,
  // CPUVeryHigh              = 0x0800,
  FirstTick                   = 0x1000, // Current tick is the first tick of the row (dopey flow-control flag)
  // MPTFilterMode            = 0x2000,
  // SurroundPan              = 0x4000,
  // ExtendedFilterRange      = 0x8000,
  // AmigaLimits              = 0x10000,
  InstrumentMode              = 0x20000, // Process instruments
  OrderListLocked                 = 0x40000, // Don't advance orderlist *(Alt-F11)
  NoStereo                    = 0x80000, // secret code for "mono"
	
  PatternLoop                 = (PatternPlayback | OrderListLocked) // Loop current pattern (F6)
}