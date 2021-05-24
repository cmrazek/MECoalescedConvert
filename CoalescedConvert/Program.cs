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
			Console.WriteLine("-h, --help  Show this help info.");
			Console.WriteLine("-me12le     Use Mass Effect 1/2 Legendary Edition format.");
			Console.WriteLine("-me3le      Use Mass Effect 3 Legendary Edition format.");
			Console.WriteLine();

			return false;
		}

		private bool ProcessArguments(string[] args)
		{
			foreach (var arg in args)
			{
				var argLower = arg.ToLower();
				if (argLower == "-h" || argLower == "--help")
				{
					return ShowUsage(null);
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
				_outputFileName = Path.Combine(Path.GetDirectoryName(_inputFileName), string.Concat(Path.GetFileNameWithoutExtension(_inputFileName), ".ini"));
				_encode = false;
			}
			else if (ext == ".ini")
			{
				_outputFileName = Path.Combine(Path.GetDirectoryName(_inputFileName), string.Concat(Path.GetFileNameWithoutExtension(_inputFileName), ".bin"));
				_encode = true;
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

			if (_encode)
			{
				Console.WriteLine($"Converting INI:\n{_inputFileName}");
				Console.WriteLine($"To BIN:\n{_outputFileName}");

				if (_format == CoalescedFormat.Unknown) _format = CoalescedFormatDetector.Detect(_outputFileName);
				if (_format == CoalescedFormat.Unknown) throw new UnknownCoalescedFormatException();

				var backupFileName = _outputFileName + ".bak";
				if (!File.Exists(backupFileName))
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
						throw new NotSupportedException();
					default:
						throw new UnknownCoalescedFormatException();
				}

				Console.WriteLine("Success");
			}
			else
			{
				Console.WriteLine($"Converting BIN:\n{_inputFileName}");
				Console.WriteLine($"To INI:\n{_outputFileName}");

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
