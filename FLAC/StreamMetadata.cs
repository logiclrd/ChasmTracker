using System.Runtime.InteropServices;

namespace libFLAC;

[StructLayout(LayoutKind.Explicit)]
public struct StreamMetadataInnerUnion
{
	[FieldOffset(0)]
	public StreamMetadata_StreamInfo StreamInfo;
	[FieldOffset(0)]
	public StreamMetadata_Padding Padding;
	[FieldOffset(0)]
	public StreamMetadata_Application Application;
	[FieldOffset(0)]
	public StreamMetadata_SeekTable SeekTable;
	[FieldOffset(0)]
	public StreamMetadata_VorbisComment VorbisComment;
	[FieldOffset(0)]
	public StreamMetadata_CueSheet CueSheet;
	[FieldOffset(0)]
	public StreamMetadata_Picture Picture;
	[FieldOffset(0)]
	public StreamMetadata_Unknown Unknown;
}

/** FLAC metadata block structure.  (c.f. <A HREF="https://xiph.org/flac/format.html#metadata_block">format specification</A>)
 */
[StructLayout(LayoutKind.Sequential)]
public class StreamMetadata
{
	public MetadataType Type;
	/**< The type of the metadata block; used determine which member of the
	 * \a data union to dereference.  If type >= FLAC__METADATA_TYPE_UNDEFINED
	 * then \a data.unknown must be used. */

	public bool IsLast;
	/**< \c true if this metadata block is the last, else \a false */

	public int Length;
	/**< Length, in bytes, of the block data as it appears in the stream. */

	public StreamMetadataInnerUnion Data;
	/**< Polymorphic block data; use the \a type value to determine which
	 * to use. */
}

[StructLayout(LayoutKind.Sequential)]
public class StreamMetadata_with_StreamInfo
{
	public MetadataType Type = MetadataType.STREAMINFO;
	public bool IsLast;
	public int Length = Marshal.SizeOf<StreamMetadata_StreamInfo>();
	public StreamMetadata_StreamInfo StreamInfo;
}

[StructLayout(LayoutKind.Sequential)]
public class StreamMetadata_with_Padding
{
	public MetadataType Type = MetadataType.PADDING;
	public bool IsLast;
	public int Length = Marshal.SizeOf<StreamMetadata_Padding>();
	public StreamMetadata_Padding Padding;
}

[StructLayout(LayoutKind.Sequential)]
public class StreamMetadata_with_Application
{
	public MetadataType Type = MetadataType.APPLICATION;
	public bool IsLast;
	public int Length = Marshal.SizeOf<StreamMetadata_Application>();
	public StreamMetadata_Application Application;
}

[StructLayout(LayoutKind.Sequential)]
public class StreamMetadata_with_SeekTable
{
	public MetadataType Type = MetadataType.SEEKTABLE;
	public bool IsLast;
	public int Length = Marshal.SizeOf<StreamMetadata_SeekTable>();
	public StreamMetadata_SeekTable SeekTable;
}

[StructLayout(LayoutKind.Sequential)]
public class StreamMetadata_with_VorbisComment
{
	public MetadataType Type = MetadataType.SEEKTABLE;
	public bool IsLast;
	public int Length = Marshal.SizeOf<StreamMetadata_VorbisComment>();
	public StreamMetadata_VorbisComment VorbisComment;
}

[StructLayout(LayoutKind.Sequential)]
public class StreamMetadata_with_CueSheet
{
	public MetadataType Type = MetadataType.CUESHEET;
	public bool IsLast;
	public int Length = Marshal.SizeOf<StreamMetadata_CueSheet>();
	public StreamMetadata_CueSheet CueSheet;
}

[StructLayout(LayoutKind.Sequential)]
public class StreamMetadata_with_Picture
{
	public MetadataType Type = MetadataType.PICTURE;
	public bool IsLast;
	public int Length = Marshal.SizeOf<StreamMetadata_Picture>();
	public StreamMetadata_Picture Picture;
}

[StructLayout(LayoutKind.Sequential)]
public class StreamMetadata_with_Unknown
{
	public MetadataType Type;
	public bool IsLast;
	public int Length = Marshal.SizeOf<StreamMetadata_Unknown>();
	public StreamMetadata_Unknown Unknown;
}
