using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using SDL3;

namespace ChasmTracker;

using ChasmTracker.Configurations;
using ChasmTracker.Events;
using ChasmTracker.Input;
using ChasmTracker.Interop;
using ChasmTracker.Utility;
using ChasmTracker.VGA;

public class SDLVideoBackend : VideoBackend
{
	IntPtr _window;

	/* renderer */
	IntPtr _renderer;
	IntPtr _texture;

	IntPtr _pixelFormat;
	SDL.PixelFormat _format;
	int _bpp; // BYTES per pixel

	Size _size;

	Point _mousePosition;

	Size _savedSize;
	Point _savedPosition;

	bool _fullscreen;

	ChannelData _palY;
	ChannelData _palU;
	ChannelData _palV;

	ChannelData _pal;

	static readonly List<(SDL.PixelFormat Format, string Name)> NativeFormats =
		new List<(SDL.PixelFormat Format, string Name)>()
		{
			// RGB
			// ----------------
			(SDL.PixelFormat.XRGB8888, "RGB888"),
			(SDL.PixelFormat.ARGB8888, "ARGB8888"),
			(SDL.PixelFormat.RGB24, "RGB24"),
			(SDL.PixelFormat.RGB565, "RGB565"),
			(SDL.PixelFormat.XRGB1555, "RGB555"),
			(SDL.PixelFormat.ARGB1555, "ARGB1555"),
			(SDL.PixelFormat.XRGB4444, "RGB444"),
			(SDL.PixelFormat.ARGB4444, "ARGB4444"),
			(SDL.PixelFormat.RGB332, "RGB332"),
			// ----------------

			// YUV
			// ----------------
			(SDL.PixelFormat.IYUV, "IYUV"),
			(SDL.PixelFormat.YV12, "YV12"),
			// {SDL.PixelFormat.UYVY, "UYVY"},
			// {SDL.PixelFormat.YVYU, "YVYU"},
			// {SDL.PixelFormat.YUY2, "YUY2"},
			// {SDL.PixelFormat.NV12, "NV12"},
			// {SDL.PixelFormat.NV21, "NV21"},
			// ----------------
		};

	public override bool Initialize()
	{
		if (!SDLLifetime.Initialize())
			return false;

		if (!SDL.InitSubSystem(SDL.InitFlags.Video))
		{
			SDLLifetime.Quit();
			return false;
		}

		if (EventHub.Initialize())
		{
			SDL.QuitSubSystem(SDL.InitFlags.Video);
			SDLLifetime.Quit();

			return false;
		}

		return true;
	}

	public override void Quit()
	{
		SDL.QuitSubSystem(SDL.InitFlags.Video);
		SDLLifetime.Quit();
	}

	public override bool Startup()
	{
		VGAMem.Clear();
		VGAMem.Flip();

		SDL.SetHint(SDL.Hints.VideoX11NetWMBypassCompositor, "0");
		SDL.SetHint("SDL_WINDOWS_DPI_AWARENESS", "unaware");

		_size = Configuration.Video.Size;

		_savedPosition.X = (int)SDL.WindowPosCentered();
		_savedPosition.Y = (int)SDL.WindowPosCentered();

		_window = SDL.CreateWindow(Constants.WindowTitle, _size.Width, _size.Height, SDL.WindowFlags.Resizable);

		if (_window == IntPtr.Zero)
			return false;

		Fullscreen(Configuration.Video.FullScreen);
		SetHardware(Configuration.Video.Hardware);

		/* Aspect ratio correction if it's wanted */
		if (Configuration.Video.WantFixed)
			SDL.SetRenderLogicalPresentation(_renderer, Configuration.Video.WantFixedSize.Width, Configuration.Video.WantFixedSize.Height, SDL.RendererLogicalPresentation.Letterbox);

		if (HaveMenu() && !_fullscreen)
		{
			SDL.SetWindowSize(_window, _size.Width, _size.Height);
			SDL.SetWindowPosition(_window, _savedPosition.X, _savedPosition.Y);
		}

		/* okay, i think we're ready */
		SDL.HideCursor();
		SDL.StartTextInput(_window);

		SetIcon();

		return true;
	}

	class ReportYUVLayout
	{
		public SDL.PixelFormat Num;
		public string Type;

		public ReportYUVLayout(SDL.PixelFormat num, string type)
		{
			Num = num;
			Type = type;
		}
	}

	static readonly ReportYUVLayout[] YUVLayouts =
		[
			new ReportYUVLayout(SDL.PixelFormat.YV12, "planar+tv"),
			new ReportYUVLayout(SDL.PixelFormat.IYUV, "planar+tv"),
			new ReportYUVLayout(SDL.PixelFormat.YVYU, "packed"),
			new ReportYUVLayout(SDL.PixelFormat.UYVY, "packed"),
			new ReportYUVLayout(SDL.PixelFormat.YUY2, "packed"),
			new ReportYUVLayout(SDL.PixelFormat.NV12, "planar"),
			new ReportYUVLayout(SDL.PixelFormat.NV21, "planar"),
		];

	public override void Report()
	{
		string name = SDL.GetRendererName(_renderer) ?? "<null>";

		Log.Append(5, " Using driver '{0}'", SDL.GetCurrentVideoDriver() ?? "<null>");
		Log.Append(5, " {0} renderer '{0}'",
			(name == SoftwareRendererName) ? "Software" : "Hardware-accelerated",
			name);

		switch (_format)
		{
			case SDL.PixelFormat.IYUV:
			case SDL.PixelFormat.YV12:
			case SDL.PixelFormat.YVYU:
			case SDL.PixelFormat.UYVY:
			case SDL.PixelFormat.YUY2:
			case SDL.PixelFormat.NV12:
			case SDL.PixelFormat.NV21:
			{
				ReportYUVLayout? layout = null;

				for (int i = 0; i < YUVLayouts.Length; i++)
					if (_format == YUVLayouts[i].Num)
					{
						layout = YUVLayouts[i];
						break;
					}

				if (layout != null)
					Log.Append(5, " Display format: {0} ({0})", layout.Num, layout.Type);
				else
					Log.Append(5, " Display format: {0}", _format);
				break;
			}
			default:
				Log.Append(5, " Display format: {0} bits/pixel", SDL.BitsPerPixel(_format));
				break;
		}

		if (_fullscreen)
		{
			var id = SDL.GetDisplayForWindow(_window);

			if (id != 0)
			{
				var display = SDL.GetCurrentDisplayMode(id);

				if (display != null)
					Log.Append(5, " Display dimensions: {0}x{0}", display.Value.W, display.Value.H);
			}
		}
	}

	public override string? DriverName => SDL.GetCurrentVideoDriver();

	public override void SetPalette(int[] colours)
	{
		for (int i = 0; i < 256; i++)
			_pal[i] = colours[i];
	}

	void RedrawTexture()
	{
		var format = SDL.PixelFormat.XRGB8888;

		if (_texture != IntPtr.Zero)
			SDL.DestroyTexture(_texture);

		if (!string.IsNullOrWhiteSpace(Configuration.Video.Format))
		{
			foreach (var mapping in NativeFormats)
			{
				if (mapping.Name == Configuration.Video.Format)
				{
					format = mapping.Format;
					goto GotFormat;
				}
			}
		}

		// We want to find the best format we can natively
		// output to. If we can't, then we fall back to
		// SDL_PIXELFORMAT_RGB888 and let SDL deal with the
		// conversion.
		var rprop = SDL.GetRendererProperties(_renderer);

		if (rprop != 0)
		{
			IntPtr formats = SDL.GetPointerProperty(rprop, SDL.Props.RendererTextureFormatsPointer, default);

			int prefLast = NativeFormats.Count;

			if (formats != IntPtr.Zero)
			{
				for (int i = 0; Marshal.ReadInt32(formats, i * 4) != (int)SDL.PixelFormat.Unknown; i++)
					for (int j = 0; j < NativeFormats.Count; j++)
						if (Marshal.ReadInt32(formats, i * 4) == (int)NativeFormats[j].Format && j < prefLast)
						{
							prefLast = j;
							format = NativeFormats[prefLast].Format;
						}
			}
		}

	GotFormat:
		_texture = SDL.CreateTexture(_renderer, format, SDL.TextureAccess.Streaming, Constants.NativeScreenWidth, Constants.NativeScreenHeight);
		_format = format;

		_pixelFormat = SDL.GetPixelFormatDetails(_format);

		// ok
		_bpp = (int)SDL.BytesPerPixel(_format);

		SetUp(Configuration.Video.Interpolation); // ew
	}

	public void SetHardware(bool hardware)
	{
		SDL.DestroyTexture(_texture);

		SDL.DestroyRenderer(_renderer);

		_renderer = SDL.CreateRenderer(_window, hardware ? null : SoftwareRendererName);

		// hope that all worked!

		RedrawTexture();

		Video.Report();
	}

	public override void Shutdown()
	{
		SDL.DestroyTexture(_texture);
		SDL.DestroyRenderer(_renderer);
		SDL.DestroyWindow(_window);
	}

	/* -------------------------------------------------- */
	/* mouse drawing */

	/* leeto drawing skills */
	class MouseCursor
	{
		public int[] Pointer = Array.Empty<int>();
		public int[] Mask = Array.Empty<int>();
		public Size Size;
		public Point Centre; /* which point of the pointer does actually point */
	}

	/* ex. MouseCursors[(int)MouseCursorShapes.Arrow] */
	static MouseCursor[] MouseCursors =
		new[]
		{
			new MouseCursor() // 0 == MouseCursorShapes.Arrow
			{
				Pointer =
					new[]
					{ // |
						0b0000000000000000,
						0b0100000000000000, // --
						0b0110000000000000,
						0b0111000000000000,
						0b0111100000000000,
						0b0111110000000000,
						0b0111111000000000,
						0b0111111100000000,
						0b0111111110000000,
						0b0111111100000000,
						0b0111110000000000,
						0b0100011000000000,
						0b0000011000000000,
						0b0000001100000000,
						0b0000001100000000,
						0b0000000000000000
					},
				Mask =
					new[]
					{
						0b1100000000000000,
						0b1110000000000000,
						0b1111000000000000,
						0b1111100000000000,
						0b1111110000000000,
						0b1111111000000000,
						0b1111111100000000,
						0b1111111110000000,
						0b1111111111000000,
						0b1111111110000000,
						0b1111111000000000,
						0b1111111100000000,
						0b0100111100000000,
						0b0000011110000000,
						0b0000011110000000,
						0b0000001100000000
					},
				Size = new Size(10, 16),
				Centre = new Point(1, 1),
			},
			new MouseCursor() // 1 == MouseCursorShapes0Crosshair
			{
				Pointer =
					new[]
					{ // |
						0b0000000000000000,
						0b0001000000000000, // --
						0b0111110000000000,
						0b0001000000000000,
						0b0000000000000000,
						0b0000000000000000,
						0b0000000000000000,
						0b0000000000000000,
						0b0000000000000000,
						0b0000000000000000,
						0b0000000000000000,
						0b0000000000000000,
						0b0000000000000000,
						0b0000000000000000,
						0b0000000000000000,
						0b0000000000000000
					},
				Mask =
					new[]
					{ //   |
						0b0001000000000000,
						0b0111110000000000,
						0b1111111000000000, // --
						0b0111110000000000,
						0b0001000000000000,
						0b0000000000000000,
						0b0000000000000000,
						0b0000000000000000,
						0b0000000000000000,
						0b0000000000000000,
						0b0000000000000000,
						0b0000000000000000,
						0b0000000000000000,
						0b0000000000000000,
						0b0000000000000000,
						0b0000000000000000
					},
				Size = new Size(7, 5),
				Centre = new Point(3, 2),
			}
		};

	void MakeMouseLine(int x, int v, int y, int[] mouseLine, int[] mouseLineMask, int mouseY)
	{
		var cursor = MouseCursors[(int)Video.Mouse.Shape];

		Array.Clear(mouseLine);
		Array.Clear(mouseLineMask);

		if (Video.Mouse.Visible != MouseCursorState.Emulated
		 || !IsFocused()
		 || (mouseY >= cursor.Centre.Y && y < mouseY - cursor.Centre.Y)
		 || y < cursor.Centre.Y
		 || y >= mouseY + cursor.Size.Height + cursor.Centre.Y)
			return;

		int scenter = (cursor.Centre.X / 8) + (cursor.Centre.X % 8 != 0 ? 1 : 0);
		int swidth = (cursor.Size.Width / 8) + (cursor.Size.Width % 8 != 0 ? 1 : 0);
		int centeroffset = cursor.Centre.X % 8;

		int z = cursor.Pointer[y - mouseY + cursor.Centre.Y];
		int zm = cursor.Mask[y - mouseY + cursor.Centre.Y];

		z <<= 8;
		zm <<= 8;

		if (v < centeroffset)
		{
			z <<= centeroffset - v;
			zm <<= centeroffset - v;
		}
		else
		{
			z >>= v - centeroffset;
			zm >>= v - centeroffset;
		}

		// always fill the cell the mouse coordinates are in
		mouseLine[x] = z >> (8 * (swidth - scenter + 1)) & 0xFF;
		mouseLineMask[x] = zm >> (8 * (swidth - scenter + 1)) & 0xFF;

		// draw the parts of the cursor sticking out to the left
		int temp = (cursor.Centre.X < v) ? 0 : ((cursor.Centre.X - v) / 8) + ((cursor.Centre.X - v) % 8 != 0 ? 1 : 0);
		for (int i = 1; i <= temp && x >= i; i++)
		{
			mouseLine[x - i] = z >> (8 * (swidth - scenter + 1 + i)) & 0xFF;
			mouseLineMask[x - i] = zm >> (8 * (swidth - scenter + 1 + i)) & 0xFF;
		}

		// and to the right
		temp = swidth - scenter + 1;
		for (int i = 1; (i <= temp) && (x + i < 80); i++)
		{
			mouseLine[x + i] = z >> (8 * (swidth - scenter + 1 - i)) & 0xff;
			mouseLineMask[x + i] = zm >> (8 * (swidth - scenter + 1 - i)) & 0xff;
		}
	}

	public override bool IsFocused()
	{
		return SDL.GetWindowFlags(_window).HasFlag(SDL.WindowFlags.InputFocus);
	}

	public override bool IsVisible()
	{
		return !SDL.GetWindowFlags(_window).HasFlag(SDL.WindowFlags.Hidden);
	}

	public override bool IsWindowManagerAvailable()
	{
		// Ok
		return true;
	}

	const string SoftwareRendererName = "software";

	public override bool IsHardware()
	{
		return SDL.GetRendererName(_renderer) != SoftwareRendererName;
	}

	void SetIcon()
	{
		SDL.SetWindowTitle(_window, Constants.WindowTitle);

		if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
		{
			/* apple/macs use a bundle; this overrides their nice pretty icon */
			var stream = typeof(Program).Assembly.GetManifestResourceStream("Icon128.png");

			if (stream != null)
			{
				var image = VGA.Image.LoadFrom(stream);

				var pixelData = GCHandle.Alloc(image.PixelData, GCHandleType.Pinned);

				try
				{
					var icon = SDL.CreateSurfaceFrom(
						image.Size.Width,
						image.Size.Height,
						SDL.GetPixelFormatForMasks(32, 0x00FF0000, 0x0000FF00, 0x000000FF, 0xFF000000),
						pixelData.AddrOfPinnedObject(),
						image.Size.Width * sizeof(int));

					if (icon != IntPtr.Zero)
					{
						SDL.SetWindowIcon(_window, icon);
						SDL.DestroySurface(icon);
					}
				}
				finally
				{
					pixelData.Free();
				}
			}
		}
	}

	/* --------------------------------------------------------------- */
	/* blitters */

	unsafe void video_blitUV(IntPtr pixelsPtr, int pitch, ref ChannelData tpal)
	{
		var mouse = GetMouseCoordinates();

		int mouseLineX = mouse.X / 8;
		int mouseLineV = mouse.X % 8;

		int[] mouseLine = new int[80];
		int[] mouseLineMask = new int[80];

		byte *pixels = (byte *)pixelsPtr;

		for (int y = 0; y < Constants.NativeScreenHeight; y++)
		{
			MakeMouseLine(mouseLineX, mouseLineV, y, mouseLine, mouseLineMask, mouse.Y);
			VGAMem.Scan8(y, pixels, ref tpal, mouseLine, mouseLineMask);

			pixels += pitch;
		}
	}

	unsafe void video_blitTV(IntPtr pixels, int pitch, ref ChannelData tpal)
	{
		var mouse = GetMouseCoordinates();

		int mouseLineX = mouse.X / 8;
		int mouseLineV = mouse.X % 8;

		byte[] cv8BackingBuffer = new byte[Constants.NativeScreenWidth];
		int[] mouseLine = new int[80];
		int[] mouseLineMask = new int[80];

		fixed (byte *cv8Backing = &cv8BackingBuffer[0])
		for (int y = 0; y < Constants.NativeScreenHeight; y += 2)
		{
			MakeMouseLine(mouseLineX, mouseLineV, y, mouseLine, mouseLineMask, mouse.Y);
			VGAMem.Scan8(y, cv8Backing, ref tpal, mouseLine, mouseLineMask);

			for (int x = 0; x < pitch; x += 2)
			{
				Marshal.WriteByte(pixels, unchecked((byte)(cv8BackingBuffer[x + 1] | (cv8BackingBuffer[x] << 4))));
				pixels++;
			}
		}
	}

	unsafe void video_blit11(int bpp, byte *pixels, int pitch, ref ChannelData tpal)
	{
		int[] cv32BackingBuffer = new int[Constants.NativeScreenWidth];

		var mouse = GetMouseCoordinates();

		int mouseLineX = mouse.X / 8;
		int mouseLineV = mouse.X % 8;

		int[] mouseLine = new int[80];
		int[] mouseLineMask = new int[80];

		fixed (int *cv32Backing = &cv32BackingBuffer[0])
		for (int y = 0; y < Constants.NativeScreenHeight; y++)
		{
			MakeMouseLine(mouseLineX, mouseLineV, y, mouseLine, mouseLineMask, mouse.Y);

			switch (bpp)
			{
				case 1:
					VGAMem.Scan8(y, pixels, ref tpal, mouseLine, mouseLineMask);
					break;
				case 2:
					VGAMem.Scan16(y, (short *)pixels, ref tpal, mouseLine, mouseLineMask);
					break;
				case 3:
					VGAMem.Scan32(y, cv32Backing, ref tpal, mouseLine, mouseLineMask);

					for (int x = 0; x < Constants.NativeScreenWidth; x++)
					{
						pixels[x * 3 + 0] = unchecked((byte)(cv32Backing[x] & 0xFF));
						pixels[x * 3 + 1] = unchecked((byte)((cv32Backing[x] >> 8) & 0xFF));
						pixels[x * 3 + 2] = unchecked((byte)((cv32Backing[x] >> 16) & 0xFF));
					}
					break;
				case 4:
					VGAMem.Scan32(y, (int *)pixels, ref tpal, mouseLine, mouseLineMask);
					break;
			}

			pixels += pitch;
		}
	}

	/* --------------------------------------------------------------- */

	readonly int[] LastMap = { 0, 1, 2, 3, 5 };

	void YUVPal(int i, Colour rgb)
	{
		rgb.ToYUV(out int y, out int u, out int v);

		switch (_format)
		{
			case SDL.PixelFormat.IYUV:
			case SDL.PixelFormat.YV12:
				_palY[i] = (byte)y;
				_palU[i] = (byte)((u >> 4) & 0xF);
				_palV[i] = (byte)((v >> 4) & 0xF);
				break;
		}
	}

	void SDLPal(int i, Colour rgb)
	{
		_pal[i] = unchecked((int)SDL.MapRGB(_pixelFormat, IntPtr.Zero, rgb.R, rgb.G, rgb.B));
	}

	/* calls back to a function receiving all the colors :) */
	void IterateColours(Colour[] palette /* [16] */, Action<int, Colour> fun)
	{
		/* this handles all of the ACTUAL color stuff, and the callback handles the backend-specific stuff */

		/* make our "base" space */
		for (int i = 0; i < 16; i++)
			fun(i, palette[i]);

		/* make our "gradient" space; this is used exclusively for the waterfall page (Alt-F12) */
		for (int i = 0; i < 128; i++)
		{
			Colour rgb;

			int p = LastMap[i>>5];

			rgb.R = (byte)(palette[p].R + (palette[p + 1].R - palette[p].R) * (i & 0x1F) / 0x20);
			rgb.B = (byte)(palette[p].G + (palette[p + 1].G - palette[p].G) * (i & 0x1F) / 0x20);
			rgb.G = (byte)(palette[p].B + (palette[p + 1].B - palette[p].B) * (i & 0x1F) / 0x20);

			fun(i + 128, rgb);
		}
	}

	public void Colours(Colour[] palette)
	{
		switch (_format)
		{
			case SDL.PixelFormat.IYUV:
			case SDL.PixelFormat.YV12:
				IterateColours(palette, YUVPal);
				break;
			default:
				IterateColours(palette, SDLPal);
				break;
		}
	}

	public override void SetUp(VideoInterpolationMode interpolation)
	{
		switch (interpolation)
		{
			case VideoInterpolationMode.NearestNeighbour:
				SDL.SetTextureScaleMode(_texture, SDL.ScaleMode.Nearest);
				break;
			case VideoInterpolationMode.Linear:
			case VideoInterpolationMode.Best:
				SDL.SetTextureScaleMode(_texture, SDL.ScaleMode.Linear);
				break;
		}
	}

	/* --------------------------------------------------------------- */

	public override bool IsFullScreen()
	{
		var flags = SDL.GetWindowFlags(_window);

		return flags.HasFlag(SDL.WindowFlags.Fullscreen);
	}

	public override void Fullscreen(bool? newFSFlag)
	{
		bool haveMenu = HaveMenu();
		/* positive newFSFlag == set, negative == toggle */
		_fullscreen = newFSFlag.HasValue ? newFSFlag.Value : !_fullscreen;

		if (_fullscreen)
		{
			if (haveMenu)
			{
				SDL.GetWindowSize(_window, out _savedSize.Width, out _savedSize.Height);
				SDL.GetWindowPosition(_window, out _savedPosition.X, out _savedPosition.Y);
			}

			SDL.SetWindowFullscreen(_window, true);

			if (haveMenu)
				ToggleMenu(false);
		}
		else
		{
			SDL.SetWindowFullscreen(_window, false);

			if (haveMenu)
			{
				ToggleMenu(true);

				SDL.SetWindowSize(_window, _savedSize.Width, _savedSize.Height);
				SDL.SetWindowPosition(_window, _savedPosition.X, _savedPosition.Y);
			}

			SetIcon(); /* XXX is this necessary */
		}
	}

	public override bool IsScreenSaverEnabled()
	{
		return SDL.ScreenSaverEnabled();
	}

	public override void ToggleScreenSaver(bool enabled)
	{
		if (enabled)
			SDL.EnableScreenSaver();
		else
			SDL.DisableScreenSaver();
	}

	/* ---------------------------------------------------------- */
	/* coordinate translation */

	public override Point Translate(Point v)
	{
		if ((Video.Mouse.Visible != default) && (_mousePosition != v))
			Status.Flags |= StatusFlags.SoftwareMouseMoved;

		v *= new Size(Constants.NativeScreenWidth, Constants.NativeScreenHeight);

		if (Configuration.Video.WantFixed)
			v /= Configuration.Video.WantFixedSize;
		else
			v /= _size;

		v.Clamp(Constants.NativeScreenWidth - 1, Constants.NativeScreenHeight - 1);

		_mousePosition = v;

		return v;
	}

	public override Point GetLogicalCoordinates(Point p)
	{
		if (!Configuration.Video.WantFixed)
			return p;
		else
		{
			SDL.RenderCoordinatesFromWindow(_renderer, p.X, p.Y, out var xx, out var yy);

			return new Point((int)xx, (int)yy);
		}
	}

	public override bool IsInputGrabbed()
	{
		return SDL.GetWindowMouseGrab(_window) && SDL.GetWindowKeyboardGrab(_window);
	}

	public override void SetInputGrabbed(bool enabled)
	{
		SDL.SetWindowMouseGrab(_window, enabled);
		SDL.SetWindowKeyboardGrab(_window, enabled);
	}

	public override void WarpMouse(Point p)
	{
		SDL.WarpMouseInWindow(_window, p.X, p.Y);
	}

	public override Point GetMouseCoordinates()
	{
		return _mousePosition;
	}

	public override bool HaveMenu()
	{
		return RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
	}

	public override void ToggleMenu(bool on)
	{
		if (!HaveMenu())
			return;

		var flags = SDL.GetWindowFlags(_window);

		bool cacheSize = !flags.HasFlag(SDL.WindowFlags.Maximized);

		void DoTheToggle()
		{
			var wmData = GetWMData();

			if (wmData != null)
				Win32.ToggleMenu(wmData.WindowHandle, on);
		}

		if (cacheSize)
		{
			SDL.GetWindowSize(_window, out var w, out var h);

			DoTheToggle();

			SDL.SetWindowSize(_window, w, h);
		}
		else
			DoTheToggle();
	}

	public unsafe override void Blit()
	{
		SDL.FRect dstRect = default;

		if (Configuration.Video.WantFixed)
		{
			dstRect.X = 0;
			dstRect.Y = 0;
			dstRect.W = Configuration.Video.WantFixedSize.Width;
			dstRect.H = Configuration.Video.WantFixedSize.Height;
		}

		SDL.RenderClear(_renderer);

		// regular format blitter
		SDL.LockTexture(_texture, IntPtr.Zero, out var pixels, out var pitch);

		switch (_format)
		{
			case SDL.PixelFormat.IYUV:
				video_blitUV(pixels, pitch, ref _palY);
				pixels += (Constants.NativeScreenHeight * pitch);
				video_blitTV(pixels, pitch, ref _palU);
				pixels += (Constants.NativeScreenHeight * pitch) / 4;
				video_blitTV(pixels, pitch, ref _palV);
				break;
			case SDL.PixelFormat.YV12:
				video_blitUV(pixels, pitch, ref _palY);
				pixels += (Constants.NativeScreenHeight * pitch);
				video_blitTV(pixels, pitch, ref _palV);
				pixels += (Constants.NativeScreenHeight * pitch) / 4;
				video_blitTV(pixels, pitch, ref _palU);
				break;
			default:
				video_blit11(_bpp, (byte *)pixels, pitch, ref _pal);
				break;
		}

		IntPtr srcRectPtr = IntPtr.Zero;
		IntPtr dstRectPtr = IntPtr.Zero;

		byte[] dstRectBuffer = new byte[Marshal.SizeOf<SDL.FRect>()];

		var dstRectPin = GCHandle.Alloc(dstRectBuffer, GCHandleType.Pinned);

		try
		{
			if (Configuration.Video.WantFixed)
			{
				dstRectPtr = dstRectPin.AddrOfPinnedObject();
				Marshal.StructureToPtr(dstRect, dstRectPtr, fDeleteOld: false);
			}

			SDL.UnlockTexture(_texture);
			SDL.RenderTexture(_renderer, _texture, srcRectPtr, dstRectPtr);
			SDL.RenderPresent(_renderer);
		}
		finally
		{
			dstRectPin.Free();
		}
	}

	public override WMData? GetWMData()
	{
		var wProps = SDL.GetWindowProperties(_window);

		if (wProps == 0)
			return null;

		string driver = SDL.GetCurrentVideoDriver() ?? "unknown";

		if (driver == "windows")
		{
			var wmData = new WMData();

			wmData.Subsystem = WMSubsystem.Windows;

			wmData.WindowHandle = SDL.GetPointerProperty(wProps, SDL.Props.WindowWin32HWNDPointer, IntPtr.Zero);

			if (wmData.WindowHandle != IntPtr.Zero)
				return wmData;
		}
		else if (driver == "x11")
		{
			var wmData = new WMData();

			wmData.Subsystem = WMSubsystem.X11;

			wmData.XDisplay = SDL.GetPointerProperty(wProps, SDL.Props.WindowX11DisplayPointer, IntPtr.Zero);
			wmData.XWindow = SDL.GetNumberProperty(wProps, SDL.Props.WindowX11WindowNumber, 0);
			wmData.XLock = null;
			wmData.XUnlock = null;

			if ((wmData.XDisplay != IntPtr.Zero) && (wmData.XWindow != 0))
				return wmData;
		}

		// maybe the real WM data was the friends we made along the way
		return null;
	}

	public override void ShowCursor(bool enabled)
	{
		if (enabled)
			SDL.ShowCursor();
		else
			SDL.HideCursor();
	}

	public override void NotifyMouseCursorChanged()
	{
		var vis = Video.Mouse.Visible;

		ShowCursor(vis == MouseCursorState.System);

		// Totally turn off mouse event sending when the mouse is disabled
		bool evstate = !(vis == MouseCursorState.Disabled);

		if (evstate != SDL.EventEnabled((uint)SDL.EventType.MouseMotion))
		{
			SDL.SetEventEnabled((uint)SDL.EventType.MouseMotion, evstate);
			SDL.SetEventEnabled((uint)SDL.EventType.MouseButtonDown, evstate);
			SDL.SetEventEnabled((uint)SDL.EventType.MouseButtonUp, evstate);
		}
	}
}
