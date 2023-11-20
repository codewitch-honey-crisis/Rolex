Imports System.IO
Imports RolexDemoVB
Module Module1

	Sub Main()
		'Dim input As String = """base foo \""bar\"" foobar  bar 123 baz -345 fubar 1foo *#( 0"""

		Using sr As StreamReader = File.OpenText("..\..\..\RolexDemo\Program.cs")
			Dim extokenizer As New RolexDemoVB.ExampleTokenizer(sr)
			For Each tok In extokenizer
				If (-1 <> tok.SymbolId) Then
					Console.WriteLine("{0}: {1} at line {2}, column {3}", tok.SymbolId, tok.Value, tok.Line, tok.Column)
				End If
			Next
		End Using
		Console.WriteLine()
		Return

	End Sub

End Module
