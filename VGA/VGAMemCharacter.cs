namespace ChasmTracker.VGA;

/* contains all the needed information to draw many
 * types of characters onto the screen. historically,
 * all of this information was stored in a single 32-bit
 * unsigned integer */
public struct VGAMemCharacter
{
	/* which font method to use */
	public FontTypes Font;

	/* Unicode: can be any Unicode codepoint; realistically
	 * only a very small subset of characters can be
	 * supported though
	 *
	 * Others: 0...255
	 */
	public uint CUnicode;

	public byte C;
	public VGAMemColours Colours;

	/* used for half-width */
	public byte C2; /* 0...255 */
	public VGAMemColours Colours2;
}
