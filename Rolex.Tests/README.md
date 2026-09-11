# Rolex.Tests

Covers the determinization, unicode escape and `_KeySet` hash fixes in
`Rolex/FA.brick.cs`. Run with:

    dotnet test Rolex.Tests\Rolex.Tests.csproj

212 tests, about 3 seconds. Against the engine as it was *before* the fix the same
suite takes just over 6 minutes, which is the problem it exists to catch.

## Why the engine is source-linked rather than referenced

`Rolex.csproj` builds a strong-name signed **Exe**, and `class FA` in
`FA.brick.cs` has no access modifier, so it is `internal` to that assembly. A
`ProjectReference` cannot see it, and `InternalsVisibleTo` would need the full
public key of `Key.snk`. The test project therefore compiles `FA.brick.cs` and
`LexContext.brick.cs` into itself. Same source file, no change required to the
shipping project.

The test project targets `net472` to match Rolex. That rules out TUnit, which
needs .NET 8 or later, so this uses xUnit.

## What is covered

**`StateTableRegressionTests`** - the load-bearing one. `TestData/fa-golden.tsv`
holds packed state tables (`FA.ToArray()`) for 13 regexes at three stages each:
NFA, after `ToDfa()`, and after `ToMinimized()`. The fix was meant to change only
time and memory, so every table must still come out identical, element for
element. Seven of the regexes are small hand-written ones; the other six are the
real R1C1 rules that caused the blow-up.

**`EngineInvariantTests`** - the two rewrites that carried a correctness
obligation, each checked against the behaviour it replaced:

- `FillEpsilonClosure` now uses a `HashSet` for its seen check. The tests assert
  it still returns each state once, in the same depth-first order, includes the
  state it was called on, agrees with the untouched static overload, and appends
  to a caller-supplied list without re-adding.
- Dead-state trimming replaced a per-transition `FillAcceptingStates()` scan with
  one reverse-reachability pass. `FillAcceptingStates` was left untouched, so it
  still serves as the oracle for what the old trim did -
  `Trim_agrees_with_the_original_oracle_on_small_machines` uses it directly.
  That method walks the whole graph per call, which is the cost the fix removed,
  so it only runs on the small regexes; `Trim_is_complete_on_every_case` asserts
  the same postcondition in linear time so it can cover the real R1C1 rules.

Plus language sampling over both the determinized and minimized machines,
including R1C1 references such as `R1048576C16384` (accepted) and `R1048577C1`
(rejected).

**`EscapeParsingTests`** - the unicode escape fix. `_ParseEscapePart` and
`_ParseRangeEscapePart` read the four hex digits of a `\uXXXX` or `\xXXXX` escape
and returned without advancing past the last one, so the trailing digit was read
again as a literal and the grammar silently meant something else: `[\u0041-\u005A]`
came out as 49..90 rather than 65..90. Covers both front ends in `FA.brick.cs`,
and pins the branches the fix must not move - the short forms of `\x`, the
non-hex escapes, and the fact that `RegexExpression` reads `\u` as the POSIX
upper class rather than as an escape at all.

It also covers a second defect in the same two switches, found in review: both
accumulated the four hex digits of `\xXXXX` into a `byte`, so the third and
fourth `b <<= 4` discarded the high bits and any value above `0x00FF` came out
wrong - `\x20AC` as `0x00AC`. `\u` was never affected; it accumulates into a
`ushort`. Same failure mode as the cursor bug: the grammar silently means a
different character and nothing reports it.

**`SubsetIdentityTests`** - the `_KeySet` hash fix. `_KeySet<T>.Add` XORed the
item's hash on every call, including calls the inner `HashSet` rejected as
duplicates. XOR is self-inverse, so the set's hash came to depend on how many times
a member arrived rather than on membership, and since `_KeySet.Equals` opens with a
hash comparison, `_Determinize` missed subsets it had already seen and allocated a
second DFA state for them. `_KeySet` is private, so the tests assert the consequence
through the public API: determinization must produce exactly one state per NFA
subset. They also check the determinized and minimized machines still agree on a
corpus sampled by walking the minimized machine and probing one codepoint either
side of every transition range.

**`GeneratedOutputTests`** - end to end. Runs the real `rolex.exe` over every
grammar in `testcases/` and checks the emitted file against the SHA-256 goldens
in `testcases/expected.txt`.

`Combined_lexer_for_six_rules_builds_without_blowing_up` is the test that
actually fails on the unfixed engine. It builds the six-rule lexer the way
`_BuildLexer` does and asserts it finishes inside 30 s. Before the fix that path
reports *"Combined build took 78.6s"* - matching the 76 s in
`testcases/README.md`. The budget is deliberately loose: it exists to catch a
return of super-linear growth, not to police milliseconds.

Peak memory is asserted separately, by
`Generated_tokenizer_stays_within_its_memory_budget`, which samples
`PeakWorkingSet64` from the `rolex.exe` child process while it generates
`full-r1c1.rl` and requires it to stay under 1 GB. It lives there rather than in
the in-process test because `GC.GetTotalMemory` reports the managed heap at one
instant, not the peak - the intermediate structures that made this path need
3 GB are garbage by the time the method returns, so an in-process reading never
actually measured the blow-up.

## Provenance of the goldens

`TestData/fa-golden.tsv` was recorded from the engine **before** the
determinization fix, using `Tools/GoldenGen.cs` compiled against the pre-fix
`FA.brick.cs`. That is what makes it evidence rather than a snapshot of current
behaviour.

The recording carries two later changes, applied to that pre-fix `FA.brick.cs`
before recording. Both have to be there, because each corrects what a right answer
looks like rather than how it is computed, and both are verified to produce
identical tables on either side of the determinization rewrite.

The unicode escape fix, because it corrects what the escape-bearing rules mean and a
table recorded without it would pin a language nothing should produce. Nine of the
thirty-nine rows moved, all belonging to `CELL_FUNCTION_LIST`, `SHEET_RANGE_PREFIX`
and `SINGLE_SHEET_PREFIX` - the only three rules in `slow-6-rules.rl` that use
escapes.

The `_KeySet.Add` hash fix, because without it determinization emits duplicate
states and a table recorded without it would pin a DFA with states that should never
have existed. Two rows moved, the `dfa` stage of `SHEET_RANGE_PREFIX` and
`SINGLE_SHEET_PREFIX`; their `nfa` and `min` stages did not, which is the signature
of the bug - minimization was already merging the duplicates away.

`Tools/GoldenGen.cs` is excluded from compilation and kept only so the file can
be reproduced. Re-recording is not a routine action - if these tables change,
the DFA changed, and that is the thing the suite is meant to stop.

To re-record, build `GoldenGen.cs` together with the `FA.brick.cs` you want to
capture into a `net472` exe and run:

    goldengen.exe testcases\slow-6-rules.rl Rolex.Tests\TestData\fa-golden.tsv

## Environment

`ROLEX_REPO_ROOT` overrides repository discovery, for running the tests from a
build output that sits outside the working tree. Otherwise the suite walks up
from the test assembly looking for `Rolex.sln`.
