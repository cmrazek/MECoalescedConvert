using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CoalescedConvert
{
	public class CoalDocument
	{
		public CoalFormat Format { get; private set; }
		public List<CoalFile> Files { get; private set; } = new List<CoalFile>();

		public CoalDocument(CoalFormat format)
		{
			Format = format;
		}

		public void Save(Stream stream)
		{
			using (var ini = new IniWriter(stream, Format, leaveOpen: true))
			{
				foreach (var file in Files)
				{
					foreach (var section in file.Sections)
					{
						ini.WriteSection(file.FileName, section.Name);

						foreach (var field in section.Fields)
						{
							foreach (var value in field.Values)
							{
								ini.WriteField(field.Name, value);
							}
						}
					}
				}

				ini.Flush();
			}
		}

		public static CoalDocument Load(Stream stream)
		{
			using (var ini = new IniReader(stream, hasEmbeddedFileNames: true, getFormatFromHeader: true))
			{
				var doc = new CoalDocument(ini.Format);
				var file = null as CoalFile;
				var section = null as CoalSection;
				var field = null as CoalField;

				while (!ini.EndOfStream)
				{
					var read = ini.Read();
					if (read.Type == IniReadResultType.EndOfStream) break;
					if (read.Type == IniReadResultType.Section)
					{
						if (file == null || file.FileName != read.Value1)
						{
							doc.Files.Add(file = new CoalFile(read.Value1));
							section = null;
							field = null;
						}
						if (!string.IsNullOrEmpty(read.Value2))
						{
							if (section == null || section.Name != read.Value2)
							{
								file.Sections.Add(section = new CoalSection(read.Value2));
								field = null;
							}
						}
					}
					else if (read.Type == IniReadResultType.Field)
					{
						if (section == null) throw new IniNoCurrentSectionException(read.LineNumber);

						if (field == null || field.Name != read.Value1)
						{
							section.Fields.Add(field = new CoalField(read.Value1));
						}
						field.Values.Add(read.Value2);
					}
				}

				return doc;
			}
		}
	}

	public class CoalFile
	{
		public string FileName { get; private set; }
		public List<CoalSection> Sections { get; private set; } = new List<CoalSection>();

		public CoalFile(string fileName)
		{
			FileName = fileName ?? throw new ArgumentNullException(nameof(fileName));
		}
	}

	public class CoalSection
	{
		public string Name { get; private set; }
		public List<CoalField> Fields { get; private set; } = new List<CoalField>();

		public CoalSection(string name)
		{
			Name = name ?? throw new ArgumentNullException(nameof(name));
		}

		public int ValueCount => Fields.Sum(x => x.Values.Count);
	}

	public class CoalField
	{
		public string Name { get; private set; }
		public List<string> Values { get; private set; } = new List<string>();

		public CoalField(string name)
		{
			Name = name ?? throw new ArgumentNullException(nameof(name));
		}

		public CoalField(string name, string singleValue)
		{
			Name = name ?? throw new ArgumentNullException(nameof(name));
			Values.Add(singleValue);
		}
	}
}
