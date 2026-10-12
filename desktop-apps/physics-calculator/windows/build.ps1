# Builds PhysicsCalculator.exe with the C# compiler included in Windows (.NET Framework 4.x).
# First checks the engine against ..\tests.tsv (results recorded from the web calculator).
# Usage: powershell -ExecutionPolicy Bypass -File build.ps1
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$common = @('/nologo', '/codepage:65001', '/optimize+', '/r:System.Numerics.dll')

# 1. Engine must match the web calculator.
$test = Join-Path $env:TEMP 'PhysicsCalculator-selftest.exe'
& $csc @common '/target:exe' "/out:$test" 'Engine.cs' 'SelfTest.cs'
if ($LASTEXITCODE -ne 0) { throw 'Compile failed (self-test)' }
& $test '..\tests.tsv'
$failed = $LASTEXITCODE
Remove-Item $test -ErrorAction SilentlyContinue
if ($failed -ne 0) { throw 'The engine does not match the web calculator' }

# 2. Build a throwaway copy in %TEMP% and use it to draw the icon file.
$gui = @('/target:winexe', '/r:System.Windows.Forms.dll', '/r:System.Drawing.dll')
$maker = Join-Path $env:TEMP 'PhysicsCalculator-icon-maker.exe'
& $csc @common @gui "/out:$maker" 'Engine.cs' 'App.cs'
if ($LASTEXITCODE -ne 0) { throw 'Compile failed' }
Start-Process -FilePath $maker -ArgumentList '--write-icon', "`"$PSScriptRoot\app.ico`"" -Wait
Remove-Item $maker -ErrorAction SilentlyContinue

# 3. The real build, with the icon embedded.
& $csc @common @gui '/win32icon:app.ico' '/out:PhysicsCalculator.exe' 'Engine.cs' 'App.cs'
if ($LASTEXITCODE -ne 0) { throw 'Compile failed' }
Write-Host 'Built PhysicsCalculator.exe'
