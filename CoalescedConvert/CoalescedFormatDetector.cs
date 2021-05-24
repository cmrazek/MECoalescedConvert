using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoalescedConvert
{
	enum CoalescedFormat
	{
		Unknown,
		MassEffect12LE,
		MassEffect3LE
	}

	class CoalescedFormatDetector
	{
		public const int ME3Signature = 0x666d726d;	// Appears as 'mrmf' in the file.

		public static CoalescedFormat Detect(string fileName)
		{
			if (!File.Exists(fileName)) return CoalescedFormat.Unknown;

			using (var fs = new FileStream(fileName, FileMode.Open))
			{
				byte[] buf = new byte[12];
				fs.Read(buf, 0, 12);

				// Easy check for ME3LE
				if (buf[0] == 'm' && buf[1] == 'r' && buf[2] == 'm' && buf[3] == 'f') return CoalescedFormat.MassEffect3LE;

				// For ME12LE, starts with number of files. Make sure this is in a reasonable range.
				int num2 = (buf[0]) | (buf[1] << 8) | (buf[2] << 16) | (buf[3] << 24);
				if (num2 > 0 && num2 < 256)
				{
					// Next field is the string prefix for the first file name. This is a negative number.
					int num3 = (buf[4]) | (buf[5] << 8) | (buf[6] << 16) | (buf[7] << 24);
					if (num3 < 0 && num3 > -260)	// 260 = Win32 MAX_PATH
					{
						return CoalescedFormat.MassEffect12LE;
					}
				}

				return CoalescedFormat.Unknown;
			}
		}
	}
}
