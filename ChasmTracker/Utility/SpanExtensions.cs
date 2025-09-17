using System;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace ChasmTracker.Utility;

public static class SpanExtensions
{
	public static string ToStringZ(this Span<byte> array)
	{
		for (int i = 0; i < array.Length; i++)
			if (array[i] == 0)
				return Encoding.ASCII.GetString(array.Slice(0, i));

		return Encoding.ASCII.GetString(array);
	}

	/* XORs all of the bytes in vbuf */
	public static unsafe void ExclusiveOr(this Span<byte> vbuf, byte c)
	{
		ref byte vbufRef = ref vbuf.GetPinnableReference();

		fixed (byte *vbufPtr = &vbufRef)
		{
			byte *buf = vbufPtr;

			int len = vbuf.Length;

			if (len >= 4)
			{
				/* expand to all bytes */
				uint cccc = c;
				cccc |= (cccc << 8);
				cccc |= (cccc << 16);

				/* align the pointer */
				for (; ((long)(IntPtr)buf).HasAnyBitSet(3); len--)
					*(buf++) ^= c;

				/* process in chunks of 8 32-bit integers */
				for (int len8 = (len / (sizeof(uint) * 8)); len8 > 0; len8--)
				{
					((uint *)buf)[0] ^= cccc;
					((uint *)buf)[1] ^= cccc;
					((uint *)buf)[2] ^= cccc;
					((uint *)buf)[3] ^= cccc;
					((uint *)buf)[4] ^= cccc;
					((uint *)buf)[5] ^= cccc;
					((uint *)buf)[6] ^= cccc;
					((uint *)buf)[7] ^= cccc;
					buf += (8 * sizeof(uint));
				}

				len %= (sizeof(uint) * 8);

				/* process in chunks of 32-bit integers */
				for (int len8 = len / sizeof(uint); len8 > 0; len8--)
				{
					((uint *)buf)[0] ^= cccc;
					buf += sizeof(uint);
				}

				len %= sizeof(uint);
			}

			/* process any that remain */
			for (; len > 0; len--)
				*(buf++) ^= c;
		}

	}
}
