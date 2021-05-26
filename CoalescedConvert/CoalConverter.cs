using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoalescedConvert
{
	public abstract class CoalConverter
	{
		public abstract CoalDocument Load(Stream stream);
		public abstract void Save(CoalDocument doc, Stream stream, bool leaveStreamOpen);

		public static CoalConverter Create(CoalFormat format)
		{
			switch (format)
			{
				case CoalFormat.MassEffect2:
					return new ME2Converter();
				case CoalFormat.MassEffect3:
					return new ME3Converter();
				case CoalFormat.MassEffect12LE:
					return new ME12LEConverter();
				default:
					throw new UnknownCoalescedFormatException();
			}
		}
	}
}
