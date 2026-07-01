# dev-selftest.ps1 - lite-dev compile + EditMode test gate.
#
# Runs batchmode against the SAME project the user's Editor uses (one project, no
# sidecar copy). A Unity project path can be held by only one instance at a time, so
# this gate requires the Editor to be CLOSED during the run; if the Editor holds the
# lock the script reports BLOCKED instead of running. The Unity version is auto-detected
# from ProjectVersion.txt so it always matches the project.
#
# Verdict = exit code + results.xml, gated on errors only (not warnings). Library is the
# user's warm Library, so a run is ~30s or less. The run triggers a domain reload on the
# main Library (harmless; equivalent to opening the project).
param(
  [string]$MainRoot = "D:\work\TEngine_block",
  [string]$Unity    = "",   # override; empty = derive from ProjectVersion.txt
  [string]$Filter   = ""    # optional -testFilter; empty = full EditMode suite
)
$ErrorActionPreference = "Stop"
$proj    = Join-Path $MainRoot "UnityProject"
$results = Join-Path $env:TEMP "dev-selftest-results.xml"
$log     = Join-Path $env:TEMP "dev-selftest.log"

# Resolve the Unity exe matching the project, unless explicitly overridden.
if (-not $Unity) {
  $verLine = Select-String -Path (Join-Path $proj "ProjectSettings\ProjectVersion.txt") -Pattern "^m_EditorVersion:\s*(.+)$"
  $ver = $verLine.Matches[0].Groups[1].Value.Trim()
  $Unity = "C:\Program Files\Unity\Hub\Editor\$ver\Editor\Unity.exe"
}
if (-not (Test-Path $Unity)) { Write-Host "VERDICT : FAIL - Unity.exe not found: $Unity"; exit 2 }

# Precondition: the Editor must not be holding this project (single-instance lock).
$lock = Join-Path $proj "Temp\UnityLockfile"
$editorRunning = @(Get-Process -Name Unity -ErrorAction SilentlyContinue).Count -gt 0
if ((Test-Path $lock) -and $editorRunning) {
  Write-Host "VERDICT : BLOCKED - Unity Editor is holding the project. Close it and re-run."
  exit 3
}

# Run batchmode on the main project path (warm Library).
if (Test-Path $results) { Remove-Item $results -Force }
$uargs = @("-runTests","-batchmode","-nographics",
           "-projectPath",$proj,
           "-testPlatform","EditMode",
           "-testResults",$results,"-logFile",$log)
if ($Filter) { $uargs += @("-testFilter",$Filter) }
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$p  = Start-Process -FilePath $Unity -ArgumentList $uargs -PassThru -Wait -NoNewWindow
$sw.Stop()
$rc = $p.ExitCode
$secs = [int]$sw.Elapsed.TotalSeconds

# Verdict: exit code + compile errors + test result.
$compileErrors = 0
if (Test-Path $log) { $compileErrors = (Select-String -Path $log -Pattern "error CS\d" -AllMatches | Measure-Object).Count }
$verdict = "FAIL"; $summary = ""
if (Test-Path $results) {
  [xml]$xml = Get-Content $results
  $run = $xml.'test-run'
  $summary = "tests total=$($run.total) passed=$($run.passed) failed=$($run.failed) result=$($run.result)"
  if ($rc -eq 0 -and $run.result -eq "Passed" -and $compileErrors -eq 0) { $verdict = "PASS" }
} elseif ($compileErrors -gt 0) {
  $summary = "COMPILE FAILED ($compileErrors CS errors) - no test run"
} else {
  $summary = "no results.xml, rc=$rc - startup abort (check log; possible stale lock)"
}

Write-Host "===================================="
Write-Host "VERDICT : $verdict   (${secs}s, exit=$rc, compileErrors=$compileErrors, unity=$Unity)"
Write-Host "DETAIL  : $summary"
Write-Host "LOG     : $log"
if ($verdict -eq "FAIL") { Write-Host "FIRST CS ERRORS:"; Select-String -Path $log -Pattern "error CS\d" | Select-Object -First 5 | ForEach-Object { $_.Line } }
if ($verdict -eq "PASS") { exit 0 } else { exit 1 }
