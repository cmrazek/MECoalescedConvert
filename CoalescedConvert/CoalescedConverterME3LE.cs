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

			using (var ini = new StreamWriter(iniFileName))
			{
				WriteIni(ini);
			}
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

				//ReportField($"String {i}", str);

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
			_doc.files = new ME3File[numFiles];

			for (int fileIndex = 0; fileIndex < numFiles; fileIndex++)
			{
				var fileName = _strings[_bin.ReadUShort()];
				_bin.ReadUInt();
				_doc.files[fileIndex] = new ME3File { fileName = fileName };
			}

			for (int fileIndex = 0; fileIndex < numFiles; fileIndex++)
			{
				var file = _doc.files[fileIndex];

				var numSections = _bin.ReadUShort();
				file.sections = new ME3Section[numSections];

				for (int sectionIndex = 0; sectionIndex < numSections; sectionIndex++)
				{
					var sectionName = _strings[_bin.ReadUShort()];
					_bin.ReadUInt();
					file.sections[sectionIndex] = new ME3Section { name = sectionName };
				}

				for (int sectionIndex = 0; sectionIndex < numSections; sectionIndex++)
				{
					var section = file.sections[sectionIndex];
					var numFields = _bin.ReadUShort();
					section.fields = new ME3Field[numFields];

					for (int fieldIndex = 0; fieldIndex < numFields; fieldIndex++)
					{
						var fieldName = _strings[_bin.ReadUShort()];
						_bin.ReadUInt();
						section.fields[fieldIndex] = new ME3Field { name = fieldName };
					}

					for (int fieldIndex = 0; fieldIndex < numFields; fieldIndex++)
					{
						var field = section.fields[fieldIndex];
						var numValues = _bin.ReadUShort();
						field.offsets = new uint[numValues];

						for (int valueIndex = 0; valueIndex < numValues; valueIndex++)
						{
							field.offsets[valueIndex] = _bin.ReadUInt();
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

			foreach (var file in _doc.files)
			{
				foreach (var section in file.sections)
				{
					foreach (var field in section.fields)
					{
						values.Clear();
						foreach (var offset in field.offsets)
						{
							var str = decomp.Decompress(_compressedData, (int)(offset & 0xFFFFFFF));
							values.Add(str);
						}
						field.values = values.ToArray();
					}
				}
			}
		}

		private void WriteIni(StreamWriter ini)
		{
			foreach (var file in _doc.files)
			{
				if (!file.sections.Any())
				{
					ini.WriteLine($"[{file.fileName}|]");
					continue;
				}
				foreach (var section in file.sections)
				{
					ini.WriteLine($"[{file.fileName}|{section.name}]");
					foreach (var field in section.fields)
					{
						if (!field.values.Any())
						{
							ini.WriteLine($"{field.name}=");
							continue;
						}

						foreach (var value in field.values)
						{
							ini.WriteLine($"{field.name}={CoalescedConverterME12LE.IniEncode(value)}");
						}
					}
				}
			}
		}
	}

	class ME3Doc
	{
		public ME3File[] files;
	}

	class ME3File
	{
		public string fileName;
		public ME3Section[] sections;
	}

	class ME3Section
	{
		public string name;
		public ME3Field[] fields;
	}

	class ME3Field
	{
		public string name;
		public uint[] offsets;
		public string[] values;
	}
}
