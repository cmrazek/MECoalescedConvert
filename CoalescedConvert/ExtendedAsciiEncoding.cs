﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoalescedConvert
{
	class ExtendedAsciiEncoding : Encoding
	{
		public override int GetByteCount(char[] chars, int index, int count) => count;

		public override int GetBytes(char[] chars, int charIndex, int charCount, byte[] bytes, int byteIndex)
		{
			for (int i = 0; i < charCount; i++)
			{
				bytes[i + byteIndex] = (byte)chars[i + charIndex];
			}
			return charCount;
		}

		public override int GetCharCount(byte[] bytes, int index, int count) => count;

		public override int GetChars(byte[] bytes, int byteIndex, int byteCount, char[] chars, int charIndex)
		{
			for (int i = 0; i < byteCount; i++)
			{
				chars[i + charIndex] = (char)bytes[i + byteIndex];
			}
			return byteCount;
		}

		public override int GetMaxByteCount(int charCount) => charCount;

		public override int GetMaxCharCount(int byteCount) => byteCount;
	}
}
