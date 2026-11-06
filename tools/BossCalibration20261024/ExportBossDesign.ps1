param(
    [string]$CoreSource = '',
    [string]$Output = '',
    [string]$UnityData = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data'
)
$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (!$CoreSource) { $CoreSource = Join-Path $taskRoot 'UnityClient/Assets/_Project/Core/Runtime' }
if (!$Output) { $Output = Join-Path $taskRoot 'outputs/boss-calibration-20261024/VerifiedDesign' }
$CoreSource = [IO.Path]::GetFullPath($CoreSource)
$Output = [IO.Path]::GetFullPath($Output)
$taskSnapshot = Join-Path $Output 'CoreSnapshot'
if ($CoreSource.TrimEnd('\','/') -eq $taskSnapshot.TrimEnd('\','/')) { throw 'Choose an output separate from the input snapshot.' }
$taskFiles = @(Get-ChildItem -LiteralPath $CoreSource -Filter '*.cs' -File | Sort-Object Name)
if ($taskFiles.Count -lt 140) { throw 'Incomplete Core source directory.' }
New-Item -ItemType Directory -Force -Path $taskSnapshot | Out-Null
$taskHashes = @($taskFiles | ForEach-Object {
    [pscustomobject]@{ Source=$_.FullName; Snapshot=(Join-Path $taskSnapshot $_.Name); SHA256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
})
foreach ($taskHash in $taskHashes) {
    Copy-Item -LiteralPath $taskHash.Source -Destination $taskHash.Snapshot -Force
    if ((Get-FileHash -LiteralPath $taskHash.Snapshot -Algorithm SHA256).Hash -ne $taskHash.SHA256) { throw 'Source changed while snapshotting.' }
}
$taskHashes | Export-Csv -LiteralPath (Join-Path $Output 'source-hashes.csv') -NoTypeInformation -Encoding utf8
$taskExporter = Join-Path $PSScriptRoot 'ExportBossDesign.cs'
$taskExporterHash = (Get-FileHash -LiteralPath $taskExporter -Algorithm SHA256).Hash
Copy-Item -LiteralPath $taskExporter -Destination (Join-Path $Output 'ExportBossDesign.cs') -Force
$taskExe = Join-Path $Output 'ExportBossDesign.exe'
$taskArgs = @("$UnityData/DotNetSdkRoslyn/csc.dll",'-nologo','-target:exe','-main:ExportBossDesign','-langversion:9','-noconfig','-nostdlib',"-out:$taskExe")
foreach ($taskAssembly in @('mscorlib.dll','System.dll','System.Core.dll')) { $taskArgs += "-r:$UnityData/MonoBleedingEdge/lib/mono/4.7.2-api/$taskAssembly" }
$taskArgs += @($taskHashes.Snapshot)
$taskArgs += (Join-Path $Output 'ExportBossDesign.cs')
& "$UnityData/NetCoreRuntime/dotnet.exe" @taskArgs 2>&1 | Tee-Object -FilePath (Join-Path $Output 'compile.log')
if ($LASTEXITCODE -ne 0) { throw 'Boss design export compilation failed.' }
& "$UnityData/MonoBleedingEdge/bin/mono.exe" $taskExe $Output $CoreSource 2>&1 | Tee-Object -FilePath (Join-Path $Output 'export.log')
if ($LASTEXITCODE -ne 0) { throw 'Boss design export failed.' }
foreach ($taskHash in $taskHashes) {
    if ((Get-FileHash -LiteralPath $taskHash.Source -Algorithm SHA256).Hash -ne $taskHash.SHA256) { throw ('Source changed during export: '+$taskHash.Source) }
    if ((Get-FileHash -LiteralPath $taskHash.Snapshot -Algorithm SHA256).Hash -ne $taskHash.SHA256) { throw 'Frozen snapshot changed during export.' }
}
if ((Get-FileHash -LiteralPath $taskExporter -Algorithm SHA256).Hash -ne $taskExporterHash) { throw 'Exporter changed during export.' }
$taskData = Get-Content -LiteralPath (Join-Path $Output 'boss-design.json') -Raw | ConvertFrom-Json
$taskProfiles = @(Import-Csv -LiteralPath (Join-Path $Output 'boss-design.csv'))
$taskCycles = @(Import-Csv -LiteralPath (Join-Path $Output 'boss-cycles.csv'))
if ($taskData.bosses.Count -ne 13 -or $taskProfiles.Count -ne 13 -or $taskCycles.Count -ne 52) { throw 'Export row count mismatch.' }
$taskHtml = [IO.File]::ReadAllText((Join-Path $Output '海战Boss设计.html'))
if ([regex]::Matches($taskHtml,'<section id=').Count -ne 13 -or [regex]::Matches($taskHtml,'class="cycle"').Count -ne 13) { throw 'HTML structure mismatch.' }
foreach ($taskBoss in $taskData.bosses) {
    if ($taskBoss.cycles.Count -ne 4) { throw 'Missing cycle rows.' }
    $taskCsvProfile = @($taskProfiles | Where-Object id -eq $taskBoss.id)
    if ($taskCsvProfile.Count -ne 1 -or [int]$taskCsvProfile[0].hull -ne $taskBoss.hull) { throw 'Profile CSV mismatch.' }
    if (@($taskCycles | Where-Object bossId -eq $taskBoss.id).Count -ne 4) { throw 'Cycle CSV identity mismatch.' }
}
$taskOutputNames = @('boss-design.json','boss-design.csv','boss-cycles.csv','海战Boss设计.html','海战Boss设计.md')
@($taskOutputNames | ForEach-Object { [pscustomobject]@{ File=$_; SHA256=(Get-FileHash -LiteralPath (Join-Path $Output $_) -Algorithm SHA256).Hash } }) |
    Export-Csv -LiteralPath (Join-Path $Output 'output-hashes.csv') -NoTypeInformation -Encoding utf8
[IO.File]::WriteAllLines((Join-Path $Output 'validation.txt'),@(
    'PASS 13 profiles / 52 cycle rows in JSON and CSV.',
    'PASS 13 HTML sections and 13 four-step tables.',
    'PASS original and frozen Core source hashes remained stable.',
    ('PASS exporter SHA256 '+$taskExporterHash),
    'No production sources, card catalogs, scenes or browser were modified.',
    'This export documents authored definitions, not measured win rates or pacing.'
),[Text.UTF8Encoding]::new($false))
Write-Output ('PASS Boss design export: '+$Output)
