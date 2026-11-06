param(
    [string]$ReferenceResponses = 'D:/Game/Artifacts/CardNumbering20261017/UiCompile',
    [string]$UnityData = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data',
    [switch]$PrepareOnly
)
$ErrorActionPreference = 'Stop'
$tideWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$tideProject = Join-Path $tideWorkspace 'UnityClient'
$tideCompile = Join-Path $tideWorkspace 'Artifacts/TideRules20261024/Compile'
$tideReferences = [IO.Path]::GetFullPath($ReferenceResponses).Replace('\','/')
$tideOutputPath = $tideCompile.Replace('\','/')
$tideAssemblies = @('Core','Content','Persistence','Presentation')
$tideSourceFiles = @()
New-Item -ItemType Directory -Force -Path $tideCompile | Out-Null

foreach ($tideAssembly in $tideAssemblies) {
    $tideResponse = [IO.File]::ReadAllLines((Join-Path $ReferenceResponses ($tideAssembly + '.rsp'))) |
        ForEach-Object { $_.Replace($tideReferences, $tideOutputPath) }
    $tideSourcePattern = '^"?(?:' + [Regex]::Escape($tideProject.Replace('\','/') + '/') + ')?Assets/_Project/' + $tideAssembly + '/Runtime/.*\.cs"?$'
    $tideResponse = @($tideResponse | Where-Object { $_ -notmatch $tideSourcePattern })
    $tideSources = @(Get-ChildItem -LiteralPath (Join-Path $tideProject ('Assets/_Project/' + $tideAssembly + '/Runtime')) -File -Filter '*.cs' -Recurse | Sort-Object FullName)
    if ($tideSources.Count -eq 0) { throw ('No production sources found for ' + $tideAssembly) }
    $tideSourceFiles += $tideSources
    $tideResponse += @($tideSources | ForEach-Object { '"' + $_.FullName.Replace('\','/') + '"' })
    if ($tideResponse | Where-Object { $_.Contains($tideReferences) }) { throw 'The response still references the old output directory.' }
    foreach ($tideOutput in @($tideResponse | Where-Object { $_ -match '^-(out|refout):' })) {
        if (-not $tideOutput.Contains($tideOutputPath + '/')) { throw ('Unexpected compiler output path: ' + $tideOutput) }
    }
    [IO.File]::WriteAllLines((Join-Path $tideCompile ($tideAssembly + '.rsp')), $tideResponse, [Text.UTF8Encoding]::new($false))
}

$tideHashes = @($tideSourceFiles | ForEach-Object {
    [pscustomobject]@{ Source = $_.FullName; SHA256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
})
$tideHashes | Export-Csv -LiteralPath (Join-Path $tideCompile 'source-hashes.csv') -NoTypeInformation -Encoding utf8
if ($PrepareOnly) { Write-Output ('Prepared production responses: ' + $tideCompile); return }

$tideResults = [Collections.Generic.List[string]]::new()
Push-Location $tideProject
try {
    foreach ($tideAssembly in $tideAssemblies) {
        $tideResponsePath = Join-Path $tideCompile ($tideAssembly + '.rsp')
        & (Join-Path $UnityData 'NetCoreRuntime/dotnet.exe') (Join-Path $UnityData 'DotNetSdkRoslyn/csc.dll') ('@' + $tideResponsePath) 2>&1 |
            Tee-Object -FilePath (Join-Path $tideCompile ($tideAssembly + '.log'))
        if ($LASTEXITCODE -ne 0) { throw ('Production compilation failed: ' + $tideAssembly) }
        $tideResults.Add('PASS ' + $tideAssembly)
        Write-Output ('PASS ' + $tideAssembly)
    }
} finally { Pop-Location }

foreach ($tideHash in $tideHashes) {
    if ((Get-FileHash -LiteralPath $tideHash.Source -Algorithm SHA256).Hash -ne $tideHash.SHA256) {
        throw ('Source changed during production compilation; rerun when edits finish: ' + $tideHash.Source)
    }
}
$tideResults.Add('PASS source hashes stable across all four assemblies')
$tideResults.Add('No Unity Editor, isolated project, scene, or preview generator was launched.')
[IO.File]::WriteAllLines((Join-Path $tideCompile 'validation.txt'), $tideResults, [Text.UTF8Encoding]::new($false))
Write-Output ('PASS production validation: ' + $tideCompile)
