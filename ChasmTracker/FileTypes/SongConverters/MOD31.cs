using System.IO;
using ChasmTracker.Songs;

namespace ChasmTracker.FileTypes.SongConverters;

/* loads everything but old 15-instrument mods... yes, even FLT8 and WOW files
	(and the definition of "everything" is always changing) */
public class MOD31 : MODBase
{
	public override int SortOrder => 1;

	public override bool CanSave => true;
	public override int SaveOrder => 3;

	public override Song LoadSong(Stream stream, LoadFlags flags)
		=> LoadSongImplementation(stream, flags, forceUntaggedAs15Sample: false);
}
