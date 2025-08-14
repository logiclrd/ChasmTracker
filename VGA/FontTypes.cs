namespace ChasmTracker.VGA;

public static class FontTypes
{
	public const uint ImpulseTracker = 0x00000000; // ASCII with weird cool box characters
	public const uint BIOS           = 0x10000000; // codepage 437
	public const uint HalfWidth      = 0x80000000; // ASCII - half-width; used in the info page and pattern editor
	public const uint Overlay        = 0x40000000; // none - draws from the overlay buffer
	public const uint Unicode        = 0x20000000; // UCS-4 - any unicode codepoint -- bit 29; leaves literally just enough space
}
