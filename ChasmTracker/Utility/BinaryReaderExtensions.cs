using System.IO;
using System.Reflection;
using System.Text;
using ChasmTracker.Utility;

public static class BinaryReaderExtensions
{
	static FieldInfo? s_BinaryReader_encoding_field =
		typeof(BinaryReader).GetField("_encoding", BindingFlags.Instance | BindingFlags.NonPublic);

	public static Encoding GetEncoding(this BinaryReader reader)
	{
		// Why on earth is there no property exposing this?
		try
		{
			if (s_BinaryReader_encoding_field != null)
				return (Encoding)s_BinaryReader_encoding_field.GetValue(reader)!;
		}
		catch { }

		return Encoding.ASCII;
	}

	public static string ReadPlainString(this BinaryReader reader, int length)
	{
		return reader.ReadBytes(length).ToStringZ(GetEncoding(reader));
	}
}
