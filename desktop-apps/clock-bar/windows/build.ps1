# Builds ClockBar.exe with the C# compiler included in Windows (.NET Framework 4.x).
# First checks that the bar draws, with ClockBar.exe --render, and leaves the picture in bar-preview.png.
# Usage: powershell -ExecutionPolicy Bypass -File build.ps1
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$common = @('/nologo', '/codepage:65001', '/target:winexe', '/optimize+', '/r:System.Windows.Forms.dll', '/r:System.Drawing.dll')

# Pass 1: build a throwaway copy in %TEMP%, draw the bar and the icon file with it.
$maker = Join-Path $env:TEMP 'ClockBar-build.exe'
& $csc @common "/out:$maker" 'Program.cs'
if ($LASTEXITCODE -ne 0) { throw 'Compile failed' }
$test = Start-Process -FilePath $maker -ArgumentList '--render', "`"$PSScriptRoot\bar-preview.png`"" -Wait -PassThru
if ($test.ExitCode -ne 0 -or -not (Test-Path 'bar-preview.png')) { throw 'Drawing the bar failed' }
Write-Host 'Bar drawn: bar-preview.png'
Start-Process -FilePath $maker -ArgumentList '--write-icon', "`"$PSScriptRoot\app.ico`"" -Wait
Remove-Item $maker -ErrorAction SilentlyContinue

# Pass 2: the real build, with the icon embedded.
& $csc @common '/win32icon:app.ico' '/out:ClockBar.exe' 'Program.cs'
if ($LASTEXITCODE -ne 0) { throw 'Compile failed' }
Write-Host 'Built ClockBar.exe'
