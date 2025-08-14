using System;
using System.Collections.Generic;

namespace ChasmTracker.SDLBackends;

using ChasmTracker.Audio;
using ChasmTracker.Utility;

using SDL3;

public class SDLAudioBackend : AudioBackend
{
	class SDLAudioDevice : AudioDevice
	{
		public SDLAudioDevice(int devNumber)
			: base(devNumber, "SDL Audio Device")
		{
		}

		// In SDL3, everything is just a stream.
		public IntPtr Stream; // SDL_AudioStream

		// We have to do this ourselves now
		public object Mutex = new object();

		public IAudioSink? Sink = null;

		public void Callback(Span<byte> data) => Sink?.Callback(data);
	}

	int _nextAudioDevice = 1;
	Dictionary<int, SDLAudioDevice> _audioDevices = new Dictionary<int, SDLAudioDevice>();

	/* SDL_AudioInit and SDL_AudioQuit were completely removed
	* in SDL3, which means we have to do this always regardless. */

	public override bool InitializeDriver(string? driverName)
	{
		string? originalDriver = Environment.GetEnvironmentVariable("SDL_AUDIO_DRIVER");

		if (driverName != null)
			Environment.SetEnvironmentVariable("SDL_AUDIO_DRIVER", driverName);

		bool ret = SDL.InitSubSystem(SDL.InitFlags.Audio);

		/* clean up our dirty work, or empty the var */
		if (driverName != null)
			Environment.SetEnvironmentVariable("SDL_AUDIO_DRIVER", originalDriver);

		// force poll for audio devices
		GetAudioDeviceInformation();

		/* forward any error, if any */
		return ret;
	}

	public override void QuitDriver()
	{
		SDL.QuitSubSystem(SDL.InitFlags.Audio);
		_devices = null;
	}

	/* ---------------------------------------------------------- */
	/* drivers */

	int GetAudioDriverCount()
	{
		return SDL.GetNumAudioDrivers();
	}

	string GetAudioDriverName(int i)
	{
		return SDL.GetAudioDriver(i) ?? "error";
	}

	public override IEnumerable<AudioDriver> EnumerateDrivers()
	{
		for (int i = 0, l = GetAudioDriverCount(); i < l; i++)
			yield return new AudioDriver(this, GetAudioDriverName(i));
	}

	/* --------------------------------------------------------------- */

	uint[]? _devices = null;

	int GetAudioDeviceInformation()
	{
		_devices = SDL.GetAudioPlaybackDevices(out var deviceCount);

		if (_devices == null)
			throw new Exception("GetAudioPlaybackDevices failed");

		// I don't think this ever happens, but because we're returned both the array
		// and, independently, the count, better safe than sorry.

		if (deviceCount < _devices.Length)
			_devices = _devices.Slice(0, deviceCount).ToArray();

		if (deviceCount > _devices.Length)
		{
			uint[] moreDevices = new uint[deviceCount];

			_devices.CopyTo(moreDevices, 0);
			for (int i=_devices.Length; i < moreDevices.Length; i++)
				moreDevices[i] = uint.MaxValue;

			_devices = moreDevices;
		}

		return _devices.Length;
	}

	public override string GetDeviceName(int i)
	{
		if (i >= int.MaxValue) return "";
		if (_devices == null) return "";
		if (i >= _devices.Length) return "";

		return SDL.GetAudioDeviceName(_devices[i]) ?? "";
	}

	public override IEnumerable<AudioDevice> EnumerateDevices()
	{
		if (_devices == null)
			yield break;

		for (int i=0; i < _devices.Length; i++)
			yield return new AudioDevice(i, GetDeviceName(i));
	}

	/* -------------------------------------------------------- */

	unsafe void AudioCallback(IntPtr userdata, IntPtr stream, int additionalAmount, int totalAamount)
	{
		int deviceIndex = (int)userdata;

		if (!_audioDevices.TryGetValue(deviceIndex, out var dev))
			return;

		if (additionalAmount > 0)
		{
			byte *data = stackalloc byte[additionalAmount];

			lock (dev.Mutex)
				dev.Callback(new Span<byte>(data, additionalAmount));

			SDL.PutAudioStreamData(stream, (IntPtr)data, additionalAmount);
		}
	}

	public override AudioDevice OpenDevice(int deviceID, AudioSpecs desired, out AudioSpecs obtained)
	{
		if (_devices == null)
			throw new Exception("Devices list not initialized");

		int devNumber = _nextAudioDevice++;

		var dev = new SDLAudioDevice(devNumber);

		_audioDevices[devNumber] = dev;

		dev.Sink = desired.Sink;

		SDL.AudioFormat format;

		switch (desired.Bits)
		{
			case 8: format = SDL.AudioFormat.AudioU8; break;
			default:
			case 16: format = SDL.AudioFormat.AudioS16LE; break;
			case 32: format = SDL.AudioFormat.AudioS32LE; break;
		}

		var sdlDesired =
			new SDL.AudioSpec()
			{
				Freq = desired.Frequency,
				Format = format,
				Channels = desired.Channels,
			};

		try
		{
			// As it turns out, SDL is still just a shell script in disguise, and requires you to
			// pass everything as strings in order to change behavior. As for why they don't just
			// include this in the spec structure anymore is beyond me.
			SDL.SetHint(SDL.Hints.AudioDeviceSampleFrames, desired.BufferSizeSamples.ToString());

			uint sdlDeviceID = (deviceID == DefaultID || deviceID >= _devices.Length)
				? SDL.AudioDeviceDefaultPlayback
				: _devices![deviceID];

			dev.Stream = SDL.OpenAudioDeviceStream(
				sdlDeviceID,
				sdlDesired,
				AudioCallback,
				devNumber);

			// reset this before checking if opening succeeded
			SDL.ResetHint(SDL.Hints.AudioDeviceSampleFrames);

			if (dev.Stream == IntPtr.Zero)
				throw new Exception("Device initialization failed");

			// PAUSE!
			SDL.PauseAudioDevice(SDL.GetAudioStreamDevice(dev.Stream));

			// lolwut
			obtained = desired;

			// Retrieve the actual buffer size SDL is using (i.e., don't lie to the user)
			SDL.GetAudioDeviceFormat(sdlDeviceID, out var xyzzy, out int samples);

			obtained.BufferSizeSamples = unchecked((ushort)samples);

			return dev;
		}
		catch
		{
			if (dev != null)
				CloseDevice(dev);

			throw;
		}
	}

	public override void CloseDevice(AudioDevice device)
	{
		if (device is SDLAudioDevice sdlDevice)
		{
			if (sdlDevice.Stream != IntPtr.Zero)
				SDL.DestroyAudioStream(sdlDevice.Stream);
		}
	}

	public override void PauseDevice(AudioDevice device, bool paused)
	{
		if (device is SDLAudioDevice sdlDevice)
		{
			if (paused)
				SDL.PauseAudioDevice(SDL.GetAudioStreamDevice(sdlDevice.Stream));
			else
				SDL.ResumeAudioDevice(SDL.GetAudioStreamDevice(sdlDevice.Stream));
		}
	}

	public override bool Initialize()
	{
		if (!SDLLifetime.Initialize())
			return false;

		return true;
	}

	public override void Quit()
	{
		// the subsystem quitting is handled by the quit driver function
		SDLLifetime.Quit();
	}
}