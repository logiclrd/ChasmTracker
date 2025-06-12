namespace ChasmTracker;

using ChasmTracker.VGA;

/* It's lo-og, lo-og, it's big, it's heavy, it's wood!
 * It's lo-og, lo-og, it's better than bad, it's good! */

public class LogPage : Page
{
	int _topLine = 0;

	public LogPage()
		: base(PageNumbers.Log, "Message Log Viewer (Ctrl-F11)", HelpTexts.Copyright /* I guess */)
	{
	}

	public override void DrawConst(VGAMem vgaMem)
	{
		vgaMem.DrawBox(new Point(1, 12), new Point(78, 48), BoxTypes.Thick | BoxTypes.Inner | BoxTypes.Inset);
		vgaMem.DrawFillChars(new Point(2, 13), new Point(77, 47), VGAMem.DefaultForeground, 0);
	}

	public override bool HandleKey(KeyEvent k)
	{
		switch (k.Sym)
		{
			case KeySym.Up:
				if (k.State == KeyState.Release)
					return true;
				_topLine--;
				break;
			case KeySym.PageUp:
				if (k.State == KeyState.Release)
					return true;
				_topLine -= 15;
				break;
			case KeySym.Down:
				if (k.State == KeyState.Release)
					return true;
				_topLine++;
				break;
			case KeySym.PageDown:
				if (k.State == KeyState.Release)
					return true;
				_topLine += 15;
				break;
			case KeySym.Home:
				if (k.State == KeyState.Release)
					return true;
				_topLine = 0;
				break;
			case KeySym.End:
				if (k.State == KeyState.Release)
					return true;
				_topLine = Log.Lines.Count;
				break;
			default:
				if (k.State == KeyState.Press)
				{
					if (k.Mouse == MouseState.ScrollUp)
					{
						_topLine -= Constants.MouseScrollLines;
						break;
					}
					else if (k.Mouse == MouseState.ScrollDown)
					{
						_topLine += Constants.MouseScrollLines;
						break;
					}
				}

				return false;
		}

		if (_topLine > Log.Lines.Count - 32)
			_topLine = Log.Lines.Count - 32;
		if (_topLine < 0)
			_topLine = 0;

		Status.Flags |= StatusFlags.NeedUpdate;
		return true;
	}

	public override void Redraw(VGAMem vgaMem)
	{
		for (int n = 0, i = _topLine; i < Log.Lines.Count && n < 33; n++)
		{
			var line = Log.Lines[i];

			if (line.BIOSFont)
				vgaMem.DrawTextBIOSLen(line.Text, 74, new Point(3, 14 + n), line.Colour, 0);
			else
				vgaMem.DrawTextLen(line.Text, 74, new Point(3, 14 + n), line.Colour, 0);
		}
	}
}
