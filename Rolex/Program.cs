using CD;
using LC;
using F;
using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace Rolex
{
	public class Program
	{
		static readonly string CodeBase = _GetCodeBase();
		static readonly string Filename = Path.GetFileName(CodeBase);
		static readonly string Name = _GetName();
		static int Main(string[] args)
		{
			return Run(args, Console.In, Console.Out, Console.Error);
		}
		class Reporter : IProgress<int>
		{
			TextWriter _output;
			bool _dots;
			public Reporter(TextWriter output,bool dots)
			{
				_output = output;
				_dots = dots;

			}
			public void Report(int value)
			{
				if (_dots)
				{
					_output.Write(".");
					return;
				}
				_WriteProgress(value, value != 0, _output);
			}
		}
		public static int Run(string[] args, TextReader stdin, TextWriter stdout, TextWriter stderr)
		{
			// our return code
			var result = 0;
			// app parameters
			string inputfile = null;
			string outputfile = null;
			string codeclass = null;
			string codelanguage = null;
			string codenamespace = null;
			string externaltoken = null;
			string nfagraph = null;
			string dfagraph = null;
			bool ignorecase = false;
			bool noshared = false;
			bool ifstale = false;
			bool staticprogress = false;
			// our working variables
			TextReader input = null;
			TextWriter output = null;
			try
			{
				if (0 == args.Length)
				{
					_PrintUsage(stderr);
					result = -1;
				}
				else if (args[0].StartsWith("/"))
				{
					throw new ArgumentException("Missing input file.");
				}
				else
				{
					// process the command line args
					inputfile = args[0];
					for (var i = 1; i < args.Length; ++i)
					{
						switch (args[i].ToLowerInvariant())
						{
							case "/output":
								if (args.Length - 1 == i) // check if we're at the end
									throw new ArgumentException(string.Format("The parameter \"{0}\" is missing an argument", args[i].Substring(1)));
								++i; // advance 
								outputfile = args[i];
								break;
							case "/class":
								if (args.Length - 1 == i) // check if we're at the end
									throw new ArgumentException(string.Format("The parameter \"{0}\" is missing an argument", args[i].Substring(1)));
								++i; // advance 
								codeclass = args[i];
								break;
							case "/language":
								if (args.Length - 1 == i) // check if we're at the end
									throw new ArgumentException(string.Format("The parameter \"{0}\" is missing an argument", args[i].Substring(1)));
								++i; // advance 
								codelanguage = args[i];
								break;
							case "/namespace":
								if (args.Length - 1 == i) // check if we're at the end
									throw new ArgumentException(string.Format("The parameter \"{0}\" is missing an argument", args[i].Substring(1)));
								++i; // advance 
								codenamespace = args[i];
								break;
							case "/external":
								if (args.Length - 1 == i) // check if we're at the end
									throw new ArgumentException(string.Format("The parameter \"{0}\" is missing an argument", args[i].Substring(1)));
								++i; // advance 
								externaltoken = args[i];
								break;
							case "/nfagraph":
								if (args.Length - 1 == i) // check if we're at the end
									throw new ArgumentException(string.Format("The parameter \"{0}\" is missing an argument", args[i].Substring(1)));
								++i; // advance 
								nfagraph = args[i];
								break;
							case "/dfagraph":
								if (args.Length - 1 == i) // check if we're at the end
									throw new ArgumentException(string.Format("The parameter \"{0}\" is missing an argument", args[i].Substring(1)));
								++i; // advance 
								dfagraph = args[i];
								break;
							case "/ignorecase":
								ignorecase = true;
								break;
							case "/noshared":
								noshared = true;
								break;
							case "/ifstale":
								ifstale = true;
								break;
							case "/staticprogress":
								staticprogress = true;
								break;


							default:
								throw new ArgumentException(string.Format("Unknown switch {0}", args[i]));
						}
					}
					// now build it
					if (string.IsNullOrEmpty(codeclass))
					{
						// default we want it to be named after the code file
						// otherwise we'll use inputfile
						if (null != outputfile)
							codeclass = Path.GetFileNameWithoutExtension(outputfile);
						else
							codeclass = Path.GetFileNameWithoutExtension(inputfile);
					}
					if (string.IsNullOrEmpty(codelanguage))
					{
						if (!string.IsNullOrEmpty(outputfile))
						{
							codelanguage = Path.GetExtension(outputfile);
							if (codelanguage.StartsWith("."))
								codelanguage = codelanguage.Substring(1);
						}
						if (string.IsNullOrEmpty(codelanguage))
							codelanguage = "cs";
					}
					var stale = true;
					if (ifstale && null != outputfile)
					{
						stale = _IsStale(inputfile, outputfile);
						if (!stale)
							stale = _IsStale(CodeBase, outputfile);
					}
					if (!stale)
					{
						stderr.WriteLine("{0} skipped building {1} because it was not stale.", Name, outputfile);
					}
					else
					{
						if (null != outputfile)
							stderr.WriteLine("{0} is building file: {1}", Name, outputfile);
						else
							stderr.WriteLine("{0} is building tokenizer.", Name);
						input = new StreamReader(inputfile);
						var rules = new List<LexRule>();
						string line;
						while(null!=(line=input.ReadLine()))
						{
							var lc = LexContext.Create(line);
							lc.TrySkipCCommentsAndWhiteSpace();
							if(-1!=lc.Current)
								rules.Add(LexRule.Parse(lc));
						}
						input.Close();
						input = null;
						LexRule.FillRuleIds(rules);

						var ccu = new CodeCompileUnit();
						var cns = new CodeNamespace();
						if (!string.IsNullOrEmpty(codenamespace))
							cns.Name = codenamespace;
						ccu.Namespaces.Add(cns);
						var symmap = new Dictionary<int, string>();
						for(int i = 0;i<rules.Count;++i)
						{
							symmap.Add(rules[i].Id, rules[i].Expression);
						}
						var fa = _BuildLexer(rules, ignorecase,inputfile,true,staticprogress, stderr);
						var symbolTable = _BuildSymbolTable(rules);
						var symids = new int[symbolTable.Length];
						for (var i = 0; i < symbolTable.Length; ++i)
							symids[i] = i;
						var blockEnds = _BuildBlockEnds(rules,ignorecase,inputfile);
						var nodeFlags = _BuildNodeFlags(rules);
						if (null != nfagraph)
						{
							var fa2 = _BuildLexer(rules, ignorecase, inputfile, false,staticprogress,TextWriter.Null);
							fa2.RenderToFile(nfagraph);
						}
						stderr.Write("Converting to DFA ");
						
						fa = fa.ToDfa(new Reporter(stderr,staticprogress));
						stderr.WriteLine(" Done!");
						if(null!=dfagraph)
						{
							fa.RenderToFile(dfagraph);
						}
						int[] dfaTable = _ToDfaStateTable(fa,symids);
						if (!noshared)
						{
							if (string.IsNullOrEmpty(externaltoken))
							{
								// import our Export/Token.cs into the library
								_ImportCompileUnit(Deslanged.Token, cns);
							}

							// import our Export/TableTokenizer.cs into the library
							_ImportCompileUnit(Deslanged.TableTokenizer, cns);

						}
						if(!string.IsNullOrEmpty(externaltoken))
							cns.Imports.Add(new CodeNamespaceImport(externaltoken));
						var origName = "Rolex.";
						CodeTypeDeclaration td = null;
						if (null == td)
						{
							td = Deslanged.TableTokenizerTemplate.Namespaces[1].Types[0];
							origName += td.Name;
							td.Name = codeclass;
							_GenerateSymbolConstants(td, symmap, symbolTable);
						}
						CodeDomVisitor.Visit(td, (ctx) =>
						{
							var tr = ctx.Target as CodeTypeReference;
							if (null != tr)
							{
								if (0 == string.Compare(origName, tr.BaseType, StringComparison.InvariantCulture))
									tr.BaseType = codeclass;
							}

						});
						CodeMemberField f = null;
						
						f = CodeDomUtility.GetByName("DfaTable", td.Members) as CodeMemberField;
						f.InitExpression = CodeDomUtility.Literal(dfaTable);
						f = CodeDomUtility.GetByName("NodeFlags", td.Members) as CodeMemberField;
						f.InitExpression = CodeDomUtility.Literal(nodeFlags);
						f = CodeDomUtility.GetByName("BlockEnds", td.Members) as CodeMemberField;
						f.InitExpression = CodeDomUtility.Literal(blockEnds);

						cns.Types.Add(td);

						var hasColNS = false;
						foreach (CodeNamespaceImport nsi in cns.Imports)
						{
							if (0 == string.Compare(nsi.Namespace, "System.Collections.Generic", StringComparison.InvariantCulture))
							{
								hasColNS = true;
								break;
							}
						}
						if (!hasColNS)
							cns.Imports.Add(new CodeNamespaceImport("System.Collections.Generic"));
						
						stderr.WriteLine();
						var prov = CodeDomProvider.CreateProvider(codelanguage);
						var opts = new CodeGeneratorOptions();
						opts.BlankLinesBetweenMembers = false;
						opts.VerbatimOrder = true;
						if (null == outputfile)
							output = stdout;
						else
						{
							// open the file and truncate it if necessary
							var stm = File.Open(outputfile, FileMode.Create);
							stm.SetLength(0);
							output = new StreamWriter(stm);
						}
						prov.GenerateCodeFromCompileUnit(ccu, output, opts);
					}
				}
			}
			// we don't like to catch in debug mode
#if !DEBUG
			catch (Exception ex)
			{
				result = _ReportError(ex, stderr);
			}
#endif
			finally
			{
				// close the input file if necessary
				if (null != input)
					input.Close();
				// close the output file if necessary
				if (null != outputfile && null != output)
					output.Close();
			}
			return result;
		}
		static bool _IsStale(string inputfile, string outputfile)
		{
			var result = true;
			// File.Exists doesn't always work right
			try
			{
				if (File.GetLastWriteTimeUtc(outputfile) >= File.GetLastWriteTimeUtc(inputfile))
					result = false;
			}
			catch { }
			return result;
		}
		static string _MakeSafeName(string name)
		{
			var sb = new StringBuilder();
			if (char.IsDigit(name[0]))
				sb.Append('_');
			for (var i = 0; i < name.Length; ++i)
			{
				var ch = name[i];
				if ('_' == ch || char.IsLetterOrDigit(ch))
					sb.Append(ch);
				else
					sb.Append('_');
			}
			return sb.ToString();
		}
		static string _MakeUniqueMember(CodeTypeDeclaration decl, string name)
		{
			var seen = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
			for (int ic = decl.Members.Count, i = 0; i < ic; i++)
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
		private static void _GenerateSymbolConstants(CodeTypeDeclaration target, IDictionary<int,string> map, IList<string> symbolTable)
		{
			// generate symbol constants
			for (int ic = symbolTable.Count, i = 0; i < ic; ++i)
			{
				var symbol = symbolTable[i];
				if (null != symbol)
				{
					var s = _MakeSafeName(symbol);
					s = _MakeUniqueMember(target, s);
					var constField = CD.CodeDomUtility.Field(typeof(int), s, MemberAttributes.Const | MemberAttributes.Public, CD.CodeDomUtility.Literal(i));
					constField.Comments.AddRange(new CodeCommentStatement[] {
						new CodeCommentStatement("<summary>Matches "+map[i]+"</summary>",true)
					});
					target.Members.Add(constField);
				}
			}
		}

		private static readonly CodeAttributeDeclaration _GeneratedCodeAttribute
			= new CodeAttributeDeclaration(CD.CodeDomUtility.Type(typeof(GeneratedCodeAttribute)), new CodeAttributeArgument(CD.CodeDomUtility.Literal("Rolex")), new CodeAttributeArgument(CD.CodeDomUtility.Literal(Assembly.GetExecutingAssembly().GetName().Version.ToString())));
		private static void _ImportCompileUnit(CodeCompileUnit fromCcu, CodeNamespace dst)
		{
			CD.CodeDomVisitor.Visit(fromCcu, (ctx) =>
			{
				var ctr = ctx.Target as CodeTypeReference;
				if (null != ctr)
				{
					if (ctr.BaseType.StartsWith("Rolex."))
						ctr.BaseType = ctr.BaseType.Substring(6);
				}
			});
			// import all the usings and all the types
			foreach (CodeNamespace ns in fromCcu.Namespaces)
			{
				foreach (CodeNamespaceImport nsi in ns.Imports)
				{
					var found = false;
					foreach (CodeNamespaceImport nsicmp in dst.Imports)
					{
						if (0 == string.Compare(nsicmp.Namespace, nsi.Namespace, StringComparison.InvariantCulture))
						{
							found = true;
							break;
						}
					}
					if (!found)
						dst.Imports.Add(nsi);
				}
				foreach (CodeTypeDeclaration type in ns.Types)
				{
					type.CustomAttributes.Add(_GeneratedCodeAttribute);
					dst.Types.Add(type);
				}
			}
		}
		const char _block = '■';
		const string _back = "\b\b\b\b\b\b\b\b\b\b\b\b\b\b\b\b\b";
		const string _twirl = "-\\|/";
		public static void _WriteProgressBar(int percent, bool update,TextWriter output)
		{
			if (update)
				output.Write(_back);
			output.Write("[");
			var p = (int)((percent / 10f) + .5f);
			for (var i = 0; i < 10; ++i)
			{
				if (i >= p)
					output.Write(' ');
				else
					output.Write(_block);
			}
			output.Write("] {0,3:##0}%", percent);
		}
		public static void _WriteProgress(int progress, bool update, TextWriter output)
		{
		 	 if (update)
				 output.Write("\b");
			 output.Write(_twirl[progress % _twirl.Length]);
		}
#if !DEBUG
		// do our error handling here (release builds)
		static int _ReportError(Exception ex, TextWriter stderr)
		{
			//_PrintUsage(stderr);
			stderr.WriteLine("Error: {0}", ex.Message);
			return -1;
		}
#endif
		static void _PrintUsage(TextWriter w)
		{
			w.Write("Usage: "+Filename + " ");
			w.WriteLine("<inputfile> [/output <outputfile>] [/class <codeclass>] [/namespace <codenamespace>]");
			w.WriteLine("   [/language <codelanguage> [/external <externaltoken>] [/ignorecase] [/noshared]");
			w.WriteLine("   [/ifstale] [/nfagraph <filename>] [/dfagraph <filename>]");
			w.WriteLine();
			w.WriteLine(Name + " generates a lexer/scanner/tokenizer in the target .NET language");
			w.WriteLine();
			w.WriteLine("   <inputfile>      The input lexer specification");
			w.WriteLine("   <outputfile>     The output source file - defaults to STDOUT");
			w.WriteLine("   <codeclass>      The name of the main class to generate - default derived from <outputfile>");
			w.WriteLine("   <codenamespace>  The namespace to generate the code under - defaults to none");
			w.WriteLine("   <codelanguage>   The .NET language to generate the code in - default derived from <outputfile>");
			w.WriteLine("   <externaltoken>  The namespace of the external token if one is to be used - default not external");
			w.WriteLine("   <ignorecase>     Create a case insensitive lexer - defaults to case sensitive");
			w.WriteLine("   <noshared>       Do not generate the shared code as part of the output. Defaults to generating the shared code");
			w.WriteLine("   <ifstale>        Only generate if the input is newer than the output");
			w.WriteLine("   <staticprogress> Do not use dynamic console features for progress indicators");
			w.WriteLine("   <nfagraph>       Write the NFA lexer graph to the specified image file.*");
			w.WriteLine("   <dfagraph>       Write the DFA lexer graph to the specified image file.*");
			w.WriteLine();
			w.WriteLine("   * Requires GraphViz to be installed and in the PATH");
			w.WriteLine();
		}
		static string _GetCodeBase()
		{
			try
			{
				return Assembly.GetExecutingAssembly().GetModules()[0].FullyQualifiedName;
			}
			catch
			{
				return Path.Combine(Environment.CurrentDirectory,"rolex.exe");
			}
		}
		static string _GetName()
		{
			try
			{
				foreach (var attr in Assembly.GetExecutingAssembly().CustomAttributes)
				{
					if (typeof(AssemblyTitleAttribute) == attr.AttributeType)
					{
						return attr.ConstructorArguments[0].Value as string;
					}
				}
			}
			catch { }
			return Path.GetFileNameWithoutExtension(Filename);
		}
		static string[] _BuildSymbolTable(IList<LexRule> rules)
		{
			int max = int.MinValue;
			for (int ic = rules.Count, i = 0; i < ic; ++i)
			{
				var rule = rules[i];
				if (rule.Id > max)
					max = rule.Id;
			}
			var result = new string[max + 1];
			for (int ic = rules.Count, i = 0; i < ic; ++i)
			{
				var rule = rules[i];
				result[rule.Id] = rule.Symbol;
			}
			return result;
		}
		static FFA ParseToFA(LexRule rule,bool ignoreCase, string filename)
		{
			FFA fa;
			if (rule.Expression.StartsWith("\""))
			{
				var pc = LexContext.Create(rule.Expression);
				fa = FFA.Literal(FFA.ToUtf32(pc.ParseJsonString()), rule.Id);
			}
			else
				fa = FFA.Parse(rule.Expression.Substring(1, rule.Expression.Length - 2), 0, rule.ExpressionLine, rule.ExpressionColumn, rule.ExpressionPosition,filename);
			if (!ignoreCase)
			{
				var ic = (bool)rule.GetAttribute("ignoreCase", false);
				if (ic)
					fa = FFA.CaseInsensitive(fa, rule.Id);
			}
			else
			{
				var ic = (bool)rule.GetAttribute("ignoreCase", true);
				if (ic)
					fa = FFA.CaseInsensitive(fa, rule.Id);
			}
			return fa;
		}
		static int[][] _BuildBlockEnds(IList<LexRule> rules,bool ignorecase, string filename)
		{
			int max = int.MinValue;
			for (int ic = rules.Count, i = 0; i < ic; ++i)
			{
				var rule = rules[i];
				if (rule.Id > max)
					max = rule.Id;
			}
			var result = new int[max + 1][];
			for (int ic = rules.Count, i = 0; i < ic; ++i)
			{
				var ci = ignorecase;
				var rule = rules[i];
				var ica = rule.GetAttribute("ignoreCase");
				if (null != ica && ica is bool)
				{
					ci = (bool)ica;
				}
				var v = rule.GetAttribute("blockEnd");
				var be = v as string;
				if (!string.IsNullOrEmpty(be))
				{
					var cfa = FFA.Literal(FFA.ToUtf32(be), 0);
					if (ci)
						cfa = FFA.CaseInsensitive(cfa, 0);
					cfa = cfa.ToMinimized();
					result[rule.Id] = cfa.ToDfaTable();
				}
				else
				{
					var lr = v as LexRule;
					if (null != lr)
					{
						var fa = ParseToFA(lr, ci, filename);
					
						fa = fa.ToMinimized();
						result[rule.Id] = fa.ToDfaTable();
					}
				}
			}
			return result;
		}
		static int[] _BuildNodeFlags(IList<LexRule> rules)
		{
			int max = int.MinValue;
			for (int ic = rules.Count, i = 0; i < ic; ++i)
			{
				var rule = rules[i];
				if (rule.Id > max)
					max = rule.Id;
			}
			var result = new int[max + 1];
			for (int ic = rules.Count, i = 0; i < ic; ++i)
			{
				var rule = rules[i];
				var hidden = rule.GetAttribute("hidden");
				if ((hidden is bool) && (bool)hidden)
					result[rule.Id] = 1;
			}
			return result;
		}
		static FFA _BuildLexer(IList<LexRule> rules, bool ignoreCase,string inputFile, bool minimized, bool dots,TextWriter output)
		{
			output.Write("Building lexer ");
			if (!dots)
			{
				_WriteProgressBar(0, false, output);
			}
			var exprs = new FFA[rules.Count];
			var result = new FFA();
			for (var i = 0; i < exprs.Length; ++i)
			{
				var rule = rules[i];
				FFA fa;
				if(rule.Expression.StartsWith("\""))
				{
					var pc = LexContext.Create(rule.Expression);
					fa = FFA.Literal(FFA.ToUtf32(pc.ParseJsonString()),rule.Id);
				} else
					fa = FFA.Parse(rule.Expression.Substring(1, rule.Expression.Length - 2), rule.Id, rule.ExpressionLine, rule.ExpressionColumn, rule.ExpressionPosition, inputFile);
				if (0 > rule.Id)
				{
					throw new InvalidOperationException(string.Format("A rule id was less than zero at line {0}", rule.ExpressionLine));
				}
				if (!ignoreCase)
				{
					var ic = (bool)rule.GetAttribute("ignoreCase", false);
					if (ic)
						fa = FFA.CaseInsensitive(fa, rule.Id);
				}
				else
				{
					var ic = (bool)rule.GetAttribute("ignoreCase", true);
					if (ic)
						fa = FFA.CaseInsensitive(fa, rule.Id);
				}
				result.AddEpsilon(minimized?fa.ToMinimized():fa);
				if (dots)
				{
					output.Write('.');
				}
				else
				{
					_WriteProgressBar((int)(((double)i / (double)exprs.Length) * 100), true, output);
				}
			}
			if (!dots)
			{
				_WriteProgressBar(100, true, output);
			}
			output.WriteLine(" Done!");
			return result;
		}
		
		static int[] _ToDfaStateTable(FFA dfa, IList<int> symbolTable = null)
		{
			var closure = dfa.FillClosure();
			var symbolLookup = new Dictionary<int, int>();
			// if we don't have a symbol table, build 
			// the symbol lookup from the states.
			if (null == symbolTable)
			{
				// go through each state, looking for accept symbols
				// and then add them to the new symbol table is we
				// haven't already
				var i = 0;
				for (int jc = closure.Count, j = 0; j < jc; ++j)
				{
					var fa = closure[j];
					if (fa.IsAccepting && !symbolLookup.ContainsKey(fa.AcceptSymbol))
					{
						if (0 > fa.AcceptSymbol)
							throw new InvalidOperationException("An accept symbol was never specified for state q" + jc.ToString());
						symbolLookup.Add(fa.AcceptSymbol, i);
						++i;
					}
				}
			}
			else // build the symbol lookup from the symbol table
				for (int ic = symbolTable.Count, i = 0; i < ic; ++i)
				{
					symbolLookup.Add(symbolTable[i], i);
				}

			// build the root array
			return dfa.ToDfaTable();
		}
	}


}
