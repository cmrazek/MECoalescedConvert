using System;
using System.IO;

namespace CoalescedConvert
{
	class Program
	{
		private string _inputFileName;
		private string _outputFileName;
		private bool _encode;
		private CoalescedFormat _format;
		private bool _verbose;

		static void Main(string[] args)
		{
			try
			{
				Environment.ExitCode = new Program().Run(args);
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.ToString());
				Environment.ExitCode = 1;
			}
		}

		private bool ShowUsage(string message)
		{
			if (!string.IsNullOrEmpty(message))
			{
				Console.WriteLine(message);
				Console.WriteLine();
			}
			Console.WriteLine("CoalescedConvert");
			Console.WriteLine("  Converts a coalesced.bin file into coalesced.ini, or vice-versa.");
			Console.WriteLine();
			Console.WriteLine("Usage:");
			Console.WriteLine("CoelescedConvert.exe <coalesced_int.bin / coalesced_int.ini> [switches]");
			Console.WriteLine();
			Console.WriteLine("Switches");
			Console.WriteLine("-h, --help     Show this help info.");
			Console.WriteLine("-v, --verbose  Show verbose logging.");
			Console.WriteLine("-o, --out      Output file name (optional)");
			Console.WriteLine("-me12le        Use Mass Effect 1/2 Legendary Edition format.");
			Console.WriteLine("-me3le         Use Mass Effect 3 Legendary Edition format.");
			Console.WriteLine();

			return false;
		}

		private bool ProcessArguments(string[] args)
		{
			for (int a = 0; a < args.Length; a++)
			{
				var arg = args[a];
				var argLower = arg.ToLower();

				if (argLower == "-h" || argLower == "--help")
				{
					return ShowUsage(null);
				}
				else if (argLower == "-v" || argLower == "--verbose")
				{
					_verbose = true;
				}
				else if (argLower == "-o" || argLower == "--out")
				{
					if (a + 1 >= args.Length) return ShowUsage($"Expected file name to follow '{arg}'.");
					_outputFileName = args[++a];
				}
				else if (argLower == "-me12le")
				{
					_format = CoalescedFormat.MassEffect12LE;
				}
				else if (argLower == "-me3le")
				{
					_format = CoalescedFormat.MassEffect3LE;
				}
				else if (string.IsNullOrEmpty(_inputFileName))
				{
					_inputFileName = arg;
				}
				else
				{
					return ShowUsage($"Unexpected argument '{arg}'.");
				}
			}

			if (string.IsNullOrEmpty(_inputFileName)) return ShowUsage("Input file name is required.");

			var ext = Path.GetExtension(_inputFileName).ToLower();
			if (ext == ".bin")
			{
				_encode = false;
				if (string.IsNullOrEmpty(_outputFileName))
				{
					_outputFileName = Path.Combine(Path.GetDirectoryName(_inputFileName), string.Concat(Path.GetFileNameWithoutExtension(_inputFileName), ".ini"));
				}
			}
			else if (ext == ".ini")
			{
				_encode = true;
				if (string.IsNullOrEmpty(_outputFileName))
				{
					_outputFileName = Path.Combine(Path.GetDirectoryName(_inputFileName), string.Concat(Path.GetFileNameWithoutExtension(_inputFileName), ".bin"));
				}
			}
			else
			{
				return ShowUsage("File name must have an extension of .bin or .ini");
			}

			return true;
		}

		private int Run(string[] args)
		{
			if (!ProcessArguments(args)) return 1;

			Log.Verbose = _verbose;

			if (_encode)
			{
				Console.WriteLine($"Converting INI:\n{_inputFileName}");
				Console.WriteLine($"To BIN:\n{_outputFileName}");

				if (!File.Exists(_inputFileName)) throw new FileNotFoundException($"The file '{_inputFileName}' could not be found.");

				if (_format == CoalescedFormat.Unknown) _format = CoalescedFormatDetector.Detect(_outputFileName);
				if (_format == CoalescedFormat.Unknown) throw new UnknownCoalescedFormatException();

				var backupFileName = _outputFileName + ".bak";
				if (File.Exists(_outputFileName) && !File.Exists(backupFileName))
				{
					Console.WriteLine($"Backup:\n{backupFileName}");
					File.Copy(_outputFileName, backupFileName);
				}

				switch (_format)
				{
					case CoalescedFormat.MassEffect12LE:
						{
							var converter = new CoalescedConverterME12LE();
							converter.Encode(_inputFileName, _outputFileName);
						}
						break;
					case CoalescedFormat.MassEffect3LE:
						{
							var converter = new CoalescedConverterME3LE();
							converter.Encode(_inputFileName, _outputFileName);
						}
						break;
					default:
						throw new UnknownCoalescedFormatException();
				}

				Console.WriteLine("Success");
			}
			else
			{
				Console.WriteLine($"Converting BIN:\n{_inputFileName}");
				Console.WriteLine($"To INI:\n{_outputFileName}");

				if (!File.Exists(_inputFileName)) throw new FileNotFoundException($"The file '{_inputFileName}' could not be found.");

				if (_format == CoalescedFormat.Unknown) _format = CoalescedFormatDetector.Detect(_inputFileName);
				if (_format == CoalescedFormat.Unknown) throw new UnknownCoalescedFormatException();

				switch (_format)
				{
					case CoalescedFormat.MassEffect12LE:
						{
							var converter = new CoalescedConverterME12LE();
							converter.Decode(_inputFileName, _outputFileName);
						}
						break;
					case CoalescedFormat.MassEffect3LE:
						{
							var converter = new CoalescedConverterME3LE();
							converter.Decode(_inputFileName, _outputFileName);
						}
						break;
					default:
						throw new UnknownCoalescedFormatException();
				}

				Console.WriteLine("Success");
			}

			return 0;
		}
	}
}
