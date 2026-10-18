param(
  [string]$BuildRoot='D:/Game/Builds/第七港口-内存优化与修复20261015',
  [string]$Python='C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe'
)
$ErrorActionPreference='Stop'
$memoryRelease=[IO.Path]::GetFullPath($BuildRoot).TrimEnd('\','/')
$memoryExpected=[IO.Path]::GetFullPath('D:/Game/Builds/第七港口-内存优化与修复20261015').TrimEnd('\','/')
if($memoryRelease -ne $memoryExpected){throw 'This release directory only.'}
$memoryWindows=Join-Path $memoryRelease 'Windows'
$memoryMac=Join-Path $memoryRelease 'Mac/第七港口.app'
foreach($memoryReport in @((Join-Path $memoryWindows 'build-report.txt'),(Join-Path $memoryRelease 'Mac/build-report.txt'))){
  $memoryReportText=Get-Content -LiteralPath $memoryReport -Raw
  if($memoryReportText -notmatch '(?m)^Result: Succeeded\r?$' -or $memoryReportText -notmatch '(?m)^Version: 0\.3\.20261015\.1\r?$' -or $memoryReportText -notmatch '(?m)^Errors: 0\r?$'){throw ('Wrong or unsuccessful release report: '+$memoryReport)}
}
$memoryDiagnostics=@(Get-ChildItem -LiteralPath $memoryRelease -File -Recurse | Where-Object {$_.Name -match 'RuntimeMemoryAudit|MemoryAudit|StartupMemoryAudit|PlayerPerformanceAudit'})
if($memoryDiagnostics.Count){throw 'Diagnostic assembly or diagnostic artifact found in release.'}
$memoryEvidence='D:/Game/Artifacts/MemoryRelease20261015/Packaging'
if(Test-Path -LiteralPath $memoryEvidence){throw 'Preserve earlier packaging evidence.'}
New-Item -ItemType Directory -Path $memoryEvidence|Out-Null
& 'D:/Game/tools/ReleaseBuild/Package.ps1' -BuildDirectory $memoryWindows -ZipPath (Join-Path $memoryRelease '第七港口-Windows-20261015.zip') -ExecutableName '第七港口.exe' *> (Join-Path $memoryEvidence 'windows-package.log')
if(-not(Test-Path -LiteralPath (Join-Path $memoryRelease '第七港口-Windows-20261015.zip'))){throw 'Windows ZIP missing.'}
if(-not(Test-Path -LiteralPath $Python -PathType Leaf)){throw 'Python runtime missing.'}
& $Python 'D:/Game/tools/ReleaseBuild/PackageMac.py' --app-directory $memoryMac --zip-path (Join-Path $memoryRelease '第七港口-macOS-20261015.zip') *> (Join-Path $memoryEvidence 'mac-package.json')
if($LASTEXITCODE -ne 0){throw 'macOS ZIP validation failed.'}
$memoryMacPackage=Get-Content -LiteralPath (Join-Path $memoryEvidence 'mac-package.json') -Raw|ConvertFrom-Json
if($memoryMacPackage.result -ne 'PASS' -or $memoryMacPackage.bundle.shortVersion -ne '0.3.20261015.1'){throw 'Actual macOS bundle version does not match the release.'}
$memoryPackages=@(foreach($memoryZip in @('第七港口-Windows-20261015.zip','第七港口-macOS-20261015.zip')){$memoryFile=Get-Item -LiteralPath (Join-Path $memoryRelease $memoryZip);[pscustomobject]@{Name=$memoryZip;Bytes=$memoryFile.Length;SHA256=(Get-FileHash -LiteralPath $memoryFile.FullName).Hash}})
[pscustomobject]@{Version='0.3.20261015.1';DiagnosticAssemblies=0;Packages=$memoryPackages;MacExecution='Not executed on macOS; bundle/Mach-O/ZIP integrity checked on Windows.'}|ConvertTo-Json -Depth 4|Set-Content -LiteralPath (Join-Path $memoryEvidence 'release-packages.json') -Encoding utf8
Get-Content -LiteralPath (Join-Path $memoryEvidence 'release-packages.json')
