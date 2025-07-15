using System;
using System.Runtime.InteropServices;

namespace libFLAC;

/** FLAC VORBIS_COMMENT structure.  (c.f. <A HREF="https://xiph.org/flac/format.html#metadata_block_vorbis_comment">format specification</A>)
 */
[StructLayout(LayoutKind.Sequential)]
public struct StreamMetadata_VorbisComment
{
	public VorbisCommentEntry VendorString;
	public int NumComments;
	[MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)]
	public VorbisCommentEntry[] Comments;
}

/** Vorbis comment entry structure used in VORBIS_COMMENT blocks.  (c.f. <A HREF="https://xiph.org/flac/format.html#metadata_block_vorbis_comment">format specification</A>)
 *
 *  For convenience, the APIs maintain a trailing NUL character at the end of
 *  \a entry which is not counted toward \a length, i.e.
 *  \code strlen(entry) == length \endcode
 */
[StructLayout(LayoutKind.Sequential)]
public struct VorbisCommentEntry
{
	public int Length;
	public IntPtr Entry;
}
