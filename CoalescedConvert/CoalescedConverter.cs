using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CoalescedConvert
{
	class CoalescedConverter : IDisposable
	{
		private FileStream _bin;
		private long _pos;
		private bool _verbose;

		public void Dispose()
		{
			if (_bin != null)
			{
				_bin.Dispose();
				_bin = null;
			}
		}

		public void Decode(string binFileName, string iniFileName, bool verbose)
		{
			using (_bin = new FileStream(binFileName, FileMode.Open))
			using (var ini = new StreamWriter(iniFileName))
			{
				_verbose = verbose;

				var fileCount = ReadInt();
				Report("File Count", fileCount);

				for (int fileIndex = 0; fileIndex < fileCount; fileIndex++)
				{
					var fileName = ReadString();
					Report("File Name", fileName);

					var sectionCount = ReadInt();
					Report("Section Count", sectionCount);

					if (sectionCount > 0)
					{
						for (int sectionIndex = 0; sectionIndex < sectionCount; sectionIndex++)
						{
							var sectionName = ReadString();
							Report("Section Name", sectionName);
							ini.WriteLine($"[{fileName}|{sectionName}]");

							var numItems = ReadInt();
							Report("Section Field Count", numItems);

							for (int i = 0; i < numItems; i++)
							{
								var fieldPos = _pos;
								var str = ReadString();
								var str2 = ReadString();
								Report(str, str2);
								ini.WriteLine($"{str}={IniEncode(str2)}");
							}
						}
					}
					else
					{
						ini.WriteLine($"[{fileName}|]");	// So the file still gets created, even though there's nothing in it.
					}
				}
			}
		}

		private static readonly Regex _rxComment = new Regex(@"^\s*;");
		private static readonly Regex _rxSection = new Regex(@"^\[([^][]+)\]");

		public void Encode(string iniFileName, string binFileName, bool verbose)
		{
			var doc = new EncDoc();
			var currentSection = null as EncSection;
			var currentFile = null as EncFile;
			var lineNumber = 0;
			int index;

			using (var ini = new StreamReader(iniFileName))
			{
				while (!ini.EndOfStream)
				{
					var line = ini.ReadLine();
					if (line == null) break;
					lineNumber++;
					if (string.IsNullOrWhiteSpace(line) || _rxComment.IsMatch(line)) continue;

					Match match;

					if ((match = _rxSection.Match(line)).Success)
					{
						var sectionName = match.Groups[1].Value;
						index = sectionName.IndexOf('|');
						if (index <= 0) throw new IniNoCurrentFileException(lineNumber);
						var fileName = sectionName.Substring(0, index);
						sectionName = sectionName.Substring(index + 1);

						if (currentFile == null || fileName != currentFile.fileName)
						{
							doc.files.Add(currentFile = new EncFile { fileName = fileName });
						}

						if (sectionName.Length > 0)
						{
							currentFile.sections.Add(currentSection = new EncSection { name = sectionName });
						}
						else
						{
							currentSection = null;
						}
						continue;
					}

					index = line.IndexOf('=');
					if (index <= 0) throw new IniInvalidKeyNameException(lineNumber);

					var fieldName = line.Substring(0, index);
					var fieldValue = IniDecode(line.Substring(index + 1));

					if (currentSection == null) throw new IniNoCurrentSectionException(lineNumber);
					currentSection.fields.Add(new EncField { name = fieldName, value = fieldValue });
				}
			}

			using (_bin = new FileStream(binFileName, FileMode.Create))
			{
				WriteInt(doc.files.Count);
				foreach (var file in doc.files)
				{
					WriteString(file.fileName);
					WriteInt(file.sections.Count);
					foreach (var section in file.sections)
					{
						WriteString(section.name);
						WriteInt(section.fields.Count);
						foreach (var field in section.fields)
						{
							WriteString(field.name);
							WriteString(field.value);
						}
					}
				}
			}
		}

		private int ReadInt()
		{
			_pos = _bin.Position;
			return _bin.ReadByte() | (_bin.ReadByte() << 8) | (_bin.ReadByte() << 16) | (_bin.ReadByte() << 24);
		}

		private void WriteInt(int value)
		{
			_bin.WriteByte((byte)(value & 0xff));
			_bin.WriteByte((byte)((value >> 8) & 0xff));
			_bin.WriteByte((byte)((value >> 16) & 0xff));
			_bin.WriteByte((byte)((value >> 24) & 0xff));
		}

		private string ReadString()
		{
			_pos = _bin.Position;

			var len = ReadInt();
			if (len <= 0)
			{
				len = -len;
				var sb = new StringBuilder();
				for (int i = 0; i < len; i++)
				{
					char ch = (char)(_bin.ReadByte() | (_bin.ReadByte() << 8));
					if (ch == 0) break;
					sb.Append(ch);
				}
				return sb.ToString().TrimEnd('\0');
			}
			else
			{
				throw new CoelescedReadException($"String prefix [{len}] is greater than zero at position 0x{_pos:X8}");
			}
		}

		private void WriteString(string str)
		{
			if (str.Length == 0)
			{
				WriteInt(0);
			}
			else
			{
				WriteInt(-(str.Length + 1));
				foreach (var ch in str)
				{
					_bin.WriteByte((byte)(ch & 0xff));
					_bin.WriteByte((byte)((ch >> 8) & 0xff));
				}
				_bin.WriteByte(0);
				_bin.WriteByte(0);
			}
		}

		private void Report(string label, object value, long pos = -1)
		{
			if (!_verbose) return;

			if (pos < 0) pos = _pos;
			Console.WriteLine($"[{pos:X8}] {label}: {value}");
		}

		private string IniEncode(string str)
		{
			var sb = new StringBuilder();

			foreach (var ch in str)
			{
				switch (ch)
				{
					case '\\':
						sb.Append("\\\\");
						break;
					case '\t':
						sb.Append("\\t");
						break;
					case '\r':
						sb.Append("\\r");
						break;
					case '\n':
						sb.Append("\\n");
						break;
					default:
						if (ch < ' ' || ch > '~') sb.Append($"\\x{(int)ch:X4}");
						else sb.Append(ch);
						break;
				}
			}

			return sb.ToString();
		}

		private string IniDecode(string str)
		{
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
						case 't':
							sb.Append('\t');
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
						case 'x':
							if (pos + 5 < len && uint.TryParse(str.Substring(pos + 2, 4), System.Globalization.NumberStyles.HexNumber, provider: null, out var code))
							{
								sb.Append((char)code);
								pos += 5;
							}
							else
							{
								sb.Append('\\');
							}
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

		private class EncDoc
		{
			public List<EncFile> files = new List<EncFile>();
		}

		private class EncFile
		{
			public string fileName;
			public List<EncSection> sections = new List<EncSection>();
		}

		private class EncSection
		{
			public string name;
			public List<EncField> fields = new List<EncField>();
		}

		private class EncField
		{
			public string name;
			public string value;
		}
	}

	class CoelescedReadException : Exception
	{
		public CoelescedReadException(string message) : base(message) { }
	}

	class IniNoCurrentFileException : Exception
	{
		public IniNoCurrentFileException(int lineNumber) : base($"A section was declared on line {lineNumber} but no file name is present.") { }
	}

	class IniInvalidKeyNameException : Exception
	{
		public IniInvalidKeyNameException(int lineNumber) : base($"The key name on line {lineNumber} is invalid.") { }
	}

	class IniNoCurrentSectionException : Exception
	{
		public IniNoCurrentSectionException(int lineNumber) : base($"A value was declared on line {lineNumber} but no current section is set.") { }
	}
}
