param(
    [string]$ProjectPath = 'D:/Game/tmp/第七港口v2-build-project',
    [string]$Output = 'D:/Game/Artifacts/BoardingEnemyPureSchools20261015/UnityValidation',
    [string]$UnityEditor = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Unity.exe',
    [string]$Filter = 'ShatteredSail.Presentation.Tests.BoardingEnemyPresentationTests;ShatteredSail.Presentation.Tests.BattleHudLayoutTests;ShatteredSail.Core.Tests.BoardingComponentDropSelectionTests'
)
$ErrorActionPreference = 'Stop'
$enemyWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$enemyProject = [IO.Path]::GetFullPath($ProjectPath).TrimEnd('\','/')
$enemyTemporaryRoot = [IO.Path]::GetFullPath((Join-Path $enemyWorkspace 'tmp')).TrimEnd('\','/') + [IO.Path]::DirectorySeparatorChar
if (-not $enemyProject.StartsWith($enemyTemporaryRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Unity validation requires an existing isolated project under the workspace tmp directory.'
}
if (-not (Test-Path -LiteralPath (Join-Path $enemyProject 'ProjectSettings/ProjectVersion.txt')) -or
    -not (Test-Path -LiteralPath (Join-Path $enemyProject 'Library'))) {
    throw 'The isolated Unity project and its existing asset cache are required.'
}
if (Test-Path -LiteralPath (Join-Path $enemyProject 'Temp/UnityLockfile')) {
    throw 'The isolated project is in use; no source files were copied.'
}
[IO.Directory]::CreateDirectory($Output) | Out-Null
$enemyHashes = @()
$enemyPruned = @()
# Synchronize source code and the exact data/shared-paper dependencies below.
# Never copy or generate preview scenes, including retired personal previews.
foreach ($enemyFolder in @('Core/Runtime','Content/Runtime','Persistence/Runtime','Presentation/Runtime','Editor','Tests/EditMode','Tests/PlayMode')) {
    $enemySourceRoot = Join-Path $enemyWorkspace ('UnityClient/Assets/_Project/' + $enemyFolder)
    $enemyDestinationRoot = [IO.Path]::GetFullPath((Join-Path $enemyProject ('Assets/_Project/' + $enemyFolder)))
    if (-not $enemyDestinationRoot.StartsWith($enemyTemporaryRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'The isolated source directory escaped the workspace tmp directory.'
    }
    # A reused cache can contain deleted scripts from older source snapshots.
    # Prune only those code files; scene and asset contents are never synchronized.
    if (Test-Path -LiteralPath $enemyDestinationRoot) {
        foreach ($enemyOld in Get-ChildItem -LiteralPath $enemyDestinationRoot -File -Recurse |
            Where-Object { $_.Name.EndsWith('.cs') -or $_.Name.EndsWith('.cs.meta') -or $_.Name.EndsWith('.asmdef') -or $_.Name.EndsWith('.asmdef.meta') }) {
            $enemyOldPath = [IO.Path]::GetFullPath($enemyOld.FullName)
            if (-not $enemyOldPath.StartsWith($enemyDestinationRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
                throw 'A stale code file escaped its isolated source directory.'
            }
            $enemyOldRelative = $enemyOldPath.Substring($enemyDestinationRoot.Length).TrimStart('\','/')
            if (-not (Test-Path -LiteralPath (Join-Path $enemySourceRoot $enemyOldRelative))) {
                $enemyPruned += $enemyOldPath
                Remove-Item -LiteralPath $enemyOldPath -Force
            }
        }
    }
    foreach ($enemySource in Get-ChildItem -LiteralPath $enemySourceRoot -File -Recurse |
        Where-Object { $_.Name.EndsWith('.cs') -or $_.Name.EndsWith('.cs.meta') -or $_.Name.EndsWith('.asmdef') -or $_.Name.EndsWith('.asmdef.meta') }) {
        $enemyRelative = $enemySource.FullName.Substring($enemySourceRoot.Length).TrimStart('\','/')
        $enemyDestination = Join-Path $enemyProject ('Assets/_Project/' + $enemyFolder + '/' + $enemyRelative)
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($enemyDestination)) | Out-Null
        Copy-Item -LiteralPath $enemySource.FullName -Destination $enemyDestination -Force
        $enemyHash = (Get-FileHash -LiteralPath $enemySource.FullName -Algorithm SHA256).Hash
        if ((Get-FileHash -LiteralPath $enemyDestination -Algorithm SHA256).Hash -ne $enemyHash) { throw 'Source snapshot mismatch.' }
        $enemyHashes += [pscustomobject]@{Source=$enemySource.FullName; Destination=$enemyDestination; SHA256=$enemyHash}
    }
}
foreach ($enemyDataFile in @('Resources/Data/component_catalog_v2.csv','Resources/Data/component_catalog_v2.csv.meta',
    'Resources/PortFacilities/Market/market_shop_panel_v3.png','Resources/PortFacilities/Market/market_shop_panel_v3.png.meta')) {
    $enemyDataSource = Join-Path $enemyWorkspace ('UnityClient/Assets/_Project/' + $enemyDataFile)
    $enemyDataDestination = Join-Path $enemyProject ('Assets/_Project/' + $enemyDataFile)
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($enemyDataDestination)) | Out-Null
    Copy-Item -LiteralPath $enemyDataSource -Destination $enemyDataDestination -Force
    $enemyDataHash = (Get-FileHash -LiteralPath $enemyDataSource -Algorithm SHA256).Hash
    if ((Get-FileHash -LiteralPath $enemyDataDestination -Algorithm SHA256).Hash -ne $enemyDataHash) { throw 'Validation dependency snapshot mismatch.' }
    $enemyHashes += [pscustomobject]@{Source=$enemyDataSource; Destination=$enemyDataDestination; SHA256=$enemyDataHash}
}
$enemyHashes | Export-Csv -LiteralPath (Join-Path $Output 'source-hashes.csv') -NoTypeInformation -Encoding utf8
$enemyPruned | Set-Content -LiteralPath (Join-Path $Output 'pruned-cache-code.txt') -Encoding utf8
$enemyArguments = @('-batchmode','-nographics','-projectPath',('"'+$enemyProject+'"'),'-runTests','-testPlatform','EditMode',
    '-testFilter',('"'+$Filter+'"'),'-testResults',('"'+(Join-Path $Output 'results.xml')+'"'),'-logFile',('"'+(Join-Path $Output 'editor.log')+'"'))
$enemyProcess = Start-Process -FilePath $UnityEditor -ArgumentList $enemyArguments -WindowStyle Hidden -PassThru
Write-Output ('Isolated Unity verification started, PID ' + $enemyProcess.Id)
$enemyProcess.WaitForExit()
if ($enemyProcess.ExitCode -ne 0) { throw ('Unity verification exited with ' + $enemyProcess.ExitCode + '; inspect editor.log and results.xml.') }
[xml]$enemyResults = Get-Content -LiteralPath (Join-Path $Output 'results.xml') -Raw
Write-Output ('Unity NUnit: passed=' + $enemyResults.'test-run'.passed + ' failed=' + $enemyResults.'test-run'.failed + ' skipped=' + $enemyResults.'test-run'.skipped)
if ([int]$enemyResults.'test-run'.failed -ne 0) { throw 'Unity verification has failed tests.' }
