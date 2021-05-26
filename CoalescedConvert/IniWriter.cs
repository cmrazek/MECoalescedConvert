using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CoalescedConvert
{
	class IniWriter : IDisposable
	{
		private StreamWriter _writer;
		private bool _firstSection;
		private bool _addBlankLinesBetweenSections;
		private bool _escapeStrings;

		public const char EscapeChar = '^';

		public IniWriter(Stream stream, CoalFormat? format, Encoding encoding = null, bool leaveOpen = false, string lineEndSequence = null, bool addBlankLinesBetweenSections = true, bool escapeStrings = true)
		{
			if (stream == null) throw new ArgumentNullException(nameof(stream));

			_writer = new StreamWriter(stream, encoding: encoding, leaveOpen: leaveOpen);

			if (lineEndSequence != null) _writer.NewLine = lineEndSequence;
			_addBlankLinesBetweenSections = addBlankLinesBetweenSections;
			_escapeStrings = escapeStrings;

			if (format.HasValue)
			{
				_writer.WriteLine(string.Concat(CoalFormatDetector.IniFirstLine, " ", format.ToString()));
			}

			_firstSection = true;
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
			if (_firstSection)
			{
				_firstSection = false;
			}
			else
			{
				if (_addBlankLinesBetweenSections) _writer.WriteLine();
			}

			_writer.Write('[');
			if (_escapeStrings) _writer.Write(IniEncode(fileName, escapePipe: true));
			else _writer.Write(fileName);
			_writer.Write('|');
			if (_escapeStrings) _writer.Write(IniEncode(sectionName));
			else _writer.Write(sectionName);
			_writer.WriteLine(']');
		}

		public void WriteSection(string sectionName)
		{
			if (_firstSection)
			{
				_firstSection = false;
			}
			else
			{
				if (_addBlankLinesBetweenSections) _writer.WriteLine();
			}

			_writer.Write('[');
			if (_escapeStrings) _writer.Write(IniEncode(sectionName));
			else _writer.Write(sectionName);
			_writer.WriteLine(']');
		}

		public void WriteField(string name, string value)
		{
			if (_escapeStrings) _writer.Write(IniEncode(name, escapeEquals: true));
			else _writer.Write(name);
			_writer.Write('=');
			if (_escapeStrings) _writer.WriteLine(IniEncode(value));
			else _writer.WriteLine(value);
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
					case EscapeChar:
						sb.Append(EscapeChar);
						sb.Append(EscapeChar);
						break;
					case '\r':
						sb.Append(EscapeChar);
						sb.Append('r');
						break;
					case '\n':
						sb.Append(EscapeChar);
						sb.Append('n');
						break;
					case '|':
						if (escapePipe)
						{
							sb.Append(EscapeChar);
							sb.Append('p');
						}
						else sb.Append(ch);
						break;
					case '=':
						if (escapeEquals)
						{
							sb.Append(EscapeChar);
							sb.Append('e');
						}
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
