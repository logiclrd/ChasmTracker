using System;
using System.IO;
using System.Text;

namespace ChasmTracker;

public class CommandLineArguments
{
	public string? AudioDriverSpec;
	public string? VideoDriverSpec;
	public bool WantFullScreen;
	public bool WantFullScreenSpecified;
	public bool NetworkMIDI;
	public bool ClassicMode, ClassicModeSpecified;
	public bool PlayOnStartup;
	public bool FontEditor;
	public bool EnableHooks = true; /* --no-hooks: don't run startup/exit scripts */
	public bool Headless;
	/* diskwrite? */
	public string? DiskwriteTo;
	/* initial module directory */
	public string? InitialDirectory;
	/* filename of song to load on startup, or NULL for none */
	public string? InitialSong;

	public CommandLineArguments()
		: this(SkipArgZero(Environment.CommandLine))
	{
	}

	string ExtractArgument(ref string input, out bool hasValue)
	{
		var buffer = new StringBuilder();

		bool inQuotes = false;

		int offset = 0;

		hasValue = false;

		while (offset < input.Length)
		{
			if (inQuotes)
			{
				if (input[offset] == '"')
					inQuotes = false;
				else
					buffer.Append(input[offset]);
			}
			else
			{
				if (input[offset] == '"')
					inQuotes = true;
				else if ((input[offset] == ' ') || (input[offset] == '='))
					break;
				else
					buffer.Append(input[offset]);
			}

			offset++;
		}

		if ((offset < input.Length) && (input[offset] == '='))
		{
			offset++;
			hasValue = true;
		}
		else
		{
			while ((offset < input.Length) && (input[offset] == ' '))
				offset++;
		}

		input = input.Substring(offset);

		return buffer.ToString();
	}

	public CommandLineArguments(string commandLine)
	{
		while (commandLine.Length > 0)
		{
			string arg = ExtractArgument(ref commandLine, out var hasValue);

			void NoValue() { if (hasValue) throw new Exception("Command-line error: " + arg + " does not take a value"); }

			if ((arg == "--audio-driver") || (arg == "-a"))
				AudioDriverSpec = ExtractArgument(ref commandLine, out _);
			else if ((arg == "--video-driver") || (arg == "-v"))
			{
				/* this is largely only here for historical reasons, as
				 * old Schism used to be able to utilize:
				 *   1. SDL 1.2 surfaces
				 *   2. YUV overlays
				 *   3. OpenGL <3.0
				 *   4. DirectDraw
				 * However, all of this cruft has been ripped out over
				 * time. Possibly we could re-add OpenGL (maybe YUV
				 * overlays as well) but the way its implemented in
				 * SDL 1.2 seems to be buggy, and that's really the
				 * only place where it's actually useful. */
				VideoDriverSpec = ExtractArgument(ref commandLine, out _);
			}
			else if ((arg == "--network") || (arg == "--no-network"))
			{
				NoValue();
				NetworkMIDI = (arg == "--network");
			}
			else if ((arg == "--classic") || (arg == "--no-classic"))
			{
				NoValue();
				ClassicMode = (arg == "--classic");
				ClassicModeSpecified = true;
			}
			else if ((arg == "--fullscreen") || (arg == "-f")
						|| (arg == "--no-fullscreen") || (arg == "-F"))
			{
				NoValue();
				WantFullScreen = (arg == "--fullscreen") || (arg == "-f");
				WantFullScreenSpecified = true;
			}
			else if ((arg == "--play") || (arg == "-p")
						|| (arg == "--no-play") || (arg == "-P"))
			{
				NoValue();
				PlayOnStartup = (arg == "--play") || (arg == "-p");
			}
			else if ((arg == "--font-editor") || (arg == "--no-font-editor"))
			{
				NoValue();
				FontEditor = (arg == "--font-editor");
			}
			else if (arg == "--diskwrite")
				DiskwriteTo = ExtractArgument(ref commandLine, out _);
			else if ((arg == "--hooks") || (arg == "--no-hooks"))
			{
				NoValue();
				if (arg == "--hooks")
					EnableHooks = true;
				else
					EnableHooks = false;
			}
			else if (arg == "--headless")
			{
				NoValue();
				Headless = true;
			}
			else if (arg == "--version")
			{
				NoValue();

				Console.WriteLine(
					"Chasm Tracker {0} {1}",
					typeof(Program).Assembly.GetName().Version,
					BuildInformation.Timestamp);
				Console.WriteLine(Copyright.ShortCopyright);

				Environment.Exit(0);
			}
			else if ((arg == "--help") || (arg == "-h"))
			{
				NoValue();

				OutputUsage();

				Console.WriteLine("  -a, --audio-driver=DRIVER");
				Console.WriteLine("  -v, --video-driver=DRIVER");
				Console.WriteLine("      --classic (--no-classic)");
				Console.WriteLine("  -f, --fullscreen (-F, --no-fullscreen)");
				Console.WriteLine("  -p, --play (-P, --no-play)");
				Console.WriteLine("      --diskwrite=FILENAME");
				Console.WriteLine("      --font-editor (--no-font-editor)");
				Console.WriteLine("      --hooks (--no-hooks)");
				Console.WriteLine("      --headless");
				Console.WriteLine("      --version");
				Console.WriteLine("  -h, --help");
				Console.WriteLine("Refer to the documentation for complete usage details.");

				Environment.Exit(0);
			}
			else if (arg.StartsWith('-'))
			{
				OutputUsage(Console.Error);
				Environment.Exit(2);
			}
			else
			{
				if (Directory.Exists(arg))
					InitialDirectory = Path.GetFullPath(arg);
				else
					InitialSong = Path.GetFullPath(arg);
			}
		}
	}

	static string SkipArgZero(string commandLine)
	{
		bool inQuote = false;
		char quoteChar = '\0';

		for (int i=0; i < commandLine.Length; i++)
		{
			switch (commandLine[i])
			{
				case '\'':
				case '"':
					if (inQuote && (commandLine[i] == quoteChar))
						inQuote = false;
					else
					{
						inQuote = true;
						quoteChar = commandLine[i];
					}
					break;

				case ' ':
					if (!inQuote)
						return commandLine.Substring(i + 1).TrimStart();
					break;
			}
		}

		return "";
	}

	string GetArgZero()
	{
		return Environment.GetCommandLineArgs()[0];
	}

	public void OutputUsage(TextWriter? output = null)
	{
		output ??= Console.Out;

		output.WriteLine("Usage: {0} [OPTIONS] [DIRECTORY] [FILE]", GetArgZero());
	}
}
