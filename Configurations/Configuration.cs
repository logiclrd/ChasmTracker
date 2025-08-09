using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace ChasmTracker.Configurations;

using ChasmTracker.FileSystem;

public static class Configuration
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
	public static GeneralConfiguration General = new GeneralConfiguration();
	public static PatternEditorConfiguration PatternEditor = new PatternEditorConfiguration();
	public static DirectoriesConfiguration Directories = new DirectoriesConfiguration();
	[ConfigurationKey("Diskwriter")]
	public static DiskWriterConfiguration DiskWriter = new DiskWriterConfiguration();
	public static InfoPageConfiguration InfoPage = new InfoPageConfiguration();
	[ConfigurationKey("MIDI")]
	public static MIDIConfiguration MIDI = new MIDIConfiguration();
	[ConfigurationKey("MIDI Port %d", FirstIndex = 1)]
	public static List<MIDIPortConfiguration> MIDIPorts = new List<MIDIPortConfiguration>();

	public static StartupFlags StartupFlags;

	public static readonly Dictionary<object, List<string>> CommentsByOwner = new Dictionary<object, List<string>>();

	public static readonly Dictionary<string, ConfigurationSection> SectionByKey = typeof(Configuration)
		.GetFields(BindingFlags.Static | BindingFlags.Public)
		.Where(field => typeof(ConfigurationSection).IsAssignableFrom(field.FieldType))
		.Select(field => (Key: GetSectionKey(field), Value: field.GetValue(null) as ConfigurationSection))
		.Where(entry => entry.Value != null)
		.ToDictionary(entry => entry.Key, entry => entry.Value!, comparer: StringComparer.InvariantCultureIgnoreCase);

	static List<(string Prefix, int FirstIndex, FieldInfo Field, Type ElementType)> s_listSections = typeof(Configuration)
		.GetFields(BindingFlags.Static | BindingFlags.Public)
		.Where(field => field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() == typeof(List<>))
		.Select(field => (Field: field, ElementType: field.FieldType.GetGenericArguments()[0]))
		.Where(entry => typeof(ConfigurationSection).IsAssignableFrom(entry.ElementType))
		.Select(entry => (Key: GetSectionKey(entry.Field), FirstIndex: GetListSectionFirstIndex(entry.Field), entry.Field, entry.ElementType))
		.Where(entry => entry.Key.EndsWith("%d"))
		.Select(entry => (PrefixOrigin: entry.Key.Substring(0, entry.Key.Length - 2), entry.FirstIndex, entry.Field, entry.ElementType))
		.ToList();

	static Dictionary<Type, EnumParseConfiguration> s_enumParseConfigurations = new Dictionary<Type, EnumParseConfiguration>();

	static List<Type> _configurableTypes = typeof(Configuration).Assembly
		.GetTypes()
		.Where(t => typeof(IConfigurable).IsAssignableFrom(t))
		.ToList();

	public static void InitializeDirectory()
	{
		string dotDirectory = Paths.GetDotDirectoryPath();

		string appDir = Path.GetDirectoryName(typeof(Configuration).Assembly.Location)!;

		string portableFile = Path.Combine(appDir, "portable.txt");

		if (File.Exists(portableFile))
		{
			Console.WriteLine("In portable mode.");

			Directories.DotSchism = appDir;
		}
		else
		{
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
	}

	static Regex s_commentLineRegex = new Regex(@"^\s*[#;]");

	public static bool IsComment(string line) => s_commentLineRegex.IsMatch(line);

	public static void Load(string fileName)
	{
		using (var reader = new StreamReader(fileName))
			Load(fileName, reader);
	}

	public static void Load(string fileName, TextReader reader)
	{
		CommentsByOwner.Clear();

		foreach (var section in SectionByKey.Values)
			section.Clear();

		List<string> comments = new List<string>();
		string currentSectionName = "<invalid>";
		ConfigurationSection? currentSection = null;

		while (true)
		{
			string? line = reader.ReadLine();

			if (line == null)
				break;

			if (s_commentLineRegex.IsMatch(line))
				comments.Add(line);
			else if (line.StartsWith("[") && line.EndsWith("]"))
			{
				string sectionName = line.Substring(1, line.Length - 2);

				if (GetSectionListItem(sectionName, out currentSection)
				 || SectionByKey.TryGetValue(sectionName, out currentSection))
				{
					CommentsByOwner[currentSection] = comments;
					comments.Clear();

					currentSectionName = sectionName;
				}
				else
				{
					Console.WriteLine("IGNORING: " + line);
					comments.Add("# " + line);
				}
			}
			else if ((currentSection == null) || !currentSection.Parse(fileName, currentSectionName, line, comments))
			{
				Console.WriteLine("IGNORING 2: " + line);
				comments.Add("# " + line);
			}
		}

		CommentsByOwner[typeof(Configuration)] = comments;

		foreach (var section in SectionByKey.Values)
		{
			section.FinalizeLoad();

			ConfigurableLoadConfiguration(section);
		}
	}

	public static void RegisterConfigurable(IConfigurable configurable)
	{
		s_configurables.Add(configurable);
	}

	public static void RegisterListGatherer(IGatherConfigurationSections configurable)
	{
		s_listGatherers.Add(configurable);
	}

	static List<IConfigurable> s_configurables = new List<IConfigurable>();
	static List<IGatherConfigurationSections> s_listGatherers = new List<IGatherConfigurationSections>();

	static MethodInfo s_configurableLoadConfigurationMethodDefinition = typeof(Configuration)
		.GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
		.Where(method => method.Name == nameof(ConfigurableLoadConfiguration) && method.IsGenericMethodDefinition)
		.Single();

	static MethodInfo s_configurableSaveConfigurationMethodDefinition = typeof(Configuration)
		.GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
		.Where(method => method.Name == nameof(ConfigurableSaveConfiguration) && method.IsGenericMethodDefinition)
		.Single();

	static void ConfigurableLoadConfiguration(ConfigurationSection section)
		=> s_configurableLoadConfigurationMethodDefinition.CreateDelegate(section.GetType()).DynamicInvoke(section);
	static void ConfigurableSaveConfiguration(ConfigurationSection section)
		=> s_configurableSaveConfigurationMethodDefinition.CreateDelegate(section.GetType()).DynamicInvoke(section);

	static void ConfigurableLoadConfiguration<T>(T config)
		where T : ConfigurationSection
	{
		foreach (var configurable in s_configurables)
			if (configurable is IConfigurable<T> configurablePage)
				configurablePage.LoadConfiguration(config);
	}

	static void ConfigurableSaveConfiguration<T>(T config)
		where T : ConfigurationSection
	{
		foreach (var configurable in s_configurables)
			if (configurable is IConfigurable<T> configurablePage)
				configurablePage.SaveConfiguration(config);
	}

	static bool GetSectionListItem(string sectionName, out ConfigurationSection section)
	{
		foreach (var listSection in s_listSections)
		{
			if (sectionName.StartsWith(listSection.Prefix)
			 && int.TryParse(sectionName.AsSpan().Slice(listSection.Prefix.Length), out var index))
			{
				var list = (System.Collections.IList)listSection.Field.GetValue(null)!;

				if (list == null)
				{
					var listType = typeof(List<>).MakeGenericType(listSection.ElementType);

					list = (System.Collections.IList)Activator.CreateInstance(listType)!;
					listSection.Field.SetValue(null, list);
				}

				while (index >= list.Count)
					list.Add((dynamic)Activator.CreateInstance(listSection.ElementType)!);

				section = (ConfigurationSection)list[index]!;
				return true;
			}
		}

		section = default!;
		return false;
	}

	public static void Save(string fileName)
	{
		using (var writer = new StreamWriter(fileName))
			Save(writer);
	}

	public static void Save(TextWriter writer)
	{
		foreach (var section in SectionByKey.Values)
		{
			ConfigurableSaveConfiguration(section);

			section.PrepareToSave();
		}

		foreach (var listSection in s_listSections)
		{
			var list = (System.Collections.IList?)listSection.Field.GetValue(null);

			if (list == null)
				continue;

			var gathererType = typeof(IGatherConfigurationSections<>).MakeGenericType(listSection.ElementType);
			var listType = typeof(IList<>).MakeGenericType(listSection.ElementType);

			if (listType.IsAssignableFrom(list.GetType()))
			{
				foreach (var gatherer in s_listGatherers)
					if (gathererType.IsAssignableFrom(gatherer.GetType()))
						((dynamic)gatherer).GatherConfiguration(list);
			}

			foreach (var section in list.OfType<ConfigurationSection>())
				section.PrepareToSave();
		}

		foreach (var section in SectionByKey)
		{
			if (CommentsByOwner.TryGetValue(section, out var sectionComments))
				sectionComments.ForEach(writer.WriteLine);

			section.Value.Format(section.Key, writer);
		}

		foreach (var listSection in s_listSections)
		{
			var list = (System.Collections.IList?)listSection.Field.GetValue(null);

			if (list == null)
				continue;

			for (int i = listSection.FirstIndex; i < list.Count; i++)
			{
				var section = (ConfigurationSection?)list[i];

				if (section == null)
					continue;

				if (CommentsByOwner.TryGetValue(section, out var sectionComments))
					sectionComments.ForEach(writer.WriteLine);

				section.Format(listSection.Prefix + i, writer);
			}
		}

		if (CommentsByOwner.TryGetValue(typeof(Configuration), out var trailingComments))
			trailingComments.ForEach(writer.WriteLine);
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

	static bool IsConfigurationSectionListType(Type type)
	{
		if (!type.IsGenericType)
			return false;

		if (type.GetGenericTypeDefinition() != typeof(List<>))
			return false;

		var elementType = type.GetGenericArguments()[0];

		return typeof(ConfigurationSection).IsAssignableFrom(elementType);
	}

	public static string GetSectionKey(MemberInfo member)
	{
		if (member is FieldInfo field)
		{
			if (!typeof(ConfigurationSection).IsAssignableFrom(field.FieldType) && !IsConfigurationSectionListType(field.FieldType))
				throw new Exception("GetSectionKey called on a member that does not yield a ConfigurationSection");
		}
		else if (member is PropertyInfo property)
		{
			if (!typeof(ConfigurationSection).IsAssignableFrom(property.PropertyType) && !IsConfigurationSectionListType(property.PropertyType))
				throw new Exception("GetSectionKey called on a member that does not yield a ConfigurationSection");
		}
		else
			throw new Exception("GetSectionKey called on a member that is not a field or a property");

		if (member.GetCustomAttribute<ConfigurationKeyAttribute>() is ConfigurationKeyAttribute @override)
			return @override.Key;

		return ConvertPascalCaseToWords(member.Name, forceLower: false, wordSeparator: ' ');
	}

	public static int GetListSectionFirstIndex(MemberInfo member)
	{
		if (member.GetCustomAttribute<ConfigurationKeyAttribute>() is ConfigurationKeyAttribute config)
			return config.FirstIndex;

		return 0;
	}

	public static string GetValueKey(MemberInfo member)
	{
		if (!(member is FieldInfo field) && !(member is PropertyInfo property))
			throw new Exception("GetSectionKey called on a member that is not a field or a property");

		if (member.GetCustomAttribute<ConfigurationKeyAttribute>() is ConfigurationKeyAttribute @override)
			return @override.Key;

		return ConvertPascalCaseToWords(member.Name, forceLower: true, wordSeparator: '_');
	}

	public static bool ParseValue([NotNullWhen(true)] FieldInfo? field, string value, out object? parsedValue)
	{
		parsedValue = default;

		if (field == null)
			return false;

		bool serializeEnumAsInt = field.GetCustomAttribute<SerializeEnumAsIntAttribute>() != null;

		return ParseValue(field.FieldType, value, serializeEnumAsInt, out parsedValue);
	}

	public static bool ParseValue(Type fieldType, string value, bool serializeEnumAsInt, out object? parsedValue)
	{
		parsedValue = default;

		if (fieldType == typeof(string))
		{
			parsedValue = value;
			return true;
		}
		else if (fieldType == typeof(int))
		{
			int p = 0;
			bool good = true;

			for (int i = 0; i < value.Length; i++)
			{
				if (char.IsDigit(value[i]))
					p = p * 10 + (value[i] - '0');
				else
				{
					good = false;
					break;
				}
			}

			parsedValue = p;
			return good;
		}
		else if (fieldType == typeof(bool))
		{
			if ((value == "1") || value.Equals("true", StringComparison.InvariantCultureIgnoreCase))
			{
				parsedValue = true;
				return true;
			}
			else if ((value == "0") || value.Equals("false", StringComparison.InvariantCultureIgnoreCase))
			{
				parsedValue = false;
				return true;
			}

			return false;
		}
		else if (fieldType.IsEnum)
		{
			if (serializeEnumAsInt)
			{
				if (int.TryParse(value, out var intValue))
				{
					parsedValue = Enum.ToObject(fieldType, intValue);
					return true;
				}
			}
			else
			{
				if (!s_enumParseConfigurations.TryGetValue(fieldType, out var enumParseConfiguration))
				{
					enumParseConfiguration = BuildEnumParseConfiguration(fieldType);

					s_enumParseConfigurations[fieldType] = enumParseConfiguration;
				}

				if (enumParseConfiguration.ValueByName.TryGetValue(value.Trim(), out var parsedEnumValue))
				{
					parsedValue = parsedEnumValue;
					return true;
				}
			}

			return false;
		}

		return false;
	}

	public static string FormatValue(Type type, bool serializeEnumAsInt, object? value)
	{
		if (value == null)
			return "";

		if (type == typeof(bool))
			return ((bool)value) ? "1" : "0";
		else if (type.IsEnum)
		{
			if (serializeEnumAsInt)
				return ((int)(value ?? 0)).ToString();
			else
				return value.ToString()?.ToLowerInvariant() ?? "";
		}
		else
			return value.ToString() ?? "";
	}

	static EnumParseConfiguration BuildEnumParseConfiguration(Type enumType)
	{
		var config = new EnumParseConfiguration();

		foreach (var field in enumType.GetFields(BindingFlags.Static | BindingFlags.Public))
		{
			if (field.GetCustomAttribute<ConfigurationDefaultAttribute>() != null)
				continue;

			var value = field.GetValue(null);

			if ((value == null) || (value.GetType() != enumType))
				continue;

			var serializedValueAttribute = field.GetCustomAttribute<ConfigurationValueAttribute>();

			if (serializedValueAttribute != null)
			{
				if (serializedValueAttribute.EverythingElse)
					config.WildcardValue = (Enum)value;
				else if (serializedValueAttribute.NotSet)
					config.ValueWhenNull = (Enum)value;
				else
				{
					var enumValue = (Enum)value;

					config.ValueByName[serializedValueAttribute.Value] = enumValue;
					config.NameByValue[enumValue] = serializedValueAttribute.Value;
				}
			}
			else
			{
				var enumValue = (Enum)value;

				config.ValueByName[field.Name] = enumValue;
				config.NameByValue[enumValue] = field.Name;
			}
		}

		return config;
	}
}
