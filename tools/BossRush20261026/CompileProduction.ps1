param(
    [string]$ReferenceResponses = 'D:/Game/Artifacts/CardNumbering20261017/UiCompile',
    [string]$UnityData = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data',
    [switch]$PrepareOnly
)
$ErrorActionPreference = 'Stop'
$rushWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$rushProject = Join-Path $rushWorkspace 'UnityClient'
$rushCompile = [IO.Path]::GetFullPath((Join-Path $rushWorkspace 'outputs/boss-rush-20261026/production-compile'))
$rushReferencePath = [IO.Path]::GetFullPath($ReferenceResponses).Replace('\', '/')
$rushOutputPath = $rushCompile.Replace('\', '/')
$rushAssemblies = @('Core', 'Content', 'Persistence', 'Presentation')
$rushSourceFiles = @()
New-Item -ItemType Directory -Force -Path $rushCompile | Out-Null

function Assert-RushCompileDirectory {
    $rushExpected = [IO.Path]::GetFullPath((Join-Path $rushWorkspace 'outputs/boss-rush-20261026/production-compile'))
    if ($rushCompile -ne $rushExpected) { throw 'Unexpected production compile directory.' }
    foreach ($rushDirectory in @($rushWorkspace, (Join-Path $rushWorkspace 'outputs'),
        (Join-Path $rushWorkspace 'outputs/boss-rush-20261026'), $rushCompile)) {
        if (((Get-Item -LiteralPath $rushDirectory).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw ('Refusing linked compile directory: ' + $rushDirectory)
        }
    }
}
Assert-RushCompileDirectory

foreach ($rushAssembly in $rushAssemblies) {
    $rushResponse = [IO.File]::ReadAllLines((Join-Path $ReferenceResponses ($rushAssembly + '.rsp'))) |
        ForEach-Object { $_.Replace($rushReferencePath, $rushOutputPath) }
    $rushSourcePattern = '^"?(?:' + [Regex]::Escape($rushProject.Replace('\', '/') + '/') +
        ')?Assets/_Project/' + $rushAssembly + '/Runtime/.*\.cs"?$'
    $rushResponse = @($rushResponse | Where-Object { $_ -notmatch $rushSourcePattern })
    $rushSources = @(Get-ChildItem -LiteralPath (Join-Path $rushProject ('Assets/_Project/' + $rushAssembly + '/Runtime')) -File -Filter '*.cs' -Recurse | Sort-Object FullName)
    if ($rushSources.Count -eq 0) { throw ('No current production sources found for ' + $rushAssembly) }
    $rushSourceFiles += $rushSources
    $rushResponse += @($rushSources | ForEach-Object { '"' + $_.FullName.Replace('\', '/') + '"' })
    if ($rushResponse | Where-Object { $_.Contains($rushReferencePath) }) { throw 'Old compiler output directory remains referenced.' }
    foreach ($rushOutput in @($rushResponse | Where-Object { $_ -match '^-(out|refout|pdb):' })) {
        if (-not $rushOutput.Contains($rushOutputPath + '/')) { throw ('Unexpected compiler output: ' + $rushOutput) }
    }
    [IO.File]::WriteAllLines((Join-Path $rushCompile ($rushAssembly + '.rsp')), $rushResponse, [Text.UTF8Encoding]::new($false))
}

$rushHashes = @($rushSourceFiles | ForEach-Object {
    [pscustomobject]@{ Source = $_.FullName; SHA256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
})
$rushHashes | Export-Csv -LiteralPath (Join-Path $rushCompile 'source-hashes.csv') -NoTypeInformation -Encoding utf8
if ($PrepareOnly) { Write-Output ('Prepared production responses: ' + $rushCompile); return }

$rushResults = [Collections.Generic.List[string]]::new()
try {
    Push-Location $rushProject
    try {
        foreach ($rushAssembly in $rushAssemblies) {
            $rushResponsePath = Join-Path $rushCompile ($rushAssembly + '.rsp')
            $rushLog = Join-Path $rushCompile ($rushAssembly + '.log')
            ('Compiling current production assembly: ' + $rushAssembly) | Set-Content -LiteralPath $rushLog -Encoding utf8
            & (Join-Path $UnityData 'NetCoreRuntime/dotnet.exe') (Join-Path $UnityData 'DotNetSdkRoslyn/csc.dll') ('@' + $rushResponsePath) 2>&1 |
                Tee-Object -FilePath $rushLog -Append
            if ($LASTEXITCODE -ne 0) { throw ('Production compilation failed: ' + $rushAssembly) }
            ('PASS ' + $rushAssembly) | Add-Content -LiteralPath $rushLog -Encoding utf8
            $rushResults.Add('PASS ' + $rushAssembly)
            Write-Output ('PASS ' + $rushAssembly)
        }
    } finally { Pop-Location }

    foreach ($rushHash in $rushHashes) {
        if ((Get-FileHash -LiteralPath $rushHash.Source -Algorithm SHA256).Hash -ne $rushHash.SHA256) {
            throw ('Production source changed during compilation: ' + $rushHash.Source)
        }
    }
    $rushResults.Add('PASS source hashes stable across all four assemblies')
    $rushResults.Add('Used existing Unity reference assemblies read-only. No Editor, isolated Unity project, scene, or preview generator was launched.')
    [IO.File]::WriteAllLines((Join-Path $rushCompile 'validation.txt'), $rushResults, [Text.UTF8Encoding]::new($false))
} finally {
    # Compiler outputs are needed only until all four sequential assemblies finish.
    # Keep response files, source hashes and logs; never clean UnityClient or the
    # shared reference-response directory used by other existing tools.
    Assert-RushCompileDirectory
    $rushConsumers = @(Get-CimInstance Win32_Process -Filter "Name='mono.exe' OR Name='dotnet.exe'" |
        Where-Object { $_.CommandLine -like ('*' + $rushCompile + '*') -or $_.CommandLine -like ('*' + $rushOutputPath + '*') })
    if ($rushConsumers.Count -gt 0) { throw 'A compiler or test process still references the task outputs; cleanup deferred.' }
    $rushReleased = 0L
    $rushRemoved = [Collections.Generic.List[string]]::new()
    foreach ($rushAssembly in $rushAssemblies) {
        foreach ($rushSuffix in @('.dll', '.pdb', '.ref.dll')) {
            $rushFile = [IO.Path]::GetFullPath((Join-Path $rushCompile ('ShatteredSail.' + $rushAssembly + $rushSuffix)))
            if ([IO.Path]::GetDirectoryName($rushFile) -ne $rushCompile) { throw 'Unexpected cleanup target.' }
            if (Test-Path -LiteralPath $rushFile) {
                $rushItem = Get-Item -LiteralPath $rushFile
                if ($rushItem.PSIsContainer -or ($rushItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                    throw ('Refusing unexpected compiler artifact: ' + $rushFile)
                }
                $rushReleased += $rushItem.Length
                Remove-Item -LiteralPath $rushFile -Force
                if (Test-Path -LiteralPath $rushFile) { throw ('Cleanup failed: ' + $rushFile) }
                $rushRemoved.Add($rushItem.Name)
            }
        }
    }
    [pscustomobject]@{ ReleasedBytes = $rushReleased; RemovedFiles = @($rushRemoved); EvidenceRetained = $true;
        MainProjectCleaned = $false; SharedReferenceAssembliesRetained = $ReferenceResponses } |
        ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $rushCompile 'cleanup.json') -Encoding utf8
    Write-Output ('Cleaned task compiler artifacts: ' + $rushReleased + ' bytes; evidence retained.')
}
Write-Output ('PASS production validation: ' + $rushCompile)
