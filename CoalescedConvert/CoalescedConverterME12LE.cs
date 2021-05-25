using System;
using System.Collections.Generic;
using System.IO;

namespace CoalescedConvert
{
	class CoalescedConverterME12LE
	{
		private CoalescedFormat _format;
		private bool _whatIf;

		public CoalescedConverterME12LE(CoalescedFormat format, bool whatIf)
		{
			_format = format;
			_whatIf = whatIf;
		}

		public void Decode(string binFileName, string iniFileName)
		{
			using (var fs = new FileStream(binFileName, FileMode.Open))
			using (var bin = new CoalescedFileStream(fs, _format))
			using (var iniMS = new MemoryStream())
			using (var ini = new IniWriter(iniMS, _format))
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
							ini.WriteSection(fileName, sectionName);

							var numItems = bin.ReadInt();

							for (int i = 0; i < numItems; i++)
							{
								var str = bin.ReadString();
								var str2 = bin.ReadString();
								ini.WriteField(str, str2);
							}
						}
					}
					else
					{
						ini.WriteSection(fileName, null);	// So the file still gets created, even though there's nothing in it.
					}
				}

				ini.Flush();

				if (!_whatIf)
				{
					var iniContent = new byte[iniMS.Length];
					iniMS.Seek(0, SeekOrigin.Begin);
					iniMS.Read(iniContent);
					File.WriteAllBytes(iniFileName, iniContent);
				}
			}
		}

		public void Encode(string iniFileName, string binFileName)
		{
			var doc = new EncDoc();
			var currentSection = null as EncSection;
			var currentFile = null as EncFile;

			using (var fs = new FileStream(iniFileName, FileMode.Open))
			using (var ini = new IniReader(fs))
			{
				while (!ini.EndOfStream)
				{
					var read = ini.Read();
					if (read.Type == IniReadResultType.Section)
					{
						if (currentFile == null || read.Value1 != currentFile.fileName)
						{
							doc.files.Add(currentFile = new EncFile { fileName = read.Value1 });
						}

						if (read.Value2.Length > 0)
						{
							currentFile.sections.Add(currentSection = new EncSection { name = read.Value2 });
						}
						else
						{
							currentSection = null;
						}
					}
					else if (read.Type == IniReadResultType.Field)
					{
						if (currentSection == null) throw new IniNoCurrentSectionException(read.LineNumber);
						currentSection.fields.Add(new EncField
						{
							name = read.Value1,
							value = read.Value2
						});
					}
					else break;
				}
			}

			using (var ms = new MemoryStream())
			using (var bin = new CoalescedFileStream(ms, _format))
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

				bin.Flush();

				if (!_whatIf)
				{
					var content = new byte[ms.Length];
					ms.Seek(0, SeekOrigin.Begin);
					ms.Read(content);
					File.WriteAllBytes(binFileName, content);
				}
			}
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
