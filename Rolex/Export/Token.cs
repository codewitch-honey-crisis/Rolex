﻿namespace Rolex
{
	/// <summary>
	/// Reference implementation for generated shared code
	/// </summary>
	struct Token
	{
		/// <summary>
		/// Indicates the line where the token occurs
		/// </summary>
		public int Line;
		/// <summary>
		/// Indicates the column where the token occurs
		/// </summary>
		public int Column;
		/// <summary>
		/// Indicates the character index where the token occurs
		/// </summary>
		public long AbsoluteIndex;
		/// <summary>
		/// Indicates the position where the token occurs
		/// </summary>
		public long Position;
		/// <summary>
		/// Indicates the symbol id or -1 for the error symbol
		/// </summary>
		public int SymbolId;
		/// <summary>
		/// Indicates the value of the token
		/// </summary>
		public string Value;

	}
}
