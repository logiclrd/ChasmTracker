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
}
