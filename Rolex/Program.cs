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

using System.Diagnostics;

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
				if (_dots && 0==(value % 10))
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
			string graph = null;
			bool ignorecase = false;
			bool noshared = false;
			bool ifstale = false;
			bool staticprogress = false;
			int dpi = 0;
			// our working variables
			TextReader input = null;
			TextWriter output = null;
#if !DEBUG
			bool parsedArgs = false;
#endif
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
							case "/graph":
								if (args.Length - 1 == i) // check if we're at the end
									throw new ArgumentException(string.Format("The parameter \"{0}\" is missing an argument", args[i].Substring(1)));
								++i; // advance 
								graph = args[i];
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
							case "/dpi":
								if (args.Length - 1 == i) // check if we're at the end
									throw new ArgumentException(string.Format("The parameter \"{0}\" is missing an argument", args[i].Substring(1)));
								++i; // advance 
								dpi = int.Parse(args[i], System.Globalization.CultureInfo.InvariantCulture.NumberFormat);
								break;
							default:
								throw new ArgumentException(string.Format("Unknown switch {0}", args[i]));
						}
					}
					if (dpi!=0 &&( graph == null && dfagraph == null && nfagraph==null))
					{
						throw new ArgumentException("<dpi> was specified but no GraphViz graph was indicated.", "/dpi");
					}
#if !DEBUG
					parsedArgs = true;
#endif
					var dotopts = new FFA.DotGraphOptions();
					if(dpi!=0)
					{
						dotopts.Dpi = dpi;
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
						rules.Sort(new Comparison<LexRule>((LexRule lhs, LexRule rhs)=>{
							int cmp = lhs.Id.CompareTo(rhs.Id);
							return cmp;
						}));
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
						FFA[] lexerFas;
						var fa = _BuildLexer(rules, ignorecase,inputfile,true,staticprogress, stderr, out lexerFas);
						var symbolTable = _BuildSymbolTable(rules);
						var symids = new int[symbolTable.Length];
						for (var i = 0; i < symbolTable.Length; ++i)
							symids[i] = i;
						var blockEnds = _BuildBlockEnds(rules,ignorecase,inputfile);
						var nodeFlags = _BuildNodeFlags(rules);
						if (null != nfagraph)
						{
							FFA[] tmpfas;
							var fa2 = _BuildLexer(rules, ignorecase, inputfile, false,staticprogress,TextWriter.Null,out tmpfas);
							fa2.RenderToFile(nfagraph,dotopts);
						}
						stderr.Write("Converting to DFA ");
						
						fa = fa.ToDfa(new Reporter(stderr,staticprogress));
						stderr.WriteLine(" Done!");
						if(null!=dfagraph)
						{
							fa.RenderToFile(dfagraph);
						}
						if(null!=graph)
						{
							_RenderDotToFile(inputfile,graph, rules,lexerFas, blockEnds,dotopts);
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

						} else
						{
							cns.Imports.Add(new CodeNamespaceImport("System.IO"));
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
				if (parsedArgs)
				{
					result = -1;
				}
				else
				{
					result = _ReportError(ex, stderr);
				}
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
			StringBuilder sb;
			if (char.IsDigit(name[0]))
			{
				sb = new StringBuilder(name.Length+1);
				sb.Append('_');
			} else
			{
				 sb = new StringBuilder(name.Length);
			}
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
			w.WriteLine("<inputfile> [/output <outputfile>] [/class <codeclass>]");
			w.WriteLine("   [/namespace <codenamespace>] [/language <codelanguage> [/external <externaltoken>]");
			w.WriteLine("   [/ignorecase] [/noshared] [/ifstale] [/nfagraph <dfafile>] [/dfagraph <nfafile>]");
			w.WriteLine("   [/graph <graphfile>] [/dpi <dpi>]");
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
			w.WriteLine("   <noshared>       Do not generate the shared code as part of the output - defaults to generating the shared code");
			w.WriteLine("   <ifstale>        Only generate if the input is newer than the output");
			w.WriteLine("   <staticprogress> Do not use dynamic console features for progress indicators");
			w.WriteLine("   <nfafile>        Write the NFA lexer graph to the specified image file.*");
			w.WriteLine("   <dfafile>        Write the DFA lexer graph to the specified image file.*");
			w.WriteLine("   <graphfile>      Write all the individual rule DFAs to a graph.*");
			w.WriteLine("   <dpi>            The DPI of any outputed graphs - defaults to 300.*");
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
		static FFA ParseToFA(int id, LexRule rule,bool ignoreCase, string filename)
		{
			FFA fa;
			if (rule.Expression.StartsWith("\""))
			{
				var pc = LexContext.Create(rule.Expression);
				fa = FFA.Literal(FFA.ToUtf32(pc.ParseJsonString()), id);
			}
			else
				fa = FFA.Parse(rule.Expression.Substring(1, rule.Expression.Length - 2), id, rule.ExpressionLine, rule.ExpressionColumn, rule.ExpressionPosition,filename);
			if (!ignoreCase)
			{
				var ic = (bool)rule.GetAttribute("ignoreCase", false);
				if (ic)
					fa = FFA.CaseInsensitive(fa, id);
			}
			else
			{
				var ic = (bool)rule.GetAttribute("ignoreCase", true);
				if (ic)
					fa = FFA.CaseInsensitive(fa, id);
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
					var cfa = FFA.Literal(FFA.ToUtf32(be), rule.Id);
					if (ci)
						cfa = FFA.CaseInsensitive(cfa, rule.Id);
					cfa = cfa.ToMinimized();
					result[rule.Id] = cfa.ToDfaTable();
				}
				else
				{
					var lr = v as LexRule;
					if (null != lr)
					{
						var fa = ParseToFA(rule.Id,lr, ci, filename);
					
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
		static FFA _BuildLexer(IList<LexRule> rules, bool ignoreCase,string inputFile, bool minimized, bool dots,TextWriter output, out FFA[] exprs)
		{
			output.Write("Building lexer ");
			if (!dots)
			{
				_WriteProgressBar(0, false, output);
			}
			exprs = new FFA[rules.Count];
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
				exprs[i] = fa;
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
		static void _AppendRangeTo(StringBuilder builder, int[] ranges, int index)
		{
			var first = ranges[index];
			var last = ranges[index + 1];
			_AppendRangeCharTo(builder, first);
			if (0 == last.CompareTo(first)) return;
			if (last == first + 1) // spit out 1 length ranges as two chars
			{
				_AppendRangeCharTo(builder, last);
				return;
			}
			builder.Append('-');
			_AppendRangeCharTo(builder, last);
		}
		static void _AppendRangeCharTo(StringBuilder builder, int rangeChar)
		{
			switch (rangeChar)
			{
				case '.':
				case '[':
				case ']':
				case '^':
				case '-':
				case '\\':
					builder.Append('\\');
					builder.Append(char.ConvertFromUtf32(rangeChar));
					return;
				case '\t':
					builder.Append("\\t");
					return;
				case '\n':
					builder.Append("\\n");
					return;
				case '\r':
					builder.Append("\\r");
					return;
				case '\0':
					builder.Append("\\0");
					return;
				case '\f':
					builder.Append("\\f");
					return;
				case '\v':
					builder.Append("\\v");
					return;
				case '\b':
					builder.Append("\\b");
					return;
				default:
					var s = char.ConvertFromUtf32(rangeChar);
					if (!char.IsLetterOrDigit(s, 0) && !char.IsSeparator(s, 0) && !char.IsPunctuation(s, 0) && !char.IsSymbol(s, 0))
					{
						if (s.Length == 1)
						{
							builder.Append("\\u");
							builder.Append(unchecked((ushort)rangeChar).ToString("x4"));
						}
						else
						{
							builder.Append("\\U");
							builder.Append(rangeChar.ToString("x8"));
						}

					}
					else
						builder.Append(s);
					break;
			}
		}
		static KeyValuePair<int, int>[] _ToPairs(int[] packedRanges)
		{
			var result = new KeyValuePair<int, int>[packedRanges.Length / 2];
			for (var i = 0; i < result.Length; ++i)
			{
				var j = i * 2;
				result[i] = new KeyValuePair<int, int>(packedRanges[j], packedRanges[j + 1]);
			}
			return result;
		}
		static IEnumerable<KeyValuePair<int, int>> _NotRanges(IEnumerable<KeyValuePair<int, int>> ranges)
		{
			// expects ranges to be normalized
			var last = 0x10ffff;
			using (var e = ranges.GetEnumerator())
			{
				if (!e.MoveNext())
				{
					yield return new KeyValuePair<int, int>(0x0, 0x10ffff);
					yield break;
				}
				if (e.Current.Key > 0)
				{
					yield return new KeyValuePair<int, int>(0, unchecked(e.Current.Key - 1));
					last = e.Current.Value;
					if (0x10ffff <= last)
						yield break;
				}
				else if (e.Current.Key == 0)
				{
					last = e.Current.Value;
					if (0x10ffff <= last)
						yield break;
				}
				while (e.MoveNext())
				{
					if (0x10ffff <= last)
						yield break;
					if (unchecked(last + 1) < e.Current.Key)
						yield return new KeyValuePair<int, int>(unchecked(last + 1), unchecked((e.Current.Key - 1)));
					last = e.Current.Value;
				}
				if (0x10ffff > last)
					yield return new KeyValuePair<int, int>(unchecked((last + 1)), 0x10ffff);

			}

		}
		static int[] _FromPairs(IList<KeyValuePair<int, int>> pairs)
		{
			var result = new int[pairs.Count * 2];
			for (int ic = pairs.Count, i = 0; i < ic; ++i)
			{
				var pair = pairs[i];
				var j = i * 2;
				result[j] = pair.Key;
				result[j + 1] = pair.Value;
			}
			return result;
		}
		static string _EscapeLabel(string label)
		{
			if (string.IsNullOrEmpty(label)) return label;

			string result = label.Replace("\\", @"\\");
			result = result.Replace("\"", "\\\"");
			result = result.Replace("\n", "\\n");
			result = result.Replace("\r", "\\r");
			result = result.Replace("\0", "\\0");
			result = result.Replace("\v", "\\v");
			result = result.Replace("\t", "\\t");
			result = result.Replace("\f", "\\f");
			return result;
		}
		static void _RenderFsmTo(FFA fa, string name, int startingIndex, string spfx, bool hideAccept, bool hideAcceptingId, TextWriter writer)
		{
			var closure = fa.FillClosure();
			var finals = new List<FFA>();
			var accepting = FFA.FillAcceptingStates(closure);
			foreach (var ffa in closure)
				if (ffa.IsFinal && !ffa.IsAccepting)
					finals.Add(ffa);
			int i = 0;
			foreach (var ffa in closure)
			{
				if (!finals.Contains(ffa))
				{
					if (ffa.IsAccepting)
						accepting.Add(ffa);
				}
				var rngGrps = ffa.FillInputTransitionRangesGroupedByState();
				foreach (var rngGrp in rngGrps)
				{
					var di = closure.IndexOf(rngGrp.Key);
					writer.Write(name);
					writer.Write(i);
					writer.Write("->");
					writer.Write(name);
					writer.Write(di.ToString());
					writer.Write(" [label=\"");
					var sb = new StringBuilder();
					IList<KeyValuePair<int, int>> rngs = _ToPairs(rngGrp.Value);
					var nrngs = new List<KeyValuePair<int, int>>(_NotRanges(rngs));
					var isNot = false;
					if (nrngs.Count < rngs.Count || (nrngs.Count == rngs.Count && 0x10ffff == rngs[rngs.Count - 1].Value))
					{
						isNot = true;
						if (0 != nrngs.Count)
						{
							sb.Append("^");
						}
						else
						{
							sb.Append(".");
						}
						rngs = nrngs;
					}
					var rpairs = _FromPairs(rngs);
					for (var r = 0; r < rpairs.Length; r += 2)
						_AppendRangeTo(sb, rpairs, r);
					if (isNot || sb.Length != 1 || (char.IsWhiteSpace(sb.ToString(), 0)))
					{
						writer.Write('[');
						writer.Write(_EscapeLabel(sb.ToString()));
						writer.Write(']');
					}
					else
						writer.Write(_EscapeLabel(sb.ToString()));
					writer.WriteLine("\"]");
				}

				++i;
			}

			i = 0;
			foreach (var ffa in closure)
			{
				writer.Write(name);
				writer.Write(i);
				writer.Write(" [");

				writer.Write("label=<");
				writer.Write("<TABLE BORDER=\"0\"><TR><TD>");
				writer.Write(spfx);
				writer.Write("<SUB>");
				writer.Write(i+startingIndex);
				writer.Write("</SUB></TD></TR>");


				if (!hideAcceptingId && ffa.IsAccepting)
				{
					writer.Write("<TR><TD>");
					writer.Write(Convert.ToString(ffa.AcceptSymbol).Replace("\"", "&quot;"));
					writer.Write("</TD></TR>");

				}
				writer.Write("</TABLE>");
				writer.Write(">");
				bool isfinal = false;
				if (!hideAccept && ( accepting.Contains(ffa) || (isfinal = finals.Contains(ffa))))
					writer.Write(",shape=doublecircle");
				if (isfinal)
				{

					writer.Write(",color=gray");

				}
				writer.WriteLine("]");
				++i;
			}
			string delim = "";
			if (!hideAccept && 0 < accepting.Count)
			{
				foreach (var ntfa in accepting)
				{
					writer.Write(delim);
					writer.Write(name);
					writer.Write(closure.IndexOf(ntfa));
					delim = ",";
				}
				writer.WriteLine(" [shape=doublecircle]");
			}

			delim = "";
			if (0 < finals.Count)
			{
				foreach (var ntfa in finals)
				{
					writer.Write(delim);
					writer.Write(name);
					writer.Write(closure.IndexOf(ntfa));
					delim = ",";
				}
				if (hideAccept)
				{
					writer.WriteLine(" [shape=circle,color=gray]");
				}
				else
				{
					writer.WriteLine(" [shape=doublecircle,color=gray]");
				}
			}

		}
		static void _RenderRuleTo(int index,LexRule rule,FFA fa, FFA blockEnd,  TextWriter writer, FFA.DotGraphOptions options = null)
		{
			if (null == options) options = new FFA.DotGraphOptions();
			string spfx = (null == options.StatePrefix ? "q" : options.StatePrefix);
			
			var name = _MakeSafeName(rule.Symbol);
			writer.WriteLine("subgraph cluster_"+index.ToString()+" {");
			writer.WriteLine("node [shape=circle];");
			writer.WriteLine("label=\"" + _EscapeLabel(rule.Symbol) + "\";");
			if (blockEnd == null)
			{
				_RenderFsmTo(fa, name,0,spfx, false, false, writer);
			}
			else
			{
				_RenderFsmTo(fa, name,0,spfx, true, true, writer);
				var cl = fa.FillClosure();
				var acl = fa.FillAcceptingStates();
				foreach(var afa in acl)
				{
					writer.Write(name + cl.IndexOf(afa).ToString() + "->");
					writer.Write(name + "BlockEnd0 [label=\"\\.\\*\\?");
					writer.WriteLine("\", style=dashed, color=grey]");
				}
				_RenderFsmTo(blockEnd, name+"BlockEnd",cl.Count, spfx, false, false, writer);
			}
			writer.WriteLine("}");
		}
		static void _RenderDotGraph(string inputfile,IList<LexRule> rules,FFA[] fas,int[][] blockEnds, FFA.DotGraphOptions options, TextWriter writer)
		{
			//writer = Console.Out;
			writer.WriteLine("digraph " + Path.GetFileNameWithoutExtension(inputfile) + " {");
			writer.WriteLine("rankdir=LR");
			writer.WriteLine("node [shape=circle]");
			
			for (var i = rules.Count - 1; i >= 0; --i)
			{
				var fa = fas[i];
				_RenderRuleTo(i, rules[i], fa, FFA.FromDfaTable(blockEnds[rules[i].Id]), writer, options);
			}
			writer.WriteLine("}");
				
		}
		static void _RenderDotToFile(string inputfile,string filename,IList<LexRule> rules, FFA[] fas, int[][] blockEnds, FFA.DotGraphOptions options = null)
		{
			if (null == options)
				options = new FFA.DotGraphOptions();
			string args = "-T";
			string ext = Path.GetExtension(filename);
			if (0 == string.Compare(".dot", ext, StringComparison.InvariantCultureIgnoreCase))
			{
				using (var writer = new StreamWriter(filename, false))
				{
					_RenderDotGraph(inputfile, rules,fas,blockEnds,options, writer);
					return;
				}
			}
			else if (0 == string.Compare(".png", ext, StringComparison.InvariantCultureIgnoreCase))
				args += "png";
			else if (0 == string.Compare(".jpg", ext, StringComparison.InvariantCultureIgnoreCase))
				args += "jpg";
			else if (0 == string.Compare(".bmp", ext, StringComparison.InvariantCultureIgnoreCase))
				args += "bmp";
			else if (0 == string.Compare(".svg", ext, StringComparison.InvariantCultureIgnoreCase))
				args += "svg";
			if (0 < options.Dpi)
				args += " -Gdpi=" + options.Dpi.ToString();

			args += " -o\"" + filename + "\"";

			var psi = new ProcessStartInfo("dot", args)
			{
				CreateNoWindow = true,
				UseShellExecute = false,
				RedirectStandardInput = true
			};
			using (var proc = Process.Start(psi))
			{
				_RenderDotGraph(inputfile, rules, fas, blockEnds, options, proc.StandardInput);
				proc.StandardInput.Close();
				proc.WaitForExit();
			}

		}
	}


}
