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
		
		public static readonly CodeAttributeDeclaration GeneratedCodeAttribute
			= new CodeAttributeDeclaration(C.Type(typeof(GeneratedCodeAttribute)), new CodeAttributeArgument(C.Literal("Rolex")), new CodeAttributeArgument(C.Literal(Assembly.GetExecutingAssembly().GetName().Version.ToString())));
	}
}
