using System;
using System.IO;

namespace RolexDemo
{
	class Program
	{
		static void Main(string[] args)
		{
			var input = "base foo \"bar\" foobar  bar 123 baz -345 fubar 1foo *#( 0";
			using (var sr = File.OpenText("..\\..\\Program.cs"))
				input = sr.ReadToEnd();

			var extokenizer = new ExampleTokenizer(input);
			foreach(var tok in extokenizer)
			{
				if(-1!=tok.SymbolId)
					Console.WriteLine("{0}: {1} at column {2}", tok.SymbolId, tok.Value,tok.Column);
			}
			Console.WriteLine();
			return;
			

		}
	}
}
