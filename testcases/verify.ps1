<#
.SYNOPSIS
    Determinization regression check for Rolex.

.DESCRIPTION
    Generates a tokenizer from every grammar under testcases/ and compares the
    SHA-256 of the generated file against the golden hashes in expected.txt.

    The golden hashes for subsets/rules-1..5 and slow-6-rules were captured from
    the build immediately BEFORE the determinization fix, so a mismatch on those
    means the DFA changed - which is a correctness regression, not a perf change.

    oom-7-rules and full-r1c1 could not be generated at all before the fix
    (OutOfMemoryException), so their hashes lock in post-fix behaviour.

.PARAMETER Exe
    Path to rolex.exe. Defaults to the copy in the solution root.

.PARAMETER Update
    Rewrite expected.txt from the current run instead of comparing. Only do this
    when you intend to change the generated tables.

.EXAMPLE
    powershell -File testcases\verify.ps1
#>
[CmdletBinding()]
param(
    [string] $Exe = (Join-Path $PSScriptRoot '..\rolex.exe'),
    [switch] $Update
)

$ErrorActionPreference = 'Stop'
$expectedFile = Join-Path $PSScriptRoot 'expected.txt'

if (-not (Test-Path $Exe)) { throw "rolex.exe not found at '$Exe'. Build the Rolex project first." }
$Exe = (Resolve-Path $Exe).Path

# Ordered smallest-first so a failure shows up on the cheapest case.
$cases = @(
    'subsets\rules-1.rl'
    'subsets\rules-2.rl'
    'subsets\rules-3.rl'
    'subsets\rules-4.rl'
    'subsets\rules-5.rl'
    'slow-6-rules.rl'
    'oom-7-rules.rl'
    'full-r1c1.rl'
)

$expected = @{}
if (-not $Update) {
    if (-not (Test-Path $expectedFile)) { throw "Golden hash file not found: $expectedFile" }
    foreach ($line in Get-Content $expectedFile) {
        if ($line -match '^\s*(#|$)') { continue }
        $parts = $line -split '\s+', 2
        $expected[$parts[1].Trim()] = $parts[0].Trim()
    }
}

$tmp = Join-Path ([IO.Path]::GetTempPath()) ("rolex-verify-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $tmp | Out-Null

$results = @()
$failed = 0
try {
    foreach ($case in $cases) {
        $rl = Join-Path $PSScriptRoot $case
        # /class and /namespace are pinned: without /class the class name is derived
        # from the output file name, which would make the hashes path-dependent.
        $out = Join-Path $tmp 'generated.cs'
        Remove-Item $out -ErrorAction SilentlyContinue

        $sw = [Diagnostics.Stopwatch]::StartNew()
        & $Exe $rl /output $out /class T /namespace N /noshared 2>&1 | Out-Null
        $code = $LASTEXITCODE
        $sw.Stop()

        if ($code -ne 0 -or -not (Test-Path $out)) {
            $results += [PSCustomObject]@{ Case = $case; Seconds = [math]::Round($sw.Elapsed.TotalSeconds, 2); Status = "FAILED (exit $code)" }
            $failed++
            continue
        }

        $hash = (Get-FileHash $out -Algorithm SHA256).Hash
        if ($Update) {
            $expected[$case] = $hash
            $status = 'recorded'
        }
        elseif (-not $expected.ContainsKey($case)) {
            $status = 'NO GOLDEN HASH'; $failed++
        }
        elseif ($expected[$case] -eq $hash) {
            $status = 'ok'
        }
        else {
            $status = 'OUTPUT CHANGED'; $failed++
        }

        $results += [PSCustomObject]@{ Case = $case; Seconds = [math]::Round($sw.Elapsed.TotalSeconds, 2); Status = $status }
    }
}
finally {
    Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue
}

$results | Format-Table -AutoSize

if ($Update) {
    # A partial rewrite is worse than no rewrite: it would silently drop the goldens for
    # whichever cases failed to generate, and the next run would then compare against a
    # file that is missing exactly the entries that were broken.
    if ($failed -gt 0) {
        Write-Host "$failed case(s) failed to generate; $expectedFile left unchanged." -ForegroundColor Red
        exit 1
    }
    $lines = @(
        '# SHA-256 of the generated tokenizer for each grammar under testcases/.',
        '# Produced by: rolex.exe <grammar> /output <file> /class T /namespace N /noshared',
        '# Regenerate with: powershell -File testcases\verify.ps1 -Update',
        ''
    )
    foreach ($case in $cases) { if ($expected.ContainsKey($case)) { $lines += ('{0}  {1}' -f $expected[$case], $case) } }
    Set-Content -Path $expectedFile -Value $lines -Encoding ASCII
    Write-Host "Wrote $expectedFile" -ForegroundColor Green
    exit 0
}

if ($failed -gt 0) {
    Write-Host "$failed case(s) failed." -ForegroundColor Red
    exit 1
}

Write-Host "All $($results.Count) cases match." -ForegroundColor Green
exit 0
