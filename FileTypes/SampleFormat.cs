using System;

namespace ChasmTracker.FileTypes;

// Flags for csf_read_sample

// Sample data characteristics
// Note:
// - None of these constants are zero
// - The format specifier must have a value set for each "section"
// - csf_read_sample DOES check the values for validity
[Flags]
public enum SampleFormat
{
	// Bit width (8 bits for simplicity)
	BitMask = 0xFF,
	_7 = 7,   // 7-bit (weird!)
	_8 = 8,   // 8-bit
	_16 = 16, // 16-bit
	_24 = 24, // 24-bit
	_32 = 32, // 32-bit
	_64 = 64, // 64-bit (for IEEE floating point)

	// Channels (4 bits)
	ChannelMask       = 0xF00,
	Mono              = 1 << 8,
	StereoInterleaved = 2 << 8,
	StereoSplit       = 3 << 8,

	// Endianness (4 bits)
	EndiannessMask = 0xF000,
	LittleEndian   = 1 << 12,
	BigEndian      = 2 << 12,

	// Encoding (8 bits)
	EncodingMask = 0xFF0000,
	PCMSigned                      = 1 << 16, // PCM, signed
	PCMUnsigned                    = 2 << 16, // PCM, unsigned
	PCMDeltaEncoded                = 3 << 16, // PCM, delta-encoded
	IT214Compressed                = 4 << 16, // Impulse Tracker 2.14 compressed
	IT215Compressed                = 5 << 16, // Impulse Tracker 2.15 compressed
	AMSPacked                      = 6 << 16, // AMS / Velvet Studio packed
	DMFHuffmanCompressed           = 7 << 16, // DMF Huffman compression
	MDLHuffmanCompressed           = 8 << 16, // MDL Huffman compression
	PTMDeltaEncoded                = 9 << 16, // PTM 8-bit delta value -> 16-bit sample
	PCM16bitTableDeltaEncoded      = 10 << 16, // PCM, 16-byte table delta-encoded
	IEEEFloatingPoint              = 11 << 16, // IEEE floating point
}
