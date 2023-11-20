using System;
using System.Collections.Generic;
using System.IO;

namespace RolexDemo
{
	class Program
	{
		static void Main(string[] args)
		{
			using (var sr = File.OpenText("..\\..\\Program.cs"))
			{
				//	input = sr.ReadToEnd();
				var input = "base foo \"bar\" foobar  bar 123 baz -345 fubar 1foo *#( 0";
				var extokenizer = new ExampleTokenizer(new TextReaderEnumerable(sr));
				foreach (var tok in extokenizer)
				{
					if (-1 != tok.SymbolId)
						Console.WriteLine("{0}: {1} at line {2}, column {3}", tok.SymbolId, tok.Value, tok.Line, tok.Column);
				}
			}
			Console.WriteLine();
			return;
			

		}
	}
}
