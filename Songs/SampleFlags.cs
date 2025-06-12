using System;

namespace ChasmTracker.Songs;

[Flags]
public enum SampleFlags
{
  _16Bit                      = 0x01, // 16-bit sample
  Loop                        = 0x02, // looped sample
  PingPongLoop                = 0x04, // bi-directional (useless unless CHN_LOOP is also set)
  SustainLoop                 = 0x08, // sample with sustain loop
  PingPongSustain             = 0x10, // bi-directional (useless unless CHN_SUSTAINLOOP is also set)
  Panning                     = 0x20, // sample with default panning set
  Stereo                      = 0x40, // stereo sample
  PingPongFlag                = 0x80, // when flag is on, sample is processed backwards
  Adlib                       = 0x20000000, // OPL mode
}
