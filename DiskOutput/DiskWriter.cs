using System;
using ChasmTracker.Songs;

namespace ChasmTracker.DiskOutput;

public class DiskWriter // "disko"
{
	public const int BufferSize = 65536;

	public static object? ExportFormat;
	public static Song? ExportSong;
	public static DiskWriter[] ExportDs = new DiskWriter[Constants.MaxChannels + 1]; /* only [0] is used unless multichannel */

	public static SyncResult Sync()
	{
		if ((ExportFormat == null) || (ExportSong == null))
		{
			Log.Append(4, "disko_sync: unexplained bacon");
			return SyncResult.Error; /* no writer running (why are we here?) */
		}

		var buffer = csf_read(ExportSong, BufferSize);

		if (!ExportSong.MultiWrite)
			ExportFormat.WriteBody(ExportDs[0], buffer);

		/* always check if something died, multi-write or not */
		for (int n = 0; ExportDs[n] != null; n++)
		{
			if (ExportDs[n].Error)
			{
				Finish();
				return SyncResult.Error;
			}
		}

		/* update the progress bar (kind of messy, yes...) */
		ExportDs[0].Length += buffer.Length;
		Status.Flags |= StatusFlags.NeedUpdate;

		if (ExportSong.Flags.HasFlag(SongFlags.EndReached))
		{
			Finish();
			return SyncResult.Done;
		}
		else
			return SyncResult.More;
	}

	public static int Finish()
	{
		// TODO
		throw new NotImplementedException();
	}

	public static DiskWriterStatus WriteOutSample(int smpnum, int pattern, bool bind)
	{
		// TODO
		return DiskWriterStatus.NotRunning;
	}

	/* Wrapper for MultiWriteSamples that writes to the current sample,
	and with a confirmation dialog if the sample already has data */
	public static void PatternToSample(int pattern, bool split, bool bind)
	{
		/* TODO
		struct pat2smp *ps;
		int n;

		if (split && bind) {
			log_appendf(4, "song_pattern_to_sample: internal error!");
			return;
		}

		if (pattern < 0 || pattern >= MAX_PATTERNS) {
			return;
		}

		// this is horrid
		for (n = 1; n < MAX_SAMPLES; n++) {
			song_sample_t *samp = song_get_sample(n);
			if (!samp) continue;
			if (((unsigned char) samp->name[23]) != 0xFF) continue;
			if (((unsigned char) samp->name[24]) != pattern) continue;
			status_text_flash("Pattern %d already linked to sample %d", pattern, n);
			return;
		}

		ps = mem_alloc(sizeof(struct pat2smp));
		ps->pattern = pattern;

		int samp = sample_get_current();
		ps->sample = samp ? samp : 1;

		ps->bind = bind;

		if (split) {
			// Nothing to confirm, as this never overwrites samples
			pat2smp_multi(ps);
		} else {
			if (current_song->samples[ps->sample].data == NULL) {
				pat2smp_single(ps);
			} else {
	dialog_create(DIALOG_OK_CANCEL, "This will replace the current sample.",
		pat2smp_single, free, 1, ps);
}
		}
		*/
	}
}
