using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoalescedConvert
{
	public abstract class CoalescedConverter
	{
		public abstract void Decode(string binFileName, string iniFileName);
		public abstract void Encode(string iniFileName, string binFileName);

		public static CoalescedConverter Create(CoalescedFormat format)
		{
			switch (format)
			{
				case CoalescedFormat.MassEffect2:
					return new ME2Converter();
				case CoalescedFormat.MassEffect3:
					return new ME3Converter();
				case CoalescedFormat.MassEffect12LE:
					return new ME12LEConverter();
				default:
					throw new UnknownCoalescedFormatException();
			}
		}

		public bool WhatIf { get; set; }
	}
}
