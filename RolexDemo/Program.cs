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
				var extokenizer = new ExampleTokenizer(sr);
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
