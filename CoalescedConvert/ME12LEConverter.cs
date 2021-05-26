using System;
using System.Collections.Generic;
using System.IO;

// TODO: This is now broken - there are differences between factory and reassembled

namespace CoalescedConvert
{
	class ME12LEConverter : CoalConverter
	{
		public override CoalDocument Load(Stream stream)
		{
			var doc = new CoalDocument(CoalFormat.MassEffect12LE);

			using (var bin = new CoalFileStream(stream, CoalFormat.MassEffect12LE))
			{
				var fileCount = bin.ReadInt();
				Log.Debug("[{1:X8}] Num files: {0}", fileCount, bin.Position);

				for (int fileIndex = 0; fileIndex < fileCount; fileIndex++)
				{
					var fileName = bin.ReadString();
					Log.Debug("[{1:X8}] File name: {0}", fileName, bin.Position);
					var file  = new CoalFile(fileName);
					doc.Files.Add(file);

					var sectionCount = bin.ReadInt();
					Log.Debug("[{1:X8}] Section count: {0}", sectionCount, bin.Position);

					if (sectionCount > 0)
					{
						for (int sectionIndex = 0; sectionIndex < sectionCount; sectionIndex++)
						{
							var sectionName = bin.ReadString();
							//Log.Debug("[{1:X8}] Section name: {0}", sectionName, bin.Position);

							var section = new CoalSection(sectionName);
							file.Sections.Add(section);

							var numFields = bin.ReadInt();
							//Log.Debug("[{1:X8}] Num fields: {0}", numFields, bin.Position);

							for (int i = 0; i < numFields; i++)
							{
								var fieldName = bin.ReadString();
								//Log.Debug("[{1:X8}] Field name: {0}", fieldName, bin.Position);

								var fieldValue = bin.ReadString();
								section.Fields.Add(new CoalField(fieldName, fieldValue));
							}
						}
					}
					else
					{
						file.Sections.Add(new CoalSection(string.Empty));   // So the file still gets created, even though there's nothing in it.
					}
				}
			}

			return doc;
		}

		public override void Save(CoalDocument doc, Stream stream, bool leaveStreamOpen)
		{
			using (var bin = new CoalFileStream(stream, CoalFormat.MassEffect12LE, leaveOpen: leaveStreamOpen))
			{
				bin.WriteInt(doc.Files.Count);
				foreach (var file in doc.Files)
				{
					bin.WriteString(file.FileName);
					bin.WriteInt(file.Sections.Count);
					foreach (var section in file.Sections)
					{
						bin.WriteString(section.Name);
						bin.WriteInt(section.ValueCount);
						foreach (var field in section.Fields)
						{
							foreach (var value in field.Values)
							{
								bin.WriteString(field.Name);
								bin.WriteString(value);
							}
						}
					}
				}

				bin.Flush();
			}
		}
	}
}
