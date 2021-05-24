using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CoalescedConvert
{
	enum IniReadResultType
	{
		EndOfStream,
		Section,
		Field
	}

	struct IniReadResult
	{
		public IniReadResultType Type { get; set; }
		public string Value1 { get; set; }
		public string Value2 { get; set; }
		public int LineNumber { get; set; }
	}

	class IniReader : IDisposable
	{
		private StreamReader _rdr;
		private int _lineNumber;

		public IniReader(Stream stream)
		{
			_rdr = new StreamReader(stream ?? throw new ArgumentNullException(nameof(stream)));
		}

		public void Dispose()
		{
			_rdr?.Dispose();
			_rdr = null;
		}

		public bool EndOfStream => _rdr.EndOfStream;

		private static readonly Regex _rxComment = new Regex(@"^\s*;");
		private static readonly Regex _rxSection = new Regex(@"^\[(.+)\]\s*$");

		public IniReadResult Read()
		{
			Match match;
			int index;

			while (!_rdr.EndOfStream)
			{
				var line = _rdr.ReadLine();
				if (line == null) break;
				_lineNumber++;
				if (string.IsNullOrWhiteSpace(line) || _rxComment.IsMatch(line)) continue;

				if ((match = _rxSection.Match(line)).Success)
				{
					var sectionName = match.Groups[1].Value;
					index = sectionName.IndexOf('|');
					if (index <= 0) throw new IniNoCurrentFileException($"No file name was included in the INI section on line {_lineNumber}.");
					var fileName = IniDecode(sectionName.Substring(0, index));
					sectionName = IniDecode(sectionName.Substring(index + 1));
					return new IniReadResult
					{
						Type = IniReadResultType.Section,
						Value1 = fileName,
						Value2 = sectionName,
						LineNumber = _lineNumber
					};
				}

				index = line.IndexOf('=');
				if (index <= 0) throw new IniInvalidKeyNameException($"No field name was included before a '=' on line {_lineNumber}.");

				var fieldName = IniDecode(line.Substring(0, index));
				var fieldValue = IniDecode(line.Substring(index + 1));
				return new IniReadResult
				{
					Type = IniReadResultType.Field,
					Value1 = fieldName,
					Value2 = fieldValue,
					LineNumber = _lineNumber
				};
			}

			return new IniReadResult
			{
				Type = IniReadResultType.EndOfStream,
				LineNumber = _lineNumber
			};
		}

		public static string IniDecode(string str)
		{
			if (string.IsNullOrEmpty(str)) return string.Empty;

			var sb = new StringBuilder();

			for (int pos = 0, len = str.Length; pos < len; pos++)
			{
				var ch = str[pos];
				if (ch == '\\' && pos + 1 < len)
				{
					switch (str[pos + 1])
					{
						case '\\':
							sb.Append('\\');
							pos++;
							break;
						case 'r':
							sb.Append('\r');
							pos++;
							break;
						case 'n':
							sb.Append('\n');
							pos++;
							break;
						case 'p':
							sb.Append('|');
							pos++;
							break;
						case 'e':
							sb.Append('=');
							pos++;
							break;
						default:
							sb.Append('\\');
							break;
					}
				}
				else
				{
					sb.Append(ch);
				}
			}

			return sb.ToString();
		}
	}
}
