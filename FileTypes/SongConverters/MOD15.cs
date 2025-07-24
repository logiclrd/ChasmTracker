using System.IO;
using ChasmTracker.Songs;

namespace ChasmTracker.FileTypes.SongConverters;

/* loads everything including old 15-instrument mods. this is a separate
	function so that it can be called later in the format-checking sequence. */
public class MOD15 : MODBase
{
	public override int SortOrder => 20;

	public override Song LoadSong(Stream stream, LoadFlags flags)
		=> LoadSongImplementation(stream, flags, forceUntaggedAs15Sample: true);
}
