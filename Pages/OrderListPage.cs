using System;
using System.Collections.Generic;
using System.Linq;

namespace ChasmTracker.Pages;

using ChasmTracker.Events;
using ChasmTracker.Input;
using ChasmTracker.Playback;
using ChasmTracker.Songs;
using ChasmTracker.Utility;
using ChasmTracker.VGA;
using ChasmTracker.Widgets;

public abstract class OrderListPage : Page
{
	protected static OtherWidget otherOrderList;

	protected static Shared<int> s_commonSelectedWidgetIndex = new Shared<int>();

	static List<Widget> s_commonWidgets = new List<Widget>();

	public OrderListPage(PageNumbers number, string title, HelpTexts helpText)
		: base(number, title, helpText)
	{
	}

	static OrderListPage()
	{
		otherOrderList = new OtherWidget(new Point(6, 15), new Size(3, 32));

		otherOrderList.OtherHandleKey += otherOrderList_HandleKey;
		otherOrderList.OtherHandleText += otherOrderList_HandleText;
		otherOrderList.OtherRedraw += otherOrderList_Redraw;
		otherOrderList.OtherAcceptsText = true;

		s_commonWidgets.Add(otherOrderList);
	}

	public override void SynchronizeWith(Page other)
	{
		base.SynchronizeWith(other);

		AllPages.OrderList = this;

		if (other is InstrumentListPage)
		{
			if (Song.CurrentSong.IsInstrumentMode)
				AllPages.SampleList.SynchronizeToInstrument();
		}
	}

	// These values are shared between the subclasses of OrderListPage, which
	// collectively act as a single page.
	static int s_topOrder;
	static int s_currentOrder;
	static int s_cursorPos;

	public int TopOrder
	{
		get => s_topOrder;
		set => s_topOrder = value;
	}

	public int CurrentOrder
	{
		get => s_currentOrder;
		set
		{
			int newOrder = value;

			newOrder = newOrder.Clamp(0, 255);

			if (s_currentOrder == newOrder)
				return;

			s_currentOrder = newOrder;

			Reposition();

			Status.Flags |= StatusFlags.NeedUpdate;
		}
	}

	static int[] s_savedOrderList = Array.Empty<int>();
	static bool s_didSaveOrderList;

	/* --------------------------------------------------------------------- */

	static void Reposition()
	{
		if (s_currentOrder < s_topOrder)
			s_topOrder = s_currentOrder;
		else if (s_currentOrder > s_topOrder + 31)
			s_topOrder = s_currentOrder - 31;
	}

	/* --------------------------------------------------------------------- */

	public void UpdateCurrentOrder()
	{
		VGAMem.DrawText(CurrentOrder.ToString("d3"), new Point(12, 5), (5, 0));
		VGAMem.DrawText(Song.CurrentSong.GetLastOrder().ToString("d3"), new Point(16, 5), (5, 0));
	}

	static void Cheater()
	{
		if ((s_currentOrder < Song.CurrentSong.OrderList.Count)
			&& (Song.CurrentSong.OrderList[s_currentOrder] != SpecialOrders.Skip)
		 && (Song.CurrentSong.OrderList[s_currentOrder] != SpecialOrders.Last))
			return;

		int cp = AllPages.PatternEditor.CurrentPattern;

		int best = -1;
		int first = -1;

		for (int i = 0; i < 199; i++)
		{
			if ((i >= Song.CurrentSong.Patterns.Count) || Song.CurrentSong.Patterns[i].IsEmpty)
			{
				if (first == -1) first = i;
				if (best == -1) best = i;
			}
			else
			{
				best = -1;
			}
		}

		if (best == -1) best = first;
		if (best == -1) return;

		Status.FlashText($"Pattern {cp} copied to pattern {best}, order {s_currentOrder}");

		Song.CurrentSong.Patterns[best] = Song.CurrentSong.Patterns[cp].Clone();
		Song.CurrentSong.SetOrder(s_currentOrder, best);

		s_currentOrder++;

		Status.Flags |= StatusFlags.SongNeedsSave;
		Status.Flags |= StatusFlags.NeedUpdate;
	}

	/* --------------------------------------------------------------------- */
	/* called from the pattern editor on ctrl-plus/minus */

	public void PreviousOrderPattern()
	{
		int newOrder = CurrentOrder;
		int lastPattern = Song.CurrentSong.OrderList[newOrder];

		do
		{
			if (--newOrder < 0)
			{
				newOrder = 0;
				break;
			}
		} while (!Status.Flags.HasFlag(StatusFlags.ClassicMode)
				&& lastPattern == Song.CurrentSong.OrderList[newOrder]
				&& Song.CurrentSong.OrderList[newOrder] == SpecialOrders.Skip);

		if (Song.CurrentSong.OrderList[newOrder] < 200)
		{
			CurrentOrder = newOrder;
			Reposition();
			AllPages.PatternEditor.CurrentPattern = Song.CurrentSong.OrderList[newOrder];
		}
	}

	public void NextOrderPattern()
	{
		int newOrder = CurrentOrder;
		int lastPattern = Song.CurrentSong.OrderList[newOrder];

		do
		{
			if (++newOrder > 255)
			{
				newOrder = 255;
				break;
			}
		} while (!Status.Flags.HasFlag(StatusFlags.ClassicMode)
				&& lastPattern == Song.CurrentSong.OrderList[newOrder]
				&& Song.CurrentSong.OrderList[newOrder] == SpecialOrders.Skip);

		if (Song.CurrentSong.OrderList[newOrder] < 200)
		{
			CurrentOrder = newOrder;
			Reposition();
			AllPages.PatternEditor.CurrentPattern = Song.CurrentSong.OrderList[newOrder];
		}
	}

	/* --------------------------------------------------------------------- */

	static string GetPatternString(int pattern)
	{
		switch (pattern)
		{
			case SpecialOrders.Skip: return "+++";
			case SpecialOrders.Last: return "---";
			default: return pattern.ToString("d3");
		}
	}

	static void otherOrderList_Redraw()
	{
		// orderlist_draw

		int playingOrder = (AudioPlayback.Mode == AudioPlaybackMode.Playing ? AudioPlayback.CurrentOrder : -1);

		/* draw the list */
		for (int pos = 0, n = s_topOrder; pos < 32; pos++, n++)
		{
			VGAMem.DrawText(n.ToString("d3"), new Point(2, 15 + pos), (n == playingOrder) ? (3, 2) : (0, 2));

			int patternNumber = Song.CurrentSong.GetOrder(n);

			VGAMem.DrawText(GetPatternString(patternNumber), new Point(6, 15 + pos), (2, 0));
		}

		/* draw the cursor */
		if (SelectedActiveWidget == otherOrderList)
		{
			int patternNumber = Song.CurrentSong.GetOrder(s_currentOrder);

			string patternNumberString = GetPatternString(patternNumber);

			int pos = s_currentOrder - s_topOrder;

			VGAMem.DrawCharacter(patternNumberString[0], new Point(s_cursorPos + 6, 15 + pos), (0, 3));
		}

		Status.Flags |= StatusFlags.NeedUpdate;
	}

	/* --------------------------------------------------------------------- */

	static void InsertPosition()
	{
		Song.CurrentSong.OrderList.Insert(s_currentOrder, SpecialOrders.Last);

		if (Song.CurrentSong.OrderList.Count > 255)
			Song.CurrentSong.OrderList.RemoveRange(255, Song.CurrentSong.OrderList.Count - 255);

		Status.Flags |= StatusFlags.NeedUpdate | StatusFlags.SongNeedsSave;
	}

	static void Save()
	{
		s_savedOrderList = Song.CurrentSong.OrderList.ToArray();
		s_didSaveOrderList = true;
	}

	static void Restore()
	{
		if (!s_didSaveOrderList)
			return;

		var oldList = Song.CurrentSong.OrderList.ToArray();

		Song.CurrentSong.OrderList.Clear();
		Song.CurrentSong.OrderList.AddRange(s_savedOrderList);

		s_savedOrderList = oldList;
	}

	static void DeletePosition()
	{
		if (s_currentOrder < Song.CurrentSong.OrderList.Count)
			Song.CurrentSong.OrderList.RemoveAt(s_currentOrder);

		Status.Flags |= StatusFlags.NeedUpdate | StatusFlags.SongNeedsSave;
	}

	static void InsertNext()
	{
		int nextPattern;

		while (s_currentOrder >= Song.CurrentSong.OrderList.Count)
			Song.CurrentSong.OrderList.Add(SpecialOrders.Last);

		if (s_currentOrder == 0 || Song.CurrentSong.OrderList[s_currentOrder - 1] >= Constants.MaxPatterns)
			return;

		nextPattern = Song.CurrentSong.OrderList[s_currentOrder - 1] + 1;
		if (nextPattern > Constants.MaxPatterns)
			nextPattern = Constants.MaxPatterns;
		Song.CurrentSong.SetOrder(s_currentOrder, nextPattern);
		if (s_currentOrder < 255)
			s_currentOrder++;

		Reposition();

		Status.Flags |= StatusFlags.NeedUpdate | StatusFlags.SongNeedsSave;
	}

	static void AddUnusedPatterns()
	{
		/* n0 = the first free order
		* n = orderlist position
		* p = pattern iterator
		* np = number of patterns */
		int np = Song.CurrentSong.GetPatternCount();

		var used = Song.CurrentSong.OrderList.Where(p => p < Constants.MaxPatterns).ToHashSet();

		int n = Song.CurrentSong.OrderList.Count - 1;

		while ((n >= 0) && (Song.CurrentSong.GetOrder(n) == SpecialOrders.Last))
			n--;

		if (n < 0)
			n = 0;
		else
			n += 2;

		int n0 = n;

		for (int p = 0; p < np; p++)
		{
			if (used.Contains(p) || (p >= Song.CurrentSong.Patterns.Count) || Song.CurrentSong.Patterns[p].IsEmpty)
				continue;

			if (n > 255)
			{
				// Status.FlashText("No more room in orderlist");
				break;
			}

			if (n < Song.CurrentSong.OrderList.Count)
				Song.CurrentSong.OrderList[n++] = p;
			else
				Song.CurrentSong.OrderList.Add(p);
		}

		if (n == n0)
			Status.FlashText("No unused patterns");
		else
		{
			AllPages.OrderList.CurrentOrder = n - 1;
			AllPages.OrderList.CurrentOrder = n0;

			if (n - n0 == 1)
				Status.FlashText("1 unused pattern found");
			else
				Status.FlashText((n - n0) + " unused patterns found");
		}

		Status.Flags |= StatusFlags.NeedUpdate | StatusFlags.SongNeedsSave;
	}

	static void Reorder()
	{
		/* err, I hope this is going to be done correctly...
		*/
		var np = new List<Pattern>();
		var mapOrderList = new int[256];

		lock (AudioPlayback.LockScope())
		{
			AddUnusedPatterns();

			for (int i = 0; i < mapOrderList.Length; i++)
				mapOrderList[i] = SpecialOrders.Last;

			for (int i = 0; i < 255; i++)
			{
				int order = Song.CurrentSong.GetOrder(i);
				if (order == SpecialOrders.Last || order == SpecialOrders.Skip)
					continue;

				if (mapOrderList[order] == SpecialOrders.Last)
				{
					np.Add(Song.CurrentSong.GetPattern(order)!);
					mapOrderList[order] = np.Count - 1;
				}
				/* replace orderlist entry */
				Song.CurrentSong.OrderList[i] = mapOrderList[order];
			}

			Song.CurrentSong.Patterns.Clear();
			Song.CurrentSong.Patterns.AddRange(np);

			Status.Flags |= StatusFlags.NeedUpdate | StatusFlags.SongNeedsSave;

			AudioPlayback.StopUnlocked(false);
		}
	}

	static int[] N = new int[3];

	static bool otherOrderList_HandleChar(char sym)
	{
		switch (sym)
		{
			case '+':
				Status.Flags |= StatusFlags.SongNeedsSave;
				Song.CurrentSong.SetOrder(s_currentOrder, SpecialOrders.Skip);
				s_cursorPos = 2;
				break;
			case '.':
			case '-':
				Status.Flags |= StatusFlags.SongNeedsSave;
				Song.CurrentSong.SetOrder(s_currentOrder, SpecialOrders.Last);
				s_cursorPos = 2;
				break;
			default:
				int c;

				if (sym >= '0' && sym <= '9')
					c = sym - '0';
				else
					return false;

				Status.Flags |= StatusFlags.SongNeedsSave;

				int curPattern = Song.CurrentSong.OrderList[s_currentOrder];

				if (curPattern < 200)
				{
					N[0] = curPattern / 100;
					N[1] = curPattern / 10 % 10;
					N[2] = curPattern % 10;
				}

				N[s_cursorPos] = c;
				curPattern = N[0] * 100 + N[1] * 10 + N[2];
				curPattern = curPattern.Clamp(0, Constants.MaxPatterns);

				Song.CurrentSong.GetPattern(curPattern, create: true);
				Song.CurrentSong.SetOrder(s_currentOrder, curPattern);

				break;
		}

		if (s_cursorPos == 2)
		{
			if (s_currentOrder < 255)
				s_currentOrder++;
			s_cursorPos = 0;
			Reposition();
		}
		else
			s_cursorPos++;

		Status.Flags |= StatusFlags.NeedUpdate;

		return true;
	}

	static bool otherOrderList_HandleText(TextInputEvent evt)
	{
		bool success = false;

		foreach (char ch in evt.Text)
			success |= otherOrderList_HandleChar(ch);

		return success;
	}

	static bool otherOrderList_HandleKey(KeyEvent k)
	{
		int prevOrder = s_currentOrder;
		int newOrder = prevOrder;
		int newCursorPos = s_cursorPos;

		if (k.Mouse != MouseState.None)
		{
			if (k.MousePosition.X >= 6 && k.MousePosition.X <= 8 && k.MousePosition.Y >= 15 && k.MousePosition.Y <= 46)
			{
				/* FIXME adjust top_order, not the cursor */
				if (k.Mouse == MouseState.ScrollUp)
					newOrder -= Constants.MouseScrollLines;
				else if (k.Mouse == MouseState.ScrollDown)
					newOrder += Constants.MouseScrollLines;
				else
				{
					if (k.State == KeyState.Press)
						return false;

					newOrder = (k.MousePosition.Y - 15) + s_topOrder;
					AllPages.OrderList.CurrentOrder = newOrder;
					newOrder = s_currentOrder;

					if (Song.CurrentSong.OrderList[s_currentOrder] != SpecialOrders.Last
					&& Song.CurrentSong.OrderList[s_currentOrder] != SpecialOrders.Skip)
					{
						newCursorPos = (k.MousePosition.X - 6);
					}
				}
			}
		}

		switch (k.Sym)
		{
			case KeySym.Backspace:
				if (Status.Flags.HasFlag(StatusFlags.ClassicMode)) return false;
				if (!k.Modifiers.HasAnyFlag(KeyMod.Alt)) return false;
				if (k.State == KeyState.Press)
					return true;
				if (!s_didSaveOrderList) return true;
				Status.FlashText("Restored orderlist");
				Restore();
				return true;

			case KeySym.Return:
			case KeySym.KP_Enter:
				if (Status.Flags.HasFlag(StatusFlags.ClassicMode)) return false;
				if (k.Modifiers.HasAnyFlag(KeyMod.Alt))
				{
					if (k.State == KeyState.Press)
						return true;
					Status.FlashText("Saved orderlist");
					Save();
					return true;
				}
				// else fall through
				goto case KeySym.g;

			case KeySym.g:
			{
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return false;
				if (k.State == KeyState.Press)
					return true;
				int n = Song.CurrentSong.OrderList[newOrder];
				while (n >= 200 && newOrder > 0)
					n = Song.CurrentSong.OrderList[--newOrder];
				if (n < 200)
				{
					AllPages.PatternEditor.CurrentPattern = n;
					SetPage(PageNumbers.PatternEditor);
				}
				return true;
			}
			case KeySym.Tab:
				if (k.Modifiers.HasAnyFlag(KeyMod.Shift))
				{
					if (k.State == KeyState.Release)
						return true;
					ChangeFocusTo(33);
				}
				else
				{
					if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift)) return false;
					if (k.State == KeyState.Release)
						return true;
					ChangeFocusTo(1);
				}
				return true;
			case KeySym.Left:
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return false;
				if (k.State == KeyState.Release)
					return true;
				newCursorPos--;
				break;
			case KeySym.Right:
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return false;
				if (k.State == KeyState.Release)
					return true;
				newCursorPos++;
				break;
			case KeySym.Home:
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return false;
				if (k.State == KeyState.Release)
					return true;
				newOrder = 0;
				break;
			case KeySym.End:
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return false;
				if (k.State == KeyState.Release)
					return true;
				newOrder = Song.CurrentSong.GetLastOrder();
				if (Song.CurrentSong.OrderList[newOrder] != SpecialOrders.Last)
					newOrder++;
				break;
			case KeySym.Up:
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return false;
				if (k.State == KeyState.Release)
					return true;
				newOrder--;
				break;
			case KeySym.Down:
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return false;
				if (k.State == KeyState.Release)
					return true;
				newOrder++;
				break;
			case KeySym.PageUp:
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return false;
				if (k.State == KeyState.Release)
					return true;
				newOrder -= 16;
				break;
			case KeySym.PageDown:
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return false;
				if (k.State == KeyState.Release)
					return true;
				newOrder += 16;
				break;
			case KeySym.Insert:
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return false;
				if (k.State == KeyState.Release)
					return true;
				InsertPosition();
				return true;
			case KeySym.Delete:
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return false;
				if (k.State == KeyState.Release)
					return true;
				DeletePosition();
				return true;
			case KeySym.F7:
				if (!k.Modifiers.HasAnyFlag(KeyMod.Control)) return false;
				/* fall through */
				goto case KeySym.Space;
			case KeySym.Space:
				if (k.State == KeyState.Release)
					return true;
				Song.CurrentSong.SetNextOrder(s_currentOrder);
				Status.FlashText($"Playing order {s_currentOrder} next");
				return true;
			case KeySym.F6:
				if (k.Modifiers.HasAnyFlag(KeyMod.Shift))
				{
					if (k.State == KeyState.Release)
						return true;
					AudioPlayback.StartAtOrder(s_currentOrder, 0);
					return true;
				}
				return false;

			case KeySym.n:
				if (k.Modifiers.HasAnyFlag(KeyMod.Shift))
				{
					if (k.State == KeyState.Press)
						return true;
					Cheater();
					return true;
				}
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return false;
				if (k.State == KeyState.Release)
					return true;
				InsertNext();
				return true;
			case KeySym.c:
			{
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return false;
				if (Status.Flags.HasFlag(StatusFlags.ClassicMode)) return false;
				if (k.State == KeyState.Press)
					return true;
				int p = AllPages.PatternEditor.CurrentPattern;
				int n;
				for (n = s_currentOrder + 1; n < 256; n++)
				{
					if (Song.CurrentSong.OrderList[n] == p)
					{
						newOrder = n;
						break;
					}
				}
				if (n == 256)
				{
					for (n = 0; n < s_currentOrder; n++)
					{
						if (Song.CurrentSong.OrderList[n] == p)
						{
							newOrder = n;
							break;
						}
					}
					if (n == s_currentOrder)
					{
						Status.FlashText($"Pattern {p} not on Order List");
						return true;
					}
				}
				break;
			}
			case KeySym.r:
				if (k.Modifiers.HasAnyFlag(KeyMod.Alt))
				{
					if (k.State == KeyState.Press)
						return true;
					Reorder();
					return true;
				}
				return false;
			case KeySym.u:
				if (k.Modifiers.HasAnyFlag(KeyMod.Alt))
				{
					if (k.State == KeyState.Release)
						return true;
					AddUnusedPatterns();
					return true;
				}
				return false;

			case KeySym.b:
				if (k.Modifiers.HasAnyFlag(KeyMod.Shift))
					return false;
				/* fall through */
				goto case KeySym.o;
			case KeySym.o:
				if (!k.Modifiers.HasAnyFlag(KeyMod.Control))
					return false;
				if (k.State == KeyState.Release)
					return true;
				Song.CurrentSong.PatternToSample(Song.CurrentSong.OrderList[s_currentOrder],
						k.Modifiers.HasAnyFlag(KeyMod.Shift), k.Sym == KeySym.b);
				return true;

			default:
				if (k.Mouse == MouseState.None)
				{
					if (!k.Modifiers.HasAnyFlag(KeyMod.ControlAlt) && !string.IsNullOrEmpty(k.Text))
						otherOrderList_HandleText(k.ToTextInputEvent());

					return false;
				}
				break;
		}

		if (newCursorPos < 0)
			newCursorPos = 2;
		else if (newCursorPos > 2)
			newCursorPos = 0;

		if (newOrder != prevOrder)
			AllPages.OrderList.CurrentOrder = newOrder;
		else if (newCursorPos != s_cursorPos)
			s_cursorPos = newCursorPos;
		else
			return false;

		Status.Flags |= StatusFlags.NeedUpdate;
		return true;
	}

	/* --------------------------------------------------------------------- */

	protected void CommonDrawConst()
	{
		VGAMem.DrawBox(new Point(5, 14), new Point(9, 47), BoxTypes.Thick | BoxTypes.Inner | BoxTypes.Inset);

		VGAMem.DrawBox(new Point(30, 14), new Point(40, 47), BoxTypes.Thick | BoxTypes.Inner | BoxTypes.FlatLight);
		VGAMem.DrawBox(new Point(64, 14), new Point(74, 47), BoxTypes.Thick | BoxTypes.Inner | BoxTypes.FlatLight);

		VGAMem.DrawCharacter(146, new Point(30, 14), (3, 2));
		VGAMem.DrawCharacter(145, new Point(40, 14), (3, 2));

		VGAMem.DrawCharacter(146, new Point(64, 14), (3, 2));
		VGAMem.DrawCharacter(145, new Point(74, 14), (3, 2));
	}

	/* --------------------------------------------------------------------- */

	static int s_lastOrder = -1;

	public override void PlaybackUpdate()
	{
		int order = (AudioPlayback.Mode == AudioPlaybackMode.Stopped) ? -1 : AudioPlayback.CurrentOrder;

		if (order != s_lastOrder)
		{
			s_lastOrder = order;
			Status.Flags |= StatusFlags.NeedUpdate;
		}
	}

	/* --------------------------------------------------------------------- */

	public override bool? HandleKey(KeyEvent k)
	{
		int n = SelectedActiveWidgetIndex?.Value ?? 0;

		if (k.State == KeyState.Release)
			return false;

		if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
			return false;

		switch (k.Sym)
		{
			case KeySym.PageDown:
				n += 8;
				break;
			case KeySym.PageUp:
				n -= 8;
				break;
			default:
				return false;
		}

		n = n.Clamp(1, 64);
		if (SelectedActiveWidgetIndex! != n)
			ChangeFocusTo(n);

		return true;
	}

	public override bool? PreHandleKey(KeyEvent k)
	{
		if (k.Sym == KeySym.F7)
		{
			if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
				return false;

			if (k.State == KeyState.Release)
				return true;

			AllPages.PatternEditor.PlaySongFromMarkOrderPan();

			return true;
		}

		return false;
	}
}
