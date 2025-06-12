namespace ChasmTracker;

/* return codes for modules savers */
public enum SaveResult
{
	Success,           /* all's well */
	Unsupported,       /* unsupported samples, or something */
	FileError,         /* couldn't write the file; check errno */
	InternalError,     /* something unrelated to disk i/o */
	NoFilename,        /* the filename is empty... */
}
