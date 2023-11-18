# Rolex

Lexical analysis is often the first stage of parsing. Before we can analyze a structured document, we must break it into lexical elements called tokens or lexemes. Most parsers use this approach, taking tokens from the lexer and using them as their elemental input. Lexers can also be used outside of parsers, and are in tools like minifiers or even simple scanners that just need to look for patterns in a document, since lexers essentially work as compound regular expression matchers.

Rolex generates lexers to make this process painless and relatively intuitive, both in terms of defining them and using them. The code Rolex generates uses a simple but reliably fast DFA algorithm. All matching is done in linear time. There are no potentially quadratic time expressions you can feed it since it doesn't backtrack. The regular expressions are simple. There are no capturing groups because they are not needed. There are no anchors because they complicate matching, and aren't very useful in tokenizers. There are no lazy expressions, but there is a facility to define multicharacter ending conditions, which is 80% of what lazy expressions are used for.

The main advantage of using Rolex is speed. The generated tokenizers are very fast. The main disadvantage of using Rolex, aside from a somewhat limited regular expression syntax, is the time it can take to generate complicated lexers. Basically you pay for the performance of Rolex upfront, during the build.

Note: This project uses deslang.exe to build. Deslang is here https://github.com/codewitch-honey-crisis/Deslang