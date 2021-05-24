using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoalescedConvert
{
	class UnknownCoalescedFormatException : Exception
	{
		public UnknownCoalescedFormatException() : base("The file format could not be determined.") { }
	}

	class CoalescedReadException : Exception
	{
		public CoalescedReadException(string message) : base(message) { }
	}

	class IniNoCurrentFileException : Exception
	{
		public IniNoCurrentFileException(int lineNumber) : base($"A section was declared on line {lineNumber} but no file name is present.") { }
		public IniNoCurrentFileException(string message) : base(message) { }
	}

	class IniInvalidKeyNameException : Exception
	{
		public IniInvalidKeyNameException(int lineNumber) : base($"The key name on line {lineNumber} is invalid.") { }
		public IniInvalidKeyNameException(string message) : base(message) { }
	}

	class IniNoCurrentSectionException : Exception
	{
		public IniNoCurrentSectionException(int lineNumber) : base($"A value was declared on line {lineNumber} but no current section is set.") { }
	}
}
