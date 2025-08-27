using System;
using System.Collections.Generic;

namespace ChasmTracker.SDLBackends;

using System.Text.RegularExpressions;
using System.Threading;
using ChasmTracker.Audio;
using ChasmTracker.Utility;

using SDL3;

public class SDLAudioBackend : AudioBackend
{
	class SDLAudioDevice : AudioDevice
	{
		public SDLAudioDevice(SDLAudioBackend backend, int devNumber, string name = "SDL Audio Device")
			: base(backend, devNumber, name)
		{
		}

		// In SDL3, everything is just a stream.
		public IntPtr Stream; // SDL_AudioStream

		// We have to do this ourselves now
		public object Mutex = new object();

		public IAudioSink? Sink = null;

		// Prevent garbage collection. We give this to SDL, and SDL expects to be able to
		// keep calling it indefinitely.
		public SDL.AudioStreamCallback? SDLCallback;

		public void Callback(Span<byte> data) => Sink?.Callback(data);
	}

	int _nextAudioDevice = 1;
	Dictionary<int, SDLAudioDevice> _audioDevices = new Dictionary<int, SDLAudioDevice>();

	/* SDL_AudioInit and SDL_AudioQuit were completely removed
	* in SDL3, which means we have to do this always regardless. */

	public override bool InitializeDriver(string? driverName)
	{
		bool ret;

		using (new SDLHintScope(SDL.Hints.AudioDriver, driverName ?? "default"))
			 ret = SDL.InitSubSystem(SDL.InitFlags.Audio);

		// force poll for audio devices
		GetAudioDeviceInformation();

		/* forward any error, if any */
		return ret;
	}

	public override void QuitDriver()
	{
		SDL.QuitSubSystem(SDL.InitFlags.Audio);
		_outputDevices = null;
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

	uint[]? _outputDevices = null;
	uint[]? _inputDevices = null;

	int GetAudioDeviceInformation()
	{
		_outputDevices = SDL.GetAudioPlaybackDevices(out var outputDeviceCount);
		_inputDevices = SDL.GetAudioRecordingDevices(out var inputDeviceCount);

		if (_outputDevices == null)
			throw new Exception("GetAudioPlaybackDevices failed");

		// I don't think this is ever actually needed, but because we're returned both the array
		// and, independently, the count, better safe than sorry.

		void EnsureArraySize(ref uint[]? array, int count)
		{
			array ??= Array.Empty<uint>();

			int oldCount = array.Length;

			Array.Resize(ref array, count);

			for (int i = oldCount; i < count; i++)
				array[i] = uint.MaxValue;
		}

		EnsureArraySize(ref _outputDevices, outputDeviceCount);
		EnsureArraySize(ref _inputDevices, inputDeviceCount);

		return _outputDevices!.Length;
	}

	public override string GetDeviceName(int i)
	{
		var devices = i.HasBitSet(CaptureDevice) ? _inputDevices : _outputDevices;

		i &= DeviceIDMask;

		if (devices == null) return "";
		if (i >= devices.Length) return "";

		return SDL.GetAudioDeviceName(devices[i]) ?? "";
	}

	public override IEnumerable<AudioDevice> EnumerateDevices(AudioBackendCapabilities capabilities)
	{
		var devices = capabilities.HasAllFlags(AudioBackendCapabilities.Input) ? _inputDevices : _outputDevices;

		if (devices == null)
			yield break;

		for (int i=0; i < devices.Length; i++)
			yield return new SDLAudioDevice(this, i, GetDeviceName(i));
	}

	/* -------------------------------------------------------- */

	/* XXX need to adapt this callback for input devices */
	unsafe void AudioOutputCallback(IntPtr userdata, IntPtr stream, int additionalAmount, int totalAmount)
	{
		int deviceIndex = (int)userdata;

		if (!_audioDevices.TryGetValue(deviceIndex, out var dev))
			return;

		Assert.IsTrue(dev.Stream == stream, "dev.Stream == stream", "streams should never differ");

		if (additionalAmount <= 0)
			return;

		byte *data = stackalloc byte[additionalAmount];

		lock (dev.Mutex)
			dev.Callback(new Span<byte>(data, additionalAmount));

		SDL.PutAudioStreamData(stream, (IntPtr)data, additionalAmount);
	}

	unsafe void AudioInputCallback(IntPtr userdata, IntPtr stream, int additionalAmount, int totalAmount)
	{
		int deviceIndex = (int)userdata;

		if (!_audioDevices.TryGetValue(deviceIndex, out var dev))
			return;

		Assert.IsTrue(dev.Stream == stream, "dev.Stream == stream", "streams should never differ");

		if (additionalAmount <= 0)
			return;

		byte *data = stackalloc byte[additionalAmount];

		int len = SDL.GetAudioStreamData(stream, (IntPtr)data, additionalAmount);

		lock (dev.Mutex)
			dev.Callback(new Span<byte>(data, additionalAmount));
	}

	public override AudioDevice OpenDevice(int deviceID, AudioSpecs desired, out AudioSpecs obtained)
	{
		if (_outputDevices == null)
			throw new Exception("Devices list not initialized");

		int devNumber = _nextAudioDevice++;

		var dev = new SDLAudioDevice(this, devNumber);

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
			bool capture = deviceID.HasBitSet(CaptureDevice);

			deviceID &= DeviceIDMask;

			var devices = capture ? _inputDevices : _outputDevices;

			uint sdlDeviceID = (devices == null || deviceID == DefaultID || deviceID >= devices.Length)
				? (capture ? SDL.AudioDeviceDefaultRecording : SDL.AudioDeviceDefaultPlayback)
				: _outputDevices![deviceID];

			using (new SDLHintScope(SDL.Hints.AudioDeviceSampleFrames, desired.BufferSizeSamples.ToString()))
			{
				dev.SDLCallback = capture ? AudioInputCallback : AudioOutputCallback;

				dev.Stream = SDL.OpenAudioDeviceStream(
					sdlDeviceID,
					sdlDesired,
					dev.SDLCallback,
					devNumber);

				if (dev.Stream == IntPtr.Zero)
					throw new Exception("Device initialization failed");
			}

			// For the most part we can just copy everything
			obtained = desired;

			/* Retrieve the actual buffer size SDL is using (i.e., don't lie to the user)
			 * This can also improve speeds since we won't have to deal with different
			 * buffer sizes clashing ;) */
			if (SDL.GetAudioDeviceFormat(sdlDeviceID, out var xyzzy, out int samples))
				obtained.BufferSizeSamples = unchecked((ushort)samples);

			return dev;
		}
		catch
		{
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