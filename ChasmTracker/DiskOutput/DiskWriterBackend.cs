using System;
using System.IO;

namespace ChasmTracker.DiskOutput;

public abstract class DiskWriterBackend
{
	public bool Error = false;

	public abstract int Length { get; }
	public abstract void Truncate(int newLength);

	public abstract Stream AsStream(bool read);

	public abstract void Write(Span<byte> buf);
	public abstract void Seek(long position, SeekOrigin whence);
	public abstract long Tell();
	public abstract void Close(DiskWriterBackupMode backupMode);
}
