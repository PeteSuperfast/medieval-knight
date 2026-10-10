# Multi-process co-op test on this PC only (127.0.0.1): 1 host + 3 clients, plus join attempts that must be refused.
# Usage:  powershell -ExecutionPolicy Bypass -File mptest.ps1 [-Port 47811] [-Out mptest]
# Results: <Out>\<role>\player.log  (lines "MPTEST PASS/FAIL ...") and screenshots in the same folders.
param([int]$Port = 47811, [string]$Out = "mptest")

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$exe = Join-Path $root "Builds\MedievalKnight.exe"
$outDir = Join-Path $root $Out
if (Test-Path $outDir) { Remove-Item -Recurse -Force $outDir }
New-Item -ItemType Directory -Force $outDir | Out-Null

function Launch([string]$role, [string[]]$extra) {
    $dir = Join-Path $outDir $role
    New-Item -ItemType Directory -Force $dir | Out-Null
    $a = @('-screen-width', '960', '-screen-height', '540', '-screen-fullscreen', '0', '-port', "$Port",
           '-mptest', $dir, '-role', $role, '-logFile', (Join-Path $dir 'player.log')) + $extra
    Start-Process -FilePath $exe -ArgumentList $a -PassThru
}

$procs = @()
$procs += Launch 'H' @('-host', '-bind', '127.0.0.1', '-name', 'Host', '-expect', '3', '-refusals')
Start-Sleep -Seconds 3
$procs += Launch 'A' @('-join', '127.0.0.1', '-name', 'Alice')
$procs += Launch 'B' @('-join', '127.0.0.1', '-name', 'Bob')
$procs += Launch 'C' @('-join', '127.0.0.1', '-name', 'Cara')
# must not get in: lobby full, other network version, nobody listening on the port
$procs += Launch 'X' @('-join', '127.0.0.1', '-name', 'Fifth', '-expecttext', 'full', '-joindelay', '14')
$procs += Launch 'V' @('-join', '127.0.0.1', '-name', 'OldBuild', '-netversion', '99', '-expecttext', 'version', '-joindelay', '16')
$Port = $Port + 1   # nothing listens here
$procs += Launch 'N' @('-join', '127.0.0.1', '-name', 'Nobody', '-expecttext', 'connect', '-joindelay', '2')

$deadline = (Get-Date).AddSeconds(400)
foreach ($p in $procs) {
    $left = [int](($deadline - (Get-Date)).TotalMilliseconds)
    if ($left -lt 1) { $left = 1 }
    if (-not $p.WaitForExit($left)) { $p.Kill(); "KILLED pid $($p.Id) (still running at the deadline)" }
}

foreach ($role in 'H', 'A', 'B', 'C', 'X', 'V', 'N') {
    $log = Join-Path $outDir "$role\player.log"
    if (-not (Test-Path $log)) { "$role : no log"; continue }
    $l = Get-Content $log
    $pass = ($l | Select-String 'MPTEST PASS').Count
    $fail = ($l | Select-String 'MPTEST FAIL').Count
    $exc = ($l | Select-String 'Exception|NET ERROR').Count
    "$role : PASS=$pass FAIL=$fail exceptions=$exc"
}
