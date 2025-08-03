using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

using ChasmTracker.FileSystem;
using ChasmTracker.MIDI;

namespace ChasmTracker.Configurations;

public class Configuration
{
	// Naming convention:
	// - By default, section names are converted to "Separate Words"
	//   and value names are converted to snake_case.
	// - If this is inadequate, it can be overridden using [ConfigurationKey].
	public static VideoConfiguration Video = new VideoConfiguration();
	public static AudioConfiguration Audio = new AudioConfiguration();

	// hmmm....
	//     [Equalizer]
	//     low_band=freq/gain
	//     med_low_band=freq/gain
	//     etc.
	// would be a cleaner way of storing this
	public static EQBandConfiguration EQLowBand = new EQBandConfiguration(0);
	public static EQBandConfiguration EQMedLowBand = new EQBandConfiguration(1);
	public static EQBandConfiguration EQMedHighBand = new EQBandConfiguration(2);
	public static EQBandConfiguration EQHighBand = new EQBandConfiguration(3);

	[ConfigurationKey("Mixer Settings")]
	public static MixerConfiguration Mixer = new MixerConfiguration();
	public static BackupsConfiguration Backups = new BackupsConfiguration();
	public static GeneralConfiguration General = new GeneralConfiguration();
	public static PatternEditorConfiguration PatternEditor = new PatternEditorConfiguration();
	public static FilesConfiguration Files = new FilesConfiguration();
	public static DirectoriesConfiguration Directories = new DirectoriesConfiguration();
	public static KeyboardConfiguration Keyboard = new KeyboardConfiguration();
	public static InfoPageConfiguration InfoPage = new InfoPageConfiguration();
	public static MIDIConfiguration MIDI = new MIDIConfiguration();
	[ConfigurationKey("MIDI Port %d")] // TODO: lists
	public static List<MIDIPortConfiguration> MIDIPorts = new List<MIDIPortConfiguration>();

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

	public static void Save()
	{
		// TODO
	}

	static string ConvertPascalCaseToWords(string pascalCase, bool forceLower, char wordSeparator)
	{
		// Tests:
		// Bob => Bob or bob
		// BobDNA => Bob DNA or bob_dna
		// BobMary => Bob Mary or bob_mary
		// DNABob => DNA Bob or dna_bob
		// BobDNAMary => Bob DNA Mary or bob_dna_mary
		// BobF => Bob F or bob_f
		// FBobF => F Bob F or f_bob_f
		var builder = new StringBuilder();

		for (int i = 0; i < pascalCase.Length; i++)
		{
			if (char.IsUpper(pascalCase, i)
			 && (i > 0)
			 && (char.IsLower(pascalCase, i - 1) || char.IsLower(pascalCase, i + 1)))
				builder.Append(wordSeparator);

			char ch = pascalCase[i];

			if (forceLower)
				ch = char.ToLowerInvariant(ch);

			builder.Append(ch);
		}

		return builder.ToString();
	}

	static string GetSectionKey(MemberInfo member)
	{
		if (member is FieldInfo field)
		{
			if (!typeof(ConfigurationSection).IsAssignableFrom(field.FieldType))
				throw new Exception("GetSectionKey called on a member that does not yield a ConfigurationSection");
		}
		else if (member is PropertyInfo property)
		{
			if (!typeof(ConfigurationSection).IsAssignableFrom(property.PropertyType))
				throw new Exception("GetSectionKey called on a member that does not yield a ConfigurationSection");
		}
		else
			throw new Exception("GetSectionKey called on a member that is not a field or a property");

		if (member.GetCustomAttribute<ConfigurationKeyAttribute>() is ConfigurationKeyAttribute @override)
			return @override.Key;

		return ConvertPascalCaseToWords(member.Name, forceLower: false, wordSeparator: ' ');
	}

	static string GetValueKey(MemberInfo member)
	{
		if (!(member is FieldInfo field) && !(member is PropertyInfo property))
			throw new Exception("GetSectionKey called on a member that is not a field or a property");

		if (member.GetCustomAttribute<ConfigurationKeyAttribute>() is ConfigurationKeyAttribute @override)
			return @override.Key;

		return ConvertPascalCaseToWords(member.Name, forceLower: true, wordSeparator: '_');
	}

	public static void LoadMIDIPortConfiguration(MIDIPort port)
	{

	}
}
