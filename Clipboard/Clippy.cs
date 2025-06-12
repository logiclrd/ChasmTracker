using System;
using System.Text;

namespace ChasmTracker.Clipboard;

using ChasmTracker.Events;
using ChasmTracker.Widgets;

public class Clippy
{
	/* XXX: should each page have a separate selection?
	* or maybe, we should replicate the windows behavior,
	* and have a separate clipboard for each "type" of
	* data (such as pattern data, text, etc) */
	static string? _currentSelection = null;
	static string? _currentClipboard = null;
	static Widget?[] _widgetOwner = new Widget?[16];

	static ClippyBackend? _backend;

	/* called when schism needs a paste operation; cb is CLIPPY_SELECT if the middle button is
	used to paste, otherwise if the "paste key" is pressed, this uses CLIPPY_BUFFER */
	public static void Paste(ClippySource cb)
	{
		string? q = InternalClippyPaste(cb);

		if (q != null)
			StringPaste(q);
	}

	/* updates the clipboard selection; called by various widgets that perform a copy operation;
	stops at the first null, so setting len to <0 means we get the next utf8 or asciiz string */
	public static void Select(Widget? w, string? data, int len = int.MaxValue)
	{
		_currentSelection = null;

		if (data == null)
			_widgetOwner[(int)ClippySource.Select] = null;
		else
		{
			int ofs = data.IndexOf('\0');

			int trunc = Math.Min(ofs, len);

			if (data.Length > trunc)
				data = data.Substring(0, trunc);

			_currentSelection = data;
			_widgetOwner[(int)ClippySource.Select] = w;

			/* notify SDL about our selection change */
			CopyToSys(ClippySource.Select);
		}
	}

	public static Widget? Owner(ClippySource source)
	{
		if (Enum.IsDefined<ClippySource>(source))
			return _widgetOwner[(int)source];
		else
			return null;
	}

	/* copies the selection to the yank buffer (0 -> 1) */
	public static void Yank()
	{
		if (!string.IsNullOrEmpty(_currentSelection))
		{
			_currentClipboard = _currentSelection;
			_widgetOwner[(int)ClippySource.Buffer] = _widgetOwner[(int)ClippySource.Select];
			CopyToSys(ClippySource.Buffer);

			Status.FlashText("Copied to selection buffer");
		}
	}

	// initializes the backend
	public static bool Initialize()
	{
		_backend = new ClippyBackendMAUI();

		if (_backend.Initialize())
			return true;

		_backend = new ClippyBackendSDL();

		if (_backend.Initialize())
			return true;

		_backend = null;
		return false;
	}

	public static void Quit()
	{
		if (_backend != null)
		{
			_backend.Quit();
			_backend = null;
		}
	}

	//-----------------------------------------------------------------------

	static string? InternalClippyPaste(ClippySource cb)
	{
		switch (cb)
		{
			case ClippySource.Select:
				if ((_backend != null) && _backend.HaveSelection)
					_currentSelection = _backend.GetSelection();

				return _currentSelection;
			case ClippySource.Buffer:
				if ((_backend != null) && _backend.HaveClipboard)
					_currentClipboard = _backend.GetClipboard();

				return _currentClipboard;
		}

		return null;
	}

	static void CopyToSys(ClippySource cb)
	{
		if (_currentSelection == null)
			return;

		var @out = new StringBuilder();

		/* normalize line breaks
		*
		* TODO: this needs to be done internally as well; every paste
		* handler ought to expect Unix LF format. */

		for (int i = 0; i < _currentSelection.Length; i++)
		{
			if (_currentSelection[i] == '\r' && _currentSelection[i + 1] == '\n')
			{
				/* CRLF -> LF */
				@out.Append('\n');
				i++;
			}
			else if (_currentSelection[i] == '\r')
			{
				/* CR -> LF */
				@out.Append('\n');
			}
			else
			{
				/* we're good */
				@out.Append(_currentSelection[i]);
			}
		}

		switch (cb)
		{
			case ClippySource.Select:
				if (_backend != null)
					_backend.SetSelection(@out.ToString());
				break;
			default:
			case ClippySource.Buffer:
				if (_backend != null)
					_backend.SetClipboard(@out.ToString());
				break;
		}
	}

	static void StringPaste(string cbptr)
	{
		EventHub.PushEvent(new ClipboardPasteEvent(cbptr));
	}
}