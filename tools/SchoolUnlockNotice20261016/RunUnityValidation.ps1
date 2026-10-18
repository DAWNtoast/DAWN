param(
    [string]$ProjectPath = 'D:/Game/tmp/escape-navigation-20261014',
    [string]$Output = 'D:/Game/Artifacts/SchoolUnlockNotice20261016/UnityValidation',
    [string]$UnityEditor = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Unity.exe',
    [string]$Filter = 'ShatteredSail.Presentation.Tests.MainMenuSceneTests.SchoolUnlockNotice_',
    [switch]$PrepareOnly
)
$ErrorActionPreference = 'Stop'
$unlockWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$unlockProject = [IO.Path]::GetFullPath($ProjectPath).TrimEnd('\','/')
$unlockTemporaryRoot = [IO.Path]::GetFullPath((Join-Path $unlockWorkspace 'tmp')).TrimEnd('\','/') + [IO.Path]::DirectorySeparatorChar
$unlockOutput = [IO.Path]::GetFullPath($Output)
$unlockArtifactRoot = [IO.Path]::GetFullPath((Join-Path $unlockWorkspace 'Artifacts')).TrimEnd('\','/') + [IO.Path]::DirectorySeparatorChar
if (-not $unlockProject.StartsWith($unlockTemporaryRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Use an isolated cached Unity project inside the workspace tmp directory.'
}
if (-not $unlockOutput.StartsWith($unlockArtifactRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Validation output must stay inside the workspace Artifacts directory.'
}
if (-not (Test-Path -LiteralPath (Join-Path $unlockProject 'Library')) -or
    -not (Test-Path -LiteralPath (Join-Path $unlockProject 'ProjectSettings/ProjectVersion.txt'))) {
    throw 'An existing isolated project and asset cache are required.'
}
if (Test-Path -LiteralPath (Join-Path $unlockProject 'Temp/UnityLockfile')) {
    throw 'The chosen isolated project is in use; no files were copied.'
}
foreach ($unlockRunning in Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'") {
    if ($unlockRunning.CommandLine -and $unlockRunning.CommandLine.Replace('\','/').Contains($unlockProject.Replace('\','/'))) {
        throw 'The chosen isolated project has a running editor; no files were copied.'
    }
}
[IO.Directory]::CreateDirectory($unlockOutput) | Out-Null
$unlockHashes = @()
$unlockPruned = @()
# Copy source only; no scene generation, no preview synchronizer, and no retired preview directory.
foreach ($unlockFolder in @('Core/Runtime','Content/Runtime','Persistence/Runtime','Presentation/Runtime','Editor','Tests/EditMode','Tests/PlayMode')) {
    $unlockSourceRoot = Join-Path $unlockWorkspace ('UnityClient/Assets/_Project/' + $unlockFolder)
    $unlockDestinationRoot = [IO.Path]::GetFullPath((Join-Path $unlockProject ('Assets/_Project/' + $unlockFolder)))
    if (-not $unlockDestinationRoot.StartsWith($unlockTemporaryRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'The isolated source directory escaped the allowed temporary root.'
    }
    if (Test-Path -LiteralPath $unlockDestinationRoot) {
        foreach ($unlockOld in Get-ChildItem -LiteralPath $unlockDestinationRoot -File -Recurse |
            Where-Object { $_.Name -match '\.(cs|asmdef)(\.meta)?$' }) {
            $unlockOldPath = [IO.Path]::GetFullPath($unlockOld.FullName)
            if (-not $unlockOldPath.StartsWith($unlockDestinationRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
                throw 'A stale source path escaped the isolated directory.'
            }
            $unlockRelative = $unlockOldPath.Substring($unlockDestinationRoot.Length).TrimStart('\','/')
            if (-not (Test-Path -LiteralPath (Join-Path $unlockSourceRoot $unlockRelative))) {
                $unlockPruned += $unlockOldPath
                Remove-Item -LiteralPath $unlockOldPath -Force
            }
        }
    }
    foreach ($unlockSource in Get-ChildItem -LiteralPath $unlockSourceRoot -File -Recurse |
        Where-Object { $_.Name -match '\.(cs|asmdef)(\.meta)?$' }) {
        $unlockRelative = $unlockSource.FullName.Substring($unlockSourceRoot.Length).TrimStart('\','/')
        $unlockDestination = Join-Path $unlockDestinationRoot $unlockRelative
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($unlockDestination)) | Out-Null
        Copy-Item -LiteralPath $unlockSource.FullName -Destination $unlockDestination -Force
        $unlockHash = (Get-FileHash -LiteralPath $unlockSource.FullName -Algorithm SHA256).Hash
        if ((Get-FileHash -LiteralPath $unlockDestination -Algorithm SHA256).Hash -ne $unlockHash) { throw 'Copied source hash mismatch.' }
        $unlockHashes += [pscustomobject]@{Source=$unlockSource.FullName; Destination=$unlockDestination; SHA256=$unlockHash}
    }
}
$unlockHashes | Export-Csv -LiteralPath (Join-Path $unlockOutput 'source-hashes.csv') -NoTypeInformation -Encoding utf8
$unlockPruned | Set-Content -LiteralPath (Join-Path $unlockOutput 'pruned-cache-code.txt') -Encoding utf8
# Refresh runtime assets only. This deliberately excludes Scenes and EditablePreviewAssets.
foreach ($unlockAssetFolder in @('Resources','Presentation/Prefabs')) {
    $unlockAssetSource = Join-Path $unlockWorkspace ('UnityClient/Assets/_Project/' + $unlockAssetFolder)
    $unlockAssetDestination = Join-Path $unlockProject ('Assets/_Project/' + $unlockAssetFolder)
    if (Test-Path -LiteralPath $unlockAssetSource) {
        & robocopy $unlockAssetSource $unlockAssetDestination /E /R:1 /W:1 /NFL /NDL /NJH /NJS /NP | Out-Null
        if ($LASTEXITCODE -ge 8) { throw ('Runtime asset copy failed: ' + $unlockAssetFolder) }
    }
}
$unlockSettingsPath = Join-Path $unlockProject 'ProjectSettings/ProjectSettings.asset'
$unlockSettings = [IO.File]::ReadAllText($unlockSettingsPath)
$unlockSettings = [regex]::Replace($unlockSettings, '(?m)^  companyName:.*$', '  companyName: ShatteredSailValidation')
$unlockSettings = [regex]::Replace($unlockSettings, '(?m)^  productName:.*$', '  productName: SchoolUnlockNotice20261016')
[IO.File]::WriteAllText($unlockSettingsPath, $unlockSettings, [Text.UTF8Encoding]::new($false))
if ($PrepareOnly) { Write-Output ('Prepared isolated source and runtime assets: ' + $unlockProject); exit 0 }
$unlockCapture = Join-Path $unlockOutput 'screenshots'
if (Test-Path -LiteralPath $unlockCapture) { throw 'Choose a fresh Output directory so captures cannot be overwritten.' }
$unlockArguments = @('-batchmode','-projectPath',('"'+$unlockProject+'"'),'-runTests','-testPlatform','PlayMode',
    '-testFilter',('"'+$Filter+'"'),'-testResults',('"'+(Join-Path $unlockOutput 'results.xml')+'"'),
    '-logFile',('"'+(Join-Path $unlockOutput 'editor.log')+'"'),'-navalVisualCapturePath',('"'+$unlockCapture+'"'))
$unlockProcess = Start-Process -FilePath $UnityEditor -ArgumentList $unlockArguments -WindowStyle Hidden -PassThru
Write-Output ('Isolated school-unlock validation started, PID ' + $unlockProcess.Id)
$unlockProcess.WaitForExit()
if ($unlockProcess.ExitCode -ne 0) { throw ('Unity validation exited with ' + $unlockProcess.ExitCode + '; inspect editor.log and results.xml.') }
foreach ($unlockEntry in $unlockHashes) {
    if ((Get-FileHash -LiteralPath $unlockEntry.Source -Algorithm SHA256).Hash -ne $unlockEntry.SHA256) {
        throw ('Source changed during validation: ' + $unlockEntry.Source)
    }
}
[xml]$unlockResults = Get-Content -LiteralPath (Join-Path $unlockOutput 'results.xml') -Raw
Write-Output ('Unity NUnit: passed=' + $unlockResults.'test-run'.passed + ' failed=' + $unlockResults.'test-run'.failed + ' skipped=' + $unlockResults.'test-run'.skipped)
if ([int]$unlockResults.'test-run'.passed -eq 0 -or [int]$unlockResults.'test-run'.failed -ne 0) { throw 'School-unlock validation did not pass.' }
