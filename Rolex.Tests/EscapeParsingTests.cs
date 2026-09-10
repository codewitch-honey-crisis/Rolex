using System.Collections.Generic;
using F;
using Xunit;

namespace Rolex.Tests
{
    /// <summary>
    /// Covers the cursor bug in <c>_ParseEscapePart</c> and <c>_ParseRangeEscapePart</c>.
    ///
    /// Both methods read the four hex digits of a \uXXXX or \xXXXX escape and then
    /// returned without advancing past the last one. The value they returned was right;
    /// the cursor was one character short, so the trailing digit was read a second time as
    /// a literal. Nothing reports an error - the grammar compiles and the generated lexer
    /// runs, it just recognises a different language.
    ///
    /// The escapes are parsed twice over in FA.brick.cs, once in <see cref="FA"/> (the path
    /// rolex.exe uses) and once in <see cref="RegexExpression"/>, and the defect was in
    /// both. The two front ends do not read the same grammar, though - see
    /// <see cref="RegexExpression_reads_backslash_u_as_the_upper_class_not_an_escape"/> -
    /// so only the \x cases can be run through both.
    /// </summary>
    public class EscapeParsingTests
    {
        public static IEnumerable<object[]> BothParsers()
        {
            yield return new object[] { "FA.Parse" };
            yield return new object[] { "RegexExpression.Parse" };
        }

        /// <summary>Builds the minimized DFA for a pattern through the named front end.</summary>
        private static FA Machine(string parser, string pattern)
        {
            var fa = parser == "FA.Parse"
                ? FA.Parse(pattern, 0, true)
                : RegexExpression.Parse(pattern).ToFA(0);
            return fa.ToMinimized();
        }

        private static FA Machine(string pattern)
        {
            return Machine("FA.Parse", pattern);
        }

        // ---- \uXXXX ------------------------------------------------------------

        /// <summary>
        /// The case from the bug report. [\u0041-\u005A] is A-Z, so the minimized machine
        /// is one state with a single 65..90 transition. Pre-fix it came out 49..90: 49 is
        /// '1', the trailing digit of \u0041, picked up as a literal because the cursor
        /// never moved past it.
        /// </summary>
        [Fact]
        public void Unicode_escape_range_in_a_charset_is_exactly_that_range()
        {
            var t = Assert.Single(Machine(@"[\u0041-\u005A]").Transitions);
            Assert.Equal(65, t.Min);
            Assert.Equal(90, t.Max);
        }

        [Theory]
        [InlineData("A", true)]
        [InlineData("M", true)]
        [InlineData("Z", true)]
        [InlineData("1", false)]
        [InlineData("@", false)]
        [InlineData("[", false)]
        public void Unicode_escape_range_in_a_charset_accepts_only_A_to_Z(string input, bool expected)
        {
            Assert.Equal(expected, Accepts(Machine(@"[\u0041-\u005A]"), input));
        }

        /// <summary>
        /// Outside a charset the leftover digit was concatenated instead, so the pattern
        /// matched "A1b" rather than "Ab".
        /// </summary>
        [Theory]
        [InlineData("Ab", true)]
        [InlineData("A1b", false)]
        public void Unicode_escape_in_a_sequence_consumes_all_four_digits(string input, bool expected)
        {
            Assert.Equal(expected, Accepts(Machine(@"\u0041b"), input));
        }

        /// <summary>The escapes ClosedXML.Parser's grammars are full of.</summary>
        [Theory]
        [InlineData(@"\u0009", '\t')]
        [InlineData(@"\u000A", '\n')]
        [InlineData(@"\u000D", '\r')]
        [InlineData(@"\u0027", '\'')]
        public void Unicode_escape_alone_is_a_single_codepoint(string pattern, char expected)
        {
            var t = Assert.Single(Machine(pattern).Transitions);
            Assert.Equal(expected, t.Min);
            Assert.Equal(expected, t.Max);
        }

        /// <summary>
        /// Why the \u cases above go through FA.Parse only.
        ///
        /// The two front ends in FA.brick.cs accept different grammars. <see cref="FA"/>
        /// claims \d \D \s \S \w \W and hands everything else to _ParseEscapePart, so \u
        /// is a codepoint escape there. <see cref="RegexExpression"/> additionally claims
        /// \h \l and \u as the POSIX blank, lower and upper classes, so \u0041 reads as
        /// [[:upper:]] followed by the literal text "0041", and the \u branch of its own
        /// _ParseEscapePart is unreachable.
        ///
        /// That divergence predates this fix and is left alone: changing what \u means in
        /// RegexExpression would be a grammar change, not a bug fix. Pinned here so the
        /// asymmetry in this file reads as deliberate.
        /// </summary>
        [Theory]
        [InlineData("Q0041", true)]
        [InlineData("A0041", true)]
        [InlineData("A", false)]
        [InlineData("q0041", false)]
        public void RegexExpression_reads_backslash_u_as_the_upper_class_not_an_escape(
            string input, bool expected)
        {
            Assert.Equal(expected, Accepts(Machine("RegexExpression.Parse", @"\u0041"), input));
        }

        // ---- \xXXXX -------------------------------------------------------------
        // The same defect, one branch up in the same two switches: the four-digit form of
        // \x also returned on the last digit without advancing. Neither front end claims
        // \x for a character class, so these run through both.

        [Theory]
        [MemberData(nameof(BothParsers))]
        public void Hex_escape_range_in_a_charset_is_exactly_that_range(string parser)
        {
            var t = Assert.Single(Machine(parser, @"[\x0041-\x005A]").Transitions);
            Assert.Equal(65, t.Min);
            Assert.Equal(90, t.Max);
        }

        [Theory]
        [InlineData("FA.Parse", "Ab", true)]
        [InlineData("FA.Parse", "A1b", false)]
        [InlineData("RegexExpression.Parse", "Ab", true)]
        [InlineData("RegexExpression.Parse", "A1b", false)]
        public void Hex_escape_in_a_sequence_consumes_all_four_digits(
            string parser, string input, bool expected)
        {
            Assert.Equal(expected, Accepts(Machine(parser, @"\x0041b"), input));
        }

        /// <summary>
        /// The short forms of \x stop at the first non-hex character, returning from a
        /// cursor that is already in the right place, so the fix must not move them.
        ///
        /// The follower has to be a non-hex letter: \x is greedy up to four digits, so
        /// \x41b is the single codepoint 0x1B, not "A" then "b". That is pre-existing
        /// behaviour and this fix does not change it.
        /// </summary>
        [Theory]
        [InlineData("FA.Parse", @"\x41z", "Az", true)]
        [InlineData("FA.Parse", @"\x41z", "A1z", false)]
        [InlineData("FA.Parse", @"[\x41-\x5A]", "M", true)]
        [InlineData("FA.Parse", @"[\x41-\x5A]", "1", false)]
        [InlineData("RegexExpression.Parse", @"\x41z", "Az", true)]
        [InlineData("RegexExpression.Parse", @"\x41z", "A1z", false)]
        [InlineData("RegexExpression.Parse", @"[\x41-\x5A]", "M", true)]
        [InlineData("RegexExpression.Parse", @"[\x41-\x5A]", "1", false)]
        public void Short_hex_escapes_are_unchanged(string parser, string pattern, string input, bool expected)
        {
            Assert.Equal(expected, Accepts(Machine(parser, pattern), input));
        }

        // ---- the escapes that already worked ------------------------------------
        // Every other branch of the two switches advanced correctly. Pinned so the fix
        // stays confined to \u and \x.

        [Theory]
        [InlineData("FA.Parse", @"\tb", "\tb", true)]
        [InlineData("FA.Parse", @"\nb", "\nb", true)]
        [InlineData("FA.Parse", @"\rb", "\rb", true)]
        [InlineData("FA.Parse", @"\.b", ".b", true)]
        [InlineData("FA.Parse", @"\.b", "xb", false)]
        [InlineData("FA.Parse", @"[\t\n]", "\n", true)]
        [InlineData("FA.Parse", @"[\t\n]", "x", false)]
        [InlineData("RegexExpression.Parse", @"\tb", "\tb", true)]
        [InlineData("RegexExpression.Parse", @"\nb", "\nb", true)]
        [InlineData("RegexExpression.Parse", @"\rb", "\rb", true)]
        [InlineData("RegexExpression.Parse", @"\.b", ".b", true)]
        [InlineData("RegexExpression.Parse", @"\.b", "xb", false)]
        [InlineData("RegexExpression.Parse", @"[\t\n]", "\n", true)]
        [InlineData("RegexExpression.Parse", @"[\t\n]", "x", false)]
        public void Non_hex_escapes_are_unchanged(string parser, string pattern, string input, bool expected)
        {
            Assert.Equal(expected, Accepts(Machine(parser, pattern), input));
        }

        /// <summary>Walks a DFA over the input. Returns false if it falls off the machine.</summary>
        private static bool Accepts(FA dfa, string input)
        {
            var current = dfa;
            foreach (var ch in input)
            {
                FA next = null;
                foreach (var t in current.Transitions)
                {
                    if (t.Min <= ch && ch <= t.Max) { next = t.To; break; }
                }
                if (next == null) return false;
                current = next;
            }
            return current.IsAccepting;
        }
    }
}
