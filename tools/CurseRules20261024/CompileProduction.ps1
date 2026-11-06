param(
    [string]$ReferenceResponses = 'D:/Game/Artifacts/CardNumbering20261017/UiCompile',
    [string]$UnityData = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data',
    [switch]$PrepareOnly
)
$ErrorActionPreference = 'Stop'
$curseWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$curseProject = Join-Path $curseWorkspace 'UnityClient'
$curseCompile = Join-Path $curseWorkspace 'Artifacts/CurseRules20261024/Compile'
$curseReferences = [IO.Path]::GetFullPath($ReferenceResponses).Replace('\','/')
$curseOutputPath = $curseCompile.Replace('\','/')
$curseAssemblies = @('Core','Content','Persistence','Presentation')
$curseSourceFiles = @()
New-Item -ItemType Directory -Force -Path $curseCompile | Out-Null

foreach ($curseAssembly in $curseAssemblies) {
    $curseResponse = [IO.File]::ReadAllLines((Join-Path $ReferenceResponses ($curseAssembly + '.rsp'))) |
        ForEach-Object { $_.Replace($curseReferences, $curseOutputPath) }
    $curseSourcePattern = '^"?(?:' + [Regex]::Escape($curseProject.Replace('\','/') + '/') + ')?Assets/_Project/' + $curseAssembly + '/Runtime/.*\.cs"?$'
    $curseResponse = @($curseResponse | Where-Object { $_ -notmatch $curseSourcePattern })
    $curseSources = @(Get-ChildItem -LiteralPath (Join-Path $curseProject ('Assets/_Project/' + $curseAssembly + '/Runtime')) -File -Filter '*.cs' -Recurse | Sort-Object FullName)
    if ($curseSources.Count -eq 0) { throw ('No production sources found for ' + $curseAssembly) }
    $curseSourceFiles += $curseSources
    $curseResponse += @($curseSources | ForEach-Object { '"' + $_.FullName.Replace('\','/') + '"' })
    if ($curseResponse | Where-Object { $_.Contains($curseReferences) }) { throw 'The response still references the old output directory.' }
    foreach ($curseOutput in @($curseResponse | Where-Object { $_ -match '^-(out|refout):' })) {
        if (-not $curseOutput.Contains($curseOutputPath + '/')) { throw ('Unexpected compiler output path: ' + $curseOutput) }
    }
    [IO.File]::WriteAllLines((Join-Path $curseCompile ($curseAssembly + '.rsp')), $curseResponse, [Text.UTF8Encoding]::new($false))
}

$curseHashes = @($curseSourceFiles | ForEach-Object {
    [pscustomobject]@{ Source = $_.FullName; SHA256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
})
$curseHashes | Export-Csv -LiteralPath (Join-Path $curseCompile 'source-hashes.csv') -NoTypeInformation -Encoding utf8
if ($PrepareOnly) { Write-Output ('Prepared production responses: ' + $curseCompile); return }

$curseResults = [Collections.Generic.List[string]]::new()
Push-Location $curseProject
try {
    foreach ($curseAssembly in $curseAssemblies) {
        $curseResponsePath = Join-Path $curseCompile ($curseAssembly + '.rsp')
        & (Join-Path $UnityData 'NetCoreRuntime/dotnet.exe') (Join-Path $UnityData 'DotNetSdkRoslyn/csc.dll') ('@' + $curseResponsePath) 2>&1 |
            Tee-Object -FilePath (Join-Path $curseCompile ($curseAssembly + '.log'))
        if ($LASTEXITCODE -ne 0) { throw ('Production compilation failed: ' + $curseAssembly) }
        $curseResults.Add('PASS ' + $curseAssembly)
        Write-Output ('PASS ' + $curseAssembly)
    }
} finally { Pop-Location }

foreach ($curseHash in $curseHashes) {
    if ((Get-FileHash -LiteralPath $curseHash.Source -Algorithm SHA256).Hash -ne $curseHash.SHA256) {
        throw ('Source changed during production compilation; rerun when edits finish: ' + $curseHash.Source)
    }
}
$curseResults.Add('PASS source hashes stable across all four assemblies')
$curseResults.Add('No Unity Editor, isolated project, scene, or preview generator was launched.')
[IO.File]::WriteAllLines((Join-Path $curseCompile 'validation.txt'), $curseResults, [Text.UTF8Encoding]::new($false))
Write-Output ('PASS production validation: ' + $curseCompile)
