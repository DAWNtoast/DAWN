param(
    [string]$ReferenceResponses = 'D:/Game/Artifacts/CardNumbering20261017/UiCompile',
    [string]$UnityData = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data',
    [switch]$PrepareOnly
)
$ErrorActionPreference = 'Stop'
$bloodWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$bloodProject = Join-Path $bloodWorkspace 'UnityClient'
$bloodCompile = Join-Path $bloodWorkspace 'Artifacts/BloodRules20261024/Compile'
$bloodReferences = [IO.Path]::GetFullPath($ReferenceResponses).Replace('\','/')
$bloodOutputPath = $bloodCompile.Replace('\','/')
$bloodAssemblies = @('Core','Content','Persistence','Presentation')
$bloodSourceFiles = @()
New-Item -ItemType Directory -Force -Path $bloodCompile | Out-Null

foreach ($bloodAssembly in $bloodAssemblies) {
    $bloodResponse = [IO.File]::ReadAllLines((Join-Path $ReferenceResponses ($bloodAssembly + '.rsp'))) |
        ForEach-Object { $_.Replace($bloodReferences, $bloodOutputPath) }
    $bloodSourcePattern = '^"?(?:' + [Regex]::Escape($bloodProject.Replace('\','/') + '/') + ')?Assets/_Project/' + $bloodAssembly + '/Runtime/.*\.cs"?$'
    $bloodResponse = @($bloodResponse | Where-Object { $_ -notmatch $bloodSourcePattern })
    $bloodSources = @(Get-ChildItem -LiteralPath (Join-Path $bloodProject ('Assets/_Project/' + $bloodAssembly + '/Runtime')) -File -Filter '*.cs' -Recurse | Sort-Object FullName)
    if ($bloodSources.Count -eq 0) { throw ('No production sources found for ' + $bloodAssembly) }
    $bloodSourceFiles += $bloodSources
    $bloodResponse += @($bloodSources | ForEach-Object { '"' + $_.FullName.Replace('\','/') + '"' })
    if ($bloodResponse | Where-Object { $_.Contains($bloodReferences) }) { throw 'The response still references the old output directory.' }
    foreach ($bloodOutput in @($bloodResponse | Where-Object { $_ -match '^-(out|refout):' })) {
        if (-not $bloodOutput.Contains($bloodOutputPath + '/')) { throw ('Unexpected compiler output path: ' + $bloodOutput) }
    }
    [IO.File]::WriteAllLines((Join-Path $bloodCompile ($bloodAssembly + '.rsp')), $bloodResponse, [Text.UTF8Encoding]::new($false))
}

$bloodHashes = @($bloodSourceFiles | ForEach-Object {
    [pscustomobject]@{ Source = $_.FullName; SHA256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
})
$bloodHashes | Export-Csv -LiteralPath (Join-Path $bloodCompile 'source-hashes.csv') -NoTypeInformation -Encoding utf8
if ($PrepareOnly) { Write-Output ('Prepared production responses: ' + $bloodCompile); return }

$bloodResults = [Collections.Generic.List[string]]::new()
Push-Location $bloodProject
try {
    foreach ($bloodAssembly in $bloodAssemblies) {
        $bloodResponsePath = Join-Path $bloodCompile ($bloodAssembly + '.rsp')
        & (Join-Path $UnityData 'NetCoreRuntime/dotnet.exe') (Join-Path $UnityData 'DotNetSdkRoslyn/csc.dll') ('@' + $bloodResponsePath) 2>&1 |
            Tee-Object -FilePath (Join-Path $bloodCompile ($bloodAssembly + '.log'))
        if ($LASTEXITCODE -ne 0) { throw ('Production compilation failed: ' + $bloodAssembly) }
        $bloodResults.Add('PASS ' + $bloodAssembly)
        Write-Output ('PASS ' + $bloodAssembly)
    }
} finally { Pop-Location }

foreach ($bloodHash in $bloodHashes) {
    if ((Get-FileHash -LiteralPath $bloodHash.Source -Algorithm SHA256).Hash -ne $bloodHash.SHA256) {
        throw ('Source changed during production compilation; rerun when edits finish: ' + $bloodHash.Source)
    }
}
$bloodResults.Add('PASS source hashes stable across all four assemblies')
$bloodResults.Add('No Unity Editor, isolated project, scene, or preview generator was launched.')
[IO.File]::WriteAllLines((Join-Path $bloodCompile 'validation.txt'), $bloodResults, [Text.UTF8Encoding]::new($false))
Write-Output ('PASS production validation: ' + $bloodCompile)
