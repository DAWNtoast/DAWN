$ErrorActionPreference = 'Stop'
$startupCaptureRoot = 'D:\Game\Artifacts\StartupMemory20261011'
$startupCaptureProject = 'D:\Game\tmp\startup-memory-20261011'
$startupCaptureSource = Join-Path $PSScriptRoot 'StartupMemoryProbe.cs'
Copy-Item -LiteralPath $startupCaptureSource -Destination (Join-Path $startupCaptureProject 'Assets\StartupMemoryAudit\StartupMemoryProbe.cs')
$startupCaptureArguments = @('-batchmode','-quit','-projectPath',$startupCaptureProject,'-logFile',(Join-Path $startupCaptureRoot 'CaptureProbeCompile.log'))
$startupCaptureQuoted = $startupCaptureArguments | ForEach-Object { '"' + $_.Replace('"','\"') + '"' }
$startupCaptureProcess = Start-Process -FilePath 'D:\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe' -ArgumentList $startupCaptureQuoted -WindowStyle Hidden -PassThru
$startupCaptureProcess.WaitForExit()
if ($startupCaptureProcess.ExitCode -ne 0) { throw 'Capture probe compilation failed.' }
$startupCaptureAssembly = Join-Path $startupCaptureProject 'Library\ScriptAssemblies\ShatteredSail.StartupMemoryAudit.dll'
$startupCaptureSourceHash = (Get-FileHash -LiteralPath $startupCaptureSource).Hash
$startupCaptureAssemblyHash = (Get-FileHash -LiteralPath $startupCaptureAssembly).Hash
foreach ($startupCaptureVariant in @('Baseline','Optimized')) {
    Copy-Item -LiteralPath $startupCaptureAssembly -Destination (Join-Path $startupCaptureRoot ($startupCaptureVariant + '\Windows\StartupAudit_Data\Managed\ShatteredSail.StartupMemoryAudit.dll'))
}
[pscustomobject]@{Purpose='Visual QA only, after the twelve completed VisibleTrials. Optional screenshot after inventory/JSON, normal production camera/canvas unchanged.';SourceSHA256=$startupCaptureSourceHash;ProbeSHA256=$startupCaptureAssemblyHash;MeasuredProbeSHA256=(Get-FileHash -LiteralPath (Join-Path $startupCaptureRoot 'MeasuredProbe\ShatteredSail.StartupMemoryAudit.dll')).Hash} | ConvertTo-Json | Set-Content (Join-Path $startupCaptureRoot 'capture-probe-parity.json') -Encoding utf8
foreach ($startupCaptureVariant in @('Baseline','Optimized')) {
    & (Join-Path $PSScriptRoot 'Measure.ps1') -Executable (Join-Path $startupCaptureRoot ($startupCaptureVariant + '\Windows\StartupAudit.exe')) -OutputDirectory (Join-Path $startupCaptureRoot ('NormalScreenshots\' + $startupCaptureVariant)) -Fixture Empty -Visible -Capture
}
