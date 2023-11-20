using System;
using System.IO;
using System.Collections.Generic;
using System.Text;

namespace Rolex
{
	class TextReaderEnumerator : Object, IEnumerator<char>
	{
		private int _state;
		private char _current;
		private TextReader _reader;
		internal TextReaderEnumerator(TextReader reader)
		{
			_reader = reader;
			_state = -1;
		}
		/// <summary>
		/// Gets the current character under the cursor
		/// </summary>
		/// <exception cref="ObjectDisposedException">The enumerator is disposed</exception>
		public char Current {
			get {
				if (_state == -3)
				{
					throw new ObjectDisposedException("The enumerator was disposed");
				}
				if (_state == -1 || _state == -2)
				{
					throw new InvalidOperationException("The enumerator is not positioned on an element.");
				}
				return _current;
			}
		}
		object System.Collections.IEnumerator.Current { get { return Current; } }
		/// <summary>
		/// Disposes of the enumerator
		/// </summary>
		public void Dispose()
		{
			if (_state == -3)
			{
				return;
			}
			_state = -3;
		}
		void IDisposable.Dispose()
		{
			Dispose();
		}
		/// <summary>
		/// Moves to the next element
		/// </summary>
		/// <returns>True if successful, false if no more data</returns>
		/// <exception cref="ObjectDisposedException">The enumerator was disposed</exception>
		public bool MoveNext()
		{
			if (_state == -3)
			{
				throw new ObjectDisposedException("The enumerator was disposed");
			}
			if (_state == -2)
			{
				return false;
			}
			int i = _reader.Read();
			if (i == -1)
			{
				_state = -2;
				return false;
			}
			_state = 0;
			_current = Convert.ToChar(i);
			return true;
		}
		bool System.Collections.IEnumerator.MoveNext()
		{
			return MoveNext();
		}
		/// <summary>
		/// Resets the enumerator
		/// </summary>
		/// <remarks>Not supported</remarks>
		/// <exception cref="ObjectDisposedException">The enumerator is disposed</exception>
		/// <exception cref="NotSupportedException">The operation is not supported (always throws)</exception>
		public void Reset()
		{
			if (_state == -3)
			{
				throw new ObjectDisposedException("The enumerator was disposed");
			}
			if (_state == -1)
			{
				return;
			}
			throw new NotSupportedException();
		}
		void System.Collections.IEnumerator.Reset()
		{
			Reset();
		}
	}
	/// <summary>
	/// Gets an enumerable instance over a TextReader
	/// </summary>
	class TextReaderEnumerable : Object, IEnumerable<char>
	{
		private TextReader _reader;
		private int _state;
		/// <summary>
		/// Creates a new instance
		/// </summary>
		/// <param name="reader">The TextReader</param>
		public TextReaderEnumerable(TextReader reader)
		{
			_reader = reader;
			_state = -1;
		}
		/// <summary>
		/// Gets the enumerator
		/// </summary>
		/// <remarks>This can only be called once</remarks>
		/// <returns>A new enumerator</returns>
		/// <exception cref="InvalidOperationException">The operation cannot be done more than once</exception>
		public IEnumerator<char> GetEnumerator()
		{
			if (_state != -1)
			{
				throw new InvalidOperationException("The collection cannot be enumerated more than once.");
			}
			var result = new TextReaderEnumerator(_reader);
			_state = 0;
			return result;
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}
	}
	/// <summary>
	/// Reference Implementation for generated shared code
	/// </summary>
	class TableTokenizer : System.Object, IEnumerable<Token>
	{
		/// <summary>
		/// The symbol id for an error
		/// </summary>
		public const int ErrorSymbol = -1;
		// our state table
		private int[] _dfaTable;
		// our block ends (specified like comment<blockEnd="*/">="/*" in a rolex spec file)
		private int[][] _blockEnds;
		// our node flags. Currently only used for the hidden attribute
		private int[] _nodeFlags;
		// the input cursor. We can get this from a string, a char array, or some other source.
		private IEnumerable<char> _input;
		private int _tabWidth;
		/// <summary>
		/// Indicates the width of a tab stop
		/// </summary>
		public int TabWidth {
			get { return _tabWidth; }
			set { if (_tabWidth <= 0) _tabWidth = 4; _tabWidth = value; }
		}
		/// <summary>
		/// Retrieves an enumerator that can be used to iterate over the tokens
		/// </summary>
		/// <returns>An enumerator that can be used to iterate over the tokens</returns>
		public IEnumerator<Token> GetEnumerator()
		{
			// just create our table tokenizer's enumerator, passing all of the relevant stuff
			// it's the real workhorse.
			TableTokenizerEnumerator result = new TableTokenizerEnumerator(_dfaTable, _blockEnds, _nodeFlags, _input.GetEnumerator());
			result.TabWidth = _tabWidth;
			return result;
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
	/// <summary>
	/// Enumerates tokens over a character enumerator
	/// </summary>
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
		private int _tabWidth;
		private StringBuilder _capture = new StringBuilder();
		private IEnumerator<char> _inner;
		/// <summary>
		/// Constructs a new token enumerator
		/// </summary>
		/// <param name="dfa">The DFA to use</param>
		/// <param name="blockEnds">The block end DFAs to use</param>
		/// <param name="nodeFlags">The node flags</param>
		/// <param name="inner">The character enumerator</param>
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
			_tabWidth = 4;
		}
		/// <summary>
		/// Indicates the width of the tab stops
		/// </summary>
		public int TabWidth { 
			get { return _tabWidth; }
			set { if (value <= 0) { _tabWidth = 4; } else { _tabWidth = value; } } 
		}
		/// <summary>
		/// Indicates the current token
		/// </summary>
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
		// legacy support
		object System.Collections.IEnumerator.Current { get { return Current; } }
		// framework support
		void System.IDisposable.Dispose()
		{
			Dispose();
		}
		/// <summary>
		/// Disposes of the enumerator
		/// </summary>
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
					_column = ((((_column - 1) / _tabWidth) + 1) * _tabWidth) + 1;
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
		// supports the framework
		bool System.Collections.IEnumerator.MoveNext()
		{
			return MoveNext();
		}
		/// <summary>
		/// Moves to the next token
		/// </summary>
		/// <returns>True if successful, or false if there were not any more tokens</returns>
		/// <exception cref="ObjectDisposedException">Thrown if the cobject was disposed of</exception>
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
		// supports the framework
		void System.Collections.IEnumerator.Reset() {
			Reset();
		}
		/// <summary>
		/// Resets the enumerator
		/// </summary>
		/// <exception cref="ObjectDisposedException">The enumerator was disposed</exception>
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
		/// <summary>
		/// Indicates the absolute character index of the cursor
		/// </summary>
		public long AbsoluteIndex {
			get {
				if (_state == -3)
				{
					throw new ObjectDisposedException("The enumerator was disposed");
				}
				return _absIndex;
			}
		}
		/// <summary>
		/// Indicates the position of the cursor
		/// </summary>
		public long Position {
			get {
				if (_state == -3)
				{
					throw new ObjectDisposedException("The enumerator was disposed");
				}
				return _position;
			}
		}
		/// <summary>
		/// Indicates the line of the cursor
		/// </summary>
		public int Line {
			get {
				if (_state == -3)
				{
					throw new ObjectDisposedException("The enumerator was disposed");
				}
				return _line;
			}
		}
		/// <summary>
		/// Indicates the column of the cursor
		/// </summary>
		public int Column {
			get { return _column; }
		}
		private bool _Lex()
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
			int column;
			if (_absIndex == 0)
			{
				column = _column;
			}
			else
			{
				column = _column - 1;
			}
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