# Offline compile for the wordless opening and port-arrival comics.
# Only this task's compile directory receives outputs; source and existing Unity dependencies are read-only.
param(
    [string]$ReferenceResponses = 'D:/Game/Artifacts/CardNumbering20261017/UiCompile',
    [string]$UnityData = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data',
    [switch]$CheckPlaybackTests
)

$ErrorActionPreference = 'Stop'
$fleetWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$fleetProject = Join-Path $fleetWorkspace 'UnityClient'
$fleetCompileFolder = 'compile'
$fleetCompile = [IO.Path]::GetFullPath((Join-Path $fleetWorkspace ('outputs/opening-port-comics-20261031/' + $fleetCompileFolder)))
$fleetReferenceRoot = [IO.Path]::GetFullPath($ReferenceResponses)
$fleetReferenceSlash = $fleetReferenceRoot.Replace('\', '/')
$fleetOutputSlash = $fleetCompile.Replace('\', '/')
$fleetAssemblies = @('Core', 'Content', 'Persistence', 'Presentation')
$fleetDotnet = Join-Path $UnityData 'NetCoreRuntime/dotnet.exe'
$fleetCompiler = Join-Path $UnityData 'DotNetSdkRoslyn/csc.dll'
$fleetSourceFiles = @()
$fleetSourceHashes = @()
$fleetReferenceHashes = @()
$fleetResponseHashes = @()
$fleetAssemblyHashes = @()
$fleetBusinessReferences = @()
$fleetResults = [Collections.Generic.List[string]]::new()
$fleetSucceeded = $false
$fleetFailure = ''
$fleetTemporaryNames = @()
$fleetPlaybackSources = @('OpeningPortComicProgressTests', 'OpeningPortComicPlaybackTests', 'ArrivalComicCatalogTests') |
    ForEach-Object { Join-Path $fleetProject ('Assets/_Project/Tests/EditMode/' + $_ + '.cs') }
foreach ($fleetAssembly in $fleetAssemblies) {
    $fleetTemporaryNames += $fleetAssembly + '.rsp'
    foreach ($fleetSuffix in @('.dll', '.ref.dll', '.pdb')) {
        $fleetTemporaryNames += 'ShatteredSail.' + $fleetAssembly + $fleetSuffix
    }
}
if ($CheckPlaybackTests) {
    $fleetTemporaryNames += @('PlaybackTests.rsp', 'ShatteredSail.PlaybackTests.dll',
        'ShatteredSail.PlaybackTests.ref.dll', 'ShatteredSail.PlaybackTests.pdb')
}

function Assert-FleetCompilePath {
    $fleetExpectedFolder = 'compile'
    $fleetExpected = [IO.Path]::GetFullPath((Join-Path $fleetWorkspace ('outputs/opening-port-comics-20261031/' + $fleetExpectedFolder)))
    if ($fleetCompile -ne $fleetExpected) { throw 'Unexpected task compilation directory.' }
    $fleetCursor = $fleetCompile
    while ($fleetCursor) {
        if ((Test-Path -LiteralPath $fleetCursor) -and
            (((Get-Item -LiteralPath $fleetCursor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0)) {
            throw ('Refusing linked compilation path: ' + $fleetCursor)
        }
        $fleetCursor = Split-Path -Path $fleetCursor -Parent
    }
}

function Get-FleetCompileConsumers {
    @(Get-CimInstance Win32_Process -Filter "Name='dotnet.exe' OR Name='mono.exe' OR Name='Unity.exe'" |
        Where-Object { $_.CommandLine -and
            $_.CommandLine.Replace('/', '\').IndexOf($fleetCompile.Replace('/', '\'), [StringComparison]::OrdinalIgnoreCase) -ge 0 })
}

Assert-FleetCompilePath
foreach ($fleetRequired in @($fleetDotnet, $fleetCompiler)) {
    if (-not (Test-Path -LiteralPath $fleetRequired -PathType Leaf)) {
        throw ('Missing existing compiler dependency: ' + $fleetRequired)
    }
}
if (@(Get-FleetCompileConsumers).Count -gt 0) { throw 'Task compilation output is already in use.' }
New-Item -ItemType Directory -Path $fleetCompile -Force | Out-Null
Assert-FleetCompilePath

try {
    foreach ($fleetAssembly in $fleetAssemblies) {
        $fleetTemplate = Join-Path $fleetReferenceRoot ($fleetAssembly + '.rsp')
        if (-not (Test-Path -LiteralPath $fleetTemplate -PathType Leaf)) {
            throw ('Missing read-only response template: ' + $fleetTemplate)
        }
        $fleetReferenceHashes += [pscustomobject]@{
            Source = $fleetTemplate
            SHA256 = (Get-FileHash -LiteralPath $fleetTemplate -Algorithm SHA256).Hash
        }
        $fleetResponse = @([IO.File]::ReadAllLines($fleetTemplate) |
            ForEach-Object { $_.Replace($fleetReferenceSlash, $fleetOutputSlash) })
        $fleetSourcePattern = '^"?(?:' + [Regex]::Escape($fleetProject.Replace('\', '/') + '/') +
            ')?Assets/_Project/' + $fleetAssembly + '/Runtime/.*\.cs"?$'
        $fleetResponse = @($fleetResponse | Where-Object { $_ -notmatch $fleetSourcePattern -and $_ -notmatch '^[-/]noconfig$' })
        $fleetSources = @(Get-ChildItem -LiteralPath (Join-Path $fleetProject ('Assets/_Project/' + $fleetAssembly + '/Runtime')) -File -Filter '*.cs' -Recurse |
            Sort-Object FullName)
        if ($fleetSources.Count -eq 0) { throw ('No current source files for ' + $fleetAssembly) }
        $fleetSourceFiles += $fleetSources
        $fleetResponse += @($fleetSources | ForEach-Object { '"' + $_.FullName.Replace('\', '/') + '"' })
        if ($fleetResponse | Where-Object { $_.Contains($fleetReferenceSlash) }) {
            throw 'A generated response still references the shared compiler output directory.'
        }
        $fleetExpectedReferences = switch ($fleetAssembly) {
            'Core' { @() }
            'Content' { @('ShatteredSail.Core.ref.dll') }
            'Persistence' { @('ShatteredSail.Core.ref.dll') }
            'Presentation' { @('ShatteredSail.Core.ref.dll', 'ShatteredSail.Content.ref.dll', 'ShatteredSail.Persistence.ref.dll') }
        }
        $fleetSeenReferences = @()
        foreach ($fleetReferenceOption in @($fleetResponse | Where-Object { $_ -match '^[-/](r|reference):.*ShatteredSail\.' })) {
            $fleetReferenceValue = ($fleetReferenceOption -replace '^[-/][^:]+:', '').Trim('"')
            $fleetResolvedReference = [IO.Path]::GetFullPath($fleetReferenceValue)
            $fleetReferenceName = [IO.Path]::GetFileName($fleetResolvedReference)
            if ([IO.Path]::GetDirectoryName($fleetResolvedReference) -ne $fleetCompile -or
                $fleetReferenceName -notin $fleetExpectedReferences) {
                throw ('Business assembly reference must point to this compilation: ' + $fleetReferenceOption)
            }
            $fleetSeenReferences += $fleetReferenceName
            $fleetBusinessReferences += [pscustomobject]@{ Assembly = $fleetAssembly; Reference = $fleetResolvedReference }
        }
        foreach ($fleetExpectedReference in $fleetExpectedReferences) {
            if ($fleetExpectedReference -notin $fleetSeenReferences) { throw ('Missing current business dependency: ' + $fleetExpectedReference) }
        }
        $fleetOutputOptions = @($fleetResponse | Where-Object { $_ -match '^[-/](out|refout|pdb|doc|errorlog|generatedfilesout):' })
        if (@($fleetOutputOptions | Where-Object { $_ -match '^[-/]out:' }).Count -ne 1) {
            throw ('Expected one explicit compiler output for ' + $fleetAssembly)
        }
        foreach ($fleetOutputOption in $fleetOutputOptions) {
            $fleetOptionValue = ($fleetOutputOption -replace '^[-/][^:]+:', '').Trim('"')
            $fleetResolvedOutput = [IO.Path]::GetFullPath($fleetOptionValue)
            if ([IO.Path]::GetDirectoryName($fleetResolvedOutput) -ne $fleetCompile -or
                [IO.Path]::GetFileName($fleetResolvedOutput) -notin $fleetTemporaryNames) {
                throw ('Refusing compiler output outside the known task artifact list: ' + $fleetOutputOption)
            }
        }
        $fleetResponsePath = Join-Path $fleetCompile ($fleetAssembly + '.rsp')
        [IO.File]::WriteAllLines($fleetResponsePath, $fleetResponse, [Text.UTF8Encoding]::new($false))
        $fleetResponseHashes += [pscustomobject]@{
            Assembly = $fleetAssembly
            SourceCount = $fleetSources.Count
            SHA256 = (Get-FileHash -LiteralPath $fleetResponsePath -Algorithm SHA256).Hash
        }
    }

    if ($CheckPlaybackTests) { $fleetSourceFiles += @($fleetPlaybackSources | ForEach-Object { Get-Item -LiteralPath $_ }) }
    $fleetSourceHashes = @($fleetSourceFiles | ForEach-Object {
        [pscustomobject]@{ Source = $_.FullName; SHA256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
    })
    $fleetSourceHashes | Export-Csv -LiteralPath (Join-Path $fleetCompile 'source-hashes.csv') -NoTypeInformation -Encoding utf8
    $fleetReferenceHashes | Export-Csv -LiteralPath (Join-Path $fleetCompile 'reference-response-hashes.csv') -NoTypeInformation -Encoding utf8
    $fleetResponseHashes | Export-Csv -LiteralPath (Join-Path $fleetCompile 'response-hashes.csv') -NoTypeInformation -Encoding utf8
    $fleetBusinessReferences | Export-Csv -LiteralPath (Join-Path $fleetCompile 'business-references.csv') -NoTypeInformation -Encoding utf8

    Push-Location $fleetProject
    try {
        foreach ($fleetAssembly in $fleetAssemblies) {
            $fleetResponsePath = Join-Path $fleetCompile ($fleetAssembly + '.rsp')
            $fleetLog = Join-Path $fleetCompile ($fleetAssembly + '.log')
            ('Compiling current production assembly: ' + $fleetAssembly) | Set-Content -LiteralPath $fleetLog -Encoding utf8
            & $fleetDotnet $fleetCompiler '-noconfig' ('@' + $fleetResponsePath) 2>&1 | Tee-Object -FilePath $fleetLog -Append
            if ($LASTEXITCODE -ne 0) { throw ('Production compilation failed: ' + $fleetAssembly) }
            $fleetResults.Add('PASS ' + $fleetAssembly)
            ('PASS ' + $fleetAssembly) | Add-Content -LiteralPath $fleetLog -Encoding utf8
            Write-Output ('PASS ' + $fleetAssembly)
            foreach ($fleetSuffix in @('.dll', '.ref.dll', '.pdb')) {
                $fleetArtifact = Join-Path $fleetCompile ('ShatteredSail.' + $fleetAssembly + $fleetSuffix)
                if (Test-Path -LiteralPath $fleetArtifact -PathType Leaf) {
                    $fleetAssemblyHashes += [pscustomobject]@{
                        Artifact = [IO.Path]::GetFileName($fleetArtifact)
                        Bytes = (Get-Item -LiteralPath $fleetArtifact).Length
                        SHA256 = (Get-FileHash -LiteralPath $fleetArtifact -Algorithm SHA256).Hash
                    }
                }
            }
        }
        if ($CheckPlaybackTests) {
            # Keep framework/Unity/NUnit options, but replace every output and source.
            # Business dependencies are the four assemblies just compiled above.
            $fleetPlaybackResponse = @([IO.File]::ReadAllLines((Join-Path $fleetCompile 'Presentation.rsp')) |
                Where-Object { $_ -match '^[-/]' -and
                    $_ -notmatch '^[-/](out|refout|pdb|doc|errorlog|generatedfilesout|analyzer|additionalfile|noconfig):?' })
            $fleetPlaybackResponse += '-r:"' + (Join-Path $fleetCompile 'ShatteredSail.Presentation.ref.dll') + '"'
            $fleetPlaybackResponse += '-out:"' + (Join-Path $fleetCompile 'ShatteredSail.PlaybackTests.dll') + '"'
            $fleetPlaybackResponse += '-refout:"' + (Join-Path $fleetCompile 'ShatteredSail.PlaybackTests.ref.dll') + '"'
            $fleetPlaybackResponse += @($fleetPlaybackSources | ForEach-Object { '"' + $_ + '"' })
            $fleetPlaybackResponsePath = Join-Path $fleetCompile 'PlaybackTests.rsp'
            [IO.File]::WriteAllLines($fleetPlaybackResponsePath, $fleetPlaybackResponse, [Text.UTF8Encoding]::new($false))
            $fleetResponseHashes += [pscustomobject]@{
                Assembly = 'PlaybackTests'
                SourceCount = $fleetPlaybackSources.Count
                SHA256 = (Get-FileHash -LiteralPath $fleetPlaybackResponsePath -Algorithm SHA256).Hash
            }
            $fleetResponseHashes | Export-Csv -LiteralPath (Join-Path $fleetCompile 'response-hashes.csv') -NoTypeInformation -Encoding utf8
            $fleetPlaybackLog = Join-Path $fleetCompile 'PlaybackTests.log'
            'Compiling current wordless opening/arrival Unity/NUnit fixtures without running Unity.' |
                Set-Content -LiteralPath $fleetPlaybackLog -Encoding utf8
            & $fleetDotnet $fleetCompiler '-noconfig' ('@' + $fleetPlaybackResponsePath) 2>&1 |
                Tee-Object -FilePath $fleetPlaybackLog -Append
            if ($LASTEXITCODE -ne 0) { throw 'Playback fixture compilation failed.' }
            $fleetResults.Add('PASS PlaybackTests syntax compilation (not executed)')
            Write-Output 'PASS PlaybackTests syntax compilation (not executed)'
            foreach ($fleetSuffix in @('.dll', '.ref.dll', '.pdb')) {
                $fleetArtifact = Join-Path $fleetCompile ('ShatteredSail.PlaybackTests' + $fleetSuffix)
                if (Test-Path -LiteralPath $fleetArtifact -PathType Leaf) {
                    $fleetAssemblyHashes += [pscustomobject]@{
                        Artifact = [IO.Path]::GetFileName($fleetArtifact)
                        Bytes = (Get-Item -LiteralPath $fleetArtifact).Length
                        SHA256 = (Get-FileHash -LiteralPath $fleetArtifact -Algorithm SHA256).Hash
                    }
                }
            }
        }
    } finally { Pop-Location }

    foreach ($fleetInputHash in @($fleetSourceHashes) + @($fleetReferenceHashes)) {
        if ((Get-FileHash -LiteralPath $fleetInputHash.Source -Algorithm SHA256).Hash -ne $fleetInputHash.SHA256) {
            throw ('Compilation input changed during verification: ' + $fleetInputHash.Source)
        }
    }
    $fleetResults.Add('PASS current source and read-only response template hashes remain stable')
    $fleetResults.Add('Compiled all current Core, Content, Persistence and Presentation Runtime sources using existing Unity dependencies. No Unity Editor or copied project was used.')
    $fleetSucceeded = $true
} catch {
    $fleetFailure = $_.Exception.Message
    $fleetResults.Add('FAIL ' + $fleetFailure)
    throw
} finally {
    Assert-FleetCompilePath
    [IO.File]::WriteAllLines((Join-Path $fleetCompile 'validation.txt'), $fleetResults, [Text.UTF8Encoding]::new($false))
    if ($fleetAssemblyHashes.Count -gt 0) {
        $fleetAssemblyHashes | Export-Csv -LiteralPath (Join-Path $fleetCompile 'assembly-hashes.csv') -NoTypeInformation -Encoding utf8
    }
    $fleetConsumers = @(Get-FleetCompileConsumers)
    $fleetReleased = 0L
    $fleetRemoved = [Collections.Generic.List[string]]::new()
    if ($fleetConsumers.Count -eq 0) {
        foreach ($fleetTemporaryName in $fleetTemporaryNames) {
            $fleetTemporaryPath = [IO.Path]::GetFullPath((Join-Path $fleetCompile $fleetTemporaryName))
            if ([IO.Path]::GetDirectoryName($fleetTemporaryPath) -ne $fleetCompile) { throw 'Unexpected cleanup path.' }
            if (-not (Test-Path -LiteralPath $fleetTemporaryPath)) { continue }
            $fleetTemporaryItem = Get-Item -LiteralPath $fleetTemporaryPath -Force
            if ($fleetTemporaryItem.PSIsContainer -or
                (($fleetTemporaryItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0)) {
                throw ('Refusing linked or non-file compiler artifact: ' + $fleetTemporaryPath)
            }
            $fleetReleased += $fleetTemporaryItem.Length
            Remove-Item -LiteralPath $fleetTemporaryPath -Force
            if (Test-Path -LiteralPath $fleetTemporaryPath) { throw ('Compiler artifact cleanup failed: ' + $fleetTemporaryPath) }
            $fleetRemoved.Add($fleetTemporaryName)
        }
    }
    [pscustomobject]@{
        CompilationPassed = $fleetSucceeded
        Failure = $fleetFailure
        ReleasedBytes = $fleetReleased
        RemovedFiles = @($fleetRemoved)
        CleanupDeferred = $fleetConsumers.Count -gt 0
        RemainingConsumerIds = @($fleetConsumers | ForEach-Object { $_.ProcessId })
        EvidenceRetained = $true
        MainProjectCleaned = $false
        SharedReferenceResponsesRetained = $fleetReferenceRoot
    } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $fleetCompile 'cleanup.json') -Encoding utf8
    if ($fleetConsumers.Count -gt 0) {
        Write-Warning 'A process still references this task output. Compiler artifacts were retained until it exits.'
    } else {
        Write-Output ('Cleaned task DLL/PDB/response artifacts: ' + $fleetReleased + ' bytes; hashes and logs retained.')
    }
}
Write-Output ('PASS production compilation: ' + $fleetCompile)
