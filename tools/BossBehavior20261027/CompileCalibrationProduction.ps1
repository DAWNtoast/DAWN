param(
    [string]$ReferenceResponses = 'D:/Game/Artifacts/CardNumbering20261017/UiCompile',
    [string]$UnityData = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data',
    [switch]$PrepareOnly
)
$ErrorActionPreference = 'Stop'
$battleResourceWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$battleResourceProject = Join-Path $battleResourceWorkspace 'UnityClient'
$battleResourceCompile = [IO.Path]::GetFullPath((Join-Path $battleResourceWorkspace 'outputs/boss-behavior-20261027/calibration-production-compile'))
$battleResourceReferencePath = [IO.Path]::GetFullPath($ReferenceResponses).Replace('\', '/')
$battleResourceOutputPath = $battleResourceCompile.Replace('\', '/')
$battleResourceAssemblies = @('Core', 'Content', 'Persistence', 'Presentation')
$battleResourceSourceFiles = @()

function Assert-BattleResourceCompileDirectory {
    $battleResourceExpected = [IO.Path]::GetFullPath((Join-Path $battleResourceWorkspace 'outputs/boss-behavior-20261027/calibration-production-compile'))
    if ($battleResourceCompile -ne $battleResourceExpected) { throw 'Unexpected production compile directory.' }
    foreach ($battleResourceDirectory in @($battleResourceWorkspace, (Join-Path $battleResourceWorkspace 'outputs'),
        (Join-Path $battleResourceWorkspace 'outputs/boss-behavior-20261027'), $battleResourceCompile)) {
        if ((Test-Path -LiteralPath $battleResourceDirectory) -and
            (((Get-Item -LiteralPath $battleResourceDirectory -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0)) {
            throw ('Refusing linked compile directory: ' + $battleResourceDirectory)
        }
    }
}
Assert-BattleResourceCompileDirectory
New-Item -ItemType Directory -Force -Path $battleResourceCompile | Out-Null
Assert-BattleResourceCompileDirectory

foreach ($battleResourceAssembly in $battleResourceAssemblies) {
    $battleResourceResponse = [IO.File]::ReadAllLines((Join-Path $ReferenceResponses ($battleResourceAssembly + '.rsp'))) |
        ForEach-Object { $_.Replace($battleResourceReferencePath, $battleResourceOutputPath) }
    $battleResourceSourcePattern = '^"?(?:' + [Regex]::Escape($battleResourceProject.Replace('\', '/') + '/') +
        ')?Assets/_Project/' + $battleResourceAssembly + '/Runtime/.*\.cs"?$'
    $battleResourceResponse = @($battleResourceResponse | Where-Object { $_ -notmatch $battleResourceSourcePattern })
    $battleResourceSources = @(Get-ChildItem -LiteralPath (Join-Path $battleResourceProject ('Assets/_Project/' + $battleResourceAssembly + '/Runtime')) -File -Filter '*.cs' -Recurse | Sort-Object FullName)
    if ($battleResourceSources.Count -eq 0) { throw ('No current production sources found for ' + $battleResourceAssembly) }
    $battleResourceSourceFiles += $battleResourceSources
    $battleResourceResponse += @($battleResourceSources | ForEach-Object { '"' + $_.FullName.Replace('\', '/') + '"' })
    if ($battleResourceResponse | Where-Object { $_.Contains($battleResourceReferencePath) }) { throw 'Old compiler output directory remains referenced.' }
    foreach ($battleResourceOutput in @($battleResourceResponse | Where-Object { $_ -match '^-(out|refout|pdb):' })) {
        if (-not $battleResourceOutput.Contains($battleResourceOutputPath + '/')) { throw ('Unexpected compiler output: ' + $battleResourceOutput) }
    }
    [IO.File]::WriteAllLines((Join-Path $battleResourceCompile ($battleResourceAssembly + '.rsp')), $battleResourceResponse, [Text.UTF8Encoding]::new($false))
}

$battleResourceHashes = @($battleResourceSourceFiles | ForEach-Object {
    [pscustomobject]@{ Source = $_.FullName; SHA256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
})
$battleResourceHashes | Export-Csv -LiteralPath (Join-Path $battleResourceCompile 'source-hashes.csv') -NoTypeInformation -Encoding utf8
if ($PrepareOnly) { Write-Output ('Prepared production responses: ' + $battleResourceCompile); return }

$battleResourceResults = [Collections.Generic.List[string]]::new()
try {
    Push-Location $battleResourceProject
    try {
        foreach ($battleResourceAssembly in $battleResourceAssemblies) {
            $battleResourceResponsePath = Join-Path $battleResourceCompile ($battleResourceAssembly + '.rsp')
            $battleResourceLog = Join-Path $battleResourceCompile ($battleResourceAssembly + '.log')
            ('Compiling current production assembly: ' + $battleResourceAssembly) | Set-Content -LiteralPath $battleResourceLog -Encoding utf8
            & (Join-Path $UnityData 'NetCoreRuntime/dotnet.exe') (Join-Path $UnityData 'DotNetSdkRoslyn/csc.dll') ('@' + $battleResourceResponsePath) 2>&1 |
                Tee-Object -FilePath $battleResourceLog -Append
            if ($LASTEXITCODE -ne 0) { throw ('Production compilation failed: ' + $battleResourceAssembly) }
            ('PASS ' + $battleResourceAssembly) | Add-Content -LiteralPath $battleResourceLog -Encoding utf8
            $battleResourceResults.Add('PASS ' + $battleResourceAssembly)
            Write-Output ('PASS ' + $battleResourceAssembly)
        }
    } finally { Pop-Location }

    foreach ($battleResourceHash in $battleResourceHashes) {
        if ((Get-FileHash -LiteralPath $battleResourceHash.Source -Algorithm SHA256).Hash -ne $battleResourceHash.SHA256) {
            throw ('Production source changed during compilation: ' + $battleResourceHash.Source)
        }
    }
    $battleResourceResults.Add('PASS source hashes stable across all four assemblies')
    $battleResourceResults.Add('Used existing Unity reference assemblies read-only. No Editor, isolated Unity project, scene, or preview generator was launched.')
    [IO.File]::WriteAllLines((Join-Path $battleResourceCompile 'validation.txt'), $battleResourceResults, [Text.UTF8Encoding]::new($false))
} finally {
    # Compiler outputs are needed only until all four sequential assemblies finish.
    # Keep response files, source hashes and logs; never clean UnityClient or the
    # shared reference-response directory used by other existing tools.
    Assert-BattleResourceCompileDirectory
    $battleResourceConsumers = @(Get-CimInstance Win32_Process -Filter "Name='mono.exe' OR Name='dotnet.exe'" |
        Where-Object { $_.CommandLine -like ('*' + $battleResourceCompile + '*') -or $_.CommandLine -like ('*' + $battleResourceOutputPath + '*') })
    if ($battleResourceConsumers.Count -gt 0) { throw 'A compiler or test process still references the task outputs; cleanup deferred.' }
    $battleResourceReleased = 0L
    $battleResourceRemoved = [Collections.Generic.List[string]]::new()
    foreach ($battleResourceAssembly in $battleResourceAssemblies) {
        foreach ($battleResourceSuffix in @('.dll', '.pdb', '.ref.dll')) {
            $battleResourceFile = [IO.Path]::GetFullPath((Join-Path $battleResourceCompile ('ShatteredSail.' + $battleResourceAssembly + $battleResourceSuffix)))
            if ([IO.Path]::GetDirectoryName($battleResourceFile) -ne $battleResourceCompile) { throw 'Unexpected cleanup target.' }
            if (Test-Path -LiteralPath $battleResourceFile) {
                $battleResourceItem = Get-Item -LiteralPath $battleResourceFile
                if ($battleResourceItem.PSIsContainer -or ($battleResourceItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                    throw ('Refusing unexpected compiler artifact: ' + $battleResourceFile)
                }
                $battleResourceReleased += $battleResourceItem.Length
                Remove-Item -LiteralPath $battleResourceFile -Force
                if (Test-Path -LiteralPath $battleResourceFile) { throw ('Cleanup failed: ' + $battleResourceFile) }
                $battleResourceRemoved.Add($battleResourceItem.Name)
            }
        }
    }
    [pscustomobject]@{ ReleasedBytes = $battleResourceReleased; RemovedFiles = @($battleResourceRemoved); EvidenceRetained = $true;
        MainProjectCleaned = $false; SharedReferenceAssembliesRetained = $ReferenceResponses } |
        ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $battleResourceCompile 'cleanup.json') -Encoding utf8
    Write-Output ('Cleaned task compiler artifacts: ' + $battleResourceReleased + ' bytes; evidence retained.')
}
Write-Output ('PASS production validation: ' + $battleResourceCompile)
