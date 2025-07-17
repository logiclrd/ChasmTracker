using System;
using System.IO;
using System.Text;

namespace ChasmTracker.FileTypes;

using ChasmTracker.FileSystem;
using ChasmTracker.Songs;

public abstract class SongFileConverter : IFileInfoReader
{
	public abstract bool FillExtendedData(Stream stream, FileReference fileReference);
	public abstract Song LoadSong(Stream stream, LoadFlags flags);
	public virtual SaveResult SaveSong(Song song, Stream stream) => throw new NotSupportedException();

	public virtual bool CanSave => false;

	protected string ReadLinedMessage(Stream fp, int len, int lineLen, Encoding? encoding = null)
	{
		encoding ??= Encoding.ASCII;

		byte[] line = new byte[lineLen];

		var msg = new StringBuilder();

		while (len > 0)
		{
			int lineSize = Math.Min(len, lineLen);

			fp.ReadExactly(line, 0, lineSize);

			len -= lineSize;

			msg.AppendLine(encoding.GetString(line, 0, lineSize).Replace('\0', ' ').TrimEnd());
		}

		return msg.ToString();
	}
}
