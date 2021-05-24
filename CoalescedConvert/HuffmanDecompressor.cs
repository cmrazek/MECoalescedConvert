using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// https://www.programmingalgorithms.com/algorithm/huffman-decompress/

namespace CoalescedConvert
{
	class HuffmanDecompressor
	{
		private int[] _data;

		public HuffmanDecompressor(int[] data)
		{
			_data = data ?? throw new ArgumentNullException(nameof(data));
		}

		public string Decompress(byte[] cmpData, int offset)
		{
			var sb = new StringBuilder();
			var end = cmpData.Length * 8;
			var pos = offset;

			while (pos < end)
			{
				Node node;
				for (node = RootNode; pos < end && !node.CharValue.HasValue; pos++)
				{
					var sample = cmpData[pos / 8] & (1 << (pos % 8));
					node = sample != 0 ? node.Right : node.Left;
				}
				var ch = node.CharValue ?? (char)0;
				if (ch == 0) break;
				sb.Append(ch);
			}

			return sb.ToString();
		}

		private Node RootNode => new Node(_data, _data.Length / 2 - 1);

		private struct Node
		{
			public int[] _data;
			public int _nid;

			public Node(int[] data, int nid)
			{
				_data = data;
				_nid = nid;
			}

			public char? CharValue => _nid > 0 ? null : (char)(-1 - _nid);
			public Node Left => new Node(_data, _data[_nid * 2]);
			public Node Right => new Node(_data, _data[_nid * 2 + 1]);
		}
	}
}
