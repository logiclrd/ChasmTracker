namespace ChasmTracker.VGA;

public enum FontTypes
{
	ImpulseTracker, // ASCII with weird cool box characters
	BIOS,           // codepage 437
	HalfWidth,      // ASCII - half-width; used in the info page and pattern editor
	Overlay,        // none - draws from the overlay buffer
	Unicode,        // UCS-4 - any unicode codepoint
}
