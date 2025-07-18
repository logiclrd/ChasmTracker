using ChasmTracker.Songs;

namespace ChasmTracker.FileTypes;

public static class EffectExtensions
{
	public static int GetWeight(this Effects effect)
	{
		switch (effect)
		{
			case Effects.PatternBreak:         return 248;
			case Effects.PositionJump:         return 240;
			case Effects.Speed:                return 232;
			case Effects.Tempo:                return 224;
			case Effects.GlobalVolume:         return 216;
			case Effects.GlobalVolumeSlide:    return 208;
			case Effects.ChannelVolume:        return 200;
			case Effects.ChannelVolumeSlide:   return 192;
			case Effects.TonePortamentoVolume: return 184;
			case Effects.TonePortamento:       return 176;
			case Effects.Arpeggio:             return 168;
			case Effects.Retrigger:            return 160;
			case Effects.Tremor:               return 152;
			case Effects.Offset:               return 144;
			case Effects.Volume:               return 136;
			case Effects.VibratoVolume:        return 128;
			case Effects.VolumeSlide:          return 120;
			case Effects.PortamentoDown:       return 112;
			case Effects.PortamentoUp:         return 104;
			case Effects.NoteSlideDown:        return  96; // IMF Hxy
			case Effects.NoteSlideUp:          return  88; // IMF Gxy
			case Effects.Panning:              return  80;
			case Effects.PanningSlide:         return  72;
			case Effects.MIDI:                 return  64;
			case Effects.Special:              return  56;
			case Effects.Panbrello:            return  48;
			case Effects.Vibrato:              return  40;
			case Effects.FineVibrato:          return  32;
			case Effects.Tremolo:              return  24;
			case Effects.KeyOff:               return  16;
			case Effects.SetEnvelopePosition:  return   8;
			case Effects.None:                 return   0;

			default: return 0;
		}
	}
}
