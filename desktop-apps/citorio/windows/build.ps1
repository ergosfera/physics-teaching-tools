# Builds Citorio.exe with the C# compiler included in Windows (.NET Framework 4.x).
# The quote lists in ..\quotes are built into the .exe. First checks that they read correctly and
# that a quote draws, with Citorio.exe --render, and leaves the picture in quote-preview.png.
# Usage: powershell -ExecutionPolicy Bypass -File build.ps1
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$common = @('/nologo', '/codepage:65001', '/target:winexe', '/optimize+', '/r:System.Windows.Forms.dll', '/r:System.Drawing.dll', '/r:System.Core.dll')
foreach ($f in Get-ChildItem '..\quotes\*.txt') { $common += "/resource:..\quotes\$($f.Name),Quotes.$($f.Name)" }

# Pass 1: build a throwaway copy in %TEMP%, check the lists and draw a quote and the icon file with it.
$maker = Join-Path $env:TEMP 'Citorio-build.exe'
& $csc @common "/out:$maker" 'Program.cs'
if ($LASTEXITCODE -ne 0) { throw 'Compile failed' }
$quotes = (Resolve-Path '..\quotes').Path
$test = Start-Process -FilePath $maker -ArgumentList '--render', "`"$PSScriptRoot\quote-preview.png`"", "`"$quotes`"" -Wait -PassThru
if ($test.ExitCode -ne 0 -or -not (Test-Path 'quote-preview.png')) { throw 'Reading the lists or drawing a quote failed' }
Write-Host 'Quote drawn: quote-preview.png'
Start-Process -FilePath $maker -ArgumentList '--write-icon', "`"$PSScriptRoot\app.ico`"" -Wait
Remove-Item $maker -ErrorAction SilentlyContinue

# Pass 2: the real build, with the icon embedded.
& $csc @common '/win32icon:app.ico' '/out:Citorio.exe' 'Program.cs'
if ($LASTEXITCODE -ne 0) { throw 'Compile failed' }
Write-Host 'Built Citorio.exe'
