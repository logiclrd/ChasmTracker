using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace ChasmTracker.Playback;

using ChasmTracker.Audio;
using ChasmTracker.Configurations;
using ChasmTracker.Events;
using ChasmTracker.MIDI;
using ChasmTracker.Pages;
using ChasmTracker.Songs;
using ChasmTracker.Utility;

public static class AudioPlayback
{
	public const int DefaultSampleRate = 48000;
	public const int DefaultBufferSize = 1024;
	public const int DefaultChannelLimit = 128;

	public const uint SamplesPlayedInit = uint.MaxValue - 1; /* for a click noise on init */

	static string? s_driverName;
	static string? s_deviceName;
	static int s_deviceID;

	public static string AudioDriver => s_driverName ?? "unknown";
	public static string? AudioDevice => s_deviceName;
	public static int AudioDeviceID;

	public static List<AudioDevice> AudioDeviceList = new List<AudioDevice>();

	public static AudioDevice? CurrentAudioDevice = null;

	public static AudioBackend[] AudioBackends = typeof(AudioBackend).Assembly
		.GetTypes()
		.Where(t => typeof(AudioBackend).IsAssignableFrom(t) && !t.IsAbstract)
		.Select(t => (AudioBackend)Activator.CreateInstance(t)!)
		.ToArray();

	public static List<AudioBackend> InitializedBackends = new List<AudioBackend>();

	public static AudioBackend? Backend;

	// ------------------------------------------------------------------------------------------------------------

	static AudioPlayback()
	{
		Configuration.RegisterConfigurable(new AudioConfigurationThunk());
	}

	class AudioConfigurationThunk : IConfigurable<AudioConfiguration>
	{
		public void SaveConfiguration(AudioConfiguration config) => AudioPlayback.SaveConfiguration(config);
		public void LoadConfiguration(AudioConfiguration config) => AudioPlayback.LoadConfiguration(config);
	}

	static void LoadConfiguration(AudioConfiguration config)
	{
		s_driverName = config.Driver;
		s_deviceName = config.Device;
	}

	static void SaveConfiguration(AudioConfiguration config)
	{
		config.Driver = s_driverName;
		config.Device = s_deviceName;
	}

	public static void Reset()
	{
		// SNDMIX: These are flags for playback control
		RampingSamples = 64;
		VULeft = 0;
		VURight = 0;
		DryLOfsVol = 0;
		DryROfsVol = 0;
		MaxVoices = 32; // ITT it is 1994

		/* This is intentionally crappy quality, so that it's very obvious if it didn't get initialized */
		MixFlags = 0;
		MixFrequency = 4000;
		MixBitsPerSample = 8;
		MixChannels = 1;
	}

	// ------------------------------------------------------------------------------------------------------------
	// drivers

	public static List<AudioDriver> FullDrivers = new List<AudioDriver>();

	static void CreateDriversList()
	{
		// should always be at the end
		AudioDriver? disk = null;
		AudioDriver? dummy = null;

		// Reset the drivers list
		FullDrivers.Clear();

		foreach (var backend in InitializedBackends)
		{
			foreach (var driver in backend.EnumerateDrivers())
			{
				if (driver.Name == "dummy")
					dummy = driver;
				else if (driver.Name == "disk")
					disk = driver;
				else
				{
					// Skip SDL 2 waveout driver; we have our own implementation
					// and the SDL 2's driver seems to randomly want to hang on
					// exit
					// We also skip SDL2's directsound driver since it only
					// supports DirectX 8 and above, while our builtin driver
					// supports DirectX 5 and above.
					if ((driver.Name == "winmm") || (driver.Name == "directsound"))
						continue;
				}

				if (FullDrivers.Any(entry => entry.Name == driver.Name))
					continue;

				FullDrivers.Add(driver);
			}
		}

		if (disk != null)
			FullDrivers.Add(disk);
		if (dummy != null)
			FullDrivers.Add(dummy);
	}

	// ------------------------------------------------------------------------
	// playback

	class AudioPlaybackSink : IAudioSink
	{
		public void Callback(Span<byte> data)
		{
			AudioCallback(data);
		}
	}

	static void AudioCallback(Span<byte> stream)
	{
		var wasRow = Song.CurrentSong.Row;
		var wasPat = Song.CurrentSong.CurrentOrder;

		stream.Clear();

		if (stream.Length == 0)
		{
			if ((Status.CurrentPage is WaterfallPage) || (Status.VisualizationStyle == TrackerVisualizationStyle.FFT))
				AllPages.Waterfall.VisualizationWork8Mono(Span<sbyte>.Empty);

			StopUnlocked(false);
		}
		else
		{
			if (SamplesPlayed >= SamplesPlayedInit)
			{
				for (int i = 0; i < stream.Length; i++)
					stream[i] = 0x80;

				unchecked
				{
					SamplesPlayed++; // will loop back to 0
				}

				return;
			}

			int n;

			if (Song.CurrentSong.Flags.HasAllFlags(SongFlags.EndReached))
				n = 0;
			else
			{
				n = SongRenderer.Read(Song.CurrentSong, stream);

				if (n == 0)
				{
					if ((Status.CurrentPage is WaterfallPage) || (Status.VisualizationStyle == TrackerVisualizationStyle.FFT))
						AllPages.Waterfall.VisualizationWork8Mono(Span<sbyte>.Empty);

					StopUnlocked(false);
				}

				unchecked
				{
					SamplesPlayed += (uint)n;
				}
			}

			if (n > 0)
			{
				var pretendShorts = MemoryMarshal.Cast<byte, short>(stream.Slice(0, n * AudioSampleSize));

				pretendShorts.CopyTo(AudioBuffer);

				/* convert 8-bit unsigned to signed by XORing the high bit */
				if (AudioOutputBits == 8)
				{
					var pretendBytes = MemoryMarshal.Cast<short, byte>(AudioBuffer);

					for (int i = 0; i < n * 2; i++)
						pretendBytes[i] ^= 0x80;
				}

				if ((Status.CurrentPage is WaterfallPage) || (Status.VisualizationStyle == TrackerVisualizationStyle.FFT))
				{
					// I don't really like this...
					switch (AudioOutputBits)
					{
						case 8:
							if (AudioOutputChannels == 2)
								AllPages.Waterfall.VisualizationWork8Stereo(MemoryMarshal.Cast<short, sbyte>(AudioBuffer));
							else
								AllPages.Waterfall.VisualizationWork8Mono(MemoryMarshal.Cast<short, sbyte>(AudioBuffer));
							break;
						case 16:
							if (AudioOutputChannels == 2)
								AllPages.Waterfall.VisualizationWork16Stereo(AudioBuffer);
							else
								AllPages.Waterfall.VisualizationWork16Mono(AudioBuffer);
							break;
						case 32:
							if (AudioOutputChannels == 2)
								AllPages.Waterfall.VisualizationWork32Stereo(MemoryMarshal.Cast<short, int>(AudioBuffer));
							else
								AllPages.Waterfall.VisualizationWork32Mono(MemoryMarshal.Cast<short, int>(AudioBuffer));
							break;
					}
				}

				if (Song.CurrentSong.NumVoices > MaxChannelsUsed)
					MaxChannelsUsed = Math.Min(Song.CurrentSong.NumVoices, MaxVoices);
			}
		}

		AudioWriteOutCount++;

		if (AudioWriteOutCount > AudioBuffersPerSecond)
			AudioWriteOutCount = 0;
		else if (wasPat == Song.CurrentSong.CurrentOrder && wasRow == Song.CurrentSong.Row
				&& !MIDIEngine.NeedFlush())
		{
			/* skip it */
			return;
		}

		PlayingRow = Song.CurrentSong.Row;
		PlayingPattern = Song.CurrentSong.CurrentPattern;
		PlayingChannels = Math.Min(Song.CurrentSong.NumVoices, Song.CurrentSong.Voices.Length);

		/* send at end */
		EventHub.PushEvent(new PlaybackEvent());
	}

	// ------------------------------------------------------------------------
	// control

	public static AudioPlaybackMode Mode
	{
		get
		{
			if (Song.CurrentSong.Flags.HasAllFlags(SongFlags.EndReached | SongFlags.Paused))
				return AudioPlaybackMode.Stopped;
			if (Song.CurrentSong.Flags.HasAllFlags(SongFlags.Paused))
				return AudioPlaybackMode.SingleStep;
			if (Song.CurrentSong.Flags.HasAllFlags(SongFlags.PatternPlayback))
				return AudioPlaybackMode.PatternLoop;
			return AudioPlaybackMode.Playing;
		}
	}

	public static MixFlags MixFlags;

	public static bool IsPlaying => Mode.HasAnyFlag(AudioPlaybackMode.Playing | AudioPlaybackMode.PatternLoop);

	public static bool Surround
	{
		get => !MixFlags.HasAllFlags(MixFlags.NoSurround);
		set
		{
			if (value)
				MixFlags &= ~MixFlags.NoSurround;
			else
				MixFlags |= MixFlags.NoSurround;

			AudioSettings.SurroundEffect = value;
		}
	}

	public static void FlipStereo()
	{
		MixFlags ^= MixFlags.ReverseStereo;
	}

	static int s_currentPlayChannel = 1;
	static bool s_multichannelMode;

	public static int CurrentPlayChannel => s_currentPlayChannel;
	public static bool MultichannelMode => s_multichannelMode;

	public static int PlayingRow;
	public static int PlayingPattern;
	public static int PlayingChannels;

	public static int CurrentOrder
	{
		get => Song.CurrentSong.CurrentOrder;
		set
		{
			lock (LockScope())
				Song.CurrentSong.CurrentOrder = value;
		}
	}

	public static uint SamplesPlayed;
	public static int MaxChannelsUsed;

	public static short[]? AudioBuffer = null;
	public static int AudioBufferSamples = 0; /* multiply by audio_sample_size to get bytes */

	public static int AudioOutputChannels = 2;
	public static int AudioOutputBits = 16;

	public static int AudioSampleSize;
	public static int AudioBuffersPerSecond;
	public static int AudioWriteOutCount;

	public static int MixStat; // number of channels being mixed (not really used)
	public static int BufferCount; // number of samples to mix per tick

	// mixer stuff -----------------------------------------------------------
	// TODO: public MixFlags MixFlags;
	public static int MixFrequency;
	public static int MixBitsPerSample;
	public static int MixChannels;
	public static SourceMode MixInterpolation;
	public static int RampingSamples; // default: 64
	public static int MaxVoices;
	public static int VULeft;
	public static int VURight;
	public static int DryROfsVol; /* un-globalized, didn't care enough */
	public static int DryLOfsVol; /* to find out what these do  -paper */
	// -----------------------------------------------------------------------

	// Returns the max value in dBs, scaled as 0 = -40dB and 128 = 0dB.
	public static void GetVUMeter(out int left, out int right)
	{
		left = Decibel.dB_s(40, VULeft / 256.0, 0.0);
		right = Decibel.dB_s(40, VURight / 256.0, 0.0);
	}

	public const int VolumeRampLength = 146; // 1.46ms == 64 samples at 44.1kHz

	public static TimeSpan CurrentTime => TimeSpan.FromSeconds(SamplesPlayed / (double)MixFrequency);

	public static AudioPlaybackState SaveState()
	{
		var state = new AudioPlaybackState();

		foreach (var field in typeof(AudioPlayback).GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
			state.Values[field.Name] = field.GetValue(null);

		return state;
	}

	public static void RestoreState(AudioPlaybackState state)
	{
		foreach (var field in typeof(AudioPlayback).GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
			field.SetValue(null, state.Values[field.Name]);
	}

	public static void InitializeModPlug()
	{
		using (LockScope())
		{
			MaxVoices = AudioSettings.ChannelLimit;

			SetResamplingMode(AudioSettings.InterpolationMode);

			if (AudioSettings.NoRamping)
				MixFlags |= MixFlags.NoRamping;
			else
				MixFlags &= ~MixFlags.NoRamping;

			// disable the S91 effect? (this doesn't make anything faster, it
			// just sounds better with one woofer.)
			Surround = AudioSettings.SurroundEffect;

			// update midi queue configuration
			int audioBufferLength = AudioBuffer?.Length ?? 0;

			audioBufferLength = audioBufferLength * AudioOutputBits / 16;

			MIDIEngine.QueueAlloc(audioBufferLength, AudioSampleSize, MixFrequency);

			// timelimit the playback_update() calls when midi isn't actively going on
			{
				int divisor = audioBufferLength * 8 * AudioSampleSize;

				AudioBuffersPerSecond = (divisor != 0) ? (MixFrequency / divisor) : 0;

				if (AudioBuffersPerSecond > 1)
					AudioBuffersPerSecond--;
			}

			Song.CurrentSong.InitializeMIDI(new MIDIEngine());
		}
	}

	public static bool SetResamplingMode(SourceMode mode)
	{
		if (!Enum.IsDefined(mode))
			throw new ArgumentException("invalid value", nameof(mode));

		MixInterpolation = mode;

		return true;
	}

	public static void ChangeCurrentPlayChannel(int relative, bool wraparound)
	{
		s_currentPlayChannel += relative;
		if (wraparound)
		{
			if (s_currentPlayChannel < 1)
				s_currentPlayChannel = Constants.MaxChannels;
			else if (s_currentPlayChannel > Constants.MaxChannels)
				s_currentPlayChannel = 1;
		}
		else
			s_currentPlayChannel = s_currentPlayChannel.Clamp(1, Constants.MaxChannels);

		Status.FlashText("Using channel " + s_currentPlayChannel + " for playback");
	}

	public static void ToggleMultichannelMode()
	{
		s_multichannelMode = !s_multichannelMode;

		Status.FlashText(
			s_multichannelMode ?
			"Multichannel playback enabled" :
			"Multichannel playback disabled");
	}

	class Scope : IDisposable
	{
		bool _active = true;

		public Scope() { LockAudio(); }
		public void Dispose() { if (_active) { UnlockAudio(); _active = false; } }
	}

	public static IDisposable LockScope() => new Scope();

	static void LockAudio()
	{
		// TODO
	}

	static void UnlockAudio()
	{
		// TODO
	}

	public static void SingleStep(int patno, int row)
	{
		var pattern = Song.CurrentSong.GetPattern(patno);

		if ((pattern == null) || (row >= pattern.Rows.Count))
			return;

		for (int i = 1; i <= Constants.MaxChannels; i++)
		{
			ref var curNote = ref pattern[row][i];
			ref var cx = ref Song.CurrentSong.Voices[i - 1];

			if (cx.Flags.HasAllFlags(ChannelFlags.Mute))
				continue; /* ick */

			int vol;

			if (curNote.VolumeEffect == VolumeEffects.Volume)
				vol = curNote.VolumeParameter;
			else
				vol = KeyJazz.DefaultVolume;

			// look familiar? this is modified slightly from pattern_editor_insert
			// (and it is wrong for the same reason as described there)
			int smp = curNote.Instrument;
			int ins = curNote.Instrument;

			if (Song.CurrentSong.IsInstrumentMode)
			{
				if (ins < 1)
					ins = KeyJazz.NoInstrument;
				smp = -1;
			}
			else
			{
				if (smp < 1)
					smp = KeyJazz.NoInstrument;
				ins = -1;
			}

			Song.CurrentSong.KeyRecord(smp, ins, curNote.Note,
				vol, i, curNote.Effect, curNote.Parameter);
		}
	}

	// ------------------------------------------------------------------------------------------------------------

	// this should be called with the audio LOCKED
	static void ResetPlayState()
	{
		Array.Clear(MIDIEngine.BendHit);
		Array.Clear(MIDIEngine.LastBendHit);
		KeyJazz.ResetChannelNoteMappings();

		// turn this crap off
		MixFlags &= ~(MixFlags.NoBackwardJumps | MixFlags.DirectToDisk);

		// TODO:
		Song.CurrentSong.OPLReset(); // gruh?

		Song.CurrentSong.SetCurrentOrder(0);

		Song.CurrentSong.RepeatCount = 0;
		Song.CurrentSong.BufferCount = 0;

		Song.CurrentSong.Flags &= ~(SongFlags.Paused | SongFlags.PatternLoop | SongFlags.EndReached);

		Song.CurrentSong.StopAtOrder = -1;
		Song.CurrentSong.StopAtRow = -1;

		SamplesPlayed = 0;
	}

	public static void StartOnce()
	{
		using (LockScope())
		{
			ResetPlayState();

			MixFlags |= MixFlags.NoBackwardJumps;

			MaxChannelsUsed = 0;

			Song.CurrentSong.RepeatCount = -1; // FIXME do this right

			GeneralMIDI.SendSongStartCode(Song.CurrentSong);
		}

		Page.NotifySongModeChangedGlobal();

		Song.CurrentSong.ResetPlaymarks();
	}

	public static void Start()
	{
		using (LockScope())
		{
			ResetPlayState();
			MaxChannelsUsed = 0;

			GeneralMIDI.SendSongStartCode(Song.CurrentSong);
		}

		Page.NotifySongModeChangedGlobal();

		Song.CurrentSong.ResetPlaymarks();
	}

	public static void Pause()
	{
		using (LockScope())
		{
			// Highly unintuitive, but SONG_PAUSED has nothing to do with pause.
			if (!Song.CurrentSong.Flags.HasAllFlags(SongFlags.Paused))
				Song.CurrentSong.Flags ^= SongFlags.EndReached;
		}

		Page.NotifySongModeChangedGlobal();
	}

	public static void Stop()
	{
		using (LockScope())
		{
			StopUnlocked(false);
		}


		Page.NotifySongModeChangedGlobal();
	}

	[ThreadStatic]
	static byte[]? s_midiPacket;

	public static void StopUnlocked(bool quitting)
	{
		s_midiPacket ??= new byte[4];

		if (Song.CurrentSong.MIDIPlaying)
		{
			// shut off everything; not IT like, but less annoying
			for (int chan = 0; chan < Constants.MaxChannels; chan++)
			{
				if (Song.CurrentSong.MIDINoteTracker[chan] != 0)
				{
					for (int j = 0; j < Constants.MaxMIDIChannels; j++)
					{
						MIDITranslator.ProcessMIDIMacro(Song.CurrentSong, chan,
							Song.CurrentSong.MIDIConfig.NoteOff,
							0, Song.CurrentSong.MIDINoteTracker[chan], 0, j);
					}

					s_midiPacket[0] = (byte)(0x80 + chan);
					s_midiPacket[1] = (byte)Song.CurrentSong.MIDINoteTracker[chan];

					Song.CurrentSong.MIDISend(s_midiPacket.Slice(0, 2), nchan: 0, fake: false);
				}
			}
		}

		if (Song.CurrentSong.MIDIPlaying)
		{
			for (int j = 0; j < Constants.MaxMIDIChannels; j++)
			{
				s_midiPacket[0] = (byte)(0xe0 + j);
				s_midiPacket[1] = 0;
				Song.CurrentSong.MIDISend(s_midiPacket.Slice(0, 2), nchan: 0, fake: false);

				s_midiPacket[0] = (byte)(0xb0 + j); // channel mode message
				s_midiPacket[1] = 0x78;   // all sound off
				s_midiPacket[2] = 0;
				Song.CurrentSong.MIDISend(s_midiPacket.Slice(0, 3), nchan: 0, fake: false);

				s_midiPacket[1] = 0x79;   // reset all controllers
				Song.CurrentSong.MIDISend(s_midiPacket.Slice(0, 3), nchan: 0, fake: false);

				s_midiPacket[1] = 0x7b;   // all notes off
				Song.CurrentSong.MIDISend(s_midiPacket.Slice(0, 3), nchan: 0, fake: false);
			}

			MIDITranslator.ProcessMIDIMacro(Song.CurrentSong, 0, Song.CurrentSong.MIDIConfig.Stop, 0, 0, 0, 0); // STOP!
			MIDIEngine.SendFlush();

			Song.CurrentSong.MIDIPlaying = false;
		}

		Song.CurrentSong.OPLReset(); // Also stop all OPL sounds
		GeneralMIDI.Reset(Song.CurrentSong, quitting);
		GeneralMIDI.SendSongStopCode(Song.CurrentSong);

		Array.Clear(Song.CurrentSong.MIDILastRow);
		Song.CurrentSong.MIDILastRowNumber = -1;

		Array.Clear(Song.CurrentSong.MIDINoteTracker);
		Array.Clear(Song.CurrentSong.MIDIVolTracker);
		Array.Clear(Song.CurrentSong.MIDIInsTracker);
		Array.Clear(Song.CurrentSong.MIDIWasProgram);
		Array.Clear(Song.CurrentSong.MIDIWasBankLo);
		Array.Clear(Song.CurrentSong.MIDIWasBankHi);

		AllPages.PatternEditor.PlaybackTracing = AllPages.PatternEditor.MIDIPlaybackTracing;

		ResetPlayState();

		// Modplug doesn't actually have a "stop" mode, but if SONG_ENDREACHED is set, csf_read just returns.
		Song.CurrentSong.Flags |= SongFlags.Paused | SongFlags.EndReached;

		VULeft = 0;
		VURight = 0;

		if (AudioBuffer != null)
			Array.Clear(AudioBuffer);
	}

	public static void LoopPattern(int pattern, int row)
	{
		using (LockScope())
		{
			ResetPlayState();

			MaxChannelsUsed = 0;
			Song.CurrentSong.LoopPattern(pattern, row);
		}

		Page.NotifySongModeChangedGlobal();

		Song.CurrentSong.ResetPlaymarks();
	}

	public static void StartAtOrder(int order, int row)
	{
		using (LockScope())
		{
			ResetPlayState();

			Song.CurrentSong.SetCurrentOrder(order);
			Song.CurrentSong.BreakRow = row;
			MaxChannelsUsed = 0;

			GeneralMIDI.SendSongStartCode(Song.CurrentSong);

			// TODO: GM_SendSongPositionCode(calculate the number of 1/16 notes)
		}

		Page.NotifySongModeChangedGlobal();

		Song.CurrentSong.ResetPlaymarks();
	}

	public static void StartAtPattern(int pattern, int row)
	{
		if (pattern < 0 || pattern > 199)
			return;

		int n = Song.CurrentSong.GetNextOrderForPattern(pattern);

		if (n > -1)
			StartAtOrder(n, row);
		else
			LoopPattern(pattern, row);
	}

	public static void StopSample(Song csf, SongSample smp)
	{
		if (!smp.HasData)
			return;

		for (int i = 0; i < csf.Voices.Length; i++)
		{
			ref var v = ref csf.Voices[i];

			if ((v.Sample == smp) || (v.CurrentSampleData.RawData == smp.RawData))
			{
				v.Note = v.NewNote = 1;
				v.NewInstrumentNumber = 0;
				v.FadeOutVolume = 0;
				v.Flags |= ChannelFlags.KeyOff | ChannelFlags.NoteFade;
				v.Frequency = 0;
				v.Position = SamplePosition.Zero;
				v.Length = 0;
				v.LoopStart = 0;
				v.LoopEnd = 0;
				v.ROfs = v.LOfs = 0;
				v.CurrentSampleData = SampleWindow.Empty;
				v.Sample = null;
				v.Instrument = null;
				v.LeftVolume = v.RightVolume = 0;
				v.LeftVolumeNew = v.RightVolumeNew = 0;
				v.LeftRamp = v.RightRamp = 0;
			}
		}
	}

	public static void InitPlayer(bool reset)
	{
		if (MaxVoices > Constants.MaxVoices)
			MaxVoices = Constants.MaxVoices;

		MixFrequency = MixFrequency.Clamp(4000, Constants.MaxSampleRate);
		RampingSamples = MixFrequency * VolumeRampLength / 100000;

		if (RampingSamples < 8)
			RampingSamples = 8;

		if (MixFlags.HasAllFlags(MixFlags.NoRamping))
			RampingSamples = 2;

		DryROfsVol = DryLOfsVol = 0;

		if (reset)
		{
			VULeft = 0;
			VURight = 0;
		}

		InitializeEQ(reset, MixFrequency);

		// I don't know why, but this "if" makes it work at the desired sample rate instead of 4000.
		// the "4000Hz" value comes from csf_reset, but I don't yet understand why the opl keeps that value, if
		// each call to Fmdrv_Init generates a new opl.
		if (MixFrequency != 4000)
			Song.CurrentSong.InitializeOPL(MixFrequency);

		GeneralMIDI.Reset(Song.CurrentSong, false);
	}

	public static void SetWaveConfig(int rate, int bits, int channels)
	{
		bool reset = (MixFrequency != rate)
			|| (MixBitsPerSample != bits)
			|| (MixChannels != channels);

		MixChannels = channels;
		MixFrequency = rate;
		MixBitsPerSample = bits;

		InitPlayer(reset);
	}

	/* --------------------------------------------------------------------------------------------------------- */

	public static void StartAudio()
	{
		if (CurrentAudioDevice != null)
			Backend?.PauseDevice(CurrentAudioDevice, false);
	}

	public static void StopAudio()
	{
		if (CurrentAudioDevice != null)
			Backend?.PauseDevice(CurrentAudioDevice, true);
	}

	/* --------------------------------------------------------------------------------------------------------- */
	/* This is completely horrible! :) */

	static bool s_wasInit = false;

	static bool LookUpDeviceName(string deviceName, out int pDevID)
	{
		if (Backend != null)
		{
			int i = 0;

			foreach (var device in Backend.EnumerateDevices(AudioBackendCapabilities.Output))
			{
				if (device.Name == deviceName)
				{
					pDevID = i;
					return true;
				}

				i++;
			}
		}

		pDevID = -1;
		return false;
	}

	static void CleanUpAudioDevice()
	{
		if (CurrentAudioDevice != null)
		{
			if (Backend != null)
				Backend.CloseDevice(CurrentAudioDevice);

			CurrentAudioDevice = null;

			s_deviceName = null;
			s_deviceID = 0;
		}
	}

	// le fuqing? open_device obtains the lock but doesn't release it?
	class OpenState : IDisposable
	{
		bool _holdsLock;

		public bool Successful;

		public static OpenState False => new OpenState(obtainLock: false) { Successful = false };

		public OpenState(bool obtainLock)
		{
			if (obtainLock)
			{
				LockAudio();
				_holdsLock = true;
			}
		}

		public void Dispose()
		{
			if (_holdsLock)
			{
				UnlockAudio();
				_holdsLock = false;
			}
		}

		public static implicit operator bool(OpenState state) => state.Successful;
	}

	static OpenState OpenDevice(int deviceID, bool verbose)
	{
		CleanUpAudioDevice();

		if (Backend == null)
			return OpenState.False;

		/* if the buffer size isn't a power of two, the dsp driver will punt since it's not nice enough to fix
		 * it for us. (contrast alsa, which is TOO nice and fixes it even when we don't want it to) */
		ushort sizePowerOf2 = 2;
		while (sizePowerOf2 < AudioSettings.BufferSize)
			sizePowerOf2 <<= 1;

		/* round to the nearest (kept for compatibility) */
		if (sizePowerOf2 != AudioSettings.BufferSize
			&& (sizePowerOf2 - AudioSettings.BufferSize) > (AudioSettings.BufferSize - (sizePowerOf2 >> 1)))
			sizePowerOf2 >>= 1;

		/* This is needed in order to coax alsa into actually respecting the buffer size, since it's evidently
		 * ignored entirely for "fake" devices such as "default" -- which SDL happens to use if no device name
		 * is set. (see SDL_alsa_audio.c: http://tinyurl.com/ybf398f)
		 * If hw doesn't exist, so be it -- let this fail, we'll fall back to the dummy device, and the
		 * user can pick a more reasonable device later. */

		/* I can't replicate this issue at all, so I'm just gonna comment this out. If it really *is* still an
		 * issue, it can be uncommented.
		 *  - paper */

		//if (s_driverName == "alsa")
		//{
		//	if (Environment.GetEnvironmentVariable("AUDIODEV") == null)
		//		Environment.SetEnvironmentVariable("AUDIODEV", "hw");
		//}

		var desired = new AudioSpecs();

		desired.Frequency = AudioSettings.SampleRate;
		desired.Bits = AudioSettings.Bits;
		desired.Channels = AudioSettings.Channels;
		desired.BufferSizeSamples = sizePowerOf2;
		desired.Sink = new AudioPlaybackSink();

		AudioSpecs? obtained = null;

		if (deviceID != AudioBackend.DefaultID)
		{
			CurrentAudioDevice = Backend.OpenDevice(deviceID, desired, out obtained);

			if (CurrentAudioDevice != null)
			{
				s_deviceName = Backend.GetDeviceName(deviceID);
				s_deviceID = deviceID;
			}
		}

		if (CurrentAudioDevice == null)
		{
			CurrentAudioDevice = Backend.OpenDevice(AudioBackend.DefaultID, desired, out obtained);

			if (CurrentAudioDevice != null)
			{
				s_deviceName = "default";  // ????
				s_deviceID = AudioBackend.DefaultID;
			}
		}

		if ((CurrentAudioDevice == null) || (obtained == null))
		{
			/* oops ! */
			return OpenState.False;
		}

		var openState = new OpenState(obtainLock: true);

		try
		{
			SetWaveConfig(obtained.Frequency, obtained.Bits, obtained.Channels);

			AudioOutputChannels = obtained.Channels;
			AudioOutputBits = obtained.Bits;
			AudioSampleSize = AudioOutputChannels * AudioOutputBits / 8;
			AudioBufferSamples = obtained.BufferSizeSamples;

			if (verbose)
			{
				Log.AppendNewLine();
				Log.AppendTimestampWithUnderline(2, "Audio initialised");
				Log.Append(5, " Using driver '{0}'", s_driverName ?? "<unknown>");
				Log.Append(5, " {0} Hz, {1} bit, {2}", obtained.Frequency, obtained.Bits,
					obtained.Channels == 1 ? "mono" : "stereo");
				Log.Append(5, " Buffer size: {0} samples", obtained.BufferSizeSamples);
			}

			openState.Successful = true;
		}
		catch { }

		return openState;
	}

	static OpenState TryDriver(AudioBackend backendPassed, string driverName, string deviceName, bool verbose)
	{
		var backendRestore = Backend;
		bool succeeded = false;

		try
		{
			Backend = backendPassed;

			if (!Backend.InitializeDriver(driverName))
				return OpenState.False;

			s_driverName = driverName;

			if (LookUpDeviceName(deviceName, out var deviceID))
			{
				// nothing
			}
			else if (string.IsNullOrEmpty(deviceName) || (deviceName == "default"))
				deviceID = AudioBackend.DefaultID;
			else
				return OpenState.False;

			var openState = OpenDevice(deviceID, verbose);

			if (openState.Successful)
			{
				s_wasInit = true;
				AudioBackend.Current = CurrentAudioDevice?.Backend ?? default!;
				AudioBackend.RefreshAudioDeviceList();
				succeeded = true;
			}

			return openState;
		}
		finally
		{
			if (!succeeded)
			{
				Backend?.QuitDriver();

				s_driverName = null;
				Backend = backendRestore;

				AudioBackend.Current = backendRestore;
			}
		}
	}

	static void QuitImpl()
	{
		if (s_wasInit)
		{
			CleanUpAudioDevice();
			s_driverName = null;
			Backend?.QuitDriver();
			s_wasInit = false;
		}
	}

	/* driver == NULL || device == NULL is fine here */
	static bool s_backendsInitialized = false;

	public static bool Initialize(string? driverName, string? deviceName)
	{
		bool success = false;

		Quit();

		/* Use the driver from the config if it exists. */
		if (string.IsNullOrEmpty(driverName))
			driverName = Configuration.Audio.Driver ?? "default";

		if (string.IsNullOrEmpty(deviceName))
			deviceName = Configuration.Audio.Device ?? "";

		if (driverName == "oss")
			driverName = "dsp";
		else if ((driverName == "nosound") || (driverName == "none"))
			driverName = "dummy";
		else if (driverName == "winmm")
			driverName = "waveout";
		else if (driverName == "directsound")
			driverName = "dsound";

		if (string.IsNullOrEmpty(driverName))
		{
			string? n = Environment.GetEnvironmentVariable("SDL_AUDIO_DRIVER");
			if (n != null) driverName = n;
		}

		if (string.IsNullOrEmpty(driverName))
		{
			string? n = Environment.GetEnvironmentVariable("SDL_AUDIODRIVER");
			if (n != null) driverName = n;
		}

		// Initialize all backends (for audio driver listing)
		if (!s_backendsInitialized)
		{
			foreach (var backend in AudioBackends)
				if (backend.Initialize())
					InitializedBackends.Add(backend);

			CreateDriversList();
			s_backendsInitialized = true;
		}

		OpenState? openState = null;

		if (FullDrivers.Any())
		{
			AudioBackend? backendDriver = null;

			if (driverName != null)
			{
				backendDriver = FullDrivers.FirstOrDefault(entry => entry.Name == driverName)?.Backend;

				if (backendDriver != null)
				{
					openState = TryDriver(backendDriver, driverName, deviceName, verbose: true);

					if (!openState)
					{
						openState.Dispose();
						openState = TryDriver(backendDriver, driverName, "", verbose: true);
					}
				}
			}

			if ((openState == null) || !openState)
			{
				openState?.Dispose();

				foreach (var driver in FullDrivers)
				{
					openState = TryDriver(driver.Backend, driver.Name, deviceName, verbose: true);

					if (!openState)
					{
						openState.Dispose();
						openState = TryDriver(driver.Backend, driver.Name, "", verbose: true);
					}

					if (openState)
					{
						success = true;
						break;
					}
				}
			}

			if (!success)
			{
				Log.AppendNewLine();
				Log.Append(4, "Failed to load requested audio driver `{0}`!", driverName ?? "<null>");
			}
		}

		if (success)
		{
			int bufferShorts = (AudioBufferSamples * AudioSampleSize + 1) / 2;

			AudioBuffer = new short[bufferShorts];

			SamplesPlayed = Status.Flags.HasAllFlags(StatusFlags.ClassicMode) ? SamplesPlayedInit : 0;

			openState?.Dispose();

			StartAudio();

			return success;
		}

		// hmmmmm ? :)
		Environment.Exit(0);
		throw new Exception("Initialization failed");
	}

	// deviceID is optional and can be null
	public static bool Reinitialize(int? deviceID)
	{
		if (Status.Flags.HasAnyFlag(StatusFlags.DiskWriterActive | StatusFlags.DiskWriterActiveForPattern))
		{
			/* never allowed */
			return false;
		}

		if (Status.Flags.HasAllFlags(StatusFlags.ClassicMode))
			Stop();

		// if we got a device, cool, otherwise use our device ID,
		// which (fingers crossed!) is the same one as last time.
		var openState = OpenDevice(deviceID ?? s_deviceID, verbose: false);

		openState.Dispose();

		AudioBackend.FlashReinitializedText(openState.Successful);

		return openState.Successful;
	}

	public static void Quit()
	{
		QuitImpl();

		foreach (var backend in InitializedBackends)
			backend.Quit();

		InitializedBackends.Clear();
	}

	/* --------------------------------------------------------------------------------------------------------- */

	public static void InitializeEQ(bool doReset, int mixFrequency)
	{
		int[] pg = new int[4];
		int[] pf = new int[4];

		for (int i = 0; i < 4; i++)
		{
			pg[i] = AudioSettings.EQBands[i].Gain;
			pf[i] = 120 + (i*128 * AudioSettings.EQBands[i].FrequencyIndex * (mixFrequency / 128) / 1024);
		}

		Equalizer.SetGains(pg, pf, doReset, mixFrequency);
	}
}
