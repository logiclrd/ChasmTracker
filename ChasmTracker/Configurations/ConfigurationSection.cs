using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;

namespace ChasmTracker.Configurations;

using ChasmTracker.Utility;

public abstract class ConfigurationSection
{
	List<string> _sequence = new List<string>();
	Dictionary<string, FieldInfo> _fieldByKey;
	List<(ArrayMemberNamingAttribute Naming, FieldInfo Field)> _arrays;

	public int Index; // Only used for list-type sections (e.g. EQ)

	protected ConfigurationSection()
	{
		_fieldByKey = GetType().GetFields()
			.ToDictionary(
				keySelector: field => Configuration.GetValueKey(field),
				elementSelector: field => field,
				comparer: StringComparer.InvariantCultureIgnoreCase);

		_arrays = GetType().GetFields()
			.Select(field => (Naming: field.GetCustomAttribute<ArrayMemberNamingAttribute>(), Field: field))
			.Where(array => array.Naming != null)
			.Select(entry => (entry.Naming!, entry.Field!))
			.ToList();
	}

	protected virtual ConfigurationSection CreatePristine() => (ConfigurationSection)Activator.CreateInstance(GetType())!;

	public void Clear()
	{
		var pristine = CreatePristine();

		foreach (var field in GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
			field.SetValue(this, field.GetValue(pristine));
	}

	public bool Parse(string fileName, string sectionName, string line, List<string> comments)
	{
		int separator = line.IndexOf('=');

		if (separator < 0)
			return false;

		_sequence.AddRange(comments);
		comments.Clear();

		string key = line.Substring(0, separator).Trim();
		string value = line.Substring(separator + 1);

		for (int i = 0; i < _sequence.Count; i++)
		{
			if (_sequence[i].Equals(key, StringComparison.InvariantCultureIgnoreCase))
			{
				Console.Error.WriteLine("{0}: duplicate key \"{1}\" in section \"{2}\"; overwriting",
					fileName,
					key,
					sectionName);

				_sequence.RemoveAt(i);

				break;
			}
		}

		value = value.Unescape();

		if (_fieldByKey.TryGetValue(key, out var field)
		 && Configuration.ParseValue(field, value, out var parsedValue))
		{
			_sequence.Add(key);
			field.SetValue(this, parsedValue);
		}
		else if (IsArrayMember(key, out var arrayField, out var elementType, out var length, out var index)
		      && Configuration.ParseValue(elementType, value, false, out var parsedArrayElementValue))
		{
			if ((index < 0) || (index >= length))
				_sequence.Add("# " + line);
			else
			{
				var fieldValue = arrayField.GetValue(this);

				if (!(fieldValue is Array array))
				{
					array = Array.CreateInstance(elementType, length);
					arrayField.SetValue(this, array);
				}

				_sequence.Add(key);
				array.SetValue(parsedArrayElementValue, index);
			}
		}
		else
			_sequence.Add("# " + line);

		return true;
	}

	public void Format(string sectionName, TextWriter writer)
	{
		writer.WriteLine("[{0}]", sectionName);

		foreach (var key in _sequence)
		{
			Type fieldType;
			object? rawValue = null;
			bool serializeEnumAsInt = false;

			if (_fieldByKey.TryGetValue(key, out var field))
			{
				fieldType = field.FieldType;
				rawValue = field.GetValue(this);

				serializeEnumAsInt = field.GetCustomAttribute<SerializeEnumAsIntAttribute>() != null;
			}
			else if (IsArrayMember(key, out var arrayField, out var elementType, out int length, out int index))
			{
				var fieldValue = arrayField.GetValue(this);

				if (!(fieldValue is Array array))
					array = Array.CreateInstance(elementType, length);

				fieldType = elementType;
				rawValue = array.GetValue(index);
			}
			else
			{
				// must be a comment
				writer.WriteLine(key);
				continue;
			}

			string value = Configuration.FormatValue(fieldType, serializeEnumAsInt, rawValue);

			writer.WriteLine("{0}={1}", key, value.Escape());
		}
	}

	bool IsArrayMember(string key, [NotNullWhen(true)] out FieldInfo? arrayField, [NotNullWhen(true)] out Type? elementType, out int length, out int index)
	{
		arrayField = default;
		elementType = default;
		length = default;
		index = default;

		foreach (var array in _arrays)
		{
			if (key.StartsWith(array.Naming.Prefix))
			{
				string indexStr = key.Substring(array.Naming.Prefix.Length);

				var style = NumberStyles.None;

				if (array.Naming.IndexFormat.Contains("X"))
					style = NumberStyles.AllowHexSpecifier;

				if (int.TryParse(indexStr, style, CultureInfo.InvariantCulture, out index))
				{
					arrayField = array.Field;
					elementType = arrayField.FieldType.GetElementType()!;
					length = array.Naming.Length;

					return true;
				}
			}
		}

		return false;
	}

	public virtual void FinalizeLoad() { }
	public virtual void PrepareToSave() { }
}
