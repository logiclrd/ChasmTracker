using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ChasmTracker.Pages;

using ChasmTracker.Configurations;
using ChasmTracker.Input;
using ChasmTracker.Pages.InfoWindows;
using ChasmTracker.Playback;
using ChasmTracker.Songs;
using ChasmTracker.Utility;
using ChasmTracker.Widgets;

public class InfoPage : Page, IConfigurable<InfoPageConfiguration>
{
	OtherWidget? otherInfo;

	public InfoPage()
		: base(PageNumbers.Info, "Info Page (F5)", HelpTexts.InfoPage)
	{
		otherInfo = new OtherWidget();

		otherInfo.OtherHandleKey += otherInfo_HandleKey;
		otherInfo.OtherRedraw += otherInfo_Redraw;

		AddWidget(otherInfo);

		InitializeWindows(out WindowTypes, out WindowTypesByID);

		_windows = CreateDefaultWindows();
	}

	bool HandleClick(Point mousePosition)
	{
		if (mousePosition.Y < 13)
			return false; /* NA */

		mousePosition.Y -= 13;

		foreach (var window in _windows)
		{
			if (mousePosition.Y < window.Height)
			{
				window.Click(mousePosition);

				return true;
			}

			mousePosition.Y -= window.Height;
		}

		return false;
	}

	bool otherInfo_HandleKey(KeyEvent k)
	{
		// info_page_handle_key
		if (k.Mouse == MouseState.Click || k.Mouse == MouseState.DoubleClick)
		{
			int p = _selectedChannel;

			var n = HandleClick(k.MousePosition);

			if (k.Mouse == MouseState.DoubleClick)
			{
				if (p == _selectedChannel)
				{
					AllPages.PatternEditor.CurrentChannel = _selectedChannel;

					int order = AudioPlayback.CurrentOrder;

					int currentlyPlayingPatternNumber;

					if (AudioPlayback.Mode == AudioPlaybackMode.Playing)
						currentlyPlayingPatternNumber = Song.CurrentSong.OrderList[order];
					else
						currentlyPlayingPatternNumber = AudioPlayback.PlayingPattern;

					if (currentlyPlayingPatternNumber < Constants.MaxPatterns)
					{
						AllPages.OrderList.CurrentOrder = order;
						AllPages.PatternEditor.CurrentPattern = currentlyPlayingPatternNumber;
						AllPages.PatternEditor.CurrentRow = AudioPlayback.CurrentRow;

						SetPage(PageNumbers.PatternEditor);
					}
				}
			}

			return n;
		}

		/* hack to render this useful :) */
		if (k.Sym == KeySym.KP_9)
		{
			k.Sym = KeySym.F9;
		}
		else if (k.Sym == KeySym.KP_0)
		{
			k.Sym = KeySym.F10;
		}

		switch (k.Sym)
		{
			case KeySym.g:
				if (k.State == KeyState.Press)
					return true;

				AllPages.PatternEditor.CurrentChannel = _selectedChannel;

				int order = AudioPlayback.CurrentOrder;

				int currentlyPlayingPatternNumber;

				if (AudioPlayback.Mode == AudioPlaybackMode.Playing)
					currentlyPlayingPatternNumber = Song.CurrentSong.OrderList[order];
				else
					currentlyPlayingPatternNumber = AudioPlayback.PlayingPattern;

				if (currentlyPlayingPatternNumber < Constants.MaxPatterns)
				{
					AllPages.OrderList.CurrentOrder = order;
					AllPages.PatternEditor.CurrentPattern = currentlyPlayingPatternNumber;
					AllPages.PatternEditor.CurrentRow = AudioPlayback.CurrentRow;

					SetPage(PageNumbers.PatternEditor);
				}

				return true;

			case KeySym.v:
				if (k.State == KeyState.Release)
					return true;

				_velocityMode.Value = !_velocityMode;
				Status.FlashText($"Using {(_velocityMode ? "velocity" : "volume")} bars");
				Status.Flags |= StatusFlags.NeedUpdate;
				return true;
			case KeySym.i:
				if (k.State == KeyState.Release)
					return true;

				_instrumentNames.Value = !_instrumentNames;
				Status.FlashText($"Using {(_instrumentNames ? "instrument" : "sample")} names");
				Status.Flags |= StatusFlags.NeedUpdate;
				return true;
			case KeySym.r:
				if (k.Modifiers.HasAnyFlag(KeyMod.Alt))
				{
					if (k.State == KeyState.Release)
						return true;

					AudioPlayback.FlipStereo();
					Status.FlashText("Left/right outputs reversed");
					return true;
				}
				return false;
			case KeySym.Equals:
				if (!k.Modifiers.HasAnyFlag(KeyMod.Shift))
					return false;
				goto case KeySym.Plus;
			case KeySym.Plus:
				if (k.State == KeyState.Release)
					return true;
				if (AudioPlayback.Mode == AudioPlaybackMode.Playing)
					AudioPlayback.CurrentOrder++;
				return true;
			case KeySym.Minus:
				if (k.State == KeyState.Release)
					return true;
				if (AudioPlayback.Mode == AudioPlaybackMode.Playing)
					AudioPlayback.CurrentOrder--;
				return true;
			case KeySym.q:
				if (k.State == KeyState.Release)
					return true;
				Song.CurrentSong.ToggleChannelMute(_selectedChannel - 1);
				AllPages.OrderListPanning.RecheckMutedChannels();
				Status.Flags |= StatusFlags.NeedUpdate;
				return true;
			case KeySym.s:
				if (k.State == KeyState.Release)
					return true;

				if (k.Modifiers.HasAnyFlag(KeyMod.Alt))
				{
					Song.CurrentSong.ToggleStereo();
					Status.FlashText($"Stereo {(Song.CurrentSong.IsStereo ? "Enabled" : "Disabled")}");
				}
				else
				{
					Song.CurrentSong.HandleChannelSolo(_selectedChannel - 1);
					AllPages.OrderListPanning.RecheckMutedChannels();
				}
				Status.Flags |= StatusFlags.NeedUpdate;
				return true;
			case KeySym.Space:
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return false;

				if (k.State == KeyState.Release)
					return true;
				Song.CurrentSong.ToggleChannelMute(_selectedChannel - 1);
				if (_selectedChannel < 64)
					_selectedChannel.Value++;
				AllPages.OrderListPanning.RecheckMutedChannels();
				break;
			case KeySym.Up:
				if (k.State == KeyState.Release)
					return true;
				if (k.Modifiers.HasAnyFlag(KeyMod.Alt))
				{
					/* make the current window one line shorter, and give the line to the next window
					below it. if the window is already as small as it can get (3 lines) or if it's
					the last window, don't do anything. */
					if (_selectedWindow == _windows.Count - 1 || _windows[_selectedWindow].Height == 3)
						return true;

					_windows[_selectedWindow].Height--;
					_windows[_selectedWindow + 1].Height++;
					break;
				}
				if (_selectedChannel > 1)
					_selectedChannel.Value--;
				break;
			case KeySym.Left:
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift) && !(k.Modifiers.HasAnyFlag(KeyMod.Alt)))
					return false;
				if (k.State == KeyState.Release)
					return true;
				if (_selectedChannel > 1)
					_selectedChannel.Value--;
				break;
			case KeySym.Down:
				if (k.State == KeyState.Release)
					return true;
				if (k.Modifiers.HasAnyFlag(KeyMod.Alt))
				{
					/* expand the current window, taking a line from
					* the next window down. BUT: don't do anything if
					* (a) this is the last window, or (b) the next
					* window is already as small as it can be (three
					* lines). */
					if (_selectedWindow == _windows.Count - 1
							|| _windows[_selectedWindow + 1].Height == 3)
					{
						return true;
					}
					_windows[_selectedWindow].Height++;
					_windows[_selectedWindow + 1].Height--;
					break;
				}
				if (_selectedChannel < 64)
					_selectedChannel.Value++;
				break;
			case KeySym.Right:
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift) && !(k.Modifiers.HasAnyFlag(KeyMod.Alt)))
					return false;
				if (k.State == KeyState.Release)
					return true;
				if (_selectedChannel < 64)
					_selectedChannel.Value++;
				break;
			case KeySym.Home:
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return false;
				if (k.State == KeyState.Release)
					return true;
				_selectedChannel.Value = 1;
				break;
			case KeySym.End:
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return false;
				if (k.State == KeyState.Release)
					return true;
				_selectedChannel.Value = Song.CurrentSong.FindLastChannel();
				break;
			case KeySym.Insert:
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return false;
				if (k.State == KeyState.Release)
					return true;
				/* add a new window, unless there's already five (the maximum)
				or if the current window isn't big enough to split in half. */
				if (_windows.Count == MaxWindows || (_windows[_selectedWindow].Height < 6))
					return true;

				int n = _windows[_selectedWindow].Height;

				/* split the height between the two windows */
				int m = n / 2;

				n -= m;

				/* shift the windows under the current one down */
				_windows[_selectedWindow].Height = n;
				_windows.Insert(_selectedWindow, WindowTypesByID["samples"](m));

				break;
			case KeySym.Delete:
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return false;
				if (k.State == KeyState.Release)
					return true;
				/* delete the current window and give the extra space to the next window down.
				if this is the only window, well then don't delete it ;) */
				if (_windows.Count == 1)
					return true;

				int combinedHeight = _windows[_selectedWindow].Height;

				if (_selectedWindow + 1 < _windows.Count)
					combinedHeight += _windows[_selectedWindow + 1].Height;
				else
					combinedHeight += _windows[_selectedWindow - 1].Height;

				/* shift the windows under the current one up */
				_windows.RemoveAt(_selectedWindow);

				if (_selectedWindow >= _windows.Count)
					_selectedWindow--;

				/* fix the current window's height */
				_windows[_selectedWindow].Height = combinedHeight;

				break;
			case KeySym.PageUp:
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return false;
				if (k.State == KeyState.Release)
					return true;
				_windows[_selectedWindow] = _windows[_selectedWindow].ConvertToPreviousWindowType();
				break;
			case KeySym.PageDown:
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return false;
				if (k.State == KeyState.Release)
					return true;
				_windows[_selectedWindow] = _windows[_selectedWindow].ConvertToNextWindowType();
				break;
			case KeySym.Tab:
				if (k.State == KeyState.Release)
					return true;
				if (k.Modifiers.HasAnyFlag(KeyMod.Shift))
				{
					if (_selectedWindow == 0)
						_selectedWindow = _windows.Count;
					_selectedWindow--;
				}
				else
				{
					_selectedWindow = (_selectedWindow + 1) % _windows.Count;
				}
				Status.Flags |= StatusFlags.NeedUpdate;
				return true;
			case KeySym.F9:
				if (k.State == KeyState.Release)
					return true;
				if (k.Modifiers.HasAnyFlag(KeyMod.Alt))
				{
					Song.CurrentSong.ToggleChannelMute(_selectedChannel - 1);
					AllPages.OrderListPanning.RecheckMutedChannels();
					return true;
				}
				return false;
			case KeySym.F10:
				if (k.Modifiers.HasAnyFlag(KeyMod.Alt))
				{
					if (k.State == KeyState.Release)
						return true;
					Song.CurrentSong.HandleChannelSolo(_selectedChannel - 1);
					AllPages.OrderListPanning.RecheckMutedChannels();
					return true;
				}
				return false;
			default:
				return false;
		}

		RecalculateWindows();
		Status.Flags |= StatusFlags.NeedUpdate;
		return true;
	}

	void otherInfo_Redraw()
	{
		// info_page_redraw
		int pos = _windows[0].UsesFirstRow ? 13 : 12;

		for (int n = 0; n < _windows.Count - 1; n++)
		{
			bool extraRow = (pos == 12);

			int height = _windows[n].Height + (extraRow ? 1 : 0);

			_windows[n].Draw(pos, height, n == _selectedWindow);

			pos += height;
		}

		/* the last window takes up all the rest of the screen */
		_windows.Last().Draw(pos, 50 - pos, _selectedWindow == _windows.Count - 1);
	}

	/* true => use velocity bars */
	Shared<bool> _velocityMode = new Shared<bool>(false);

	/* true => instrument names */
	Shared<bool> _instrumentNames = new Shared<bool>(false);

	/* --------------------------------------------------------------------- */
	/* window setup */

	int _selectedWindow = 0;
	Shared<int> _selectedChannel = new Shared<int>(1);

	/* five, because that's Impulse Tracker's maximum */
	const int MaxWindows = 5;

	Func<int, int, InfoWindow>[] WindowTypes;
	Dictionary<string, Func<int, InfoWindow>> WindowTypesByID;

	List<InfoWindow> _windows;

	void InitializeWindows(out Func<int, int, InfoWindow>[] windowTypes, out Dictionary<string, Func<int, InfoWindow>> windowTypesByID)
	{
		/* --------------------------------------------------------------------- */
		/* declarations of the window types (factories) */
		windowTypes =
			new Func<int, int, InfoWindow>[]
			{
				(windowType, height) => new SamplesInfoWindow(windowType, _selectedChannel, height, 1, _velocityMode, _instrumentNames),
				(windowType, height) => new TrackView13Window(windowType, _selectedChannel, height, 1),
				(windowType, height) => new TrackView8Window(windowType, _selectedChannel, height, 1),
				(windowType, height) => new TrackView7Window(windowType, _selectedChannel, height, 1),
				(windowType, height) => new TrackView6Window(windowType, _selectedChannel, height, 1),
				(windowType, height) => new TrackView3WideWindow(windowType, _selectedChannel, height, 1),
				(windowType, height) => new TrackView3NarrowWindow(windowType, _selectedChannel, height, 1),
				(windowType, height) => new TrackView2Window(windowType, _selectedChannel, height, 1),
				(windowType, height) => new TrackView1Window(windowType, _selectedChannel, height, 1),
				(windowType, height) => new ActiveChannelsWindow(windowType, _selectedChannel, height, 1),
				(windowType, height) => new NoteDotsWindow(windowType, _selectedChannel, height, 1, _velocityMode),
				(windowType, height) => new TechnicalInfoWindow(windowType, _selectedChannel, height, 1),
			};

		windowTypesByID = new Dictionary<string, Func<int, InfoWindow>>();

		for (int i = 0; i < windowTypes.Length; i++)
		{
			var factory = windowTypes[i];
			var prototype = factory(i, 0);

			windowTypesByID[prototype.ConfigurationID] =
				height => factory(i, height);
		}
	}

	List<InfoWindow> CreateDefaultWindows()
	{
		return
			new List<InfoWindow>()
			{
				/* Samples */ WindowTypesByID["samples"](19),
				/* Active Channels */ WindowTypesByID["global"](3),
				/* Tracks (3-column with separator) */ WindowTypesByID["track5"](15),
			};
	}

	void FixChannels(int n)
	{
		var w = _windows[n];

		int channels = w.GetNumChannels();

		if (channels == 0)
			return;

		/* crappy hack (to squeeze in an extra row on the top window) */
		if (w.UsesFirstRow)
			channels++;

		if (_selectedChannel < w.FirstChannel)
			w.FirstChannel = _selectedChannel;
		else if (_selectedChannel >= (w.FirstChannel + channels))
			w.FirstChannel = _selectedChannel - channels + 1;
		w.FirstChannel = w.FirstChannel.Clamp(1, Constants.MaxChannels - channels + 1);
	}

	void RecalculateWindows()
	{
		int pos = 13;

		for (int n = 0; n < _windows.Count - 1; n++)
		{
			FixChannels(n);

			pos += _windows[n].Height;

			if (pos > 50)
			{
				/* Too big? Throw out the rest of the windows. */
				_windows.RemoveRange(n, _windows.Count - n);
				break;
			}
		}

		Assert.IsTrue(_windows.Count > 0, "_windows.Count > 0", "Should always have at least one window.");

		_windows[_windows.Count - 1].Height = 50 - pos;

		FixChannels(_windows.Count - 1);
	}

	/* --------------------------------------------------------------------------------------------------------- */
	/* settings */

	public void SaveConfiguration(InfoPageConfiguration config)
	{
		var builder = new StringBuilder();

		foreach (var window in _windows)
		{
			if (builder.Length > 0)
				builder.Append(' ');

			builder.Append(window.ConfigurationID).Append(' ').Append(window.Height);
		}

		config.Layout = builder.ToString();
	}

	void LoadConfigurationOld(InfoPageConfiguration config)
	{
		_windows = new List<InfoWindow>();

		for (int i = 0; i < config.NumWindows; i++)
		{
			int packed =
				i switch
				{
					0 => config.Window1,
					1 => config.Window2,
					2 => config.Window3,
					3 => config.Window4,
					4 => config.Window5,
					_ => -1
				};

			if (packed < 0)
			{
				_windows.Clear();
				break;
			}

			int type = packed >> 8;
			int height = packed & 0xff;

			if (type >= 2)
			{
				// compensate for added 8-character columns view
				type++;
			}

			if ((type < 0) || (type >= WindowTypes.Length) || (height < 3))
			{
				/* Broken window? */
				_windows.Clear();
				break;
			}

			_windows.Add(WindowTypes[type](type, height));
		}

		/* last window's size < 3 lines? */

		if (_windows.Count == 0)
			_windows = CreateDefaultWindows();

		RecalculateWindows();

		if (Status.CurrentPageNumber == PageNumbers.Info)
			Status.Flags |= StatusFlags.NeedUpdate;
	}

	static readonly char[] ConfigurationDelimeters = { ' ', '\t' };

	public void LoadConfiguration(InfoPageConfiguration config)
	{
		if (string.IsNullOrEmpty(config.Layout))
		{
			LoadConfigurationOld(config);
			return;
		}

		_windows = new List<InfoWindow>();

		string[] tokens = config.Layout.Split(ConfigurationDelimeters, StringSplitOptions.RemoveEmptyEntries);

		for (int i = 0; i < tokens.Length; i += 2)
		{
			string id = tokens[i];

			if (!WindowTypesByID.TryGetValue(id, out var factory))
				break;

			int height = 3;

			if ((i + 1 < tokens.Length) && int.TryParse(tokens[i + 1], out int configuredHeight))
				height = configuredHeight;

			_windows.Add(factory(height));
		}

		RecalculateWindows();

		if (Status.CurrentPageNumber == PageNumbers.Info)
			Status.Flags |= StatusFlags.NeedUpdate;
	}

	public override void PlaybackUpdate()
	{
		if (AudioPlayback.Mode != AudioPlaybackMode.Stopped)
			Status.Flags |= StatusFlags.NeedUpdate;
	}
}
