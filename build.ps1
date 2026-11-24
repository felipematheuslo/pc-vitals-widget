$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

if (-not (Test-Path $csc)) {
    throw ".NET Framework 4.x compiler not found at $csc"
}

$outExe = Join-Path $root "PCVitalsWidget.exe"

& $csc /nologo /optimize+ /platform:x64 /target:winexe `
    /win32manifest:"$root\src\app.manifest" `
    /r:"$root\lib\LibreHardwareMonitorLib.dll" `
    /r:"$root\lib\HidSharp.dll" `
    /r:System.Management.dll `
    /r:System.Windows.Forms.dll `
    /r:System.Drawing.dll `
    /resource:"$root\lib\LibreHardwareMonitorLib.dll",LibreHardwareMonitorLib.dll `
    /resource:"$root\lib\HidSharp.dll",HidSharp.dll `
    /out:"$outExe" `
    "$root\src\Program.cs"

if ($LASTEXITCODE -ne 0) {
    throw "Build failed with exit code $LASTEXITCODE"
}

Write-Host "Built standalone executable: $outExe" -ForegroundColor Green
