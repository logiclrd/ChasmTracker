using System;

namespace ChasmTracker.Pages;

using ChasmTracker.Input;
using ChasmTracker.Playback;
using ChasmTracker.Songs;
using ChasmTracker.Utility;
using ChasmTracker.VGA;
using ChasmTracker.Widgets;

public class WaterfallPage : Page
{
	OtherWidget otherDisplay;

	const int ScopeRows = 32;

	VGAMemOverlay _ovl;

	public WaterfallPage()
		: base(PageNumbers.Waterfall, "", HelpTexts.Global)
	{
		otherDisplay = new OtherWidget();

		otherDisplay.OtherHandleKey += otherDisplay_HandleKey;

		/* get the _whole_ display */
		_ovl = VGAMem.AllocateOverlay(new Point(0, 0), new Point(79, 49));
	}

	/* Convert the output of */
	static int DoBits(Span<byte> q, int qOffset, Span<byte> @in, int length, int y)
	{
		for (int i = 0; i < length; i++)
		{
			/* j is has range 0 to 128. Now use the upper side for drawing.*/
			int c = 128 + @in[i];

			if (c > 255) c = 255;

			q[qOffset] = (byte)c;

			qOffset += y;
		}

		return qOffset;
	}

	void DrawSlice(int x, int h, byte c)
	{
		int y = ((h >> 2) % ScopeRows) + 1;

		_ovl.DrawLine(
			new Point(x, Constants.NativeScreenHeight - y),
			new Point(x, Constants.NativeScreenHeight - 1),
			c);
	}

	// "chan" is either zero for all, or nonzero for a specific output channel

	/*x = screen.x, h = 0..128, c = colour */

	void ProcessVisualization()
	{
		const int k = Constants.NativeScreenWidth / 2;
		int i;

		byte[] outFFT = new byte[Constants.NativeScreenWidth];

		/* move up previous line by one pixel */
		int waterfallBytes = Constants.NativeScreenWidth * (_ovl.Size.Height - 1) - ScopeRows;

		_ovl.Q.Slice(Constants.NativeScreenWidth, waterfallBytes).CopyTo(_ovl.Q);

		var q = _ovl.Q.Slice(waterfallBytes);

		if (FFT.Mono)
		{
			FFT.GetColumns(outFFT, 0);
			DoBits(q, 0, outFFT, Constants.NativeScreenWidth, 1);
		}
		else
		{
			FFT.GetColumns(outFFT.Slice(0, k), 1);
			FFT.GetColumns(outFFT.Slice(k, k), 2);

			DoBits(q, k - 1, outFFT, k, -1);
			DoBits(q, k, outFFT.Slice(k), k, 1);
		}

		/* draw the scope at the bottom */
		q = _ovl.Q.Slice(Constants.NativeScreenWidth * (Constants.NativeScreenHeight - ScopeRows));

		q.Clear();

		if (FFT.Mono)
			for (i = 0; i < Constants.NativeScreenWidth; i++) DrawSlice(i, outFFT[i], 5);
		else
		{
			for (i = 0; i < k; i++) DrawSlice(k - i - 1, outFFT[i], 5);
			for (i = 0; i < k; i++) DrawSlice(k + i, outFFT[k + i], 5);

		}

		Status.Flags |= StatusFlags.NeedUpdate;
	}

	short[] _dl = new short[FFT.BufferSize];
	short[] _dr = new short[FFT.BufferSize];

	public void VisualizationWork8Stereo(Span<sbyte> data)
	{
		if (data.Length == 0)
			FFT.ClearData();
		else
		{
			for (int i = 0; i < FFT.BufferSize && (i + i + 1) < data.Length; i++)
			{
				_dl[i] = unchecked((short)(data[i + i] << 8));
				_dr[i] = unchecked((short)(data[i + i + 1] << 8));
			}

			FFT.DataWork(0, _dl);
			FFT.DataWork(1, _dr);
		}

		if (Status.CurrentPage is WaterfallPage)
			ProcessVisualization();
	}

	public void VisualizationWork8Mono(Span<sbyte> data)
	{
		if (data.Length == 0)
			FFT.ClearData();
		else
		{
			for (int i = 0; i < FFT.BufferSize && i < data.Length; i++)
				_dl[i] = _dr[i] = unchecked((short)(data[i] << 8));

			FFT.DataWork(0, _dl);
			FFT.DataWork(1, _dr);
		}

		if (Status.CurrentPage is WaterfallPage)
			ProcessVisualization();
	}

	public void VisualizationWork16Stereo(Span<short> data)
	{
		if (data.Length == 0)
			FFT.ClearData();
		else
		{
			for (int i = 0; i < FFT.BufferSize && (i + i + 1) < data.Length; i++)
			{
				_dl[i] = data[i + i];
				_dr[i] = data[i + i + 1];
			}

			FFT.DataWork(0, _dl);
			FFT.DataWork(1, _dr);
		}

		if (Status.CurrentPage is WaterfallPage)
			ProcessVisualization();
	}

	public void VisualizationWork16Mono(Span<short> data)
	{
		if (data.Length == 0)
			FFT.ClearData();
		else
		{
			for (int i = 0; i < FFT.BufferSize && i < data.Length; i++)
				_dl[i] = _dr[i] = data[i];

			FFT.DataWork(0, _dl);
			FFT.DataWork(1, _dr);
		}

		if (Status.CurrentPage is WaterfallPage)
			ProcessVisualization();
	}

	public void VisualizationWork32Stereo(Span<int> data)
	{
		if (data.Length == 0)
			FFT.ClearData();
		else
		{
			for (int i = 0; i < FFT.BufferSize && (i + i + 1) < data.Length; i++)
			{
				_dl[i] = unchecked((short)(data[i + i] >> 16));
				_dr[i] = unchecked((short)(data[i + i + 1] >> 16));
			}

			FFT.DataWork(0, _dl);
			FFT.DataWork(1, _dr);
		}

		if (Status.CurrentPage is WaterfallPage)
			ProcessVisualization();
	}

	public void VisualizationWork32Mono(Span<int> data)
	{
		if (data.Length == 0)
			FFT.ClearData();
		else
		{
			for (int i = 0; i < FFT.BufferSize && i < data.Length; i++)
				_dl[i] = _dr[i] = unchecked((short)(data[i] >> 16));

			FFT.DataWork(0, _dl);
			FFT.DataWork(1, _dr);
		}

		if (Status.CurrentPage is WaterfallPage)
			ProcessVisualization();
	}

	public override void DrawFull()
	{
		VGAMem.ApplyOverlay(_ovl);
	}

	public override void SetPage()
	{
		_ovl.Clear(0);
	}

	bool otherDisplay_HandleKey(KeyEvent k)
	{
		int n, v, order, ii;

		if (!k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
		{
			if (k.MIDINote > -1)
			{
				n = k.MIDINote;
				if (k.MIDIVolume > -1)
					v = k.MIDIVolume / 2;
				else
					v = 64;
			}
			else
			{
				v = 64;
				n = k.NoteValue;
			}

			if (n > -1)
			{
				if (Song.CurrentSong.IsInstrumentMode)
					ii = AllPages.InstrumentList.CurrentInstrument;
				else
					ii = AllPages.SampleList.CurrentSample;

				if (k.State == KeyState.Release)
				{
					Song.CurrentSong.KeyUp(KeyJazz.NoInstrument, ii, n);
					Status.LastKeySym = KeySym.None;
				}
				else if (!k.IsRepeat)
					Song.CurrentSong.KeyDown(KeyJazz.NoInstrument, ii, n, v, KeyJazz.CurrentChannel);

				return true;
			}
		}

		switch (k.Sym)
		{
			case KeySym.s:
				if (k.Modifiers.HasAnyFlag(KeyMod.Alt))
				{
					if (k.State == KeyState.Release)
						return true;

					Song.CurrentSong.ToggleStereo();
					Status.Flags |= StatusFlags.NeedUpdate;
					return true;
				}
				return false;
			case KeySym.m:
				if (k.Modifiers.HasAnyFlag(KeyMod.Alt))
				{
					if (k.State == KeyState.Release)
						return true;
					FFT.Mono = !FFT.Mono;
					return true;
				}
				return false;
			case KeySym.Left:
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return false;
				if (k.State == KeyState.Release)
					return true;
				FFT.NoiseFloor -= 4;
				break;
			case KeySym.Right:
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return false;
				if (k.State == KeyState.Release)
					return true;
				FFT.NoiseFloor += 4;
				break;
			case KeySym.g:
				if (k.Modifiers.HasAnyFlag(KeyMod.Alt))
				{
					if (k.State == KeyState.Press)
						return true;

					order = Song.CurrentSong.CurrentOrder;

					if (AudioPlayback.Mode == AudioPlaybackMode.Playing)
						n = Song.CurrentSong.OrderList[order];
					else
						n = AudioPlayback.PlayingPattern;

					if (n < Constants.MaxPathLength)
					{
						AllPages.OrderList.CurrentOrder = order;
						AllPages.PatternEditor.CurrentPattern = n;
						AllPages.PatternEditor.CurrentRow = AudioPlayback.PlayingRow;

						SetPage(PageNumbers.PatternEditor);
					}

					return true;
				}
				return false;
			case KeySym.r:
				if (k.Modifiers.HasAnyFlag(KeyMod.Alt))
				{
					if (k.State == KeyState.Release)
						return true;

					AudioPlayback.FlipStereo();
					return true;
				}
				return false;
			case KeySym.Plus:
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return false;
				if (k.State == KeyState.Release)
					return true;
				if (AudioPlayback.Mode == AudioPlaybackMode.Playing)
					AudioPlayback.CurrentOrder++;
				return true;
			case KeySym.Minus:
				if (k.Modifiers.HasAnyFlag(KeyMod.ControlAltShift))
					return false;
				if (k.State == KeyState.Release)
					return true;
				if (AudioPlayback.Mode == AudioPlaybackMode.Playing)
					AudioPlayback.CurrentOrder--;
				return true;
			case KeySym.Semicolon:
			case KeySym.Colon:
				if (k.State == KeyState.Release)
					return true;
				if (Song.CurrentSong.IsInstrumentMode)
					AllPages.InstrumentList.CurrentInstrument--;
				else
					AllPages.SampleList.CurrentSample--;
				return true;
			case KeySym.Quote:
			case KeySym.QuoteDbl:
				if (k.State == KeyState.Release)
					return true;
				if (Song.CurrentSong.IsInstrumentMode)
					AllPages.InstrumentList.CurrentInstrument++;
				else
					AllPages.SampleList.CurrentSample++;
				return true;
			case KeySym.Comma:
			case KeySym.Less:
				if (k.State == KeyState.Release)
					return true;
				AudioPlayback.ChangeCurrentPlayChannel(-1, false);
				return true;
			case KeySym.Period:
			case KeySym.Greater:
				if (k.State == KeyState.Release)
					return true;
				AudioPlayback.ChangeCurrentPlayChannel(1, false);
				return true;
			default:
				return false;
		}

		FFT.NoiseFloor = FFT.NoiseFloor.Clamp(36, 96);
		return true;
	}
}
