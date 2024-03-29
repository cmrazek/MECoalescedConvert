﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoalescedConvert
{
	class CoalFileStream : IDisposable
	{
		private Stream _fs;
		private CoalFormat _format;
		private long _valuePos;
		private bool _leaveOpen;

		public CoalFileStream(Stream stream, CoalFormat format, bool leaveOpen = false)
		{
			_fs = stream ?? throw new ArgumentNullException(nameof(stream));
			_format = format;
			_leaveOpen = leaveOpen;
		}

		public long ValuePosition => _valuePos;

		public void Dispose()
		{
			if (!_leaveOpen) _fs?.Dispose();
			_fs = null;
		}

		public void Flush()
		{
			_fs.Flush();
		}

		public bool EndOfStream => _fs.Position >= _fs.Length;

		public int ReadInt()
		{
			_valuePos = _fs.Position;

			var n1 = _fs.ReadByte();
			if (n1 < 0) throw new EndOfStreamException();
			var n2 = _fs.ReadByte();
			if (n2 < 0) throw new EndOfStreamException();
			var n3 = _fs.ReadByte();
			if (n3 < 0) throw new EndOfStreamException();
			var n4 = _fs.ReadByte();
			if (n4 < 0) throw new EndOfStreamException();

			return n1 | (n2 << 8) | (n3 << 16) | (n4 << 24);
		}

		public void WriteInt(int value)
		{
			_fs.WriteByte((byte)(value & 0xff));
			_fs.WriteByte((byte)((value >> 8) & 0xff));
			_fs.WriteByte((byte)((value >> 16) & 0xff));
			_fs.WriteByte((byte)((value >> 24) & 0xff));
		}

		public uint ReadUInt()
		{
			_valuePos = _fs.Position;

			var n1 = _fs.ReadByte();
			if (n1 < 0) throw new EndOfStreamException();
			var n2 = _fs.ReadByte();
			if (n2 < 0) throw new EndOfStreamException();
			var n3 = _fs.ReadByte();
			if (n3 < 0) throw new EndOfStreamException();
			var n4 = _fs.ReadByte();
			if (n4 < 0) throw new EndOfStreamException();

			return (uint)n1 | (uint)(n2 << 8) | (uint)(n3 << 16) | (uint)(n4 << 24);
		}

		public void WriteUInt(uint value)
		{
			_fs.WriteByte((byte)(value & 0xff));
			_fs.WriteByte((byte)((value >> 8) & 0xff));
			_fs.WriteByte((byte)((value >> 16) & 0xff));
			_fs.WriteByte((byte)((value >> 24) & 0xff));
		}

		public short ReadShort()
		{
			_valuePos = _fs.Position;

			var n1 = _fs.ReadByte();
			if (n1 < 0) throw new EndOfStreamException();
			var n2 = _fs.ReadByte();
			if (n2 < 0) throw new EndOfStreamException();

			return (short)(n1 | (n2 << 8));
		}

		public void WriteShort(short value)
		{
			_fs.WriteByte((byte)(value & 0xff));
			_fs.WriteByte((byte)((value >> 8) & 0xff));
		}

		public ushort ReadUShort()
		{
			_valuePos = _fs.Position;

			var n1 = _fs.ReadByte();
			if (n1 < 0) throw new EndOfStreamException();
			var n2 = _fs.ReadByte();
			if (n2 < 0) throw new EndOfStreamException();

			return (ushort)(n1 | (n2 << 8));
		}

		public void WriteUShort(ushort value)
		{
			_fs.WriteByte((byte)(value & 0xff));
			_fs.WriteByte((byte)((value >> 8) & 0xff));
		}

		public byte ReadByte()
		{
			_valuePos = _fs.Position;

			var n = _fs.ReadByte();
			if (n < 0) throw new EndOfStreamException();
			return (byte)n;
		}

		public void WriteByte(byte value)
		{
			_fs.WriteByte(value);
		}

		public void WriteBytes(byte[] bytes)
		{
			_fs.Write(bytes);
		}

		public string ReadString()
		{
			if (_format == CoalFormat.MassEffect12LE)
			{
				var pos = _fs.Position;
				var len = ReadInt();
				if (len > 0) throw new CoalescedReadException($"String prefix [{len}] is greater than zero at position 0x{pos:X8}");
				len = -len;

				var sb = new StringBuilder(len);
				for (int i = 0; i < len; i++)
				{
					char ch = (char)(ReadByte() | (ReadByte() << 8));
					if (ch == 0) break;
					sb.Append(ch);
				}

				_valuePos = pos;
				return sb.ToString();
			}
			else if (_format == CoalFormat.MassEffect2)
			{
				var pos = _fs.Position;
				var len = ReadInt();
				if (len < 0) throw new CoalescedReadException($"String prefix [{len}] is less than zero at position 0x{pos:X8}");

				var sb = new StringBuilder(len);
				for (int i = 0; i < len; i++)
				{
					char ch = (char)ReadByte();
					if (ch == 0) break;
					sb.Append(ch);
				}

				_valuePos = pos;
				return sb.ToString();
			}
			else if (_format == CoalFormat.MassEffect3)
			{
				var pos = _fs.Position;
				var len = ReadShort();
				if (len < 0) throw new CoalescedReadException($"String prefix [{len}] is less than zero at position 0x{pos}");

#if CC_UTF8
				var bytes = new byte[len];
				for (int i = 0; i < len; i++) bytes[i] = ReadByte();
				_valuePos = pos;
				return Encoding.UTF8.GetString(bytes);
#else
				var sb = new StringBuilder(len);
				for (int i = 0; i < len; i++) sb.Append((char)ReadByte());
				return sb.ToString();
#endif
			}
			else throw new UnknownCoalescedFormatException();
		}

		public void WriteString(string str)
		{
			if (_format == CoalFormat.MassEffect12LE)
			{
				if (str.Length == 0)
				{
					WriteInt(0);
				}
				else
				{
					WriteInt(-(str.Length + 1));
					foreach (var ch in str)
					{
						WriteByte((byte)(ch & 0xff));
						WriteByte((byte)((ch >> 8) & 0xff));
					}
					WriteByte(0);
					WriteByte(0);
				}
			}
			else if (_format == CoalFormat.MassEffect2)
			{
				if (str.Length == 0)
				{
					WriteInt(0);
				}
				else
				{
					WriteInt(str.Length + 1);
					foreach (var ch in str)
					{
						WriteByte((byte)ch);
					}
					WriteByte(0);
				}
			}
			else if (_format == CoalFormat.MassEffect3)
			{
#if CC_UTF8
				var bytes = Encoding.UTF8.GetBytes(str);
				if (bytes.Length == 0)
				{
					WriteShort(0);
				}
				else
				{
					if (bytes.Length > short.MaxValue) throw new ArgumentException($"String length is too long to be encoded in coalesced.bin: {str}");
					WriteShort((short)bytes.Length);
					foreach (var n in bytes) WriteByte(n);
				}
#else
				if (string.IsNullOrEmpty(str))
				{
					WriteShort(0);
				}
				else
				{
					WriteUShort((ushort)str.Length);
					foreach (var ch in str) WriteByte((byte)ch);
				}
#endif
			}
			else throw new UnknownCoalescedFormatException();
		}

		public void Read(byte[] buf)
		{
			_valuePos = _fs.Position;

			var numRead = _fs.Read(buf);
			if (numRead != buf.Length) throw new EndOfStreamException();
		}

		public byte[] ReadBytes(int length)
		{
			_valuePos = _fs.Position;

			var buf = new byte[length];
			var numRead = _fs.Read(buf);
			if (numRead != buf.Length) throw new EndOfStreamException();
			return buf;
		}

		public long Position => _fs.Position;

		public void Goto(long pos)
		{
			_fs.Seek(pos, SeekOrigin.Begin);
		}
	}
}
