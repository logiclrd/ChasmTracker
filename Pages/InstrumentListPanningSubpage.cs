using System;

namespace ChasmTracker.Pages;

using ChasmTracker.Input;
using ChasmTracker.Songs;
using ChasmTracker.Utility;
using ChasmTracker.VGA;
using ChasmTracker.Widgets;

public class InstrumentListPanningSubpage : InstrumentListEnvelopeSubpageBase
{
	ToggleWidget toggleDefaultPanning;
	ThumbBarWidget thumbBarPanningValue;
	OtherWidget otherPitchPanCenter;
	ThumbBarWidget thumbBarPitchPanSeparation;
	ThumbBarWidget thumbBarPanningSwing;

	int _currentEnvelopeNode;

	public InstrumentListPanningSubpage()
		: base(PageNumbers.InstrumentListPanning)
	{
		toggleDefaultPanning = new ToggleWidget(new Point(54, 42));
		thumbBarPanningValue = new ThumbBarWidget(new Point(54, 43), 9, 0, 64);

		otherPitchPanCenter = new OtherWidget(new Point(54, 45), new Size(9, 1));
		otherPitchPanCenter.OtherHandleKey += PitchPanCenterHandleKey;
		otherPitchPanCenter.OtherRedraw += PitchPanCenterDraw;

		thumbBarPitchPanSeparation = new ThumbBarWidget(new Point(54, 46), 9, -32, 32);
		thumbBarPanningSwing = new ThumbBarWidget(new Point(54, 47), 9, 0, 64);

		toggleDefaultPanning.Changed += UpdateValues;
		thumbBarPanningValue.Changed += UpdateValues;
		thumbBarPitchPanSeparation.Changed += UpdateValues;
		thumbBarPanningSwing.Changed += UpdateValues;
	}

	public void ResetCurrentNode(SongInstrument ins)
	{
		_currentEnvelopeNode = Math.Max(0, (ins.PanningEnvelope?.Nodes.Count ?? 0) - 1);
	}

	protected override void EnvelopeDraw()
	{
		bool isSelected = SelectedActiveWidget == otherEnvelope;

		var ins = Song.CurrentSong.GetInstrument(CurrentInstrument);

		DrawEnvelopeLabel("Panning", isSelected);

		EnvelopeDraw(ins.PanningEnvelope, false, _currentEnvelopeNode,
			ins.Flags.HasAllFlags(InstrumentFlags.PanningEnvelope),
			ins.Flags.HasAllFlags(InstrumentFlags.PanningEnvelopeLoop),
			ins.Flags.HasAllFlags(InstrumentFlags.PanningEnvelopeSustain),
			0);
	}

	protected override bool EnvelopeHandleKey(KeyEvent k)
	{
		var ins = Song.CurrentSong.GetInstrument(CurrentInstrument);

		if (EnvelopeHandleMouse(k, ins.PanningEnvelope, ref _currentEnvelopeNode))
		{
			ins.Flags |= InstrumentFlags.PanningEnvelope;
			return true;
		}

		int r;

		if (s_envelopeEditMode)
			r = EnvelopeHandleKeyEditMode(k, ins.PanningEnvelope, ref _currentEnvelopeNode);
		else
			r = EnvelopeHandleKeyViewMode(k, ins.PanningEnvelope, ref _currentEnvelopeNode, InstrumentFlags.PanningEnvelope);

		if (r.HasBitSet(2))
		{
			r ^= 2;
			ins.Flags |= InstrumentFlags.PanningEnvelope;
		}

		return (r != 0);
	}

	/* --------------------------------------------------------------------------------------------------------- */
	/* pitch-pan center */

	bool PitchPanCenterHandleKey(KeyEvent k)
	{
		var ins = Song.CurrentSong.GetInstrument(CurrentInstrument);

		int ppc = ins.PitchPanCenter;

		if (k.State == KeyState.Release)
			return false;
		switch (k.Sym)
		{
			case KeySym.Left:
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return false;
				ppc--;
				break;
			case KeySym.Right:
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return false;
				ppc++;
				break;
			default:
				if (!k.Modifiers.HasAnyFlag(KeyMod.ControlAlt))
				{
					ppc = k.NoteValue;
					if (ppc < 1 || ppc > 120)
						return false;
					ppc--;
					break;
				}
				return false;
		}

		if (ppc != ins.PitchPanCenter
		&& ppc >= 0 && ppc < 120)
		{
			ins.PitchPanCenter = ppc;
			Status.Flags |= StatusFlags.NeedUpdate;
		}

		return true;
	}

	void PitchPanCenterDraw()
	{
		bool isSelected = (SelectedActiveWidget == otherPitchPanCenter);

		var ins = Song.CurrentSong.GetInstrument(CurrentInstrument);

		VGAMem.DrawText(SongNote.GetNoteString(ins.PitchPanCenter + 1), new Point(54, 45), isSelected ? (3, 0) : (2, 0));
	}

	public override void PredrawHook()
	{
		var ins = Song.CurrentSong.GetInstrument(CurrentInstrument);
		var env = ins.PanningEnvelope;

		if (env == null)
			ins.Flags &= ~InstrumentFlags.PanningEnvelope;

		var toggleEnabled = (ToggleWidget)widgetEnvelopeEnabled;

		toggleEnabled.State = ins.Flags.HasAllFlags(InstrumentFlags.PanningEnvelope);
		toggleEnvelopeCarry.State = ins.Flags.HasAllFlags(InstrumentFlags.PanningEnvelopeCarry);
		toggleEnvelopeLoop.State = ins.Flags.HasAllFlags(InstrumentFlags.PanningEnvelopeLoop);
		toggleEnvelopeSustainLoop.State = ins.Flags.HasAllFlags(InstrumentFlags.PanningEnvelopeSustain);

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

		toggleDefaultPanning.State = ins.Flags.HasAllFlags(InstrumentFlags.SetPanning);
		thumbBarPanningValue.Value = ins.Panning >> 2;
		thumbBarPitchPanSeparation.Value = ins.PitchPanSeparation;
		thumbBarPanningSwing.Value = ins.PanningSwing;
	}

	protected override void UpdateValues()
	{
		var ins = Song.CurrentSong.GetInstrument(CurrentInstrument);

		Status.Flags |= StatusFlags.SongNeedsSave;

		ins.Flags &= ~(InstrumentFlags.PanningEnvelope | InstrumentFlags.PanningEnvelopeCarry | InstrumentFlags.PanningEnvelopeLoop | InstrumentFlags.PanningEnvelopeSustain);

		if (((ToggleWidget)widgetEnvelopeEnabled).State)
			ins.Flags |= InstrumentFlags.PanningEnvelope;
		if (toggleEnvelopeCarry.State)
			ins.Flags |= InstrumentFlags.PanningEnvelopeCarry;
		if (toggleEnvelopeLoop.State)
			ins.Flags |= InstrumentFlags.PanningEnvelopeLoop;
		if (toggleEnvelopeSustainLoop.State)
			ins.Flags |= InstrumentFlags.PanningEnvelopeSustain;

		var env = ins.PanningEnvelope;

		if (env != null)
		{
			if (env.LoopStart != numberEntryEnvelopeLoopBegin.Value)
			{
				env.LoopStart = numberEntryEnvelopeLoopBegin.Value;
				ins.Flags |= InstrumentFlags.PanningEnvelopeLoop;
			}

			if (env.LoopEnd != numberEntryEnvelopeLoopEnd.Value)
			{
				env.LoopEnd = numberEntryEnvelopeLoopEnd.Value;
				ins.Flags |= InstrumentFlags.PanningEnvelopeLoop;
			}

			if (env.SustainStart != numberEntryEnvelopeSustainLoopBegin.Value)
			{
				env.SustainStart = numberEntryEnvelopeSustainLoopBegin.Value;
				ins.Flags |= InstrumentFlags.PanningEnvelopeSustain;
			}

			if (env.SustainEnd != numberEntryEnvelopeSustainLoopEnd.Value)
			{
				env.SustainEnd = numberEntryEnvelopeSustainLoopEnd.Value;
				ins.Flags |= InstrumentFlags.PanningEnvelopeSustain;
			}
		}

		/* more ugly shifts */
		int n = thumbBarPanningValue.Value << 2;

		if (ins.Panning != n)
		{
			ins.Panning = n;
			ins.Flags |= InstrumentFlags.SetPanning;
		}

		ins.PitchPanSeparation = thumbBarPitchPanSeparation.Value;
		ins.PanningSwing = thumbBarPanningSwing.Value;

		Song.CurrentSong.UpdatePlayingInstrument(CurrentInstrument);
	}

	protected override string Label => "Panning";

	public override void DrawConst()
	{
		base.DrawConst();

		VGAMem.DrawFillCharacters(new Point(57, 42), new Point(62, 45), (VGAMem.DefaultForeground, 0));

		VGAMem.DrawBox(new Point(53, 41), new Point(63, 48), BoxTypes.Thick | BoxTypes.Inner | BoxTypes.Inset);

		VGAMem.DrawText("Default Pan", new Point(42, 42), (0, 2));
		VGAMem.DrawText("Pan Value", new Point(44, 43), (0, 2));
		VGAMem.DrawText("Pitch-Pan Center", new Point(37, 45), (0, 2));
		VGAMem.DrawText("Pitch-Pan Separation", new Point(33, 46), (0, 2));

		if (Status.Flags.HasAllFlags(StatusFlags.ClassicMode))
		{
			/* Hmm. The 's' in swing isn't capitalised. ;) */
			VGAMem.DrawText("Pan swing", new Point(44, 47), (0, 2));
		}
		else
			VGAMem.DrawText("Pan Swing", new Point(44, 47), (0, 2));

		VGAMem.DrawText("\x9a\x9a\x9a\x9a\x9a\x9a\x9a\x9a\x9a", new Point(54, 44), (2, 0));
	}

	public override bool? PreHandleKey(KeyEvent k)
	{
		var ins = Song.CurrentSong.GetInstrument(CurrentInstrument);

		if (s_envelopeMouseEdit)
		{
			if (EnvelopeHandleMouse(k, ins.PanningEnvelope, ref _currentEnvelopeNode))
			{
				ins.Flags |= InstrumentFlags.PanningEnvelope;
				return true;
			}
		}

		if ((k.Sym == KeySym.l || k.Sym == KeySym.b) && k.Modifiers.HasAnyFlag(KeyMod.Alt))
			return 0 != EnvelopeHandleKeyViewMode(k, ins.PanningEnvelope, ref _currentEnvelopeNode, InstrumentFlags.PanningEnvelope);

		return base.PreHandleKey(k);
	}
}
