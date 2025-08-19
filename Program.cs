using System;
using System.IO;
using System.Threading;

namespace ChasmTracker;

using ChasmTracker.Audio;
using ChasmTracker.Clipboard;
using ChasmTracker.Configurations;
using ChasmTracker.Dialogs;
using ChasmTracker.DiskOutput;
using ChasmTracker.Events;
using ChasmTracker.FileSystem;
using ChasmTracker.Input;
using ChasmTracker.Memory;
using ChasmTracker.Menus;
using ChasmTracker.MIDI;
using ChasmTracker.Pages;
using ChasmTracker.Playback;
using ChasmTracker.Songs;
using ChasmTracker.Utility;
using ChasmTracker.VGA;

public class Program
{
	public static readonly DateTime StartTimeUTC = DateTime.UtcNow;

	//VideoDriver s_videoDriver;
	//AudioDriver s_audioDriver;
	//AudioDevice s_audioDevice;

	static CommandLineArguments? s_args;
	static ShutdownFlags s_shutdownProcess;

	static void EventLoop()
	{
		Point lastPosition = default; /* character */
		DateTime lastMouseDown, lastAudioPoll;
		DateTime startDown;
		bool downTrip;
		NumLockHandling fixNumLockKey;
		bool screensaver;
		MouseButton button = MouseButton.Unknown;
		KeyEvent kk = new KeyEvent();

		bool keyboardFocus = false;

		fixNumLockKey = Status.FixNumLockSetting;

		downTrip = false;
		lastMouseDown = DateTime.MinValue;
		lastAudioPoll = DateTime.UtcNow;
		startDown = DateTime.MinValue;
		Status.LastKeySym = KeySym.None;
		Status.LastOriginalKeySym = KeySym.None;

		Status.KeyMod = EventHub.CurrentKeyMod;

		OS.GetModKey(ref Status.KeyMod);

		Video.ToggleScreenSaver(true);
		screensaver = true;

		Status.Now = DateTime.UtcNow;

		while (true)
		{
			while (EventHub.PollEvent(out var se))
			{
				if (se == null)
					continue;

				if (MIDIEngine.HandleEvent(se))
					continue;

				kk.Reset(kk.StartPosition);

				if (se is IButtonPressEvent buttonPress)
					kk.State = buttonPress.KeyState;

				if ((se is KeyboardEvent) || (se is TextInputEvent))
				{
					if ((se is KeyboardEvent keyboardEvent) && (keyboardEvent.Sym == KeySym.None))
					{
						// XXX when does this happen?
						kk.Mouse = MouseState.None;
						kk.IsRepeat = false;
					}
				}

				switch (se)
				{
					case QuitEvent:
						ShowExitPrompt();
						break;
					case AudioDevicesChangedEvent:
						AudioBackend.RefreshAudioDeviceList();
						Status.Flags |= StatusFlags.NeedUpdate;
						break;
					case TextInputEvent textEvent:
						Page.MainHandleTextInput(textEvent.Text);
						break;
					case KeyboardEvent keyboardEvent:
						if (keyboardEvent.IsPressEvent)
						{
							if (keyboardEvent.IsRepeat)
							{
								if (Keyboard.RepeatEnabled)
									break;

								kk.IsRepeat = true;
							}
						}

						kk.Sym = keyboardEvent.Sym;
						kk.ScanCode = keyboardEvent.ScanCode;

						kk.Mouse = MouseState.None;

						// normalize the text for processing
						kk.Text = keyboardEvent.Text;

						// grab the keymod
						kk.Modifiers = keyboardEvent.Modifiers;
						// fix it
						OS.GetModKey(ref kk.Modifiers);

						// numlock hacks, mostly here because of macs
						switch (fixNumLockKey)
						{
							case NumLockHandling.Guess:
								/* should be handled per OS */
								break;
							case NumLockHandling.AlwaysOff:
								Status.KeyMod &= ~KeyMod.Num;
								break;
							case NumLockHandling.AlwaysOn:
								Status.KeyMod |= KeyMod.Num;
								break;
						}

						// copy the keymod into the status global
						Status.KeyMod = kk.Modifiers;

						// apply translations for different keyboard layouts,
						// e.g. shift-8 -> asterisk on standard U.S.
						kk.TranslateKey();

						// airball
						Page.MainHandleKey(kk);

						if (keyboardEvent.IsReleaseEvent)
						{
							/* only empty the key repeat if
								* the last keydown is the same sym */
							if ((Status.LastOriginalKeySym == kk.OriginalSym) || kk.IsModifierKey)
								Keyboard.EmptyKeyRepeat();
						}
						else
						{
							Keyboard.CacheKeyRepeat(kk);

							if (!kk.IsModifierKey)
							{
								Status.LastKeySym = kk.Sym;
								Status.LastOriginalKeySym = kk.OriginalSym;
								Status.LastKeyMod = kk.Modifiers;
							}
						}

						break;
					case MouseEvent mouseEvent:
						if (kk.State == KeyState.Press)
						{
							Status.KeyMod = EventHub.CurrentKeyMod;
							OS.GetModKey(ref Status.KeyMod);
						}

						kk.Sym = KeySym.None;
						kk.Modifiers = KeyMod.None;

						kk.MousePositionFine = Video.Translate(mouseEvent.Position);

						switch (mouseEvent)
						{
							case MouseWheelEvent mouseWheelEvent:
								kk.State = KeyState.Unknown;
								kk.Mouse = (mouseWheelEvent.WheelDelta.Y > 0) ? MouseState.ScrollUp : MouseState.ScrollDown;
								break;
							case MouseMotionEvent:
								kk.State = KeyState.Drag;
								break;
							case MouseButtonEvent mouseButton:
								switch (mouseButton.EventType)
								{
									case MouseButtonEventType.Down:
										// we also have to update the current button
										if (mouseButton.Button == MouseButton.Left)
										{
											/* macosx cruft: Ctrl-LeftClick = RightClick */
											button = Status.KeyMod.HasAnyFlag(KeyMod.Control) ? MouseButton.Right
												: Status.KeyMod.HasAnyFlag(KeyMod.Alt | KeyMod.GUI) ? MouseButton.Middle
												: MouseButton.Left;
										}
										break;
									case MouseButtonEventType.Up:
										button = mouseButton.Button;
										break;
								}
								break;
						}

						bool captureStartPosition = false;
						bool isMouseReleaseEvent = false;

						if ((mouseEvent is MouseButtonEvent mouseButtonEvent) && mouseButtonEvent.IsPressEvent)
						{
							if (mouseButtonEvent.IsReleaseEvent)
								isMouseReleaseEvent = true;
							else
							{
								// we also have to update the current button
								if (Status.KeyMod.HasAnyFlag(KeyMod.Control)
								 || (mouseButtonEvent.Button == MouseButton.Right))
									button = MouseButton.Right;
								else if (Status.KeyMod.HasAnyFlag(KeyMod.Alt | KeyMod.GUI)
								 || (mouseButtonEvent.Button == MouseButton.Middle))
									button = MouseButton.Middle;
								else
									button = MouseButton.Left;

								captureStartPosition = true;
							}
						}

						/* character resolution */
						kk.MousePosition = kk.MousePositionFine / kk.CharacterResolution;
						/* half-character selection */
						kk.MouseXHalfCharacter = (kk.MousePositionFine.X * 2 / kk.CharacterResolution.X) & 1;

						if (se is MouseWheelEvent)
						{
							Page.MainHandleKey(kk);
							break; /* nothing else to do here */
						}

						if (captureStartPosition)
							kk.StartPosition = kk.MousePosition;

						// what?
						startDown = default;

						if (button != MouseButton.Unknown)
						{
							kk.MouseButton = button;

							if (kk.State == KeyState.Release)
							{
								var ticker = DateTime.UtcNow;

								if ((lastPosition == kk.MousePosition) && ((ticker - lastMouseDown).TotalMilliseconds < 300))
								{
									lastMouseDown = default;
									kk.Mouse = MouseState.DoubleClick;
								}
								else
								{
									lastMouseDown = ticker;
									kk.Mouse = MouseState.Click;
								}

								lastPosition = kk.MousePosition;

								// dirty hack
								button = MouseButton.Unknown;
							}
							else
								kk.Mouse = MouseState.Click;

							if (Status.DialogType == DialogTypes.None)
							{
								if (kk.MousePosition.Y <= 9 && (Status.CurrentPage is not FontEditorPage))
								{
									if ((kk.State == KeyState.Release) && (kk.MouseButton == MouseButton.Right))
									{
										Menu.Show();
										break;
									}

									if ((kk.State == KeyState.Press) && (kk.MouseButton == MouseButton.Left))
										startDown = DateTime.UtcNow;
								}
							}

							kk.OnTarget = Page.ActiveWidgetContext?.ChangeFocusToXY(kk.MousePosition) ?? false;

							if (isMouseReleaseEvent && downTrip)
							{
								downTrip = false;
								break;
							}

							//if (se is not MouseMotionEvent)
							Page.MainHandleKey(kk);
						}

						break;

					case WindowEvent windowEvent:
						/* this logic sucks, but it does what should probably be considered the "right" thing to do */
						switch (windowEvent.EventType)
						{
							case WindowEventType.FocusGained:
								keyboardFocus = true;
								goto case WindowEventType.Shown;

							case WindowEventType.Shown:
								Video.SetMouseCursor(MouseCursorMode.ResetState);
								break;

							case WindowEventType.Enter:
								if (keyboardFocus)
									Video.SetMouseCursor(MouseCursorMode.ResetState);
								break;

							case WindowEventType.FocusLost:
								keyboardFocus = false;
								goto case WindowEventType.Leave;

							case WindowEventType.Leave:
								Video.ShowCursor(true);
								break;

							case WindowEventType.SizeChanged: /* tiling window managers */
								Video.Resize(windowEvent.NewSize);
								goto case WindowEventType.Exposed;

							case WindowEventType.Exposed:
								Status.Flags |= StatusFlags.NeedUpdate;
								break;
						}

						break;
					case FileDropEvent fileDropEvent:
						Dialog.Destroy();

						switch (Status.CurrentPage)
						{
							case SampleListPage:
							case SampleLoadPage:
							case SampleLibraryPage:
								Song.CurrentSong?.LoadSample(AllPages.SampleList.CurrentSample, fileDropEvent.FilePath);
								MemoryUsage.NotifySongChanged();
								Status.Flags |= StatusFlags.SongNeedsSave;
								Page.SetPage(PageNumbers.SampleList);
								break;
							case InstrumentListGeneralSubpage:
							case InstrumentListVolumeSubpage:
							case InstrumentListPanningSubpage:
							case InstrumentListPitchSubpage:
							case InstrumentLoadPage:
							case InstrumentLibraryPage:
								Song.CurrentSong?.LoadInstrumentWithPrompt(AllPages.InstrumentList.CurrentInstrument, fileDropEvent.FilePath);
								MemoryUsage.NotifySongChanged();
								Status.Flags |= StatusFlags.SongNeedsSave;
								Page.SetPage(PageNumbers.SampleList);
								break;
							default:
								Song.Load(fileDropEvent.FilePath);
								break;
						}

						break;

					case UpdateIPMIDIEvent:
						Status.Flags |= StatusFlags.NeedUpdate;
						MIDIEngine.PollPorts();
						break;

					case PlaybackEvent:
						/* this is the sound thread */
						MIDIEngine.SendFlush();
						if (!Status.Flags.HasAnyFlag(StatusFlags.DiskWriterActive | StatusFlags.DiskWriterActiveForPattern))
							Page.MainPlaybackUpdate();
						break;

					case ClipboardPasteEvent clipboardPasteEvent:
						/* handle clipboard events */
						if (Page.DoClipboardPaste(clipboardPasteEvent))
							break;

						Page.MainHandleTextInput(clipboardPasteEvent.Clipboard.ToStringZ());

						break;

					case NativeOpenEvent openEvent: /* open song */
						Song.Load(openEvent.FilePath);
						break;

					case NativeScriptEvent scriptEvent:
						/* destroy any active dialog before changing pages */
						Dialog.Destroy();

						switch (scriptEvent.Which)
						{
							case "new": Dialog.Show<NewSongDialog>(); break;
							case "save": AllPages.ModuleSave.SaveSongOrSaveAs(); break;
							case "save_as": Page.SetPage(PageNumbers.ModuleSave); break;
							case "export_song": Page.SetPage(PageNumbers.ModuleExport); break;
							case "logviewer": Page.SetPage(PageNumbers.Log); break;
							case "font_editor": Page.SetPage(PageNumbers.FontEditor); break;
							case "load": Page.SetPage(PageNumbers.ModuleLoad); break;
							case "help": Page.SetPage(PageNumbers.Help); break;
							case "pattern": Page.SetPage(PageNumbers.PatternEditor); break;
							case "orders": Page.SetPage(PageNumbers.OrderList); break;
							case "variables": Page.SetPage(PageNumbers.SongVariables); break;
							case "message_edit": Page.SetPage(PageNumbers.Message); break;
							case "info": Page.SetPage(PageNumbers.Info); break;
							case "play": AudioPlayback.Start(); break;
							case "play_pattern": Song.CurrentSong?.LoopPattern(AllPages.PatternEditor.CurrentPattern, 0); break;
							case "play_order": AudioPlayback.StartAtOrder(AllPages.OrderList.CurrentOrder, 0); break;
							case "play_mark": AllPages.PatternEditor.PlaySongFromMark(); break;
							case "stop": AudioPlayback.Stop(); break;
							case "calc_length": Page.ShowSongLength(); break;
							case "sample_page": Page.SetPage(PageNumbers.SampleList); break;
							case "sample_library": Page.SetPage(PageNumbers.SampleLibrary); break;
							case "init_sound": /* does nothing :) */ break;
							case "inst_page": Page.SetPage(PageNumbers.InstrumentList); break;
							case "inst_library": Page.SetPage(PageNumbers.InstrumentLibrary); break;
							case "preferences": Page.SetPage(PageNumbers.Preferences); break;
							case "system_config": Page.SetPage(PageNumbers.Config); break;
							case "midi_config": Page.SetPage(PageNumbers.MIDI); break;
							case "palette_page": Page.SetPage(PageNumbers.PaletteEditor); break;
							case "fullscreen": Video.ToggleDisplayFullscreen(); break;
						}

						break;
				}
			}

			/* handle key repeats */
			Keyboard.HandleKeyRepeat();

			/* now we can do whatever we need to do */
			Status.Now = DateTime.UtcNow;

			if ((Status.DialogType == DialogTypes.None)
			 && (startDown != default)
			 && (Status.Now - startDown > TimeSpan.FromSeconds(1)))
			{
				Menu.Show();
				startDown = default;
				downTrip = true;
			}

			if (Status.Flags.HasAnyFlag(StatusFlags.ClippyPasteSelection | StatusFlags.ClippyPasteBuffer))
			{
				Clippy.Paste(Status.Flags.HasAllFlags(StatusFlags.ClippyPasteBuffer)
					? ClippySource.Buffer : ClippySource.Select);
				Status.Flags &= ~(StatusFlags.ClippyPasteSelection | StatusFlags.ClippyPasteBuffer);
			}

			Video.CheckUpdate();

			switch (AudioPlayback.Mode)
			{
				case AudioPlaybackMode.Playing:
				case AudioPlaybackMode.PatternLoop:
					if (screensaver)
					{
						Video.ToggleScreenSaver(false);
						screensaver = false;
					}
					break;
				default:
					if (!screensaver)
					{
						Video.ToggleScreenSaver(true);
						screensaver = true;
					}
					break;
			}
			;

			// Check for new audio devices every 5 seconds.
			if (Status.Now - lastAudioPoll > TimeSpan.FromMilliseconds(5000))
			{
				AudioBackend.RefreshAudioDeviceList();
				Status.Flags |= StatusFlags.NeedUpdate;
				lastAudioPoll = DateTime.UtcNow;
			}

			if (Status.Flags.HasAllFlags(StatusFlags.DiskWriterActive))
			{
				var q = DiskWriter.Sync();

				while (q == SyncResult.More && !EventHub.HaveEvent())
				{
					Video.CheckUpdate();
					q = DiskWriter.Sync();
				}

				if (q == SyncResult.Done)
				{
					Hooks.DiskWriterOutputComplete();

					if (s_args?.DiskwriteTo != null)
					{
						Console.WriteLine("Diskwrite complete, exiting...\n");
						Exit(0);
					}
				}
			}

			/* completely arbitrary; I think this is a good amount of files
			 * to let dmoz work through before trying to reload the page.
			 *
			 * Ideally dmoz would do this by itself, but it's stuck here for now. */
			for (int i = 0; i < 512 && DirectoryScanner.TakeAsynchronousFileListStep(); i++)
				;

			/* let dmoz build directory lists, etc
			 *
			 * as long as there's no user-event going on... */
			while (!Status.Flags.HasAllFlags(StatusFlags.NeedUpdate) && DirectoryScanner.TakeAsynchronousFileListStep() && !EventHub.HaveEvent())
				;

			/* sleep for a little bit to not hog CPU time */
			if (!EventHub.HaveEvent())
				Thread.Sleep(5);
		}
	}

	public static void Exit(int x)
	{
		if (s_shutdownProcess.HasAllFlags(ShutdownFlags.RunHook))
			Hooks.Exit();

		if (s_shutdownProcess.HasAllFlags(ShutdownFlags.SaveConfiguration))
			Configuration.Save();

		if (s_shutdownProcess.HasAllFlags(ShutdownFlags.SDLQuit))
		{
			Video.Refresh();

			Video.Blit();
			Video.ShutDown();
			/*
			Don't use this function as atexit handler, because that will cause
			segfault when MESA runs on Wayland or KMS/DRM: Never call SDL_Quit()
			inside an atexit handler.
			You're probably still on X11 if this has not bitten you yet.
			See long-standing bug: https://github.com/libsdl-org/SDL/issues/3184
				/ Vanfanel
			*/
		}

		using (AudioPlayback.LockScope())
			AudioPlayback.StopUnlocked(true);

		MIDIEngine.Stop();

		AudioBackend.Current?.Quit();
		Clippy.Quit();
		EventHub.Quit();
		Timer.Quit();

		Environment.Exit(x);
	}

	static int Main(string[] args)
	{
		FFT.Initialize();

		Version.Initialize();

		s_args = new CommandLineArguments(); /* shouldn't this be like, first? */

		if (s_args.StartupFlags.HasAllFlags(StartupFlags.Headless))
			Status.Flags |= StatusFlags.Headless;

		/* Eh. */
		Log.Append(false, 3, Version.GetBanner(classic: false));
		Log.AppendNewLine();

		Configuration.InitializeDirectory();

		if (s_args.StartupFlags.HasAllFlags(StartupFlags.Hooks))
		{
			Hooks.Startup();
			s_shutdownProcess |= ShutdownFlags.RunHook;
		}

		Timer.Initialize();

		Song.Initialize();

		Configuration.Load();

		if (!Clippy.Initialize())
		{
			Log.Append(4, "Failed to initialize a clipboard backend!");
			Log.Append(4, "Copying to the system clipboard will not work properly!");
			Log.AppendNewLine();
		}

		if (s_args.ClassicModeSpecified)
		{
			Status.Flags &= ~StatusFlags.ClassicMode;

			if (s_args.StartupFlags.HasAllFlags(StartupFlags.Classic))
				Status.Flags |= StatusFlags.ClassicMode;
		}

		if (!s_args.StartupFlags.HasAllFlags(StartupFlags.Network))
			Status.Flags |= StatusFlags.NoNetwork;

		s_shutdownProcess |= ShutdownFlags.SaveConfiguration;
		s_shutdownProcess |= ShutdownFlags.SDLQuit;

		if (s_args.StartupFlags.HasAllFlags(StartupFlags.Headless))
		{
			if (s_args.DiskwriteTo == null)
			{
				Console.Error.WriteLine("Error: --headless requires --diskwrite");
				return 1;
			}
			if (s_args.InitialSong == null)
			{
				Console.Error.WriteLine("Error: --headless requires an input song file");
				return 1;
			}

			// Initialize modplug only
			AudioPlayback.InitializeModPlug();

			// Load and export song
			if (Song.LoadUnchecked(s_args.InitialSong) is Song initialSong)
			{
				int multiOffset = s_args.DiskwriteTo.IndexOf("%c", StringComparison.InvariantCultureIgnoreCase);

				string driver = s_args.DiskwriteTo.EndsWith(".aif", StringComparison.InvariantCultureIgnoreCase)
					? "AIFF"
					: "WAV";

				if (multiOffset >= 0)
					driver = "M" + driver;

				if (Song.CurrentSong.ExportSong(s_args.DiskwriteTo, driver) != SaveResult.Success)
					Exit(1);

				// Wait for diskwrite to complete
				while (Status.Flags.HasAllFlags(StatusFlags.DiskWriterActive))
				{
					var q = DiskWriter.Sync();

					if (q == SyncResult.Done)
						break;
					else if (q != SyncResult.More)
					{
						Console.Error.WriteLine("Error: Diskwrite failed");
						Exit(1);
					}
				}

				Exit(0);
			}
			else
			{
				Console.Error.WriteLine("Error: Failed to load song %s", s_args.InitialSong);
				Exit(1);
			}
		}

		var ret = Video.Startup();

		Assert.IsTrue(ret, "Video.Startup()", "Failed to initialize video!");

		// TODO: native menu??

		if (s_args.WantFullScreenSpecified)
			Video.Fullscreen(s_args.WantFullScreen);

		VGAMem.CurrentPalette.Apply();
		Font.Initialize();
		MIDIEngine.Start();
		AudioPlayback.Initialize(AudioPlayback.AudioDriver, AudioPlayback.AudioDevice);
		AudioPlayback.InitializeModPlug();

		Video.SetMouseCursor(Configuration.Video.MouseCursor);
		Status.FlashText(" "); /* silence the mouse cursor message */

		Page.NotifySongChangedGlobal();

		if ((s_args.InitialSong != null) && (s_args.InitialDirectory == null))
			s_args.InitialDirectory = Path.GetDirectoryName(Path.GetFullPath(s_args.InitialSong!));

		if (s_args.InitialDirectory != null)
		{
			Configuration.Directories.ModulesDirectory = s_args.InitialDirectory;
			Configuration.Directories.SamplesDirectory = s_args.InitialDirectory;
			Configuration.Directories.InstrumentsDirectory = s_args.InitialDirectory;
		}

		if (s_args.StartupFlags.HasAllFlags(StartupFlags.FontEdit))
		{
			Status.Flags |= StatusFlags.StartupFontEdit;
			Page.SetPage(PageNumbers.FontEditor);
		}
		else if (s_args.InitialSong != null)
		{
			Page.SetPage(PageNumbers.Log);

			var song = Song.Load(s_args.InitialSong);

			if (song != null)
			{
				Song.CurrentSong = song;

				AudioPlayback.Reset();

				if (s_args.DiskwriteTo != null)
				{
					// make a guess?
					int multiOffset = s_args.DiskwriteTo.IndexOf("%c", StringComparison.InvariantCultureIgnoreCase);

					string driver = s_args.DiskwriteTo.EndsWith(".aif", StringComparison.InvariantCultureIgnoreCase)
						? "AIFF"
						: "WAV";

					if (multiOffset >= 0)
						driver = "M" + driver;

					if (Song.CurrentSong.ExportSong(s_args.DiskwriteTo, driver) != SaveResult.Success)
						Exit(1);
				}
				else if (s_args.StartupFlags.HasAllFlags(StartupFlags.Play))
				{
					AudioPlayback.Start();
					Page.SetPage(PageNumbers.Info);
				}
			}
		}
		else
			Page.SetPage(PageNumbers.About);

		Thread.CurrentThread.Priority = ThreadPriority.BelowNormal;

		/* poll once */
		MIDIEngine.PollPorts();

		EventLoop();

		return 0; /* blah */
	}

	public static void ShowExitPrompt()
	{
		/* This used to kill all open dialogs, but that doesn't seem to be necessary.
		Do keep in mind though, a dialog *might* exist when this function is called
		(for example, if the WM sends a close request). */

		if (Status.CurrentPage is AboutPage)
		{
			/* haven't even started up yet; don't bother confirming */
			Program.Exit(0);
		}
		else if (Status.CurrentPage is FontEditorPage)
		{
			if (Status.Flags.HasAllFlags(StatusFlags.StartupFontEdit))
			{
				MessageBox.Show(MessageBoxTypes.OKCancel, "Exit Font Editor?", () => Program.Exit(0));
			}
			else
			{
				/* don't ask, just go away */
				Dialog.DestroyAll();
				Page.SetPage(AllPages.FontEditor.ReturnPageNumber);
			}
		}
		else if (Status.MessageBoxType != MessageBoxTypes.OKCancel)
		{
			/* don't draw an exit prompt on top of an existing one */
			MessageBox.Show(
				MessageBoxTypes.OKCancel,
				Status.Flags.HasAllFlags(StatusFlags.ClassicMode)
				? "Exit Impulse Tracker?"
				: "Exit Schism Tracker?",
				() => Page.SaveCheck(() => Program.Exit(0)));
		}
	}
}
