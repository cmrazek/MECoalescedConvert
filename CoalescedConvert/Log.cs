using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoalescedConvert
{
	public static class Log
	{
		private static bool _verbose = false;

		public static bool Verbose { get => _verbose; set => _verbose = value; }

		public static void Debug(string message)
		{
			if (_verbose)
			{
				Console.ForegroundColor = ConsoleColor.Cyan;
				Console.WriteLine(message);
			}
		}

		public static void Debug(string format, params object[] args)
		{
			if (_verbose)
			{
				Debug(string.Format(format, args));
			}
		}

		public static void Debug(Func<string> callback)
		{
			if (_verbose)
			{
				Debug(callback());
			}
		}

		public static void Info(string message)
		{
			Console.ForegroundColor = ConsoleColor.Gray;
			Console.WriteLine(message);
		}

		public static void Heading(string message)
		{
			Console.ForegroundColor = ConsoleColor.White;
			Console.WriteLine(message);
		}

		public static void Success(string message)
		{
			Console.ForegroundColor = ConsoleColor.Green;
			Console.WriteLine(message);
		}

		public static void Warning(string message)
		{
			Console.ForegroundColor = ConsoleColor.Yellow;
			Console.WriteLine(message);
		}

		public static void Error(Exception ex)
		{
			Console.ForegroundColor = ConsoleColor.Red;
			Console.WriteLine(ex.ToString());
		}

		public static void NewLine()
		{
			Console.WriteLine();
		}
	}
}
