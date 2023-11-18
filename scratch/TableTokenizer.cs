﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using Token = Rolex.Token;
namespace scratch
{

	/// <summary>
	/// Reference Implementation for generated shared code
	/// </summary>
	class TableTokenizer : System.Object, IEnumerable<Token>
	{
		public const int ErrorSymbol = -1;
		// our state table
		private int[] _dfaTable;
		// our block ends (specified like comment<blockEnd="*/">="/*" in a rolex spec file)
		private int[][] _blockEnds;
		// our node flags. Currently only used for the hidden attribute
		private int[] _nodeFlags;
		// the input cursor. We can get this from a string, a char array, or some other source.
		private IEnumerable<char> _input;
		/// <summary>
		/// Retrieves an enumerator that can be used to iterate over the tokens
		/// </summary>
		/// <returns>An enumerator that can be used to iterate over the tokens</returns>
		public IEnumerator<Token> GetEnumerator()
		{
			// just create our table tokenizer's enumerator, passing all of the relevant stuff
			// it's the real workhorse.
			return new TableTokenizerEnumerator(_dfaTable, _blockEnds, _nodeFlags, _input.GetEnumerator());
		}
		// legacy collection support (required)
		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}
		/// <summary>
		/// Constructs a new instance
		/// </summary>
		/// <param name="dfaTable">The DFA state table to use</param>
		/// <param name="blockEnds">The block ends table</param>
		/// <param name="nodeFlags">The node flags table</param>
		/// <param name="input">The input character sequence</param>
		public TableTokenizer(int[] dfaTable, int[][] blockEnds, int[] nodeFlags, IEnumerable<char> input)
		{
			if (null == dfaTable)
				throw new ArgumentNullException("dfaTable");
			if (null == blockEnds)
				throw new ArgumentNullException("blockEnds");
			if (null == nodeFlags)
				throw new ArgumentNullException("nodeFlags");
			if (null == input)
				throw new ArgumentNullException("input");
			_dfaTable = dfaTable;
			_blockEnds = blockEnds;
			_nodeFlags = nodeFlags;
			_input = input;
		}
	}
	class TableTokenizerEnumerator : System.Object, IEnumerator<Token>
	{
		private int _state;
		private Token _token;
		private int _ch;
		private int[] _dfa;
		private int[][] _blockEnds;
		private int[] _nodeFlags;
		private long _position;
		private long _absIndex;
		private int _line;
		private int _column;
		private StringBuilder _capture = new StringBuilder();
		private IEnumerator<char> _inner;
		public TableTokenizerEnumerator(int[] dfa, int[][] blockEnds, int[] nodeFlags, IEnumerator<char> inner)
		{
			_position = 0;
			_line = 1;
			_column = 1;
			_absIndex = 0;
			_dfa = dfa;
			_blockEnds = blockEnds;
			_nodeFlags = nodeFlags;
			_inner = inner;
			_ch = -2;
			_token.SymbolId = -2;
			_state = -1;
		}
		public Token Current {
			get {
				if (_state == -3)
				{
					throw new ObjectDisposedException("The enumerator was disposed");
				}
				else if (_state == -2 || _state == -1)
				{
					throw new InvalidOperationException("The enumerator is not positioned on an element");
				}
				return _token;
			}
		}
		object System.Collections.IEnumerator.Current { get { return Current; } }
		
		void System.IDisposable.Dispose()
		{
			Dispose();
		}
		public void Dispose()
		{
			if (_state == -3) return;
			_inner.Dispose();
			_state = -3;
		}

		private bool _FetchNextInput()
		{
			if (!_inner.MoveNext())
			{
				_ch = -1;
				return false;
			}
			char ch1 = _inner.Current;
			if (char.IsHighSurrogate(ch1))
			{
				if (!_inner.MoveNext())
				{
					throw new IOException("The stream is not valid Unicode");
				}
				char ch2 = _inner.Current;
				if (!char.IsLowSurrogate(ch2))
				{
					throw new IOException("The stream is not valid Unicode");
				}
				++_absIndex;
				++_column;
				_ch = char.ConvertToUtf32(ch1, ch2);
			}
			else
			{
				if (ch1 == '\r')
				{
					_column = 1;
				}
				else if (ch1 == '\n')
				{
					++_line;
					_column = 1;
				}
				else if (ch1 == '\t')
				{
					_column = ((_column / 4) + 1) * 4;
				}
				else
				{
					++_column;
				}
				_ch = Convert.ToInt32(ch1);
			}
			++_position;
			++_absIndex;
			return true;
		}
		bool System.Collections.IEnumerator.MoveNext()
		{
			return MoveNext();
		}
		public bool MoveNext()
		{
			if (_state == -3)
			{
				throw new ObjectDisposedException("The enumerator was disposed");
			}
			else if (_state == -1)
			{
				_state = 0;
			}
			if (_state == 0)
			{
				while (true)
				{
					if (_ch == -1)
					{
						_state = -2;
						return false;
					}
					_Lex();

					if (_token.SymbolId > -1)
					{
						if (0 == (_nodeFlags[_token.SymbolId] & 1))
						{
							return true;

						}
					}
					else
					{
						return true;
					}
				}
			}
			return false;
		}
		void System.Collections.IEnumerator.Reset() {
			Reset();
		}
		public void Reset()
		{
			if (_state == -3)
			{
				throw new ObjectDisposedException("The enumerator was disposed");
			}
			_state = -1;
			_ch = -2;
			_position = 0;
			_absIndex = 0;
			_line = 1;
			_column = 1;
			_token.SymbolId = -2;
		}
		public long AbsoluteIndex {
			get {
				if (_state == -3)
				{
					throw new ObjectDisposedException("The enumerator was disposed");
				}
				return _absIndex;
			}
		}
		public long Position {
			get {
				if (_state == -3)
				{
					throw new ObjectDisposedException("The enumerator was disposed");
				}
				return _position;
			}
		}
		public int Line {
			get {
				if (_state == -3)
				{
					throw new ObjectDisposedException("The enumerator was disposed");
				}
				return _line;
			}
		}
		public int Column {
			get { return _column; }
		}
		bool _Lex()
		{
			int tlen;
			int tto;
			int prlen;
			int pmin;
			int pmax;
			int i;
			int j;
			int state = 0;
			int acc;
			long cursor_pos = _position;
			int line = _line;
			int column = _column;
			long absi = _absIndex;
			if (_ch == -2)
			{
				_FetchNextInput();
				if (_ch == -1)
				{
					_state = -2;
					return false;
				}
			}
			else if (_ch == -1)
			{
				_state = -2;
				return false;
			}
			_token.Position = cursor_pos;
			start_dfa:
			acc = _dfa[state];
			++state;
			tlen = _dfa[state];
			++state;
			for (i = 0; i < tlen; ++i)
			{
				tto = _dfa[state];
				++state;
				prlen = _dfa[state];
				++state;
				//if (prlen == 6) System.Diagnostics.Debugger.Break();
				for (j = 0; j < prlen; ++j)
				{
					pmin = _dfa[state];
					++state;
					pmax = _dfa[state];
					++state;
					if (_ch < pmin)
					{
						state += ((prlen - (j + 1)) * 2);
						j = prlen;
					}
					else if (_ch <= pmax)
					{
						_capture.Append(char.ConvertFromUtf32(_ch));
						_FetchNextInput();
						state = tto;
						goto start_dfa;
					}
				}
			}
			if (acc != -1)
			{
				int sym = acc;
				int[] be = _blockEnds[acc];
				if (be != null)
				{
					state = 0;
					start_be_dfa:
					acc = be[state];
					++state;
					tlen = be[state];
					++state;
					for (i = 0; i < tlen; ++i)
					{
						tto = be[state];
						++state;
						prlen = be[state];
						++state;
						for (j = 0; j < prlen; ++j)
						{
							pmin = be[state];
							++state;
							pmax = be[state];
							++state;
							if (_ch < pmin)
							{
								state += ((prlen - (j + 1)) * 2);
								j = prlen;
							}
							else if (_ch <= pmax)
							{
								_capture.Append(char.ConvertFromUtf32(_ch));
								_FetchNextInput();
								state = tto;
								goto start_be_dfa;
							}
						}
					}
					if (acc != -1)
					{
						_token.SymbolId = sym;
						_token.Value = _capture.ToString();
						_token.AbsoluteIndex = absi;
						_token.Position = cursor_pos;
						_token.Line = line;
						_token.Column = column;
						_capture.Clear();
						return true;
					}
					if (_ch == -1)
					{
						_token.SymbolId = -1;
						_token.Value = _capture.ToString();
						_token.AbsoluteIndex = absi;
						_token.Position = cursor_pos;
						_token.Line = line;
						_token.Column = column;
						_capture.Clear();
						return false;
					}
					_capture.Append(char.ConvertFromUtf32(_ch));
					_FetchNextInput();
					state = 0;
					goto start_be_dfa;
				}
				_token.SymbolId = acc;
				_token.Value = _capture.ToString();
				_token.AbsoluteIndex = absi;
				_token.Position = cursor_pos;
				_token.Line = line;
				_token.Column = column;
				_capture.Clear();
				return true;
			}
			bool fetch = _capture.Length == 0;
			if (fetch)
			{
				_capture.Append(char.ConvertFromUtf32(_ch));
			}
			_token.SymbolId = -1;
			_token.Value = _capture.ToString();
			_token.AbsoluteIndex = absi;
			_token.Position = cursor_pos;
			_token.Line = line;
			_token.Column = column;
			_capture.Clear();
			if (fetch)
			{
				_FetchNextInput();
			}
			return false;

		}
	}
}