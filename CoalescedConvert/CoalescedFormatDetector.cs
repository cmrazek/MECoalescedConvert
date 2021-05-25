using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoalescedConvert
{
	public enum CoalescedFormat
	{
		MassEffect2,
		MassEffect3,
		MassEffect12LE
	}

	public class CoalescedFormatDetector
	{
		public const int ME2Signature = 0x1e;
		public const int ME3Signature = 0x666d726d; // Appears as 'mrmf' in the file.
		public const int ME3Version = 1;
		public const string IniFirstLine = "; CoalescedConvert Export";

		public static string GetExtension(CoalescedFormat format)
		{
			switch (format)
			{
				case CoalescedFormat.MassEffect2:
					return ".ini";
				default:
					return ".bin";
			}
		}

		public static FormatDetectionResult Detect(string fileName)
		{
			if (!File.Exists(fileName)) throw new FileNotFoundException();

			using (var fs = new FileStream(fileName, FileMode.Open))
			{
				byte[] buf = new byte[12];
				fs.Read(buf, 0, 12);

				// ME2 starts with 0x1e then has the length of the first file name.
				if (BytesToInt(buf, 0) == ME2Signature && BytesToInt(buf, 4) > 0)
				{
					return new FormatDetectionResult
					{
						Format = CoalescedFormat.MassEffect2,
						IsExport = false
					};
				}

				// ME3 starts with 'mrmf' then has a version number.
				if (buf[0] == 'm' && buf[1] == 'r' && buf[2] == 'm' && buf[3] == 'f' && BytesToInt(buf, 4) == ME3Version)
				{
					return new FormatDetectionResult
					{
						Format = CoalescedFormat.MassEffect3,
						IsExport = false
					};
				}

				// ME12LE starts with number of files. Make sure this is in a reasonable range.
				int num2 = (buf[0]) | (buf[1] << 8) | (buf[2] << 16) | (buf[3] << 24);
				if (num2 > 0 && num2 < 256)
				{
					// Next field is the string prefix for the first file name; this is a negative number.
					int num3 = (buf[4]) | (buf[5] << 8) | (buf[6] << 16) | (buf[7] << 24);
					if (num3 < 0 && num3 > -260)	// 260 = Win32 MAX_PATH
					{
						return new FormatDetectionResult
						{
							Format = CoalescedFormat.MassEffect12LE,
							IsExport = false
						};
					}
					else if (num3 > 0 && num3 < 260)	// For ME2, this is a postive number.
					{
						return new FormatDetectionResult
						{
							Format = CoalescedFormat.MassEffect2,
							IsExport = false
						};
					}
				}

				// Check if this is a text INI file.
				fs.Seek(0, SeekOrigin.Begin);
				using (var rdr = new StreamReader(fs))
				{
					var line = rdr.ReadLine();
					if (line != null && line.StartsWith(IniFirstLine))
					{
						var formatString = line.Substring(IniFirstLine.Length).Trim();
						if (Enum.TryParse<CoalescedFormat>(formatString, ignoreCase: true, out var format))
						{
							return new FormatDetectionResult
							{
								Format = format,
								IsExport = true
							};
						}
					}
				}

				throw new UnknownCoalescedFormatException();
			}
		}

		public static int BytesToInt(byte[] bytes, int start) => bytes[start] | (bytes[start + 1] << 8) | (bytes[start + 2] << 16) | (bytes[start + 3] << 24);

		public struct FormatDetectionResult
		{
			public CoalescedFormat Format { get; set; }
			public bool IsExport { get; set; }
		}
	}
}
