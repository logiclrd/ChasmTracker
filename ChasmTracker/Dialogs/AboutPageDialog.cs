using System;

namespace ChasmTracker.Dialogs;

using ChasmTracker.Input;
using ChasmTracker.Pages;
using ChasmTracker.Playback;
using ChasmTracker.Utility;
using ChasmTracker.VGA;
using ChasmTracker.Widgets;

public class AboutPageDialog : Dialog
{
	/* --------------------------------------------------------------------------------------------------------- */
	/* multichannel dialog */
	ButtonWidget? buttonContinue;

	int _fakeDriver;

	static VGAMemOverlay s_logoImage;
	static Image s_logo;

	static AboutPageDialog()
	{
		Point topLeft, bottomRight;

		if (Status.Flags.HasAllFlags(StatusFlags.ClassicMode))
		{
			topLeft = new Point(23, 17);
			bottomRight = new Point(58, 24);
		}
		else
		{
			topLeft = new Point(12, 13);
			bottomRight = new Point(67, 24);
		}

		s_logoImage = VGAMem.AllocateOverlay(topLeft, bottomRight);

		string logoFileName =
			Status.Flags.HasAllFlags(StatusFlags.ClassicMode)
			? "ChasmTracker.ImpulseTrackerLogo.png"
			: "ChasmTracker.LogoBanner.png";

		var logoResourceStream = typeof(AboutPage).Assembly.GetManifestResourceStream(logoFileName);

		if (logoResourceStream != null)
			s_logo = Image.LoadFrom(logoResourceStream);
		else
			s_logo = new Image(new Size(0, 0));
	}

	public AboutPageDialog()
		: base(
			new Point(11, Status.Flags.HasAllFlags(StatusFlags.ClassicMode) ? 16 : 12),
			new Size(58, Status.Flags.HasAllFlags(StatusFlags.ClassicMode) ? 20 : 24))
	{
		_fakeDriver = ((new Random().Next() & 3) != 0) ? 0 : 1;

		/* this is currently pretty gross */
		byte fg = Status.Flags.HasAllFlags(StatusFlags.ClassicMode) ? (byte)11 : (byte)0;
		byte bg = 2;

		for (int y = 0; y < s_logo.Size.Height; y++)
		{
			for (int x = 0; x < s_logo.Size.Width; x++)
			{
				if (s_logo[x, y] != 0)
					s_logoImage[x, y] = fg;
				else
					s_logoImage[x, y] = bg;
			}

			s_logoImage[s_logo.Size.Width, y+6] = 2;
			s_logoImage[s_logo.Size.Width+1, y+6] = 2;
		}
	}

	protected override void Initialize()
	{
		buttonContinue = new ButtonWidget(
			position: new Point(33, 33),
			width: 12,
			DialogButtonYes,
			"Continue",
			padding: 3);

		AddWidget(buttonContinue);
	}

	public override void DrawConst()
	{
		if (Status.CurrentPageNumber == PageNumbers.About)
		{
			/* redraw outer part */
			//VGAMem.DrawBox(
			//	new Point(11, 16),
			//	new Point(68, 35),
			//	BoxTypes.Thin | BoxTypes.Outer | BoxTypes.FlatDark);
		}

		if (Status.Flags.HasAllFlags(StatusFlags.ClassicMode))
		{
			VGAMem.DrawBox(
				new Point(25, 25),
				new Point(56, 30),
				BoxTypes.Thin | BoxTypes.Outer | BoxTypes.FlatDark);

			VGAMem.DrawText("Sound Card Setup", new Point(32, 26), (0, 2));

			if (AudioPlayback.AudioDriver == "dummy")
				VGAMem.DrawText("No sound card detected", new Point(29, 28), (0, 2));
			else
			{
				switch (_fakeDriver)
				{
					case 0:
						VGAMem.DrawText("Sound Blaster 16 detected", new Point(26, 28), (0, 2));
						VGAMem.DrawText("Port 220h, IRQ 7, DMA 5", new Point(26, 29), (0, 2));
						break;
					case 1:
						/* FIXME: The GUS driver displays the memory settings a bit
						differently from the SB. If we're "supporting" it, we should
						probably keep the rest of the UI consistent with our choice.
						(Also: no love for the AWE cards?)

						Alternately, it would be totally awesome to probe the system
						for the actual name and parameters of the card in use :) */
						VGAMem.DrawText("Gravis UltraSound detected", new Point(26, 28), (0, 2));
						VGAMem.DrawText("Port 240h, IRQ 5, 1024k RAM", new Point(26, 29), (0, 2));
						break;
				}
			}
		}
		else
		{
			string buf = $"Using {AudioPlayback.AudioDriver} on {Video.DriverName}";

			VGAMem.DrawText(buf, new Point((80 - buf.Length) / 2, 25), (0, 2));

			/* build date */
			string buildLine =
				$"{BuildInformation.Commit.Substring(0, 8)}{(BuildInformation.IsPristine ? "" : "*")} {BuildInformation.Timestamp}";

			VGAMem.DrawText(buildLine, new Point(15, 27), (1, 2));
			VGAMem.DrawText(Copyright.ShortCopyright, new Point(15, 29), (1, 2));
			VGAMem.DrawText(Copyright.ShortBasedOn, new Point(15, 30), (1, 2));

			/* XXX if we allow key remapping, need to reflect the *real* help key here */
			VGAMem.DrawText("Press F1 for copyright and full credits", new Point(15, 31), (1, 2));
		}

		VGAMem.ApplyOverlay(s_logoImage);
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
