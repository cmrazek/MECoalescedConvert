using System;
using System.IO;

namespace CoalescedConvert
{
	class Program
	{
		private string _inputFileName;
		private string _outputFileName;
		private bool _encode;
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
			Console.WriteLine("-h, --help  Show this help info.");
			Console.WriteLine();

			return false;
		}

		private bool ProcessArguments(string[] args)
		{
			foreach (var arg in args)
			{
				if (arg == "-h" || arg == "--help") return ShowUsage(null);
				else if (arg == "-v" || arg == "--verbose") _verbose = true;
				else if (string.IsNullOrEmpty(_inputFileName)) _inputFileName = arg;
				else return ShowUsage($"Unexpected argument '{arg}'.");
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

			var converter = new CoalescedConverter();
			if (_encode)
			{
				Console.WriteLine($"Converting INI:\n{_inputFileName}");
				Console.WriteLine($"To BIN:\n{_outputFileName}");

				var backupFileName = _outputFileName + ".bak";
				if (!File.Exists(backupFileName))
				{
					Console.WriteLine($"Backup:\n{backupFileName}");
					File.Copy(_outputFileName, backupFileName);
				}

				converter.Encode(_inputFileName, _outputFileName, _verbose);

				Console.WriteLine("Success");
			}
			else
			{
				Console.WriteLine($"Converting BIN:\n{_inputFileName}");
				Console.WriteLine($"To INI:\n{_outputFileName}");

				converter.Decode(_inputFileName, _outputFileName, _verbose);

				Console.WriteLine("Success");
			}

			return 0;
		}
	}
}
