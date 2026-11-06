param(
    [string]$ReferenceResponses = 'D:/Game/Artifacts/CardNumbering20261017/UiCompile',
    [string]$UnityData = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data',
    [switch]$PrepareOnly
)
$ErrorActionPreference = 'Stop'
$bossFieldWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$bossFieldProject = Join-Path $bossFieldWorkspace 'UnityClient'
$bossFieldCompile = [IO.Path]::GetFullPath((Join-Path $bossFieldWorkspace 'outputs/boss-field-behavior-20261026/production-compile'))
$bossFieldReferencePath = [IO.Path]::GetFullPath($ReferenceResponses).Replace('\', '/')
$bossFieldOutputPath = $bossFieldCompile.Replace('\', '/')
$bossFieldAssemblies = @('Core', 'Content', 'Persistence', 'Presentation')
$bossFieldSourceFiles = @()

function Assert-BossFieldCompileDirectory {
    $bossFieldExpected = [IO.Path]::GetFullPath((Join-Path $bossFieldWorkspace 'outputs/boss-field-behavior-20261026/production-compile'))
    if ($bossFieldCompile -ne $bossFieldExpected) { throw 'Unexpected production compile directory.' }
    foreach ($bossFieldDirectory in @($bossFieldWorkspace, (Join-Path $bossFieldWorkspace 'outputs'),
        (Join-Path $bossFieldWorkspace 'outputs/boss-field-behavior-20261026'), $bossFieldCompile)) {
        if ((Test-Path -LiteralPath $bossFieldDirectory) -and
            (((Get-Item -LiteralPath $bossFieldDirectory -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0)) {
            throw ('Refusing linked compile directory: ' + $bossFieldDirectory)
        }
    }
}
Assert-BossFieldCompileDirectory
New-Item -ItemType Directory -Force -Path $bossFieldCompile | Out-Null
Assert-BossFieldCompileDirectory

foreach ($bossFieldAssembly in $bossFieldAssemblies) {
    $bossFieldResponse = [IO.File]::ReadAllLines((Join-Path $ReferenceResponses ($bossFieldAssembly + '.rsp'))) |
        ForEach-Object { $_.Replace($bossFieldReferencePath, $bossFieldOutputPath) }
    $bossFieldSourcePattern = '^"?(?:' + [Regex]::Escape($bossFieldProject.Replace('\', '/') + '/') +
        ')?Assets/_Project/' + $bossFieldAssembly + '/Runtime/.*\.cs"?$'
    $bossFieldResponse = @($bossFieldResponse | Where-Object { $_ -notmatch $bossFieldSourcePattern })
    $bossFieldSources = @(Get-ChildItem -LiteralPath (Join-Path $bossFieldProject ('Assets/_Project/' + $bossFieldAssembly + '/Runtime')) -File -Filter '*.cs' -Recurse | Sort-Object FullName)
    if ($bossFieldSources.Count -eq 0) { throw ('No current production sources found for ' + $bossFieldAssembly) }
    $bossFieldSourceFiles += $bossFieldSources
    $bossFieldResponse += @($bossFieldSources | ForEach-Object { '"' + $_.FullName.Replace('\', '/') + '"' })
    if ($bossFieldResponse | Where-Object { $_.Contains($bossFieldReferencePath) }) { throw 'Old compiler output directory remains referenced.' }
    foreach ($bossFieldOutput in @($bossFieldResponse | Where-Object { $_ -match '^-(out|refout|pdb):' })) {
        if (-not $bossFieldOutput.Contains($bossFieldOutputPath + '/')) { throw ('Unexpected compiler output: ' + $bossFieldOutput) }
    }
    [IO.File]::WriteAllLines((Join-Path $bossFieldCompile ($bossFieldAssembly + '.rsp')), $bossFieldResponse, [Text.UTF8Encoding]::new($false))
}

$bossFieldHashes = @($bossFieldSourceFiles | ForEach-Object {
    [pscustomobject]@{ Source = $_.FullName; SHA256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
})
$bossFieldHashes | Export-Csv -LiteralPath (Join-Path $bossFieldCompile 'source-hashes.csv') -NoTypeInformation -Encoding utf8
if ($PrepareOnly) { Write-Output ('Prepared production responses: ' + $bossFieldCompile); return }

$bossFieldResults = [Collections.Generic.List[string]]::new()
try {
    Push-Location $bossFieldProject
    try {
        foreach ($bossFieldAssembly in $bossFieldAssemblies) {
            $bossFieldResponsePath = Join-Path $bossFieldCompile ($bossFieldAssembly + '.rsp')
            $bossFieldLog = Join-Path $bossFieldCompile ($bossFieldAssembly + '.log')
            ('Compiling current production assembly: ' + $bossFieldAssembly) | Set-Content -LiteralPath $bossFieldLog -Encoding utf8
            & (Join-Path $UnityData 'NetCoreRuntime/dotnet.exe') (Join-Path $UnityData 'DotNetSdkRoslyn/csc.dll') ('@' + $bossFieldResponsePath) 2>&1 |
                Tee-Object -FilePath $bossFieldLog -Append
            if ($LASTEXITCODE -ne 0) { throw ('Production compilation failed: ' + $bossFieldAssembly) }
            ('PASS ' + $bossFieldAssembly) | Add-Content -LiteralPath $bossFieldLog -Encoding utf8
            $bossFieldResults.Add('PASS ' + $bossFieldAssembly)
            Write-Output ('PASS ' + $bossFieldAssembly)
        }
    } finally { Pop-Location }

    foreach ($bossFieldHash in $bossFieldHashes) {
        if ((Get-FileHash -LiteralPath $bossFieldHash.Source -Algorithm SHA256).Hash -ne $bossFieldHash.SHA256) {
            throw ('Production source changed during compilation: ' + $bossFieldHash.Source)
        }
    }
    $bossFieldResults.Add('PASS source hashes stable across all four assemblies')
    $bossFieldResults.Add('Used existing Unity reference assemblies read-only. No Editor, isolated Unity project, scene, or preview generator was launched.')
    [IO.File]::WriteAllLines((Join-Path $bossFieldCompile 'validation.txt'), $bossFieldResults, [Text.UTF8Encoding]::new($false))
} finally {
    # Compiler outputs are needed only until all four sequential assemblies finish.
    # Keep response files, source hashes and logs; never clean UnityClient or the
    # shared reference-response directory used by other existing tools.
    Assert-BossFieldCompileDirectory
    $bossFieldConsumers = @(Get-CimInstance Win32_Process -Filter "Name='mono.exe' OR Name='dotnet.exe'" |
        Where-Object { $_.CommandLine -like ('*' + $bossFieldCompile + '*') -or $_.CommandLine -like ('*' + $bossFieldOutputPath + '*') })
    if ($bossFieldConsumers.Count -gt 0) { throw 'A compiler or test process still references the task outputs; cleanup deferred.' }
    $bossFieldReleased = 0L
    $bossFieldRemoved = [Collections.Generic.List[string]]::new()
    foreach ($bossFieldAssembly in $bossFieldAssemblies) {
        foreach ($bossFieldSuffix in @('.dll', '.pdb', '.ref.dll')) {
            $bossFieldFile = [IO.Path]::GetFullPath((Join-Path $bossFieldCompile ('ShatteredSail.' + $bossFieldAssembly + $bossFieldSuffix)))
            if ([IO.Path]::GetDirectoryName($bossFieldFile) -ne $bossFieldCompile) { throw 'Unexpected cleanup target.' }
            if (Test-Path -LiteralPath $bossFieldFile) {
                $bossFieldItem = Get-Item -LiteralPath $bossFieldFile
                if ($bossFieldItem.PSIsContainer -or ($bossFieldItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                    throw ('Refusing unexpected compiler artifact: ' + $bossFieldFile)
                }
                $bossFieldReleased += $bossFieldItem.Length
                Remove-Item -LiteralPath $bossFieldFile -Force
                if (Test-Path -LiteralPath $bossFieldFile) { throw ('Cleanup failed: ' + $bossFieldFile) }
                $bossFieldRemoved.Add($bossFieldItem.Name)
            }
        }
    }
    [pscustomobject]@{ ReleasedBytes = $bossFieldReleased; RemovedFiles = @($bossFieldRemoved); EvidenceRetained = $true;
        MainProjectCleaned = $false; SharedReferenceAssembliesRetained = $ReferenceResponses } |
        ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $bossFieldCompile 'cleanup.json') -Encoding utf8
    Write-Output ('Cleaned task compiler artifacts: ' + $bossFieldReleased + ' bytes; evidence retained.')
}
Write-Output ('PASS production validation: ' + $bossFieldCompile)
