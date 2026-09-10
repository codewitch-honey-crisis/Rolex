# Determinization blow-up test cases

Taken from ClosedXML.Parser's R1C1 formula lexer. Each file is a subset of the
same grammar, one `NAME = 'regex'` rule per line.

Run any of them with:

    rolex.exe <file>.rl /output out.cs /class T /namespace N /noshared

## Regression check

    powershell -File testcases\verify.ps1

Generates every grammar below and compares the SHA-256 of the result against
`expected.txt`. The golden hashes for `subsets/rules-1..5` and `slow-6-rules`
were captured from the build **immediately before** the determinization fix, so
a mismatch on those is a DFA correctness regression, not a performance change.
`oom-7-rules` and `full-r1c1` could not be generated at all before the fix, so
their hashes lock in post-fix behaviour.

Six of the eight were re-recorded once, for the unicode escape fix, which changed
what the escape-bearing rules mean. They were re-captured from that same
pre-determinization build with only the escape fix applied, and they came out
byte-identical to the fully fixed build - so the determinization fix is still
output-neutral on every grammar here. `rules-1` and `rules-2` use no escapes and
have never changed.

The `_KeySet.Add` hash fix did not move any of these. It removes duplicate states
from `ToDfa()`, and every duplicate it removes was one minimization already merged,
so generated output is untouched - which is what these hashes are here to show.

Use `-Update` to re-record the hashes, and only when the tables are meant to
change.

## Measurements

Before and after the determinization fix, measured on the shipped
AnyCPU/32-bit-preferred build:

| file                | rules | before               | after  | peak RAM after |
|---------------------|-------|----------------------|--------|----------------|
| subsets/rules-1.rl  | 1     | 0.3 s                | 0.17 s | 18 MB          |
| subsets/rules-2.rl  | 2     | 0.9 s                | 0.14 s | 17 MB          |
| subsets/rules-3.rl  | 3     | 1.1 s                | 0.15 s | 18 MB          |
| subsets/rules-4.rl  | 4     | 2.9 s                | 0.21 s | 27 MB          |
| subsets/rules-5.rl  | 5     | OutOfMemoryException | 0.36 s | 35 MB          |
| slow-6-rules.rl     | 6     | 76 s                 | 0.46 s | 36 MB          |
| oom-7-rules.rl      | 7     | OutOfMemoryException | 2.5 s  | 100 MB         |
| full-r1c1.rl        | 39    | OutOfMemoryException | 2.6 s  | 103 MB         |

The "before" column is the shape of the original problem: a 26x cost for roughly
1.5x the input between 4 and 6 rules, then failure. The 5-rule case needed more
than 2 GB, so it OOM'd on the shipped 32-bit-preferred build; clearing
32BITREQUIRED and 32BITPREFERRED let it reach ~3.4 GB and it still failed.

The cliff is gone: cost now grows smoothly with the input, and the whole
39-rule grammar completes in about 2.6 s inside 103 MB.

## What was wrong

Three separate super-linear costs, all in `Rolex/FA.brick.cs`:

1. **`_Minimize` built its alphabet `sigma` from a `List<int>` with no
   de-duplication**, while `_Determinize` builds the equivalent `points` set
   from a `HashSet<int>`. After `Totalize()` every state carries a transition
   per distinct range boundary, so `sigma` held one entry per transition per
   state instead of one per distinct boundary - 74,350 entries where only 59
   were distinct. `_Minimize` then allocates `states x sigma.Length` `Queue<FA>`
   and `_FList` objects, making the allocation O(states^2 x boundaries): 61.2M
   objects (~3.4 GB) for the 5-rule case against 48,616 after de-duplication.
   This was the OutOfMemoryException.

2. **`_Determinize` and `_Minimize` trimmed dead transitions by calling
   `trns.To.FillAcceptingStates()` once per transition**, and each call walks
   the entire reachable graph. On the 7-rule case that is 5,249 states x 71
   transitions = 372k full traversals, and it accounted for 129.5 s of the
   130.7 s spent in `_Determinize`. Replaced with a single reverse-reachability
   pass (`_FillCanReachAccepting`). This is sound because a transition is only
   removed when its target cannot reach an accepting state, and removing such a
   transition can never destroy a path to an accepting state - so the predicate
   is invariant while the loop runs and can be computed once up front.

3. **`_Determinize` recomputed epsilon closures inside the points loop** and
   rescanned every transition once per point. The closures are now memoized
   before the worklist loop, the instance `FillEpsilonClosure` uses a `HashSet`
   for its seen check (and no longer tests `result.Contains(this)` twice), and
   the points loop is inverted so each transition's range is bucketed against
   the points array via binary search instead of the array being rescanned per
   point. These were minor here (the closures in this grammar are nearly all
   singletons) but they remove the quadratic behaviour from the general case.

The generated tables are unchanged. Every case that could be generated before
the fix produces a byte-identical file after it, as do the repository's own
`Example.rl`, `Example2.rl` and `Slang.rl` grammars.
