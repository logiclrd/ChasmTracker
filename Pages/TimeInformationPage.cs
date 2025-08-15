using System;

namespace ChasmTracker.Pages;

using ChasmTracker.Configurations;
using ChasmTracker.Input;
using ChasmTracker.Songs;
using ChasmTracker.Utility;
using ChasmTracker.VGA;
using ChasmTracker.Widgets;

public class TimeInformationPage : Page
{
	OtherWidget otherPage;

	bool _displaySession;

	// list
	int _topLine;

	public TimeInformationPage()
		: base(PageNumbers.TimeInformation, "Time Information", HelpTexts.TimeInformation)
	{
		otherPage = new OtherWidget();

		otherPage.OtherHandleKey += otherPage_HandleKey;
		otherPage.OtherRedraw += otherPage_Redraw;
	}

	bool otherPage_HandleKey(KeyEvent k)
	{
		switch (k.Sym)
		{
			case KeySym.Backquote:
				if (k.State != KeyState.Release)
					return false;

				if (k.Modifiers.HasAllFlags(KeyMod.RightAlt) && k.Modifiers.HasAllFlags(KeyMod.RightShift))
				{
					_displaySession = !_displaySession;
					Status.Flags |= StatusFlags.NeedUpdate;
					return true;
				}
				return false;
			case KeySym.s:
				if (k.State != KeyState.Release)
					return false;

				if (!Status.Flags.HasAllFlags(StatusFlags.ClassicMode))
				{
					_displaySession = !_displaySession;
					Status.Flags |= StatusFlags.NeedUpdate;
					return true;
				}

				return false;
			default:
				break;
		}

		if (_displaySession)
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
					_topLine = Song.CurrentSong.History.Count;
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

			_topLine = Math.Min(_topLine, Song.CurrentSong.History.Count);
			_topLine = Math.Max(_topLine, 0);

			Status.Flags |= StatusFlags.NeedUpdate;

			return true;
		}
		else
			return false;
	}

	static readonly TimeSpan MaxTime = new TimeSpan(999, 59, 59);

	void DrawTime(TimeSpan time, Point position)
	{
		if (time < TimeSpan.Zero)
			time = TimeSpan.Zero;
		if (time > MaxTime)
			time = MaxTime;

		var buf = time.TotalHours.ToString().PadLeft(4) + time.ToString(":mm:ss");

		int amt = buf.Length.Clamp(0, 64).Clamp(0, 80 - position.X);

		VGAMem.DrawTextLen(buf, amt, position, (0, 2));
	}

	void otherPage_Redraw()
	{
		var totalTime = TimeSpan.Zero;

		{
			// Module time
			var sum = TimeSpan.Zero;

			foreach (var entry in Song.CurrentSong.History)
				sum += entry.Runtime;

			DrawTime(sum, new Point(18, 13));

			totalTime += sum;
		}

		{
			// Current session
			var runtime = DateTime.UtcNow - Song.CurrentSong.EditStart.Time;

			DrawTime(runtime, new Point(18, 14));
			totalTime += runtime;
		}

		DrawTime(totalTime, new Point(18, 16));

		// draw the bar
		for (int x = 1; x < 79; x++)
			VGAMem.DrawCharacter(154, new Point(x, 18), (0, 2));

		if (_displaySession)
		{
			var sessionTime = TimeSpan.Zero;

			for (int i = 0; i < Song.CurrentSong.History.Count; i++)
			{
				var entry = Song.CurrentSong.History[i];

				sessionTime += entry.Runtime;

				if (i >= _topLine && i < _topLine + 29)
				{
					if (entry.TimeValid)
					{
						string buf = entry.Time.ToString(Configuration.General.DateFormat.GetFormatString());
						VGAMem.DrawTextLen(buf, 27, new Point(4, 20 + i - _topLine), (0, 2));

						buf = entry.Time.ToString(Configuration.General.TimeFormat.GetFormatString());
						VGAMem.DrawTextLen(buf, 27, new Point(29, 20 + i - _topLine), (0, 2));
					}
					else
						VGAMem.DrawText("<unknown date>", new Point(4, 20 + i - _topLine), (0, 2));

					DrawTime(entry.Runtime, new Point(44, 20 + i - _topLine));
					DrawTime(sessionTime, new Point(64, 20 + i - _topLine));
				}
			}
		}
	}

	public override void DrawConst()
	{
		VGAMem.DrawText("Module time:", new Point(6, 13), (0, 2));
		VGAMem.DrawText("Current session:", new Point(2, 14), (0, 2));
		VGAMem.DrawText("Total time:", new Point(7, 16), (0, 2));
	}

	public override void SetPage()
	{
		// reset this
		_displaySession = false;
	}
}
