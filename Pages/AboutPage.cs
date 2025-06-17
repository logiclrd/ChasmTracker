namespace ChasmTracker;

using System;
using System.Collections.Generic;

using ChasmTracker.Dialogs;
using ChasmTracker.Pages;
using ChasmTracker.Songs;
using ChasmTracker.VGA;
using ChasmTracker.Widgets;

public class AboutPage : Page
{
	public AboutPage()
		: base(PageNumbers.About, "", HelpTexts.Copyright)
	{
		_fakeDriver = ((new Random().Next() & 3) != 0) ? 0 : 1;
	}

	int _fakeDriver;
	Overlay _logoOverlay;

	public override void SetPage(VGAMem vgaMem)
	{
		if (_logoOverlay == null)
		{
			string logoFileName =
				Status.Flags.HasFlag(StatusFlags.ClassicMode)
				? "ChasmTracker.ImpulseTrackerLogo.png"
				: "ChasmTracker.LogoBanner.png";

			var logoResourceStream = typeof(AboutPage).Assembly.GetManifestResourceStream(logoFileName);

			if (logoResourceStream != null)
			{
				var logoImage = Image.LoadFrom(logoResourceStream);

				/* this is currently pretty gross */
				uint fg = Status.Flags.HasFlag(StatusFlags.ClassicMode) ? 11u : 0u;
				uint bg = 2u;

				for (int i = 0; i < logoImage.PixelData.Length; i++)
					if (logoImage.PixelData[i] != 0)
						logoImage.PixelData[i] = fg;
					else
						logoImage.PixelData[i] = bg;

				_logoOverlay = new Overlay(23, 17, 58, 24, logoImage);
			}
		}

		var widgets = new List<Widget>();

		widgets.Add(new ButtonWidget(
			position: new Point(32, 32),
			width: 12,
			Dialog.DialogButtonYes,
			"Continue",
			padding: 3));

		var d = Dialog.CreateCustom(
			new Point(11, 16),
			new Size(58, 19),
			widgets.ToArray(),
			0,
			DialogDrawConst,
			default);
			/*
	public static Dialog CreateCustom(Point position, Size size, Widget[] widgets, int selectedWidget, Action<VGAMem> drawConst, object data)
	{
		Dialog d = new Dialog(position, size);

		lock (s_activeDialogs)
			s_activeDialogs.Push(d);

		if (Status.DialogType == DialogTypes.Menu)
			Menu.Hide();

		d.Type = DialogTypes.Custom;
		d.Widgets = widgets.ToList();
		d.SelectedWidget.Value = selectedWidget;
		d.DrawConst = drawConst;

		d.Text = null;
		d.Data = data;
		d.ActionYes = null;
		d.ActionNo = null;
		d.ActionCancel = null;
		d.HandleKey = null;

		Status.DialogType = DialogTypes.Custom;

		Page.ActiveWidgets = d.Widgets;
		Page.SelectedActiveWidget = d.SelectedWidget;

		Status.Flags |= StatusFlags.NeedUpdate;

		return d;
	}
			*/

		d.ActionYes = CloseAbout;
		d.ActionNo = CloseAbout;
		d.ActionCancel = CloseAbout;

		/* okay, in just a moment, we're going to the module page.
		* if your modules dir is large enough, this causes an annoying pause.
		* to defeat this, we start scanning *NOW*. this makes startup "feel"
		* faster.
		*/
		Status.Flags |= StatusFlags.ModulesDirectoryChanged;
		AllPages.ModuleLoad.SetPage(vgaMem);
	}

	void CloseAbout()
	{
		if (Status.CurrentPageNumber == PageNumbers.About)
			SetPage(PageNumbers.ModuleLoad);

		Status.Flags |= StatusFlags.NeedUpdate;
	}

	void DialogDrawConst(VGAMem vgaMem)
	{
		if (Status.CurrentPageNumber == PageNumbers.About)
		{
			/* redraw outer part */
			vgaMem.DrawBox(
				new Point(11, 16),
				new Point(68, 34),
				BoxTypes.Thin | BoxTypes.Outer | BoxTypes.FlatDark);
		}

		if (Status.Flags.HasFlag(StatusFlags.ClassicMode))
		{
			vgaMem.DrawBox(
				new Point(25, 25),
				new Point(56, 30),
				BoxTypes.Thin | BoxTypes.Outer | BoxTypes.FlatDark);

			vgaMem.DrawText("Sound Card Setup", new Point(32, 26), 0, 2);

			if (Song.AudioDriver == "dummy")
				vgaMem.DrawText("No sound card detected", new Point(29, 28), 0, 2);
			else
			{
				switch (_fakeDriver)
				{
					case 0:
						vgaMem.DrawText("Sound Blaster 16 detected", new Point(26, 28), 0, 2);
						vgaMem.DrawText("Port 220h, IRQ 7, DMA 5", new Point(26, 29), 0, 2);
						break;
					case 1:
						/* FIXME: The GUS driver displays the memory settings a bit
						differently from the SB. If we're "supporting" it, we should
						probably keep the rest of the UI consistent with our choice.
						(Also: no love for the AWE cards?)

						Alternately, it would be totally awesome to probe the system
						for the actual name and parameters of the card in use :) */
						vgaMem.DrawText("Gravis UltraSound detected", new Point(26, 28), 0, 2);
						vgaMem.DrawText("Port 240h, IRQ 5, 1024k RAM", new Point(26, 29), 0, 2);
						break;
				}
			}
		}
		else
		{
			string buf = $"Using {Song.AudioDriver} on {Video.DriverName}";

			vgaMem.DrawText(buf, new Point((80 - buf.Length) / 2, 25), 0, 2);

			/* build date */
			string buildLine =
				$"Build {BuildInformation.Commit}{(BuildInformation.IsPristine ? "" : "*")} at {BuildInformation.Timestamp}";

			vgaMem.DrawText(buildLine, new Point(15, 27), 1, 2);
			vgaMem.DrawText(Copyright.ShortCopyright, new Point(15, 29), 1, 2);
			vgaMem.DrawText(Copyright.ShortBasedOn, new Point(15, 30), 1, 2);

			/* XXX if we allow key remapping, need to reflect the *real* help key here */
			vgaMem.DrawText("Press F1 for copyright and full credits", new Point(15, 31), 1, 2);
		}

		vgaMem.ApplyOverlay(_logoOverlay);
	}

	public override bool PreHandleKey(KeyEvent k)
	{
		if ((k.Mouse != MouseState.None) && (k.MousePosition.Y > 20))
			return false;

		switch (k.Sym)
		{
			case KeySym.Left:
			case KeySym.Right:
			case KeySym.Down:
			case KeySym.Up:
			case KeySym.Tab:
			case KeySym.Return:
			case KeySym.Escape:
				/* use default handler */
				return false;
			case KeySym.F2:
			case KeySym.F5:
			case KeySym.F9:
			case KeySym.F10:
				/* Ctrl + these keys does not lead to a new screen. */
				if (k.Modifiers.HasAnyFlag(KeyMod.Control))
					break;
				goto case KeySym.F1;
			// Fall through.
			case KeySym.F1:
			case KeySym.F3:
			case KeySym.F4:
			case KeySym.F11:
			case KeySym.F12:
				// Ignore Alt and so on.
				if (k.Modifiers.HasAnyFlag(KeyMod.Alt | KeyMod.Shift))
					break;

				dialog_destroy();
				return false;
		}

		/* this way, we can't pull up help here */
		return true;
	}

	public override void DrawFull(VGAMem vgaMem)
	{
		vgaMem.DrawFillChars(new Point(0, 0), new Point(79, 49), VGAMem.DefaultForeground, 0);
	}

	public override void OnClosed()
	{
		if (Status.CurrentPageNumber == PageNumbers.About)
			SetPage(PageNumbers.ModuleLoad);

		Status.Flags |= StatusFlags.NeedUpdate;
	}
}
