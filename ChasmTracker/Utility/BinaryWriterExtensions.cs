using System.IO;
using System.Reflection;
using System.Text;

namespace ChasmTracker.Utility;

public static class BinaryWriterExtensions
{
	static FieldInfo? s_BinaryWriter_encoding_field =
		typeof(BinaryWriter).GetField("_encoding", BindingFlags.Instance | BindingFlags.NonPublic);

	public static Encoding GetEncoding(this BinaryWriter writer)
	{
		// Why on earth is there no property exposing this?
		try
		{
			if (s_BinaryWriter_encoding_field != null)
				return (Encoding)s_BinaryWriter_encoding_field.GetValue(writer)!;
		}
		catch { }

		return Encoding.ASCII;
	}

	public static void WritePlain(this BinaryWriter writer, string str)
	{
		writer.Write(writer.GetEncoding().GetBytes(str));
	}
}