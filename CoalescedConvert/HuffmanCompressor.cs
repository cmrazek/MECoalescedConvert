using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace CoalescedConvert
{
	class HuffmanCompressor
	{
		private Dictionary<string, int> _strings = new Dictionary<string, int>();
		private int[] _nodes;
		private Dictionary<char, ChainCode> _chainCodes;
		private byte[] _compressedData;

		public void Add(string str)
		{
			_strings[str] = 0;
		}

		public int GetStringPosition(string str)
		{
			return _strings[str];
		}

		public void Compress()
		{
			// Calculate weights
			var buf = new byte[32];
			var weights = new Dictionary<char, uint>();
			var uncompressedSize = 0;
			uint weight;

			foreach (var str in _strings.Keys)
			{
#if CC_UTF8
				var numChars = Encoding.UTF8.GetByteCount(str);
				if (numChars > buf.Length) buf = new byte[numChars];
				Encoding.UTF8.GetBytes(str, 0, str.Length, buf, 0);
				for (int i = 0; i < numChars; i++)
				{
					if (weights.TryGetValue((char)buf[i], out weight)) weights[(char)buf[i]] = weight + 1;
					else weights[(char)buf[i]] = 0;
				}
				if (weights.TryGetValue('\0', out weight)) weights['\0'] = weight + 1;
				else weights['\0'] = 0;
				uncompressedSize += numChars + 1;
#else
				foreach (var ch in str)
				{
					if (weights.TryGetValue(ch, out weight)) weights[ch] = weight + 1;
					else weights[ch] = 0;
				}
				if (weights.TryGetValue('\0', out weight)) weights['\0'] = weight + 1;
				else weights['\0'] = 0;
#endif
			}

			Log.Debug($"Uncompressed data length: {uncompressedSize}");

			BuildNodes(weights);

			// Compress the data
			var cmpPos = 0;
			var cmpData = new List<byte>();
			byte cmpByte = 0;
			var cmpBit = 0;
			Action<int> addBit = (val) =>
			{
				if (val != 0) cmpByte |= (byte)(1 << cmpBit);
				cmpBit++;
				if (cmpBit == 8)
				{
					cmpData.Add(cmpByte);
					cmpByte = 0;
					cmpBit = 0;
				}
				cmpPos++;
			};
			foreach (var str in _strings.Keys.ToList())
			{
				_strings[str] = cmpPos;

				ChainCode chainCode;

#if CC_UTF8
				var numChars = Encoding.UTF8.GetByteCount(str);
				if (numChars > buf.Length) buf = new byte[numChars];
				Encoding.UTF8.GetBytes(str, 0, str.Length, buf, 0);
				for (int i = 0; i < numChars; i++)
				{
					var n = buf[i];
					chainCode = _chainCodes[(char)n];
					for (int j = 0; j < chainCode.length; j++)
					{
						addBit(chainCode.code & (1 << j));
					}
				}
#else
				foreach (var ch in str)
				{
					chainCode = _chainCodes[ch];
					for (int j = 0; j < chainCode.length; j++)
					{
						addBit(chainCode.code & (1 << j));
					}
				}
#endif
				chainCode = _chainCodes['\0'];
				for (int j = 0; j < chainCode.length; j++)
				{
					addBit(chainCode.code & (1 << j));
				}
			}
			if (cmpBit != 0) cmpData.Add(cmpByte);

			_compressedData = cmpData.ToArray();

			Log.Debug($"Compressed data length: {_compressedData.Length}");
		}

		private void BuildNodes(Dictionary<char, uint> weights)
		{
			// Build a priority queue with the value nodes
			var queue = new List<Node>(256);
			foreach (var ch in weights.Keys)
			{
				queue.Add(new Node { value = ch, weight = weights[ch] });
			}
			if (queue.Count == 0) queue.Add(new Node { value = '\0' });    // Rare edge-case that will likely never be hit.
			queue = queue.OrderBy(x => x.weight).ToList();

			// Convert to a tree
			while (queue.Count > 1)
			{
				var left = queue[0];
				var right = queue[1];
				var node = new Node
				{
					value = null,
					weight = left.weight + right.weight,
					left = left,
					right = right
				};

				queue.RemoveRange(0, 2);

				var index = queue.FindIndex(x => x.weight > node.weight);
				if (index < 0) queue.Add(node);
				else queue.Insert(index, node);
			}

			// Convert to an int array
			var flatNodes = new List<Node>();
			FlattenNodes(queue[0], flatNodes);

			// Calculate chain codes
			_chainCodes = new Dictionary<char, ChainCode>();
			AssignChainCodes(queue[0], ChainCode.Empty);

			var intArray = new int[flatNodes.Count * 2];
			var intIndex = 0;
			foreach (var node in flatNodes)
			{
				intArray[intIndex++] = node.left.value.HasValue ? (-1 - node.left.value.Value) : flatNodes.IndexOf(node.left);
				intArray[intIndex++] = node.right.value.HasValue ? (-1 - node.right.value.Value) : flatNodes.IndexOf(node.right);

				if (node.value.HasValue)
				{
					_chainCodes[node.value.Value] = node.chainCode;
				}
			}

			_nodes = intArray.ToArray();
#if DEBUG
			Debug_NodeTreeToJson(queue[0]);
			Debug_RawNodes();
#endif
		}

		private void FlattenNodes(Node node, List<Node> nodes)
		{
			if (node.value == null)
			{
				FlattenNodes(node.left, nodes);
				FlattenNodes(node.right, nodes);
				nodes.Add(node);
			}
		}

		private void AssignChainCodes(Node node, ChainCode chainCode)
		{
			node.chainCode = chainCode;

			if (node.value != null)
			{
				_chainCodes[node.value.Value] = node.chainCode;
			}

			if (node.left != null)
			{
				AssignChainCodes(node.left, chainCode.Left());
			}

			if (node.right != null)
			{
				AssignChainCodes(node.right, chainCode.Right());
			}
		}

		public byte[] CompressedData => _compressedData;

		public int[] NodeData => _nodes;

		private class Node
		{
			public char? value;
			public uint weight;
			public Node left;
			public Node right;
			public ChainCode chainCode;
		}

		private void Debug_NodeTreeToJson(Node rootNode)
		{
			using (var stream = new FileStream("c:\\temp\\nodes.json", FileMode.Create))
			using (var json = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
			{
				Debug_NodeTreeToJson(json, rootNode);
			}
		}

		private void Debug_NodeTreeToJson(Utf8JsonWriter json, Node node)
		{
			json.WriteStartObject();

			if (node.value.HasValue)
			{
				json.WriteString("value", $"{node.value} ({(char)node.value})");
				json.WriteNumber("weight", node.weight);
				json.WriteString("chainCode", node.chainCode.ToString());
			}

			if (node.left != null)
			{
				json.WritePropertyName("left");
				Debug_NodeTreeToJson(json, node.left);
			}

			if (node.right != null)
			{
				json.WritePropertyName("right");
				Debug_NodeTreeToJson(json, node.right);
			}

			json.WriteEndObject();
		}

		private void Debug_RawNodes()
		{
			using (var rep = new StreamWriter("c:\\temp\\rawnodes.txt"))
			{
				for (int i = 0; i < _nodes.Length; i += 2)
				{
					rep.WriteLine($"Node {i / 2} left [{_nodes[i]}] right [{_nodes[i + 1]}]");
				}
			}
		}

		private struct ChainCode
		{
			public int code;
			public int length;

			public static readonly ChainCode Empty = new ChainCode { code = 0, length = 0 };

			public ChainCode Left()
			{
				return new ChainCode
				{
					code = code,
					length = length + 1
				};
			}

			public ChainCode Right()
			{
				return new ChainCode
				{
					code = code | (1 << length),
					length = length + 1
				};
			}

			public override string ToString()
			{
				var sb = new StringBuilder();
				for (int i = 0; i < length; i++)
				{
					sb.Append((code & (1 << i)) != 0 ? "1" : "0");
				}
				return sb.ToString();
			}
		}
	}
}
