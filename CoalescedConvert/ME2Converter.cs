using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CoalescedConvert
{
	class ME2Converter : CoalescedConverter
	{
		private Encoding _encoding = Encoding.GetEncoding(1252);

		public ME2Converter(bool whatIf)
			: base(whatIf)
		{
		}

		public override void Decode(string binFileName, string iniFileName)
		{
			var doc = new ME2Doc();

			using (var fs = new FileStream(binFileName, FileMode.Open))
			using (var bin = new CoalescedFileStream(fs, CoalescedFormat.MassEffect2))
			{
				var signature = bin.ReadInt();

				while (!bin.EndOfStream)
				{
					var fileName = bin.ReadString();
					Log.Debug("File name: {0}", fileName);

					var fileContent = bin.ReadString();
					Log.Debug("File content: {0} bytes", fileContent.Length);

					var file = new ME2File(fileName);
					doc.Files.Add(file);
					var section = null as ME2Section;
					var numSections = 0;
					var numFields = 0;

					using (var ms = new MemoryStream(_encoding.GetBytes(fileContent)))
					using (var ini = new IniReader(ms, hasEmbeddedFileNames: false, unescapeStrings: false))
					{
						while (!ini.EndOfStream)
						{
							var read = ini.Read();
							if (read.Type == IniReadResultType.EndOfStream) break;
							if (read.Type == IniReadResultType.Section)
							{
								file.Sections.Add(section = new ME2Section(read.Value1));
								numSections++;
							}
							else if (read.Type == IniReadResultType.Field)
							{
								if (section == null) throw new IniNoCurrentSectionException(read.LineNumber);
								section.Fields.Add(new ME2Field(read.Value1, read.Value2));
								numFields++;
							}
						}
					}

					Log.Debug("Num sections: {0}", numSections);
					Log.Debug("Num fields: {0}", numFields);
				}
			}

			using (var ms = new MemoryStream())
			using (var ini = new IniWriter(ms, CoalescedFormat.MassEffect2))
			{
				foreach (var file in doc.Files)
				{
					foreach (var section in file.Sections)
					{
						ini.WriteSection(file.FileName, section.Name);

						foreach (var field in section.Fields)
						{
							ini.WriteField(field.Name, field.Value);
						}
					}
				}

				ini.Flush();

				if (!WhatIf)
				{
					var iniContent = new byte[ms.Length];
					ms.Seek(0, SeekOrigin.Begin);
					ms.Read(iniContent);
					File.WriteAllBytes(iniFileName, iniContent);
				}
			}
		}

		public override void Encode(string iniFileName, string binFileName)
		{
			var doc = new ME2Doc();

			using (var fs = new FileStream(iniFileName, FileMode.Open))
			using (var ini = new IniReader(fs, hasEmbeddedFileNames: true))
			{
				var file = null as ME2File;
				var section = null as ME2Section;

				while (!ini.EndOfStream)
				{
					var read = ini.Read();
					if (read.Type == IniReadResultType.EndOfStream) break;
					if (read.Type == IniReadResultType.Section)
					{
						if (file == null || file.FileName != read.Value1)
						{
							doc.Files.Add(file = new ME2File(read.Value1));
							section = null;
						}
						if (section == null || section.Name != read.Value2)
						{
							file.Sections.Add(section = new ME2Section(read.Value2));
						}
					}
					else if (read.Type == IniReadResultType.Field)
					{
						if (section == null) throw new IniNoCurrentSectionException(read.LineNumber);
						section.Fields.Add(new ME2Field(read.Value1, read.Value2));
					}
				}
			}

			using (var ms = new MemoryStream())
			using (var bin = new CoalescedFileStream(ms, CoalescedFormat.MassEffect2))
			{
				bin.WriteInt(CoalescedFormatDetector.ME2Signature);

				foreach (var file in doc.Files)
				{
					bin.WriteString(file.FileName);

					using (var sectionMS = new MemoryStream())
					using (var ini = new IniWriter(sectionMS, format: null,
						lineEndSequence: "\n",
						addBlankLinesBetweenSections: false,
						escapeStrings: false))
					{
						foreach (var section in file.Sections)
						{
							ini.WriteSection(section.Name);

							foreach (var field in section.Fields)
							{
								ini.WriteField(field.Name, field.Value);
							}
						}

						ini.Flush();

						var bytes = new byte[sectionMS.Length];
						sectionMS.Seek(0, SeekOrigin.Begin);
						sectionMS.Read(bytes);

						bin.WriteInt(bytes.Length + 1);
						bin.WriteBytes(bytes);
						bin.WriteByte(0);
					}
				}

				bin.Flush();

				if (!WhatIf)
				{
					var bytes = new byte[ms.Length];
					ms.Seek(0, SeekOrigin.Begin);
					ms.Read(bytes);
					File.WriteAllBytes(binFileName, bytes);
				}
			}
		}

		private class ME2Doc
		{
			public List<ME2File> Files { get; private set; } = new List<ME2File>();
		}

		private class ME2File
		{
			public string FileName { get; private set; }
			public List<ME2Section> Sections { get; private set; } = new List<ME2Section>();

			public ME2File(string fileName)
			{
				FileName = fileName;
			}
		}

		private class ME2Section
		{
			public string Name { get; private set; }
			public List<ME2Field> Fields { get; private set; } = new List<ME2Field>();

			public ME2Section(string name)
			{
				Name = name;
			}
		}

		private class ME2Field
		{
			public string Name { get; private set; }
			public string Value { get; private set; }

			public ME2Field(string name, string value)
			{
				Name = name;
				Value = value;
			}
		}
	}
}
