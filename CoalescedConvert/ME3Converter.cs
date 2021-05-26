using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoalescedConvert
{
	class ME3Converter : CoalConverter
	{
		private CoalFileStream _bin;
		private StringTable _strings;

		private uint _compressedDataLength;

		private const uint Type_String = 4;

		public override CoalDocument Load(Stream stream)
		{
			var doc = null as CoalDocument;

			using (_bin = new CoalFileStream(stream, CoalFormat.MassEffect3))
			{
				ReadHeader();
				ReadStringTable();
				var huffmanNodes = ReadHuffmanNodes();
				doc = ReadTree();
				ReadCompressedData(huffmanNodes, doc);
			}

			return doc;
		}

		public override void Save(CoalDocument doc, Stream stream, bool leaveStreamOpen)
		{
			var compressor = new HuffmanCompressor();

			_strings = new StringTable(1024);
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

			buf.WriteToStream(stream);
		}

		private void ReadHeader()
		{
			var sig = _bin.ReadInt();
			if (sig != CoalFormatDetector.ME3Signature) throw new CoalescedReadException("File does not start with standard signature. This may be the incorrect format.");

			var version = _bin.ReadInt();
			if (version != 1) throw new CoalescedReadException("File version is not 1. This may be the incorrect format.");

			var maxFieldNameLength = _bin.ReadInt();
			var maxFieldValueLength = _bin.ReadInt();
			var stringSectionLength = _bin.ReadInt();
			var huffmanNodesLength = _bin.ReadInt();
			var treeSectionLength = _bin.ReadInt();
			_compressedDataLength = _bin.ReadUInt();
		}

		private void WriteHeader(BinaryBuffer buf, CoalDocument doc, BinaryBuffer stringTableBuf, BinaryBuffer treeBuf, HuffmanCompressor compressor)
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

			buf.WriteInt(CoalFormatDetector.ME3Signature);
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

			_strings = new StringTable((int)numStrings);
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
			foreach (var str in _strings.Strings)
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

		private CoalDocument ReadTree()
		{
			Log.Debug("Tree start: 0x{0:X8}", _bin.Position);

			var doc = new CoalDocument(CoalFormat.MassEffect3);

			var numFiles = _bin.ReadUShort();
			Log.Debug("Number of files: {0}", numFiles);

			for (int fileIndex = 0; fileIndex < numFiles; fileIndex++)
			{
				var fileNameId = _bin.ReadUShort();
				var fileName = _strings.GetString(fileNameId);
				_bin.ReadUInt();
				doc.Files.Add(new CoalFile(fileName));
			}

			for (int fileIndex = 0; fileIndex < numFiles; fileIndex++)
			{
				var file = doc.Files[fileIndex];
				Log.Debug("File: {0} Position: 0x{1:X8}", file.FileName, _bin.Position);

				var numSections = _bin.ReadUShort();
				//Log.Debug("Number of sections: {0}", numSections);

				for (int sectionIndex = 0; sectionIndex < numSections; sectionIndex++)
				{
					var sectionName = _strings.GetString(_bin.ReadUShort());
					_bin.ReadUInt();
					file.Sections.Add(new CoalSection(sectionName));
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
						var fieldName = _strings.GetString(fieldNameId);
						_bin.ReadUInt();
						section.Fields.Add(new CoalField(fieldName));
					}

					for (int fieldIndex = 0; fieldIndex < numFields; fieldIndex++)
					{
						var field = section.Fields[fieldIndex];
						var numValues = _bin.ReadUShort();

						for (int valueIndex = 0; valueIndex < numValues; valueIndex++)
						{
							field.Values.Add(_bin.ReadUInt().ToString());
						}
					}
				}
			}

			Log.Debug("Tree end: 0x{0:X8}", _bin.Position);
			return doc;
		}

		private BinaryBuffer WriteTree(CoalDocument doc, HuffmanCompressor compressor)
		{
			foreach (var file in doc.Files)
			{
				_strings.Add(file.FileName);

				foreach (var section in file.Sections)
				{
					_strings.Add(section.Name);

					foreach (var field in section.Fields)
					{
						_strings.Add(field.Name);

						foreach (var value in field.Values)
						{
							compressor.Add(value);
						}
					}
				}
			}

			_strings.Sort();
			compressor.Compress();

			var treeBuf = new BinaryBuffer();
			treeBuf.WriteUShort((ushort)doc.Files.Count);
			Log.Debug("Number of files: {0}", doc.Files.Count);

			var fileBuf = new BinaryBuffer();
			var sectionBuf = new BinaryBuffer();
			var fieldBuf = new BinaryBuffer();

			foreach (var file in doc.Files)
			{
				treeBuf.WriteUShort(_strings.GetId(file.FileName));
				treeBuf.WriteInt(doc.Files.Count * 6 + 2 + fileBuf.Length);

				fileBuf.WriteUShort((ushort)file.Sections.Count);
				sectionBuf.Clear();

				foreach (var section in file.Sections)
				{
					fileBuf.WriteUShort(_strings.GetId(section.Name));
					fileBuf.WriteInt(file.Sections.Count * 6 + 2 + sectionBuf.Length);

					sectionBuf.WriteUShort((ushort)section.Fields.Count);
					fieldBuf.Clear();

					foreach (var field in section.Fields)
					{
						sectionBuf.WriteUShort(_strings.GetId(field.Name));
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

		private void ReadCompressedData(int[] huffmanNodes, CoalDocument doc)
		{
			Log.Debug("Compressed data section start: 0x{0:X8}", _bin.Position);

			var unk = _bin.ReadUInt();
			var compressedData = _bin.ReadBytes((int)_compressedDataLength);

			var values = new List<string>();
			var decomp = new HuffmanDecompressor(huffmanNodes, compressedData);

			foreach (var file in doc.Files)
			{
				foreach (var section in file.Sections)
				{
					foreach (var field in section.Fields)
					{
						for (int v = 0, vv = field.Values.Count; v < vv; v++)
						{
							var offset = uint.Parse(field.Values[v]);
							var str = decomp.GetString((int)(offset & 0xFFFFFFF));
							var type = offset >> 28;
							if (type != Type_String) throw new UnsupportedValueType(type);
							field.Values[v] = str;
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
	}
}
