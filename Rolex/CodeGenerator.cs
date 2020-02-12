// This file handles all of the ugly details of generating our classes
// It's rather long, but most of it generates static code
// See the reference implementation in Tokenizer.cs for the
// code this generates
using System.CodeDom;
using System.Collections.Generic;
using System.Text;
using System;
using System.CodeDom.Compiler;
using System.Reflection;
using F;
namespace Rolex
{
	using C = CD.CodeDomUtility;
	static class CodeGenerator
	{
		const int _ErrorSymbol = -1;
		const int _EosSymbol = -2;
		const int _Disposed = -4;
		const int _BeforeBegin = -3;
		const int _AfterEnd = -2;
		const int _InnerFinished = -1;
		const int _Enumerating = 0;
		const int _TabWidth = 4;

		
		
		static string _MakeSafeName(string name)
		{
			var sb = new StringBuilder();
			if (char.IsDigit(name[0]))
				sb.Append('_');
			for(var i = 0;i<name.Length;++i)
			{
				var ch = name[i];
				if ('_' == ch || char.IsLetterOrDigit(ch))
					sb.Append(ch);
				else
					sb.Append('_');
			}
			return sb.ToString();
		}
		static string _MakeUniqueMember(CodeTypeDeclaration decl,string name)
		{
			var seen = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
			for(int ic=decl.Members.Count,i = 0;i<ic;i++)
				seen.Add(decl.Members[i].Name);
			var result = name;
			var suffix = 2;
			while (seen.Contains(result))
			{
				result = string.Concat(name, suffix.ToString());
				++suffix;
			}
			return result;
		}
		
		public static void GenerateSymbolConstants(CodeTypeDeclaration target,IList<string> symbolTable)
		{
			/*var e = _MakeUniqueMember(target, "ErrorSymbol");
			var errField = C.Field(typeof(int), e,MemberAttributes.Const | MemberAttributes.Public,C.Literal(_ErrorSymbol));
			target.Members.Add(errField);*/
			// generate symbol constants
			for (int ic = symbolTable.Count, i = 0; i < ic; ++i)
			{
				var symbol = symbolTable[i];
				if (null != symbol)
				{
					var s = _MakeSafeName(symbol);
					s = _MakeUniqueMember(target, s);
					var constField = C.Field(typeof(int), s,MemberAttributes.Const | MemberAttributes.Public,C.Literal(i));
					target.Members.Add(constField);
				}
			}
		}
		
		// we use our own serialization here to avoid the codedom trying to reference the DfaEntry under the wrong namespace
		public static CodeExpression GenerateDfaTableInitializer(DfaEntry[] dfaTable)
		{
			var result = new CodeArrayCreateExpression("DfaEntry");
			for(var i = 0;i<dfaTable.Length;i++)
			{
				var entry = new CodeObjectCreateExpression("DfaEntry");
				var transitions = new CodeArrayCreateExpression("DfaTransitionEntry");
				var de = dfaTable[i];
				var trns = de.Transitions;
				for (var j = 0; j < trns.Length; j++)
				{
					var transition = new CodeObjectCreateExpression(transitions.CreateType);
					var ranges = new CodeArrayCreateExpression(typeof(int));
					var trn = trns[j];
					var rngs = trn.PackedRanges;
					for (var k=0;k<rngs.Length;k++)
						ranges.Initializers.Add(new CodePrimitiveExpression(rngs[k]));
					transition.Parameters.Add(ranges);
					transition.Parameters.Add(new CodePrimitiveExpression(trn.Destination));
					transitions.Initializers.Add(transition);
				}
				entry.Parameters.Add(transitions);
				entry.Parameters.Add(new CodePrimitiveExpression(de.AcceptSymbolId));
				result.Initializers.Add(entry);
			}
			return result;
		}

		
		// generates an "NFA" table from a dfa state table, primarily for testing
		public static CodeExpression GenerateNfaTableInitializer(DfaEntry[] dfaTable)
		{
			var result = new CodeArrayCreateExpression("NfaEntry");
			for (var i = 0; i < dfaTable.Length; i++)
			{
				var entry = new CodeObjectCreateExpression("NfaEntry");
				var transitions = new CodeArrayCreateExpression("NfaTransitionEntry");
				var ne = dfaTable[i];
				var trns = ne.Transitions;
				for (var j = 0; j < trns.Length; j++)
				{
					var transition = new CodeObjectCreateExpression(transitions.CreateType);
					var ranges = new CodeArrayCreateExpression(typeof(int));
					var trn = trns[j];
					var rngs = trn.PackedRanges;
					for (var k = 0; k < rngs.Length; k++)
						ranges.Initializers.Add(new CodePrimitiveExpression(rngs[k]));
					transition.Parameters.Add(ranges);
					transition.Parameters.Add(new CodePrimitiveExpression(trn.Destination));
					transitions.Initializers.Add(transition);
				}
				entry.Parameters.Add(new CodePrimitiveExpression(ne.AcceptSymbolId));
				entry.Parameters.Add(transitions);
				var etrns = new CodeArrayCreateExpression(typeof(int[]));
				entry.Parameters.Add(etrns);
				result.Initializers.Add(entry);
			}
			return result;
		}

		public static readonly CodeAttributeDeclaration GeneratedCodeAttribute
			= new CodeAttributeDeclaration(C.Type(typeof(GeneratedCodeAttribute)), new CodeAttributeArgument(C.Literal("Rolex")), new CodeAttributeArgument(C.Literal(Assembly.GetExecutingAssembly().GetName().Version.ToString())));
	}
}
