using System;
using System.Collections.Generic;

namespace ChasmTracker.Pages;

using ChasmTracker.Dialogs;
using ChasmTracker.VGA;

public class PatternEditorHistoryDialog : Dialog
{
	/* --------------------------------------------------------------------------------------------------------- */
	/* undo dialog */
	static int s_undoSelection;

	List<PatternSnap> _undoHistory;

	public PatternEditorHistoryDialog(List<PatternSnap> undoHistory)
		: base(new Point(17, 21), new Size(47, 16))
	{
		_undoHistory = undoHistory;

		ActionYes = _ => { };
		ActionCancel = _ => { };
	}

	public Action<int>? RestoreHistory;

	public override void DrawConst()
	{
		VGAMem.DrawText("Undo", new Point(38, 22), (3, 2));
		VGAMem.DrawBox(new Point(19, 23), new Point(60, 34), BoxTypes.Thin | BoxTypes.Inner | BoxTypes.Inset);

		for (int i = 0; i < 10; i++)
		{
			byte fg, bg;

			if (i == s_undoSelection)
			{
				fg = 0;
				bg = 3;
			}
			else
			{
				fg = 2;
				bg = 0;
			}

			VGAMem.DrawCharacter(' ', new Point(20, 24 + i), (fg, bg));

			if ((i < _undoHistory.Count)
			 && (_undoHistory[_undoHistory.Count - i - 1].SnapOp is string snapOp))
				VGAMem.DrawTextLen(snapOp, 39, new Point(21, 24 + i), (fg, bg));
		}
	}

	public override bool HandleKey(KeyEvent k)
	{
		if (k.Modifiers != KeyMod.None)
			return false;

		switch (k.Sym)
		{
			case KeySym.Escape:
				if (k.State == KeyState.Press)
					return false;

				DialogButtonCancel();
				Status.Flags |= StatusFlags.NeedUpdate;

				break;
			case KeySym.Up:
				if (k.State == KeyState.Release)
					return false;

				s_undoSelection--;
				if (s_undoSelection < 0)
					s_undoSelection = 0;

				Status.Flags |= StatusFlags.NeedUpdate;

				return true;
			case KeySym.Down:
				if (k.State == KeyState.Release)
					return false;

				s_undoSelection++;
				if (s_undoSelection >= _undoHistory.Count)
					s_undoSelection = _undoHistory.Count - 1;

				Status.Flags |= StatusFlags.NeedUpdate;

				return true;
			case KeySym.Return:
				if (k.State == KeyState.Release)
					return false;

				RestoreHistory?.Invoke((_undoHistory.Count - s_undoSelection + 1) % _undoHistory.Count);

				DialogButtonCancel();
				Status.Flags |= StatusFlags.NeedUpdate;

				return true;
		}

		return false;
	}
}