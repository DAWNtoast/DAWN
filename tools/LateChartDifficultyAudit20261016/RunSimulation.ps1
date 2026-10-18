param(
    [ValidatePattern('^[A-Za-z0-9_-]+$')][string]$RunTag = 'Baseline',
    [ValidateRange(1,500)][int]$Seeds = 30,
    [ValidateRange(0,100000)][int]$StartSeed = 0,
    [ValidatePattern('^[A-Za-z0-9_-]+$')][string]$Scenario = 'B0',
    [ValidateSet('balanced','all')][string]$PolicySet = 'balanced',
    [ValidateRange(1,100)][int]$EntryPercent = 100,
    [ValidateSet('reference','fire','arcane','curse','tide','thunder','blood')][string]$School = 'reference',
    [string]$CandidateFile = '',
    [string]$UnityData = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data'
)
$ErrorActionPreference = 'Stop'
$noviceWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$noviceOutput = Join-Path $noviceWorkspace ('Artifacts/LateChartDifficultyAudit20261016/' + $RunTag)
$noviceSnapshot = Join-Path $noviceOutput 'CoreSnapshot'
$noviceBuild = Join-Path $noviceOutput 'Build'
New-Item -ItemType Directory -Force -Path $noviceSnapshot,$noviceBuild | Out-Null
$noviceSources = @(Get-ChildItem -LiteralPath (Join-Path $noviceWorkspace 'UnityClient/Assets/_Project/Core/Runtime') -Filter '*.cs')
$noviceSources += Get-Item -LiteralPath (Join-Path $noviceWorkspace 'UnityClient/Assets/_Project/Content/Runtime/CrewNarrativeCatalog.cs')
$noviceHashes = foreach ($noviceSource in $noviceSources) {
    $noviceCopy = Join-Path $noviceSnapshot $noviceSource.Name
    Copy-Item -LiteralPath $noviceSource.FullName -Destination $noviceCopy -Force
    $noviceHash = (Get-FileHash -LiteralPath $noviceCopy -Algorithm SHA256).Hash
    if ($noviceHash -ne (Get-FileHash -LiteralPath $noviceSource.FullName -Algorithm SHA256).Hash) { throw "Snapshot mismatch: $($noviceSource.Name)" }
    [pscustomobject]@{Source=$noviceSource.FullName;SHA256=$noviceHash}
}
$noviceHashes | Export-Csv -LiteralPath (Join-Path $noviceOutput 'source-hashes.csv') -NoTypeInformation -Encoding utf8
$noviceHarness = Join-Path $noviceBuild 'SimulateLateCharts.cs'
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'SimulateLateCharts.cs') -Destination $noviceHarness -Force
$latePolicy = Join-Path $noviceBuild 'SystemPolicy.cs'
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'SystemPolicy.cs') -Destination $latePolicy -Force
$lateSpecSource = Join-Path $noviceWorkspace 'Artifacts/LateChartDifficultyAudit20261016/school-spec.txt'
$lateSpecCopy = Join-Path $noviceBuild 'school-spec.txt'
Copy-Item -LiteralPath $lateSpecSource -Destination $lateSpecCopy -Force
$lateSpecText = Get-Content -LiteralPath $lateSpecCopy -Raw
$lateSpecMatch = [regex]::Match($lateSpecText, '(?s)\[\r?\n.*?\r?\n\]')
if (-not $lateSpecMatch.Success) { throw 'Missing school specification JSON.' }
$lateSpecs = $lateSpecMatch.Value | ConvertFrom-Json
$lateSpecTable = Join-Path $noviceBuild 'school-spec.tsv'
$lateSpecLines = @('school|minimumPirateLevel|baseIds|crewIds|sourceBoardingKeys|medicine|powder')
foreach ($lateSpec in $lateSpecs) {
    $lateSpecLines += [string]::Join('|', @($lateSpec.school, $lateSpec.minimumPirateLevel,
        ($lateSpec.baseIds -join ','), ($lateSpec.crewIds -join ','), ($lateSpec.sourceBoardingKeys -join ','),
        $lateSpec.medicine, $lateSpec.powder))
}
[IO.File]::WriteAllLines($lateSpecTable, $lateSpecLines, [Text.UTF8Encoding]::new($false))
@($noviceHarness,$latePolicy,$lateSpecCopy,$lateSpecTable) | ForEach-Object {
    [pscustomobject]@{Source=$_;SHA256=(Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash}
} | Export-Csv -LiteralPath (Join-Path $noviceOutput 'harness-hashes.csv') -NoTypeInformation -Encoding utf8
if ($CandidateFile) {
    $noviceCandidateCopy = Join-Path $noviceBuild 'candidates.csv'
    Copy-Item -LiteralPath $CandidateFile -Destination $noviceCandidateCopy -Force
} else { $noviceCandidateCopy = '-' }
$noviceExe = Join-Path $noviceBuild 'SimulateLateCharts.exe'
$noviceArgs = @((Join-Path $UnityData 'DotNetSdkRoslyn/csc.dll'), '-nologo', '-target:exe', '-langversion:9', '-noconfig', '-nostdlib', "-out:$noviceExe")
foreach ($noviceAssembly in @('mscorlib.dll', 'System.dll', 'System.Core.dll')) {
    $noviceArgs += '-r:' + (Join-Path $UnityData ('MonoBleedingEdge/lib/mono/4.7.2-api/' + $noviceAssembly))
}
$noviceArgs += @(Get-ChildItem -LiteralPath $noviceSnapshot -Filter '*.cs' | ForEach-Object FullName)
$noviceArgs += $noviceHarness
$noviceArgs += $latePolicy
& (Join-Path $UnityData 'NetCoreRuntime/dotnet.exe') @noviceArgs 2>&1 | Tee-Object -FilePath (Join-Path $noviceOutput 'compile.log')
if ($LASTEXITCODE -ne 0) { throw 'Novice simulation compilation failed.' }
& (Join-Path $UnityData 'MonoBleedingEdge/bin/mono.exe') $noviceExe $noviceOutput $Seeds $StartSeed $Scenario $PolicySet $EntryPercent $noviceCandidateCopy $School $lateSpecTable 2>&1 | Tee-Object -FilePath (Join-Path $noviceOutput 'simulation.log')
if ($LASTEXITCODE -ne 0) { throw 'Novice simulation failed.' }
