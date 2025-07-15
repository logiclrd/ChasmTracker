using System;
using System.Runtime.InteropServices;

namespace libFLAC;

/** FLAC CUESHEET structure.  (See the
 * <A HREF="https://xiph.org/flac/format.html#metadata_block_cuesheet">format specification</A>
 * for the full description of each field.)
 */
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
public struct StreamMetadata_CueSheet
{
	[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 129)]
	public string MediaCatalogNumber;
	/**< Media catalog number, in ASCII printable characters 0x20-0x7e.  In
	 * general, the media catalog number may be 0 to 128 bytes long; any
	 * unused characters should be right-padded with NUL characters.
	 */

	public long LeadIn;
	/**< The number of lead-in samples. */

	public bool IsCD;
	/**< \c true if CUESHEET corresponds to a Compact Disc, else \c false. */

	public int NumTracks;
	/**< The number of tracks. */

	[MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)]
	public CueSheetTrack[] Tracks;
	/**< NULL if num_tracks == 0, else pointer to array of tracks. */
}

/** FLAC CUESHEET track structure.  (See the
 * <A HREF="https://xiph.org/flac/format.html#cuesheet_track">format specification</A> for
 * the full description of each field.)
 */
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
public struct CueSheetTrack
{
	public long Offset;
	/**< Track offset in samples, relative to the beginning of the FLAC audio stream. */

	public byte Number;
	/**< The track number. */

	[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 13)]
	public string ISRC;
	/**< Track ISRC.  This is a 12-digit alphanumeric code plus a trailing \c NUL byte */

	public CueSheetTrackFlags Flags;
	/**< The track type: 0 for audio, 1 for non-audio. */
	/**< The pre-emphasis flag: 0 for no pre-emphasis, 2 for pre-emphasis. */

	public byte NumIndices;
	/**< The number of track index points. */

	[MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4)]
	public CueSheetIndex[] Indices;
	/**< NULL if num_indices == 0, else pointer to array of index points. */

}

[Flags]
public enum CueSheetTrackFlags : byte
{
	TypeNonAudio = 1,
	PreEmphasis = 2,
}

/** FLAC CUESHEET track index structure.  (See the
 * <A HREF="https://xiph.org/flac/format.html#cuesheet_track_index">format specification</A> for
 * the full description of each field.)
 */
[StructLayout(LayoutKind.Sequential)]
public struct CueSheetIndex
{
	public long Offset;
	/**< Offset in samples, relative to the track offset, of the index
	 * point.
	 */

	public byte Number;
	/**< The index point number. */
}
