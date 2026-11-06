param(
    [string]$ReferenceResponses = 'D:/Game/Artifacts/CardNumbering20261017/UiCompile',
    [string]$UnityData = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data'
)

$ErrorActionPreference = 'Stop'
$horizonWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$horizonProject = Join-Path $horizonWorkspace 'UnityClient'
$horizonCompile = [IO.Path]::GetFullPath((Join-Path $horizonWorkspace 'outputs/last-horizon-neutral-20261031/compile'))
$horizonReferenceRoot = [IO.Path]::GetFullPath($ReferenceResponses)
$horizonReferenceSlash = $horizonReferenceRoot.Replace('\', '/')
$horizonOutputSlash = $horizonCompile.Replace('\', '/')
$horizonAssemblies = @('Core', 'Content', 'Persistence', 'Presentation')
$horizonDotnet = Join-Path $UnityData 'NetCoreRuntime/dotnet.exe'
$horizonCompiler = Join-Path $UnityData 'DotNetSdkRoslyn/csc.dll'
$horizonSourceFiles = @()
$horizonSourceHashes = @()
$horizonReferenceHashes = @()
$horizonResponseHashes = @()
$horizonAssemblyHashes = @()
$horizonBusinessReferences = @()
$horizonResults = [Collections.Generic.List[string]]::new()
$horizonSucceeded = $false
$horizonFailure = ''
$horizonTemporaryNames = @()
foreach ($horizonAssembly in $horizonAssemblies) {
    $horizonTemporaryNames += $horizonAssembly + '.rsp'
    foreach ($horizonSuffix in @('.dll', '.ref.dll', '.pdb')) {
        $horizonTemporaryNames += 'ShatteredSail.' + $horizonAssembly + $horizonSuffix
    }
}

function Assert-HorizonCompilePath {
    $horizonExpected = [IO.Path]::GetFullPath((Join-Path $horizonWorkspace 'outputs/last-horizon-neutral-20261031/compile'))
    if ($horizonCompile -ne $horizonExpected) { throw 'Unexpected task compilation directory.' }
    $horizonCursor = $horizonCompile
    while ($horizonCursor) {
        if ((Test-Path -LiteralPath $horizonCursor) -and
            (((Get-Item -LiteralPath $horizonCursor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0)) {
            throw ('Refusing linked compilation path: ' + $horizonCursor)
        }
        $horizonCursor = Split-Path -Path $horizonCursor -Parent
    }
}

function Get-HorizonCompileConsumers {
    @(Get-CimInstance Win32_Process -Filter "Name='dotnet.exe' OR Name='mono.exe' OR Name='Unity.exe'" |
        Where-Object { $_.CommandLine -and
            $_.CommandLine.Replace('/', '\').IndexOf($horizonCompile.Replace('/', '\'), [StringComparison]::OrdinalIgnoreCase) -ge 0 })
}

Assert-HorizonCompilePath
foreach ($horizonRequired in @($horizonDotnet, $horizonCompiler)) {
    if (-not (Test-Path -LiteralPath $horizonRequired -PathType Leaf)) {
        throw ('Missing existing compiler dependency: ' + $horizonRequired)
    }
}
if (@(Get-HorizonCompileConsumers).Count -gt 0) { throw 'Task compilation output is already in use.' }
New-Item -ItemType Directory -Path $horizonCompile -Force | Out-Null
Assert-HorizonCompilePath

try {
    foreach ($horizonAssembly in $horizonAssemblies) {
        $horizonTemplate = Join-Path $horizonReferenceRoot ($horizonAssembly + '.rsp')
        if (-not (Test-Path -LiteralPath $horizonTemplate -PathType Leaf)) {
            throw ('Missing read-only response template: ' + $horizonTemplate)
        }
        $horizonReferenceHashes += [pscustomobject]@{
            Source = $horizonTemplate
            SHA256 = (Get-FileHash -LiteralPath $horizonTemplate -Algorithm SHA256).Hash
        }
        $horizonResponse = @([IO.File]::ReadAllLines($horizonTemplate) |
            ForEach-Object { $_.Replace($horizonReferenceSlash, $horizonOutputSlash) })
        $horizonSourcePattern = '^"?(?:' + [Regex]::Escape($horizonProject.Replace('\', '/') + '/') +
            ')?Assets/_Project/' + $horizonAssembly + '/Runtime/.*\.cs"?$'
        $horizonResponse = @($horizonResponse | Where-Object { $_ -notmatch $horizonSourcePattern -and $_ -notmatch '^[-/]noconfig$' })
        $horizonSources = @(Get-ChildItem -LiteralPath (Join-Path $horizonProject ('Assets/_Project/' + $horizonAssembly + '/Runtime')) -File -Filter '*.cs' -Recurse |
            Sort-Object FullName)
        if ($horizonSources.Count -eq 0) { throw ('No current source files for ' + $horizonAssembly) }
        $horizonSourceFiles += $horizonSources
        $horizonResponse += @($horizonSources | ForEach-Object { '"' + $_.FullName.Replace('\', '/') + '"' })
        if ($horizonResponse | Where-Object { $_.Contains($horizonReferenceSlash) }) {
            throw 'A generated response still references the shared compiler output directory.'
        }
        $horizonExpectedReferences = switch ($horizonAssembly) {
            'Core' { @() }
            'Content' { @('ShatteredSail.Core.ref.dll') }
            'Persistence' { @('ShatteredSail.Core.ref.dll') }
            'Presentation' { @('ShatteredSail.Core.ref.dll', 'ShatteredSail.Content.ref.dll', 'ShatteredSail.Persistence.ref.dll') }
        }
        $horizonSeenReferences = @()
        foreach ($horizonReferenceOption in @($horizonResponse | Where-Object { $_ -match '^[-/](r|reference):.*ShatteredSail\.' })) {
            $horizonReferenceValue = ($horizonReferenceOption -replace '^[-/][^:]+:', '').Trim('"')
            $horizonResolvedReference = [IO.Path]::GetFullPath($horizonReferenceValue)
            $horizonReferenceName = [IO.Path]::GetFileName($horizonResolvedReference)
            if ([IO.Path]::GetDirectoryName($horizonResolvedReference) -ne $horizonCompile -or
                $horizonReferenceName -notin $horizonExpectedReferences) {
                throw ('Business assembly reference must point to this compilation: ' + $horizonReferenceOption)
            }
            $horizonSeenReferences += $horizonReferenceName
            $horizonBusinessReferences += [pscustomobject]@{ Assembly = $horizonAssembly; Reference = $horizonResolvedReference }
        }
        foreach ($horizonExpectedReference in $horizonExpectedReferences) {
            if ($horizonExpectedReference -notin $horizonSeenReferences) { throw ('Missing current business dependency: ' + $horizonExpectedReference) }
        }
        $horizonOutputOptions = @($horizonResponse | Where-Object { $_ -match '^[-/](out|refout|pdb|doc|errorlog|generatedfilesout):' })
        if (@($horizonOutputOptions | Where-Object { $_ -match '^[-/]out:' }).Count -ne 1) {
            throw ('Expected one explicit compiler output for ' + $horizonAssembly)
        }
        foreach ($horizonOutputOption in $horizonOutputOptions) {
            $horizonOptionValue = ($horizonOutputOption -replace '^[-/][^:]+:', '').Trim('"')
            $horizonResolvedOutput = [IO.Path]::GetFullPath($horizonOptionValue)
            if ([IO.Path]::GetDirectoryName($horizonResolvedOutput) -ne $horizonCompile -or
                [IO.Path]::GetFileName($horizonResolvedOutput) -notin $horizonTemporaryNames) {
                throw ('Refusing compiler output outside the known task artifact list: ' + $horizonOutputOption)
            }
        }
        $horizonResponsePath = Join-Path $horizonCompile ($horizonAssembly + '.rsp')
        [IO.File]::WriteAllLines($horizonResponsePath, $horizonResponse, [Text.UTF8Encoding]::new($false))
        $horizonResponseHashes += [pscustomobject]@{
            Assembly = $horizonAssembly
            SourceCount = $horizonSources.Count
            SHA256 = (Get-FileHash -LiteralPath $horizonResponsePath -Algorithm SHA256).Hash
        }
    }

    $horizonSourceHashes = @($horizonSourceFiles | ForEach-Object {
        [pscustomobject]@{ Source = $_.FullName; SHA256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
    })
    $horizonSourceHashes | Export-Csv -LiteralPath (Join-Path $horizonCompile 'source-hashes.csv') -NoTypeInformation -Encoding utf8
    $horizonReferenceHashes | Export-Csv -LiteralPath (Join-Path $horizonCompile 'reference-response-hashes.csv') -NoTypeInformation -Encoding utf8
    $horizonResponseHashes | Export-Csv -LiteralPath (Join-Path $horizonCompile 'response-hashes.csv') -NoTypeInformation -Encoding utf8
    $horizonBusinessReferences | Export-Csv -LiteralPath (Join-Path $horizonCompile 'business-references.csv') -NoTypeInformation -Encoding utf8

    Push-Location $horizonProject
    try {
        foreach ($horizonAssembly in $horizonAssemblies) {
            $horizonResponsePath = Join-Path $horizonCompile ($horizonAssembly + '.rsp')
            $horizonLog = Join-Path $horizonCompile ($horizonAssembly + '.log')
            ('Compiling current production assembly: ' + $horizonAssembly) | Set-Content -LiteralPath $horizonLog -Encoding utf8
            & $horizonDotnet $horizonCompiler '-noconfig' ('@' + $horizonResponsePath) 2>&1 | Tee-Object -FilePath $horizonLog -Append
            if ($LASTEXITCODE -ne 0) { throw ('Production compilation failed: ' + $horizonAssembly) }
            $horizonResults.Add('PASS ' + $horizonAssembly)
            ('PASS ' + $horizonAssembly) | Add-Content -LiteralPath $horizonLog -Encoding utf8
            Write-Output ('PASS ' + $horizonAssembly)
            foreach ($horizonSuffix in @('.dll', '.ref.dll', '.pdb')) {
                $horizonArtifact = Join-Path $horizonCompile ('ShatteredSail.' + $horizonAssembly + $horizonSuffix)
                if (Test-Path -LiteralPath $horizonArtifact -PathType Leaf) {
                    $horizonAssemblyHashes += [pscustomobject]@{
                        Artifact = [IO.Path]::GetFileName($horizonArtifact)
                        Bytes = (Get-Item -LiteralPath $horizonArtifact).Length
                        SHA256 = (Get-FileHash -LiteralPath $horizonArtifact -Algorithm SHA256).Hash
                    }
                }
            }
        }
    } finally { Pop-Location }

    foreach ($horizonInputHash in @($horizonSourceHashes) + @($horizonReferenceHashes)) {
        if ((Get-FileHash -LiteralPath $horizonInputHash.Source -Algorithm SHA256).Hash -ne $horizonInputHash.SHA256) {
            throw ('Compilation input changed during verification: ' + $horizonInputHash.Source)
        }
    }
    $horizonResults.Add('PASS current source and read-only response template hashes remain stable')
    $horizonResults.Add('Compiled all current Core, Content, Persistence and Presentation Runtime sources using existing Unity dependencies. No Unity Editor or copied project was used.')
    $horizonSucceeded = $true
} catch {
    $horizonFailure = $_.Exception.Message
    $horizonResults.Add('FAIL ' + $horizonFailure)
    throw
} finally {
    Assert-HorizonCompilePath
    [IO.File]::WriteAllLines((Join-Path $horizonCompile 'validation.txt'), $horizonResults, [Text.UTF8Encoding]::new($false))
    if ($horizonAssemblyHashes.Count -gt 0) {
        $horizonAssemblyHashes | Export-Csv -LiteralPath (Join-Path $horizonCompile 'assembly-hashes.csv') -NoTypeInformation -Encoding utf8
    }
    $horizonConsumers = @(Get-HorizonCompileConsumers)
    $horizonReleased = 0L
    $horizonRemoved = [Collections.Generic.List[string]]::new()
    if ($horizonConsumers.Count -eq 0) {
        foreach ($horizonTemporaryName in $horizonTemporaryNames) {
            $horizonTemporaryPath = [IO.Path]::GetFullPath((Join-Path $horizonCompile $horizonTemporaryName))
            if ([IO.Path]::GetDirectoryName($horizonTemporaryPath) -ne $horizonCompile) { throw 'Unexpected cleanup path.' }
            if (-not (Test-Path -LiteralPath $horizonTemporaryPath)) { continue }
            $horizonTemporaryItem = Get-Item -LiteralPath $horizonTemporaryPath -Force
            if ($horizonTemporaryItem.PSIsContainer -or
                (($horizonTemporaryItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0)) {
                throw ('Refusing linked or non-file compiler artifact: ' + $horizonTemporaryPath)
            }
            $horizonReleased += $horizonTemporaryItem.Length
            Remove-Item -LiteralPath $horizonTemporaryPath -Force
            if (Test-Path -LiteralPath $horizonTemporaryPath) { throw ('Compiler artifact cleanup failed: ' + $horizonTemporaryPath) }
            $horizonRemoved.Add($horizonTemporaryName)
        }
    }
    [pscustomobject]@{
        CompilationPassed = $horizonSucceeded
        Failure = $horizonFailure
        ReleasedBytes = $horizonReleased
        RemovedFiles = @($horizonRemoved)
        CleanupDeferred = $horizonConsumers.Count -gt 0
        RemainingConsumerIds = @($horizonConsumers | ForEach-Object { $_.ProcessId })
        EvidenceRetained = $true
        MainProjectCleaned = $false
        SharedReferenceResponsesRetained = $horizonReferenceRoot
    } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $horizonCompile 'cleanup.json') -Encoding utf8
    if ($horizonConsumers.Count -gt 0) {
        Write-Warning 'A process still references this task output. Compiler artifacts were retained until it exits.'
    } else {
        Write-Output ('Cleaned task DLL/PDB/response artifacts: ' + $horizonReleased + ' bytes; hashes and logs retained.')
    }
}
Write-Output ('PASS production compilation: ' + $horizonCompile)
