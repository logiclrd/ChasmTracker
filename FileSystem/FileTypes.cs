using System;

namespace ChasmTracker.FileSystem;

[Flags]
public enum FileTypes
{
	BrowsableMask = 0x1, /* if type.HasFlag(BrowsableMask) it's readable as a library */
	FileMask = 0x2, /* if type.HasFlag(FileMask) it's a regular file */
	Directory = 0x4 | BrowsableMask, /* if (type == Directory) ... guess what! */
	NonRegular = 0x8, /* if (type == NonRegular) it's something weird, e.g. a socket */

	/* (flags & 0xF0) are reserved for future use */

	/* this has to match BrowsableMask for directories */
	ExtendedDataMask = 0xFFF01, /* if (type.HasAnyFlag(ExtendedDataMaks)) the extended data has been checked */

	ModuleMask = 0xF00, /* if type.HasFlag(ModuleMask) it's loadable as a module */
	ModuleMOD = 0x100 | BrowsableMask | FileMask,
	ModuleS3M = 0x200 | BrowsableMask | FileMask,
	ModuleXM = 0x300 | BrowsableMask | FileMask,
	ModuleIT = 0x400 | BrowsableMask | FileMask,

	InstrumentMask = 0xF000, /* if type.HasAnyFlag(InstrumentMask) it's loadable as an instrument */
	InstrumentITI = 0x1000 | FileMask, /* .iti (native) instrument */
	InstrumentXI = 0x2000 | FileMask, /* fast tracker .xi */
	InstrumentOther = 0x3000 | FileMask, /* gus patch, soundfont, ...? */

	SampleMask = 0xF0000, /* if type.HasAnyFlag(SampleMask) it's loadable as a sample */
	Unknown = 0x10000 | FileMask, /* any unrecognized file, loaded as raw pcm data */
	SamplePlain = 0x20000 | FileMask, /* au, aiff, wav (simple formats) */
	SampleExtended = 0x30000 | FileMask, /* its, s3i (tracker formats with extended stuff) */
	SampleCompressed = 0x40000 | FileMask, /* ogg, mp3 (compressed audio) */

	InternalFlags = 0xF00000,
	Hidden = 0x100000,
}
