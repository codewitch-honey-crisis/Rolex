# Rolex.Tests

Covers the determinization fix in `Rolex/FA.brick.cs`. Run with:

    dotnet test Rolex.Tests\Rolex.Tests.csproj

114 tests, about 7 seconds. Against the engine as it was *before* the fix the same
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

**`GeneratedOutputTests`** - end to end. Runs the real `rolex.exe` over every
grammar in `testcases/` and checks the emitted file against the SHA-256 goldens
in `testcases/expected.txt`.

`Combined_lexer_for_six_rules_builds_without_blowing_up` is the test that
actually fails on the unfixed engine. It builds the six-rule lexer the way
`_BuildLexer` does and asserts it finishes inside 30 s and 512 MB. Before the
fix that path reports *"Combined build took 78.6s"* - matching the 76 s in
`testcases/README.md` - and needed over 3 GB. The budgets are deliberately loose:
they exist to catch a return of super-linear growth, not to police milliseconds.

## Provenance of the goldens

`TestData/fa-golden.tsv` was recorded from the engine **before** the fix, using
`Tools/GoldenGen.cs` compiled against the pre-fix `FA.brick.cs`. That is what
makes it evidence rather than a snapshot of current behaviour.

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
