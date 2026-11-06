param(
    [string]$ReferenceResponses = 'D:/Game/Artifacts/CardNumbering20261017/UiCompile',
    [string]$UnityData = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data',
    [switch]$PrepareOnly
)
$ErrorActionPreference = 'Stop'
$arcaneWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$arcaneProject = Join-Path $arcaneWorkspace 'UnityClient'
$arcaneCompile = Join-Path $arcaneWorkspace 'Artifacts/ArcaneRules20261024/Compile'
$arcaneReferences = [IO.Path]::GetFullPath($ReferenceResponses).Replace('\','/')
$arcaneOutputPath = $arcaneCompile.Replace('\','/')
$arcaneAssemblies = @('Core','Content','Persistence','Presentation')
$arcaneSourceFiles = @()
New-Item -ItemType Directory -Force -Path $arcaneCompile | Out-Null

foreach ($arcaneAssembly in $arcaneAssemblies) {
    $arcaneResponse = [IO.File]::ReadAllLines((Join-Path $ReferenceResponses ($arcaneAssembly + '.rsp'))) |
        ForEach-Object { $_.Replace($arcaneReferences, $arcaneOutputPath) }
    $arcaneSourcePattern = '^"?(?:' + [Regex]::Escape($arcaneProject.Replace('\','/') + '/') + ')?Assets/_Project/' + $arcaneAssembly + '/Runtime/.*\.cs"?$'
    $arcaneResponse = @($arcaneResponse | Where-Object { $_ -notmatch $arcaneSourcePattern })
    $arcaneSources = @(Get-ChildItem -LiteralPath (Join-Path $arcaneProject ('Assets/_Project/' + $arcaneAssembly + '/Runtime')) -File -Filter '*.cs' -Recurse | Sort-Object FullName)
    if ($arcaneSources.Count -eq 0) { throw ('No production sources found for ' + $arcaneAssembly) }
    $arcaneSourceFiles += $arcaneSources
    $arcaneResponse += @($arcaneSources | ForEach-Object { '"' + $_.FullName.Replace('\','/') + '"' })
    if ($arcaneResponse | Where-Object { $_.Contains($arcaneReferences) }) { throw 'The response still references the old output directory.' }
    foreach ($arcaneOutput in @($arcaneResponse | Where-Object { $_ -match '^-(out|refout):' })) {
        if (-not $arcaneOutput.Contains($arcaneOutputPath + '/')) { throw ('Unexpected compiler output path: ' + $arcaneOutput) }
    }
    [IO.File]::WriteAllLines((Join-Path $arcaneCompile ($arcaneAssembly + '.rsp')), $arcaneResponse, [Text.UTF8Encoding]::new($false))
}

$arcaneHashes = @($arcaneSourceFiles | ForEach-Object {
    [pscustomobject]@{ Source = $_.FullName; SHA256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
})
$arcaneHashes | Export-Csv -LiteralPath (Join-Path $arcaneCompile 'source-hashes.csv') -NoTypeInformation -Encoding utf8
if ($PrepareOnly) { Write-Output ('Prepared production responses: ' + $arcaneCompile); return }

$arcaneResults = [Collections.Generic.List[string]]::new()
Push-Location $arcaneProject
try {
    foreach ($arcaneAssembly in $arcaneAssemblies) {
        $arcaneResponsePath = Join-Path $arcaneCompile ($arcaneAssembly + '.rsp')
        & (Join-Path $UnityData 'NetCoreRuntime/dotnet.exe') (Join-Path $UnityData 'DotNetSdkRoslyn/csc.dll') ('@' + $arcaneResponsePath) 2>&1 |
            Tee-Object -FilePath (Join-Path $arcaneCompile ($arcaneAssembly + '.log'))
        if ($LASTEXITCODE -ne 0) { throw ('Production compilation failed: ' + $arcaneAssembly) }
        $arcaneResults.Add('PASS ' + $arcaneAssembly)
        Write-Output ('PASS ' + $arcaneAssembly)
    }
} finally { Pop-Location }

foreach ($arcaneHash in $arcaneHashes) {
    if ((Get-FileHash -LiteralPath $arcaneHash.Source -Algorithm SHA256).Hash -ne $arcaneHash.SHA256) {
        throw ('Source changed during production compilation; rerun when edits finish: ' + $arcaneHash.Source)
    }
}
$arcaneResults.Add('PASS source hashes stable across all four assemblies')
$arcaneResults.Add('No Unity Editor, isolated project, scene, or preview generator was launched.')
[IO.File]::WriteAllLines((Join-Path $arcaneCompile 'validation.txt'), $arcaneResults, [Text.UTF8Encoding]::new($false))
Write-Output ('PASS production validation: ' + $arcaneCompile)
