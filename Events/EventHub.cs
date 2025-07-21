using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using SDL3;

namespace ChasmTracker.Events;

using ChasmTracker.Configurations;
using ChasmTracker.Input;
using ChasmTracker.Interop;
using ChasmTracker.Pages;
using ChasmTracker.Utility;

public static class EventHub
{
	public static KeyMod CurrentKeyMod => TranslateSDLModKey(SDL.GetModState());

	const int EVENTQUEUE_CAPACITY = 128;

	static Queue<Event> s_queue = new Queue<Event>();
	static object s_queueMutex = new object();

	public static bool HaveEvent()
	{
		lock (s_queueMutex)
		{
			if (s_queue.Count > 0)
				return true;

			// try pumping the events.
			PumpEvents();

			if (s_queue.Count > 0)
				return true;

			return false;
		}
	}

	public static void PumpEvents()
	{
		while (SDL.PollEvent(out var e))
		{
			// TODO: controller?
			// # ifdef SCHISM_CONTROLLER
			// if (!sdl3_controller_sdlevent(&e))
			// 	continue;
			// #endif

			switch ((SDL.EventType)e.Type) {
				case SDL.EventType.Quit:
					PushEvent(new QuitEvent());
					break;
				case SDL.EventType.WindowShown:
					PushEvent(new WindowEvent(WindowEventType.Shown));
					break;
				case SDL.EventType.WindowExposed:
					PushEvent(new WindowEvent(WindowEventType.Exposed));
					break;
				case SDL.EventType.WindowFocusLost:
					PushEvent(new WindowEvent(WindowEventType.FocusLost));
					break;
				case SDL.EventType.WindowFocusGained:
					PushEvent(new WindowEvent(WindowEventType.FocusGained));
					break;
				case SDL.EventType.WindowMouseEnter:
					PushEvent(new WindowEvent(WindowEventType.Enter));
					break;
				case SDL.EventType.WindowMouseLeave:
					PushEvent(new WindowEvent(WindowEventType.Leave));
					break;
				case SDL.EventType.WindowResized:
				{
					// the RESIZED event was changed in SDL 3 and now
					// is basically what SDL_WINDOWEVENT_SIZE_CHANGED
					// once was. doesn't matter for us, we handled
					// both events the same anyway.

					PushEvent(
						new WindowEvent(WindowEventType.Resized)
						{
							NewSize = new Size(e.Window.Data1, e.Window.Data2)
						});

					break;
				}
				case SDL.EventType.KeyDown:
				{
					// pop any pending keydowns
					PopPendingKeyDown(null);

					if (e.Key.Key.HasFlag(SDL.Keycode.ExtendedMask))
						break; // I don't know how to handle these

					var chasmEvent = new KeyboardEvent();

					chasmEvent.State = KeyState.Press;
					chasmEvent.IsRepeat = e.Key.Repeat;

					// Chasm's and SDL3's representation of these are the same.
					chasmEvent.Sym = (KeySym)e.Key.Key;
					chasmEvent.ScanCode = (ScanCode)e.Key.Scancode;
					chasmEvent.Modifiers = TranslateSDLModKey(e.Key.Mod); // except this one!

					//if (!sdl3_TextInputActive()) {
					// push it NOW
					//	PushEvent(&schism_event);
					//} else {
					PushPendingKeyDown(chasmEvent);

					break;
				}
				case SDL.EventType.KeyUp:
				{
					// pop any pending keydowns
					PopPendingKeyDown(null);

					if (e.Key.Key.HasFlag(SDL.Keycode.ExtendedMask))
						break; // I don't know how to handle these

					var chasmEvent = new KeyboardEvent();

					chasmEvent.State = KeyState.Release;
					chasmEvent.Sym = (KeySym)e.Key.Key;
					chasmEvent.ScanCode = (ScanCode)e.Key.Scancode;
					chasmEvent.Modifiers = TranslateSDLModKey(e.Key.Mod);

					PushEvent(chasmEvent);

					break;
				}
				case SDL.EventType.TextInput:
				{
					var text = Marshal.PtrToStringUTF8(e.Text.Text) ?? "";

					if (_pendingKeyDown != null)
						PopPendingKeyDown(text);
					else
						PushEvent(new TextInputEvent(text));

					break;
				}
				case SDL.EventType.MouseMotion:
					PushEvent(new MouseMotionEvent((int)e.Motion.X, (int)e.Motion.Y));
					break;
				case SDL.EventType.MouseButtonDown:
				case SDL.EventType.MouseButtonUp:
				{
					MouseButtonEventType eventType;
					MouseButton button = MouseButton.Unknown;

					if ((SDL.EventType)e.Type == SDL.EventType.MouseButtonDown)
						eventType = MouseButtonEventType.Down;
					else
						eventType = MouseButtonEventType.Up;

					switch (e.Button.Button)
					{
						case SDL.ButtonLeft: button = MouseButton.Left; break;
						case SDL.ButtonMiddle: button = MouseButton.Middle; break;
						case SDL.ButtonRight: button = MouseButton.Right; break;
					}

					PushEvent(new MouseButtonEvent(eventType, button, e.Button.Down, e.Button.Clicks, (int)e.Button.X, (int)e.Button.Y));

					break;
				}
				case SDL.EventType.MouseWheel:
					PushEvent(new MouseWheelEvent((int)e.Wheel.X, (int)e.Wheel.Y, (int)e.Wheel.MouseX, (int)e.Wheel.MouseY));
					break;
				case SDL.EventType.DropFile:
					if (Marshal.PtrToStringUTF8(e.Drop.Data) is string filePath)
						PushEvent(new FileDropEvent(filePath));
					break;
				/* these two have no structures because we don't use them */
				case SDL.EventType.AudioDeviceAdded:
				case SDL.EventType.AudioDeviceRemoved:
					PushEvent(new AudioDevicesChangedEvent());
					break;
				default:
					break;
			}
		}

		PopPendingKeyDown(null);
	}

	public static bool PollEvent(out Event? @event)
	{
		lock (s_queueMutex)
		{
			if (s_queue.TryDequeue(out @event))
				return true;

			// try pumping the events.
			PumpEvents();

			if (s_queue.TryDequeue(out @event))
				return true;

			// welp
			return false;
		}
	}

	// An array of event filters. The reason this is used *here*
	// instead of in main is because we need to filter X11 events
	// *as they are pumped*, and putting win32 and macosx events
	// here is just a side effect of that.
	static Func<Event, Event?>[] s_eventFilters;

	static EventHub()
	{
		var filters = new List<Func<Event, Event?>>();

		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			filters.Add(Win32EventFilter);
		if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
			filters.Add(MacOSXEventFilter);

		// TODO: better detection?
		if (Environment.GetEnvironmentVariable("DISPLAY") != null)
			filters.Add(X11EventFilter);

		s_eventFilters = filters.ToArray();
	}

	// implicitly fills in the timestamp
	public static void PushEvent(Event @event)
	{
		@event.Timestamp = DateTime.UtcNow;

		foreach (var filter in s_eventFilters)
		{
			var filteredEvent = filter(@event);

			if (filteredEvent == null)
				return;

			@event = filteredEvent;
		}

		lock (s_queueMutex)
				s_queue.Enqueue(@event);
	}

	static KeyMod TranslateSDLModKey(SDL.Keymod mod)
	{
		KeyMod res = KeyMod.None;

		if (mod.HasFlag(SDL.Keymod.LShift))
			res |= KeyMod.LeftShift;

		if (mod.HasFlag(SDL.Keymod.RShift))
			res |= KeyMod.RightShift;

		if (mod.HasFlag(SDL.Keymod.LCtrl))
			res |= KeyMod.LeftControl;

		if (mod.HasFlag(SDL.Keymod.RCtrl))
			res |= KeyMod.RightControl;

		if (mod.HasFlag(SDL.Keymod.LAlt))
			res |= KeyMod.LeftAlt;

		if (mod.HasFlag(SDL.Keymod.RAlt))
			res |= KeyMod.RightAlt;

		if (mod.HasFlag(SDL.Keymod.LGUI))
			res |= KeyMod.LeftGUI;

		if (mod.HasFlag(SDL.Keymod.RGUI))
			res |= KeyMod.RightGUI;

		if (mod.HasFlag(SDL.Keymod.Num))
			res |= KeyMod.Num;

		if (mod.HasFlag(SDL.Keymod.Caps))
			res |= KeyMod.Caps;

		if (mod.HasFlag(SDL.Keymod.Mode))
			res |= KeyMod.Mode;

		return res;
	}

	static Event? Win32EventFilter(Event @event)
	{
		if (@event is WindowManagerMessageEvent wmMsg)
		{
			if (wmMsg.Subsystem != WMSubsystem.Windows)
				return @event;

			if (wmMsg.Win!.msg == Win32.WM_COMMAND)
			{
				switch (wmMsg.Win.wParam & 0xFFFF)
				{
					case Win32.IDM_FILE_NEW: return new NativeScriptEvent("new");
					case Win32.IDM_FILE_LOAD: return new NativeScriptEvent("load");
					case Win32.IDM_FILE_SAVE_CURRENT: return new NativeScriptEvent("save");
					case Win32.IDM_FILE_SAVE_AS: return new NativeScriptEvent("save_as");
					case Win32.IDM_FILE_EXPORT: return new NativeScriptEvent("export_song");
					case Win32.IDM_FILE_MESSAGE_LOG: return new NativeScriptEvent("logviewer");
					case Win32.IDM_FILE_QUIT: return new QuitEvent();
					case Win32.IDM_PLAYBACK_SHOW_INFOPAGE: return new NativeScriptEvent("info");
					case Win32.IDM_PLAYBACK_PLAY_SONG: return new NativeScriptEvent("play");
					case Win32.IDM_PLAYBACK_PLAY_PATTERN: return new NativeScriptEvent("play_pattern");
					case Win32.IDM_PLAYBACK_PLAY_FROM_ORDER: return new NativeScriptEvent("play_order");
					case Win32.IDM_PLAYBACK_PLAY_FROM_MARK_CURSOR: return new NativeScriptEvent("play_mark");
					case Win32.IDM_PLAYBACK_STOP: return new NativeScriptEvent("stop");
					case Win32.IDM_PLAYBACK_CALCULATE_LENGTH: return new NativeScriptEvent("calc_length");
					case Win32.IDM_SAMPLES_SAMPLE_LIST: return new NativeScriptEvent("sample_page");
					case Win32.IDM_SAMPLES_SAMPLE_LIBRARY: return new NativeScriptEvent("sample_library");
					case Win32.IDM_SAMPLES_RELOAD_SOUNDCARD: return new NativeScriptEvent("init_sound");
					case Win32.IDM_INSTRUMENTS_INSTRUMENT_LIST: return new NativeScriptEvent("inst_page");
					case Win32.IDM_INSTRUMENTS_INSTRUMENT_LIBRARY: return new NativeScriptEvent("inst_library");
					case Win32.IDM_VIEW_HELP: return new NativeScriptEvent("help");
					case Win32.IDM_VIEW_VIEW_PATTERNS: return new NativeScriptEvent("pattern");
					case Win32.IDM_VIEW_ORDERS_PANNING: return new NativeScriptEvent("orders");
					case Win32.IDM_VIEW_VARIABLES: return new NativeScriptEvent("variables");
					case Win32.IDM_VIEW_MESSAGE_EDITOR: return new NativeScriptEvent("message_edit");
					case Win32.IDM_VIEW_TOGGLE_FULLSCREEN: return new NativeScriptEvent("fullscreen");
					case Win32.IDM_SETTINGS_PREFERENCES: return new NativeScriptEvent("preferences");
					case Win32.IDM_SETTINGS_MIDI_CONFIGURATION: return new NativeScriptEvent("midi_config");
					case Win32.IDM_SETTINGS_PALETTE_EDITOR: return new NativeScriptEvent("palette_page");
					case Win32.IDM_SETTINGS_FONT_EDITOR: return new NativeScriptEvent("font_editor");
					case Win32.IDM_SETTINGS_SYSTEM_CONFIGURATION: return new NativeScriptEvent("system_config");
					default:
						return null;
				}
			}
			else if (wmMsg.Win.msg == Win32.WM_DROPFILES)
			{
				/* Drag and drop support */
				IntPtr drop = wmMsg.Win.wParam; // HDROP

				int needed = Win32.DragQueryFileW(drop, 0, null, 0);

				char[] f = new char[needed + 1];

				int actual = Win32.DragQueryFileW(drop, 0, f, needed + 1);

				if (actual == 0)
					return null;

				string filePath = new string(f, 0, actual);

				return new FileDropEvent(filePath);
			}

			return null;
		}
		else if (@event is KeyboardEvent keyEvent)
		{
			// We get bogus keydowns for Ctrl-Pause.
			// As a workaround, we can check what Windows thinks, but only for Right Ctrl.
			// Left Ctrl just gets completely ignored and there's nothing we can do about it.
			if ((keyEvent.Sym == KeySym.ScrollLock) && keyEvent.Modifiers.HasFlag(KeyMod.RightControl) && (0 == (0x80 & Win32.GetKeyState(Win32.VK_SCROLL))))
				keyEvent.Sym = KeySym.Pause;

			return keyEvent;
		}

		return @event;
	}



	static Event? MacOSXEventFilter(Event @event)
	{
		switch (@event)
		{
			case WindowEvent windowEvent:
			{
				if (windowEvent.EventType == WindowEventType.FocusGained)
				{
					MacOSX.GetSetFnKeyMode(MacOSX.FnKeyMode.TheOtherMode);
					return @event;
				}

				if (windowEvent.EventType == WindowEventType.FocusLost)
				{
					MacOSX.RestoreFnKeyMode();
					return @event;
				}

				break;
			}
			case KeyboardEvent keyEvent:
			{
				if (Status.FixNumLockSetting == NumLockHandling.Guess)
				{
					/* why is this checking for ibook_helper? */
					if (MacOSX.FnKeyModeAtStartup != MacOSX.FnKeyMode.Check)
					{
						if ((Page.ActiveWidgets != null)
						 && (Page.SelectedActiveWidgetIndex != null)
						 && (Page.SelectedActiveWidgetIndex >= 0)
						 && (Page.SelectedActiveWidgetIndex < Page.ActiveWidgets.Count)
						 && Page.ActiveWidgets[Page.SelectedActiveWidgetIndex].AcceptsText)
						{
							/* text is more likely? */
							keyEvent.Modifiers |= KeyMod.Num;
						}
						else
						{
							keyEvent.Modifiers &= ~KeyMod.Num;
						}
					}

					/* otherwise honor it */
					/* other cases are handled in schism/main.c */
					break;
				}

				if (keyEvent.ScanCode == ScanCode.KP_Enter)
				{
					/* On portables, the regular Insert key
					 * isn't available. This is equivalent to
					 * pressing Fn-Return, which just so happens
					 * to be a "de facto" Insert in mac land.
					 * However, on external keyboards this causes
					 * a real keypad enter to get eaten by this
					 * function as well. IMO it's more important
					 * for now that portable users can actually
					 * have an Insert key.
					 *
					 *   - paper */
					keyEvent.Sym = KeySym.Insert;
					break;
				}

				return @event;
			}
		}

		return @event;
	}

	static Event? X11EventFilter(Event @event)
	{
		/* The clipboard code in this file was taken from SDL 2,
		 * whose license is included in sys/x11/clippy.c */

		if (!(@event is WindowManagerMessageEvent wmMsg))
			return @event;
		if ((wmMsg.Subsystem != WMSubsystem.X11) || (wmMsg.X11 == null))
			return @event;

		var wmData = Video.GetWMData();

		if ((wmData == null) || (wmData.Subsystem != WMSubsystem.X11))
			return @event; // ???

		wmData.XLock?.Invoke();

		switch (wmMsg.X11.Type)
		{
			case X11EventNames.SelectionNotify:
				// sent when a selection is received from XConvertSelection
				//x11_clippy_selection_waiting = 0;
				break;
			case X11EventNames.SelectionRequest:
			{
				uint XA_TARGETS = X11.XInternAtom(wmData.XDisplay, "TARGETS", false);

				var sevent =
					new XSelectionEvent()
					{
						Type = X11EventNames.SelectionNotify,
						Selection = wmMsg.X11.Selection,
						Target = 0,
						Property = 0,
						Requestor = wmMsg.X11.Requestor,
						Time = wmMsg.X11.Time,
					};

				if (wmMsg.X11.Target == XA_TARGETS)
				{
					var supportedFormats = new List<uint>();

					supportedFormats.Add(XA_TARGETS);

					foreach (var fmt in Enum.GetValues<X11ClipboardMIMETypes>())
						supportedFormats.Add(X11.GetCutBufferExternalFmt(wmData.XDisplay, fmt));

					X11.XChangeProperty(
						wmData.XDisplay,
						wmMsg.X11.Requestor,
						wmMsg.X11.Property,
						X11.XA_ATOM,
						32,
						XPropMode.Replace,
						supportedFormats.ToArray(),
						supportedFormats.Count);

					sevent.Property = wmMsg.X11.Property;
					sevent.Target = XA_TARGETS;
				}
				else
				{
					foreach (var fmt in Enum.GetValues<X11ClipboardMIMETypes>())
					{
						if (X11.GetCutBufferExternalFmt(wmData.XDisplay, fmt) != wmMsg.X11.Target)
							continue;

						var result = X11.XGetWindowProperty(
							wmData.XDisplay,
							X11.DefaultRootWindow(wmData.XDisplay),
							X11.GetCutBufferType(wmData.XDisplay, fmt, wmMsg.X11.Selection),
							0,
							int.MaxValue / 4,
							false,
							X11.GetCutBufferExternalFmt(wmData.XDisplay, fmt),
							out sevent.Target,
							out var selectionFormat,
							out var numBytes,
							out var overflow,
							out var selectionData);

						try
						{
							if (result == XStatus.Success)
							{
								if (selectionFormat != X11.None)
								{
									X11.XChangeProperty(
										wmData.XDisplay,
										wmMsg.X11.Requestor,
										wmMsg.X11.Property,
										wmMsg.X11.Target,
										8,
										XPropMode.Replace,
										selectionData,
										numBytes);

									sevent.Property = wmMsg.X11.Property;
									sevent.Target = wmMsg.X11.Target;

								}
							}
						}
						finally
						{
							X11.XFree(selectionData);
						}
					}
				}

				X11.XSendEvent(wmData.XDisplay, wmMsg.X11.Requestor, false, 0, sevent);
				X11.XSync(wmData.XDisplay, false);

				break;
			}
		}

		wmData.XUnlock?.Invoke();

		return null;
	}

	// Called back by the video backend;
	public static bool Initialize()
	{
		if ((Configuration.Keyboard.KeyboardRepeatDelay > 0)
		 && (Configuration.Keyboard.KeyboardRepeatRate > 0))
		{
			// Override everything.
			Keyboard.SetRepeat(
				Configuration.Keyboard.KeyboardRepeatDelay,
				Configuration.Keyboard.KeyboardRepeatRate);
		}

		if (!SDLLifetime.Initialize())
			return false;

		if (!SDL.InitSubSystem(SDL.InitFlags.Events))
			return false;

		SDL.SetWindowsMessageHook(Win32MsgHook, default);

		return true;
	}

	static Event? _pendingKeyDown;

	static void PushPendingKeyDown(Event @event)
	{
		if (_pendingKeyDown == null)
			_pendingKeyDown = @event;
	}

	static void PopPendingKeyDown(string? text)
	{
		if (_pendingKeyDown is KeyboardEvent keyEvent)
		{
			keyEvent.Text = text;
			EventHub.PushEvent(_pendingKeyDown);
			_pendingKeyDown = null;
		}
	}

	static bool X11MsgHook(IntPtr userdata, IntPtr xevent)
	{
		return false;
	}

	static bool Win32MsgHook(IntPtr userdata, IntPtr msg)
	{
		var e = new WindowManagerMessageEvent();

		e.Subsystem = WMSubsystem.Windows;
		e.Win = new WindowManagerMessageEvent.WinMessage();
		e.Win.hWnd = Marshal.ReadIntPtr(msg, 0);
		e.Win.msg = Marshal.ReadInt32(msg, 8);
		e.Win.wParam = Marshal.ReadIntPtr(msg, 16);
		e.Win.lParam = Marshal.ReadIntPtr(msg, 24);

		// ignore WM_DROPFILES messages. these are already handled
		// by the SDL_DROPFILES event and trying to use our implementation
		// only results in an empty string which is undesirable
		const int WM_DROPFILES = 0x233;

		if (e.Win.msg != WM_DROPFILES)
			EventHub.PushEvent(e);

		return true;
	}

	public static void Quit()
	{
		SDL.QuitSubSystem(SDL.InitFlags.Events);

		SDLLifetime.Quit();
	}
}
