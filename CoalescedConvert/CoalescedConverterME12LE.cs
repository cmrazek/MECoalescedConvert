using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CoalescedConvert
{
	class CoalescedConverterME12LE
	{
		public void Decode(string binFileName, string iniFileName)
		{
			using (var fs = new FileStream(binFileName, FileMode.Open))
			using (var bin = new CoalescedFileStream(fs, CoalescedFormat.MassEffect12LE))
			using (var ini = new StreamWriter(iniFileName))
			{
				var fileCount = bin.ReadInt();

				for (int fileIndex = 0; fileIndex < fileCount; fileIndex++)
				{
					var fileName = bin.ReadString();
					var sectionCount = bin.ReadInt();

					if (sectionCount > 0)
					{
						for (int sectionIndex = 0; sectionIndex < sectionCount; sectionIndex++)
						{
							var sectionName = bin.ReadString();
							ini.WriteLine($"[{fileName}|{sectionName}]");

							var numItems = bin.ReadInt();

							for (int i = 0; i < numItems; i++)
							{
								var str = bin.ReadString();
								var str2 = bin.ReadString();
								ini.WriteLine($"{str}={IniEncode(str2)}");
							}
						}
					}
					else
					{
						ini.WriteLine($"[{fileName}|]");    // So the file still gets created, even though there's nothing in it.
					}
				}
			}
		}

		private static readonly Regex _rxComment = new Regex(@"^\s*;");
		private static readonly Regex _rxSection = new Regex(@"^\[([^][]+)\]");

		public void Encode(string iniFileName, string binFileName)
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

			using (var fs = new FileStream(binFileName, FileMode.Create))
			using (var bin = new CoalescedFileStream(fs, CoalescedFormat.MassEffect12LE))
			{
				bin.WriteInt(doc.files.Count);
				foreach (var file in doc.files)
				{
					bin.WriteString(file.fileName);
					bin.WriteInt(file.sections.Count);
					foreach (var section in file.sections)
					{
						bin.WriteString(section.name);
						bin.WriteInt(section.fields.Count);
						foreach (var field in section.fields)
						{
							bin.WriteString(field.name);
							bin.WriteString(field.value);
						}
					}
				}
			}
		}

		public static string IniEncode(string str)
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

		public static string IniDecode(string str)
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
}
