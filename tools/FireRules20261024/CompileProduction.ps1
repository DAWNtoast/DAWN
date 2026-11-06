param(
    [string]$ReferenceResponses = 'D:/Game/Artifacts/CardNumbering20261017/UiCompile',
    [string]$UnityData = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data',
    [switch]$PrepareOnly
)
$ErrorActionPreference = 'Stop'
$fireWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$fireProject = Join-Path $fireWorkspace 'UnityClient'
$fireCompile = Join-Path $fireWorkspace 'Artifacts/FireRules20261024/Compile'
$fireReferences = [IO.Path]::GetFullPath($ReferenceResponses).Replace('\','/')
$fireOutputPath = $fireCompile.Replace('\','/')
$fireAssemblies = @('Core','Content','Persistence','Presentation')
$fireSourceFiles = @()
New-Item -ItemType Directory -Force -Path $fireCompile | Out-Null

foreach ($fireAssembly in $fireAssemblies) {
    $fireResponse = [IO.File]::ReadAllLines((Join-Path $ReferenceResponses ($fireAssembly + '.rsp'))) |
        ForEach-Object { $_.Replace($fireReferences, $fireOutputPath) }
    $fireSourcePattern = '^"?(?:' + [Regex]::Escape($fireProject.Replace('\','/') + '/') + ')?Assets/_Project/' + $fireAssembly + '/Runtime/.*\.cs"?$'
    $fireResponse = @($fireResponse | Where-Object { $_ -notmatch $fireSourcePattern })
    $fireSources = @(Get-ChildItem -LiteralPath (Join-Path $fireProject ('Assets/_Project/' + $fireAssembly + '/Runtime')) -File -Filter '*.cs' -Recurse | Sort-Object FullName)
    if ($fireSources.Count -eq 0) { throw ('No production sources found for ' + $fireAssembly) }
    $fireSourceFiles += $fireSources
    $fireResponse += @($fireSources | ForEach-Object { '"' + $_.FullName.Replace('\','/') + '"' })
    if ($fireResponse | Where-Object { $_.Contains($fireReferences) }) { throw 'The response still references the old output directory.' }
    foreach ($fireOutput in @($fireResponse | Where-Object { $_ -match '^-(out|refout):' })) {
        if (-not $fireOutput.Contains($fireOutputPath + '/')) { throw ('Unexpected compiler output path: ' + $fireOutput) }
    }
    [IO.File]::WriteAllLines((Join-Path $fireCompile ($fireAssembly + '.rsp')), $fireResponse, [Text.UTF8Encoding]::new($false))
}

$fireHashes = @($fireSourceFiles | ForEach-Object {
    [pscustomobject]@{ Source = $_.FullName; SHA256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
})
$fireHashes | Export-Csv -LiteralPath (Join-Path $fireCompile 'source-hashes.csv') -NoTypeInformation -Encoding utf8
if ($PrepareOnly) { Write-Output ('Prepared production responses: ' + $fireCompile); return }

$fireResults = [Collections.Generic.List[string]]::new()
Push-Location $fireProject
try {
    foreach ($fireAssembly in $fireAssemblies) {
        $fireResponsePath = Join-Path $fireCompile ($fireAssembly + '.rsp')
        & (Join-Path $UnityData 'NetCoreRuntime/dotnet.exe') (Join-Path $UnityData 'DotNetSdkRoslyn/csc.dll') ('@' + $fireResponsePath) 2>&1 |
            Tee-Object -FilePath (Join-Path $fireCompile ($fireAssembly + '.log'))
        if ($LASTEXITCODE -ne 0) { throw ('Production compilation failed: ' + $fireAssembly) }
        $fireResults.Add('PASS ' + $fireAssembly)
        Write-Output ('PASS ' + $fireAssembly)
    }
} finally { Pop-Location }

foreach ($fireHash in $fireHashes) {
    if ((Get-FileHash -LiteralPath $fireHash.Source -Algorithm SHA256).Hash -ne $fireHash.SHA256) {
        throw ('Source changed during production compilation; rerun when edits finish: ' + $fireHash.Source)
    }
}
$fireResults.Add('PASS source hashes stable across all four assemblies')
$fireResults.Add('No Unity Editor, isolated project, scene, or preview generator was launched.')
[IO.File]::WriteAllLines((Join-Path $fireCompile 'validation.txt'), $fireResults, [Text.UTF8Encoding]::new($false))
Write-Output ('PASS production validation: ' + $fireCompile)
