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
		private uint _compressedDataLength;

		private const uint Type_String = 4;

		public void Decode(string binFileName, string iniFileName)
		{
			using (var fs = new FileStream(binFileName, FileMode.Open))
			using (_bin = new CoalescedFileStream(fs, CoalescedFormat.MassEffect3LE))
			{
				ReadHeader();
				ReadStringTable();
				var huffmanNodes = ReadHuffmanNodes();
				ReadTree();
				ReadCompressedData(huffmanNodes);
			}

			WriteIni(iniFileName);
		}

		public void Encode(string iniFileName, string binFileName)
		{
			var doc = ReadIniToDocument(iniFileName);
			var compressor = new HuffmanCompressor();

			_strings = new List<string>();
			_strings.Add(string.Empty);

			// Create the tree buffer
			var treeBuf = WriteTree(doc, compressor);
			var stringTableBuf = WriteStringTable();

			// File Header
			var buf = new BinaryBuffer();
			WriteHeader(buf, doc, stringTableBuf, treeBuf, compressor);

			// Sections
			Log.Debug("String section start: 0x{0:X8}", buf.Length);
			buf.WriteBuffer(stringTableBuf);
			Log.Debug("String section end: 0x{0:X8}", buf.Length);

			Log.Debug("Huffman node section start: 0x{0:X8}", buf.Length);
			WriteHuffmanNodes(compressor, buf);
			Log.Debug("Huffman node section end: 0x{0:X8}", buf.Length);

			Log.Debug("Tree section start: 0x{0:X8}", buf.Length);
			buf.WriteBuffer(treeBuf);
			Log.Debug("Tree section end: 0x{0:X8}", buf.Length);

			Log.Debug("Compressed data section start: 0x{0:X8}", buf.Length);
			WriteCompressedData(compressor, buf);
			Log.Debug("Compressed data section end: 0x{0:X8}", buf.Length);

			using (var fs = new FileStream(binFileName, FileMode.Create))
			{
				buf.WriteToStream(fs);
			}
		}

		private void ReadHeader()
		{
			var sig = _bin.ReadInt();
			if (sig != CoalescedFormatDetector.ME3Signature) throw new CoalescedReadException("File does not start with standard signature. This may be the incorrect format.");

			var version = _bin.ReadInt();
			if (version != 1) throw new CoalescedReadException("File version is not 1. This may be the incorrect format.");

			var maxFieldNameLength = _bin.ReadInt();
			var maxFieldValueLength = _bin.ReadInt();
			var stringSectionLength = _bin.ReadInt();
			var huffmanNodesLength = _bin.ReadInt();
			var treeSectionLength = _bin.ReadInt();
			_compressedDataLength = _bin.ReadUInt();
		}

		private void WriteHeader(BinaryBuffer buf, ME3Doc doc, BinaryBuffer stringTableBuf, BinaryBuffer treeBuf, HuffmanCompressor compressor)
		{
			var maxFieldNameLength = 0;
			var maxFieldValueLength = 0;
			foreach (var file in doc.Files)
			{
				foreach (var section in file.Sections)
				{
					foreach (var field in section.Fields)
					{
						if (field.Name.Length > maxFieldNameLength) maxFieldNameLength = field.Name.Length;
						foreach (var value in field.Values)
						{
							if (value.Length > maxFieldValueLength) maxFieldValueLength = value.Length;
						}
					}
				}
			}

			buf.WriteInt(CoalescedFormatDetector.ME3Signature);
			buf.WriteInt(1);
			buf.WriteInt(maxFieldNameLength);
			buf.WriteInt(maxFieldValueLength);
			buf.WriteInt(stringTableBuf.Length);
			buf.WriteInt(compressor.NodeData.Length * 4 + 2);
			buf.WriteInt(treeBuf.Length);
			buf.WriteInt(compressor.CompressedData.Length);
		}

		private void ReadStringTable()
		{
			Log.Debug("String section: 0x{0:X8}", _bin.Position);

			var stringTableLength = _bin.ReadInt();
			var numStrings = _bin.ReadUInt();
			Log.Debug("Number of strings: {0}", numStrings);

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
			Log.Debug("String data: 0x{0:X8}", _bin.Position);
			_bin.Read(stringBuf);

			_strings = new List<string>((int)numStrings);
			var sb = new StringBuilder();

			for (int i = 0; i < numStrings; i++)
			{
				var fileCrc = stringIndex[i * 2];

				var stringOffset = stringIndex[i * 2 + 1];
				stringOffset -= stringIndexLength;
				var stringLen = stringBuf[stringOffset] | (stringBuf[stringOffset + 1] << 8);
				stringOffset += 2;

				string str;
#if CC_UTF8
				str = Encoding.UTF8.GetString(stringBuf, (int)stringOffset, stringLen);
#else
				sb.Clear();
				for (int c = (int)stringOffset, cc = (int)stringOffset + stringLen; c < cc; c++) sb.Append((char)stringBuf[c]);
				str = sb.ToString();
#endif

				var calcCrc = Crc32.Hash(str);
				if (fileCrc != calcCrc) throw new CoalescedReadException($"CRC of string in file {fileCrc:X8} does not match calculated {calcCrc:X8}.");

				_strings.Add(str);
			}

			Log.Debug("String section ends at: 0x{0:X8}", _bin.Position);
		}

		private BinaryBuffer WriteStringTable()
		{
			var indexBuf = new BinaryBuffer();
			var strBuf = new BinaryBuffer();

			var stringOffset = _strings.Count * 8;
			foreach (var str in _strings)
			{
				indexBuf.WriteUInt(Crc32.Hash(str));
				indexBuf.WriteInt(stringOffset);

				var beforeLen = strBuf.Length;
				strBuf.WriteME3String(str);
				var strLen = strBuf.Length - beforeLen;
				stringOffset += strLen;
			}

			var stringTableLength = indexBuf.Length + strBuf.Length + 8;
			var buf = new BinaryBuffer(stringTableLength);
			buf.WriteInt(stringTableLength);
			buf.WriteInt(_strings.Count);
			buf.WriteBuffer(indexBuf);
			buf.WriteBuffer(strBuf);
			return buf;
		}

		private int[] ReadHuffmanNodes()
		{
			Log.Debug("Huffman nodes section start: 0x{0:X8}", _bin.Position);

			var numHuffmanNodes = _bin.ReadUShort();

			var huffmanNodes = new int[numHuffmanNodes * 2];
			for (int i = 0, ii = huffmanNodes.Length; i < ii; i++) huffmanNodes[i] = _bin.ReadInt();

			Log.Debug("Huffman nodes section end: 0x{0:X8}", _bin.Position);
			return huffmanNodes;
		}

		private void WriteHuffmanNodes(HuffmanCompressor compressor, BinaryBuffer buf)
		{
			var data = compressor.NodeData;
			buf.WriteUShort((ushort)(data.Length / 2));
			foreach (var n in data) buf.WriteInt(n);
		}

		private void ReadTree()
		{
			Log.Debug("Tree start: 0x{0:X8}", _bin.Position);

			_doc = new ME3Doc();

			var numFiles = _bin.ReadUShort();
			Log.Debug("Number of files: {0}", numFiles);

			for (int fileIndex = 0; fileIndex < numFiles; fileIndex++)
			{
				var fileNameId = _bin.ReadUShort();
				var fileName = _strings[fileNameId];
				_bin.ReadUInt();
				_doc.Files.Add(new ME3File(fileName));
			}

			for (int fileIndex = 0; fileIndex < numFiles; fileIndex++)
			{
				var file = _doc.Files[fileIndex];
				Log.Debug("File: {0} Position: 0x{1:X8}", file.FileName, _bin.Position);

				var numSections = _bin.ReadUShort();
				//Log.Debug("Number of sections: {0}", numSections);

				for (int sectionIndex = 0; sectionIndex < numSections; sectionIndex++)
				{
					var sectionName = _strings[_bin.ReadUShort()];
					_bin.ReadUInt();
					file.Sections.Add(new ME3Section(sectionName));
				}

				for (int sectionIndex = 0; sectionIndex < numSections; sectionIndex++)
				{
					var section = file.Sections[sectionIndex];
					//Log.Debug("Section: {0} Position: 0x{1:X8}", section.Name, _bin.Position);

					var numFields = _bin.ReadUShort();
					//Log.Debug("Number of fields: {0}", numFields);

					for (int fieldIndex = 0; fieldIndex < numFields; fieldIndex++)
					{
						var fieldNameId = _bin.ReadUShort();
						var fieldName = _strings[fieldNameId];
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

			Log.Debug("Tree end: 0x{0:X8}", _bin.Position);
		}

		private BinaryBuffer WriteTree(ME3Doc doc, HuffmanCompressor compressor)
		{
			foreach (var file in doc.Files)
			{
				foreach (var section in file.Sections)
				{
					foreach (var field in section.Fields)
					{
						foreach (var value in field.Values)
						{
							compressor.Add(value);
						}
					}
				}
			}

			compressor.Compress();

			var treeBuf = new BinaryBuffer();
			treeBuf.WriteUShort((ushort)doc.Files.Count);
			Log.Debug("Number of files: {0}", doc.Files.Count);

			var fileBuf = new BinaryBuffer();
			var sectionBuf = new BinaryBuffer();
			var fieldBuf = new BinaryBuffer();

			foreach (var file in doc.Files)
			{
				treeBuf.WriteUShort(StoreString(file.FileName));
				treeBuf.WriteInt(doc.Files.Count * 6 + 2 + fileBuf.Length);

				fileBuf.WriteUShort((ushort)file.Sections.Count);
				sectionBuf.Clear();

				foreach (var section in file.Sections)
				{
					fileBuf.WriteUShort(StoreString(section.Name));
					fileBuf.WriteInt(file.Sections.Count * 6 + 2 + sectionBuf.Length);

					sectionBuf.WriteUShort((ushort)section.Fields.Count);
					fieldBuf.Clear();

					foreach (var field in section.Fields)
					{
						sectionBuf.WriteUShort(StoreString(field.Name));
						sectionBuf.WriteInt(section.Fields.Count * 6 + 2 + fieldBuf.Length);

						fieldBuf.WriteUShort((ushort)field.Values.Count);

						foreach (var value in field.Values)
						{
							fieldBuf.WriteInt(compressor.GetStringPosition(value) | ((int)Type_String << 28));
						}
					}

					sectionBuf.WriteBuffer(fieldBuf);
				}

				fileBuf.WriteBuffer(sectionBuf);
			}

			treeBuf.WriteBuffer(fileBuf);
			return treeBuf;
		}

		private void ReadCompressedData(int[] huffmanNodes)
		{
			Log.Debug("Compressed data section start: 0x{0:X8}", _bin.Position);

			var unk = _bin.ReadUInt();
			var compressedData = _bin.ReadBytes((int)_compressedDataLength);

			var values = new List<string>();
			var decomp = new HuffmanDecompressor(huffmanNodes, compressedData);

			foreach (var file in _doc.Files)
			{
				foreach (var section in file.Sections)
				{
					foreach (var field in section.Fields)
					{
						foreach (var offset in field.Offsets)
						{
							var str = decomp.GetString((int)(offset & 0xFFFFFFF));
							var type = offset >> 28;
							if (type != Type_String) throw new UnsupportedValueType(type);
							field.Values.Add(str);
						}
					}
				}
			}

			Log.Debug("Compressed data section end: 0x{0:X8}", _bin.Position);
		}

		private void WriteCompressedData(HuffmanCompressor compressor, BinaryBuffer buf)
		{
			var compressedData = compressor.CompressedData;
			buf.WriteInt(compressedData.Length);
			buf.WriteBytes(compressedData);
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

		private ME3Doc ReadIniToDocument(string fileName)
		{
			var doc = new ME3Doc();

			using (var fs = new FileStream(fileName, FileMode.Open))
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

			return doc;
		}

		private ushort StoreString(string str)
		{
			var id = _strings.IndexOf(str);
			if (id < 0)
			{
				id = _strings.Count;
				_strings.Add(str);
			}
			if (id > ushort.MaxValue) throw new TooManyStringsException();
			return (ushort)id;
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
