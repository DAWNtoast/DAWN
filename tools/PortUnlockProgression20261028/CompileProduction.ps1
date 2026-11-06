param(
    [string]$ReferenceResponses = 'D:/Game/Artifacts/CardNumbering20261017/UiCompile',
    [string]$UnityData = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data',
    [string]$PlayModeResponse = 'D:/Game/UnityClient/Library/Bee/artifacts/1900b0aE.dag/ShatteredSail.Presentation.PlayModeTests.rsp',
    [switch]$IncludePlayMode,
    [string]$EditModeResponse = 'D:/Game/UnityClient/Library/Bee/artifacts/1900b0aE.dag/ShatteredSail.Core.Tests.rsp',
    [switch]$IncludeEditMode,
    [switch]$PrepareOnly
)
$ErrorActionPreference = 'Stop'
$portValidationWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$portValidationProject = Join-Path $portValidationWorkspace 'UnityClient'
$portValidationCompile = [IO.Path]::GetFullPath((Join-Path $portValidationWorkspace 'outputs/port-unlock-progression-20261028/production-compile'))
$portValidationReferencePath = [IO.Path]::GetFullPath($ReferenceResponses).Replace('\', '/')
$portValidationOutputPath = $portValidationCompile.Replace('\', '/')
$portValidationAssemblies = @('Core', 'Content', 'Persistence', 'Presentation')
$portValidationSourceFiles = @()

function Assert-PortValidationCompileDirectory {
    $portValidationExpected = [IO.Path]::GetFullPath((Join-Path $portValidationWorkspace 'outputs/port-unlock-progression-20261028/production-compile'))
    if ($portValidationCompile -ne $portValidationExpected) { throw 'Unexpected production compile directory.' }
    foreach ($portValidationDirectory in @($portValidationWorkspace, (Join-Path $portValidationWorkspace 'outputs'),
        (Join-Path $portValidationWorkspace 'outputs/port-unlock-progression-20261028'), $portValidationCompile)) {
        if ((Test-Path -LiteralPath $portValidationDirectory) -and
            (((Get-Item -LiteralPath $portValidationDirectory -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0)) {
            throw ('Refusing linked compile directory: ' + $portValidationDirectory)
        }
    }
}
Assert-PortValidationCompileDirectory
New-Item -ItemType Directory -Force -Path $portValidationCompile | Out-Null
Assert-PortValidationCompileDirectory

foreach ($portValidationAssembly in $portValidationAssemblies) {
    $portValidationResponse = [IO.File]::ReadAllLines((Join-Path $ReferenceResponses ($portValidationAssembly + '.rsp'))) |
        ForEach-Object { $_.Replace($portValidationReferencePath, $portValidationOutputPath) }
    $portValidationSourcePattern = '^"?(?:' + [Regex]::Escape($portValidationProject.Replace('\', '/') + '/') +
        ')?Assets/_Project/' + $portValidationAssembly + '/Runtime/.*\.cs"?$'
    $portValidationResponse = @($portValidationResponse | Where-Object { $_ -notmatch $portValidationSourcePattern })
    $portValidationSources = @(Get-ChildItem -LiteralPath (Join-Path $portValidationProject ('Assets/_Project/' + $portValidationAssembly + '/Runtime')) -File -Filter '*.cs' -Recurse | Sort-Object FullName)
    if ($portValidationSources.Count -eq 0) { throw ('No current production sources found for ' + $portValidationAssembly) }
    $portValidationSourceFiles += $portValidationSources
    $portValidationResponse += @($portValidationSources | ForEach-Object { '"' + $_.FullName.Replace('\', '/') + '"' })
    if ($portValidationResponse | Where-Object { $_.Contains($portValidationReferencePath) }) { throw 'Old compiler output directory remains referenced.' }
    foreach ($portValidationOutput in @($portValidationResponse | Where-Object { $_ -match '^-(out|refout|pdb):' })) {
        if (-not $portValidationOutput.Contains($portValidationOutputPath + '/')) { throw ('Unexpected compiler output: ' + $portValidationOutput) }
    }
    [IO.File]::WriteAllLines((Join-Path $portValidationCompile ($portValidationAssembly + '.rsp')), $portValidationResponse, [Text.UTF8Encoding]::new($false))
}

if ($IncludePlayMode) {
# Compile current PlayMode tests against the same freshly built production code.
# This verifies integration signatures only; no Unity scene or profile is opened.
$portValidationTestSources = @(Get-ChildItem -LiteralPath (Join-Path $portValidationProject 'Assets/_Project/Tests/PlayMode') -File -Filter '*.cs' -Recurse | Sort-Object FullName)
$portValidationSourceFiles += $portValidationTestSources
$portValidationTestResponse = @([IO.File]::ReadAllLines($PlayModeResponse) | Where-Object { $_ -notmatch '^"?Assets/_Project/Tests/PlayMode/.*\.cs"?$' } | ForEach-Object {
    if ($_ -match '^-(out|refout|pdb):') {
        $portValidationKind = $Matches[1]
        $portValidationSuffix = if ($portValidationKind -eq 'refout') { '.ref.dll' } elseif ($portValidationKind -eq 'pdb') { '.pdb' } else { '.dll' }
        '-' + $portValidationKind + ':"' + $portValidationOutputPath + '/ShatteredSail.Presentation.PlayModeTests' + $portValidationSuffix + '"'
    } elseif ($_ -match '^-r:"[^"]*/(ShatteredSail\.(Core|Content|Persistence|Presentation)\.ref\.dll)"$') {
        '-r:"' + $portValidationOutputPath + '/' + $Matches[1] + '"'
    } else { $_ }
})
$portValidationTestResponse += @($portValidationTestSources | ForEach-Object { '"' + $_.FullName.Replace('\', '/') + '"' })
foreach ($portValidationOutput in @($portValidationTestResponse | Where-Object { $_ -match '^-(out|refout|pdb):' })) {
    if (-not $portValidationOutput.Contains($portValidationOutputPath + '/')) { throw ('Unexpected test compiler output: ' + $portValidationOutput) }
}
[IO.File]::WriteAllLines((Join-Path $portValidationCompile 'Presentation.PlayModeTests.rsp'), $portValidationTestResponse, [Text.UTF8Encoding]::new($false))
$portValidationAssemblies += 'Presentation.PlayModeTests'


}

if ($IncludeEditMode) {
# Compile current EditMode tests against the same freshly built production code.
# This verifies integration signatures only; no Unity scene or profile is opened.
$portValidationTestSources = @(Get-ChildItem -LiteralPath (Join-Path $portValidationProject 'Assets/_Project/Tests/EditMode') -File -Filter '*.cs' -Recurse | Sort-Object FullName)
$portValidationSourceFiles += $portValidationTestSources
$portValidationTestResponse = @([IO.File]::ReadAllLines($EditModeResponse) | Where-Object { $_ -notmatch '^"?Assets/_Project/Tests/EditMode/.*\.cs"?$' } | ForEach-Object {
    if ($_ -match '^-(out|refout|pdb):') {
        $portValidationKind = $Matches[1]
        $portValidationSuffix = if ($portValidationKind -eq 'refout') { '.ref.dll' } elseif ($portValidationKind -eq 'pdb') { '.pdb' } else { '.dll' }
        '-' + $portValidationKind + ':"' + $portValidationOutputPath + '/ShatteredSail.Core.Tests' + $portValidationSuffix + '"'
    } elseif ($_ -match '^-r:"[^"]*/(ShatteredSail\.(Core|Content|Persistence|Presentation)\.ref\.dll)"$') {
        '-r:"' + $portValidationOutputPath + '/' + $Matches[1] + '"'
    } else { $_ }
})
$portValidationTestResponse += @($portValidationTestSources | ForEach-Object { '"' + $_.FullName.Replace('\', '/') + '"' })
foreach ($portValidationOutput in @($portValidationTestResponse | Where-Object { $_ -match '^-(out|refout|pdb):' })) {
    if (-not $portValidationOutput.Contains($portValidationOutputPath + '/')) { throw ('Unexpected test compiler output: ' + $portValidationOutput) }
}
[IO.File]::WriteAllLines((Join-Path $portValidationCompile 'Core.Tests.rsp'), $portValidationTestResponse, [Text.UTF8Encoding]::new($false))
$portValidationAssemblies += 'Core.Tests'


}

$portValidationHashes = @($portValidationSourceFiles | ForEach-Object {
    [pscustomobject]@{ Source = $_.FullName; SHA256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
})
$portValidationHashes | Export-Csv -LiteralPath (Join-Path $portValidationCompile 'source-hashes.csv') -NoTypeInformation -Encoding utf8
if ($PrepareOnly) { Write-Output ('Prepared production responses: ' + $portValidationCompile); return }

$portValidationResults = [Collections.Generic.List[string]]::new()
try {
    Push-Location $portValidationProject
    try {
        foreach ($portValidationAssembly in $portValidationAssemblies) {
            $portValidationResponsePath = Join-Path $portValidationCompile ($portValidationAssembly + '.rsp')
            $portValidationLog = Join-Path $portValidationCompile ($portValidationAssembly + '.log')
            ('Compiling current production assembly: ' + $portValidationAssembly) | Set-Content -LiteralPath $portValidationLog -Encoding utf8
            & (Join-Path $UnityData 'NetCoreRuntime/dotnet.exe') (Join-Path $UnityData 'DotNetSdkRoslyn/csc.dll') ('@' + $portValidationResponsePath) 2>&1 |
                Tee-Object -FilePath $portValidationLog -Append
            if ($LASTEXITCODE -ne 0) { throw ('Production compilation failed: ' + $portValidationAssembly) }
            ('PASS ' + $portValidationAssembly) | Add-Content -LiteralPath $portValidationLog -Encoding utf8
            $portValidationResults.Add('PASS ' + $portValidationAssembly)
            Write-Output ('PASS ' + $portValidationAssembly)
        }
    } finally { Pop-Location }

    foreach ($portValidationHash in $portValidationHashes) {
        if ((Get-FileHash -LiteralPath $portValidationHash.Source -Algorithm SHA256).Hash -ne $portValidationHash.SHA256) {
            throw ('Production source changed during compilation: ' + $portValidationHash.Source)
        }
    }
    $portValidationResults.Add('PASS source hashes stable across all requested assemblies')
    $portValidationResults.Add('Used existing Unity reference assemblies read-only. No Editor, isolated Unity project, scene, or preview generator was launched.')
    [IO.File]::WriteAllLines((Join-Path $portValidationCompile 'validation.txt'), $portValidationResults, [Text.UTF8Encoding]::new($false))
} finally {
    # Compiler outputs are needed only until all four sequential assemblies finish.
    # Keep response files, source hashes and logs; never clean UnityClient or the
    # shared reference-response directory used by other existing tools.
    Assert-PortValidationCompileDirectory
    $portValidationConsumers = @(Get-CimInstance Win32_Process -Filter "Name='mono.exe' OR Name='dotnet.exe'" |
        Where-Object { $_.CommandLine -like ('*' + $portValidationCompile + '*') -or $_.CommandLine -like ('*' + $portValidationOutputPath + '*') })
    if ($portValidationConsumers.Count -gt 0) { throw 'A compiler or test process still references the task outputs; cleanup deferred.' }
    $portValidationReleased = 0L
    $portValidationRemoved = [Collections.Generic.List[string]]::new()
    foreach ($portValidationAssembly in $portValidationAssemblies) {
        foreach ($portValidationSuffix in @('.dll', '.pdb', '.ref.dll')) {
            $portValidationFile = [IO.Path]::GetFullPath((Join-Path $portValidationCompile ('ShatteredSail.' + $portValidationAssembly + $portValidationSuffix)))
            if ([IO.Path]::GetDirectoryName($portValidationFile) -ne $portValidationCompile) { throw 'Unexpected cleanup target.' }
            if (Test-Path -LiteralPath $portValidationFile) {
                $portValidationItem = Get-Item -LiteralPath $portValidationFile
                if ($portValidationItem.PSIsContainer -or ($portValidationItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                    throw ('Refusing unexpected compiler artifact: ' + $portValidationFile)
                }
                $portValidationReleased += $portValidationItem.Length
                Remove-Item -LiteralPath $portValidationFile -Force
                if (Test-Path -LiteralPath $portValidationFile) { throw ('Cleanup failed: ' + $portValidationFile) }
                $portValidationRemoved.Add($portValidationItem.Name)
            }
        }
    }
    [pscustomobject]@{ ReleasedBytes = $portValidationReleased; RemovedFiles = @($portValidationRemoved); EvidenceRetained = $true;
        MainProjectCleaned = $false; SharedReferenceAssembliesRetained = $ReferenceResponses } |
        ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $portValidationCompile 'cleanup.json') -Encoding utf8
    Write-Output ('Cleaned task compiler artifacts: ' + $portValidationReleased + ' bytes; evidence retained.')
}
Write-Output ('PASS production validation: ' + $portValidationCompile)
