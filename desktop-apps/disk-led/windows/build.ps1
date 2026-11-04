# Builds DiskLED.exe with the C# compiler included in Windows (.NET Framework 4.x).
# First checks that the drive activity counters can be read with DiskLED.exe --selftest,
# and draws the graph into graph-preview.png.
# Usage: powershell -ExecutionPolicy Bypass -File build.ps1
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$common = @('/nologo', '/codepage:65001', '/target:winexe', '/optimize+', '/r:System.Windows.Forms.dll', '/r:System.Drawing.dll')

# Pass 1: build a throwaway copy in %TEMP%, run the self-test and draw the graph and the icon file with it.
$maker = Join-Path $env:TEMP 'DiskLED-build.exe'
& $csc @common "/out:$maker" 'Program.cs'
if ($LASTEXITCODE -ne 0) { throw 'Compile failed' }
$test = Start-Process -FilePath $maker -ArgumentList '--selftest' -Wait -PassThru
# Some virtual machines hide the drive counters; that shouldn't stop the build, but it should be seen
if ($test.ExitCode -ne 0) { Write-Warning 'Self-test: no drive activity counters could be read on this machine' } else { Write-Host 'Self-test passed' }
Start-Process -FilePath $maker -ArgumentList '--render-graph', "`"$PSScriptRoot\graph-preview.png`"" -Wait
if (-not (Test-Path 'graph-preview.png')) { throw 'Drawing the graph failed' }
Start-Process -FilePath $maker -ArgumentList '--write-icon', "`"$PSScriptRoot\app.ico`"" -Wait
Remove-Item $maker -ErrorAction SilentlyContinue

# Pass 2: the real build, with the icon embedded.
& $csc @common '/win32icon:app.ico' '/out:DiskLED.exe' 'Program.cs'
if ($LASTEXITCODE -ne 0) { throw 'Compile failed' }
Write-Host 'Built DiskLED.exe'
