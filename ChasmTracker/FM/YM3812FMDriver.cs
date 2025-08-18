using System;
using System.Runtime.CompilerServices;

namespace ChasmTracker.FM;

using ChasmTracker.Utility;

// license:GPL-2.0+
// copyright-holders:Jarek Burczynski,Tatsuyuki Satoh
/*
**
** File: fmopl.c - software implementation of FM sound generator
**                                            types OPL and OPL2
**
** Copyright Jarek Burczynski (bujar at mame dot net)
** Copyright Tatsuyuki Satoh , MultiArcadeMachineEmulator development
**
** Version 0.72
**

Revision History:

11-08-2025 Jonathan Gilbert:
 - translate to C#

04-08-2003 Jarek Burczynski:
 - removed BFRDY hack. BFRDY is busy flag, and it should be 0 only when the chip
	 handles memory read/write or during the adpcm synthesis when the chip
	 requests another byte of ADPCM data.

24-07-2003 Jarek Burczynski:
 - added a small hack for Y8950 status BFRDY flag (bit 3 should be set after
	 some (unknown) delay). Right now it's always set.

14-06-2003 Jarek Burczynski:
 - implemented all of the status register flags in Y8950 emulation
 - renamed y8950_set_delta_t_memory() parameters from _rom_ to _mem_ since
	 they can be either RAM or ROM

08-10-2002 Jarek Burczynski (thanks to Dox for the YM3526 chip)
 - corrected ym3526_read() to always set bit 2 and bit 1
	 to HIGH state - identical to ym3812_read (verified on real YM3526)

04-28-2002 Jarek Burczynski:
 - binary exact Envelope Generator (verified on real YM3812);
	 compared to YM2151: the EG clock is equal to internal_clock,
	 rates are 2 times slower and volume resolution is one bit less
 - modified interface functions (they no longer return pointer -
	 that's internal to the emulator now):
		- new wrapper functions for OPLCreate: ym3526_init(), ym3812_init() and y8950_init()
 - corrected 'off by one' error in feedback calculations (when feedback is off)
 - enabled waveform usage (credit goes to Vlad Romascanu and zazzal22)
 - speeded up noise generator calculations (Nicola Salmoria)

03-24-2002 Jarek Burczynski (thanks to Dox for the YM3812 chip)
 Complete rewrite (all verified on real YM3812):
 - corrected sin_tab and tl_tab data
 - corrected operator output calculations
 - corrected waveform_select_enable register;
	 simply: ignore all writes to waveform_select register when
	 waveform_select_enable == 0 and do not change the waveform previously selected.
 - corrected KSR handling
 - corrected Envelope Generator: attack shape, Sustain mode and
	 Percussive/Non-percussive modes handling
 - Envelope Generator rates are two times slower now
 - LFO amplitude (tremolo) and phase modulation (vibrato)
 - rhythm sounds phase generation
 - white noise generator (big thanks to Olivier Galibert for mentioning Berlekamp-Massey algorithm)
 - corrected key on/off handling (the 'key' signal is ORed from three sources: FM, rhythm and CSM)
 - funky details (like ignoring output of operator 1 in BD rhythm sound when connect == 1)

12-28-2001 Acho A. Tang
 - reflected Delta-T EOS status on Y8950 status port.
 - fixed subscription range of attack/decay tables


		To do:
				add delay before key off in CSM mode (see CSMKeyControll)
				verify volume of the FM part on the Y8950
*/

/* OPL2 */

public class YM3812FMDriver : FMDriver
{
	public override uint RateDivisor => 288u;

	/* output final shift */
	const int FinalShift = 0;
	const int MaxOut = short.MaxValue;
	const int MinOut = short.MinValue;

	const int FrequencyShift         = 16;  /* 16.16 fixed point (frequency calculations) */
	const int EnvelopeGeneratorShift = 16;  /* 16.16 fixed point (EG timing)              */
	const int LFOShift               = 24;  /*  8.24 fixed point (LFO calculations)       */

	const int FrequencyMask = ((1 << FrequencyShift) - 1);

	/* envelope output entries */
	const int EnvelopeBits      = 10;
	const int EnvelopeLength    = 1 << EnvelopeBits;
	internal const double EnvelopeStep   = 128.0 / EnvelopeLength;

	const int MaxAttenuationIndex = (1 << (EnvelopeBits - 1)) - 1; /* 511 */
	const int MinAttenuationIndex = 0;

	const int EnvelopeQuiet = YM3812Tables.TLTableLength >> 4;


	/* register number to channel number , slot offset */
	const int Slot1 = 0;
	const int Slot2 = 1;

	/* Envelope Generator phases */
	enum EnvelopeGeneratorPhase : byte
	{
		Attack = 4,
		Decay = 3,
		Sustain = 2,
		Release = 1,

		Off = 0,
	}

	/* ---------- Generic interface section ---------- */
	const OPLType YM3526 = 0;
	const OPLType YM3812 = OPLType.WaveformSelect;
	//const OPLType Y8950 = OPLType.ADPCM | OPLType.Keyboard | OPLType.IO;

	unsafe struct OPLSlot
	{
		public uint   ar;         /* attack rate: AR<<2           */
		public uint   dr;         /* decay rate:  DR<<2           */
		public uint   rr;         /* release rate:RR<<2           */
		public byte   KSR;        /* key scale rate               */
		public byte   ksl;        /* keyscale level               */
		public byte   ksr;        /* key scale rate: kcode>>KSR   */
		public byte   mul;        /* multiple: mul_tab[ML]        */

		/* Phase Generator */
		public uint   Cnt;        /* frequency counter            */
		public uint   Incr;       /* frequency counter step       */
		public byte   FB;         /* feedback shift value         */
		public Shared<int> connect1;  /* slot1 output pointer         */
		public OPLSlotOutArray op1_out; /* slot1 output for feedback    */
		public byte   CON;        /* connection (algorithm) type  */

		/* Envelope Generator */
		public bool   eg_type;    /* percussive/non-percussive mode */
		public EnvelopeGeneratorPhase  state;      /* phase type                   */
		public uint   TL;         /* total level: TL << 2         */
		public int    TLL;        /* adjusted now TL              */
		public int    volume;     /* envelope counter             */
		public uint   sl;         /* sustain level: sl_tab[SL]    */
		public byte   eg_sh_ar;   /* (attack state)               */
		public byte   eg_sel_ar;  /* (attack state)               */
		public byte   eg_sh_dr;   /* (decay state)                */
		public byte   eg_sel_dr;  /* (decay state)                */
		public byte   eg_sh_rr;   /* (release state)              */
		public byte   eg_sel_rr;  /* (release state)              */
		public uint   key;        /* 0 = KEY OFF, >0 = KEY ON     */

		/* LFO */
		public uint   AMmask;     /* LFO Amplitude Modulation enable mask */
		public bool   vib;        /* LFO Phase Modulation enable flag (active high)*/

		/* waveform select */
		public ushort wavetable;
	}

	[InlineArray(2)]
	struct OPLChannelSlotArray
	{
		OPLSlot _element0;
	}

	struct OPLChannel
	{
		public OPLChannelSlotArray Slot;
		/* phase generator state */
		public uint block_fnum; /* block+fnum                   */
		public uint fc;         /* Freq. Increment base         */
		public uint ksl_base;   /* KeyScaleLevel Base step      */
		public byte kcode;      /* key code (for key scaling)   */

		/* CSM Key Controll */
		public void KeyControll()
		{
			FM_KEYON (ref Slot[Slot1], 4);
			FM_KEYON (ref Slot[Slot2], 4);

			/* The key off should happen exactly one sample later - not implemented correctly yet */

			FM_KEYOFF(ref Slot[Slot1], ~4u);
			FM_KEYOFF(ref Slot[Slot2], ~4u);
		}
	}

	/* OPL state */
	/* FM channel slots */
	OPLChannel[] P_CH = new OPLChannel[9];                /* OPL/OPL2 chips have 9 channels*/

	uint      eg_cnt;                 /* global envelope generator counter    */
	uint      eg_timer;               /* global envelope generator counter works at frequency = chipclock/72 */
	uint      eg_timer_add;           /* step of eg_timer                     */
	uint      eg_timer_overflow;      /* envelope generator timer overlfows every 1 sample (on real chip) */

	byte      rhythm;                 /* Rhythm mode                  */

	uint[]    fn_tab = new uint[1024];           /* fnumber->increment counter   */

	/* LFO */
	uint      LFO_AM;
	int       LFO_PM;

	bool      lfo_am_depth;
	byte      lfo_pm_depth_range;
	uint      lfo_am_cnt;
	uint      lfo_am_inc;
	uint      lfo_pm_cnt;
	uint      lfo_pm_inc;

	uint      noise_rng;              /* 23 bit noise shift register  */
	uint      noise_p;                /* current noise 'phase'        */
	uint      noise_f;                /* current noise period         */

	bool      wavesel;                /* waveform select enable flag  */

	int[]     T = new int[2];         /* timer counters               */
	bool[]    st = new bool[2];       /* timer enable                 */

	enum StatusFlags : byte
	{
		ST1 = 0x01,
		ST2 = 0x02,
		X = 0x04,
		BufferReady = 0x08,
		EOS = 0x10,
		TimerB = 0x20,
		TimerA = 0x40,
		IRQEnabled = 0x80,
	}

	OPLType type;                     /* chip type                    */
	byte    address;                  /* address register             */
	StatusFlags status;                   /* status flag                  */
	StatusFlags statusmask;               /* status mask                  */
	StatusFlags mode;                     /* Reg.08 : CSM,notesel,etc.    */

	uint     clock;                   /* master clock  (Hz)           */
	uint     rate;                    /* sampling rate (Hz)           */
	double freqbase;                /* frequency base               */
	double TimerBase;               /* Timer base time (==sampling time)*/
	Shared<int> phase_modulation = new Shared<int>();    /* phase modulation input (SLOT 2) */
	Shared<int> output = new Shared<int>();

	internal const int RateSteps = 8;

	ref OPLSlot Slot7_1 => ref P_CH[7].Slot[Slot1];
	ref OPLSlot Slot7_2 => ref P_CH[7].Slot[Slot2];
	ref OPLSlot Slot8_1 => ref P_CH[8].Slot[Slot1];
	ref OPLSlot Slot8_2 => ref P_CH[8].Slot[Slot2];

	/* status set and IRQ handling */
	void SetStatus(StatusFlags flag)
	{
		/* set status flag */
		status |= flag;
		if(!status.HasAllFlags(StatusFlags.IRQEnabled))
		{
			if(status.HasAnyFlag(statusmask))
			{
				/* IRQ on */
				status |= StatusFlags.IRQEnabled;
				/* callback user interrupt handler (IRQ is OFF to ON) */
				OnIRQ(true);
			}
		}
	}

	/* status reset and IRQ handling */
	void ResetStatus(StatusFlags flag)
	{
		/* reset status flag */
		status &=~flag;
		if(status.HasAllFlags(StatusFlags.IRQEnabled))
		{
			if (!status.HasAnyFlag(statusmask))
			{
				status &= ~StatusFlags.IRQEnabled;
				/* callback user interrupt handler (IRQ is ON to OFF) */
				OnIRQ(false);
			}
		}
	}

	/* IRQ mask set */
	void SetStatusMask(StatusFlags flag)
	{
		statusmask = flag;
		/* IRQ handling check */
		SetStatus(0);
		ResetStatus(0);
	}

	/* advance LFO to next sample */
	void advance_lfo()
	{
		/* LFO */
		lfo_am_cnt += lfo_am_inc;
		if (lfo_am_cnt >= ((uint)YM3812Tables.lfo_am_table.Length<<LFOShift) )  /* lfo_am_table is 210 elements long */
			lfo_am_cnt -= (uint)YM3812Tables.lfo_am_table.Length<<LFOShift;

		var tmp = YM3812Tables.lfo_am_table[ lfo_am_cnt >> LFOShift ];

		if (lfo_am_depth)
			LFO_AM = tmp;
		else
			LFO_AM = unchecked((uint)(tmp >> 2));

		lfo_pm_cnt += lfo_pm_inc;
		LFO_PM = unchecked((int)(((lfo_pm_cnt>>LFOShift) & 7) | lfo_pm_depth_range));
	}

	/* advance to next sample */
	void advance()
	{
		eg_timer += eg_timer_add;

		while (eg_timer >= eg_timer_overflow)
		{
			eg_timer -= eg_timer_overflow;

			eg_cnt++;

			for (int i=0; i<9*2; i++)
			{
				ref var CH  = ref P_CH[i/2];
				ref var op  = ref CH.Slot[i&1];

				/* Envelope Generator */
				switch(op.state)
				{
				case EnvelopeGeneratorPhase.Attack:        /* attack phase */
					if ( !eg_cnt.HasAnyBitSet((1<<op.eg_sh_ar)-1) )
					{
						op.volume += (~op.volume *
							(YM3812Tables.eg_inc[op.eg_sel_ar + ((eg_cnt>>op.eg_sh_ar)&7)])
						) >> 3;

						if (op.volume <= MinAttenuationIndex)
						{
							op.volume = MinAttenuationIndex;
							op.state = EnvelopeGeneratorPhase.Decay;
						}

					}
				break;

				case EnvelopeGeneratorPhase.Decay:    /* decay phase */
					if ( !eg_cnt.HasAnyBitSet((1<<op.eg_sh_dr)-1) )
					{
						op.volume += YM3812Tables.eg_inc[op.eg_sel_dr + ((eg_cnt>>op.eg_sh_dr)&7)];

						if ( unchecked((uint)op.volume) >= op.sl )
							op.state = EnvelopeGeneratorPhase.Sustain;

					}
				break;

				case EnvelopeGeneratorPhase.Sustain:    /* sustain phase */

					/* this is important behaviour:
					one can change percusive/non-percussive modes on the fly and
					the chip will remain in sustain phase - verified on real YM3812 */

					if(op.eg_type)     /* non-percussive mode */
					{
										/* do nothing */
					}
					else                /* percussive mode */
					{
						/* during sustain phase chip adds Release Rate (in percussive mode) */
						if ( !eg_cnt.HasAnyBitSet((1<<op.eg_sh_rr)-1) )
						{
							op.volume += YM3812Tables.eg_inc[op.eg_sel_rr + ((eg_cnt>>op.eg_sh_rr)&7)];

							if ( op.volume >= MaxAttenuationIndex )
								op.volume =  MaxAttenuationIndex;
						}
						/* else do nothing in sustain phase */
					}
				break;

				case EnvelopeGeneratorPhase.Release:    /* release phase */
					if ( !eg_cnt.HasAnyBitSet((1<<op.eg_sh_rr)-1) )
					{
						op.volume += YM3812Tables.eg_inc[op.eg_sel_rr + ((eg_cnt>>op.eg_sh_rr)&7)];

						if ( op.volume >= MaxAttenuationIndex )
						{
							op.volume = MaxAttenuationIndex;
							op.state = EnvelopeGeneratorPhase.Off;
						}

					}
				break;

				default:
				break;
				}
			}
		}

		for (int i=0; i<9*2; i++)
		{
			ref var CH  = ref P_CH[i/2];
			ref var op  = ref CH.Slot[i&1];

			/* Phase Generator */
			if(op.vib)
			{
				uint block_fnum = CH.block_fnum;

				uint fnum_lfo   = (block_fnum&0x0380) >> 7;

				int lfo_fn_table_index_offset = YM3812Tables.lfo_pm_table[LFO_PM + 16*fnum_lfo ];

				if (lfo_fn_table_index_offset != 0)  /* LFO phase modulation active */
				{
					block_fnum = unchecked((uint)(block_fnum + lfo_fn_table_index_offset));
					byte block = unchecked((byte)((block_fnum&0x1c00) >> 10));
					op.Cnt += (fn_tab[block_fnum&0x03ff] >> (7-block)) * op.mul;
				}
				else    /* LFO phase modulation  = zero */
				{
					op.Cnt += op.Incr;
				}
			}
			else    /* LFO phase modulation disabled for this operator */
			{
				op.Cnt += op.Incr;
			}
		}

		/*  The Noise Generator of the YM3812 is 23-bit shift register.
		*   Period is equal to 2^23-2 samples.
		*   Register works at sampling frequency of the chip, so output
		*   can change on every sample.
		*
		*   Output of the register and input to the bit 22 is:
		*   bit0 XOR bit14 XOR bit15 XOR bit22
		*
		*   Simply use bit 22 as the noise output.
		*/

		noise_p += noise_f;
		uint ii = noise_p >> FrequencyShift;        /* number of events (shifts of the shift register) */
		noise_p &= FrequencyMask;
		while (ii != 0)
		{
			/*
			uint j;
			j = ( (noise_rng) ^ (noise_rng>>14) ^ (noise_rng>>15) ^ (noise_rng>>22) ) & 1;
			noise_rng = (j<<22) | (noise_rng>>1);
			*/

			/*
					Instead of doing all the logic operations above, we
					use a trick here (and use bit 0 as the noise output).
					The difference is only that the noise bit changes one
					step ahead. This doesn't matter since we don't know
					what is real state of the noise_rng after the reset.
			*/

			if (noise_rng.HasBitSet(1)) noise_rng ^= 0x800302;
			noise_rng >>= 1;

			ii--;
		}
	}

	int op_calc(uint phase, uint env, int pm, uint wave_tab)
	{
		uint p;

		p = (env<<4) + YM3812Tables.sin_tab[wave_tab + ((((int)((phase & ~FrequencyMask) + (pm << 16)))
								>> FrequencyShift) & YM3812Tables.SineMask)];

		if (p >= YM3812Tables.TLTableLength)
			return 0;
		return YM3812Tables.tl_tab[p];
	}

	int op_calc1(uint phase, uint env, int pm, uint wave_tab)
	{
		uint p;

		p = (env<<4) + YM3812Tables.sin_tab[wave_tab + ((((int)((phase & ~FrequencyMask) + pm))
								>> FrequencyShift) & YM3812Tables.SineMask)];

		if (p >= YM3812Tables.TLTableLength)
			return 0;
		return YM3812Tables.tl_tab[p];
	}

	uint volume_calc(ref OPLSlot OP) => unchecked((uint)OP.TLL) + ((uint)OP.volume) + (LFO_AM & OP.AMmask);

	/* calculate output */
	unsafe void OPL_CALC_CH(ref OPLChannel CH)
	{
		phase_modulation.Value = 0;

		/* SLOT 1 */
		{
			ref var SLOT = ref CH.Slot[Slot1];
			uint env  = volume_calc(ref SLOT);
			int @out  = SLOT.op1_out[0] + SLOT.op1_out[1];
			SLOT.op1_out[0] = SLOT.op1_out[1];
			SLOT.connect1.Value += SLOT.op1_out[0];
			SLOT.op1_out[1] = 0;
			if( env < EnvelopeQuiet )
			{
				if (SLOT.FB == 0)
					@out = 0;
				SLOT.op1_out[1] = op_calc1(SLOT.Cnt, env, @out<<SLOT.FB, SLOT.wavetable );
			}
		}

		/* SLOT 2 */
		{
			ref var SLOT = ref CH.Slot[Slot2];
			uint env = volume_calc(ref SLOT);
			if( env < EnvelopeQuiet )
				output.Value += op_calc(SLOT.Cnt, env, phase_modulation, SLOT.wavetable);
		}
	}

	/*
			operators used in the rhythm sounds generation process:

			Envelope Generator:

	channel  operator  register number   Bass  High  Snare Tom  Top
	/ slot   number    TL ARDR SLRR Wave Drum  Hat   Drum  Tom  Cymbal
	6 / 0   12        50  70   90   f0  +
	6 / 1   15        53  73   93   f3  +
	7 / 0   13        51  71   91   f1        +
	7 / 1   16        54  74   94   f4              +
	8 / 0   14        52  72   92   f2                    +
	8 / 1   17        55  75   95   f5                          +

			Phase Generator:

	channel  operator  register number   Bass  High  Snare Tom  Top
	/ slot   number    MULTIPLE          Drum  Hat   Drum  Tom  Cymbal
	6 / 0   12        30                +
	6 / 1   15        33                +
	7 / 0   13        31                      +     +           +
	7 / 1   16        34                -----  n o t  u s e d -----
	8 / 0   14        32                                  +
	8 / 1   17        35                      +                 +

	channel  operator  register number   Bass  High  Snare Tom  Top
	number   number    BLK/FNUM2 FNUM    Drum  Hat   Drum  Tom  Cymbal
		6     12,15     B6        A6      +

		7     13,16     B7        A7            +     +           +

		8     14,17     B8        A8            +           +     +

	*/

	/* calculate rhythm */

	void OPL_CALC_RH(OPLChannel[] CH, bool noise)
	{
		/* Bass Drum (verified on real YM3812):
			- depends on the channel 6 'connect' register:
					when connect = 0 it works the same as in normal (non-rhythm) mode (op1->op2->out)
					when connect = 1 _only_ operator 2 is present on output (op2->out), operator 1 is ignored
			- output sample always is multiplied by 2
		*/

		phase_modulation.Value = 0;

		/* SLOT 1 */
		{
			ref var SLOT = ref CH[6].Slot[Slot1];

			uint env = volume_calc(ref SLOT);

			int @out = SLOT.op1_out[0] + SLOT.op1_out[1];
			SLOT.op1_out[0] = SLOT.op1_out[1];

			if (SLOT.CON == 0)
				phase_modulation.Value = SLOT.op1_out[0];
			/* else ignore output of operator 1 */

			SLOT.op1_out[1] = 0;
			if( env < EnvelopeQuiet )
			{
				if (SLOT.FB == 0)
					@out = 0;
				SLOT.op1_out[1] = op_calc1(SLOT.Cnt, env, @out<<SLOT.FB, SLOT.wavetable );
			}
		}

		/* SLOT 2 */
		{
			ref var SLOT = ref CH[6].Slot[Slot2];

			uint env = volume_calc(ref SLOT);

			if( env < EnvelopeQuiet )
				output.Value += op_calc(SLOT.Cnt, env, phase_modulation, SLOT.wavetable) * 2;
		}

		/* Phase generation is based on: */
		/* HH  (13) channel 7->slot 1 combined with channel 8->slot 2
			(same combination as TOP CYMBAL but different output phases) */
		/* SD  (16) channel 7->slot 1 */
		/* TOM (14) channel 8->slot 1 */
		/* TOP (17) channel 7->slot 1 combined with channel 8->slot 2
			(same combination as HIGH HAT but different output phases) */

		/* Envelope generation based on: */
		/* HH  channel 7->slot1 */
		/* SD  channel 7->slot2 */
		/* TOM channel 8->slot1 */
		/* TOP channel 8->slot2 */


		/* The following formulas can be well optimized.
			I leave them in direct form for now (in case I've missed something).
		*/

		/* High Hat (verified on real YM3812) */
		{
			uint env = volume_calc(ref Slot7_1);
			if( env < EnvelopeQuiet )
			{
				/* high hat phase generation:
						phase = d0 or 234 (based on frequency only)
						phase = 34 or 2d0 (based on noise)
				*/

				/* base frequency derived from operator 1 in channel 7 */
				uint bit7 = ((Slot7_1.Cnt >> FrequencyShift) >> 7) & 1;
				uint bit3 = ((Slot7_1.Cnt >> FrequencyShift) >> 3) & 1;
				uint bit2 = ((Slot7_1.Cnt >> FrequencyShift) >> 2) & 1;

				uint res1 = (bit2 ^ bit7) | bit3;

				/* when res1 = 0 phase = 0x000 | 0xd0; */
				/* when res1 = 1 phase = 0x200 | (0xd0>>2); */
				uint phase = (res1 != 0) ? (0x200u | (0xd0 >> 2)) : 0xd0;

				/* enable gate based on frequency of operator 2 in channel 8 */
				uint bit5e = ((Slot8_2.Cnt >> FrequencyShift) >> 5) & 1;
				uint bit3e = ((Slot8_2.Cnt >> FrequencyShift) >> 3) & 1;

				uint res2 = bit3e ^ bit5e;

				/* when res2 = 0 pass the phase from calculation above (res1); */
				/* when res2 = 1 phase = 0x200 | (0xd0>>2); */
				if (res2 != 0)
					phase = 0x200 | (0xd0>>2);

				/* when phase & 0x200 is set and noise=1 then phase = 0x200|0xd0 */
				/* when phase & 0x200 is set and noise=0 then phase = 0x200|(0xd0>>2), ie no change */
				if (phase.HasBitSet(0x200))
				{
					if (noise)
						phase = 0x200|0xd0;
				}
				else
				/* when phase & 0x200 is clear and noise=1 then phase = 0xd0>>2 */
				/* when phase & 0x200 is clear and noise=0 then phase = 0xd0, ie no change */
				{
					if (noise)
						phase = 0xd0>>2;
				}

				output.Value += op_calc(phase << FrequencyShift, env, 0, Slot7_1.wavetable) * 2;
			}
		}

		/* Snare Drum (verified on real YM3812) */
		{
			uint env = volume_calc(ref Slot7_2);

			if( env < EnvelopeQuiet )
			{
				/* base frequency derived from operator 1 in channel 7 */
				uint bit8 = ((Slot7_1.Cnt >> FrequencyShift) >> 8) & 1;

				/* when bit8 = 0 phase = 0x100; */
				/* when bit8 = 1 phase = 0x200; */
				uint phase = (bit8 != 0) ? 0x200u : 0x100;

				/* Noise bit XOR'es phase by 0x100 */
				/* when noisebit = 0 pass the phase from calculation above */
				/* when noisebit = 1 phase ^= 0x100; */
				/* in other words: phase ^= (noisebit<<8); */
				if (noise)
					phase ^= 0x100;

				output.Value += op_calc(phase << FrequencyShift, env, 0, Slot7_2.wavetable) * 2;
			}
		}

		/* Tom Tom (verified on real YM3812) */
		{
			uint env = volume_calc(ref Slot8_1);

			if( env < EnvelopeQuiet )
				output.Value += op_calc(Slot8_1.Cnt, env, 0, Slot8_1.wavetable) * 2;
		}

		/* Top Cymbal (verified on real YM3812) */
		{
			uint env = volume_calc(ref Slot8_2);

			if( env < EnvelopeQuiet )
			{
				/* base frequency derived from operator 1 in channel 7 */
				uint bit7 = ((Slot7_1.Cnt >> FrequencyShift) >> 7) & 1;
				uint bit3 = ((Slot7_1.Cnt >> FrequencyShift) >> 3) & 1;
				uint bit2 = ((Slot7_1.Cnt >> FrequencyShift) >> 2) & 1;

				uint res1 = (bit2 ^ bit7) | bit3;

				/* when res1 = 0 phase = 0x000 | 0x100; */
				/* when res1 = 1 phase = 0x200 | 0x100; */
				uint phase = (res1 != 0) ? 0x300u : 0x100;

				/* enable gate based on frequency of operator 2 in channel 8 */
				uint bit5e = ((Slot8_2.Cnt >> FrequencyShift) >> 5) & 1;
				uint bit3e = ((Slot8_2.Cnt >> FrequencyShift) >> 3) & 1;

				uint res2 = bit3e ^ bit5e;

				/* when res2 = 0 pass the phase from calculation above (res1); */
				/* when res2 = 1 phase = 0x200 | 0x100; */
				if (res2 != 0)
					phase = 0x300;

				output.Value += op_calc(phase<<FrequencyShift, env, 0, Slot8_2.wavetable) * 2;
			}
		}
	}

	void InitializeGlobalTables()
	{
		/* frequency base */
		freqbase  = (rate != 0) ? ((double)clock / 72.0) / rate  : 0;
#if false
		rate = unchecked((uint)((double)clock / 72.0));
		freqbase  = 1.0;
#endif

		/*logerror("freqbase=%f\n", freqbase);*/

		/* Timer base time */
		TimerBase = 72.0 / (double)clock;

		/* make fnumber -> increment counter table */
		for (int i = 0; i < 1024; i++)
		{
			/* opn phase increment counter = 20bit */
			/* -10 because chip works with 10.10 fixed point, while we use 16.16 */
			fn_tab[i] = (uint)( (double)i * 64 * freqbase * (1<<(FrequencyShift-10)) );
#if false
			Console.Error.WriteLine("FMOPL.C: fn_tab[{0:###0}] = {1:x8} (dec={2:#######0})",
							i, fn_tab[i]>>6, fn_tab[i]>>6 );
#endif
		}

#if false
		for (int i=0 ; i < 16 ; i++)
		{
			Console.Error.WriteLine("FMOPL.C: sl_tab[{0}] = {1:x8}",
				i, YM3812Tables.sl_tab[i] );
		}

		for (int i=0 ; i < 8 ; i++)
		{
			int j;
			Console.Error.Write("FMOPL.C: ksl_tab[oct={0:##}] =",i);
			for (j=0; j<16; j++)
			{
				Console.Error.Write(YM3812Tables.ksl_tab[i*16+j].ToString("x8") );
			}
			Console.Error.WriteLine();
		}
#endif


		/* Amplitude modulation: 27 output levels (triangle waveform);
		1 level takes one of: 192, 256 or 448 samples */
		/* One entry from LFO_AM_TABLE lasts for 64 samples */
		lfo_am_inc = (uint)((1.0 / 64.0 ) * (1<<LFOShift) * freqbase);

		/* Vibrato: 8 output levels (triangle waveform); 1 level takes 1024 samples */
		lfo_pm_inc = (uint)((1.0 / 1024.0) * (1<<LFOShift) * freqbase);

		/*Console.Error.WriteLine("lfo_am_inc = {0:x8} ; lfo_pm_inc = {1:x8}", lfo_am_inc, lfo_pm_inc);*/

		/* Noise generator: a step takes 1 sample */
		noise_f = (uint)((1.0 / 1.0) * (1<<FrequencyShift) * freqbase);

		eg_timer_add  = (uint)((1<<EnvelopeGeneratorShift)  * freqbase);
		eg_timer_overflow = ( 1 ) * (1<<EnvelopeGeneratorShift);
		/*Console.Error.WriteLine("OPLinit eg_timer_add={0:x8} eg_timer_overflow={1:x8}",
			eg_timer_add, eg_timer_overflow);*/
	}

	static void FM_KEYON(ref OPLSlot SLOT, uint key_set)
	{
		if (SLOT.key == 0)
		{
			/* restart Phase Generator */
			SLOT.Cnt = 0;
			/* phase -> Attack */
			SLOT.state = EnvelopeGeneratorPhase.Attack;
		}
		SLOT.key |= key_set;
	}

	static void FM_KEYOFF(ref OPLSlot SLOT, uint key_clr)
	{
		if (SLOT.key != 0)
		{
			SLOT.key &= key_clr;

			if (SLOT.key == 0)
			{
				/* phase -> Release */
				if (SLOT.state > EnvelopeGeneratorPhase.Release)
					SLOT.state = EnvelopeGeneratorPhase.Release;
			}
		}
	}

	/* update phase increment counter of operator (also update the EG rates if necessary) */
	static void CALC_FCSLOT(ref OPLChannel CH, ref OPLSlot SLOT)
	{
		/* (frequency) phase increment counter */
		SLOT.Incr = CH.fc * SLOT.mul;

		byte ksr = unchecked((byte)(CH.kcode >> SLOT.KSR));

		if( SLOT.ksr != ksr )
		{
			SLOT.ksr = ksr;

			/* calculate envelope generator rates */
			if ((SLOT.ar + SLOT.ksr) < 16+62)
			{
				SLOT.eg_sh_ar  = YM3812Tables.eg_rate_shift [SLOT.ar + SLOT.ksr ];
				SLOT.eg_sel_ar = YM3812Tables.eg_rate_select[SLOT.ar + SLOT.ksr ];
			}
			else
			{
				SLOT.eg_sh_ar  = 0;
				SLOT.eg_sel_ar = 13 * RateSteps;
			}
			SLOT.eg_sh_dr  = YM3812Tables.eg_rate_shift [SLOT.dr + SLOT.ksr ];
			SLOT.eg_sel_dr = YM3812Tables.eg_rate_select[SLOT.dr + SLOT.ksr ];
			SLOT.eg_sh_rr  = YM3812Tables.eg_rate_shift [SLOT.rr + SLOT.ksr ];
			SLOT.eg_sel_rr = YM3812Tables.eg_rate_select[SLOT.rr + SLOT.ksr ];
		}
	}

	/* set multi,am,vib,EG-TYP,KSR,mul */
	void set_mul(int slot, int v)
	{
		ref var CH   = ref P_CH[slot / 2];
		ref var SLOT = ref CH.Slot[slot & 1];

		SLOT.mul     = YM3812Tables.mul_tab[v&0x0f];
		SLOT.KSR     = v.HasBitSet(0x10) ? (byte)0 : (byte)2;
		SLOT.eg_type = v.HasBitSet(0x20);
		SLOT.vib     = v.HasBitSet(0x40);
		SLOT.AMmask  = v.HasBitSet(0x80) ? unchecked((byte)~0) : (byte)0;

		CALC_FCSLOT(ref CH, ref SLOT);
	}

	/* set ksl & tl */
	void set_ksl_tl(int slot, int v)
	{
		ref var CH   = ref P_CH[slot / 2];
		ref var SLOT = ref CH.Slot[slot & 1];

		SLOT.ksl = YM3812Tables.ksl_shift[v >> 6];
		SLOT.TL  = ((uint)v & 0x3f) << (EnvelopeBits-1-7); /* 7 bits TL (bit 6 = always 0) */

		SLOT.TLL = unchecked((int)(SLOT.TL + (CH.ksl_base>>SLOT.ksl)));
	}

	/* set attack rate & decay rate  */
	void set_ar_dr(int slot, int v)
	{
		ref var CH   = ref P_CH[slot / 2];
		ref var SLOT = ref CH.Slot[slot & 1];

		SLOT.ar = ((v >> 4) != 0) ? 16 + unchecked((uint)((v >> 4) << 2)) : 0u;

		if ((SLOT.ar + SLOT.ksr) < 16+62)
		{
			SLOT.eg_sh_ar  = YM3812Tables.eg_rate_shift [SLOT.ar + SLOT.ksr ];
			SLOT.eg_sel_ar = YM3812Tables.eg_rate_select[SLOT.ar + SLOT.ksr ];
		}
		else
		{
			SLOT.eg_sh_ar  = 0;
			SLOT.eg_sel_ar = 13*RateSteps;
		}

		SLOT.dr    = v.HasAnyBitSet(0x0f) ? 16 + unchecked((uint)((v & 0x0f) << 2)) : 0u;
		SLOT.eg_sh_dr  = YM3812Tables.eg_rate_shift [SLOT.dr + SLOT.ksr ];
		SLOT.eg_sel_dr = YM3812Tables.eg_rate_select[SLOT.dr + SLOT.ksr ];
	}

	/* set sustain level & release rate */
	void set_sl_rr(int slot, int v)
	{
		ref var CH   = ref P_CH[slot / 2];
		ref var SLOT = ref CH.Slot[slot & 1];

		SLOT.sl  = YM3812Tables.sl_tab[ v>>4 ];

		SLOT.rr  = v.HasAnyBitSet(0x0f) ? 16 + unchecked((uint)((v & 0x0f) << 2)) : 0u;
		SLOT.eg_sh_rr  = YM3812Tables.eg_rate_shift [SLOT.rr + SLOT.ksr ];
		SLOT.eg_sel_rr = YM3812Tables.eg_rate_select[SLOT.rr + SLOT.ksr ];
	}

	/* write a value v to register r on OPL chip */
	void WriteRegister(int r, int v)
	{
		/* adjust bus to 8 bits */
		r &= 0xff;
		v &= 0xff;

		switch(r & 0xe0)
		{
			case 0x00:  /* 00-1f:control */
				switch (r & 0x1f)
				{
					case 0x01:  /* waveform select enable */
						if (type.HasAllFlags(OPLType.WaveformSelect))
						{
							wavesel = v.HasBitSet(0x20);
							/* do not change the waveform previously selected */
						}
						break;
					case 0x02:  /* Timer 1 */
						T[0] = (256 - v) * 4;
						break;
					case 0x03:  /* Timer 2 */
						T[1] = (256 - v) * 16;
						break;
					case 0x04:  /* IRQ clear / mask and Timer enable */
						if (v.HasBitSet(0x80))
						{
							/* IRQ flag clear */
							/* don't reset BFRDY flag or we will have to call deltat module to set the flag */
							ResetStatus(~StatusFlags.IRQEnabled & ~StatusFlags.BufferReady);
						}
						else
						{
							StatusFlags vf = (StatusFlags)v;

							/* set IRQ mask ,timer enable*/
							bool st1 = vf.HasAllFlags(StatusFlags.ST1);
							bool st2 = vf.HasAllFlags(StatusFlags.ST2);

							/* IRQRST,T1MSK,t2MSK,EOSMSK,BRMSK,x,ST2,ST1 */
							ResetStatus(vf & ~(StatusFlags.IRQEnabled | StatusFlags.BufferReady | StatusFlags.X | StatusFlags.ST2 | StatusFlags.ST1));
							SetStatusMask(~vf & ~(StatusFlags.IRQEnabled | StatusFlags.X | StatusFlags.ST2 | StatusFlags.ST1));

							/* timer 2 */
							if(st[1] != st2)
							{
								double period = st2 ? (TimerBase * T[1]) : 0.0;
								st[1] = st2;
								OnTimer(1, period);
							}
							/* timer 1 */
							if(st[0] != st1)
							{
								double period = st1 ? (TimerBase * T[0]) : 0.0;
								st[0] = st1;
								OnTimer(0, period);
							}
						}
						break;
					case 0x08:  /* MODE,DELTA-T control 2 : CSM,NOTESEL,x,x,smpl,da/ad,64k,rom */
						mode = (StatusFlags)v;
						break;
					default:
						/*logerror("FMOPL.C: write to unknown register: %02x\n",r);*/
						break;
				}
				break;
			case 0x20:  /* am ON, vib ON, ksr, eg_type, mul */
			{
				int slot = YM3812Tables.slot_array[r&0x1f];
				if(slot < 0) return;
				set_mul(slot, v);
				break;
			}
			case 0x40:
			{
				int slot = YM3812Tables.slot_array[r&0x1f];
				if(slot < 0) return;
				set_ksl_tl(slot, v);
				break;
			}
			case 0x60:
			{
				int slot = YM3812Tables.slot_array[r&0x1f];
				if(slot < 0) return;
				set_ar_dr(slot, v);
				break;
			}
			case 0x80:
			{
				int slot = YM3812Tables.slot_array[r&0x1f];
				if(slot < 0) return;
				set_sl_rr(slot, v);
				break;
			}
			case 0xa0:
				if (r == 0xbd)          /* am depth, vibrato depth, r,bd,sd,tom,tc,hh */
				{
					lfo_am_depth = v.HasBitSet(0x80);
					lfo_pm_depth_range = v.HasBitSet(0x40) ? (byte)8 : (byte)0;

					rhythm  = unchecked((byte)(v & 0x3f));

					if(rhythm.HasBitSet(0x20))
					{
						/* BD key on/off */
						if(v.HasBitSet(0x10))
						{
							FM_KEYON (ref P_CH[6].Slot[Slot1], 2);
							FM_KEYON (ref P_CH[6].Slot[Slot2], 2);
						}
						else
						{
							FM_KEYOFF(ref P_CH[6].Slot[Slot1],~2u);
							FM_KEYOFF(ref P_CH[6].Slot[Slot2],~2u);
						}
						/* HH key on/off */
						if(v.HasBitSet(0x01)) FM_KEYON (ref P_CH[7].Slot[Slot1], 2);
						else                  FM_KEYOFF(ref P_CH[7].Slot[Slot1],~2u);
						/* SD key on/off */
						if(v.HasBitSet(0x08)) FM_KEYON (ref P_CH[7].Slot[Slot2], 2);
						else                  FM_KEYOFF(ref P_CH[7].Slot[Slot2],~2u);
						/* TOM key on/off */
						if(v.HasBitSet(0x04)) FM_KEYON (ref P_CH[8].Slot[Slot1], 2);
						else                  FM_KEYOFF(ref P_CH[8].Slot[Slot1],~2u);
						/* TOP-CY key on/off */
						if(v.HasBitSet(0x02)) FM_KEYON (ref P_CH[8].Slot[Slot2], 2);
						else                  FM_KEYOFF(ref P_CH[8].Slot[Slot2],~2u);
					}
					else
					{
						/* BD key off */
						FM_KEYOFF(ref P_CH[6].Slot[Slot1],~2u);
						FM_KEYOFF(ref P_CH[6].Slot[Slot2],~2u);
						/* HH key off */
						FM_KEYOFF(ref P_CH[7].Slot[Slot1],~2u);
						/* SD key off */
						FM_KEYOFF(ref P_CH[7].Slot[Slot2],~2u);
						/* TOM key off */
						FM_KEYOFF(ref P_CH[8].Slot[Slot1],~2u);
						/* TOP-CY off */
						FM_KEYOFF(ref P_CH[8].Slot[Slot2],~2u);
					}
					return;
				}
				/* keyon,block,fnum */
				if( (r&0x0f) > 8) return;
				ref var CH = ref P_CH[r&0x0f];
				uint block_fnum;
				if(!r.HasBitSet(0x10))
				{   /* a0-a8 */
					block_fnum  = (CH.block_fnum&0x1f00) | unchecked((uint)v);
				}
				else
				{   /* b0-b8 */
					block_fnum = unchecked((((uint)v & 0x1f) << 8) | (CH.block_fnum & 0xffu));

					if(v.HasBitSet(0x20))
					{
						FM_KEYON (ref CH.Slot[Slot1], 1);
						FM_KEYON (ref CH.Slot[Slot2], 1);
					}
					else
					{
						FM_KEYOFF(ref CH.Slot[Slot1],~1u);
						FM_KEYOFF(ref CH.Slot[Slot2],~1u);
					}
				}
				/* update */
				if (CH.block_fnum != block_fnum)
				{
					byte block  = unchecked((byte)(block_fnum >> 10));

					CH.block_fnum = block_fnum;

					CH.ksl_base = YM3812Tables.ksl_tab[block_fnum>>6];
					CH.fc       = fn_tab[block_fnum&0x03ff] >> (7-block);

					/* BLK 2,1,0 bits -> bits 3,2,1 of kcode */
					CH.kcode    = unchecked((byte)((CH.block_fnum & 0x1c00) >> 9));

					/* the info below is actually opposite to what is stated in the Manuals
					(verifed on real YM3812) */
					/* if notesel == 0 -> lsb of kcode is bit 10 (MSB) of fnum  */
					/* if notesel == 1 -> lsb of kcode is bit 9 (MSB-1) of fnum */
					if (mode.HasAllFlags(StatusFlags.TimerA))
						CH.kcode |= unchecked((byte)((CH.block_fnum & 0x100) >> 8)); /* notesel == 1 */
					else
						CH.kcode |= unchecked((byte)((CH.block_fnum & 0x200) >> 9)); /* notesel == 0 */

					/* refresh Total Level in both SLOTs of this channel */
					CH.Slot[Slot1].TLL = unchecked((int)(CH.Slot[Slot1].TL + (CH.ksl_base >> CH.Slot[Slot1].ksl)));
					CH.Slot[Slot2].TLL = unchecked((int)(CH.Slot[Slot2].TL + (CH.ksl_base >> CH.Slot[Slot2].ksl)));

					/* refresh frequency counter in both SLOTs of this channel */
					CALC_FCSLOT(ref CH, ref CH.Slot[Slot1]);
					CALC_FCSLOT(ref CH, ref CH.Slot[Slot2]);
				}
				break;
			case 0xc0:
				/* FB,C */
				if( (r&0x0f) > 8) return;
				CH = ref P_CH[r&0x0f];
				CH.Slot[Slot1].FB  = (v >> 1).HasAnyBitSet(7) ? unchecked((byte)(((v>>1)&7) + 7)) : (byte)0;
				CH.Slot[Slot1].CON = unchecked((byte)(v & 1));
				CH.Slot[Slot1].connect1 = (CH.Slot[Slot1].CON != 0) ? output : phase_modulation;
				break;
			case 0xe0: /* waveform select */
				/* simply ignore write to the waveform select register
				if selecting not enabled in test register */
				if(wavesel)
				{
					var slot = YM3812Tables.slot_array[r&0x1f];
					if(slot < 0) return;
					CH = ref P_CH[slot/2];

					CH.Slot[slot & 1].wavetable = unchecked((ushort)((v & 0x03) * YM3812Tables.SineLength));
				}
				break;
		}
	}

	/* lock/unlock for common table */
	static int LockTable()
	{
		YM3812Tables.num_lock++;
		if(YM3812Tables.num_lock>1) return 0;

		/* first time */

		/* allocate total level table (128kb space) */
		if( !YM3812Tables.InitializeTables() )
		{
			YM3812Tables.num_lock--;
			return -1;
		}

		return 0;
	}

	static void UnlockTable()
	{
		if(YM3812Tables.num_lock > 0) YM3812Tables.num_lock--;
		if(YM3812Tables.num_lock > 0) return;

		/* last time */
		//OPLCloseTable();
	}

	public override void ResetChip()
	{
		eg_timer = 0;
		eg_cnt   = 0;

		noise_rng = 1; /* noise shift register */
		mode   = 0;    /* normal mode */
		ResetStatus(~StatusFlags.IRQEnabled);

		/* reset with register write */
		WriteRegister(0x01, 0); /* wavesel disable */
		WriteRegister(0x02, 0); /* Timer1 */
		WriteRegister(0x03, 0); /* Timer2 */
		WriteRegister(0x04, 0); /* IRQ mask clear */

		for (int i = 0xff; i >= 0x20; i--) WriteRegister(i,0);

		/* reset operator parameters */
		for (int c = 0 ; c < 9 ; c++)
		{
			ref var CH = ref P_CH[c];

			for (int s = 0; s < 2; s++)
			{
				/* wave table */
				CH.Slot[s].wavetable = 0;
				CH.Slot[s].state     = EnvelopeGeneratorPhase.Off;
				CH.Slot[s].volume    = MaxAttenuationIndex;
			}
		}
	}

#if false // not used anywhere
	void PostLoad()
	{
		for(int ch=0; ch < 9; ch++)
		{
			ref var CH = ref P_CH[ch];

			/* Look up key scale level */
			uint block_fnum = CH.block_fnum;

			CH.ksl_base = YM3812Tables.ksl_tab[block_fnum >> 6];
			CH.fc       = fn_tab[block_fnum & 0x03ff] >> unchecked((int)(7 - (block_fnum >> 10)));

			for(int slot = 0; slot < 2; slot++)
			{
				ref var SLOT = ref CH.Slot[slot];

				/* Calculate key scale rate */
				SLOT.ksr = unchecked((byte)(CH.kcode >> SLOT.KSR));

				/* Calculate attack, decay and release rates */
				if ((SLOT.ar + SLOT.ksr) < 16+62)
				{
					SLOT.eg_sh_ar  = YM3812Tables.eg_rate_shift [SLOT.ar + SLOT.ksr ];
					SLOT.eg_sel_ar = YM3812Tables.eg_rate_select[SLOT.ar + SLOT.ksr ];
				}
				else
				{
					SLOT.eg_sh_ar  = 0;
					SLOT.eg_sel_ar = 13 * RateSteps;
				}
				SLOT.eg_sh_dr  = YM3812Tables.eg_rate_shift [SLOT.dr + SLOT.ksr ];
				SLOT.eg_sel_dr = YM3812Tables.eg_rate_select[SLOT.dr + SLOT.ksr ];
				SLOT.eg_sh_rr  = YM3812Tables.eg_rate_shift [SLOT.rr + SLOT.ksr ];
				SLOT.eg_sel_rr = YM3812Tables.eg_rate_select[SLOT.rr + SLOT.ksr ];

				/* Calculate phase increment */
				SLOT.Incr = CH.fc * SLOT.mul;

				/* Total level */
				SLOT.TLL = unchecked((int)(SLOT.TL + (CH.ksl_base >> SLOT.ksl)));

				/* Connect output */
				SLOT.connect1 = (SLOT.CON != 0) ? output : phase_modulation;
			}
		}
	}
#endif

	/* Create one of virtual YM3812/YM3526/Y8950 */
	/* 'clock' is chip clock in Hz  */
	/* 'rate'  is sampling rate  */
	public override void Initialize(uint clock, uint rate)
	{
		if (LockTable() == -1) throw new Exception("LockTable failed");

		this.type  = YM3812;
		this.clock = clock;
		this.rate  = rate;

		/* init global tables */
		InitializeGlobalTables();

		ResetChip();
	}

	/* emulator shutdown */
	public override void ShutDown()
	{
		/* Destroy one of virtual YM3812 */
		UnlockTable();
	}

	public override bool Write(int a, int v)
	{
		if (!a.HasBitSet(1))
		{   /* address port */
			address = unchecked((byte)(v & 0xff));
		}
		else
		{   /* data port */
			OnUpdate(0);
			WriteRegister(address, v);
		}

		return status.HasAllFlags(StatusFlags.IRQEnabled);
	}

	public override byte Read(int a)
	{
		if (!a.HasBitSet(1))
		{
			/* status port */

			/* OPL and OPL2 */
			return unchecked((byte)(status & (statusmask | StatusFlags.IRQEnabled)));
		}

		return 0xff;
	}

	public override bool TimerOver(int c)
	{
		if (c != 0)
		{
			/* Timer B */
			SetStatus(StatusFlags.TimerB);
		}
		else
		{
			/* Timer A */
			SetStatus(StatusFlags.TimerA);

			/* CSM mode key,TL controll */
			if (mode.HasAllFlags(StatusFlags.IRQEnabled))
			{
				/* CSM mode total level latch and auto key on */
				OnUpdate(0);

				for (int ch=0; ch<9; ch++)
					P_CH[ch].KeyControll();
			}
		}

		/* reload timer */
		OnTimer(c, TimerBase * T[c]);

		return status.HasAllFlags(StatusFlags.IRQEnabled);
	}

	/* like update_one, but does it for each channel independently
	 * XXX: vuMax should be [static 9] but I don't know how many compilers support it */
	public override void UpdateMulti(Memory<int>?[] buffers, uint[] vuMax)
	{
		bool rhythm_part = rhythm.HasBitSet(0x20);

		for (int i = 0; i < buffers.Length; i++)
		{
			advance_lfo();

			for (int j = 0; j < 6; j++)
			{
				output.Value = 0;

				OPL_CALC_CH(ref P_CH[j]);

				uint ab = unchecked((uint)Math.Abs(output.Value));

				vuMax[j] = Math.Max(vuMax[j], ab);

				var maybeBuffer = buffers[j];

				if (maybeBuffer.HasValue)
				{
					var buffer = maybeBuffer.Value.Span;

					var sample = output.Value * Volume;

					buffer[i*2+0] += sample;
					buffer[i*2+1] += sample;
				}
			}

			if (!rhythm_part)
			{
				for (int j = 6; j < 9; j++)
				{
					output.Value = 0;
					OPL_CALC_CH(ref P_CH[j]);

					uint ab = unchecked((uint)Math.Abs(output.Value));

					vuMax[j] = Math.Max(vuMax[j], ab);

					var maybeBuffer = buffers[j];

					if (maybeBuffer.HasValue)
					{
						var buffer = maybeBuffer.Value.Span;

						var sample = output.Value * Volume;

						buffer[i*2+0] += sample;
						buffer[i*2+1] += sample;
					}
				}
			}
			else
			{
				output.Value = 0;
				OPL_CALC_RH(P_CH, noise_rng.HasBitSet(1));

				uint ab = unchecked((uint)Math.Abs(output.Value));

				vuMax[0] = Math.Max(vuMax[0], ab);

				var maybeBuffer = buffers[0];

				if (maybeBuffer.HasValue)
				{
					var buffer = maybeBuffer.Value.Span;

					var sample = output.Value * Volume;

					buffer[i*2+0] += sample;
					buffer[i*2+1] += sample;
				}
			}

			advance();
		}
	}
}
