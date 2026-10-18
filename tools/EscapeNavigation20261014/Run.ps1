param(
    [ValidateSet('Prepare','Compile','EditMode','PlayMode')][string]$Stage = 'Prepare',
    [string]$RunName = 'run1',
    [string]$ProjectPath = 'D:\Game\tmp\escape-navigation-20261014',
    [string]$TestFilter = ''
)
$ErrorActionPreference = 'Stop'
$escapeSource = 'D:\Game\UnityClient'
$escapeProject = [IO.Path]::GetFullPath($ProjectPath).TrimEnd('\')
$escapeRoot = 'D:\Game\Artifacts\EscapeNavigation20261014\validation'
$escapeUnity = 'D:\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe'
$escapeIdentity = 'EscapeNavigationValidation20261014'
if (-not $escapeProject.StartsWith('D:\Game\tmp\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Use an isolated project below D:\Game\tmp.' }
if ($RunName -notmatch '^[a-zA-Z0-9_-]+$') { throw 'RunName must be a simple identifier.' }
if (-not (Test-Path -LiteralPath (Join-Path $escapeProject 'Library'))) { throw 'A warm isolated project is required.' }
if (-not (Test-Path -LiteralPath $escapeUnity)) { throw 'The required Unity editor is unavailable.' }
if (@(Get-CimInstance Win32_Process -Filter "name='Unity.exe'" | Where-Object { $_.CommandLine -and $_.CommandLine.Replace('/','\').IndexOf($escapeProject, [StringComparison]::OrdinalIgnoreCase) -ge 0 }).Count) { throw 'Isolated project is already running.' }
$escapeUnityLock = Join-Path $escapeProject 'Temp\UnityLockfile'
if (Test-Path -LiteralPath $escapeUnityLock) {
    try { $escapeCheck = [IO.File]::Open($escapeUnityLock, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::None); $escapeCheck.Dispose() }
    catch { throw 'The isolated Unity project is already open.' }
}
[IO.Directory]::CreateDirectory($escapeRoot) | Out-Null
$escapeLease = $null
try {
    # This lease serializes preparation and verification for this task.
    $escapeLease = [IO.File]::Open((Join-Path $escapeProject 'EscapeNavigation20261014.lock'), [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
    $escapeRetired = [IO.Path]::GetFullPath((Join-Path $escapeProject 'Assets\_Project\Scenes\UIPreviewsForMe'))
    $escapeSettingsPath = Join-Path $escapeProject 'ProjectSettings\ProjectSettings.asset'
    if ($Stage -eq 'Prepare') {
        # Use a dedicated project that has never contained retired scene assets.
        if (Test-Path -LiteralPath $escapeRetired) { throw 'Choose an isolated project without retired preview assets.' }
        foreach ($escapePart in @('Assets','Packages','ProjectSettings')) {
            $escapeCopy = @((Join-Path $escapeSource $escapePart), (Join-Path $escapeProject $escapePart), '/E','/R:1','/W:1','/NFL','/NDL','/NP','/NJH','/NJS')
            if ($escapePart -eq 'Assets') {
                $escapeCopy += @('/XD', (Join-Path $escapeSource 'Assets\_Project\Scenes\UIPreviewsForMe'), '/XF', 'UIPreviewsForMe.meta')
            }
            & robocopy @escapeCopy | Out-Null
            if ($LASTEXITCODE -ge 8) { throw ('Copy failed: ' + $escapePart) }
        }
        $escapeScratch = [IO.Path]::GetFullPath((Join-Path $escapeProject 'Assets\_Project'))
        foreach ($escapeOld in @(Get-ChildItem -LiteralPath $escapeScratch -Filter '*.cs' -File -Recurse)) {
            $escapeRelative = $escapeOld.FullName.Substring($escapeScratch.Length + 1)
            if (Test-Path -LiteralPath (Join-Path $escapeSource ('Assets\_Project\' + $escapeRelative))) { continue }
            $escapeResolved = [IO.Path]::GetFullPath($escapeOld.FullName)
            if (-not $escapeResolved.StartsWith($escapeScratch + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Stale source is outside the isolated project.' }
            Remove-Item -LiteralPath $escapeResolved -Force
            if (Test-Path -LiteralPath ($escapeResolved + '.meta')) { Remove-Item -LiteralPath ($escapeResolved + '.meta') -Force }
        }
        $escapeSettings = [IO.File]::ReadAllText($escapeSettingsPath)
        $escapeSettings = [regex]::Replace($escapeSettings, '(?m)^  companyName:.*$', ('  companyName: ' + $escapeIdentity))
        $escapeSettings = [regex]::Replace($escapeSettings, '(?m)^  productName:.*$', ('  productName: ' + $escapeIdentity))
        [IO.File]::WriteAllText($escapeSettingsPath, $escapeSettings, [Text.UTF8Encoding]::new($false))
        if (Test-Path -LiteralPath $escapeRetired) { throw 'Retired preview assets must remain absent.' }
        Write-Output ('Prepared current source with separate PlayerPrefs identity: ' + $escapeProject + '. Warm Library retained; no scenes generated.')
        exit 0
    }
    if (Test-Path -LiteralPath $escapeRetired) { throw 'Choose an isolated project without retired preview assets before launching Unity.' }
    $escapeSettings = [IO.File]::ReadAllText($escapeSettingsPath)
    foreach ($escapeSetting in @('companyName','productName')) {
        if ($escapeSettings -notmatch ('(?m)^  ' + $escapeSetting + ': ' + [regex]::Escape($escapeIdentity) + '\s*$')) { throw 'Run Prepare to isolate PlayerPrefs before launching Unity.' }
    }
    if (($Stage -eq 'EditMode' -or $Stage -eq 'PlayMode') -and -not $TestFilter) {
        $TestFilter = if ($Stage -eq 'EditMode') {
            'PortSettingsModalTests;VoyageSettingsModalTests'
        } else {
            'EscapeNavigation_;MarketScene_SwitchingInvalidatesQuotesAndEscapeCancelsChoiceBeforeClosingShop;DeckBuilderMinimum_;DeckBuilderCards_DragChangesOnceAndCancellationNeverChangesTheDeck;PortSettingsRuntimeIsolationTests;SettingsPrefab_'
        }
    }
    $escapeOutput = Join-Path $escapeRoot $RunName
    if (Test-Path -LiteralPath $escapeOutput) { throw 'Use a fresh RunName to preserve existing output.' }
    [IO.Directory]::CreateDirectory($escapeOutput) | Out-Null
    $escapeArgs = @('-batchmode','-force-d3d11','-projectPath',$escapeProject,'-logFile',(Join-Path $escapeOutput ($Stage + '.log')))
    if ($Stage -eq 'Compile') {
        $escapeArgs += '-quit'
    } else {
        $escapeArgs += @('-runTests','-testPlatform',$Stage,'-testFilter',$TestFilter,'-testResults',(Join-Path $escapeOutput 'results.xml'))
    }
    $escapeQuoted = $escapeArgs | ForEach-Object { '"' + $_.Replace('"','\"') + '"' }
    $escapeProcess = Start-Process -FilePath $escapeUnity -WindowStyle Hidden -ArgumentList $escapeQuoted -PassThru
    [pscustomobject]@{ Stage=$Stage; ProcessId=$escapeProcess.Id; Project=$escapeProject; Arguments=$escapeArgs } | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $escapeOutput 'launch.json') -Encoding utf8
    Write-Output ('Unity ' + $Stage + ' PID ' + $escapeProcess.Id + '; output=' + $escapeOutput)
    $escapeProcess.WaitForExit()
    $escapeProcess.Refresh()
    if (Test-Path -LiteralPath $escapeRetired) { throw ('Unity unexpectedly created retired preview assets; inspect ' + $escapeOutput) }
    if ($escapeProcess.ExitCode -ne 0) { throw ('Unity failed with exit ' + $escapeProcess.ExitCode + '; inspect ' + $escapeOutput) }
    if ($Stage -eq 'EditMode' -or $Stage -eq 'PlayMode') {
        [xml]$escapeResults = Get-Content -Raw -LiteralPath (Join-Path $escapeOutput 'results.xml')
        if ($escapeResults.'test-run'.result -ne 'Passed' -or [int]$escapeResults.'test-run'.passed -lt 1) { throw ('Tests failed or no tests matched; inspect ' + $escapeOutput) }
        Write-Output ('Passed ' + $escapeResults.'test-run'.passed + ' tests.')
    }
    Write-Output ('Completed ' + $Stage + ': ' + $escapeOutput)
} finally {
    if ($escapeLease) { $escapeLease.Dispose() }
}
