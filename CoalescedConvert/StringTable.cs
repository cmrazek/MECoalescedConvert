using System.Collections.Generic;

namespace CoalescedConvert
{
	class StringTable
	{
		private List<string> _strings;
		private Dictionary<string, int> _map;

		public StringTable(int capacity)
		{
			_strings = new List<string>(capacity);
			_map = new Dictionary<string, int>(capacity);
		}

		public void Add(string str)
		{
			str = str.ToLower();
			if (!_map.ContainsKey(str))
			{
				_map[str] = _strings.Count;
				_strings.Add(str);
				if (_strings.Count > ushort.MaxValue) throw new TooManyStringsException();
			}
		}

		public int Count => _strings.Count;
		public IEnumerable<string> Strings => _strings;

		public ushort GetId(string str)
		{
			return _map.TryGetValue(str.ToLower(), out var id) ? (ushort)id : (ushort)0;
		}

		public string GetString(int id)
		{
			return _strings[id];
		}

		public void Sort()
		{
			_strings.Sort(new Crc32StringComparer());

			for (int i = 0, ii = _strings.Count; i < ii; i++) _map[_strings[i]] = i;
		}

		private class Crc32StringComparer : IComparer<string>
		{
			public int Compare(string x, string y)
			{
				var xHash = Crc32.Hash(x);
				var yHash = Crc32.Hash(y);

				if (xHash < yHash) return -1;
				if (xHash > yHash) return 1;
				return 0;
			}
		}
	}
}
