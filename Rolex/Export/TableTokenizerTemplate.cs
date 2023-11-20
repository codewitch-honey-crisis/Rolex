using System.IO;
using System.Collections.Generic;

namespace Rolex
{
	/// <summary>
	/// A table driven tokenizer
	/// </summary>
	class TableTokenizerTemplate : TableTokenizer
	{
		internal static int[] DfaTable;
		internal static int[] NodeFlags;
		internal static int[][] BlockEnds;
		/// <summary>
		/// Constructs a new table tokenizer
		/// </summary>
		/// <param name="input">The input character stream</param>
		public TableTokenizerTemplate(IEnumerable<char> input) :
			   base(DfaTable, BlockEnds, NodeFlags, input)
		{
		}
		/// <summary>
		/// Constructs a new table tokenizer
		/// </summary>
		/// <param name="input">The input character stream</param>
		public TableTokenizerTemplate(TextReader input) :
			   base(DfaTable, BlockEnds, NodeFlags, new TextReaderEnumerable(input))
		{
		}
	}
}
