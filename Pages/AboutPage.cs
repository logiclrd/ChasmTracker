namespace ChasmTracker;

using ChasmTracker.Dialogs;
using ChasmTracker.Input;
using ChasmTracker.Pages;
using ChasmTracker.Utility;
using ChasmTracker.VGA;

public class AboutPage : Page
{
	public AboutPage()
		: base(PageNumbers.About, "", HelpTexts.Copyright)
	{
	}

	public override void SetPage()
	{
		var d = Dialog.Show<AboutPageDialog>();

		d.ActionYes = CloseAbout;
		d.ActionNo = CloseAbout;
		d.ActionCancel = CloseAbout;

		/* okay, in just a moment, we're going to the module page.
		 * if your modules dir is large enough, this causes an annoying pause.
		 * to defeat this, we start scanning *NOW*. this makes startup "feel"
		 * faster.
		 */
		Status.Flags |= StatusFlags.ModulesDirectoryChanged;
		AllPages.ModuleLoad.SetPage();
	}

	void CloseAbout(object? data)
	{
		if (Status.CurrentPageNumber == PageNumbers.About)
			SetPage(PageNumbers.ModuleLoad);

		Status.Flags |= StatusFlags.NeedUpdate;
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

				Dialog.Destroy();
				return false;
		}

		/* this way, we can't pull up help here */
		return true;
	}

	public override void DrawFull()
	{
		VGAMem.DrawFillCharacters(new Point(0, 0), new Point(79, 49), (VGAMem.DefaultForeground, 0));
	}
}
