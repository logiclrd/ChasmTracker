namespace ChasmTracker.Dialogs;

using ChasmTracker.VGA;
using ChasmTracker.Widgets;

public class PatternEditorMultiChannelDialog : Dialog
{
	/* --------------------------------------------------------------------------------------------------------- */
	/* multichannel dialog */
	ToggleWidget[]? toggleChannel;
	ButtonWidget? buttonOK;

	bool[] _channelMulti;

	public bool IsMultiChannelEnabledForChannel(int channel)
		=> toggleChannel![channel].State;

	public PatternEditorMultiChannelDialog(bool[] channelMulti)
		: base(new Point(7, 18), new Size(66, 25))
	{
		_channelMulti = channelMulti;
	}

	protected override void Initialize()
	{
		toggleChannel = new ToggleWidget[64];

		for (int i = 0; i < 64; i++)
		{
			toggleChannel[i] = new ToggleWidget(
				new Point(
					20 + ((i / 16) * 16), /* X */
					22 + (i % 16)));  /* Y */

			toggleChannel[i].Changed += AdvanceChannel;
			toggleChannel[i].State = _channelMulti[i];
		}

		buttonOK = new ButtonWidget(
			new Point(36, 40),
			width: 6,
			"OK",
			3);

		buttonOK.Clicked += DialogButtonYes;

		Widgets.AddRange(toggleChannel);
		Widgets.Add(buttonOK);
	}

	void AdvanceChannel()
	{
		ChangeFocusTo(SelectedWidget + 1);
	}

	public override void DrawConst(VGAMem vgaMem)
	{
		for (int i = 0; i < 64; i++)
		{
			vgaMem.DrawText(
				"Channel " + (i + 1).ToString("d2"),
				new Point(
					9 + ((i / 16) * 16), /* X */
					22 + (i % 16)),  /* Y */
				0, 2);
		}

		for (int i = 0; i < 64; i += 16)
		{
			vgaMem.DrawBox(
				new Point(
					19 + ((i / 16) * 16), /* X */
					21),
				new Point(
					23 + ((i / 16) * 16), /* X */
					38),
				BoxTypes.Thin | BoxTypes.Inner | BoxTypes.Inset);
		}

		vgaMem.DrawText("Multichannel Selection", new Point(29, 19), 3, 2);
	}

	public override bool HandleKey(KeyEvent k)
	{
		if (k.Sym == KeySym.n)
		{
			if (k.Modifiers.HasAnyFlag(KeyMod.Alt) && k.State == KeyState.Press)
				DialogButtonYes();
			else if (!k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift) && k.State == KeyState.Release)
				DialogButtonCancel();

			return true;
		}

		return false;
	}
}
