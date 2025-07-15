using System.Runtime.InteropServices;

namespace libFLAC;

[StructLayout(LayoutKind.Sequential)]
public class EntropyCodingMethod
{
	public EntropyCodingMethodType Type;
	public EntropyCodingMethodPartitionedRice PartitionedRiceData = new EntropyCodingMethodPartitionedRice();
}
