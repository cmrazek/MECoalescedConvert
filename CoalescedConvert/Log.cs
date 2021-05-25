using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoalescedConvert
{
	static class Log
	{
		private static bool _verbose = false;

		public static bool Verbose { get => _verbose; set => _verbose = value; }

		public static void Debug(string message)
		{
			if (_verbose)
			{
				Console.WriteLine(message);
			}
		}

		public static void Debug(string format, params object[] args)
		{
			if (_verbose)
			{
				Console.WriteLine(string.Format(format, args));
			}
		}

		public static void Debug(Func<string> callback)
		{
			if (_verbose)
			{
				Console.WriteLine(callback());
			}
		}
	}
}
