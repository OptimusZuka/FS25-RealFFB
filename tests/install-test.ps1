# FS25 RealFFB - automated installer test on a clean Windows machine (GitHub Actions).
# Real software, no steering wheel: it checks the installer, the HidHide configuration, idempotency,
# preservation of other programs' HidHide entries, the uninstaller and the official HidHide removal.
# Wheel identification itself is covered by tests\SetupTests.cs (simulated devices).
$ErrorActionPreference = 'Continue'
$fails = 0
function Check([string]$name, [bool]$ok, [string]$detail = '') {
    if ($ok) { Write-Host "PASS  $name $detail" } else { Write-Host "FAIL  $name $detail"; $script:fails++ }
}
function HH { param([string[]]$a) $c = Get-HidHideCli; if (-not $c) { return '' }; [string](& $c @a 2>&1 | Out-String) }
# RealFFB.Setup.exe is a Windows (GUI) program: PowerShell does not wait for it unless Start-Process -Wait is used
function RunSetup([string]$exe, [string]$argline, [int]$timeoutSec = 120) {
    $p = Start-Process $exe -ArgumentList $argline -PassThru
    if (-not $p.WaitForExit($timeoutSec * 1000)) { try { $p.Kill() } catch {}; Write-Host "   TIMEOUT: $argline"; return -1 }
    return $p.ExitCode
}
function Get-HidHideCli {
    foreach ($p in @("$env:ProgramFiles\Nefarius Software Solutions\HidHide\x64\HidHideCLI.exe", "$env:ProgramFiles\Nefarius Software Solutions\HidHide\HidHideCLI.exe")) { if (Test-Path $p) { return $p } }
    return $null
}
function Apps { HH @('--app-list') }
function Devs { HH @('--dev-list') }
function Ini([string]$file, [string]$key) { $l = Get-Content $file -ErrorAction SilentlyContinue | Where-Object { $_ -like "$key=*" } | Select-Object -First 1; if ($l) { $l.Substring($key.Length + 1) } else { '' } }
function HidHideEntries {
    foreach ($r in @('HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall', 'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall')) {
        Get-ChildItem $r -ErrorAction SilentlyContinue | ForEach-Object { $p = Get-ItemProperty $_.PSPath; if ($p.DisplayName -like '*HidHide*') { $p } }
    }
}

$setupExe = (Get-ChildItem dist\FS25_RealFFB_Setup_*.exe | Select-Object -First 1).FullName
Check 'installer was built' ($setupExe -ne $null) $setupExe

# fake FS25 install (an empty exe with the game's name) and a game.xml like the game writes it
$game = "$env:ProgramFiles\Farming Simulator 2025"
New-Item -ItemType Directory -Force $game | Out-Null
Copy-Item "$env:WINDIR\System32\notepad.exe" "$game\FarmingSimulator2025.exe" -Force
$prof = Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'My Games\FarmingSimulator2025'
New-Item -ItemType Directory -Force "$prof\mods" | Out-Null
$gxOrig = "<?xml version=`"1.0`" encoding=`"utf-8`" standalone=`"no`" ?>`r`n<game>`r`n    <joystick enable=`"false`" vibration=`"true`"/>`r`n</game>`r`n"
[IO.File]::WriteAllText("$prof\game.xml", $gxOrig)
$state = "$env:APPDATA\FS25 RealFFB\install-state.ini"

Write-Host "`n== 1. Fresh install, HidHide not installed =="
Check 'HidHide absent before' ((Get-HidHideCli) -eq $null)
$p = Start-Process $setupExe -ArgumentList '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/LOG=install1.log' -PassThru -Wait
Check 'installer exit code 0' ($p.ExitCode -eq 0) "($($p.ExitCode))"
Get-Content install1.log | Select-String 'Helper ' | ForEach-Object { Write-Host "   $_" }
$appExe = @("$env:LOCALAPPDATA\Programs\FS25 RealFFB\RealFFB.exe", "$env:ProgramFiles\FS25 RealFFB\RealFFB.exe") | Where-Object { Test-Path $_ } | Select-Object -First 1
Check 'RealFFB.exe installed' ($appExe -ne $null) $appExe
$appDir = Split-Path $appExe
Check 'HidHide installed by the installer (official, signature checked)' ((Get-HidHideCli) -ne $null)
Check 'state: HidHide installed by us' ((Ini $state 'HH_InstalledByUs') -eq '1')
if ((Ini $state 'PendingResume') -eq '1') {
    Write-Host '   HidHide asked for a restart: running the resumed configuration now'
    RunSetup "$appDir\RealFFB.Setup.exe" 'configure --quiet --out r.ini' | Out-Null; Get-Content r.ini | ForEach-Object { Write-Host "   $_" }
}
$apps = Apps
Write-Host $apps
Check 'RealFFB.exe allowed in HidHide without a wheel connected' ($apps -like "*$appExe*")
Check 'last setup result = no-wheel (no hardware on this machine)' ((Ini $state 'HH_LastResult') -eq 'no-wheel') (Ini $state 'HH_LastResult')
$gx = Get-Content "$prof\game.xml" -Raw
Check 'game.xml patched (joystick on, game FFB off)' (($gx -like '*joystick enable="true"*') -and ($gx -like '*forceFeedback enable="false"*'))

Write-Host "`n== 2. Other programs' HidHide entries + repeated configuration (idempotent) =="
$cli = Get-HidHideCli
& $cli --app-reg "C:\Tools\OtherApp.exe" | Out-Null
& $cli --dev-hide "HID\VID_1234&PID_5678\7&OTHER&0&0000" | Out-Null
# an entry that FS25 RealFFB added earlier (as after a real wheel setup), recorded in the state
& $cli --dev-hide "HID\VID_346E&PID_0004\8&RFFBTEST&0&0000" | Out-Null
Add-Content $state "HH_DevAdded=HID\VID_346E&PID_0004\8&RFFBTEST&0&0000"
for ($i = 1; $i -le 3; $i++) { $c = RunSetup "$appDir\RealFFB.Setup.exe" "configure --quiet --out r$i.ini"; Write-Host "   run $i -> $c status=$(Ini "r$i.ini" 'status')" }
$apps = Apps
$n = ([regex]::Matches($apps, [regex]::Escape($appExe))).Count
Check 'still exactly one RealFFB.exe entry after 3 runs' ($n -eq 1) "($n)"
Check 'other application kept' ($apps -like '*OtherApp.exe*')
Check 'other device kept' ((Devs) -like '*VID_1234&PID_5678*')

Write-Host "`n== 3. Diagnostic report =="
RunSetup "$appDir\RealFFB.Setup.exe" "diagnose --report `"$PWD\diag.txt`" --quiet --out d.ini" | Out-Null
$diag = Get-Content diag.txt -Raw
Write-Host $diag
Check 'diag: HidHide installed OK' ($diag -match '\[OK\] HidHide installed')
Check 'diag: driver responding' ($diag -match '\[OK\] HidHide driver')
Check 'diag: whitelist OK' ($diag -match '\[OK\] FS25 RealFFB whitelist')
Check 'diag: no wheel reported as a hardware problem, not a configuration one' ($diag -match 'Steering wheel detected: no force feedback wheel base found by Windows -> hardware')
Check 'diag: no user name' (-not ($diag -match [regex]::Escape($env:USERNAME)))

Write-Host "`n== 4. Uninstall, keeping HidHide =="
$un = Get-ChildItem $appDir -Filter 'unins*.exe' | Select-Object -First 1
$p = Start-Process $un.FullName -ArgumentList '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART' -PassThru -Wait
Start-Sleep -Seconds 3
Check 'uninstaller exit code 0' ($p.ExitCode -eq 0) "($($p.ExitCode))"
$apps = Apps; $devs = Devs
Write-Host $apps; Write-Host $devs
Check 'RealFFB.exe removed from HidHide' (-not ($apps -like '*RealFFB.exe*'))
Check 'device added by FS25 RealFFB unhidden' (-not ($devs -like '*RFFBTEST*'))
Check 'other application still allowed' ($apps -like '*OtherApp.exe*')
Check 'other device still hidden' ($devs -like '*VID_1234&PID_5678*')
Check 'HidHide still installed' ((Get-HidHideCli) -ne $null)
$gxNow = [IO.File]::ReadAllText("$prof\game.xml")
Check 'game.xml restored' ($gxNow -eq $gxOrig)
if ($gxNow -ne $gxOrig) { Write-Host "--- game.xml now:"; Write-Host $gxNow; Get-Content "$env:APPDATA\FS25 RealFFB\setup_log.txt" -ErrorAction SilentlyContinue | Select-Object -Last 30 | ForEach-Object { Write-Host "   log: $_" } }

Write-Host "`n== 5. Reinstall with HidHide already installed and configured =="
$p = Start-Process $setupExe -ArgumentList '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/LOG=install2.log' -PassThru -Wait
Check 'installer exit code 0' ($p.ExitCode -eq 0) "($($p.ExitCode))"
Get-Content install2.log | Select-String 'Helper ' | ForEach-Object { Write-Host "   $_" }
$apps = Apps
Check 'RealFFB.exe allowed again' ($apps -like "*$appExe*")
Check 'other application untouched' ($apps -like '*OtherApp.exe*')
Check 'HidHide marked as already present' ((Ini $state 'HH_Preexisting') -eq '1')

Write-Host "`n== 6. Official HidHide removal (the uninstaller's 'Yes, uninstall HidHide' path) =="
HidHideEntries | ForEach-Object { Write-Host "   entry: $($_.DisplayName) $($_.DisplayVersion) | $($_.UninstallString) | SystemComponent=$($_.SystemComponent)" }
& $cli --cloak-on | Out-Null
$r = (Resolve-Path dist).Path + '\hh.ini'
$c = RunSetup ((Resolve-Path dist\app).Path + '\RealFFB.Setup.exe') "hidhide-uninstall --out `"$r`"" 300
Write-Host "   exit $c"
Get-Content $r -ErrorAction SilentlyContinue | ForEach-Object { Write-Host "   $_" }
$st = Ini $r 'status'
Check 'HidHide removed (or removed pending restart)' ($st -eq 'removed' -or $st -eq 'reboot') $st
Check 'HidHide uninstall entry gone' ((@(HidHideEntries)).Count -eq 0)

Write-Host "`n== 7. Final uninstall of FS25 RealFFB without HidHide =="
$un = Get-ChildItem $appDir -Filter 'unins*.exe' | Select-Object -First 1
$p = Start-Process $un.FullName -ArgumentList '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART' -PassThru -Wait
Check 'uninstaller exit code 0 with HidHide already removed' ($p.ExitCode -eq 0) "($($p.ExitCode))"
Check 'app folder removed' (-not (Test-Path $appExe))

Write-Host "`n$fails failure(s)"
exit $fails
