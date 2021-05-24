using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoalescedConvert
{
	class Crc32
	{
        private const uint Polynomial = 79764919;
        private static uint[] Table = Crc32.GenerateTable();

        private static uint[] GenerateTable()
        {
            uint[] entries = new uint[256];
            entries[0] = 0;
            entries[1] = Polynomial;
            for (uint i = 2; i < 256; ++i)
            {
                uint x = i << 24;
                for (uint j = 8; j > 0U; --j)
                {
                    if (((int)x & int.MinValue) != 0)
                        x = (uint)((int)x << 1 ^ Polynomial);
                    else
                        x <<= 1;
                }
                entries[i] = x;
            }
            return entries;
        }

        public static uint Hash(string s)
        {
            uint hash = uint.MaxValue;
            foreach (char ch in s)
            {
                uint x = hash ^ (uint)(((int)ch & (int)byte.MaxValue) << 24);
                hash = x << 8 ^ Crc32.Table[(x >> 24)];
            }
            return ~hash;
        }
    }
}
