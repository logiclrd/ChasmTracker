using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ChasmTracker.FileSystem;

public class DirectoryScanner
{
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
	public static bool Populate(string directory, FileList fileList, DirectoryList? dirList = null, Action<string, FileList, DirectoryList?> loadLibrary = null)
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
}
