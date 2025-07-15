using System.Runtime.InteropServices;

namespace libFLAC;

/** FLAC PADDING structure.  (c.f. <A HREF="https://xiph.org/flac/format.html#metadata_block_padding">format specification</A>)
 */
[StructLayout(LayoutKind.Sequential)]
public struct StreamMetadata_Padding
{
	int _dummy;
	/**< Conceptually this is an empty struct since we don't store the
	 * padding bytes.  Empty structs are not allowed by some C compilers,
	 * hence the dummy.
	 */
}
