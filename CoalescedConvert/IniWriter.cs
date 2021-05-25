using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CoalescedConvert
{
	class IniWriter : IDisposable
	{
		private StreamWriter _writer;
		private bool _fieldsWritten;

		public IniWriter(Stream stream, CoalescedFormat format)
		{
			_writer = new StreamWriter(stream ?? throw new ArgumentNullException(nameof(stream)));
			_writer.WriteLine(string.Concat(CoalescedFormatDetector.IniFirstLine, " ", format.ToString()));
		}

		public void Dispose()
		{
			_writer?.Dispose();
			_writer = null;
		}

		public void Flush()
		{
			_writer.Flush();
		}

		public void WriteSection(string fileName, string sectionName)
		{
			if (_fieldsWritten)
			{
				_writer.WriteLine();
				_fieldsWritten = false;
			}

			_writer.Write('[');
			_writer.Write(IniEncode(fileName, escapePipe: true));
			_writer.Write('|');
			_writer.Write(IniEncode(sectionName));
			_writer.WriteLine(']');
		}

		public void WriteField(string name, string value)
		{
			_writer.Write(IniEncode(name, escapeEquals: true));
			_writer.Write('=');
			_writer.WriteLine(IniEncode(value));

			_fieldsWritten = true;
		}

		public void WriteField(string name, IEnumerable<string> values)
		{
			foreach (var value in values)
			{
				WriteField(name, value);
			}
		}

		public static string IniEncode(string str, bool escapePipe = false, bool escapeEquals = false)
		{
			if (string.IsNullOrEmpty(str)) return string.Empty;

			var sb = new StringBuilder();

			foreach (var ch in str)
			{
				switch (ch)
				{
					case '\\':
						sb.Append("\\\\");
						break;
					case '\r':
						sb.Append("\\r");
						break;
					case '\n':
						sb.Append("\\n");
						break;
					case '|':
						if (escapePipe) sb.Append("\\p");
						else sb.Append(ch);
						break;
					case '=':
						if (escapeEquals) sb.Append("\\e");
						else sb.Append(ch);
						break;
					default:
						sb.Append(ch);
						break;
				}
			}

			return sb.ToString();
		}
	}
}
