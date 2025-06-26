using System;
using System.IO;
using System.Text;

namespace ChasmTracker.FileTypes;

using ChasmTracker.FileSystem;
using ChasmTracker.Songs;

public abstract class SongFileConverter
{
	public abstract bool FillExtendedData(Stream stream, FileReference fileReference);
	public abstract Song LoadSong(Stream stream, LoadFlags flags);
	// public abstract void SaveSong(Song song);

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
