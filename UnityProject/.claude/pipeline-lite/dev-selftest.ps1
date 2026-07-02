# dev-selftest.ps1 - lite-dev compile + EditMode test gate (junction-twin model).
#
# Runs batchmode against a JUNCTION TWIN of the project instead of the project the user's
# Editor holds. The twin (default D:\work\TEngine_block\UnityProject_selftest, a sibling of
# UnityProject so relative `file:` package paths in manifest.json resolve identically) junctions
# Assets + Packages back to the primary (shared source, not copied), syncs ProjectSettings each
# run, and keeps its OWN Library + Temp. Because Unity's single-instance lock is per project
# path, the twin runs while the primary Editor stays OPEN - no need to close the Editor.
#
# First run cold-imports the shared Assets into the twin's own Library (~6min); after that the
# twin Library is warm and a run is ~50s. Unity version is derived from the primary's
# ProjectVersion.txt so it always matches what the user's Editor uses.
#
# Verdict = exit code + results.xml, gated on errors only (not warnings).
param(
  [string]$MainRoot = "D:\work\TEngine_block",
  [string]$Twin     = "D:\work\TEngine_block\UnityProject_selftest",
  [string]$Unity    = "",   # override; empty = derive from primary ProjectVersion.txt
  [string]$Filter   = ""    # optional -testFilter; empty = full EditMode suite
)
$ErrorActionPreference = "Stop"
$srcProj = Join-Path $MainRoot "UnityProject"
$results = Join-Path $env:TEMP "dev-selftest-results.xml"
$log     = Join-Path $env:TEMP "dev-selftest.log"

# Resolve the Unity exe matching the project version, across known install roots.
if (-not $Unity) {
  $verLine = Select-String -Path (Join-Path $srcProj "ProjectSettings\ProjectVersion.txt") -Pattern "^m_EditorVersion:\s*(.+)$"
  $ver = $verLine.Matches[0].Groups[1].Value.Trim()
  $cands = @(
    "C:\Program Files\Unity\Hub\Editor\$ver\Editor\Unity.exe",
    "D:\Program Files\Unity\Hub\Editor\$ver\Editor\Unity.exe",
    "D:\Program Files\Unity\$ver\Editor\Unity.exe"
  )
  $Unity = $cands | Where-Object { Test-Path $_ } | Select-Object -First 1
}
if (-not $Unity -or -not (Test-Path $Unity)) { Write-Host "VERDICT : FAIL - Unity.exe not found for version '$ver' (looked in Hub/D: roots)"; exit 2 }

# Bootstrap / refresh the twin: ensure it exists, junction Assets + Packages, sync ProjectSettings.
$freshTwin = $false
if (-not (Test-Path $Twin)) { New-Item -ItemType Directory -Path $Twin | Out-Null }
if (-not (Test-Path (Join-Path $Twin "Assets")))   { New-Item -ItemType Junction -Path (Join-Path $Twin "Assets")   -Target (Join-Path $srcProj "Assets")   | Out-Null; $freshTwin = $true }
if (-not (Test-Path (Join-Path $Twin "Packages"))) { New-Item -ItemType Junction -Path (Join-Path $Twin "Packages") -Target (Join-Path $srcProj "Packages") | Out-Null }
$twinPS = Join-Path $Twin "ProjectSettings"
# Sync ProjectSettings from primary, copying only CHANGED files (robocopy /MIR) so an unchanged run
# keeps the twin's Library warm. A blind Copy-Item -Force rewrites mtimes every run, which makes Unity
# observe "script compilation related files changed" and recompile from cold each time.
robocopy (Join-Path $srcProj "ProjectSettings") $twinPS /MIR /NJH /NJS /NDL /NFL /NP | Out-Null
if ($LASTEXITCODE -ge 8) { Write-Host "VERDICT : FAIL - ProjectSettings robocopy sync failed (rc=$LASTEXITCODE)"; exit 2 }
$global:LASTEXITCODE = 0

# Precondition: only the TWIN's own lock matters (the primary Editor may stay open). A held lock
# (another self-check running) can't be deleted -> BLOCKED; a stale lock (crashed run) deletes -> proceed.
$lock = Join-Path $Twin "Temp\UnityLockfile"
if (Test-Path $lock) {
  try { Remove-Item $lock -Force -ErrorAction Stop }
  catch { Write-Host "VERDICT : BLOCKED - twin is busy (another self-check running). Wait and re-run."; exit 3 }
}

# A freshly-created twin has a cold Library. Its very first batchmode run cold-imports all Assets
# while directory monitoring is disabled (symlinks), which makes Unity spuriously abort with
# "Scripts have compiler errors" even though compilation succeeds and tests pass. Do a one-time
# throwaway warm-up import/compile so the gated run below is warm and reports a truthful verdict.
if ($freshTwin) {
  Write-Host "INFO    : fresh twin - one-time cold warm-up import (subsequent runs are warm ~50s)..."
  $wlog = Join-Path $env:TEMP "dev-selftest-warmup.log"
  Start-Process -FilePath $Unity -ArgumentList @("-batchmode","-nographics","-quit","-projectPath",$Twin,"-logFile",$wlog,"-acceptSoftwareTermsForThisRunOnly") -Wait -NoNewWindow | Out-Null
}

# Run batchmode on the twin (its own warm Library).
if (Test-Path $results) { Remove-Item $results -Force }
$uargs = @("-runTests","-batchmode","-nographics",
           "-projectPath",$Twin,
           "-testPlatform","EditMode",
           "-testResults",$results,"-logFile",$log,
           "-acceptSoftwareTermsForThisRunOnly")
if ($Filter) { $uargs += @("-testFilter",$Filter) }
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$p  = Start-Process -FilePath $Unity -ArgumentList $uargs -PassThru -Wait -NoNewWindow
$sw.Stop()
$rc = $p.ExitCode
$secs = [int]$sw.Elapsed.TotalSeconds

# Verdict: exit code + compile errors + test result.
$compileErrors = 0
if (Test-Path $log) { $compileErrors = (Select-String -Path $log -Pattern "error CS\d" -AllMatches | Measure-Object).Count }
$verdict = "FAIL"; $summary = ""; $failedNames = @()
if (Test-Path $results) {
  # Read results.xml as raw text and pull the summary from the root <test-run ...>
  # opening tag via regex. A full [xml] parse throws when any test's captured <output>
  # holds content the XML reader rejects (e.g. unbalanced tags in logged text), even
  # though the run itself is fine; the counts we gate on live only in the root tag.
  $raw = Get-Content $results -Raw -ErrorAction SilentlyContinue
  $tag = [regex]::Match($raw, '<test-run\b[^>]*>').Value
  $attr = { param($n) $mm = [regex]::Match($tag, $n + '="([^"]*)"'); if ($mm.Success) { $mm.Groups[1].Value } else { "" } }
  $rResult = & $attr 'result'
  $summary = "tests total=$(& $attr 'total') passed=$(& $attr 'passed') failed=$(& $attr 'failed') skipped=$(& $attr 'skipped') result=$rResult"
  if ($rc -eq 0 -and $rResult -eq "Passed" -and $compileErrors -eq 0) { $verdict = "PASS" }
  else {
    # Failed test fullnames for actionable FAIL output (regex, same rationale as above).
    $failedNames = [regex]::Matches($raw, '<test-case\b[^>]*\bresult="Failed"[^>]*>') |
      ForEach-Object { $m2 = [regex]::Match($_.Value, 'fullname="([^"]*)"'); if ($m2.Success) { $m2.Groups[1].Value } } |
      Select-Object -Unique
  }
} elseif ($compileErrors -gt 0) {
  $summary = "COMPILE FAILED ($compileErrors CS errors) - no test run"
} else {
  $summary = "no results.xml, rc=$rc - startup abort (check log; possible stale lock)"
}

Write-Host "===================================="
Write-Host "VERDICT : $verdict   (${secs}s, exit=$rc, compileErrors=$compileErrors, unity=$Unity)"
Write-Host "DETAIL  : $summary"
Write-Host "LOG     : $log"
if ($verdict -eq "FAIL") {
  Write-Host "FIRST CS ERRORS:"; Select-String -Path $log -Pattern "error CS\d" | Select-Object -First 5 | ForEach-Object { $_.Line }
  if ($failedNames.Count -gt 0) { Write-Host "FAILED TESTS:"; $failedNames | Select-Object -First 10 | ForEach-Object { Write-Host "  $_" } }
}
if ($verdict -eq "PASS") { exit 0 } else { exit 1 }
