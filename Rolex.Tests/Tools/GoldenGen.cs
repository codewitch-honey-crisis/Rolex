using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using F;

// Dumps FA-level golden state tables. Built against the PRE-FIX FA.brick.cs - master's
// engine, with only the correctness fixes applied - so the recorded tables carry real
// pre-fix provenance, then asserted against post-fix code.
//
// Two fixes are carried over, both because they change what a correct table looks like:
//
//   the \u/\x escape fix, which changes what the escape-bearing rules mean, so a table
//   recorded without it would pin a language nothing should produce;
//
//   the _KeySet.Add hash fix, without which determinization emits duplicate states, so a
//   table recorded without it would pin a DFA with states that should never have existed.
//
// The determinization rewrite these tables are evidence for is deliberately absent, and
// both carried fixes are verified to produce identical tables on either side of it.
static class GoldenGen
{
    static IEnumerable<KeyValuePair<string, string>> RuleRegexes(string rlPath)
    {
        foreach (var raw in File.ReadAllLines(rlPath))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("//")) continue;
            var eq = line.IndexOf('=');
            if (eq < 0) continue;
            var name = line.Substring(0, eq).Trim();
            var expr = line.Substring(eq + 1).Trim();
            if (expr.Length >= 2 && expr[0] == '\'' && expr[expr.Length - 1] == '\'')
                expr = expr.Substring(1, expr.Length - 2);
            yield return new KeyValuePair<string, string>(name, expr);
        }
    }

    static string Pack(int[] table)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < table.Length; ++i)
        {
            if (i != 0) sb.Append(',');
            sb.Append(table[i].ToString(CultureInfo.InvariantCulture));
        }
        return sb.ToString();
    }

    public static List<KeyValuePair<string, string>> BuildCases(string rl)
    {
        var cases = new List<KeyValuePair<string, string>>();
        cases.Add(new KeyValuePair<string, string>("lit_abc", "abc"));
        cases.Add(new KeyValuePair<string, string>("alt_abc", "a|b|c"));
        cases.Add(new KeyValuePair<string, string>("star_group", "(ab)*c"));
        cases.Add(new KeyValuePair<string, string>("plus_digit", "[0-9]+"));
        cases.Add(new KeyValuePair<string, string>("opt_nested", "(a(b|c)?d)*"));
        cases.Add(new KeyValuePair<string, string>("classic_abb", "(a|b)*abb"));
        cases.Add(new KeyValuePair<string, string>("dead_branch", "(ab|ac)d"));
        foreach (var kv in RuleRegexes(rl)) cases.Add(kv);
        return cases;
    }

    static int Main(string[] args)
    {
        var rl = args[0];
        var outPath = args[1];
        using (var w = new StreamWriter(outPath, false, new UTF8Encoding(false)))
        {
            w.NewLine = "\n";
            w.WriteLine("# name\tstage\tpacked-state-table");
            w.WriteLine("# generated from the pre-determinization-fix FA.brick.cs, with the escape and _KeySet hash fixes applied");
            foreach (var kv in BuildCases(rl))
            {
                var nfa = FA.Parse(kv.Value, 0, true);
                var dfa = FA.Parse(kv.Value, 0, true).ToDfa();
                var min = FA.Parse(kv.Value, 0, true).ToMinimized();
                w.WriteLine(kv.Key + "\tnfa\t" + Pack(nfa.ToArray()));
                w.WriteLine(kv.Key + "\tdfa\t" + Pack(dfa.ToArray()));
                w.WriteLine(kv.Key + "\tmin\t" + Pack(min.ToArray()));
                Console.Error.WriteLine("  " + kv.Key + " ok");
            }
        }
        Console.Error.WriteLine("wrote " + outPath);
        return 0;
    }
}
