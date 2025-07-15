using System;

namespace libFLAC;

/** Structure that is used when a metadata block of unknown type is loaded.
 *  The contents are opaque.  The structure is used only internally to
 *  correctly handle unknown metadata.
 */
public struct StreamMetadata_Unknown
{
	public IntPtr Data;
}
