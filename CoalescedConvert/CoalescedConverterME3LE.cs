using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoalescedConvert
{
	class CoalescedConverterME3LE
	{
		private CoalescedFileStream _bin;
		private List<string> _strings;

		private ME3Doc _doc;
		private int[] _huffmanNodes;
		private uint _compressedDataLength;
		private byte[] _compressedData;

		public void Decode(string binFileName, string iniFileName)
		{
			using (var fs = new FileStream(binFileName, FileMode.Open))
			using (_bin = new CoalescedFileStream(fs, CoalescedFormat.MassEffect3LE))
			{
				ReadHeader();
				ReadStringTable();
				ReadHuffmanNodes();
				ReadTree();
				ReadCompressedData();
			}

			WriteIni(iniFileName);
		}

		private void ReadHeader()
		{
			var sig = _bin.ReadInt();
			if (sig != CoalescedFormatDetector.ME3Signature) throw new CoalescedReadException("File does not start with standard signature. This may be the incorrect format.");

			var version = _bin.ReadInt();
			if (version != 1) throw new CoalescedReadException("File version is not 1. This may be the incorrect format.");

			var num2 = _bin.ReadInt();
			var num3 = _bin.ReadInt();
			var stringSectionLength = _bin.ReadInt();
			var huffmanNodesSectionLength = _bin.ReadInt();
			var treeSectionLength = _bin.ReadInt();
			_compressedDataLength = _bin.ReadUInt();
		}

		private void ReadStringTable()
		{
			var stringTableLength = _bin.ReadInt();
			var numStrings = _bin.ReadUInt();

			var stringIndexLength = numStrings * 8;
			var stringIndex = new uint[numStrings * 2];
			var s = 0;
			for (int i = 0; i < numStrings; i++)
			{
				stringIndex[s++] = _bin.ReadUInt();
				stringIndex[s++] = _bin.ReadUInt();
			}

			var stringBufferLength = stringTableLength - numStrings * 8 - 8;
			var stringBuf = new byte[stringBufferLength];
			_bin.Read(stringBuf);

			_strings = new List<string>((int)numStrings);
			for (int i = 0; i < numStrings; i++)
			{
				var fileCrc = stringIndex[i * 2];

				var stringOffset = stringIndex[i * 2 + 1];
				stringOffset -= stringIndexLength;
				var stringLen = stringBuf[stringOffset] | (stringBuf[stringOffset + 1] << 8);
				stringOffset += 2;
				var str = Encoding.UTF8.GetString(stringBuf, (int)stringOffset, stringLen);

				var calcCrc = Crc32.Hash(str);
				if (fileCrc != calcCrc) throw new CoalescedReadException($"CRC of string in file {fileCrc:X8} does not match calculated {calcCrc:X8}.");

				_strings.Add(str);
			}
		}

		private void ReadHuffmanNodes()
		{
			var numHuffmanNodes = _bin.ReadUShort();

			_huffmanNodes = new int[numHuffmanNodes * 2];
			for (int i = 0, ii = _huffmanNodes.Length; i < ii; i++) _huffmanNodes[i] = _bin.ReadInt();
		}

		private void ReadTree()
		{
			_doc = new ME3Doc();

			var numFiles = _bin.ReadUShort();

			for (int fileIndex = 0; fileIndex < numFiles; fileIndex++)
			{
				var fileName = _strings[_bin.ReadUShort()];
				_bin.ReadUInt();
				_doc.Files.Add(new ME3File(fileName));
			}

			for (int fileIndex = 0; fileIndex < numFiles; fileIndex++)
			{
				var file = _doc.Files[fileIndex];
				var numSections = _bin.ReadUShort();

				for (int sectionIndex = 0; sectionIndex < numSections; sectionIndex++)
				{
					var sectionName = _strings[_bin.ReadUShort()];
					_bin.ReadUInt();
					file.Sections.Add(new ME3Section(sectionName));
				}

				for (int sectionIndex = 0; sectionIndex < numSections; sectionIndex++)
				{
					var section = file.Sections[sectionIndex];
					var numFields = _bin.ReadUShort();

					for (int fieldIndex = 0; fieldIndex < numFields; fieldIndex++)
					{
						var fieldName = _strings[_bin.ReadUShort()];
						_bin.ReadUInt();
						section.Fields.Add(new ME3Field(fieldName));
					}

					for (int fieldIndex = 0; fieldIndex < numFields; fieldIndex++)
					{
						var field = section.Fields[fieldIndex];
						var numValues = _bin.ReadUShort();

						for (int valueIndex = 0; valueIndex < numValues; valueIndex++)
						{
							field.Offsets.Add(_bin.ReadUInt());
						}
					}
				}
			}
		}

		private void ReadCompressedData()
		{
			var unk = _bin.ReadUInt();
			_compressedData = _bin.ReadBytes((int)_compressedDataLength);

			var values = new List<string>();
			var decomp = new HuffmanDecompressor(_huffmanNodes);

			foreach (var file in _doc.Files)
			{
				foreach (var section in file.Sections)
				{
					foreach (var field in section.Fields)
					{
						foreach (var offset in field.Offsets)
						{
							var str = decomp.Decompress(_compressedData, (int)(offset & 0xFFFFFFF));
							field.Values.Add(str);
						}
					}
				}
			}
		}

		private void WriteIni(string fileName)
		{
			using (var ms = new MemoryStream())
			using (var ini = new IniWriter(ms))
			{
				foreach (var file in _doc.Files)
				{
					if (!file.Sections.Any())
					{
						ini.WriteSection(file.FileName, null);
						continue;
					}
					foreach (var section in file.Sections)
					{
						ini.WriteSection(file.FileName, section.Name);
						foreach (var field in section.Fields)
						{
							if (!field.Values.Any())
							{
								ini.WriteField(field.Name, string.Empty);
								continue;
							}

							ini.WriteField(field.Name, field.Values);
						}
					}
				}

				ini.Flush();
				var iniContent = new byte[ms.Length];
				ms.Seek(0, SeekOrigin.Begin);
				ms.Read(iniContent);
				File.WriteAllBytes(fileName, iniContent);
			}
		}

		public void Encode(string iniFileName, string binFileName)
		{
			var doc = new ME3Doc();

			using (var fs = new FileStream(iniFileName, FileMode.Open))
			using (var ini = new IniReader(fs))
			{
				while (!ini.EndOfStream)
				{
					var read = ini.Read();
					if (read.Type == IniReadResultType.Section)
					{
						if (doc.Files.Count == 0 || doc.Files.Last().FileName != read.Value1)
						{
							doc.Files.Add(new ME3File(read.Value1));
						}

						var file = doc.Files.Last();
						if (file.Sections.Count == 0 || file.Sections.Last().Name != read.Value2)
						{
							file.Sections.Add(new ME3Section(read.Value2));
						}
					}
					else if (read.Type == IniReadResultType.Field)
					{
						var section = doc.Files.LastOrDefault()?.Sections.LastOrDefault();
						if (section == null) throw new IniNoCurrentSectionException(read.LineNumber);

						var field = section.Fields.LastOrDefault();
						if (field?.Name == read.Value1)
						{
							field.Values.Add(read.Value2);
						}
						else
						{
							section.Fields.Add(new ME3Field(read.Value1, read.Value2));
						}
					}
					else break;
				}
			}
		}
	}

	class ME3Doc
	{
		public List<ME3File> Files { get; private set; } = new List<ME3File>();
	}

	class ME3File
	{
		public string FileName;
		public List<ME3Section> Sections { get; private set; } = new List<ME3Section>();

		public ME3File(string fileName)
		{
			FileName = fileName;
		}
	}

	class ME3Section
	{
		public string Name;
		public List<ME3Field> Fields { get; private set; } = new List<ME3Field>();

		public ME3Section(string name)
		{
			Name = name;
		}
	}

	class ME3Field
	{
		public string Name { get; private set; }
		public List<uint> Offsets { get; private set; } = new List<uint>();
		public List<string> Values { get; private set; } = new List<string>();

		public ME3Field(string name)
		{
			Name = name;
		}

		public ME3Field(string name, string value)
		{
			Name = name;
			Values.Add(value);
		}
	}
}
