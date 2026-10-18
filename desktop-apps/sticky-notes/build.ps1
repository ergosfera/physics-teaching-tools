# Builds StickyNotes.exe with the C# compiler included in Windows (.NET Framework 4.x).
# First checks the note file format (shared with the Mac app) with StickyNotes.exe --selftest.
# Usage: powershell -ExecutionPolicy Bypass -File build.ps1
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$common = @('/nologo', '/target:winexe', '/optimize+', '/r:System.Windows.Forms.dll', '/r:System.Drawing.dll')

# Pass 1: build a throwaway copy in %TEMP%, run the self-test and draw the icon file with it.
$maker = Join-Path $env:TEMP 'StickyNotes-build.exe'
& $csc @common "/out:$maker" 'Program.cs'
if ($LASTEXITCODE -ne 0) { throw 'Compile failed' }
$test = Start-Process -FilePath $maker -ArgumentList '--selftest' -Wait -PassThru
if ($test.ExitCode -ne 0) { throw "Note format self-test failed ($($test.ExitCode) cases)" }
Write-Host 'Note format self-test passed'
Start-Process -FilePath $maker -ArgumentList '--write-icon', "`"$PSScriptRoot\app.ico`"" -Wait
Remove-Item $maker -ErrorAction SilentlyContinue

# Pass 2: the real build, with the icon embedded.
& $csc @common '/win32icon:app.ico' '/out:StickyNotes.exe' 'Program.cs'
if ($LASTEXITCODE -ne 0) { throw 'Compile failed' }
Write-Host 'Built StickyNotes.exe'
