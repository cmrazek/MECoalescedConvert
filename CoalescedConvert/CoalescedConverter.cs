using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoalescedConvert
{
	abstract class CoalescedConverter
	{
		public abstract void Decode(string binFileName, string iniFileName);
		public abstract void Encode(string iniFileName, string binFileName);

		private bool _whatIf;

		public CoalescedConverter(bool whatIf)
		{
			_whatIf = whatIf;
		}

		public bool WhatIf => _whatIf;
	}
}
