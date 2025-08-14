namespace ChasmTracker.Pages;

using System;
using ChasmTracker.Input;
using ChasmTracker.Songs;
using ChasmTracker.Utility;
using ChasmTracker.VGA;
using ChasmTracker.Widgets;

public class InstrumentListPitchSubpage : InstrumentListEnvelopeSubpageBase
{
	ThumbBarWidget thumbBarDefaultCutoff;
	ThumbBarWidget thumbBarDefaultResonance;
	BitSetWidget bitSetMIDIChannels;
	ThumbBarWidget thumbBarMIDIProgram;
	ThumbBarWidget thumbBarMIDIBankLow;
	ThumbBarWidget thumbBarMIDIBankHigh;

	int _currentEnvelopeNode;

	public InstrumentListPitchSubpage()
		: base(PageNumbers.InstrumentListPitch)
	{
		thumbBarDefaultCutoff = new ThumbBarWidget(new Point(54, 42), 17, -1, 127);
		thumbBarDefaultCutoff.TextAtMinimum = "Off";
		thumbBarDefaultResonance = new ThumbBarWidget(new Point(54, 43), 17, -1, 127);
		thumbBarDefaultResonance.TextAtMinimum = "Off";

		/* midi crap */
		bitSetMIDIChannels = new BitSetWidget(new Point(54, 44), 17,
			17,
			new[] { " 1", " 2", " 3", " 4", " 5", " 6", " 7", " 8", " 9", "P", "11", "12", "13", "14", "15", "16", "M" },
			new[] { ".", ".", ".", ".", ".", ".", ".", ".", ".", "p", ".", ".", ".", ".", ".", ".", "m" },
			new Shared<int>());

		bitSetMIDIChannels.ActivationKeys = "123456789pabcdefm".ToCharArray();

		thumbBarMIDIProgram = new ThumbBarWidget(new Point(54, 45), 17, -1, 127);
		thumbBarMIDIProgram.TextAtMinimum = "Off";
		thumbBarMIDIBankLow = new ThumbBarWidget(new Point(54, 46), 17, -1, 127);
		thumbBarMIDIBankLow.TextAtMinimum = "Off";
		thumbBarMIDIBankHigh = new ThumbBarWidget(new Point(54, 47), 17, -1, 127);
		thumbBarMIDIBankHigh.TextAtMinimum = "Off";

		thumbBarDefaultCutoff.Changed += UpdateValues;
		thumbBarDefaultResonance.Changed += UpdateValues;
		bitSetMIDIChannels.Changed += UpdateValues;
		thumbBarMIDIProgram.Changed += UpdateValues;
		thumbBarMIDIBankLow.Changed += UpdateValues;
		thumbBarMIDIBankHigh.Changed += UpdateValues;

		AddWidget(thumbBarDefaultCutoff);
		AddWidget(thumbBarDefaultResonance);
		AddWidget(bitSetMIDIChannels);
		AddWidget(thumbBarMIDIProgram);
		AddWidget(thumbBarMIDIBankLow);
		AddWidget(thumbBarMIDIBankHigh);
	}

	protected override Widget CreateEnvelopeEnabledWidget()
	{
		return new MenuToggleWidget(new Point(54, 28),
			new[] { "Off", "On Pitch", "On Filter" });
	}

	public void ResetCurrentNode(SongInstrument ins)
	{
		_currentEnvelopeNode = Math.Max(0, (ins.PitchEnvelope?.Nodes.Count ?? 0) - 1);
	}

	protected override void EnvelopeDraw()
	{
		bool isSelected = SelectedActiveWidget == otherEnvelope;

		var ins = Song.CurrentSong.GetInstrument(CurrentInstrument);

		DrawEnvelopeLabel("Pitch", isSelected);

		EnvelopeDraw(ins.PitchEnvelope, false, _currentEnvelopeNode,
			ins.Flags.HasFlag(InstrumentFlags.PitchEnvelope),
			ins.Flags.HasFlag(InstrumentFlags.PitchEnvelopeLoop),
			ins.Flags.HasFlag(InstrumentFlags.PitchEnvelopeSustain),
			0);
	}

	protected override bool EnvelopeHandleKey(KeyEvent k)
	{
		var ins = Song.CurrentSong.GetInstrument(CurrentInstrument);

		if (EnvelopeHandleMouse(k, ins.PitchEnvelope, ref _currentEnvelopeNode))
		{
			ins.Flags |= InstrumentFlags.PitchEnvelope;
			return true;
		}

		int r;

		if (s_envelopeEditMode)
			r = EnvelopeHandleKeyEditMode(k, ins.PitchEnvelope, ref _currentEnvelopeNode);
		else
			r = EnvelopeHandleKeyViewMode(k, ins.PitchEnvelope, ref _currentEnvelopeNode, InstrumentFlags.PitchEnvelope);

		if (r.HasBitSet(2))
		{
			r ^= 2;
			ins.Flags |= InstrumentFlags.PitchEnvelope;
		}

		return (r != 0);
	}

	public override void PredrawHook()
	{
		var ins = Song.CurrentSong.GetInstrument(CurrentInstrument);
		var env = ins.PitchEnvelope;

		if (env == null)
			ins.Flags &= ~InstrumentFlags.PitchEnvelope;

		var menuToggleEnabled = (MenuToggleWidget)widgetEnvelopeEnabled;

		menuToggleEnabled.State =
			ins.Flags.HasFlag(InstrumentFlags.PitchEnvelope)
			? (ins.Flags.HasFlag(InstrumentFlags.Filter) ? 2 : 1)
			: 0;

		toggleEnvelopeCarry.State = ins.Flags.HasFlag(InstrumentFlags.PitchEnvelopeCarry);
		toggleEnvelopeLoop.State = ins.Flags.HasFlag(InstrumentFlags.PitchEnvelopeLoop);
		toggleEnvelopeSustainLoop.State = ins.Flags.HasFlag(InstrumentFlags.PitchEnvelopeSustain);

		if (env != null)
		{
			if (env.LoopStart > env.LoopEnd)
				env.LoopEnd = env.LoopStart;
			if (env.SustainStart > env.SustainEnd)
				env.SustainEnd = env.SustainStart;

			numberEntryEnvelopeLoopBegin.Maximum = env.Nodes.Count - 1;
			numberEntryEnvelopeLoopEnd.Maximum = env.Nodes.Count - 1;
			numberEntryEnvelopeSustainLoopBegin.Maximum = env.Nodes.Count - 1;
			numberEntryEnvelopeSustainLoopEnd.Maximum = env.Nodes.Count - 1;

			numberEntryEnvelopeLoopBegin.Value = env.LoopStart;
			numberEntryEnvelopeLoopEnd.Value = env.LoopEnd;
			numberEntryEnvelopeSustainLoopBegin.Value = env.SustainStart;
			numberEntryEnvelopeSustainLoopEnd.Value = env.SustainEnd;
		}

		if ((ins.IFCutoff & 0x80) != 0)
			thumbBarDefaultCutoff.Value = ins.IFCutoff & 0x7F;
		else
			thumbBarDefaultCutoff.Value = -1;

		if ((ins.IFResonance & 0x80) != 0)
			thumbBarDefaultResonance.Value = ins.IFResonance & 0x7F;
		else
			thumbBarDefaultResonance.Value = -1;

		/* printf("ins%02d: ch%04d pgm%04d bank%06d drum%04d\n", current_instrument,
			ins->midi_channel, ins->midi_program, ins->midi_bank, ins->midi_drum_key); */
		bitSetMIDIChannels.Value = ins.MIDIChannelMask;
		thumbBarMIDIProgram.Value = ins.MIDIProgram;
		thumbBarMIDIBankLow.Value = ins.MIDIBank & 0xFF;
		thumbBarMIDIBankHigh.Value = ins.MIDIBank >> 8;

		/* what is midi_drum_key for? */
	}

	protected override void UpdateValues()
	{
		var ins = Song.CurrentSong.GetInstrument(CurrentInstrument);

		Status.Flags |= StatusFlags.SongNeedsSave;

		ins.Flags &= ~(InstrumentFlags.PitchEnvelope | InstrumentFlags.PitchEnvelopeCarry | InstrumentFlags.PitchEnvelopeLoop | InstrumentFlags.PitchEnvelopeSustain);

		var enabledWidget = (MenuToggleWidget)widgetEnvelopeEnabled;

		if (enabledWidget.State >= 2)
			ins.Flags |= InstrumentFlags.Filter;
		if (enabledWidget.State >= 1)
			ins.Flags |= InstrumentFlags.PitchEnvelope;
		if (toggleEnvelopeCarry.State)
			ins.Flags |= InstrumentFlags.PitchEnvelopeCarry;
		if (toggleEnvelopeLoop.State)
			ins.Flags |= InstrumentFlags.PitchEnvelopeLoop;
		if (toggleEnvelopeSustainLoop.State)
			ins.Flags |= InstrumentFlags.PitchEnvelopeSustain;

		var env = ins.PitchEnvelope;

		if (env != null)
		{
			if (env.LoopStart != numberEntryEnvelopeLoopBegin.Value)
			{
				env.LoopStart = numberEntryEnvelopeLoopBegin.Value;
				ins.Flags |= InstrumentFlags.PitchEnvelopeLoop;
			}

			if (env.LoopEnd != numberEntryEnvelopeLoopEnd.Value)
			{
				env.LoopEnd = numberEntryEnvelopeLoopEnd.Value;
				ins.Flags |= InstrumentFlags.PitchEnvelopeLoop;
			}

			if (env.SustainStart != numberEntryEnvelopeSustainLoopBegin.Value)
			{
				env.SustainStart = numberEntryEnvelopeSustainLoopBegin.Value;
				ins.Flags |= InstrumentFlags.PitchEnvelopeSustain;
			}

			if (env.SustainEnd != numberEntryEnvelopeSustainLoopEnd.Value)
			{
				env.SustainEnd = numberEntryEnvelopeSustainLoopEnd.Value;
				ins.Flags |= InstrumentFlags.PitchEnvelopeSustain;
			}
		}

		if (thumbBarDefaultCutoff.Value > -1)
			ins.IFCutoff = thumbBarDefaultCutoff.Value | 0x80;
		else
			ins.IFCutoff = 0x7F;

		if (thumbBarDefaultResonance.Value > -1)
			ins.IFResonance = thumbBarDefaultResonance.Value | 0x80;
		else
			ins.IFResonance = 0x7F;

		ins.MIDIChannelMask = bitSetMIDIChannels.Value;
		ins.MIDIProgram = thumbBarMIDIProgram.Value;
		ins.MIDIBank =
			(thumbBarMIDIBankHigh.Value << 8) |
			(thumbBarMIDIBankLow.Value & 0xFF);

		Song.CurrentSong.UpdatePlayingInstrument(CurrentInstrument);
	}

	protected override string Label => "Frequency";

	public override void DrawConst()
	{
		base.DrawConst();

		VGAMem.DrawBox(new Point(53, 41), new Point(71, 48), BoxTypes.Thick | BoxTypes.Inner | BoxTypes.Inset);

		VGAMem.DrawText("Default Cutoff", new Point(36, 42), (0, 2));
		VGAMem.DrawText("Default Resonance", new Point(36, 43), (0, 2));
		VGAMem.DrawText("MIDI Channels", new Point(36, 44), (0, 2));
		VGAMem.DrawText("MIDI Program", new Point(36, 45), (0, 2));
		VGAMem.DrawText("MIDI Bank Low", new Point(36, 46), (0, 2));
		VGAMem.DrawText("MIDI Bank High", new Point(36, 47), (0, 2));
	}

	public override bool? PreHandleKey(KeyEvent k)
	{
		var ins = Song.CurrentSong.GetInstrument(CurrentInstrument);

		if (s_envelopeMouseEdit)
		{
			if (EnvelopeHandleMouse(k, ins.PitchEnvelope, ref _currentEnvelopeNode))
			{
				ins.Flags |= InstrumentFlags.PitchEnvelope;
				return true;
			}
		}

		if ((k.Sym == KeySym.l || k.Sym == KeySym.b) && k.Modifiers.HasAnyFlag(KeyMod.Alt))
			return 0 != EnvelopeHandleKeyViewMode(k, ins.PitchEnvelope, ref _currentEnvelopeNode, InstrumentFlags.PitchEnvelope);

		return base.PreHandleKey(k);
	}
}
