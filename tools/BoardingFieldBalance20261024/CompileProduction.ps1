param(
    [string]$ReferenceResponses = 'D:/Game/Artifacts/CardNumbering20261017/UiCompile',
    [string]$UnityData = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data',
    [switch]$PrepareOnly
)
$ErrorActionPreference = 'Stop'
$boardingWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$boardingProject = Join-Path $boardingWorkspace 'UnityClient'
$boardingCompile = Join-Path $boardingWorkspace 'Artifacts/BoardingFieldBalance20261024/Compile'
$boardingReferences = [IO.Path]::GetFullPath($ReferenceResponses).Replace('\','/')
$boardingOutputPath = $boardingCompile.Replace('\','/')
$boardingAssemblies = @('Core','Content','Persistence','Presentation')
$boardingSourceFiles = @()
New-Item -ItemType Directory -Force -Path $boardingCompile | Out-Null

foreach ($boardingAssembly in $boardingAssemblies) {
    $boardingResponse = [IO.File]::ReadAllLines((Join-Path $ReferenceResponses ($boardingAssembly + '.rsp'))) |
        ForEach-Object { $_.Replace($boardingReferences, $boardingOutputPath) }
    $boardingSourcePattern = '^"?(?:' + [Regex]::Escape($boardingProject.Replace('\','/') + '/') + ')?Assets/_Project/' + $boardingAssembly + '/Runtime/.*\.cs"?$'
    $boardingResponse = @($boardingResponse | Where-Object { $_ -notmatch $boardingSourcePattern })
    $boardingSources = @(Get-ChildItem -LiteralPath (Join-Path $boardingProject ('Assets/_Project/' + $boardingAssembly + '/Runtime')) -File -Filter '*.cs' -Recurse | Sort-Object FullName)
    if ($boardingSources.Count -eq 0) { throw ('No production sources found for ' + $boardingAssembly) }
    $boardingSourceFiles += $boardingSources
    $boardingResponse += @($boardingSources | ForEach-Object { '"' + $_.FullName.Replace('\','/') + '"' })
    if ($boardingResponse | Where-Object { $_.Contains($boardingReferences) }) { throw 'The response still references the old output directory.' }
    foreach ($boardingOutput in @($boardingResponse | Where-Object { $_ -match '^-(out|refout):' })) {
        if (-not $boardingOutput.Contains($boardingOutputPath + '/')) { throw ('Unexpected compiler output path: ' + $boardingOutput) }
    }
    [IO.File]::WriteAllLines((Join-Path $boardingCompile ($boardingAssembly + '.rsp')), $boardingResponse, [Text.UTF8Encoding]::new($false))
}

$boardingHashes = @($boardingSourceFiles | ForEach-Object {
    [pscustomobject]@{ Source = $_.FullName; SHA256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
})
$boardingHashes | Export-Csv -LiteralPath (Join-Path $boardingCompile 'source-hashes.csv') -NoTypeInformation -Encoding utf8
if ($PrepareOnly) { Write-Output ('Prepared production responses: ' + $boardingCompile); return }

$boardingResults = [Collections.Generic.List[string]]::new()
Push-Location $boardingProject
try {
    foreach ($boardingAssembly in $boardingAssemblies) {
        $boardingResponsePath = Join-Path $boardingCompile ($boardingAssembly + '.rsp')
        & (Join-Path $UnityData 'NetCoreRuntime/dotnet.exe') (Join-Path $UnityData 'DotNetSdkRoslyn/csc.dll') ('@' + $boardingResponsePath) 2>&1 |
            Tee-Object -FilePath (Join-Path $boardingCompile ($boardingAssembly + '.log'))
        if ($LASTEXITCODE -ne 0) { throw ('Production compilation failed: ' + $boardingAssembly) }
        $boardingResults.Add('PASS ' + $boardingAssembly)
        Write-Output ('PASS ' + $boardingAssembly)
    }
} finally { Pop-Location }

foreach ($boardingHash in $boardingHashes) {
    if ((Get-FileHash -LiteralPath $boardingHash.Source -Algorithm SHA256).Hash -ne $boardingHash.SHA256) {
        throw ('Source changed during production compilation; rerun when edits finish: ' + $boardingHash.Source)
    }
}
$boardingResults.Add('PASS source hashes stable across all four assemblies')
$boardingResults.Add('No Unity Editor, isolated project, scene, or preview generator was launched.')
[IO.File]::WriteAllLines((Join-Path $boardingCompile 'validation.txt'), $boardingResults, [Text.UTF8Encoding]::new($false))
Write-Output ('PASS production validation: ' + $boardingCompile)
