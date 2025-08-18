namespace ChasmTracker.FileTypes;

public enum FillResult
{
	Success = 0,     /* nothing wrong */
	Unsupported = 1, /* unsupported file type */
	Empty = 2,       /* zero-byte-long file */
}
