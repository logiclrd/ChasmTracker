using System.IO;

namespace ChasmTracker.FileSystem.MetadataReaders;

using ChasmTracker.FileTypes;
using ChasmTracker.Utility;

public class MED : IFileInfoReader
{
	/*
	struct MMD0
	{
		/  0 / char     id[4];
		/  4 / uint32_t modlen;
		/  8 / uint32_t song_ptr; // struct MMD0song
		/ 12 / uint16_t psecnum;    // for the player routine, MMD2 only
		/ 14 / uint16_t pseq;       //  "   "   "   "
		/ 16 / uint32_t blockarr_ptr; // struct MMD0Block *[] (array of pointers)
		/ 20 / uint8_t  mmdflags;
		/ 21 / uint8_t  reserved[3];
		/ 24 / uint32_t smplarr_ptr; // struct InstrHdr *[] (array of pointers)
		/ 28 / uint32_t reserved2;
		/ 32 / uint32_t expdata_ptr; // struct MMD0exp <-- song name is hiding in here
		/ 36 / uint32_t reserved3;
		/ 40 / uint16_t pstate;  // some data for the player routine
		/ 42 / uint16_t pblock;
		/ 44 / uint16_t pline;
		/ 46 / uint16_t pseqnum;
		/ 48 / int16_t  actplayline;
		/ 50 / uint8_t  counter;
		/ 51 / uint8_t  extra_songs; // number of songs - 1
	};

	struct MMD0exp
	{
		/  0 / uint32_t nextmod_ptr; // struct MMD0
		/  4 / uint32_t exp_smp_ptr; // struct InstrExp
		/  8 / uint16_t s_ext_entries;
		/ 10 / uint16_t s_ext_entrsz;
		/ 12 / uint32_t annotxt_ptr; // char[]
		/ 16 / uint32_t annolen;
		/ 20 / uint32_t iinfo_ptr; // struct MMDInstrInfo
		/ 24 / uint16_t i_ext_entries;
		/ 26 / uint16_t i_ext_entrsz;
		/ 28 / uint32_t jumpmask;
		/ 32 / uint32_t rgbtable_ptr; // uint16_t[]
		/ 36 / uint8_t  channelsplit[4];
		/ 40 / uint32_t n_info_ptr // struct NotationInfo *
		/ 44 / uint32_t songname_ptr; // char[]
		/ 48 / uint32_t songnamelen;
		/ 52 / uint32_t dumps_ptr; // struct MMDDumpData
		/ 56 / uint32_t mmdinfo_ptr; // struct MMDInfo
		/ 60 / uint32_t mmdrexx_ptr; // struct MMDARexx
		/ 64 / uint32_t mmdcmd3x_ptr; // struct MMDMIDICmd3x
		/ 68 / uint32_t reserved2[3];
		/ 80 / uint32_t tag_end;
	};
	*/

	public bool FillExtendedData(Stream stream, FileReference file)
	{
		long startPosition = stream.Position;

		string magic = stream.ReadString(4);

		if ((magic.Substring(0, 3) != "MMD") || !char.IsDigit(magic[3]))
			return false;

		if (stream.Position + 36 >= stream.Length)
			return false;

		stream.Position = startPosition + 32;

		int expStructurePtr = ByteSwap.Swap(stream.ReadStructure<int>());

		long expStructurePosition = startPosition + expStructurePtr;

		if (expStructurePosition + 52 >= stream.Length)
			return false;

		// get the offset & length of the name
		stream.Position = expStructurePosition + 44;

		int namePtr = ByteSwap.Swap(stream.ReadStructure<int>());
		int nameLength = ByteSwap.Swap(stream.ReadStructure<int>());

		stream.Position = startPosition + namePtr;

		string title = stream.ReadString(nameLength);

		file.Description = "OctaMed";
		file.Title = title;
		file.Type = FileTypes.ModuleMOD; // err, more like XM for Amiga

		return true;
	}
}
