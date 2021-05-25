using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoalescedConvert
{
	class BinaryBuffer : IEnumerable<byte>
	{
		private byte[] _buf;
		private int _len;

		private const int DefaultCapacity = 32;

		public BinaryBuffer()
		{
			_buf = new byte[DefaultCapacity];
			_len = 0;
		}

		public BinaryBuffer(int capacity)
		{
			if (capacity < 0) throw new ArgumentOutOfRangeException(nameof(capacity));
			_buf = new byte[capacity];
			_len = 0;
		}

		public int Length => _len;

		public int Capacity
		{
			get => _buf.Length;
			set
			{
				if (_buf.Length >= value) return;

				var capacity = _buf.Length;
				while (value > capacity) capacity *= 2;

				var newBuf = new byte[capacity];
				if (_len != 0) _buf.CopyTo(newBuf, 0);
				_buf = newBuf;
			}
		}

		public void Clear()
		{
			_len = 0;
		}

		public void WriteBuffer(BinaryBuffer bb)
		{
			Capacity = _len + bb._len;
			for (int i = 0, ii = bb._len; i < ii; i++)
			{
				_buf[_len++] = bb._buf[i];
			}
		}

		public void WriteInt(int value)
		{
			Capacity = _len + 4;
			_buf[_len++] = (byte)(value & 0xff);
			_buf[_len++] = (byte)((value >> 8) & 0xff);
			_buf[_len++] = (byte)((value >> 16) & 0xff);
			_buf[_len++] = (byte)((value >> 24) & 0xff);
		}

		public void WriteUInt(uint value)
		{
			Capacity = _len + 4;
			_buf[_len++] = (byte)(value & 0xff);
			_buf[_len++] = (byte)((value >> 8) & 0xff);
			_buf[_len++] = (byte)((value >> 16) & 0xff);
			_buf[_len++] = (byte)((value >> 24) & 0xff);
		}

		public void WriteShort(short value)
		{
			Capacity = _len + 2;
			_buf[_len++] = (byte)(value & 0xff);
			_buf[_len++] = (byte)((value >> 8) & 0xff);
		}

		public void WriteUShort(ushort value)
		{
			Capacity = _len + 2;
			_buf[_len++] = (byte)(value & 0xff);
			_buf[_len++] = (byte)((value >> 8) & 0xff);
		}

		public void WriteByte(byte value)
		{
			Capacity = _len + 1;
			_buf[_len++] = value;
		}

		public void WriteBytes(byte[] bytes)
		{
			Capacity = _len + bytes.Length;
			for (int i = 0, ii = bytes.Length; i < ii; i++) _buf[_len++] = bytes[i];
		}

		public void WriteME3String(string str)
		{
			if (string.IsNullOrEmpty(str))
			{
				WriteUShort(0);
				return;
			}

#if CC_UTF8
			var bytes = Encoding.UTF8.GetBytes(str);
			if (bytes.Length > ushort.MaxValue) throw new StringTooLongException();
			WriteUShort((ushort)bytes.Length);
			WriteBytes(bytes);
#else
			WriteUShort((ushort)str.Length);
			foreach (var ch in str) WriteByte((byte)ch);
#endif
		}

		public void WriteToStream(Stream stream)
		{
			stream.Write(_buf, 0, _len);
		}

		public void WriteToFile(string fileName)
		{
			using (var fs = new FileStream(fileName, FileMode.Create))
			{
				WriteToStream(fs);
			}
		}

		public IEnumerator<byte> GetEnumerator()
		{
			return new BinaryBufferEnumerator(this);
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return new BinaryBufferEnumerator(this);
		}

		public class BinaryBufferEnumerator : IEnumerator<byte>
		{
			private BinaryBuffer _buf;
			private int _index;

			public BinaryBufferEnumerator(BinaryBuffer buf)
			{
				_buf = buf;
				_index = -1;
			}

			public byte Current => _buf._buf[_index];

			object IEnumerator.Current => _buf._buf[_index];

			public bool MoveNext()
			{
				if (_index < _buf._len) _index++;
				return _index < _buf._len;
			}

			public void Reset()
			{
				_index = 0;
			}

			public void Dispose()
			{
			}
		}
	}
}
