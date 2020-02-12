Imports System.IO
Imports RolexDemoVB
Module Module1

	Sub Main()
		Dim input As String = """base foo \""bar\"" foobar  bar 123 baz -345 fubar 1foo *#( 0"""

		Using sr As StreamReader = File.OpenText("..\..\..\RolexDemo\Program.cs")
			input = sr.ReadToEnd
		End Using
		Dim extokenizer As New RolexDemoVB.ExampleTokenizer(input)
		For Each tok In extokenizer
			If (-1 <> tok.SymbolId) Then
				Console.WriteLine("{0}: {1} at column {2}", tok.SymbolId, tok.Value, tok.Column)
			End If
		Next
		Console.WriteLine()
		Return

	End Sub

End Module
