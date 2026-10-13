param(
    [ValidateSet('Prepare','Sync','EditMode','PlayMode','CaptureMenus','Build','Smoke')][string]$Stage,
    [string]$Suffix = '',
    [string]$TestFilter = '',
    [string[]]$ExtraArguments = @(),
    [string]$ProjectPath = 'D:\Game\tmp\latest-package-review-20261011',
    [string]$OutputPath = 'D:\Game\Artifacts\LatestPackageReview20261011',
    [string]$BuildPath = 'D:\Game\Builds\第七港口-代码复核优化20261011\Windows\第七港口.exe'
)
$ErrorActionPreference = 'Stop'
$auditSource = 'D:\Game\UnityClient'
$auditProject = [IO.Path]::GetFullPath($ProjectPath)
$auditOutput = [IO.Path]::GetFullPath($OutputPath)
if (-not $auditProject.StartsWith('D:\Game\tmp\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Use an isolated project under D:\Game\tmp.' }
if ($Suffix -notmatch '^[A-Za-z0-9_-]*$') { throw 'Suffix must be a simple identifier.' }
[IO.Directory]::CreateDirectory($auditOutput) | Out-Null
if ($Stage -in @('Prepare','Sync')) {
    if ($Stage -eq 'Prepare' -and (Test-Path -LiteralPath $auditProject)) { throw 'Prepare requires a new snapshot directory.' }
    $auditLock = Join-Path $auditProject 'Temp\UnityLockfile'
    if (Test-Path -LiteralPath $auditLock) {
        $auditLockStream = [IO.File]::Open($auditLock, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::None)
        $auditLockStream.Dispose()
    }
    foreach ($auditPart in @('Assets','Packages','ProjectSettings')) {
        & robocopy (Join-Path $auditSource $auditPart) (Join-Path $auditProject $auditPart) /E /R:1 /W:1 /NFL /NDL /NP /NJH /NJS ('/LOG+:' + (Join-Path $auditOutput ('Sync' + $Suffix + '.log'))) | Out-Null
        if ($LASTEXITCODE -ge 8) { throw "Copy failed: $auditPart" }
    }
    if ($Stage -eq 'Prepare') {
        & robocopy 'D:\Game\tmp\naval-ammunition-rules-20261011\Library' (Join-Path $auditProject 'Library') /E /R:1 /W:1 /NFL /NDL /NP /NJH /NJS ('/LOG:' + (Join-Path $auditOutput 'Cache.log')) | Out-Null
        if ($LASTEXITCODE -ge 8) { throw 'Library cache copy failed.' }
    }
    $auditSettingsPath = Join-Path $auditProject 'ProjectSettings\ProjectSettings.asset'
    $auditSettings = [IO.File]::ReadAllText($auditSettingsPath)
    $auditSettings = [regex]::Replace($auditSettings, '(?m)^  companyName:.*$', '  companyName: LatestPackageReviewValidation')
    $auditSettings = [regex]::Replace($auditSettings, '(?m)^  productName:.*$', '  productName: LatestPackageReview20261011')
    [IO.File]::WriteAllText($auditSettingsPath, $auditSettings, [Text.UTF8Encoding]::new($false))
    [pscustomobject]@{Project=$auditProject;Source=$auditSource;Identity='LatestPackageReviewValidation / LatestPackageReview20261011';Utc=[DateTime]::UtcNow.ToString('o')} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $auditOutput ('snapshot' + $Suffix + '.json')) -Encoding utf8
    Write-Output 'Isolated snapshot synchronized.'
    exit 0
}
$auditArgs = @('-batchmode','-projectPath',$auditProject,'-logFile',(Join-Path $auditOutput ($Stage + $Suffix + '.log')))
$auditExecutable = 'D:\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe'
if ($Stage -in @('EditMode','PlayMode')) {
    $auditArgs += @('-runTests','-testPlatform',$Stage,'-testResults',(Join-Path $auditOutput ($Stage + $Suffix + '.xml')))
    if ($TestFilter) { $auditArgs += @('-testFilter',$TestFilter) }
} elseif ($Stage -eq 'CaptureMenus') {
    $auditArgs += @('-executeMethod','ShatteredSail.Editor.UiAuditReadOnlyCapture.CaptureMenusFromCommandLine','-uiAuditPath',(Join-Path $auditOutput ('MenuScreens' + $Suffix)),'-quit')
} elseif ($Stage -eq 'Build') {
    $auditArgs += @('-nographics','-executeMethod','ShatteredSail.Editor.WindowsReleaseBuilder.BuildFromCommandLine','-releaseOutput',$BuildPath,'-quit')
} elseif ($Stage -eq 'Smoke') {
    $auditExecutable = $BuildPath
    $auditArgs = @('-screen-fullscreen','0','-screen-width','1280','-screen-height','720','-shatteredSailSmokeTest',(Join-Path $auditOutput ('PlayerSmoke' + $Suffix)),'-logFile',(Join-Path $auditOutput ('PlayerSmoke' + $Suffix + '.log')))
}
$auditArgs += $ExtraArguments
$auditQuotedArgs = $auditArgs | ForEach-Object { '"' + $_.Replace('"','\"') + '"' }
$auditProcess = Start-Process -FilePath $auditExecutable -ArgumentList $auditQuotedArgs -WindowStyle Hidden -PassThru
[pscustomobject]@{Stage=$Stage;ProcessId=$auditProcess.Id;Executable=$auditExecutable;Args=$auditArgs;StartedUtc=[DateTime]::UtcNow.ToString('o')} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $auditOutput ($Stage + $Suffix + '-launch.json')) -Encoding utf8
Write-Output ('PID ' + $auditProcess.Id)
$auditProcess.WaitForExit()
$auditExit = $auditProcess.ExitCode
[pscustomobject]@{Stage=$Stage;ExitCode=$auditExit;CompletedUtc=[DateTime]::UtcNow.ToString('o')} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $auditOutput ($Stage + $Suffix + '-exit.json')) -Encoding utf8
if ($auditExit -ne 0) { throw ('Process failed: ' + $auditExit) }
Write-Output 'PASS'
