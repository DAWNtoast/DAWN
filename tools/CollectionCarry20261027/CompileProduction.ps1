param(
    [string]$ReferenceResponses = 'D:/Game/Artifacts/CardNumbering20261017/UiCompile',
    [string]$UnityData = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data',
    [string]$PlayModeResponse = 'D:/Game/UnityClient/Library/Bee/artifacts/1900b0aE.dag/ShatteredSail.Presentation.PlayModeTests.rsp',
    [switch]$StageIntegration,
    [switch]$PrepareOnly
)
$ErrorActionPreference = 'Stop'
$carryWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$carryProject = Join-Path $carryWorkspace 'UnityClient'
$carryCompile = [IO.Path]::GetFullPath((Join-Path $carryWorkspace 'outputs/collection-carry-20261027/compile'))
$carryReferencePath = [IO.Path]::GetFullPath($ReferenceResponses).Replace('\', '/')
$carryOutputPath = $carryCompile.Replace('\', '/')
$carryAssemblies = @('Core', 'Content', 'Persistence', 'Presentation')
$carrySourceFiles = @()

function Assert-CarryCompileDirectory {
    $carryExpected = [IO.Path]::GetFullPath((Join-Path $carryWorkspace 'outputs/collection-carry-20261027/compile'))
    if ($carryCompile -ne $carryExpected) { throw 'Unexpected production compile directory.' }
    foreach ($carryDirectory in @($carryWorkspace, (Join-Path $carryWorkspace 'outputs'),
        (Join-Path $carryWorkspace 'outputs/collection-carry-20261027'), $carryCompile)) {
        if ((Test-Path -LiteralPath $carryDirectory) -and
            (((Get-Item -LiteralPath $carryDirectory -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0)) {
            throw ('Refusing linked compile directory: ' + $carryDirectory)
        }
    }
}
Assert-CarryCompileDirectory
New-Item -ItemType Directory -Force -Path $carryCompile | Out-Null
Assert-CarryCompileDirectory

foreach ($carryAssembly in $carryAssemblies) {
    $carryResponse = [IO.File]::ReadAllLines((Join-Path $ReferenceResponses ($carryAssembly + '.rsp'))) |
        ForEach-Object { $_.Replace($carryReferencePath, $carryOutputPath) }
    $carrySourcePattern = '^"?(?:' + [Regex]::Escape($carryProject.Replace('\', '/') + '/') +
        ')?Assets/_Project/' + $carryAssembly + '/Runtime/.*\.cs"?$'
    $carryResponse = @($carryResponse | Where-Object { $_ -notmatch $carrySourcePattern })
    $carrySources = @(Get-ChildItem -LiteralPath (Join-Path $carryProject ('Assets/_Project/' + $carryAssembly + '/Runtime')) -File -Filter '*.cs' -Recurse | Sort-Object FullName)
    if ($carrySources.Count -eq 0) { throw ('No current production sources found for ' + $carryAssembly) }
    $carrySourceFiles += $carrySources
    $carryResponse += @($carrySources | ForEach-Object { '"' + $_.FullName.Replace('\', '/') + '"' })
    if ($carryResponse | Where-Object { $_.Contains($carryReferencePath) }) { throw 'Old compiler output directory remains referenced.' }
    foreach ($carryOutput in @($carryResponse | Where-Object { $_ -match '^-(out|refout|pdb):' })) {
        if (-not $carryOutput.Contains($carryOutputPath + '/')) { throw ('Unexpected compiler output: ' + $carryOutput) }
    }
    [IO.File]::WriteAllLines((Join-Path $carryCompile ($carryAssembly + '.rsp')), $carryResponse, [Text.UTF8Encoding]::new($false))
}

# Compile current PlayMode tests against the same freshly built production code.
# This verifies integration signatures only; no Unity scene or profile is opened.
$carryTestSources = @(Get-ChildItem -LiteralPath (Join-Path $carryProject 'Assets/_Project/Tests/PlayMode') -File -Filter '*.cs' -Recurse | Sort-Object FullName)
$carrySourceFiles += $carryTestSources
$carryTestResponse = @([IO.File]::ReadAllLines($PlayModeResponse) | Where-Object { $_ -notmatch '^"?Assets/_Project/Tests/PlayMode/.*\.cs"?$' } | ForEach-Object {
    if ($_ -match '^-(out|refout|pdb):') {
        $carryKind = $Matches[1]
        $carrySuffix = if ($carryKind -eq 'refout') { '.ref.dll' } elseif ($carryKind -eq 'pdb') { '.pdb' } else { '.dll' }
        '-' + $carryKind + ':"' + $carryOutputPath + '/ShatteredSail.Presentation.PlayModeTests' + $carrySuffix + '"'
    } elseif ($_ -match '^-r:"[^"]*/(ShatteredSail\.(Core|Content|Persistence|Presentation)\.ref\.dll)"$') {
        '-r:"' + $carryOutputPath + '/' + $Matches[1] + '"'
    } else { $_ }
})
$carryTestResponse += @($carryTestSources | ForEach-Object { '"' + $_.FullName.Replace('\', '/') + '"' })
foreach ($carryOutput in @($carryTestResponse | Where-Object { $_ -match '^-(out|refout|pdb):' })) {
    if (-not $carryOutput.Contains($carryOutputPath + '/')) { throw ('Unexpected test compiler output: ' + $carryOutput) }
}
[IO.File]::WriteAllLines((Join-Path $carryCompile 'Presentation.PlayModeTests.rsp'), $carryTestResponse, [Text.UTF8Encoding]::new($false))
$carryAssemblies += 'Presentation.PlayModeTests'

$carryHashes = @($carrySourceFiles | ForEach-Object {
    [pscustomobject]@{ Source = $_.FullName; SHA256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
})
$carryHashes | Export-Csv -LiteralPath (Join-Path $carryCompile 'source-hashes.csv') -NoTypeInformation -Encoding utf8
if ($PrepareOnly) { Write-Output ('Prepared production responses: ' + $carryCompile); return }

$carryResults = [Collections.Generic.List[string]]::new()
try {
    Push-Location $carryProject
    try {
        foreach ($carryAssembly in $carryAssemblies) {
            $carryResponsePath = Join-Path $carryCompile ($carryAssembly + '.rsp')
            $carryLog = Join-Path $carryCompile ($carryAssembly + '.log')
            ('Compiling current production assembly: ' + $carryAssembly) | Set-Content -LiteralPath $carryLog -Encoding utf8
            & (Join-Path $UnityData 'NetCoreRuntime/dotnet.exe') (Join-Path $UnityData 'DotNetSdkRoslyn/csc.dll') ('@' + $carryResponsePath) 2>&1 |
                Tee-Object -FilePath $carryLog -Append
            if ($LASTEXITCODE -ne 0) { throw ('Production compilation failed: ' + $carryAssembly) }
            ('PASS ' + $carryAssembly) | Add-Content -LiteralPath $carryLog -Encoding utf8
            $carryResults.Add('PASS ' + $carryAssembly)
            Write-Output ('PASS ' + $carryAssembly)
        }
    } finally { Pop-Location }

    foreach ($carryHash in $carryHashes) {
        if ((Get-FileHash -LiteralPath $carryHash.Source -Algorithm SHA256).Hash -ne $carryHash.SHA256) {
            throw ('Production source changed during compilation: ' + $carryHash.Source)
        }
    }
    $carryResults.Add('PASS source hashes stable across four production assemblies and PlayMode test compilation')
    $carryResults.Add('Used existing Unity reference assemblies read-only. No Editor, isolated Unity project, scene, or preview generator was launched.')
    [IO.File]::WriteAllLines((Join-Path $carryCompile 'validation.txt'), $carryResults, [Text.UTF8Encoding]::new($false))
    if ($StageIntegration) {
        $carryIntegration = [IO.Path]::GetFullPath((Join-Path $carryWorkspace 'tmp/collection-carry-20261027-integration'))
        if (-not (Test-Path -LiteralPath (Join-Path $carryIntegration 'OWNED_COLLECTION_CARRY_VALIDATION.txt'))) { throw 'Dedicated integration project marker is missing.' }
        $carryPlugins = Join-Path $carryIntegration 'Assets/Plugins'
        $carryAncestor = Get-Item -LiteralPath $carryPlugins -Force
        while ($null -ne $carryAncestor) {
            if ($carryAncestor.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Linked integration project is forbidden.' }
            $carryAncestor = $carryAncestor.Parent
        }
        foreach ($carryAssembly in @('Core', 'Content', 'Persistence', 'Presentation')) {
            Copy-Item -LiteralPath (Join-Path $carryCompile ('ShatteredSail.' + $carryAssembly + '.dll')) -Destination $carryPlugins
        }
    }
} finally {
    # Compiler outputs are needed only until all four sequential assemblies finish.
    # Keep response files, source hashes and logs; never clean UnityClient or the
    # shared reference-response directory used by other existing tools.
    Assert-CarryCompileDirectory
    $carryConsumers = @(Get-CimInstance Win32_Process -Filter "Name='mono.exe' OR Name='dotnet.exe'" |
        Where-Object { $_.CommandLine -like ('*' + $carryCompile + '*') -or $_.CommandLine -like ('*' + $carryOutputPath + '*') })
    if ($carryConsumers.Count -gt 0) { throw 'A compiler or test process still references the task outputs; cleanup deferred.' }
    $carryReleased = 0L
    $carryRemoved = [Collections.Generic.List[string]]::new()
    foreach ($carryAssembly in $carryAssemblies) {
        foreach ($carrySuffix in @('.dll', '.pdb', '.ref.dll')) {
            $carryFile = [IO.Path]::GetFullPath((Join-Path $carryCompile ('ShatteredSail.' + $carryAssembly + $carrySuffix)))
            if ([IO.Path]::GetDirectoryName($carryFile) -ne $carryCompile) { throw 'Unexpected cleanup target.' }
            if (Test-Path -LiteralPath $carryFile) {
                $carryItem = Get-Item -LiteralPath $carryFile
                if ($carryItem.PSIsContainer -or ($carryItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                    throw ('Refusing unexpected compiler artifact: ' + $carryFile)
                }
                $carryReleased += $carryItem.Length
                Remove-Item -LiteralPath $carryFile -Force
                if (Test-Path -LiteralPath $carryFile) { throw ('Cleanup failed: ' + $carryFile) }
                $carryRemoved.Add($carryItem.Name)
            }
        }
    }
    [pscustomobject]@{ ReleasedBytes = $carryReleased; RemovedFiles = @($carryRemoved); EvidenceRetained = $true;
        MainProjectCleaned = $false; SharedReferenceAssembliesRetained = $ReferenceResponses } |
        ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $carryCompile 'cleanup.json') -Encoding utf8
    Write-Output ('Cleaned task compiler artifacts: ' + $carryReleased + ' bytes; evidence retained.')
}
Write-Output ('PASS production validation: ' + $carryCompile)
