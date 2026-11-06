param(
    [string]$ReferenceResponses = 'D:/Game/Artifacts/CardNumbering20261017/UiCompile',
    [string]$UnityData = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data',
    [string]$PlayModeResponse = 'D:/Game/UnityClient/Library/Bee/artifacts/1900b0aE.dag/ShatteredSail.Presentation.PlayModeTests.rsp',
    [switch]$IncludePlayMode,
    [string]$EditModeResponse = 'D:/Game/UnityClient/Library/Bee/artifacts/1900b0aE.dag/ShatteredSail.Core.Tests.rsp',
    [switch]$IncludeEditMode,
    [string]$JournalPluginDirectory,
    [switch]$PrepareOnly
)
$ErrorActionPreference = 'Stop'
$cardReviewValidationWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$cardReviewValidationProject = Join-Path $cardReviewValidationWorkspace 'UnityClient'
$cardReviewValidationCompile = [IO.Path]::GetFullPath((Join-Path $cardReviewValidationWorkspace 'outputs/card-game-view-20261030/production-compile'))
$cardReviewValidationReferencePath = [IO.Path]::GetFullPath($ReferenceResponses).Replace('\', '/')
$cardReviewValidationOutputPath = $cardReviewValidationCompile.Replace('\', '/')
$cardReviewValidationAssemblies = @('Core', 'Content', 'Persistence', 'Presentation')
$cardReviewValidationSourceFiles = @()

function Assert-CardReviewValidationCompileDirectory {
    $cardReviewValidationExpected = [IO.Path]::GetFullPath((Join-Path $cardReviewValidationWorkspace 'outputs/card-game-view-20261030/production-compile'))
    if ($cardReviewValidationCompile -ne $cardReviewValidationExpected) { throw 'Unexpected production compile directory.' }
    foreach ($cardReviewValidationDirectory in @($cardReviewValidationWorkspace, (Join-Path $cardReviewValidationWorkspace 'outputs'),
        (Join-Path $cardReviewValidationWorkspace 'outputs/card-game-view-20261030'), $cardReviewValidationCompile)) {
        if ((Test-Path -LiteralPath $cardReviewValidationDirectory) -and
            (((Get-Item -LiteralPath $cardReviewValidationDirectory -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0)) {
            throw ('Refusing linked compile directory: ' + $cardReviewValidationDirectory)
        }
    }
}
Assert-CardReviewValidationCompileDirectory
New-Item -ItemType Directory -Force -Path $cardReviewValidationCompile | Out-Null
Assert-CardReviewValidationCompileDirectory

foreach ($cardReviewValidationAssembly in $cardReviewValidationAssemblies) {
    $cardReviewValidationResponse = [IO.File]::ReadAllLines((Join-Path $ReferenceResponses ($cardReviewValidationAssembly + '.rsp'))) |
        ForEach-Object { $_.Replace($cardReviewValidationReferencePath, $cardReviewValidationOutputPath) }
    $cardReviewValidationSourcePattern = '^"?(?:' + [Regex]::Escape($cardReviewValidationProject.Replace('\', '/') + '/') +
        ')?Assets/_Project/' + $cardReviewValidationAssembly + '/Runtime/.*\.cs"?$'
    $cardReviewValidationResponse = @($cardReviewValidationResponse | Where-Object { $_ -notmatch $cardReviewValidationSourcePattern })
    $cardReviewValidationSources = @(Get-ChildItem -LiteralPath (Join-Path $cardReviewValidationProject ('Assets/_Project/' + $cardReviewValidationAssembly + '/Runtime')) -File -Filter '*.cs' -Recurse | Sort-Object FullName)
    if ($cardReviewValidationSources.Count -eq 0) { throw ('No current production sources found for ' + $cardReviewValidationAssembly) }
    $cardReviewValidationSourceFiles += $cardReviewValidationSources
    $cardReviewValidationResponse += @($cardReviewValidationSources | ForEach-Object { '"' + $_.FullName.Replace('\', '/') + '"' })
    if ($cardReviewValidationResponse | Where-Object { $_.Contains($cardReviewValidationReferencePath) }) { throw 'Old compiler output directory remains referenced.' }
    foreach ($cardReviewValidationOutput in @($cardReviewValidationResponse | Where-Object { $_ -match '^-(out|refout|pdb):' })) {
        if (-not $cardReviewValidationOutput.Contains($cardReviewValidationOutputPath + '/')) { throw ('Unexpected compiler output: ' + $cardReviewValidationOutput) }
    }
    [IO.File]::WriteAllLines((Join-Path $cardReviewValidationCompile ($cardReviewValidationAssembly + '.rsp')), $cardReviewValidationResponse, [Text.UTF8Encoding]::new($false))
}

if ($IncludePlayMode) {
# Compile current PlayMode tests against the same freshly built production code.
# This verifies integration signatures only; no Unity scene or profile is opened.
$cardReviewValidationTestSources = @(Get-ChildItem -LiteralPath (Join-Path $cardReviewValidationProject 'Assets/_Project/Tests/PlayMode') -File -Filter '*.cs' -Recurse | Sort-Object FullName)
$cardReviewValidationSourceFiles += $cardReviewValidationTestSources
$cardReviewValidationTestResponse = @([IO.File]::ReadAllLines($PlayModeResponse) | Where-Object { $_ -notmatch '^"?Assets/_Project/Tests/PlayMode/.*\.cs"?$' } | ForEach-Object {
    if ($_ -match '^-(out|refout|pdb):') {
        $cardReviewValidationKind = $Matches[1]
        $cardReviewValidationSuffix = if ($cardReviewValidationKind -eq 'refout') { '.ref.dll' } elseif ($cardReviewValidationKind -eq 'pdb') { '.pdb' } else { '.dll' }
        '-' + $cardReviewValidationKind + ':"' + $cardReviewValidationOutputPath + '/ShatteredSail.Presentation.PlayModeTests' + $cardReviewValidationSuffix + '"'
    } elseif ($_ -match '^-r:"[^"]*/(ShatteredSail\.(Core|Content|Persistence|Presentation)\.ref\.dll)"$') {
        '-r:"' + $cardReviewValidationOutputPath + '/' + $Matches[1] + '"'
    } else { $_ }
})
$cardReviewValidationTestResponse += @($cardReviewValidationTestSources | ForEach-Object { '"' + $_.FullName.Replace('\', '/') + '"' })
foreach ($cardReviewValidationOutput in @($cardReviewValidationTestResponse | Where-Object { $_ -match '^-(out|refout|pdb):' })) {
    if (-not $cardReviewValidationOutput.Contains($cardReviewValidationOutputPath + '/')) { throw ('Unexpected test compiler output: ' + $cardReviewValidationOutput) }
}
[IO.File]::WriteAllLines((Join-Path $cardReviewValidationCompile 'Presentation.PlayModeTests.rsp'), $cardReviewValidationTestResponse, [Text.UTF8Encoding]::new($false))
$cardReviewValidationAssemblies += 'Presentation.PlayModeTests'


}

if ($IncludeEditMode) {
# Compile current EditMode tests against the same freshly built production code.
# This verifies integration signatures only; no Unity scene or profile is opened.
$cardReviewValidationTestSources = @(Get-ChildItem -LiteralPath (Join-Path $cardReviewValidationProject 'Assets/_Project/Tests/EditMode') -File -Filter '*.cs' -Recurse | Sort-Object FullName)
$cardReviewValidationSourceFiles += $cardReviewValidationTestSources
$cardReviewValidationTestResponse = @([IO.File]::ReadAllLines($EditModeResponse) | Where-Object { $_ -notmatch '^"?Assets/_Project/Tests/EditMode/.*\.cs"?$' } | ForEach-Object {
    if ($_ -match '^-(out|refout|pdb):') {
        $cardReviewValidationKind = $Matches[1]
        $cardReviewValidationSuffix = if ($cardReviewValidationKind -eq 'refout') { '.ref.dll' } elseif ($cardReviewValidationKind -eq 'pdb') { '.pdb' } else { '.dll' }
        '-' + $cardReviewValidationKind + ':"' + $cardReviewValidationOutputPath + '/ShatteredSail.Core.Tests' + $cardReviewValidationSuffix + '"'
    } elseif ($_ -match '^-r:"[^"]*/(ShatteredSail\.(Core|Content|Persistence|Presentation)\.ref\.dll)"$') {
        '-r:"' + $cardReviewValidationOutputPath + '/' + $Matches[1] + '"'
    } else { $_ }
})
$cardReviewValidationTestResponse += @($cardReviewValidationTestSources | ForEach-Object { '"' + $_.FullName.Replace('\', '/') + '"' })
foreach ($cardReviewValidationOutput in @($cardReviewValidationTestResponse | Where-Object { $_ -match '^-(out|refout|pdb):' })) {
    if (-not $cardReviewValidationOutput.Contains($cardReviewValidationOutputPath + '/')) { throw ('Unexpected test compiler output: ' + $cardReviewValidationOutput) }
}
[IO.File]::WriteAllLines((Join-Path $cardReviewValidationCompile 'Core.Tests.rsp'), $cardReviewValidationTestResponse, [Text.UTF8Encoding]::new($false))
$cardReviewValidationAssemblies += 'Core.Tests'


}

$cardReviewValidationHashes = @($cardReviewValidationSourceFiles | ForEach-Object {
    [pscustomobject]@{ Source = $_.FullName; SHA256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
})
$cardReviewValidationHashes | Export-Csv -LiteralPath (Join-Path $cardReviewValidationCompile 'source-hashes.csv') -NoTypeInformation -Encoding utf8
if ($PrepareOnly) { Write-Output ('Prepared production responses: ' + $cardReviewValidationCompile); return }

$cardReviewValidationResults = [Collections.Generic.List[string]]::new()
try {
    Push-Location $cardReviewValidationProject
    try {
        foreach ($cardReviewValidationAssembly in $cardReviewValidationAssemblies) {
            $cardReviewValidationResponsePath = Join-Path $cardReviewValidationCompile ($cardReviewValidationAssembly + '.rsp')
            $cardReviewValidationLog = Join-Path $cardReviewValidationCompile ($cardReviewValidationAssembly + '.log')
            ('Compiling current production assembly: ' + $cardReviewValidationAssembly) | Set-Content -LiteralPath $cardReviewValidationLog -Encoding utf8
            & (Join-Path $UnityData 'NetCoreRuntime/dotnet.exe') (Join-Path $UnityData 'DotNetSdkRoslyn/csc.dll') ('@' + $cardReviewValidationResponsePath) 2>&1 |
                Tee-Object -FilePath $cardReviewValidationLog -Append
            if ($LASTEXITCODE -ne 0) { throw ('Production compilation failed: ' + $cardReviewValidationAssembly) }
            ('PASS ' + $cardReviewValidationAssembly) | Add-Content -LiteralPath $cardReviewValidationLog -Encoding utf8
            $cardReviewValidationResults.Add('PASS ' + $cardReviewValidationAssembly)
            Write-Output ('PASS ' + $cardReviewValidationAssembly)
        }
    } finally { Pop-Location }

    foreach ($cardReviewValidationHash in $cardReviewValidationHashes) {
        if ((Get-FileHash -LiteralPath $cardReviewValidationHash.Source -Algorithm SHA256).Hash -ne $cardReviewValidationHash.SHA256) {
            throw ('Production source changed during compilation: ' + $cardReviewValidationHash.Source)
        }
    }
    if ($JournalPluginDirectory) {
        $journalPlugins = [IO.Path]::GetFullPath($JournalPluginDirectory)
        $journalProject = Split-Path (Split-Path $journalPlugins -Parent) -Parent
        $journalTemporaryRoot = [IO.Path]::GetFullPath((Join-Path $cardReviewValidationWorkspace 'tmp')).TrimEnd('\','/')
        if ([IO.Path]::GetDirectoryName($journalProject) -ne $journalTemporaryRoot -or
            [IO.Path]::GetFileName($journalProject) -notmatch '^card-game-view-20261030-[a-f0-9]{32}$' -or
            $journalPlugins -ne (Join-Path $journalProject 'Assets/Plugins') -or
            !(Test-Path -LiteralPath (Join-Path $journalProject 'OWNED_CARD_GAME_VIEW.txt'))) { throw 'Unexpected journal validation project.' }
        foreach ($journalAssembly in @('Core','Content','Persistence','Presentation')) {
            Copy-Item -LiteralPath (Join-Path $cardReviewValidationCompile ('ShatteredSail.'+$journalAssembly+'.dll')) -Destination $journalPlugins
        }
    }
    $cardReviewValidationResults.Add('PASS source hashes stable across all requested assemblies')
    $cardReviewValidationResults.Add('Used existing Unity reference assemblies read-only. No Editor, isolated Unity project, scene, or preview generator was launched.')
    [IO.File]::WriteAllLines((Join-Path $cardReviewValidationCompile 'validation.txt'), $cardReviewValidationResults, [Text.UTF8Encoding]::new($false))
} finally {
    # Compiler outputs are needed only until all four sequential assemblies finish.
    # Keep response files, source hashes and logs; never clean UnityClient or the
    # shared reference-response directory used by other existing tools.
    Assert-CardReviewValidationCompileDirectory
    $cardReviewValidationConsumers = @(Get-CimInstance Win32_Process -Filter "Name='mono.exe' OR Name='dotnet.exe'" |
        Where-Object { $_.CommandLine -like ('*' + $cardReviewValidationCompile + '*') -or $_.CommandLine -like ('*' + $cardReviewValidationOutputPath + '*') })
    if ($cardReviewValidationConsumers.Count -gt 0) { throw 'A compiler or test process still references the task outputs; cleanup deferred.' }
    $cardReviewValidationReleased = 0L
    $cardReviewValidationRemoved = [Collections.Generic.List[string]]::new()
    foreach ($cardReviewValidationAssembly in $cardReviewValidationAssemblies) {
        foreach ($cardReviewValidationSuffix in @('.dll', '.pdb', '.ref.dll')) {
            $cardReviewValidationFile = [IO.Path]::GetFullPath((Join-Path $cardReviewValidationCompile ('ShatteredSail.' + $cardReviewValidationAssembly + $cardReviewValidationSuffix)))
            if ([IO.Path]::GetDirectoryName($cardReviewValidationFile) -ne $cardReviewValidationCompile) { throw 'Unexpected cleanup target.' }
            if (Test-Path -LiteralPath $cardReviewValidationFile) {
                $cardReviewValidationItem = Get-Item -LiteralPath $cardReviewValidationFile
                if ($cardReviewValidationItem.PSIsContainer -or ($cardReviewValidationItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                    throw ('Refusing unexpected compiler artifact: ' + $cardReviewValidationFile)
                }
                $cardReviewValidationReleased += $cardReviewValidationItem.Length
                Remove-Item -LiteralPath $cardReviewValidationFile -Force
                if (Test-Path -LiteralPath $cardReviewValidationFile) { throw ('Cleanup failed: ' + $cardReviewValidationFile) }
                $cardReviewValidationRemoved.Add($cardReviewValidationItem.Name)
            }
        }
    }
    [pscustomobject]@{ ReleasedBytes = $cardReviewValidationReleased; RemovedFiles = @($cardReviewValidationRemoved); EvidenceRetained = $true;
        MainProjectCleaned = $false; SharedReferenceAssembliesRetained = $ReferenceResponses } |
        ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $cardReviewValidationCompile 'cleanup.json') -Encoding utf8
    Write-Output ('Cleaned task compiler artifacts: ' + $cardReviewValidationReleased + ' bytes; evidence retained.')
    $cardReviewHistoryPath=Join-Path $cardReviewValidationCompile 'cleanup-history.json'
    $cardReviewHistory=@()
    if(Test-Path -LiteralPath $cardReviewHistoryPath){$cardReviewHistory=@(Get-Content -LiteralPath $cardReviewHistoryPath -Raw | ConvertFrom-Json)}
    $cardReviewHistory += [pscustomobject]@{Run=$cardReviewHistory.Count+1;Purpose=($cardReviewValidationAssemblies -join ', ');ReleasedBytes=$cardReviewValidationReleased;CompletedUtc=[DateTime]::UtcNow.ToString('o');Evidence='Current cleanup.json and compiler logs'}
    $cardReviewHistory | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $cardReviewHistoryPath -Encoding utf8
}
Write-Output ('PASS production validation: ' + $cardReviewValidationCompile)
