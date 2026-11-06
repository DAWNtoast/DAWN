param(
    [string]$ReferenceResponses = 'D:/Game/Artifacts/CardNumbering20261017/UiCompile',
    [string]$UnityData = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data',
    [string]$PlayModeResponse = 'D:/Game/UnityClient/Library/Bee/artifacts/1900b0aE.dag/ShatteredSail.Presentation.PlayModeTests.rsp',
    [switch]$IncludePlayMode,
    [switch]$PrepareOnly
)
$ErrorActionPreference = 'Stop'
$chartWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$chartProject = Join-Path $chartWorkspace 'UnityClient'
$chartCompile = [IO.Path]::GetFullPath((Join-Path $chartWorkspace 'outputs/sea-chart-threshold-20261028/production-compile'))
$chartReferencePath = [IO.Path]::GetFullPath($ReferenceResponses).Replace('\', '/')
$chartOutputPath = $chartCompile.Replace('\', '/')
$chartAssemblies = @('Core', 'Content', 'Persistence', 'Presentation')
$chartSourceFiles = @()

function Assert-ChartCompileDirectory {
    $chartExpected = [IO.Path]::GetFullPath((Join-Path $chartWorkspace 'outputs/sea-chart-threshold-20261028/production-compile'))
    if ($chartCompile -ne $chartExpected) { throw 'Unexpected production compile directory.' }
    foreach ($chartDirectory in @($chartWorkspace, (Join-Path $chartWorkspace 'outputs'),
        (Join-Path $chartWorkspace 'outputs/sea-chart-threshold-20261028'), $chartCompile)) {
        if ((Test-Path -LiteralPath $chartDirectory) -and
            (((Get-Item -LiteralPath $chartDirectory -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0)) {
            throw ('Refusing linked compile directory: ' + $chartDirectory)
        }
    }
}
Assert-ChartCompileDirectory
New-Item -ItemType Directory -Force -Path $chartCompile | Out-Null
Assert-ChartCompileDirectory

foreach ($chartAssembly in $chartAssemblies) {
    $chartResponse = [IO.File]::ReadAllLines((Join-Path $ReferenceResponses ($chartAssembly + '.rsp'))) |
        ForEach-Object { $_.Replace($chartReferencePath, $chartOutputPath) }
    $chartSourcePattern = '^"?(?:' + [Regex]::Escape($chartProject.Replace('\', '/') + '/') +
        ')?Assets/_Project/' + $chartAssembly + '/Runtime/.*\.cs"?$'
    $chartResponse = @($chartResponse | Where-Object { $_ -notmatch $chartSourcePattern })
    $chartSources = @(Get-ChildItem -LiteralPath (Join-Path $chartProject ('Assets/_Project/' + $chartAssembly + '/Runtime')) -File -Filter '*.cs' -Recurse | Sort-Object FullName)
    if ($chartSources.Count -eq 0) { throw ('No current production sources found for ' + $chartAssembly) }
    $chartSourceFiles += $chartSources
    $chartResponse += @($chartSources | ForEach-Object { '"' + $_.FullName.Replace('\', '/') + '"' })
    if ($chartResponse | Where-Object { $_.Contains($chartReferencePath) }) { throw 'Old compiler output directory remains referenced.' }
    foreach ($chartOutput in @($chartResponse | Where-Object { $_ -match '^-(out|refout|pdb):' })) {
        if (-not $chartOutput.Contains($chartOutputPath + '/')) { throw ('Unexpected compiler output: ' + $chartOutput) }
    }
    [IO.File]::WriteAllLines((Join-Path $chartCompile ($chartAssembly + '.rsp')), $chartResponse, [Text.UTF8Encoding]::new($false))
}

if ($IncludePlayMode) {
# Compile current PlayMode tests against the same freshly built production code.
# This verifies integration signatures only; no Unity scene or profile is opened.
$chartTestSources = @(Get-ChildItem -LiteralPath (Join-Path $chartProject 'Assets/_Project/Tests/PlayMode') -File -Filter '*.cs' -Recurse | Sort-Object FullName)
$chartSourceFiles += $chartTestSources
$chartTestResponse = @([IO.File]::ReadAllLines($PlayModeResponse) | Where-Object { $_ -notmatch '^"?Assets/_Project/Tests/PlayMode/.*\.cs"?$' } | ForEach-Object {
    if ($_ -match '^-(out|refout|pdb):') {
        $chartKind = $Matches[1]
        $chartSuffix = if ($chartKind -eq 'refout') { '.ref.dll' } elseif ($chartKind -eq 'pdb') { '.pdb' } else { '.dll' }
        '-' + $chartKind + ':"' + $chartOutputPath + '/ShatteredSail.Presentation.PlayModeTests' + $chartSuffix + '"'
    } elseif ($_ -match '^-r:"[^"]*/(ShatteredSail\.(Core|Content|Persistence|Presentation)\.ref\.dll)"$') {
        '-r:"' + $chartOutputPath + '/' + $Matches[1] + '"'
    } else { $_ }
})
$chartTestResponse += @($chartTestSources | ForEach-Object { '"' + $_.FullName.Replace('\', '/') + '"' })
foreach ($chartOutput in @($chartTestResponse | Where-Object { $_ -match '^-(out|refout|pdb):' })) {
    if (-not $chartOutput.Contains($chartOutputPath + '/')) { throw ('Unexpected test compiler output: ' + $chartOutput) }
}
[IO.File]::WriteAllLines((Join-Path $chartCompile 'Presentation.PlayModeTests.rsp'), $chartTestResponse, [Text.UTF8Encoding]::new($false))
$chartAssemblies += 'Presentation.PlayModeTests'


}

$chartHashes = @($chartSourceFiles | ForEach-Object {
    [pscustomobject]@{ Source = $_.FullName; SHA256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
})
$chartHashes | Export-Csv -LiteralPath (Join-Path $chartCompile 'source-hashes.csv') -NoTypeInformation -Encoding utf8
if ($PrepareOnly) { Write-Output ('Prepared production responses: ' + $chartCompile); return }

$chartResults = [Collections.Generic.List[string]]::new()
try {
    Push-Location $chartProject
    try {
        foreach ($chartAssembly in $chartAssemblies) {
            $chartResponsePath = Join-Path $chartCompile ($chartAssembly + '.rsp')
            $chartLog = Join-Path $chartCompile ($chartAssembly + '.log')
            ('Compiling current production assembly: ' + $chartAssembly) | Set-Content -LiteralPath $chartLog -Encoding utf8
            & (Join-Path $UnityData 'NetCoreRuntime/dotnet.exe') (Join-Path $UnityData 'DotNetSdkRoslyn/csc.dll') ('@' + $chartResponsePath) 2>&1 |
                Tee-Object -FilePath $chartLog -Append
            if ($LASTEXITCODE -ne 0) { throw ('Production compilation failed: ' + $chartAssembly) }
            ('PASS ' + $chartAssembly) | Add-Content -LiteralPath $chartLog -Encoding utf8
            $chartResults.Add('PASS ' + $chartAssembly)
            Write-Output ('PASS ' + $chartAssembly)
        }
    } finally { Pop-Location }

    foreach ($chartHash in $chartHashes) {
        if ((Get-FileHash -LiteralPath $chartHash.Source -Algorithm SHA256).Hash -ne $chartHash.SHA256) {
            throw ('Production source changed during compilation: ' + $chartHash.Source)
        }
    }
    $chartResults.Add('PASS source hashes stable across all requested assemblies')
    $chartResults.Add('Used existing Unity reference assemblies read-only. No Editor, isolated Unity project, scene, or preview generator was launched.')
    [IO.File]::WriteAllLines((Join-Path $chartCompile 'validation.txt'), $chartResults, [Text.UTF8Encoding]::new($false))
} finally {
    # Compiler outputs are needed only until all four sequential assemblies finish.
    # Keep response files, source hashes and logs; never clean UnityClient or the
    # shared reference-response directory used by other existing tools.
    Assert-ChartCompileDirectory
    $chartConsumers = @(Get-CimInstance Win32_Process -Filter "Name='mono.exe' OR Name='dotnet.exe'" |
        Where-Object { $_.CommandLine -like ('*' + $chartCompile + '*') -or $_.CommandLine -like ('*' + $chartOutputPath + '*') })
    if ($chartConsumers.Count -gt 0) { throw 'A compiler or test process still references the task outputs; cleanup deferred.' }
    $chartReleased = 0L
    $chartRemoved = [Collections.Generic.List[string]]::new()
    foreach ($chartAssembly in $chartAssemblies) {
        foreach ($chartSuffix in @('.dll', '.pdb', '.ref.dll')) {
            $chartFile = [IO.Path]::GetFullPath((Join-Path $chartCompile ('ShatteredSail.' + $chartAssembly + $chartSuffix)))
            if ([IO.Path]::GetDirectoryName($chartFile) -ne $chartCompile) { throw 'Unexpected cleanup target.' }
            if (Test-Path -LiteralPath $chartFile) {
                $chartItem = Get-Item -LiteralPath $chartFile
                if ($chartItem.PSIsContainer -or ($chartItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                    throw ('Refusing unexpected compiler artifact: ' + $chartFile)
                }
                $chartReleased += $chartItem.Length
                Remove-Item -LiteralPath $chartFile -Force
                if (Test-Path -LiteralPath $chartFile) { throw ('Cleanup failed: ' + $chartFile) }
                $chartRemoved.Add($chartItem.Name)
            }
        }
    }
    [pscustomobject]@{ ReleasedBytes = $chartReleased; RemovedFiles = @($chartRemoved); EvidenceRetained = $true;
        MainProjectCleaned = $false; SharedReferenceAssembliesRetained = $ReferenceResponses } |
        ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $chartCompile 'cleanup.json') -Encoding utf8
    Write-Output ('Cleaned task compiler artifacts: ' + $chartReleased + ' bytes; evidence retained.')
}
Write-Output ('PASS production validation: ' + $chartCompile)
