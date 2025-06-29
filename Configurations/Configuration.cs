using System;
using System.IO;
using System.Linq;
using ChasmTracker.FileSystem;

namespace ChasmTracker.Configurations;

public class Configuration
{
	public static VideoConfiguration Video = new VideoConfiguration();
	public static BackupsConfiguration Backups = new BackupsConfiguration();
	public static GeneralConfiguration General = new GeneralConfiguration();
	[ConfigurationKey("Pattern Editor")]
	public static PatternEditorConfiguration PatternEditor = new PatternEditorConfiguration();
	public static FilesConfiguration Files = new FilesConfiguration();
	public static DirectoriesConfiguration Directories = new DirectoriesConfiguration();
	public static KeyboardConfiguration Keyboard = new KeyboardConfiguration();

	public static StartupFlags StartupFlags;

	public static void InitializeDirectory()
	{
		string dotDirectory = Paths.GetDotDirectoryPath();

		foreach (string candidate in Paths.EnumerateDotFolders())
		{
			string fullPath = Path.Combine(candidate);

			if (Directory.Exists(fullPath))
			{
				Directories.DotSchism = fullPath;
				return;
			}
		}

		Directories.DotSchism = Path.Combine(
			dotDirectory,
			Paths.EnumerateDotFolders().First());

		Console.WriteLine("Creating directory {0}", Directories.DotSchism);
		Console.WriteLine("Chasm Tracker uses this directory to store your settings.");

		try
		{
			Directory.CreateDirectory(Directories.DotSchism);
		}
		catch (Exception e)
		{
			Console.Error.WriteLine("Error creating directory: {0}: {1}", e.GetType().Name, e.Message);
			Console.Error.WriteLine("Everything will still work, but preferences will not be saved.");
		}
	}

	public void Save()
	{
		// TODO
	}
}
