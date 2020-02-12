using CD;
using LC;
using F;
using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

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
			string nfagraph = null;
			string dfagraph = null;
			bool ignorecase = false;
			bool noshared = false;
			bool ifstale = false;
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
							stderr.Write("{0} is building file: {1}", Name, outputfile);
						else
							stderr.Write("{0} is building tokenizer.", Name);
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
						var fa = _BuildLexer(rules, ignorecase,inputfile);
						var symbolTable = _BuildSymbolTable(rules);
						var symids = new int[symbolTable.Length];
						for (var i = 0; i < symbolTable.Length; ++i)
							symids[i] = i;
						var blockEnds = _BuildBlockEnds(rules);
						var nodeFlags = _BuildNodeFlags(rules);
						if (null != nfagraph)
						{
							fa.RenderToFile(nfagraph);
						}
						
						fa = fa.ToDfa();
						DfaEntry[] dfaTable = null;
						dfaTable = _ToDfaStateTable(fa,symids);
						if (!noshared)
						{
							// import our Export/Token.cs into the library
							_ImportCompileUnit(Deslanged.Token, cns);

							// import our Export/TableTokenizer.cs into the library
							_ImportCompileUnit(Deslanged.TableTokenizer, cns);

						}
						var origName = "Rolex.";
						CodeTypeDeclaration td = null;
						if (null == td)
						{
							td = Deslanged.TableTokenizerTemplate.Namespaces[1].Types[0];
							origName += td.Name;
							td.Name = codeclass;
							CodeGenerator.GenerateSymbolConstants(td, symbolTable);
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
						f.InitExpression = CodeGenerator.GenerateDfaTableInitializer(dfaTable);

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
					type.CustomAttributes.Add(CodeGenerator.GeneratedCodeAttribute);
					dst.Types.Add(type);
				}
			}
		}

		// do our error handling here (release builds)
		static int _ReportError(Exception ex, TextWriter stderr)
		{
			//_PrintUsage(stderr);
			stderr.WriteLine("Error: {0}", ex.Message);
			return -1;
		}
		static void _PrintUsage(TextWriter w)
		{
			w.Write("Usage: "+Filename + " ");
			w.WriteLine("<inputfile> [/output <outputfile>] [/class <codeclass>] [/namespace <codenamespace>]");
			w.WriteLine("   [/language <codelanguage> [/ignorecase] [/noshared] [/ifstale]");
			w.WriteLine();
			w.WriteLine(Name + " generates a lexer/scanner/tokenizer in the target .NET language");
			w.WriteLine();
			w.WriteLine("   <inputfile>     The input lexer specification");
			w.WriteLine("   <outputfile>    The output source file - defaults to STDOUT");
			w.WriteLine("   <codeclass>     The name of the main class to generate - default derived from <outputfile>");
			w.WriteLine("   <codenamespace> The namespace to generate the code under - defaults to none");
			w.WriteLine("   <codelanguage>  The .NET language to generate the code in - default derived from <outputfile>");
			w.WriteLine("   <ignorecase>    Create a case insensitive lexer - defaults to case sensitive");
			w.WriteLine("   <noshared>      Do not generate the shared code as part of the output. Defaults to generating the shared code");
			w.WriteLine("   <ifstale>       Only generate if the input is newer than the output");
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
		static int[][] _BuildBlockEnds(IList<LexRule> rules)
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
				var rule = rules[i];
				var be = rule.GetAttribute("blockEnd") as string;
				if (!string.IsNullOrEmpty(be))
				{
					result[rule.Id] = new List<int>(UnicodeUtility.ToUtf32(be)).ToArray();
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
		static FFA _BuildLexer(IList<LexRule> rules, bool ignoreCase,string inputFile)
		{
			var exprs = new FFA[rules.Count];
			var result = new FFA();
			for (var i = 0; i < exprs.Length; ++i)
			{
				var rule = rules[i];

				var fa = FFA.Parse(rule.Expression.Substring(1, rule.Expression.Length - 2), rule.Id, rule.ExpressionLine, rule.ExpressionColumn, rule.ExpressionPosition, inputFile);
				if (0 > rule.Id)
					System.Diagnostics.Debugger.Break();
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


				result.AddEpsilon(fa);
			}
			return result;
		}
		static DfaEntry[] _ToDfaStateTable(FFA dfa, IList<int> symbolTable = null)
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
			var result = new DfaEntry[closure.Count];
			for (var i = 0; i < result.Length; i++)
			{
				var fa = closure[i];
#if DEBUG
				if (fa.IsAccepting)
					System.Diagnostics.Debug.Assert(-1 < fa.AcceptSymbol, "Illegal accept symbol " + fa.AcceptSymbol.ToString() + " was found on state state q" + i.ToString());
#endif
				// get all the transition ranges for each destination state
				var trgs = fa.FillInputTransitionRangesGroupedByState();
				// make a new transition entry array for our DFA state table
				var trns = new DfaTransitionEntry[trgs.Count];
				var j = 0;
				// for each transition range
				foreach (var trg in trgs)
				{
					// add the transition entry using
					// the packed ranges from CharRange
					trns[j] = new DfaTransitionEntry(
						trg.Value,
						closure.IndexOf(trg.Key));

					++j;
				}

				// now add the state entry for the state above
#if DEBUG
				if (fa.IsAccepting && !symbolLookup.ContainsKey(fa.AcceptSymbol))
				{
					try
					{
						dfa.RenderToFile(@"dfastatetable_crashdump_dfa.jpg");
					}
					catch
					{

					}
					System.Diagnostics.Debug.Assert(false, "The symbol table did not contain an entry for state q" + i.ToString());
				}
#endif
				result[i] = new DfaEntry(
					fa.IsAccepting ? symbolLookup[fa.AcceptSymbol] : -1,
					trns);

			}
			return result;
		}
	}


}
