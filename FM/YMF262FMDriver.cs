using System;
using System.Runtime.CompilerServices;

namespace ChasmTracker.FM;

using ChasmTracker.Utility;

// license:GPL-2.0+
// copyright-holders:Jarek Burczynski
/*
**
** File: ymf262.c - software implementation of YMF262
**                  FM sound generator type OPL3
**
** Copyright Jarek Burczynski
**
** Version 0.2
**

Revision History:

11-08-2025 Jonathan Gilbert:
 - translate to C#

03-03-2003: initial release
 - thanks to Olivier Galibert and Chris Hardy for YMF262 and YAC512 chips
 - thanks to Stiletto for the datasheets

	 Features as listed in 4MF262A6 data sheet:
		1. Registers are compatible with YM3812 (OPL2) FM sound source.
		2. Up to six sounds can be used as four-operator melody sounds for variety.
		3. 18 simultaneous melody sounds, or 15 melody sounds with 5 rhythm sounds (with two operators).
		4. 6 four-operator melody sounds and 6 two-operator melody sounds, or 6 four-operator melody
			 sounds, 3 two-operator melody sounds and 5 rhythm sounds (with four operators).
		5. 8 selectable waveforms.
		6. 4-channel sound output.
		7. YMF262 compabile DAC (YAC512) is available.
		8. LFO for vibrato and tremolo effedts.
		9. 2 programable timers.
	 10. Shorter register access time compared with YM3812.
	 11. 5V single supply silicon gate CMOS process.
	 12. 24 Pin SOP Package (YMF262-M), 48 Pin SQFP Package (YMF262-S).


differences between OPL2 and OPL3 not documented in Yamaha datahasheets:
- sinus table is a little different: the negative part is off by one...

- in order to enable selection of four different waveforms on OPL2
	one must set bit 5 in register 0x01(test).
	on OPL3 this bit is ignored and 4-waveform select works *always*.
	(Don't confuse this with OPL3's 8-waveform select.)

- Envelope Generator: all 15 x rates take zero time on OPL3
	(on OPL2 15 0 and 15 1 rates take some time while 15 2 and 15 3 rates
	take zero time)

- channel calculations: output of operator 1 is in perfect sync with
	output of operator 2 on OPL3; on OPL and OPL2 output of operator 1
	is always delayed by one sample compared to output of operator 2


differences between OPL2 and OPL3 shown in datasheets:
- YMF262 does not support CSM mode


*/

/* OPL3 */

public class YMF262FMDriver : FMDriver
{
	public override uint RateDivisor => 288u;

	/* output final shift */
	const int MaxOut = short.MaxValue;
	const int MinOut = short.MinValue;

	const int FrequencyShift         = 16;  /* 16.16 fixed point (frequency calculations) */
	const int EnvelopeGeneratorShift = 16;  /* 16.16 fixed point (EG timing)              */
	const int LFOShift               = 24;  /*  8.24 fixed point (LFO calculations)       */

	const int FrequencyMask = (1 << FrequencyShift) - 1;

	/* envelope output entries */
	const int EnvelopeBits      = 10;
	const int EnvelopeLength    = 1 << EnvelopeBits;
	internal const double EnvelopeStep   = 128.0 / EnvelopeLength;

	const int MaxAttenuationIndex = (1 << (EnvelopeBits - 1)) - 1; /* 511 */
	const int MinAttenuationIndex = 0;

	/* register number to channel number , slot offset */
	const int Slot1 = 0;
	const int Slot2 = 1;

	/* save output as raw 16-bit sample */

	const OPLType YMF262 = 0;   /* 36 operators, 8 waveforms */

	struct OPL3Slot
	{
		public uint  ar;         /* attack rate: AR<<2           */
		public uint  dr;         /* decay rate:  DR<<2           */
		public uint  rr;         /* release rate:RR<<2           */
		public byte  KSR;        /* key scale rate               */
		public byte  ksl;        /* keyscale level               */
		public byte  ksr;        /* key scale rate: kcode>>KSR   */
		public byte  mul;        /* multiple: mul_tab[ML]        */

		/* Phase Generator */
		public uint  Cnt;        /* frequency counter            */
		public uint  Incr;       /* frequency counter step       */
		public byte  FB;         /* feedback shift value         */
		public Shared<int> connect;   /* slot output pointer          */
		public OPLSlotOutArray op1_out; /* slot1 output for feedback    */
		public byte  CON;        /* connection (algorithm) type  */

		/* Envelope Generator */
		public bool  eg_type;    /* percussive/non-percussive mode */
		public EnvelopeGeneratorPhase state;      /* phase type                   */
		public uint  TL;         /* total level: TL << 2         */
		public int   TLL;        /* adjusted now TL              */
		public int   volume;     /* envelope counter             */
		public uint  sl;         /* sustain level: sl_tab[SL]    */

		public uint  eg_m_ar;    /* (attack state)               */
		public byte  eg_sh_ar;   /* (attack state)               */
		public byte  eg_sel_ar;  /* (attack state)               */
		public uint  eg_m_dr;    /* (decay state)                */
		public byte  eg_sh_dr;   /* (decay state)                */
		public byte  eg_sel_dr;  /* (decay state)                */
		public uint  eg_m_rr;    /* (release state)              */
		public byte  eg_sh_rr;   /* (release state)              */
		public byte  eg_sel_rr;  /* (release state)              */

		public uint  key;        /* 0 = KEY OFF, >0 = KEY ON     */

		/* LFO */
		public uint  AMmask;     /* LFO Amplitude Modulation enable mask */
		public bool  vib;        /* LFO Phase Modulation enable flag (active high)*/

		/* waveform select */
		public byte  waveform_number;
		public uint  wavetable;

		[InlineArray(128 - 112)] //speedup: pump up the struct size to power of 2
		struct Padding { byte _element0; }
		Padding _reserved;
	}

	[InlineArray(2)]
	struct OPL3ChannelSlotArray
	{
		OPL3Slot _element0;
	}

	struct OPL3Channel
	{
		public OPL3ChannelSlotArray Slot;

		public uint  block_fnum; /* block+fnum                   */
		public uint  fc;         /* Freq. Increment base         */
		public uint  ksl_base;   /* KeyScaleLevel Base step      */
		public byte  kcode;      /* key code (for key scaling)   */

		/*
			there are 12 2-operator channels which can be combined in pairs
			to form six 4-operator channel, they are:
				0 and 3,
				1 and 4,
				2 and 5,
				9 and 12,
				10 and 13,
				11 and 14
		*/
		public bool  extended;   /* set to 1 if this channel forms up a 4op channel with another channel(only used by first of pair of channels, ie 0,1,2 and 9,10,11) */

		[InlineArray(512 - 272)] //speedup: pump up the struct size to power of 2
		struct Padding { byte _element0; }
		Padding _reserved;
	}

	/* OPL3 state */
	OPL3Channel[] P_CH = new OPL3Channel[18];               /* OPL3 chips have 18 channels  */

	uint[]  pan = new uint[18*4];              /* channels output masks (0xffffffff = enable); 4 masks per one channel */
	uint[]  pan_ctrl_value = new uint[18];     /* output control values 1 per one channel (1 value contains 4 masks) */

	Shared<int>[] chanout =
		new Shared<int>[18]
		{
			new Shared<int>(), new Shared<int>(), new Shared<int>(),
			new Shared<int>(), new Shared<int>(), new Shared<int>(),
			new Shared<int>(), new Shared<int>(), new Shared<int>(),

			new Shared<int>(), new Shared<int>(), new Shared<int>(),
			new Shared<int>(), new Shared<int>(), new Shared<int>(),
			new Shared<int>(), new Shared<int>(), new Shared<int>(),
		};

	Shared<int> phase_modulation = new Shared<int>();   /* phase modulation input (SLOT 2) */
	Shared<int> phase_modulation2 = new Shared<int>();  /* phase modulation input (SLOT 3 in 4 operator channels) */

	internal const int RateSteps = 8;

	uint  eg_cnt;                 /* global envelope generator counter    */
	uint  eg_timer;               /* global envelope generator counter works at frequency = chipclock/288 (288=8*36) */
	uint  eg_timer_add;           /* step of eg_timer                     */
	uint  eg_timer_overflow;      /* envelope generator timer overlfows every 1 sample (on real chip) */

	uint[]  fn_tab = new uint[1024];           /* fnumber->increment counter   */

	/* LFO */
	uint  LFO_AM;
	int   LFO_PM;

	bool  lfo_am_depth;
	byte  lfo_pm_depth_range;
	uint  lfo_am_cnt;
	uint  lfo_am_inc;
	uint  lfo_pm_cnt;
	uint  lfo_pm_inc;

	uint  noise_rng;              /* 23 bit noise shift register  */
	uint  noise_p;                /* current noise 'phase'        */
	uint  noise_f;                /* current noise period         */

	bool   OPL3_mode;              /* OPL3 extension enable flag   */

	byte   rhythm;                 /* Rhythm mode                  */

	int[]     T = new int[2];                   /* timer counters               */
	bool[]    st = new bool[2];                  /* timer enable                 */

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

	int  address;                /* address register             */
	StatusFlags status;                 /* status flag                  */
	StatusFlags statusmask;             /* status mask                  */

	byte   nts;                    /* NTS (note select)            */

	//OPLType type;                     /* chip type                    */
	uint clock;                   /* master clock  (Hz)           */
	uint rate;                    /* sampling rate (Hz)           */
	double freqbase;                /* frequency base               */
	double TimerBase;         /* Timer base time (==sampling time)*/

	internal const int EnvelopeQuiet = YMF262Tables.TLTableLength >> 4;

	/* work table */
	ref OPL3Slot Slot7_1 => ref P_CH[7].Slot[Slot1];
	ref OPL3Slot Slot7_2 => ref P_CH[7].Slot[Slot2];
	ref OPL3Slot Slot8_1 => ref P_CH[8].Slot[Slot1];
	ref OPL3Slot Slot8_2 => ref P_CH[8].Slot[Slot2];

	/* status set and IRQ handling */
	void SetStatus(StatusFlags flag)
	{
		/* set status flag masking out disabled IRQs */
		status |= (flag & statusmask);
		if(!status.HasAllFlags(StatusFlags.IRQEnabled))
		{
			if (status.HasAnyFlag(~StatusFlags.IRQEnabled))
			{   /* IRQ on */
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
		status &= ~flag;
		if	(status.HasAllFlags(StatusFlags.IRQEnabled))
		{
			if (!status.HasAnyFlag(~StatusFlags.IRQEnabled))
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
		byte tmp;

		/* LFO */
		lfo_am_cnt += lfo_am_inc;
		if (lfo_am_cnt >= ((uint)YMF262Tables.lfo_am_table.Length << LFOShift) ) /* lfo_am_table is 210 elements long */
			lfo_am_cnt -= ((uint)YMF262Tables.lfo_am_table.Length << LFOShift);

		tmp = YMF262Tables.lfo_am_table[ lfo_am_cnt >> LFOShift ];

		if (lfo_am_depth)
			LFO_AM = tmp;
		else
			LFO_AM = unchecked((uint)tmp) >> 2;

		lfo_pm_cnt += lfo_pm_inc;
		LFO_PM = unchecked((int)(((lfo_pm_cnt >> LFOShift) & 7) | lfo_pm_depth_range));
	}

	/* advance to next sample */
	void advance()
	{
		eg_timer += eg_timer_add;

		while (eg_timer >= eg_timer_overflow)
		{
			eg_timer -= eg_timer_overflow;

			eg_cnt++;

			for (int i=0; i<9*2*2; i++)
			{
				ref var CH  = ref P_CH[i/2];
				ref var op  = ref CH.Slot[i & 1];

				/* Envelope Generator */
				switch (op.state)
				{
					case EnvelopeGeneratorPhase.Attack:    /* attack phase */
						// if ( !(eg_cnt & ((1 << op.eg_sh_ar)-1) ) )
						if (!eg_cnt.HasAnyBitSet(op.eg_m_ar))
						{
							op.volume += (~op.volume *
														(YMF262Tables.eg_inc[op.eg_sel_ar + ((eg_cnt>>op.eg_sh_ar)&7)])
														) >>3;

							if (op.volume <= MinAttenuationIndex)
							{
								op.volume = MinAttenuationIndex;
								op.state = EnvelopeGeneratorPhase.Decay;
							}

						}
						break;

					case EnvelopeGeneratorPhase.Decay:    /* decay phase */
		//              if ( !(eg_cnt & ((1<<op.eg_sh_dr)-1) ) )
						if (!eg_cnt.HasAnyBitSet(op.eg_m_dr))
						{
							op.volume += YMF262Tables.eg_inc[op.eg_sel_dr + ((eg_cnt>>op.eg_sh_dr)&7)];

							if ( op.volume >= op.sl )
								op.state = EnvelopeGeneratorPhase.Sustain;

						}
						break;

					case EnvelopeGeneratorPhase.Sustain:    /* sustain phase */

						/* this is important behaviour:
						one can change percusive/non-percussive modes on the fly and
						the chip will remain in sustain phase - verified on real YM3812 */

						if (op.eg_type)     /* non-percussive mode */
						{
							/* do nothing */
						}
						else                /* percussive mode */
						{
							/* during sustain phase chip adds Release Rate (in percussive mode) */
							// if ( !(eg_cnt & ((1<<op.eg_sh_rr)-1) ) )
							if (!eg_cnt.HasAnyBitSet(op.eg_m_rr))
							{
								op.volume += YMF262Tables.eg_inc[op.eg_sel_rr + ((eg_cnt>>op.eg_sh_rr)&7)];

								if ( op.volume >= MaxAttenuationIndex)
									op.volume = MaxAttenuationIndex;
							}
							/* else do nothing in sustain phase */
						}
						break;

					case EnvelopeGeneratorPhase.Release:    /* release phase */
						// if ( !(eg_cnt & ((1<<op.eg_sh_rr)-1) ) )
						if (!eg_cnt.HasAnyBitSet(op.eg_m_rr))
						{
							op.volume += YMF262Tables.eg_inc[op.eg_sel_rr + ((eg_cnt>>op.eg_sh_rr)&7)];

							if ( op.volume >= MaxAttenuationIndex)
							{
								op.volume = MaxAttenuationIndex;
								op.state = EnvelopeGeneratorPhase.Off;
							}

						}
						break;
				}
			}
		}

		for (int i=0; i<9*2*2; i++)
		{
			ref var CH  = ref P_CH[i/2];
			ref var op  = ref CH.Slot[i & 1];

			/* Phase Generator */
			if (op.vib)
			{
				byte block;
				uint block_fnum = CH.block_fnum;

				uint fnum_lfo   = (block_fnum&0x0380) >> 7;

				int lfo_fn_table_index_offset = YMF262Tables.lfo_pm_table[LFO_PM + 16*fnum_lfo ];

				if (lfo_fn_table_index_offset != 0)  /* LFO phase modulation active */
				{
					block_fnum = unchecked((uint)(block_fnum + lfo_fn_table_index_offset));
					block = unchecked((byte)((block_fnum & 0x1c00) >> 10));
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
		uint ii = noise_p >> FrequencyShift;       /* number of events (shifts of the shift register) */
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
		uint p = (env<<4) + YMF262Tables.sin_tab[wave_tab + ((((int)((phase & ~FrequencyMask) + (pm << 16))) >> FrequencyShift) & YMF262Tables.SineMask) ];

		if (p >= YMF262Tables.TLTableLength)
			return 0;
		return YMF262Tables.tl_tab[p];
	}

	int op_calc1(uint phase, uint env, int pm, uint wave_tab)
	{
		uint p = (env<<4) + YMF262Tables.sin_tab[wave_tab + ((((int)((phase & ~FrequencyMask) + pm)) >> FrequencyShift) & YMF262Tables.SineMask)];

		if (p >= YMF262Tables.TLTableLength)
			return 0;
		return YMF262Tables.tl_tab[p];
	}

	uint volume_calc(ref OPL3Slot OP) => unchecked((uint)OP.TLL) + ((uint)OP.volume) + (LFO_AM & OP.AMmask);

	/* calculate output of a standard 2 operator channel
	(or 1st part of a 4-op channel) */
	void chan_calc(ref OPL3Channel CH)
	{
		phase_modulation.Value  = 0;
		phase_modulation2.Value = 0;

		/* SLOT 1 */
		{
			ref var SLOT = ref CH.Slot[Slot1];
			uint env  = volume_calc(ref SLOT);
			int @out  = SLOT.op1_out[0] + SLOT.op1_out[1];
			SLOT.op1_out[0] = SLOT.op1_out[1];
			SLOT.op1_out[1] = 0;
			if( env < EnvelopeQuiet )
			{
				if (SLOT.FB == 0)
					@out = 0;
				SLOT.op1_out[1] = op_calc1(SLOT.Cnt, env, (@out<<SLOT.FB), SLOT.wavetable);
			}
			SLOT.connect.Value += SLOT.op1_out[1];
			//Console.Error.WriteLine("out0={0:####0} vol0={0:###0} ", SLOT.op1_out[1], env );
		}

		/* SLOT 2 */
		{
			ref var SLOT = ref CH.Slot[Slot2];
			uint env = volume_calc(ref SLOT);
			if (env < EnvelopeQuiet)
				SLOT.connect.Value += op_calc(SLOT.Cnt, env, phase_modulation, SLOT.wavetable);
			//Console.Error.WriteLine("out1={0:####0} vol1={0:###0}\n", op_calc(SLOT.Cnt, env, phase_modulation, SLOT.wavetable), env );
		}
	}

	/* calculate output of a 2nd part of 4-op channel */
	void chan_calc_ext(ref OPL3Channel CH)
	{
		phase_modulation.Value = 0;

		/* SLOT 1 */
		{
			ref var SLOT = ref CH.Slot[Slot1];
			uint env  = volume_calc(ref SLOT);
			if( env < EnvelopeQuiet )
				SLOT.connect.Value += op_calc(SLOT.Cnt, env, phase_modulation2, SLOT.wavetable );
		}

		/* SLOT 2 */
		{
			ref var SLOT = ref CH.Slot[Slot2];
			uint env = volume_calc(ref SLOT);
			if( env < EnvelopeQuiet )
				SLOT.connect.Value += op_calc(SLOT.Cnt, env, phase_modulation, SLOT.wavetable);
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

	void chan_calc_rhythm(OPL3Channel[] CH, bool noise)
	{
		/* Bass Drum (verified on real YM3812):
			- depends on the channel 6 'connect' register:
					when connect = 0 it works the same as in normal (non-rhythm) mode (op1->op2->out)
					when connect = 1 _only_ operator 2 is present on output (op2->out), operator 1 is ignored
			- output sample always is multiplied by 2
		*/

		int @out;

		phase_modulation.Value = 0;

		/* SLOT 1 */
		{
			ref var SLOT = ref CH[6].Slot[Slot1];
			uint env = volume_calc(ref SLOT);

			@out = SLOT.op1_out[0] + SLOT.op1_out[1];
			SLOT.op1_out[0] = SLOT.op1_out[1];

			if (SLOT.CON == 0)
				phase_modulation.Value = SLOT.op1_out[0];
			//else ignore output of operator 1

			SLOT.op1_out[1] = 0;
			if( env < EnvelopeQuiet )
			{
				if (SLOT.FB == 0)
					@out = 0;
				SLOT.op1_out[1] = op_calc1(SLOT.Cnt, env, (@out << SLOT.FB), SLOT.wavetable );
			}
		}

		/* SLOT 2 */
		{
			ref var SLOT = ref CH[6].Slot[Slot2];
			uint env = volume_calc(ref SLOT);
			if( env < EnvelopeQuiet )
				chanout[6].Value += op_calc(SLOT.Cnt, env, phase_modulation, SLOT.wavetable) * 2;
		}

		/* Phase generation is based on: */
		// HH  (13) channel 7->slot 1 combined with channel 8->slot 2 (same combination as TOP CYMBAL but different output phases)
		// SD  (16) channel 7->slot 1
		// TOM (14) channel 8->slot 1
		// TOP (17) channel 7->slot 1 combined with channel 8->slot 2 (same combination as HIGH HAT but different output phases)

		/* Envelope generation based on: */
		// HH  channel 7->slot1
		// SD  channel 7->slot2
		// TOM channel 8->slot1
		// TOP channel 8->slot2


		/* The following formulas can be well optimized.
			I leave them in direct form for now (in case I've missed something).
		*/

		{
			/* High Hat (verified on real YM3812) */
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
				/* when res1 = 1 phase = 0x200 | (0xd0 >> 2); */
				uint phase = (res1 != 0) ? (0x200u | (0xd0 >> 2)) : 0xd0;

				/* enable gate based on frequency of operator 2 in channel 8 */
				uint bit5e= ((Slot8_2.Cnt >> FrequencyShift) >> 5) & 1;
				uint bit3e= ((Slot8_2.Cnt >> FrequencyShift) >> 3) & 1;

				uint res2 = (bit3e ^ bit5e);

				/* when res2 = 0 pass the phase from calculation above (res1); */
				/* when res2 = 1 pha != 0se = 0x200 | (0xd0>>2); */
				if (res2 != 0)
					phase = (0x200|(0xd0>>2));


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

				chanout[7].Value += op_calc(phase<<FrequencyShift, env, 0, Slot7_1.wavetable) * 2;
			}
		}

		{
			/* Snare Drum (verified on real YM3812) */
			uint env = volume_calc(ref Slot7_2);
			if (env < EnvelopeQuiet)
			{
				/* base frequency derived from operator 1 in channel 7 */
				bool bit8 = ((Slot7_1.Cnt >> FrequencyShift) >> 8).HasBitSet(1);

				/* when bit8 = 0 phase = 0x100; */
				/* when bit8 = 1 phase = 0x200; */
				uint phase = bit8 ? 0x200u : 0x100;

				/* Noise bit XOR'es phase by 0x100 */
				/* when noisebit = 0 pass the phase from calculation above */
				/* when noisebit = 1 phase ^= 0x100; */
				/* in other words: phase ^= (noisebit<<8); */
				if (noise)
					phase ^= 0x100;

				chanout[7].Value += op_calc(phase<<FrequencyShift, env, 0, Slot7_2.wavetable) * 2;
			}
		}

		{
			/* Tom Tom (verified on real YM3812) */
			uint env = volume_calc(ref Slot8_1);
			if (env < EnvelopeQuiet)
				chanout[8].Value += op_calc(Slot8_1.Cnt, env, 0, Slot8_1.wavetable) * 2;
		}

		{
			/* Top Cymbal (verified on real YM3812) */
			uint env = volume_calc(ref Slot8_2);
			if (env < EnvelopeQuiet)
			{
				/* base frequency derived from operator 1 in channel 7 */
				uint bit7 = ((Slot7_1.Cnt>>FrequencyShift)>>7) & 1;
				uint bit3 = ((Slot7_1.Cnt>>FrequencyShift)>>3) & 1;
				uint bit2 = ((Slot7_1.Cnt>>FrequencyShift)>>2) & 1;

				uint res1 = (bit2 ^ bit7) | bit3;

				/* when res1 = 0 phase = 0x000 | 0x100; */
				/* when res1 = 1 phase = 0x200 | 0x100; */
				uint phase = (res1 != 0) ? 0x300u : 0x100;

				/* enable gate based on frequency of operator 2 in channel 8 */
				uint bit5e= ((Slot8_2.Cnt>>FrequencyShift)>>5) & 1;
				uint bit3e= ((Slot8_2.Cnt>>FrequencyShift)>>3) & 1;

				uint res2 = (bit3e ^ bit5e);
				/* when res2 = 0 pass the phase from calculation above (res1); */
				/* when res2 = 1 phase = 0x200 | 0x100; */
				if (res2 != 0)
					phase = 0x300;

				chanout[8].Value += op_calc(phase<<FrequencyShift, env, 0, Slot8_2.wavetable) * 2;
			}
		}
	}

	static void OPLCloseTable()
	{
		// lalala
	}

	void InitializeGlobalTables()
	{
		/* frequency base */
		freqbase  = (rate != 0) ? ((double)clock / (8.0*36)) / rate  : 0;
#if false
		rate = (uint)((double)clock / (8.0*36));
		freqbase  = 1.0;
#endif

		/* Console.WriteLine("YMF262: freqbase={0}\n", freqbase); */

		/* Timer base time */
		TimerBase = (8*36) / clock;

		/* make fnumber -> increment counter table */
		for (int i = 0; i < 1024; i++)
		{
			/* opn phase increment counter = 20bit */
			fn_tab[i] = (uint)( (double)i * 64 * freqbase * (1<<(FrequencyShift-10)) ); /* -10 because chip works with 10.10 fixed point, while we use 16.16 */
#if false
			Console.Error.WriteLine("YMF262.C: fn_tab[{0:###0}] = {1:x8} (dec={2:#######0})\n",
						i, fn_tab[i]>>6, fn_tab[i]>>6 );
#endif
		}

#if false
		for (int i = 0; i < 16; i++)
		{
			Console.Error.WriteLine("YMF262.C: sl_tab[{0}] = {1:x8}",
				i, YMF262Tables.sl_tab[i] );
		}

		for (int i = 0; i < 8; i++)
		{
			Console.Error.Write("YMF262.C: ksl_tab[oct={0:#0}] =",i);
			for (int j=0; j<16; j++)
				Console.Error.Write("{0:x8} ", YMF262Tables.ksl_tab[i*16+j]);
			Console.Error.WriteLine();
		}
#endif


		/* Amplitude modulation: 27 output levels (triangle waveform); 1 level takes one of: 192, 256 or 448 samples */
		/* One entry from LFO_AM_TABLE lasts for 64 samples */
		lfo_am_inc = (uint)((1.0 / 64.0 ) * (1<<LFOShift) * freqbase);

		/* Vibrato: 8 output levels (triangle waveform); 1 level takes 1024 samples */
		lfo_pm_inc = (uint)((1.0 / 1024.0) * (1<<LFOShift) * freqbase);

		/*logerror ("lfo_am_inc = %8x ; lfo_pm_inc = %8x\n", lfo_am_inc, lfo_pm_inc);*/

		/* Noise generator: a step takes 1 sample */
		noise_f = (uint)((1.0 / 1.0) * (1<<FrequencyShift) * freqbase);

		eg_timer_add  = (uint)((1<<EnvelopeGeneratorShift)  * freqbase);
		eg_timer_overflow = ( 1 ) * (1<<EnvelopeGeneratorShift);
		/*logerror("YMF262init eg_timer_add=%8x eg_timer_overflow=%8x\n", eg_timer_add, eg_timer_overflow);*/

	}

	void FM_KEYON(ref OPL3Slot SLOT, uint key_set)
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

	void FM_KEYOFF(ref OPL3Slot SLOT, uint key_clr)
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
	void CALC_FCSLOT(ref OPL3Channel CH,ref OPL3Slot SLOT)
	{
		/* (frequency) phase increment counter */
		SLOT.Incr = CH.fc * SLOT.mul;

		byte ksr = unchecked((byte)(CH.kcode >> SLOT.KSR));

		if( SLOT.ksr != ksr )
		{
			SLOT.ksr = ksr;

			/* calculate envelope generator rates */
			if ((SLOT.ar + SLOT.ksr) < 16+60)
			{
				SLOT.eg_sh_ar  = YMF262Tables.eg_rate_shift [SLOT.ar + SLOT.ksr ];
				SLOT.eg_m_ar   = (1u << SLOT.eg_sh_ar) - 1;
				SLOT.eg_sel_ar = YMF262Tables.eg_rate_select[SLOT.ar + SLOT.ksr ];
			}
			else
			{
				SLOT.eg_sh_ar  = 0;
				SLOT.eg_m_ar   = (1u << SLOT.eg_sh_ar) - 1;
				SLOT.eg_sel_ar = 13 * RateSteps;
			}
			SLOT.eg_sh_dr  = YMF262Tables.eg_rate_shift [SLOT.dr + SLOT.ksr ];
			SLOT.eg_m_dr   = (1u << SLOT.eg_sh_dr) - 1;
			SLOT.eg_sel_dr = YMF262Tables.eg_rate_select[SLOT.dr + SLOT.ksr ];
			SLOT.eg_sh_rr  = YMF262Tables.eg_rate_shift [SLOT.rr + SLOT.ksr ];
			SLOT.eg_m_rr   = (1u << SLOT.eg_sh_rr) - 1;
			SLOT.eg_sel_rr = YMF262Tables.eg_rate_select[SLOT.rr + SLOT.ksr ];
		}
	}

	/* set multi,am,vib,EG-TYP,KSR,mul */
	void set_mul(int slot, int v)
	{
		int CHslot = slot / 2;

		ref var CH = ref P_CH[CHslot];
		ref var SLOT = ref CH.Slot[slot & 1];

		SLOT.mul     = YMF262Tables.mul_tab[v&0x0f];
		SLOT.KSR     = v.HasBitSet(0x10) ? (byte)0 : (byte)2;
		SLOT.eg_type = v.HasBitSet(0x20);
		SLOT.vib     = v.HasBitSet(0x40);
		SLOT.AMmask  = v.HasBitSet(0x80) ? unchecked((byte)~0) : (byte)0;

		if (OPL3_mode)
		{
			int chan_no = slot/2;

			/* in OPL3 mode */
			//DO THIS:
			//if this is one of the slots of 1st channel forming up a 4-op channel
			//do normal operation
			//else normal 2 operator function
			//OR THIS:
			//if this is one of the slots of 2nd channel forming up a 4-op channel
			//update it using channel data of 1st channel of a pair
			//else normal 2 operator function
			switch(chan_no)
			{
				case 0: case 1: case 2:
				case 9: case 10: case 11:
					if (CH.extended)
					{
						/* normal */
						CALC_FCSLOT(ref CH,ref SLOT);
					}
					else
					{
						/* normal */
						CALC_FCSLOT(ref CH,ref SLOT);
					}
					break;
				case 3: case 4: case 5:
				case 12: case 13: case 14:
					if (P_CH[CHslot - 3].extended)
					{
						/* update this SLOT using frequency data for 1st channel of a pair */
						CALC_FCSLOT(ref P_CH[CHslot - 3],ref SLOT);
					}
					else
					{
						/* normal */
						CALC_FCSLOT(ref CH,ref SLOT);
					}
					break;
				default:
					/* normal */
					CALC_FCSLOT(ref CH,ref SLOT);
					break;
			}
		}
		else
		{
			/* in OPL2 mode */
			CALC_FCSLOT(ref CH,ref SLOT);
		}
	}

	/* set ksl & tl */
	void set_ksl_tl(int slot, int v)
	{
		ref var CH   = ref P_CH[slot/2];
		ref var SLOT = ref CH.Slot[slot & 1];

		SLOT.ksl = YMF262Tables.ksl_shift[v >> 6];
		SLOT.TL = unchecked((uint)((v & 0x3f) << (EnvelopeBits - 1 - 7))); /* 7 bits TL (bit 6 = always 0) */

		if (OPL3_mode)
		{
			int chan_no = slot/2;

			/* in OPL3 mode */
			//DO THIS:
			//if this is one of the slots of 1st channel forming up a 4-op channel
			//do normal operation
			//else normal 2 operator function
			//OR THIS:
			//if this is one of the slots of 2nd channel forming up a 4-op channel
			//update it using channel data of 1st channel of a pair
			//else normal 2 operator function
			switch(chan_no)
			{
				case 0: case 1: case 2:
				case 9: case 10: case 11:
					if (CH.extended)
					{
						/* normal */
						SLOT.TLL = unchecked((int)(SLOT.TL + (CH.ksl_base >> SLOT.ksl)));
					}
					else
					{
						/* normal */
						SLOT.TLL = unchecked((int)(SLOT.TL + (CH.ksl_base >> SLOT.ksl)));
					}
					break;
				case 3: case 4: case 5:
				case 12: case 13: case 14:
					if (P_CH[chan_no - 3].extended)
					{
						/* update this SLOT using frequency data for 1st channel of a pair */
						SLOT.TLL = unchecked((int)(SLOT.TL + (P_CH[chan_no - 3].ksl_base >> SLOT.ksl)));
					}
					else
					{
						/* normal */
						SLOT.TLL = unchecked((int)(SLOT.TL + (CH.ksl_base >> SLOT.ksl)));
					}
					break;
				default:
					/* normal */
					SLOT.TLL = unchecked((int)(SLOT.TL + (CH.ksl_base >> SLOT.ksl)));
					break;
			}
		}
		else
		{
			/* in OPL2 mode */
			SLOT.TLL = unchecked((int)(SLOT.TL + (CH.ksl_base>>SLOT.ksl)));
		}

	}

	/* set attack rate & decay rate  */
	void set_ar_dr(int slot, int v)
	{
		ref var CH   = ref P_CH[slot/2];
		ref var SLOT = ref CH.Slot[slot & 1];

		SLOT.ar = ((v>>4) != 0) ? unchecked((uint)(16 + ((v>>4) << 2))) : 0u;

		if ((SLOT.ar + SLOT.ksr) < 16+60) /* verified on real YMF262 - all 15 x rates take "zero" time */
		{
			SLOT.eg_sh_ar  = YMF262Tables.eg_rate_shift [SLOT.ar + SLOT.ksr ];
			SLOT.eg_m_ar   = unchecked((uint)((1 << SLOT.eg_sh_ar) - 1));
			SLOT.eg_sel_ar = YMF262Tables.eg_rate_select[SLOT.ar + SLOT.ksr ];
		}
		else
		{
			SLOT.eg_sh_ar  = 0;
			SLOT.eg_m_ar   = unchecked((uint)((1 << SLOT.eg_sh_ar) - 1));
			SLOT.eg_sel_ar = 13 * RateSteps;
		}

		SLOT.dr    = v.HasAnyBitSet(0x0f) ? unchecked((uint)(16 + ((v & 0x0f) << 2))) : 0u;
		SLOT.eg_sh_dr  = YMF262Tables.eg_rate_shift [SLOT.dr + SLOT.ksr ];
		SLOT.eg_m_dr   = unchecked((uint)((1 << SLOT.eg_sh_dr) - 1));
		SLOT.eg_sel_dr = YMF262Tables.eg_rate_select[SLOT.dr + SLOT.ksr ];
	}

	/* set sustain level & release rate */
	void set_sl_rr(int slot, int v)
	{
		ref var CH   = ref P_CH[slot/2];
		ref var SLOT = ref CH.Slot[slot & 1];

		SLOT.sl  = YMF262Tables.sl_tab[ v>>4 ];

		SLOT.rr  = v.HasAnyBitSet(0x0f) ? unchecked((uint)(16 + ((v & 0x0f) << 2))) : 0u;
		SLOT.eg_sh_rr  = YMF262Tables.eg_rate_shift [SLOT.rr + SLOT.ksr ];
		SLOT.eg_m_rr   = unchecked((uint)((1 << SLOT.eg_sh_rr) - 1));
		SLOT.eg_sel_rr = YMF262Tables.eg_rate_select[SLOT.rr + SLOT.ksr ];
	}

	static void update_channels(ref OPL3Channel CH)
	{
		/* update channel passed as a parameter and a channel at CH+=3; */
		if (CH.extended)
		{
			/* we've just switched to combined 4 operator mode */
		}
		else
		{
			/* we've just switched to normal 2 operator mode */
		}
	}

	/* write a value v to register r on OPL chip */
	void WriteRegister(int r, int v)
	{
		int ch_offset = 0;

		if(r.HasBitSet(0x100))
		{
			switch(r)
			{
				case 0x101: /* test register */
					return;

				case 0x104: /* 6 channels enable */
				{
					bool prev;

					{
						ref var CH = ref P_CH[0];    /* channel 0 */
						prev = CH.extended;
						CH.extended = (v>>0).HasBitSet(1);
						if(prev != CH.extended)
							update_channels(ref CH);
					}
					{
						ref var CH = ref P_CH[1];          /* channel 1 */
						prev = CH.extended;
						CH.extended = (v>>1).HasBitSet(1);
						if(prev != CH.extended)
							update_channels(ref CH);
					}
					{
						ref var CH = ref P_CH[2];          /* channel 2 */
						prev = CH.extended;
						CH.extended = (v>>2).HasBitSet(1);
						if(prev != CH.extended)
							update_channels(ref CH);
					}

					{
						ref var CH = ref P_CH[9];    /* channel 9 */
						prev = CH.extended;
						CH.extended = (v>>3).HasBitSet(1);
						if(prev != CH.extended)
							update_channels(ref CH);
					}
					{
						ref var CH = ref P_CH[10];            /* channel 10 */
						prev = CH.extended;
						CH.extended = (v>>4).HasBitSet(1);
						if(prev != CH.extended)
							update_channels(ref CH);
					}
					{
						ref var CH = ref P_CH[11];            /* channel 11 */
						prev = CH.extended;
						CH.extended = (v>>5).HasBitSet(1);
						if(prev != CH.extended)
							update_channels(ref CH);
					}

					return;
				}

				case 0x105: /* OPL3 extensions enable register */

					OPL3_mode = v.HasBitSet(0x01);   /* OPL3 mode when bit0=1 otherwise it is OPL2 mode */

					/* following behaviour was tested on real YMF262,
					switching OPL3/OPL2 modes on the fly:
					- does not change the waveform previously selected (unless when ....)
					- does not update CH.A, CH.B, CH.C and CH.D output selectors (registers c0-c8) (unless when ....)
					- does not disable channels 9-17 on OPL3->OPL2 switch
					- does not switch 4 operator channels back to 2 operator channels
					*/

					return;

				default:
					//if (r < 0x120)
					//	Console.Error.WriteLine("YMF262: write to unknown register (set#2): {0:x3} value={0:x2}",r,v);
					break;
			}

			ch_offset = 9;  /* register page #2 starts from channel 9 (counting from 0) */
		}

		/* adjust bus to 8 bits */
		r &= 0xff;
		v &= 0xff;


		switch(r&0xe0)
		{
			case 0x00:  /* 00-1f:control */
				switch(r&0x1f)
				{
					case 0x01:  /* test register */
						break;
					case 0x02:  /* Timer 1 */
						T[0] = (256-v)*4;
						break;
					case 0x03:  /* Timer 2 */
						T[1] = (256-v)*16;
						break;
					case 0x04:  /* IRQ clear / mask and Timer enable */
						if (v.HasBitSet(0x80))
						{   /* IRQ flags clear */
							ResetStatus(StatusFlags.TimerA | StatusFlags.TimerB);
						}
						else
						{
							StatusFlags vf = (StatusFlags)v;

							/* set IRQ mask ,timer enable */
							bool st1 = vf.HasAllFlags(StatusFlags.ST1);
							bool st2 = vf.HasAllFlags(StatusFlags.ST2);

							/* IRQRST,T1MSK,t2MSK,x,x,x,ST2,ST1 */
							ResetStatus(vf & (StatusFlags.TimerA | StatusFlags.TimerB));
							SetStatusMask((~vf) & (StatusFlags.TimerA | StatusFlags.TimerB));

							/* timer 2 */
							if(st[1] != st2)
							{
								double period = st2 ? TimerBase * T[1] : 0.0;
								st[1] = st2;
								OnTimer(1, period);
							}
							/* timer 1 */
							if(st[0] != st1)
							{
								double period = st1 ? TimerBase * T[0] : 0.0;
								st[0] = st1;
								OnTimer(0, period);
							}
						}
						break;
					case 0x08:  /* x,NTS,x,x, x,x,x,x */
						nts = unchecked((byte)v);
						break;

					default:
						/*Console.Error.WriteLine("YMF262: write to unknown register: {0:x2} value={1:x2}",r,v);*/
						break;
				}
				break;
			case 0x20:  /* am ON, vib ON, ksr, eg_type, mul */
			{
				int slot = YMF262Tables.slot_array[r&0x1f];
				if(slot < 0) return;
				set_mul(slot + ch_offset*2, v);
				break;
			}
			case 0x40:
			{
				int slot = YMF262Tables.slot_array[r&0x1f];
				if(slot < 0) return;
				set_ksl_tl(slot + ch_offset*2, v);
				break;
			}
			case 0x60:
			{
				int slot = YMF262Tables.slot_array[r&0x1f];
				if(slot < 0) return;
				set_ar_dr(slot + ch_offset*2, v);
				break;
			}
			case 0x80:
			{
				int slot = YMF262Tables.slot_array[r&0x1f];
				if(slot < 0) return;
				set_sl_rr(slot + ch_offset*2, v);
				break;
			}
			case 0xa0:
			{
				if (r == 0xbd)          /* am depth, vibrato depth, r,bd,sd,tom,tc,hh */
				{
					if (ch_offset != 0) /* 0xbd register is present in set #1 only */
						return;

					lfo_am_depth = v.HasBitSet(0x80);
					lfo_pm_depth_range = v.HasBitSet(0x40) ? (byte)8 : (byte)0;

					rhythm = unchecked((byte)(v & 0x3f));

					if (rhythm.HasBitSet(0x20))
					{
						/* BD key on/off */
						if (v.HasBitSet(0x10))
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
				int ch_num = (r&0x0f) + ch_offset;
				ref var CH = ref P_CH[ch_num];

				uint block_fnum;

				if(!r.HasBitSet(0x10))
				{   /* a0-a8 */
					block_fnum  = (CH.block_fnum & 0x1f00) | unchecked((uint)v);
				}
				else
				{   /* b0-b8 */
					block_fnum = ((unchecked((uint)v) & 0x1fu) << 8) | (CH.block_fnum&0xff);

					if (OPL3_mode)
					{
						int chan_no = (r&0x0f) + ch_offset;

						/* in OPL3 mode */
						//DO THIS:
						//if this is 1st channel forming up a 4-op channel
						//ALSO keyon/off slots of 2nd channel forming up 4-op channel
						//else normal 2 operator function keyon/off
						//OR THIS:
						//if this is 2nd channel forming up 4-op channel just do nothing
						//else normal 2 operator function keyon/off
						switch(chan_no)
						{
						case 0: case 1: case 2:
						case 9: case 10: case 11:
							if (CH.extended)
							{
								//if this is 1st channel forming up a 4-op channel
								//ALSO keyon/off slots of 2nd channel forming up 4-op channel
								if (v.HasBitSet(0x20))
								{
									FM_KEYON (ref CH.Slot[Slot1], 1);
									FM_KEYON (ref CH.Slot[Slot2], 1);
									FM_KEYON (ref P_CH[ch_num + 3].Slot[Slot1], 1);
									FM_KEYON (ref P_CH[ch_num + 3].Slot[Slot2], 1);
								}
								else
								{
									FM_KEYOFF(ref CH.Slot[Slot1],~1u);
									FM_KEYOFF(ref CH.Slot[Slot2],~1u);
									FM_KEYOFF(ref P_CH[ch_num + 3].Slot[Slot1],~1u);
									FM_KEYOFF(ref P_CH[ch_num + 3].Slot[Slot2],~1u);
								}
							}
							else
							{
								//else normal 2 operator function keyon/off
								if (v.HasBitSet(0x20))
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
						break;

						case 3: case 4: case 5:
						case 12: case 13: case 14:
							if (P_CH[ch_num - 3].extended)
							{
								//if this is 2nd channel forming up 4-op channel just do nothing
							}
							else
							{
								//else normal 2 operator function keyon/off
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
						break;

						default:
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
						break;
						}
					}
					else
					{
						if (v.HasBitSet(0x20))
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
				}
				/* update */
				if(CH.block_fnum != block_fnum)
				{
					byte block  = unchecked((byte)(block_fnum >> 10));

					CH.block_fnum = block_fnum;

					CH.ksl_base = (uint)(YMF262Tables.ksl_tab[block_fnum>>6]);
					CH.fc       = fn_tab[block_fnum&0x03ff] >> (7-block);

					/* BLK 2,1,0 bits -> bits 3,2,1 of kcode */
					CH.kcode    = unchecked((byte)((CH.block_fnum & 0x1c00) >> 9));

					/* the info below is actually opposite to what is stated in the Manuals (verifed on real YMF262) */
					/* if notesel == 0 -> lsb of kcode is bit 10 (MSB) of fnum  */
					/* if notesel == 1 -> lsb of kcode is bit 9 (MSB-1) of fnum */
					if (nts.HasBitSet(0x40))
						CH.kcode |= unchecked((byte)((CH.block_fnum & 0x100) >> 8)); /* notesel == 1 */
					else
						CH.kcode |= unchecked((byte)((CH.block_fnum & 0x200) >> 9)); /* notesel == 0 */

					if (OPL3_mode)
					{
						int chan_no = (r&0x0f) + ch_offset;
						/* in OPL3 mode */
						//DO THIS:
						//if this is 1st channel forming up a 4-op channel
						//ALSO update slots of 2nd channel forming up 4-op channel
						//else normal 2 operator function keyon/off
						//OR THIS:
						//if this is 2nd channel forming up 4-op channel just do nothing
						//else normal 2 operator function keyon/off
						switch(chan_no)
						{
						case 0: case 1: case 2:
						case 9: case 10: case 11:
							if (CH.extended)
							{
								//if this is 1st channel forming up a 4-op channel
								//ALSO update slots of 2nd channel forming up 4-op channel

								/* refresh Total Level in FOUR SLOTs of this channel and channel+3 using data from THIS channel */
								CH.Slot[Slot1].TLL = unchecked((int)(CH.Slot[Slot1].TL + (CH.ksl_base>>CH.Slot[Slot1].ksl)));
								CH.Slot[Slot2].TLL = unchecked((int)(CH.Slot[Slot2].TL + (CH.ksl_base>>CH.Slot[Slot2].ksl)));
								P_CH[ch_num + 3].Slot[Slot1].TLL = unchecked((int)(P_CH[ch_num + 3].Slot[Slot1].TL + (CH.ksl_base>>P_CH[ch_num + 3].Slot[Slot1].ksl)));
								P_CH[ch_num + 3].Slot[Slot2].TLL = unchecked((int)(P_CH[ch_num + 3].Slot[Slot2].TL + (CH.ksl_base>>P_CH[ch_num + 3].Slot[Slot2].ksl)));

								/* refresh frequency counter in FOUR SLOTs of this channel and channel+3 using data from THIS channel */
								CALC_FCSLOT(ref CH, ref CH.Slot[Slot1]);
								CALC_FCSLOT(ref CH, ref CH.Slot[Slot2]);
								CALC_FCSLOT(ref CH, ref P_CH[ch_num + 3].Slot[Slot1]);
								CALC_FCSLOT(ref CH, ref P_CH[ch_num + 3].Slot[Slot2]);
							}
							else
							{
								//else normal 2 operator function
								/* refresh Total Level in both SLOTs of this channel */
								CH.Slot[Slot1].TLL = unchecked((int)(CH.Slot[Slot1].TL + (CH.ksl_base>>CH.Slot[Slot1].ksl)));
								CH.Slot[Slot2].TLL = unchecked((int)(CH.Slot[Slot2].TL + (CH.ksl_base>>CH.Slot[Slot2].ksl)));

								/* refresh frequency counter in both SLOTs of this channel */
								CALC_FCSLOT(ref CH, ref CH.Slot[Slot1]);
								CALC_FCSLOT(ref CH, ref CH.Slot[Slot2]);
							}
						break;

						case 3: case 4: case 5:
						case 12: case 13: case 14:
							if (P_CH[ch_num - 3].extended)
							{
								//if this is 2nd channel forming up 4-op channel just do nothing
							}
							else
							{
								//else normal 2 operator function
								/* refresh Total Level in both SLOTs of this channel */
								CH.Slot[Slot1].TLL = unchecked((int)(CH.Slot[Slot1].TL + (CH.ksl_base>>CH.Slot[Slot1].ksl)));
								CH.Slot[Slot2].TLL = unchecked((int)(CH.Slot[Slot2].TL + (CH.ksl_base>>CH.Slot[Slot2].ksl)));

								/* refresh frequency counter in both SLOTs of this channel */
								CALC_FCSLOT(ref CH, ref CH.Slot[Slot1]);
								CALC_FCSLOT(ref CH, ref CH.Slot[Slot2]);
							}
						break;

						default:
							/* refresh Total Level in both SLOTs of this channel */
							CH.Slot[Slot1].TLL = unchecked((int)(CH.Slot[Slot1].TL + (CH.ksl_base>>CH.Slot[Slot1].ksl)));
							CH.Slot[Slot2].TLL = unchecked((int)(CH.Slot[Slot2].TL + (CH.ksl_base>>CH.Slot[Slot2].ksl)));

							/* refresh frequency counter in both SLOTs of this channel */
							CALC_FCSLOT(ref CH, ref CH.Slot[Slot1]);
							CALC_FCSLOT(ref CH, ref CH.Slot[Slot2]);
						break;
						}
					}
					else
					{
						/* in OPL2 mode */

						/* refresh Total Level in both SLOTs of this channel */
						CH.Slot[Slot1].TLL = unchecked((int)(CH.Slot[Slot1].TL + (CH.ksl_base>>CH.Slot[Slot1].ksl)));
						CH.Slot[Slot2].TLL = unchecked((int)(CH.Slot[Slot2].TL + (CH.ksl_base>>CH.Slot[Slot2].ksl)));

						/* refresh frequency counter in both SLOTs of this channel */
						CALC_FCSLOT(ref CH, ref CH.Slot[Slot1]);
						CALC_FCSLOT(ref CH, ref CH.Slot[Slot2]);
					}
				}
				break;
			}
			case 0xc0:
			{
				/* CH.D, CH.C, CH.B, CH.A, FB(3bits), C */
				if ((r & 0xf) > 8) return;

				int ch_num = (r & 0xf) + ch_offset;

				ref var CH = ref P_CH[ch_num];

				if (OPL3_mode)
				{
					int @base = ((r&0xf) + ch_offset) * 4;

					/* OPL3 mode */
					pan[ @base    ] = v.HasBitSet(0x10) ? ~0u : 0; /* ch.A */
					pan[ @base +1 ] = v.HasBitSet(0x20) ? ~0u : 0; /* ch.B */
					pan[ @base +2 ] = v.HasBitSet(0x40) ? ~0u : 0; /* ch.C */
					pan[ @base +3 ] = v.HasBitSet(0x80) ? ~0u : 0; /* ch.D */
				}
				else
				{
					int @base = ((r&0xf) + ch_offset) * 4;

					/* OPL2 mode - always enabled */
					pan[ @base    ] = ~0u;      /* ch.A */
					pan[ @base +1 ] = ~0u;      /* ch.B */
					pan[ @base +2 ] = ~0u;      /* ch.C */
					pan[ @base +3 ] = ~0u;      /* ch.D */
				}

				pan_ctrl_value[(r & 0xf) + ch_offset] = unchecked((uint)v);    /* store control value for OPL3/OPL2 mode switching on the fly */

				CH.Slot[Slot1].FB = unchecked((byte)((v >> 1).HasAnyBitSet(7) ? ((v >> 1) & 7) + 7 : 0));
				CH.Slot[Slot1].CON = unchecked((byte)(v & 1));

				if (OPL3_mode)
				{
					int chan_no = (r&0x0f) + ch_offset;

					switch(chan_no)
					{
						case 0: case 1: case 2:
						case 9: case 10: case 11:
							if (CH.extended)
							{
								byte conn = unchecked((byte)((CH.Slot[Slot1].CON << 1) | (P_CH[ch_num + 3].Slot[Slot1].CON << 0)));

								switch (conn)
								{
									case 0:
										/* 1 -> 2 -> 3 -> 4 - out */

										CH.Slot[Slot1].connect = phase_modulation;
										CH.Slot[Slot2].connect = phase_modulation2;
										P_CH[ch_num + 3].Slot[Slot1].connect = phase_modulation;
										P_CH[ch_num + 3].Slot[Slot2].connect = chanout[chan_no + 3];
									break;
									case 1:
										/* 1 -> 2 -\
											3 -> 4 -+- out */

										CH.Slot[Slot1].connect = phase_modulation;
										CH.Slot[Slot2].connect = chanout[chan_no];
										P_CH[ch_num + 3].Slot[Slot1].connect = phase_modulation;
										P_CH[ch_num + 3].Slot[Slot2].connect = chanout[chan_no + 3];
									break;
									case 2:
										/* 1 -----------\
											2 -> 3 -> 4 -+- out */

										CH.Slot[Slot1].connect = chanout[chan_no];
										CH.Slot[Slot2].connect = phase_modulation2;
										P_CH[ch_num + 3].Slot[Slot1].connect = phase_modulation;
										P_CH[ch_num + 3].Slot[Slot2].connect = chanout[chan_no + 3];
									break;
									case 3:
										/* 1 ------\
											2 -> 3 -+- out
											4 ------/     */
										CH.Slot[Slot1].connect = chanout[chan_no];
										CH.Slot[Slot2].connect = phase_modulation2;
										P_CH[ch_num + 3].Slot[Slot1].connect = chanout[chan_no + 3];
										P_CH[ch_num + 3].Slot[Slot2].connect = chanout[chan_no + 3];
									break;
								}
							}
							else
							{
								/* 2 operators mode */
								CH.Slot[Slot1].connect = (CH.Slot[Slot1].CON != 0) ? chanout[(r & 0xf) + ch_offset] : phase_modulation;
								CH.Slot[Slot2].connect = chanout[(r & 0xf) + ch_offset];
							}
							break;

						case 3: case 4: case 5:
						case 12: case 13: case 14:
							if (P_CH[ch_num - 3].extended)
							{
								byte conn = unchecked((byte)((P_CH[ch_num - 3].Slot[Slot1].CON << 1) | (CH.Slot[Slot1].CON << 0)));
								switch (conn)
								{
									case 0:
										/* 1 -> 2 -> 3 -> 4 - out */

										P_CH[ch_num - 3].Slot[Slot1].connect = phase_modulation;
										P_CH[ch_num - 3].Slot[Slot2].connect = phase_modulation2;
										CH.Slot[Slot1].connect = phase_modulation;
										CH.Slot[Slot2].connect = chanout[ chan_no ];
										break;
									case 1:
										/* 1 -> 2 -\
											3 -> 4 -+- out */

										P_CH[ch_num - 3].Slot[Slot1].connect = phase_modulation;
										P_CH[ch_num - 3].Slot[Slot2].connect = chanout[chan_no - 3];
										CH.Slot[Slot1].connect = phase_modulation;
										CH.Slot[Slot2].connect = chanout[chan_no];
										break;
									case 2:
										/* 1 -----------\
											2 -> 3 -> 4 -+- out */

										P_CH[ch_num - 3].Slot[Slot1].connect = chanout[ chan_no - 3 ];
										P_CH[ch_num - 3].Slot[Slot2].connect = phase_modulation2;
										CH.Slot[Slot1].connect = phase_modulation;
										CH.Slot[Slot2].connect = chanout[chan_no];
										break;
									case 3:
										/* 1 ------\
											2 -> 3 -+- out
											4 ------/     */
										P_CH[ch_num - 3].Slot[Slot1].connect = chanout[chan_no - 3];
										P_CH[ch_num - 3].Slot[Slot2].connect = phase_modulation2;
										CH.Slot[Slot1].connect = chanout[chan_no];
										CH.Slot[Slot2].connect = chanout[chan_no];
										break;
								}
							}
							else
							{
								/* 2 operators mode */
								CH.Slot[Slot1].connect = (CH.Slot[Slot1].CON != 0) ? chanout[(r & 0xf) + ch_offset] : phase_modulation;
								CH.Slot[Slot2].connect = chanout[(r & 0xf) + ch_offset];
							}
							break;

						default:
							/* 2 operators mode */
							CH.Slot[Slot1].connect = (CH.Slot[Slot1].CON != 0) ? chanout[(r & 0xf) + ch_offset] : phase_modulation;
							CH.Slot[Slot2].connect = chanout[(r & 0xf) + ch_offset];
							break;
					}
				}
				else
				{
					/* OPL2 mode - always 2 operators mode */
					CH.Slot[Slot1].connect = (CH.Slot[Slot1].CON != 0) ? chanout[(r & 0xf) + ch_offset] : phase_modulation;
					CH.Slot[Slot2].connect = chanout[(r & 0xf) + ch_offset];
				}
				break;
			}
			case 0xe0: /* waveform select */
			{
				int slot = YMF262Tables.slot_array[r&0x1f];
				if(slot < 0) return;

				slot += ch_offset*2;

				ref var CH = ref P_CH[slot/2];


				/* store 3-bit value written regardless of current OPL2 or OPL3 mode... (verified on real YMF262) */
				v &= 7;
				CH.Slot[slot & 1].waveform_number = unchecked((byte)v);

				/* ... but select only waveforms 0-3 in OPL2 mode */
				if (!OPL3_mode)
				{
					v &= 3; /* we're in OPL2 mode */
				}
				CH.Slot[slot & 1].wavetable = unchecked((uint)(v * YMF262Tables.SineLength));
				break;
			}
		}
	}

	/* lock/unlock for common table */
	int LockTable()
	{
		YMF262Tables.num_lock++;
		if (YMF262Tables.num_lock > 1) return 0;

		/* first time */

		if (!YMF262Tables.InitializeTables())
		{
			YMF262Tables.num_lock--;
			return -1;
		}

		return 0;
	}

	void UnlockTable()
	{
		if (YMF262Tables.num_lock != 0) YMF262Tables.num_lock--;
		if (YMF262Tables.num_lock != 0) return;

		/* last time */
		OPLCloseTable();
	}

	public override void ResetChip()
	{
		int c,s;

		eg_timer = 0;
		eg_cnt   = 0;

		noise_rng = 1;    /* noise shift register */
		nts       = 0;    /* note split */
		ResetStatus(StatusFlags.TimerA | StatusFlags.TimerB);

		/* reset with register write */
		WriteRegister(0x01, 0); /* test register */
		WriteRegister(0x02, 0); /* Timer1 */
		WriteRegister(0x03, 0); /* Timer2 */
		WriteRegister(0x04, 0); /* IRQ mask clear */


	//FIX IT  registers 101, 104 and 105


	//FIX IT (dont change CH.D, CH.C, CH.B and CH.A in C0-C8 registers)
		for(c = 0xff ; c >= 0x20 ; c-- )
			WriteRegister(c, 0);
	//FIX IT (dont change CH.D, CH.C, CH.B and CH.A in C0-C8 registers)
		for(c = 0x1ff ; c >= 0x120 ; c-- )
			WriteRegister(c, 0);



		/* reset operator parameters */
		for( c = 0 ; c < 9*2 ; c++ )
		{
			ref OPL3Channel CH = ref P_CH[c];

			for(s = 0 ; s < 2 ; s++ )
			{
				CH.Slot[s].state     = EnvelopeGeneratorPhase.Off;
				CH.Slot[s].volume    = MaxAttenuationIndex;
			}
		}
	}

	/* Create one of virtual YMF262 */
	/* 'clock' is chip clock in Hz  */
	/* 'rate'  is sampling rate  */
	public override void Initialize(uint clock, uint rate)
	{
		if (LockTable() == -1) throw new Exception("LockTable failed");

		//this.type  = YMF262;
		this.clock = clock;
		this.rate  = rate;

		/* init global tables */
		InitializeGlobalTables();

		ResetChip();
	}

	public override void ShutDown()
	{
		/* Destroy one of virtual YMF262 */
		UnlockTable();
	}

	/* YMF262 I/O interface */
	public override bool Write(int a, int v)
	{
		/* data bus is 8 bits */
		v &= 0xff;

		switch(a&3)
		{
			case 0: /* address port 0 (register set #1) */
				address = v;
				break;

			case 1: /* data port - ignore A1 */
			case 3: /* data port - ignore A1 */
				OnUpdate(0);
				WriteRegister(address, v);
				break;

			case 2: /* address port 1 (register set #2) */

				/* verified on real YMF262:
				in OPL3 mode:
					address line A1 is stored during *address* write and ignored during *data* write.

				in OPL2 mode:
					register set#2 writes go to register set#1 (ignoring A1)
					verified on registers from set#2: 0x01, 0x04, 0x20-0xef
					The only exception is register 0x05.
				*/
				if (OPL3_mode)
				{
					/* OPL3 mode */
					address = v | 0x100;
				}
				else
				{
					/* in OPL2 mode the only accessible in set #2 is register 0x05 */
					if( v==5 )
						address = v | 0x100;
					else
						address = v;  /* verified range: 0x01, 0x04, 0x20-0xef(set #2 becomes set #1 in opl2 mode) */
				}
				break;
		}

		return status.HasAllFlags(StatusFlags.IRQEnabled);
	}

	public override byte Read(int a)
	{
		/* Note on status register: */

		/* YM3526(OPL) and YM3812(OPL2) return bit2 and bit1 in HIGH state */

		/* YMF262(OPL3) always returns bit2 and bit1 in LOW state */
		/* which can be used to identify the chip */

		/* YMF278(OPL4) returns bit2 in LOW and bit1 in HIGH state ??? info from manual - not verified */

		if (a == 0)
		{
			/* status port */
			return unchecked((byte)status);
		}

		return 0x00;    /* verified on real YMF262 */
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
		}

		/* reload timer */
		OnTimer(c, TimerBase * T[c]);

		return status.HasAllFlags(StatusFlags.IRQEnabled);
	}

	// `buffers` is an array of 18 pointers, all pointing to separate 32-bit interlaced stereo
	// buffers of `length` size in samples.
	public override void UpdateMulti(Memory<int>?[] buffers, uint[] vuMax)
	{
		bool rhythm_part = rhythm.HasBitSet(0x20);

		for (int i = 0; i < buffers.Length; i++)
		{
			advance_lfo();

			/* clear channel outputs */
			for (int j=0; j < chanout.Length; j++)
				chanout[j].Value = 0;

			/* register set #1 */
			chan_calc(ref P_CH[0]);            /* extended 4op ch#0 part 1 or 2op ch#0 */
			if (P_CH[0].extended)
				chan_calc_ext(ref P_CH[3]);    /* extended 4op ch#0 part 2 */
			else
				chan_calc(ref P_CH[3]);        /* standard 2op ch#3 */


			chan_calc(ref P_CH[1]);            /* extended 4op ch#1 part 1 or 2op ch#1 */
			if (P_CH[1].extended)
				chan_calc_ext(ref P_CH[4]);    /* extended 4op ch#1 part 2 */
			else
				chan_calc(ref P_CH[4]);        /* standard 2op ch#4 */


			chan_calc(ref P_CH[2]);            /* extended 4op ch#2 part 1 or 2op ch#2 */
			if (P_CH[2].extended)
				chan_calc_ext(ref P_CH[5]);    /* extended 4op ch#2 part 2 */
			else
				chan_calc(ref P_CH[5]);        /* standard 2op ch#5 */


			if(!rhythm_part)
			{
				chan_calc(ref P_CH[6]);
				chan_calc(ref P_CH[7]);
				chan_calc(ref P_CH[8]);
			}
			else
			{
				/* Rhythm part */
				chan_calc_rhythm(P_CH, noise_rng.HasBitSet(1));
			}

			/* register set #2 */
			chan_calc(ref P_CH[ 9]);
			if (P_CH[9].extended)
				chan_calc_ext(ref P_CH[12]);
			else
				chan_calc(ref P_CH[12]);


			chan_calc(ref P_CH[10]);
			if (P_CH[10].extended)
				chan_calc_ext(ref P_CH[13]);
			else
				chan_calc(ref P_CH[13]);


			chan_calc(ref P_CH[11]);
			if (P_CH[11].extended)
				chan_calc_ext(ref P_CH[14]);
			else
				chan_calc(ref P_CH[14]);


			/* channels 15,16,17 are fixed 2-operator channels only */
			chan_calc(ref P_CH[15]);
			chan_calc(ref P_CH[16]);
			chan_calc(ref P_CH[17]);

			/* accumulator register set #1 */
			for (int j = 0, k = 0; j < 18; j++)
			{
				var maybeBuffer = buffers[j];

				if (maybeBuffer.HasValue)
				{
					var buffer = maybeBuffer.Value.Span;

					buffer[i * 2 + 0] += chanout[j] & (int)pan[k++];
					buffer[i * 2 + 1] += chanout[j] & (int)pan[k++];
					k += 2; // skip next two pans
				}
				else
					k += 4;
			}

			advance();
		}
	}
}