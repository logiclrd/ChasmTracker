using System;
using System.IO;

namespace ChasmTracker.Playback;

using ChasmTracker.MIDI;
using ChasmTracker.Pages;
using ChasmTracker.Songs;
using ChasmTracker.Utility;

public class AudioPlayback
{
	public static string SongFileName = "";
	public static string SongBaseName = "";
	public static AudioPlaybackMode Mode;
	public static MixFlags MixFlags;

	public static bool IsPlaying => Mode.HasAnyFlag(AudioPlaybackMode.Playing | AudioPlaybackMode.PatternLoop);

	public static bool Surround
	{
		get => !MixFlags.HasFlag(MixFlags.NoSurround);
		set
		{
			if (value)
				MixFlags &= ~MixFlags.NoSurround;
			else
				MixFlags |= MixFlags.NoSurround;

			AudioSettings.SurroundEffect = value;
		}
	}

	static int s_currentPlayChannel;
	static bool s_multichannelMode;

	public static int CurrentPlayChannel => s_currentPlayChannel;
	public static bool MultichannelMode => s_multichannelMode;

	public static int CurrentRow;
	public static int PlayingPattern;
	public static int CurrentOrder; // TODO: accessors (song_set_current_order)

	public static int SamplesPlayed;
	public static int MaxChannelsUsed;

	public static short[]? AudioBuffer = null;

	public static int AudioOutputChannels = 2;
	public static int AudioOutputBits = 16;

	public static int AudioSampleSize;
	public static int AudioBuffersPerSecond;
	public static int AudioWriteOutCount;

	public static int NumVoices; // how many are currently playing. (POTENTIALLY larger than global max_voices)
	public static int MixStat; // number of channels being mixed (not really used)
	public static int BufferCount; // number of samples to mix per tick

	static string? s_driverName;

	public static string AudioDriver => s_driverName ?? "unknown";

	public static void InitializeModPlug()
	{
		using (LockScope())
		{
			Song.CurrentSong.MaxVoices = AudioSettings.ChannelLimit;

			SetResamplingMode(AudioSettings.InterpolationMode);

			if (AudioSettings.NoRamping)
				MixFlags |= MixFlags.NoRamping;
			else
				MixFlags &= ~MixFlags.NoRamping;

			// disable the S91 effect? (this doesn't make anything faster, it
			// just sounds better with one woofer.)
			Surround = AudioSettings.SurroundEffect;

			// update midi queue configuration
			MIDIEngine.QueueAlloc(AudioBuffer?.Length ?? 0, AudioSampleSize, Song.CurrentSong.MixFrequency);

			// timelimit the playback_update() calls when midi isn't actively going on
			{
				int divisor = (AudioBuffer?.Length ?? 0) * 8 * AudioSampleSize;

				AudioBuffersPerSecond = (divisor != 0) ? (Song.CurrentSong.MixFrequency / divisor) : 0;

				if (AudioBuffersPerSecond > 1)
					AudioBuffersPerSecond--;
			}

			Song.InitializeMIDI(new MIDIEngine());
		}
	}

	public static void SetFileName(string? file)
	{
		if (!string.IsNullOrEmpty(file))
		{
			SongFileName = file;
			SongBaseName = Path.GetFileName(file);
		}
		else
		{
			SongFileName = "";
			SongBaseName = "";
		}
	}

	public static bool SetResamplingMode(SourceMode mode)
	{
		var d = MixFlags & ~(MixFlags.NoResampling | MixFlags.HQResampler | MixFlags.UltraHQSourceMode);

		switch (mode)
		{
			case SourceMode.Nearest: d |= MixFlags.NoResampling; break;
			case SourceMode.Linear: break;
			case SourceMode.Spline: d |= MixFlags.HQResampler; break;
			case SourceMode.Polyphase: d |= MixFlags.HQResampler | MixFlags.UltraHQSourceMode; break;

			default: return false;
		}

		MixFlags = d;

		return true;
	}

	public static void ChangeCurrentPlayChannel(int relative, bool wraparound)
	{
		s_currentPlayChannel += relative;
		if (wraparound)
		{
			if (s_currentPlayChannel < 1)
				s_currentPlayChannel = 64;
			else if (s_currentPlayChannel > 64)
				s_currentPlayChannel = 1;
		}
		else
			s_currentPlayChannel = s_currentPlayChannel.Clamp(1, 64);

		Status.FlashText("Using channel " + s_currentPlayChannel + " for playback");
	}

	public static void Reinitialize(object? floobs)
	{
		// TODO
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

		for (int i = 1; i <= 64; i++)
		{
			ref var curNote = ref pattern[row][i];
			ref var cx = ref Song.CurrentSong.Voices[i - 1];

			if (cx.Flags.HasFlag(ChannelFlags.Mute))
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

			Song.KeyRecord(smp, ins, curNote.Note,
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
		//OPL_Reset(current_song); // gruh?

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

			// TODO
			//GM_SendSongStartCode(Song.CurrentSong)
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

			// TODO
			//GM_SendSongStartCode(current_song);
		}

		Page.NotifySongModeChangedGlobal();

		Song.CurrentSong.ResetPlaymarks();
	}

	public static void Pause()
	{
		using (LockScope())
		{
			// Highly unintuitive, but SONG_PAUSED has nothing to do with pause.
			if (!Song.CurrentSong.Flags.HasFlag(SongFlags.Paused))
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

	public static void StopUnlocked(bool quitting)
	{
		if (Song.CurrentSong.MIDIPlaying)
		{
			byte[] midiPacket = new byte[4];

			// shut off everything; not IT like, but less annoying
			for (int chan = 0; chan < Constants.MaxChannels; chan++)
			{
				if (Song.CurrentSong.MIDINoteTracker[chan] != 0)
				{
					for (int j = 0; j < Constants.MaxMIDIChannels; j++)
					{
						Song.CurrentSong.ProcessMIDIMacro(chan,
							Song.CurrentSong.MIDIConfig.NoteOff,
							0, Song.CurrentSong.MIDINoteTracker[chan], 0, j);
					}

					midiPacket[0] = (byte)(0x80 + chan);
					midiPacket[1] = (byte)Song.CurrentSong.MIDINoteTracker[chan];

					Song.CurrentSong.MIDISend(midiPacket, 2, 0, fake: false);
				}
			}
		}

		if (Song.CurrentSong.MIDIPlaying)
		{
			byte[] midiPacket = new byte[4];

			for (int j = 0; j < Constants.MaxMIDIChannels; j++)
			{
				midiPacket[0] = (byte)(0xe0 + j);
				midiPacket[1] = 0;
				Song.CurrentSong.MIDISend(midiPacket, 2, 0, fake: false);

				midiPacket[0] = (byte)(0xb0 + j); // channel mode message
				midiPacket[1] = 0x78;   // all sound off
				midiPacket[2] = 0;
				Song.CurrentSong.MIDISend(midiPacket, 3, 0, fake: false);

				midiPacket[1] = 0x79;   // reset all controllers
				Song.CurrentSong.MIDISend(midiPacket, 3, 0, fake: false);

				midiPacket[1] = 0x7b;   // all notes off
				Song.CurrentSong.MIDISend(midiPacket, 3, 0, fake: false);
			}

			Song.CurrentSong.ProcessMIDIMacro(0, Song.CurrentSong.MIDIConfig.Stop, 0, 0, 0, 0); // STOP!
			MIDIEngine.SendFlush();

			Song.CurrentSong.MIDIPlaying = false;
		}

		// TODO
		//OPL_Reset(current_song); // Also stop all OPL sounds
		//GM_Reset(current_song, quitting);
		//GM_SendSongStopCode(current_song);

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

		Song.CurrentSong.VULeft = 0;
		Song.CurrentSong.VURight = 0;

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

			// TODO
			//GM_SendSongStartCode(current_song);

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

			if ((v.Sample == smp) || (v.CurrentSampleData == smp.Data))
			{
				v.Note = v.NewNote = 1;
				v.NewInstrumentNumber = 0;
				v.FadeOutVolume = 0;
				v.Flags |= ChannelFlags.KeyOff | ChannelFlags.NoteFade;
				v.Frequency = 0;
				v.Position = v.Length = 0;
				v.LoopStart = 0;
				v.LoopEnd = 0;
				v.ROfs = v.LOfs = 0;
				v.CurrentSampleData = null;
				v.Sample = null;
				v.Instrument = null;
				v.LeftVolume = v.RightVolume = 0;
				v.LeftVolumeNew = v.RightVolumeNew = 0;
				v.LeftRamp = v.RightRamp = 0;
			}
		}
	}
}
