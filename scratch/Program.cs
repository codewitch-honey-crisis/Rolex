using System;
using F;

namespace Rolex
{
	class Program
	{
		static void Main(string[] args)
		{
			var str = "/*test*/-12.32 false foo-/*bar-123=*/abc456";
			var tokenizer = new ExampleTokenizer(str);
				//new scratch.TableTokenizer(ExampleTokenizer.DfaTable,ExampleTokenizer.BlockEnds,ExampleTokenizer.NodeFlags, str);
			FFA.FromDfaTable(ExampleTokenizer.DfaTable).RenderToFile("dfa.jpg");
			foreach(var t in tokenizer)
			{
				Console.WriteLine("{0} {1} {2}",t.Position,  t.SymbolId, t.Value);
			}
		}
	}

}
