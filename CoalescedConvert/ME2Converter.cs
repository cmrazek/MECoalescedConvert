using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CoalescedConvert
{
	class ME2Converter : CoalConverter
	{
		private Encoding _encoding = Encoding.GetEncoding(1252);

		public override CoalDocument Load(Stream stream)
		{
			var doc = new CoalDocument(CoalFormat.MassEffect2);

			using (var bin = new CoalFileStream(stream, CoalFormat.MassEffect2))
			{
				var signature = bin.ReadInt();

				while (!bin.EndOfStream)
				{
					var fileName = bin.ReadString();
					Log.Debug("File name: {0}", fileName);

					var fileContent = bin.ReadString();
					Log.Debug("File content: {0} bytes", fileContent.Length);

					var file = new CoalFile(fileName);
					doc.Files.Add(file);
					var section = null as CoalSection;
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
								file.Sections.Add(section = new CoalSection(read.Value1));
								numSections++;
							}
							else if (read.Type == IniReadResultType.Field)
							{
								if (section == null) throw new IniNoCurrentSectionException(read.LineNumber);
								section.Fields.Add(new CoalField(read.Value1, read.Value2));
								numFields++;
							}
						}
					}

					Log.Debug("Num sections: {0}", numSections);
					Log.Debug("Num fields: {0}", numFields);
				}
			}

			return doc;
		}

		public override void Save(CoalDocument doc, Stream stream, bool leaveStreamOpen)
		{
			using (var bin = new CoalFileStream(stream, CoalFormat.MassEffect2, leaveOpen: leaveStreamOpen))
			{
				bin.WriteInt(CoalFormatDetector.ME2Signature);

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
								foreach (var value in field.Values)
								{
									ini.WriteField(field.Name, value);
								}
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
