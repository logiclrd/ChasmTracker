using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace ChasmTracker.FileSystem;

using ChasmTracker.Playback;
using ChasmTracker.Songs;
using ChasmTracker.Utility;

public class DirectoryScanner
{
	public static FilterOperation? CurrentFilterOperation = null;

	// dmoz_filter_filelist
	public static void SetAsynchronousFileListParameters(FileList fileList, Func<FileReference, bool> grep, Shared<int> pointer, Action? fn = null)
{
		CurrentFilterOperation = fileList.BeginFilter(grep, pointer);
		CurrentFilterOperation.ChangeMade +=
			() =>
			{
				if (CurrentFilterOperation.IsFinished)
					CurrentFilterOperation = null;

				fn?.Invoke();
			};
	}

	// thunk
	public static bool FillExtendedData(FileReference reference)
		=> reference.FillExtendedData();

	// dmoz_worker
	// this is called by main to actually do some dmoz work. returns 0 if there is no dmoz work to do...
	public static bool TakeAsynchronousFileListStep()
	{
		if (CurrentFilterOperation == null)
			return false;

		return CurrentFilterOperation.TakeStep();
	}

	public static void AddPlatformDirs(FileList fileList, DirectoryList? dirList)
	{
		void Add(string fullPath, int sortOrder)
		{
			if (dirList != null)
				dirList.AddDir(fullPath, sortOrder);
			else
				fileList.AddFile(fullPath, sortOrder);
		}

		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			foreach (var driveInfo in DriveInfo.GetDrives())
				Add(driveInfo.RootDirectory.FullName, -(1024 - 'A' - driveInfo.Name[0]));
		}
		else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
		{
			// Query directory services
			// > dscl . -list /Users
			//   < amavisd
			//   < appowner
			//   < appserver
			//   < clamav
			//   < ...
			//   < actualuser
			//   < www
			// > disc . -read /Users/amavisd
			//   < _writers_passwd: amavisd
			//   < AppleMetaNodeLocation: /NetInfo/DefaultLocalNode
			//   < Change: 0
			//   < Expire: 0
			// * < NFSHomeDirectory: /var/virusmails
			//   < Password: *
			//   < PrimaryGroupID: 83
			//   < RealName: Amavisd User
			//   < RecordName: amazisd
			//   < RecordType: dsRecTypeStandard:Users
			// * < UniqueID: 83
			//   < UserShell: /bin/tcsh
			// Login screen only lists users whose UniqueID is greater than 500 (so this one
			// is hidden). Home directory is (apparently?) always named "NFSHomeDirectory".

			TextReader DSCL(string args)
			{
				var psi = new ProcessStartInfo();

				psi.FileName = "dscl";
				psi.Arguments = ". list /Users";
				psi.RedirectStandardOutput = true;

				using (var process = Process.Start(psi))
					if (process != null)
						return new StringReader(process.StandardOutput.ReadToEnd());

				return new StringReader("");
			}

			var allUsers = DSCL(". -list /Users");

			while (allUsers.ReadLine() is string username)
			{
				var userProperties = DSCL(". -read /Users/" + username);

				int uid = -1;
				string homePath = "";

				while (userProperties.ReadLine() is string property)
				{
					int separator = property.IndexOf(": ");

					if (separator > 0)
					{
						string propertyName = property.Substring(0, separator);
						string propertyValue = property.Substring(separator + 2);

						if (propertyName == "UniqueID")
							int.TryParse(propertyValue, out uid);
						else if (propertyName == "NFSHomeDirectory")
							homePath = propertyValue;
					}
				}

				if ((uid > 500) && Directory.Exists(homePath))
					Add(homePath, -1024);
			}
		}
		else
		{
			// Parse /etc/passwd
			try
			{
				using (var reader = new StreamReader("/etc/passwd"))
				{
					while (reader.ReadLine() is string passwdLine)
					{
						string[] fields = passwdLine.Split(':', count: 7);

						if (int.TryParse(fields[2], out var uid)
						 && (uid >= 1000)
						 && Directory.Exists(fields[6]))
							Add(fields[6], -1024);
					}
				}
			}
			catch { }
		}
	}

	// dmoz_read
	public static bool Populate(string directory, FileList fileList, DirectoryList? dirList = null, Action<string, FileList, DirectoryList?>? loadLibrary = null)
	{
		int _;

		try
		{
			if (File.Exists(directory))
			{
				/* oops, it's a file! -- load it as a library */
				if (loadLibrary != null)
					loadLibrary(directory, fileList, dirList);
				else if (Path.GetDirectoryName(directory) is string parentDirectory)
					_ = dirList?.AddDir(parentDirectory, ".", -10) ?? fileList.AddFile(parentDirectory, ".", -10);
				else
					AddPlatformDirs(fileList, dirList);
			}
			else if (Directory.Exists(directory))
			{
				foreach (var entry in new DirectoryInfo(directory).EnumerateFileSystemInfos())
				{
					/* ignore hidden/backup files */
					if (entry.Attributes.HasFlag(FileAttributes.Hidden))
						continue;

					if (entry.Name.EndsWith("~"))
						continue;

					if (entry.Attributes.HasFlag(FileAttributes.Directory))
						_ = dirList?.AddDir(entry.FullName, 0) ?? fileList.AddFile(entry.FullName, 0);
					else
						fileList.AddFile(entry.FullName, 1);
				}

				AddPlatformDirs(fileList, dirList);
			}

			Sort(fileList, dirList);

			return true;
		}
		catch
		{
			return false;
		}
	}

	public static void Sort(FileList fileList, DirectoryList? dirList)
	{
		fileList.Sort();
	}

	// ------------------------------------------------------------------------
	// sample library browsing

	static Song? s_library = null;

	public static void ReadSampleLibraryThunk(string path, FileList flist, DirectoryList? dlist)
	{
		ReadSampleLibrary(path, flist, dlist);
	}

	public static bool ReadSampleLibrary(string path, FileList flist, DirectoryList? dlist)
	{
		// XXX: just [0] ??
		if (Song.CurrentSong.Samples[0] is SongSample firstSample)
			AudioPlayback.StopSample(Song.CurrentSong, firstSample);

		if (s_library != null)
		{
			// s_library.Destroy();
			s_library = null;
		}

		string @base = Path.GetFileName(path);

		try
		{
			/* ask what type of file we have
			* FIXME use slurp and read info funcs manually */
			var infoFile = new FileReference(path);

			infoFile.FileSize = new FileInfo(path).Length;

			infoFile.FillExtendedData();

			if (infoFile.Type.HasAnyFlag(FileTypes.ModuleMask))
				s_library = Song.Load(path);
			else if (infoFile.Type.HasAnyFlag(FileTypes.InstrumentMask))
			{
				s_library = new Song();

				s_library.LoadInstrument(1, path);
			}
			else
				return false;
		}
		catch (Exception e)
		{
			Log.AppendException(e);
			return false;
		}

		if (s_library == null)
			return false;

		for (int n = 1; n < s_library.Samples.Count; n++)
		{
			if ((s_library.Samples[n] is SongSample sample) && (sample.Length > 0))
			{
				sample.Name = sample.Name.Replace('\0', ' ');

				var file = new FileReference(path, @base, n);

				file.Type = FileTypes.SampleExtended;
				file.Description = "Impulse Tracker Sample"; /* FIXME: this lies for XI and PAT */
				file.FileSize = sample.Length * (sample.Flags.HasFlag(SampleFlags.Stereo) ? 2 : 1) * (sample.Flags.HasFlag(SampleFlags._16Bit) ? 2 : 1);
				file.SampleSpeed = sample.C5Speed;
				file.SampleLoopStart = sample.LoopStart;
				file.SampleLoopEnd = sample.LoopEnd;
				file.SampleSustainStart = sample.SustainStart;
				file.SampleSustainEnd = sample.SustainEnd;
				file.SampleLength = sample.Length;
				file.SampleFlags = sample.Flags;
				file.SampleDefaultVolume = sample.Volume >> 2;
				file.SampleGlobalVolume = sample.GlobalVolume;
				file.SampleVibratoSpeed = sample.VibratoSpeed;
				file.SampleVibratoDepth = sample.VibratoDepth;
				file.SampleVibratoRate = sample.VibratoDepth;

				// don't screw this up...
				if ((sample.Name.Length > 23) && (sample.Name[23] == 0xFF))
					sample.Name = sample.Name.Substring(0, 23) + ' ' + sample.Name.Substring(23);

				file.Title = sample.Name;
				file.Sample = sample;

				flist.AddFile(file);
			}
		}

		return true;
	}

	public static void ReadInstrumentLibraryThunk(string path, FileList flist, DirectoryList? dlist)
	{
		ReadInstrumentLibrary(path, flist, dlist);
	}

	// TODO: stat the file?
	public static bool ReadInstrumentLibrary(string path, FileList flist, DirectoryList? dlist)
	{
		// FIXME why does this do this ? seems to be a no-op
		if (Song.CurrentSong.Samples.FirstOrDefault() is SongSample sample)
			Song.CurrentSong.StopSample(sample);

		s_library = null;

		string @base = Path.GetFileName(path);

		try
		{
			s_library = Song.CreateLoad(path);
		}
		catch (Exception e)
		{
			Log.AppendException(e, @base);
			return false;
		}

		if (s_library == null)
			return false;

		for (int n = 1; n < s_library.Instruments.Count; n++)
		{
			var instrument = s_library.Instruments[n];

			if (instrument == null)
				continue;

			var file = new FileReference(path, @base, n);

			flist.AddFile(file);

			file.Title = instrument.Name ?? "";

			var seenSample = new HashSet<int>();

			file.SampleCount = 0;
			file.FileSize = 0;
			file.InstrumentNumber = n;

			for (int j = 0; j < 128; j++)
			{
				int x = instrument.SampleMap[j];

				if (seenSample.Add(x))
				{
					if ((x > 0) && (x < s_library.Samples.Count))
					{
						file.FileSize += s_library.Samples[x]?.Length ?? 0;
						file.SampleCount++;
					}
				}
			}

			file.Type = FileTypes.InstrumentITI;
			file.Description = "Fishcakes";

			// IT doesn't support this, despite it being useful.
			// Simply "unrecognized"
		}

		return true;
	}

	class Cache
	{
		public Cache? Next;
		public string? Path;
		public string? CacheFileName;
		public string? CacheDirectoryName;
	}

	static Cache s_cacheTop = null;

	public static void CacheUpdate(string path, FileList? fl, DirectoryList? dl)
	{
		string? fn = null;
		string? dn = null;

		if ((fl != null) && (fl.SelectedIndex > -1) && (fl.SelectedIndex < fl.NumFiles))
			fn = fl[fl.SelectedIndex].BaseName;

		if ((dl != null) && (dl.SelectedIndex > -1) && (dl.SelectedIndex < dl.NumDirectories))
			dn = dl[dl.SelectedIndex].BaseName;

		CacheUpdateNames(path, fn, dn);
	}

	public static void CacheUpdateNames(string path, string? filen, string? dirn)
	{
		filen = Path.GetFileName(filen);
		dirn = Path.GetFileName(dirn);

		if (filen == "..")
			filen = null;
		if (dirn == "..")
			dirn = null;

		for (Cache? p = s_cacheTop, lp = null; p != null; lp = p, p = p.Next)
		{
			if (p.Path == path)
			{
				if (filen != null)
					p.CacheFileName = filen;
				if (dirn != null)
					p.CacheDirectoryName = dirn;

				if (lp != null)
				{
					lp.Next = p.Next;
					/* !lp means we're already cache_top */
					p.Next = s_cacheTop;
					s_cacheTop = p;
				}

				return;
			}
		}

		var np = new Cache();
		np.Path = path;
		np.CacheFileName = filen;
		np.CacheDirectoryName = dirn;
		np.Next = s_cacheTop;
		s_cacheTop = np;
	}

	public static void CacheLookup(string path, FileList? fl, DirectoryList? dl)
	{
		if (fl != null)
			fl.SelectedIndex = 0;
		if (dl != null)
			dl.SelectedIndex = 0;

		for (var p = s_cacheTop; p != null; p = p.Next)
		{
			if (p.Path == path)
			{
				fl?.SelectFileByName(p.CacheFileName);
				dl?.SelectDirectoryByName(p.CacheDirectoryName);
				break;
			}
		}
	}

	/* Normalize a path (remove /../ and stuff, condense multiple slashes, etc.)
	this will return NULL if the path could not be normalized (not well-formed?).
	the returned string must be free()'d. */
	/* This function should:
		- strip out any parent directory references ("/sheep/../goat" => "/goat")
		- switch slashes to backslashes for MS systems ("c:/winnt" => "c:\\winnt")
		- condense multiple slashes into one ("/sheep//goat" => "/sheep/goat")
		- remove any trailing slashes
	*/
	public static string NormalizePath(string path)
	{
		var builder = new StringBuilder();

		if (Path.IsPathRooted(path))
			builder.Append(Path.GetPathRoot(path));

		var componentList = path
			.Replace('\\', '/')
			.TrimEnd('/')
			.Split('/', StringSplitOptions.RemoveEmptyEntries)
			.ToList();

		for (int i = 0; i < componentList.Count; i++)
		{
			if (componentList[i] == "..")
			{
				componentList.RemoveAt(i);
				if (i > 0)
					componentList.RemoveAt(i - 1);

				i--;
			}
		}

		if (componentList.Any())
		{
			foreach (var component in componentList)
				builder.Append(component).Append('/');

			builder.Length--;
		}

		if (Path.DirectorySeparatorChar == '\\')
		{
			for (int i = 0; i < builder.Length; i++)
				if (builder[i] == '/')
					builder[i] = '\\';
		}

		return builder.ToString();
	}
}
