using System;
using System.Text;

namespace CoalescedConvert
{
	class HuffmanDecompressor
	{
		private int[] _nodes;
		private byte[] _compressedData;

		public HuffmanDecompressor(int[] nodes, byte[] compressedData)
		{
			_nodes = nodes ?? throw new ArgumentNullException(nameof(nodes));
			_compressedData = compressedData ?? throw new ArgumentNullException(nameof(compressedData));
		}

		public string GetString(int position)
		{
			var sb = new StringBuilder();
			var end = _compressedData.Length * 8;
			var curNode = _nodes.Length - 2;

			for (var pos = position; pos < end; pos++)
			{
				var sample = _compressedData[pos / 8] & (1 << (pos % 8));
				var next = _nodes[curNode + (sample != 0 ? 1 : 0)];
				if (next < 0)
				{
					var ch = (char)(-1 - next);
					if (ch == 0) break;
					sb.Append(ch);
					curNode = _nodes.Length - 2;
				}
				else
				{
					curNode = next * 2;
					if (curNode > _nodes.Length) throw new DecompressionException("The decompression nodes are malformed.");
				}
			}

			return sb.ToString();
		}

		public static string DumpNodesToText(int[] nodes)
		{
			var sb = new StringBuilder();
			for (int i = 0, ii = nodes.Length; i < ii; i += 2)
			{
				sb.AppendLine($"Node {i / 2} left [{nodes[i]}] right [{nodes[i + 1]}]");
			}
			return sb.ToString();
		}
	}
}
