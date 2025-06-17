using ChasmTracker.Songs;

namespace ChasmTracker;

public class AudioPlayback
{
	static int s_currentPlayChannel;
	static bool s_multichannelMode;

	public static int CurrentPlayChannel => s_currentPlayChannel;
	public static bool MultichannelMode => s_multichannelMode;

	public static int CurrentRow;
	public static int PlayingPattern;

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

	public static void ToggleMultichannelMode()
	{
		s_multichannelMode = !s_multichannelMode;

		Status.FlashText(
			s_multichannelMode ?
			"Multichannel playback enabled" :
			"Multichannel playback disabled");
	}

	public static void LockAudio()
	{
		// TODO
	}

	public static void UnlockAudio()
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
		// TODO

		/*
		memset(midi_bend_hit, 0, sizeof(midi_bend_hit));
		memset(midi_last_bend_hit, 0, sizeof(midi_last_bend_hit));
		memset(keyjazz_note_to_chan, 0, sizeof(keyjazz_note_to_chan));
		memset(keyjazz_chan_to_note, 0, sizeof(keyjazz_chan_to_note));

		// turn this crap off
		current_song->mix_flags &= ~(SNDMIX_NOBACKWARDJUMPS | SNDMIX_DIRECTTODISK);

		OPL_Reset(current_song); // gruh?

		csf_set_current_order(current_song, 0);

		current_song->repeat_count = 0;
		current_song->buffer_count = 0;
		current_song->flags &= ~(SONG_PAUSED | SONG_PATTERNLOOP | SONG_ENDREACHED);

		current_song->stop_at_order = -1;
		current_song->stop_at_row = -1;
		samples_played = 0;
		*/
	}

	public static void StartOnce()
	{
		// TODO
		/*
		song_lock_audio();

		song_reset_play_state();
		current_song->mix_flags |= SNDMIX_NOBACKWARDJUMPS;
		max_channels_used = 0;
		current_song->repeat_count = -1; // FIXME do this right

		GM_SendSongStartCode(current_song);
		song_unlock_audio();
		main_song_mode_changed_cb();

		csf_reset_playmarks(current_song);
		*/
	}

	public static void Start()
	{
		// TODO
		/*
		song_lock_audio();

		song_reset_play_state();
		max_channels_used = 0;

		GM_SendSongStartCode(current_song);
		song_unlock_audio();
		main_song_mode_changed_cb();

		csf_reset_playmarks(current_song);
		*/
	}

	public static void Pause()
	{
		// TODO
		/*
		song_lock_audio();
		// Highly unintuitive, but SONG_PAUSED has nothing to do with pause.
		if (!(current_song->flags & SONG_PAUSED))
			current_song->flags ^= SONG_ENDREACHED;
		song_unlock_audio();
		main_song_mode_changed_cb();
		*/
	}

	public static void Stop()
	{
		// TODO
		/*
		song_lock_audio();
		song_stop_unlocked(0);
		song_unlock_audio();
		main_song_mode_changed_cb();
		*/
	}

	public static void StopUnlocked(bool quitting)
	{
		// TODO
		/*
		if (!current_song) return;

		if (current_song->midi_playing) {
			unsigned char moff[4];

			// shut off everything; not IT like, but less annoying
		for (int chan = 0; chan < 64; chan++) {
			if (current_song->midi_note_tracker[chan] != 0) {
				for (int j = 0; j < MAX_MIDI_CHANNELS; j++) {
					csf_process_midi_macro(current_song, chan,
						current_song->midi_config.note_off,
						0, current_song->midi_note_tracker[chan], 0, j);
				}
				moff[0] = 0x80 + chan;
				moff[1] = current_song->midi_note_tracker[chan];
				csf_midi_send(current_song, (unsigned char *) moff, 2, 0, 0);
				}
			}
			for (int j = 0; j < MAX_MIDI_CHANNELS; j++) {
				moff[0] = 0xe0 + j;
				moff[1] = 0;
				csf_midi_send(current_song, (unsigned char *) moff, 2, 0, 0);

				moff[0] = 0xb0 + j;	// channel mode message
				moff[1] = 0x78;		// all sound off
				moff[2] = 0;
				csf_midi_send(current_song, (unsigned char *) moff, 3, 0, 0);

				moff[1] = 0x79;		// reset all controllers
				csf_midi_send(current_song, (unsigned char *) moff, 3, 0, 0);

				moff[1] = 0x7b;		// all notes off
				csf_midi_send(current_song, (unsigned char *) moff, 3, 0, 0);
			}

			csf_process_midi_macro(current_song, 0, current_song->midi_config.stop, 0, 0, 0, 0); // STOP!
			midi_send_flush(); // NOW!

			current_song->midi_playing = 0;
		}

		OPL_Reset(current_song); // Also stop all OPL sounds
		GM_Reset(current_song, quitting);
		GM_SendSongStopCode(current_song);

		memset(current_song->midi_last_row,0,sizeof(current_song->midi_last_row));
		current_song->midi_last_row_number = -1;

		memset(current_song->midi_note_tracker,0,sizeof(current_song->midi_note_tracker));
		memset(current_song->midi_vol_tracker,0,sizeof(current_song->midi_vol_tracker));
		memset(current_song->midi_ins_tracker,0,sizeof(current_song->midi_ins_tracker));
		memset(current_song->midi_was_program,0,sizeof(current_song->midi_was_program));
		memset(current_song->midi_was_banklo,0,sizeof(current_song->midi_was_banklo));
		memset(current_song->midi_was_bankhi,0,sizeof(current_song->midi_was_bankhi));

		playback_tracing = midi_playback_tracing;

		song_reset_play_state();
		// Modplug doesn't actually have a "stop" mode, but if SONG_ENDREACHED is set, csf_read just returns.
		current_song->flags |= SONG_PAUSED | SONG_ENDREACHED;

		current_song->vu_left = 0;
		current_song->vu_right = 0;
		memset(audio_buffer, 0, audio_buffer_samples * audio_sample_size);
		*/
	}

	public static void LoopPattern(int pattern, int row)
	{
		// TODO
		/*
		song_lock_audio();

		song_reset_play_state();

		max_channels_used = 0;
		csf_loop_pattern(current_song, pattern, row);

		GM_SendSongStartCode(current_song);

		song_unlock_audio();
		main_song_mode_changed_cb();

		csf_reset_playmarks(current_song);
		*/
	}

	public static void StartAtOrder(int order, int row)
	{
		// TODO
		/*
		song_lock_audio();

		song_reset_play_state();

		csf_set_current_order(current_song, order);
		current_song->break_row = row;
		max_channels_used = 0;

		GM_SendSongStartCode(current_song);
		// TODO: GM_SendSongPositionCode(calculate the number of 1/16 notes)
		song_unlock_audio();
		main_song_mode_changed_cb();

		csf_reset_playmarks(current_song);
		*/
	}

	public static void StartAtPattern(int pattern, int row)
	{
		// TODO
		/*
		if (pattern < 0 || pattern > 199)
			return;

		int n = song_next_order_for_pattern(pattern);

		if (n > -1) {
			song_start_at_order(n, row);
			return;
		}

		song_loop_pattern(pattern, row);
		*/
	}
}
