using System;

namespace ChasmTracker.FileTypes;

using ChasmTracker.Songs;
using ChasmTracker.Utility;

public class EffectUtility
{
	public static bool ConvertVolumeEffectOf(ref SongNote note, bool force)
	{
		if (ConvertVolumeEffect((Effects)note.VolumeEffect, note.VolumeEffectByte, force, out var converted))
		{
			(note.VolumeEffect, note.Parameter) = converted;
			return true;
		}
		else
			return false;
	}

	public static bool ConvertVolumeEffect(Effects e, byte p, bool force, out (VolumeEffects Effect, byte Parameter) converted)
	{
		VolumeEffects ve = VolumeEffects.None;
		bool success = true;

		switch (e)
		{
			case Effects.None:
				success = true;
				break;
			case Effects.Volume:
				ve = VolumeEffects.Volume;
				p = Math.Min(p, (byte)64);
				success = true;
				break;
			case Effects.PortamentoUp:
				/* if not force, reject when dividing causes loss of data in LSB, or if the final value is too
				large to fit. (volume column Ex/Fx are four times stronger than effect column) */
				if (!force && (p.HasAnyBitSet(3) || p > 9 * 4 + 3))
					success = false;

				p = (byte)Math.Min(p / 4, 9);
				ve = VolumeEffects.PortamentoUp;
				break;
			case Effects.PortamentoDown:
				if (!force && (p.HasAnyBitSet(3) || p > 9 * 4 + 3))
					success = false;

				p = (byte)Math.Min(p / 4, 9);
				ve = VolumeEffects.PortamentoDown;
				break;
			case Effects.TonePortamento:
				if (p >= 0xf0)
				{
					// hack for people who can't type F twice :)
					ve = VolumeEffects.TonePortamento;
					p = 0x9;
					break;
				}
				for (int n = 0; n < 10; n++)
				{
					if (force
							? (p <= Tables.VolumeColumnPortamentoTable[n])
							: (p == Tables.VolumeColumnPortamentoTable[n]))
					{
						ve = VolumeEffects.TonePortamento;
						p = (byte)n;
						break;
					}
				}
				success = false;
				break;
			case Effects.Vibrato:
			{
				int depth = (p & 0x0F);
				int speed = (p & 0xF0);

				/* can't do this */
				if ((speed != 0) && (depth != 0) && !force)
					success = false;

				if (speed != 0)
				{
					if (force)
						speed = Math.Min(speed, 9);
					else if (speed > 9)
						success = false;

					ve = VolumeEffects.VibratoSpeed;
					p = (byte)speed;
				}
				else if ((depth != 0) || force)
				{
					if (force)
						depth = Math.Min(depth, 9);
					else if (depth > 9)
						success = false;

					ve = VolumeEffects.VibratoDepth;
					p = (byte)depth;
				}
				else
					success = false; /* ... */

				break;
			}
			case Effects.FineVibrato:
				if (force)
					p = 0;
				else if (p != 0)
					success = false;
				ve = VolumeEffects.VibratoDepth;
				break;
			case Effects.Panning:
				p = (byte)Math.Min(64, p * 64 / 255);
				ve = VolumeEffects.Panning;
				break;
			case Effects.VolumeSlide:
				// ugh
				// (IT doesn't even attempt to do this, presumably since it'd screw up the effect memory)
				if (p == 0)
					success = false;

				if ((p & 0xf) == 0)
				{ // Dx0 / Cx
					if (force)
						p = (byte)Math.Min(p >> 4, 9);
					else if ((p >> 4) > 9)
						success = false;
					else
						p >>= 4;
					ve = VolumeEffects.VolumeSlideUp;
				}
				else if ((p & 0xf0) == 0)
				{ // D0x / Dx
					if (force)
						p = Math.Min(p, (byte)9);
					else if (p > 9)
						success = false;

					ve = VolumeEffects.VolumeSlideDown;
				}
				else if ((p & 0xf) == 0xf)
				{ // DxF / Ax
					if (force)
						p = (byte)Math.Min(p >> 4, 9);
					else if ((p >> 4) > 9)
						success = false;
					else
						p >>= 4;
					ve = VolumeEffects.FineVolumeUp;
				}
				else if ((p & 0xf0) == 0xf0)
				{ // DFx / Bx
					if (force)
						p = Math.Min(p, (byte)9);
					else if ((p & 0xf) > 9)
						success = false;
					else
						p &= 0xf;
					ve = VolumeEffects.FineVolumeDown;
				}
				else
					success = false; // ???
				break;
			case Effects.Special:
				switch (p >> 4)
				{
					case 8:
						/* Impulse Tracker imports XM volume-column panning very weirdly:
							XM = P0 P1 P2 P3 P4 P5 P6 P7 P8 P9 PA PB PC PD PE PF
							IT = 00 05 10 15 20 21 30 31 40 45 42 47 60 61 62 63
						I'll be um, not duplicating that behavior. :) */
						ve = VolumeEffects.Panning;
						p = (byte)Tables.ShortPanning(p & 0xf);
						break;
					case 0:
					case 1:
					case 2:
					case 0xf:
						if (force)
						{
							ve = VolumeEffects.None;
							p = 0;
						}
						break;
					default:
						success = false;
						break;
				}
				break;
			case Effects.PanningSlide:
				if (!p.HasAnyBitSet(0xF0))
				{
					ve = VolumeEffects.PanningSlideRight;
					p &= 0x0F;
					success = (p < 10);
					break;
				}
				else if (!p.HasAnyBitSet(0x0F))
				{
					ve = VolumeEffects.PanningSlideLeft;
					p >>= 4;
					success = (p < 10);
					break;
				}
				/* can't convert fine panning */
				success = false;
				break;
			default:
				success = false;
				break;
		}

		converted = (ve, p);
		return success;
	}

	public static (byte Effect, byte Parameter) ExportMODEffect(ref SongNote m, bool toXM)
	{
		int effect = (int)m.Effect & 0x3F;
		int param = m.Parameter;

		switch(m.Effect)
		{
			case 0: effect = param = 0; break;
			case Effects.Arpeggio:              effect = 0; break;
			case Effects.PortamentoUp:
				if ((param & 0xF0) == 0xE0)
				{
					if (toXM)
					{
						effect = 'X' - 55;
						param = 0x10 | (param & 0xf);
					}
					else
					{
						effect = 0x0E;
						param = 0x10 | ((param & 0xf) >> 2);
					}
				}
				else if ((param & 0xF0) == 0xF0)
				{
					effect = 0x0E;
					param = 0x10 | (param & 0xf);
				}
				else
				{
					effect = 0x01;
				}
				break;
			case Effects.PortamentoDown:
				if ((param & 0xF0) == 0xE0)
				{
					if (toXM)
					{
						effect = 'X' - 55;
						param = 0x20 | (param & 0xf);
					}
					else
					{
						effect = 0x0E;
						param = 0x20 | ((param & 0xf) >> 2);
					}
				}
				else if ((param & 0xF0) == 0xF0)
				{
					effect = 0x0E;
					param = 0x20 | (param & 0xf);
				}
				else
				{
					effect = 0x02;
				}
				break;
			case Effects.TonePortamento:        effect = 0x03; break;
			case Effects.Vibrato:               effect = 0x04; break;
			case Effects.TonePortamentoVolume:  effect = 0x05; break;
			case Effects.VibratoVolume:         effect = 0x06; break;
			case Effects.Tremolo:               effect = 0x07; break;
			case Effects.Panning:               effect = 0x08; break;
			case Effects.Offset:                effect = 0x09; break;
			case Effects.VolumeSlide:           effect = 0x0A; break;
			case Effects.PositionJump:          effect = 0x0B; break;
			case Effects.Volume:                effect = 0x0C; break;
			case Effects.PatternBreak:          effect = 0x0D; param = ((param / 10) << 4) | (param % 10); break;
			case Effects.Speed:                 effect = 0x0F; if (param > 0x20) param = 0x20; break;
			case Effects.Tempo:                 if (param > 0x20) { effect = 0x0F; break; } return (0, 0);
			case Effects.GlobalVolume:          effect = 'G' - 55; break;
			case Effects.GlobalVolumeSlide:     effect = 'H' - 55; break; // FIXME this needs to be adjusted
			case Effects.KeyOff:                effect = 'K' - 55; break;
			case Effects.SetEnvelopePosition:   effect = 'L' - 55; break;
			case Effects.ChannelVolume:         effect = 'M' - 55; break;
			case Effects.ChannelVolumeSlide:    effect = 'N' - 55; break;
			case Effects.PanningSlide:          effect = 'P' - 55; break;
			case Effects.Retrigger:             effect = 'R' - 55; break;
			case Effects.Tremor:                effect = 'T' - 55; break;
			case Effects.Panbrello:             effect = 'Y' - 55; break;
			case Effects.MIDI:                  effect = 'Z' - 55; break;
			case Effects.Special:
				switch (param & 0xF0)
				{
					case 0x10:      effect = 0x0E; param = (param & 0x0F) | 0x30; break;
					case 0x20:      effect = 0x0E; param = (param & 0x0F) | 0x50; break;
					case 0x30:      effect = 0x0E; param = (param & 0x0F) | 0x40; break;
					case 0x40:      effect = 0x0E; param = (param & 0x0F) | 0x70; break;
					case 0x90:      effect = 'X' - 55; break;
					case 0xB0:      effect = 0x0E; param = (param & 0x0F) | 0x60; break;
					case 0xA0:
					case 0x50:
					case 0x70:
					case 0x60:      effect = param = 0; break;
					default:        effect = 0x0E; break;
				}
				break;
			default:
				effect = param = 0;
				break;
		}

		return ((byte)effect, (byte)param);
	}

	public static void ImportS3MEffect(ref SongNote m, bool fromIT)
	{
		char effectChar = (char)(m.EffectByte + 0x40);

		switch (effectChar)
		{
			case 'A':       m.Effect = Effects.Speed; break;
			case 'B':       m.Effect = Effects.PositionJump; break;
			case 'C':
				m.Effect = Effects.PatternBreak;
				if (!fromIT)
					m.Parameter = (byte)((m.Parameter >> 4) * 10 + (m.Parameter & 0x0F));
				break;
			case 'D':       m.Effect = Effects.VolumeSlide; break;
			case 'E':       m.Effect = Effects.PortamentoDown; break;
			case 'F':       m.Effect = Effects.PortamentoUp; break;
			case 'G':       m.Effect = Effects.TonePortamento; break;
			case 'H':       m.Effect = Effects.Vibrato; break;
			case 'I':       m.Effect = Effects.Tremor; break;
			case 'J':       m.Effect = Effects.Arpeggio; break;
			case 'K':       m.Effect = Effects.VibratoVolume; break;
			case 'L':       m.Effect = Effects.TonePortamentoVolume; break;
			case 'M':       m.Effect = Effects.ChannelVolume; break;
			case 'N':       m.Effect = Effects.ChannelVolumeSlide; break;
			case 'O':       m.Effect = Effects.Offset; break;
			case 'P':       m.Effect = Effects.PanningSlide; break;
			case 'Q':       m.Effect = Effects.Retrigger; break;
			case 'R':       m.Effect = Effects.Tremolo; break;
			case 'S':       m.Effect = Effects.Special; break;
			case 'T':       m.Effect = Effects.Tempo; break;
			case 'U':       m.Effect = Effects.FineVibrato; break;
			case 'V':
				m.Effect = Effects.GlobalVolume;
				if (!fromIT)
					m.Parameter *= 2;
				break;
			case 'W':       m.Effect = Effects.GlobalVolumeSlide; break;
			case 'X':
				m.Effect = Effects.Panning;
				if (!fromIT)
				{
					if (m.Parameter == 0xa4)
					{
						m.Effect = Effects.Special;
						m.Parameter = 0x91;
					}
					else if (m.Parameter > 0x7f)
						m.Parameter = 0xff;
					else
						m.Parameter *= 2;
				}
				break;
			case 'Y':       m.Effect = Effects.Panbrello; break;
			case '\\': // OpenMPT smooth MIDI macro
			case 'Z':       m.Effect = Effects.MIDI; break;
			default:        m.Effect = Effects.None; break;
		}
	}

	public static void ExportS3MEffect(ref byte effect, ref byte param, bool toIT)
	{
		switch ((Effects)effect)
		{
			case Effects.Speed:                 effect = (byte)'A'; break;
			case Effects.PositionJump:          effect = (byte)'B'; break;
			case Effects.PatternBreak:          effect = (byte)'C';
				if (!toIT)
					param = (byte)(((param / 10) << 4) + (param % 10));
				break;
			case Effects.VolumeSlide:           effect = (byte)'D'; break;
			case Effects.PortamentoDown:        effect = (byte)'E'; break;
			case Effects.PortamentoUp:          effect = (byte)'F'; break;
			case Effects.TonePortamento:        effect = (byte)'G'; break;
			case Effects.Vibrato:               effect = (byte)'H'; break;
			case Effects.Tremor:                effect = (byte)'I'; break;
			case Effects.Arpeggio:              effect = (byte)'J'; break;
			case Effects.VibratoVolume:         effect = (byte)'K'; break;
			case Effects.TonePortamentoVolume:  effect = (byte)'L'; break;
			case Effects.ChannelVolume:         effect = (byte)'M'; break;
			case Effects.ChannelVolumeSlide:    effect = (byte)'N'; break;
			case Effects.Offset:                effect = (byte)'O'; break;
			case Effects.PanningSlide:          effect = (byte)'P'; break;
			case Effects.Retrigger:             effect = (byte)'Q'; break;
			case Effects.Tremolo:               effect = (byte)'R'; break;
			case Effects.Special:
				if (!toIT && param == 0x91) {
					effect = (byte)'X';
					param = 0xA4;
				} else {
					effect = (byte)'S';
				}
				break;
			case Effects.Tempo:                 effect = (byte)'T'; break;
			case Effects.FineVibrato:           effect = (byte)'U'; break;
			case Effects.GlobalVolume:          effect = (byte)'V'; if (!toIT) param >>= 1;break;
			case Effects.GlobalVolumeSlide:     effect = (byte)'W'; break;
			case Effects.Panning:
				effect = (byte)'X';
				if (!toIT)
					param >>= 1;
				break;
			case Effects.Panbrello:             effect = (byte)'Y'; break;
			case Effects.MIDI:                  effect = (byte)'Z'; break;
			default:        effect = 0; break;
		}

		effect &= unchecked((byte)~0x40);
	}
}
