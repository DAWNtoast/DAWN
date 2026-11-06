param(
    [string]$ReferenceResponses = 'D:/Game/Artifacts/CardNumbering20261017/UiCompile',
    [string]$UnityData = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data',
    [switch]$PrepareOnly
)
$ErrorActionPreference = 'Stop'
$thunderWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$thunderProject = Join-Path $thunderWorkspace 'UnityClient'
$thunderCompile = Join-Path $thunderWorkspace 'Artifacts/ThunderRules20261024/Compile'
$thunderReferences = [IO.Path]::GetFullPath($ReferenceResponses).Replace('\','/')
$thunderOutputPath = $thunderCompile.Replace('\','/')
$thunderAssemblies = @('Core','Content','Persistence','Presentation')
$thunderSourceFiles = @()
New-Item -ItemType Directory -Force -Path $thunderCompile | Out-Null

foreach ($thunderAssembly in $thunderAssemblies) {
    $thunderResponse = [IO.File]::ReadAllLines((Join-Path $ReferenceResponses ($thunderAssembly + '.rsp'))) |
        ForEach-Object { $_.Replace($thunderReferences, $thunderOutputPath) }
    $thunderSourcePattern = '^"?(?:' + [Regex]::Escape($thunderProject.Replace('\','/') + '/') + ')?Assets/_Project/' + $thunderAssembly + '/Runtime/.*\.cs"?$'
    $thunderResponse = @($thunderResponse | Where-Object { $_ -notmatch $thunderSourcePattern })
    $thunderSources = @(Get-ChildItem -LiteralPath (Join-Path $thunderProject ('Assets/_Project/' + $thunderAssembly + '/Runtime')) -File -Filter '*.cs' -Recurse | Sort-Object FullName)
    if ($thunderSources.Count -eq 0) { throw ('No production sources found for ' + $thunderAssembly) }
    $thunderSourceFiles += $thunderSources
    $thunderResponse += @($thunderSources | ForEach-Object { '"' + $_.FullName.Replace('\','/') + '"' })
    if ($thunderResponse | Where-Object { $_.Contains($thunderReferences) }) { throw 'The response still references the old output directory.' }
    foreach ($thunderOutput in @($thunderResponse | Where-Object { $_ -match '^-(out|refout):' })) {
        if (-not $thunderOutput.Contains($thunderOutputPath + '/')) { throw ('Unexpected compiler output path: ' + $thunderOutput) }
    }
    [IO.File]::WriteAllLines((Join-Path $thunderCompile ($thunderAssembly + '.rsp')), $thunderResponse, [Text.UTF8Encoding]::new($false))
}

$thunderHashes = @($thunderSourceFiles | ForEach-Object {
    [pscustomobject]@{ Source = $_.FullName; SHA256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
})
$thunderHashes | Export-Csv -LiteralPath (Join-Path $thunderCompile 'source-hashes.csv') -NoTypeInformation -Encoding utf8
if ($PrepareOnly) { Write-Output ('Prepared production responses: ' + $thunderCompile); return }

$thunderResults = [Collections.Generic.List[string]]::new()
Push-Location $thunderProject
try {
    foreach ($thunderAssembly in $thunderAssemblies) {
        $thunderResponsePath = Join-Path $thunderCompile ($thunderAssembly + '.rsp')
        & (Join-Path $UnityData 'NetCoreRuntime/dotnet.exe') (Join-Path $UnityData 'DotNetSdkRoslyn/csc.dll') ('@' + $thunderResponsePath) 2>&1 |
            Tee-Object -FilePath (Join-Path $thunderCompile ($thunderAssembly + '.log'))
        if ($LASTEXITCODE -ne 0) { throw ('Production compilation failed: ' + $thunderAssembly) }
        $thunderResults.Add('PASS ' + $thunderAssembly)
        Write-Output ('PASS ' + $thunderAssembly)
    }
} finally { Pop-Location }

foreach ($thunderHash in $thunderHashes) {
    if ((Get-FileHash -LiteralPath $thunderHash.Source -Algorithm SHA256).Hash -ne $thunderHash.SHA256) {
        throw ('Source changed during production compilation; rerun when edits finish: ' + $thunderHash.Source)
    }
}
$thunderResults.Add('PASS source hashes stable across all four assemblies')
$thunderResults.Add('No Unity Editor, isolated project, scene, or preview generator was launched.')
[IO.File]::WriteAllLines((Join-Path $thunderCompile 'validation.txt'), $thunderResults, [Text.UTF8Encoding]::new($false))
Write-Output ('PASS production validation: ' + $thunderCompile)
