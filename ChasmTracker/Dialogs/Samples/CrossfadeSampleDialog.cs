using System;

namespace ChasmTracker.Dialogs.Samples;

using ChasmTracker.Songs;
using ChasmTracker.Utility;
using ChasmTracker.VGA;
using ChasmTracker.Widgets;

public class CrossfadeSampleDialog : Dialog
{
	ToggleButtonWidget? toggleButtonLoop;
	ToggleButtonWidget? toggleButtonSustain;
	NumberEntryWidget? numberEntrySamplesToFade;
	ThumbBarWidget? thumbBarPriority;
	ButtonWidget? buttonCancel;
	ButtonWidget? buttonOK;

	public bool SustainLoop => toggleButtonSustain!.State;
	public int SamplesToFade => numberEntrySamplesToFade!.Value;
	public int Priority => thumbBarPriority!.Value;

	SongSample _sample;

	public CrossfadeSampleDialog(SongSample sample)
		: base(new Point(26, 20), new Size(28, 17))
	{
		_sample = sample;
	}

	const int LoopGroup = 1;

	protected override void Initialize()
	{
		// Sample Loop/Sustain Loop
		// FIXME the buttons for loop/sustain ought to be disabled when their respective loops are not valid
		toggleButtonLoop = new ToggleButtonWidget(new Point(31, 24), "Loop", 2, LoopGroup);
		toggleButtonSustain = new ToggleButtonWidget(new Point(41, 24), "Sustain", 1, LoopGroup);

		toggleButtonLoop.Changed += LoopChanged;
		toggleButtonSustain.Changed += LoopChanged;

		// Default to sustain loop if there is a sustain loop but no regular loop, or the regular loop is not valid
		// (Note that a loop that starts at 0 is not valid, because crossfading requires data before the loop.)
		if (_sample.Flags.HasAllFlags(SampleFlags.SustainLoop) && !(_sample.Flags.HasAllFlags(SampleFlags.Loop) && (_sample.LoopStart > 0) && (_sample.LoopEnd > 0)))
			toggleButtonSustain.SetState(true);
		else
			toggleButtonLoop.SetState(true);

		/* Samples To Fade; the max and value are initialized separately, since these
		 * can be (and likely are) different between the regular and sustain loop. */
		numberEntrySamplesToFade = new NumberEntryWidget(new Point(45, 27), 7, 0, 1, new Shared<int>());

		LoopChanged();

		// Priority
		thumbBarPriority = new ThumbBarWidget(new Point(28, 31), 20, -50, 50);

		// Cancel/OK
		buttonCancel = new ButtonWidget(new Point(31, 34), 6, "Cancel", 1);
		buttonOK = new ButtonWidget(new Point(41, 34), 6, "OK", 3);

		buttonCancel.Clicked += DialogButtonCancel;
		buttonOK.Clicked += DialogButtonYes;

		AddWidget(toggleButtonLoop);
		AddWidget(toggleButtonSustain);
		AddWidget(numberEntrySamplesToFade);
		AddWidget(thumbBarPriority);
		AddWidget(buttonCancel);
		AddWidget(buttonOK);
	}

	public override void DrawConst()
	{
		VGAMem.DrawText("Crossfade Sample", new Point(32, 22), (3, 2));
		VGAMem.DrawText("Samples To Fade", new Point(28, 27), (0, 2));
		VGAMem.DrawText("Volume", new Point(28, 29), (0, 2));
		VGAMem.DrawText("Power", new Point(47, 29), (0, 2));
		VGAMem.DrawBox(new Point(27, 30), new Point(48, 32), BoxTypes.Thin | BoxTypes.Inner | BoxTypes.Inset);

		VGAMem.DrawBox(new Point(44, 26), new Point(52, 28), BoxTypes.Thick | BoxTypes.Inner | BoxTypes.Inset);
	}

	// update the sample loop widget range based on loop/susloop data
	void LoopChanged()
	{
		bool sustain = toggleButtonSustain!.State;

		int loopStart = sustain ? _sample.SustainStart : _sample.LoopStart;
		int loopEnd = sustain ? _sample.SustainEnd : _sample.LoopEnd;

		int max = Math.Min(loopEnd - loopStart, loopStart);

		numberEntrySamplesToFade!.Maximum = max;
		numberEntrySamplesToFade.Value = max;
	}
}
