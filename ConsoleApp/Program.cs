using System;
using System.IO;
using System.Text;

namespace CoalescedConvert
{
	class Program
	{
		private string _inputFileName;
		private string _outputFileName;
		private bool _verbose;
		private static ConsoleColor _defaultConsoleColor;
		private bool _whatIf;

		public static ConsoleColor DefaultConsoleColor => _defaultConsoleColor;

		static void Main(string[] args)
		{
			_defaultConsoleColor = Console.ForegroundColor;
			try
			{
				Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

				Environment.ExitCode = new Program().Run(args);
			}
			catch (Exception ex)
			{
				Log.Error(ex);
				Environment.ExitCode = 1;
			}
			finally
			{
				Console.ForegroundColor = _defaultConsoleColor;
			}
		}

		private bool ShowUsage(string message)
		{
			//        <------------------------------   80 chars   ---------------------------------->
			if (!string.IsNullOrEmpty(message))
			{
				Log.Warning(message);
				Log.NewLine();
			}
			Log.Heading("Mass Effect Coalesced Convert");
			Log.Info("  Converts a Mass Effect Coalesced file into an INI file, or vice-versa.");
			Log.NewLine();
			Log.Heading("Usage:");
			Log.Info("> mecc.exe <input_file_name> [switches]");
			Log.NewLine();
			Log.Heading("Switches");
			Log.Info("-h, --help             Show this help info.");
			Log.Info("-o, --out <file_name>  Override output file name.");
			Log.Info("-w, --whatIf           Do all processing but don't write the final file.");
			Log.Info("-v, --verbose          Show verbose logging.");
			Log.NewLine();
			Log.Heading("Example:");
			Log.NewLine();
			Log.Info("Export \"Coalesced_INT.bin\" into an INI file:");
			Log.Info("> mecc.exe \"Coalesced_INT.bin\"");
			Log.Info("This generates a new file \"Coalesced-export.ini\" in the same folder.");
			Log.NewLine();
			Log.Info("(make your changes to the INI file)");
			Log.NewLine();
			Log.Info("Import \"Coalesced_INT-export.ini\" back into \"Coalesced_INT.bin\":");
			Log.Info("> mecc.exe \"Coalesced_INT.bin\"");
			Log.Info("This creates a backup of \"Coalesced_INT.bin\" and overwrites the file with the");
			Log.Info("changes you've made to \"Coalesced_INT-export.ini\".");
			Log.NewLine();
			//        <------------------------------   80 chars   ---------------------------------->
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
				else if (argLower == "-w" || argLower == "--whatif")
				{
					_whatIf = true;
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

			return true;
		}

		private int Run(string[] args)
		{
			if (!ProcessArguments(args)) return 1;

			Log.Verbose = _verbose;

			if (!File.Exists(_inputFileName)) throw new FileNotFoundException($"The file '{_inputFileName}' could not be found.");
			var fmt = CoalFormatDetector.Detect(_inputFileName);
			var converter = CoalConverter.Create(fmt.Format);

			if (fmt.IsExport)
			{
				// ini -> bin

				if (string.IsNullOrEmpty(_outputFileName))
				{
					var dir = Path.GetDirectoryName(_inputFileName);
					var fn = Path.GetFileNameWithoutExtension(_inputFileName);
					var dstExt = CoalFormatDetector.GetExtension(fmt.Format).ToLower();
					var srcExt = Path.GetExtension(_inputFileName).ToLower();
					if (fn.EndsWith("-export"))
					{
						fn = fn.Substring(0, fn.Length - "-export".Length);
						_outputFileName = Path.Combine(dir, string.Concat(fn, dstExt));
					}
					else if (srcExt == dstExt)
					{
						_outputFileName = Path.Combine(dir, string.Concat(fn, "-coalesced", dstExt));
					}
					else
					{
						_outputFileName = Path.Combine(dir, string.Concat(fn, dstExt));
					}
				}

				Log.Heading($"Converting {fmt.Format} INI:");
				Log.Info(_inputFileName);
				Log.Heading("To Coalesced:");
				Log.Info(_outputFileName);

				if (File.Exists(_outputFileName))
				{
					var backupFileName = Path.Combine(Path.GetDirectoryName(_outputFileName), string.Concat(Path.GetFileNameWithoutExtension(_outputFileName),
						"-backup", Path.GetExtension(_outputFileName)));
					if (!File.Exists(backupFileName))
					{
						Log.Heading("Backup:");
						Log.Info(backupFileName);
						if (!_whatIf) File.Copy(_outputFileName, backupFileName);
					}
				}

				var doc = null as CoalDocument;
				using (var fs = new FileStream(_inputFileName, FileMode.Open))
				{
					doc = CoalDocument.Load(fs);
				}

				using (var ms = new MemoryStream())
				{
					converter.Save(doc, ms, leaveStreamOpen: true);

					if (!_whatIf)
					{
						var bytes = new byte[ms.Length];
						ms.Position = 0;
						ms.Read(bytes);
						File.WriteAllBytes(_outputFileName, bytes);
					}
				}
			}
			else
			{
				// bin -> ini

				if (string.IsNullOrEmpty(_outputFileName))
				{
					var dir = Path.GetDirectoryName(_inputFileName);
					var fn = Path.GetFileNameWithoutExtension(_inputFileName);
					_outputFileName = Path.Combine(dir, string.Concat(fn, "-export.ini"));
				}

				Log.Heading($"Converting {fmt.Format} Coalesced:");
				Log.Info(_inputFileName);
				Log.Heading($"To INI:");
				Log.Info(_outputFileName);

				var doc = null as CoalDocument;
				using (var fs = new FileStream(_inputFileName, FileMode.Open))
				{
					doc = converter.Load(fs);
				}

				using (var ms = new MemoryStream())
				{
					doc.Save(ms);

					if (!_whatIf)
					{
						var bytes = new byte[ms.Length];
						ms.Position = 0;
						ms.Read(bytes);
						File.WriteAllBytes(_outputFileName, bytes);
					}
				}
			}

			if (_whatIf) Log.Success("No changes made");
			else Log.Success("Success");
			return 0;
		}
	}
}
