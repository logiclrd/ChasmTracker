using ChasmTracker.Songs;
using ChasmTracker.Utility;
using ChasmTracker.VGA;
using ChasmTracker.Widgets;

namespace ChasmTracker.Dialogs;

public class NewSongDialog : Dialog
{
	ToggleButtonWidget? toggleButtonPatternsKeep;
	ToggleButtonWidget? toggleButtonPatternsClear;
	ToggleButtonWidget? toggleButtonSamplesKeep;
	ToggleButtonWidget? toggleButtonSamplesClear;
	ToggleButtonWidget? toggleButtonInstrumentsKeep;
	ToggleButtonWidget? toggleButtonInstrumentsClear;
	ToggleButtonWidget? toggleButtonOrderListKeep;
	ToggleButtonWidget? toggleButtonOrderListClear;
	ButtonWidget? buttonOK;
	ButtonWidget? buttonCancel;

	public NewSongDialog()
		: base(new Point(21, 20), new Size(38, 19))
	{
		ActionYes += OK;
	}

	protected override void Initialize()
	{
		toggleButtonPatternsKeep = new ToggleButtonWidget(new Point(35, 24), "Keep", 2, 0);
		toggleButtonPatternsClear = new ToggleButtonWidget(new Point(45, 24), "Clear", 2, 0);
		toggleButtonSamplesKeep = new ToggleButtonWidget(new Point(35, 27), "Keep", 2, 1);
		toggleButtonSamplesClear = new ToggleButtonWidget(new Point(45, 27), "Clear", 2, 1);
		toggleButtonInstrumentsKeep = new ToggleButtonWidget(new Point(35, 30), "Keep", 2, 2);
		toggleButtonInstrumentsClear = new ToggleButtonWidget(new Point(45, 30), "Clear", 2, 2);
		toggleButtonOrderListKeep = new ToggleButtonWidget(new Point(35, 33), "Keep", 2, 3);
		toggleButtonOrderListClear = new ToggleButtonWidget(new Point(45, 33), "Clear", 2, 3);

		buttonOK = new ButtonWidget(new Point(28, 36), 8, "OK", 4);
		buttonOK.Clicked += DialogButtonYes;

		buttonCancel = new ButtonWidget(new Point(41, 36), 8, "Cancel", 4);
		buttonCancel.Clicked += DialogButtonCancel;

		Widgets.Add(toggleButtonPatternsKeep);
		Widgets.Add(toggleButtonPatternsClear);
		Widgets.Add(toggleButtonSamplesKeep);
		Widgets.Add(toggleButtonSamplesClear);
		Widgets.Add(toggleButtonInstrumentsKeep);
		Widgets.Add(toggleButtonInstrumentsClear);
		Widgets.Add(toggleButtonOrderListKeep);
		Widgets.Add(toggleButtonOrderListClear);
		Widgets.Add(buttonOK);
		Widgets.Add(buttonCancel);

		toggleButtonPatternsClear.SetState(true);
		toggleButtonSamplesClear.SetState(true);
		toggleButtonInstrumentsClear.SetState(true);
		toggleButtonOrderListClear.SetState(true);
	}

	public override void DrawConst()
	{
		VGAMem.DrawText("New Song", new Point(36, 21), (3, 2));
		VGAMem.DrawText("Patterns", new Point(26, 24), (0, 2));
		VGAMem.DrawText("Samples", new Point(27, 27), (0, 2));
		VGAMem.DrawText("Instruments", new Point(23, 30), (0, 2));
		VGAMem.DrawText("Order List", new Point(24, 33), (0, 2));
	}

	void OK(object? data)
	{
		var flags = NewSongFlags.ClearAll;

		if (toggleButtonPatternsKeep!.State)
			flags |= NewSongFlags.KeepPatterns;
		if (toggleButtonSamplesKeep!.State)
			flags |= NewSongFlags.KeepSamples;
		if (toggleButtonInstrumentsKeep!.State)
			flags |= NewSongFlags.KeepInstruments;
		if (toggleButtonOrderListKeep!.State)
			flags |= NewSongFlags.KeepOrderList;

		Song.New(flags);
	}
}
