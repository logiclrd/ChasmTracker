using System;

namespace ChasmTracker.Pages;

using ChasmTracker.Input;
using ChasmTracker.Songs;
using ChasmTracker.Utility;
using ChasmTracker.VGA;
using ChasmTracker.Widgets;

public class InstrumentListVolumeSubpage : InstrumentListEnvelopeSubpageBase
{
	ThumbBarWidget thumbBarGlobalVolume;
	ThumbBarWidget thumbBarFadeOut;
	ThumbBarWidget thumbBarVolumeSwing;

	static Envelope s_defaultVolumeEnvelope = new Envelope(64);

	int _currentEnvelopeNode;

	public InstrumentListVolumeSubpage()
		: base(PageNumbers.InstrumentListVolume)
	{
		thumbBarGlobalVolume = new ThumbBarWidget(new Point(54, 42), 17, 0, 128);
		thumbBarFadeOut = new ThumbBarWidget(new Point(54, 43), 17, 0, 256);
		thumbBarVolumeSwing = new ThumbBarWidget(new Point(54, 46), 17, 0, 100);

		thumbBarGlobalVolume.Changed += UpdateValues;
		thumbBarFadeOut.Changed += UpdateValues;
		thumbBarVolumeSwing.Changed += UpdateValues;

		AddWidget(thumbBarGlobalVolume);
		AddWidget(thumbBarFadeOut);
		AddWidget(thumbBarVolumeSwing);
	}

	public void ResetCurrentNode(SongInstrument ins)
	{
		_currentEnvelopeNode = Math.Max(0, (ins.VolumeEnvelope?.Nodes.Count ?? 0) - 1);
	}

	protected override void EnvelopeDraw()
	{
		bool isSelected = SelectedActiveWidget == otherEnvelope;

		var ins = Song.CurrentSong.GetInstrument(CurrentInstrument);

		DrawEnvelopeLabel("Volume", isSelected);

		EnvelopeDraw(ins.VolumeEnvelope, s_defaultVolumeEnvelope, false, _currentEnvelopeNode,
			ins.Flags.HasAllFlags(InstrumentFlags.VolumeEnvelope),
			ins.Flags.HasAllFlags(InstrumentFlags.VolumeEnvelopeLoop),
			ins.Flags.HasAllFlags(InstrumentFlags.VolumeEnvelopeSustain),
			0);
	}

	protected override bool EnvelopeHandleKey(KeyEvent k)
	{
		var ins = Song.CurrentSong.GetInstrument(CurrentInstrument);

		if (EnvelopeHandleMouse(k, ref ins.VolumeEnvelope, s_defaultVolumeEnvelope, ref _currentEnvelopeNode))
		{
			ins.Flags |= InstrumentFlags.VolumeEnvelope;
			return true;
		}

		int r;

		if (s_envelopeEditMode)
			r = EnvelopeHandleKeyEditMode(k, ref ins.VolumeEnvelope, s_defaultVolumeEnvelope, middle: false, ref _currentEnvelopeNode);
		else
			r = EnvelopeHandleKeyViewMode(k, ref ins.VolumeEnvelope, s_defaultVolumeEnvelope, ref _currentEnvelopeNode, InstrumentFlags.VolumeEnvelope);

		if (r.HasBitSet(2))
		{
			r ^= 2;
			ins.Flags |= InstrumentFlags.VolumeEnvelope;
		}

		return (r != 0);
	}

	public override void PredrawHook()
	{
		var ins = Song.CurrentSong.GetInstrument(CurrentInstrument);
		var env = ins.VolumeEnvelope;

		if (env == null)
			ins.Flags &= ~InstrumentFlags.VolumeEnvelope;

		var toggleEnabled = (ToggleWidget)widgetEnvelopeEnabled;

		toggleEnabled.State = ins.Flags.HasAllFlags(InstrumentFlags.VolumeEnvelope);
		toggleEnvelopeCarry.State = ins.Flags.HasAllFlags(InstrumentFlags.VolumeEnvelopeCarry);
		toggleEnvelopeLoop.State = ins.Flags.HasAllFlags(InstrumentFlags.VolumeEnvelopeLoop);
		toggleEnvelopeSustainLoop.State = ins.Flags.HasAllFlags(InstrumentFlags.VolumeEnvelopeSustain);

		/* FIXME: this is the wrong place for this.
		... and it's probably not even right -- how does Impulse Tracker handle loop constraints?
		See below for panning/pitch envelopes; same deal there. */
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

		/* current_song hack: shifting values all over the place here, ugh */
		thumbBarGlobalVolume.Value = ins.GlobalVolume;
		thumbBarFadeOut.Value = ins.FadeOut >> 5;
		thumbBarVolumeSwing.Value = ins.VolumeSwing;
	}

	protected override void UpdateValues()
	{
		var ins = Song.CurrentSong.GetInstrument(CurrentInstrument);

		Status.Flags |= StatusFlags.SongNeedsSave;

		ins.Flags &= ~(InstrumentFlags.VolumeEnvelope | InstrumentFlags.VolumeEnvelopeCarry | InstrumentFlags.VolumeEnvelopeLoop | InstrumentFlags.VolumeEnvelopeSustain);

		if (((ToggleWidget)widgetEnvelopeEnabled).State)
			ins.Flags |= InstrumentFlags.VolumeEnvelope;
		if (toggleEnvelopeCarry.State)
			ins.Flags |= InstrumentFlags.VolumeEnvelopeCarry;
		if (toggleEnvelopeLoop.State)
			ins.Flags |= InstrumentFlags.VolumeEnvelopeLoop;
		if (toggleEnvelopeSustainLoop.State)
			ins.Flags |= InstrumentFlags.VolumeEnvelopeSustain;

		var env = ins.VolumeEnvelope;

		if (env != null)
		{
			if (env.LoopStart != numberEntryEnvelopeLoopBegin.Value)
			{
				env.LoopStart = numberEntryEnvelopeLoopBegin.Value;
				ins.Flags |= InstrumentFlags.VolumeEnvelopeLoop;
			}

			if (env.LoopEnd != numberEntryEnvelopeLoopEnd.Value)
			{
				env.LoopEnd = numberEntryEnvelopeLoopEnd.Value;
				ins.Flags |= InstrumentFlags.VolumeEnvelopeLoop;
			}

			if (env.SustainStart != numberEntryEnvelopeSustainLoopBegin.Value)
			{
				env.SustainStart = numberEntryEnvelopeSustainLoopBegin.Value;
				ins.Flags |= InstrumentFlags.VolumeEnvelopeSustain;
			}

			if (env.SustainEnd != numberEntryEnvelopeSustainLoopEnd.Value)
			{
				env.SustainEnd = numberEntryEnvelopeSustainLoopEnd.Value;
				ins.Flags |= InstrumentFlags.VolumeEnvelopeSustain;
			}
		}

		/* more ugly shifts */
		ins.GlobalVolume = thumbBarGlobalVolume.Value;
		ins.FadeOut = thumbBarFadeOut.Value << 5;
		ins.VolumeSwing = thumbBarVolumeSwing.Value;

		Song.CurrentSong.UpdatePlayingInstrument(CurrentInstrument);
	}

	protected override string Label => "Volume";

	public override void DrawConst()
	{
		base.DrawConst();

		VGAMem.DrawBox(new Point(53, 41), new Point(71, 44), BoxTypes.Thick | BoxTypes.Inner | BoxTypes.Inset);
		VGAMem.DrawBox(new Point(53, 45), new Point(71, 47), BoxTypes.Thick | BoxTypes.Inner | BoxTypes.Inset);

		VGAMem.DrawText("Global Volume", new Point(40, 42), (0, 2));
		VGAMem.DrawText("Fadeout", new Point(46, 43), (0, 2));
		VGAMem.DrawText("Volume Swing %", new Point(39, 46), (0, 2));
	}

	public override bool? PreHandleKey(KeyEvent k)
	{
		var ins = Song.CurrentSong.GetInstrument(CurrentInstrument);

		if (s_envelopeMouseEdit)
		{
			if (EnvelopeHandleMouse(k, ref ins.VolumeEnvelope, s_defaultVolumeEnvelope, ref _currentEnvelopeNode))
			{
				ins.Flags |= InstrumentFlags.VolumeEnvelope;
				return true;
			}
		}

		if ((k.Sym == KeySym.l || k.Sym == KeySym.b) && k.Modifiers.HasAnyFlag(KeyMod.Alt))
			return 0 != EnvelopeHandleKeyViewMode(k, ref ins.VolumeEnvelope, s_defaultVolumeEnvelope, ref _currentEnvelopeNode, InstrumentFlags.VolumeEnvelope);

		return base.PreHandleKey(k);
	}
}
