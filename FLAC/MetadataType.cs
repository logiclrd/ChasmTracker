namespace libFLAC;

/** An enumeration of the available metadata block types. */
public enum MetadataType
{

	STREAMINFO = 0,
	/**< <A HREF="https://xiph.org/flac/format.html#metadata_block_streaminfo">STREAMINFO</A> block */

	PADDING = 1,
	/**< <A HREF="https://xiph.org/flac/format.html#metadata_block_padding">PADDING</A> block */

	APPLICATION = 2,
	/**< <A HREF="https://xiph.org/flac/format.html#metadata_block_application">APPLICATION</A> block */

	SEEKTABLE = 3,
	/**< <A HREF="https://xiph.org/flac/format.html#metadata_block_seektable">SEEKTABLE</A> block */

	VORBISCOMMENT = 4,
	/**< <A HREF="https://xiph.org/flac/format.html#metadata_block_vorbis_comment">VORBISCOMMENT</A> block (a.k.a. FLAC tags) */

	CUESHEET = 5,
	/**< <A HREF="https://xiph.org/flac/format.html#metadata_block_cuesheet">CUESHEET</A> block */

	PICTURE = 6,
	/**< <A HREF="https://xiph.org/flac/format.html#metadata_block_picture">PICTURE</A> block */

	Undefined = 7,
	/**< marker to denote beginning of undefined type range; this number will increase as new metadata types are added */

	MaxMetadataType = 126,
	/**< No type will ever be greater than this. There is not enough room in the protocol block. */
}
