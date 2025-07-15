using System;
using System.Runtime.InteropServices;

namespace libFLAC;

/** FLAC PICTURE structure.  (See the
 * <A HREF="https://xiph.org/flac/format.html#metadata_block_picture">format specification</A>
 * for the full description of each field.)
 */
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
public struct StreamMetadata_Picture
{
	public StreamMetadata_Picture_Type Type;
	/**< The kind of picture stored. */

	[MarshalAs(UnmanagedType.LPStr)]
	public string MIMEType;
	/**< Picture data's MIME type, in ASCII printable characters
	 * 0x20-0x7e, NUL terminated.  For best compatibility with players,
	 * use picture data of MIME type \c image/jpeg or \c image/png.  A
	 * MIME type of '-->' is also allowed, in which case the picture
	 * data should be a complete URL.  In file storage, the MIME type is
	 * stored as a 32-bit length followed by the ASCII string with no NUL
	 * terminator, but is converted to a plain C string in this structure
	 * for convenience.
	 */

	public IntPtr Description;
	/**< Picture's description in UTF-8, NUL terminated.  In file storage,
	 * the description is stored as a 32-bit length followed by the UTF-8
	 * string with no NUL terminator, but is converted to a plain C string
	 * in this structure for convenience.
	 */

	public uint Width;
	/**< Picture's width in pixels. */

	public uint Height;
	/**< Picture's height in pixels. */

	public uint Depth;
	/**< Picture's color depth in bits-per-pixel. */

	public uint Colors;
	/**< For indexed palettes (like GIF), picture's number of colors (the
	 * number of palette entries), or \c 0 for non-indexed (i.e. 2^depth).
	 */

	public uint DataLength;
	/**< Length of binary picture data in bytes. */

	public IntPtr Data;
	/**< Binary picture data. */

}

/** An enumeration of the PICTURE types (see FLAC__StreamMetadataPicture and id3 v2.4 APIC tag). */
public enum StreamMetadata_Picture_Type
{
	Other = 0, /**< Other */
	FileIconStandard = 1, /**< 32x32 pixels 'file icon' (PNG only) */
	FileIcon = 2, /**< Other file icon */
	FrontCover = 3, /**< Cover (front) */
	BackCover = 4, /**< Cover (back) */
	LeafletPage = 5, /**< Leaflet page */
	Media = 6, /**< Media (e.g. label side of CD) */
	LeadArtist = 7, /**< Lead artist/lead performer/soloist */
	Artist = 8, /**< Artist/performer */
	Conductor = 9, /**< Conductor */
	Band = 10, /**< Band/Orchestra */
	Composer = 11, /**< Composer */
	Lyricist = 12, /**< Lyricist/text writer */
	RecordingLocation = 13, /**< Recording Location */
	DuringRecording = 14, /**< During recording */
	DuringPerformance = 15, /**< During performance */
	VideoScreenCapture = 16, /**< Movie/video screen capture */
	Fish = 17, /**< A bright coloured fish */
	Illustration = 18, /**< Illustration */
	BandLogotype = 19, /**< Band/artist logotype */
	PublisherLogotype = 20, /**< Publisher/Studio logotype */
	Undefined
}