param(
    [ValidateSet('Prepare','Compile','EditMode','PlayMode')][string]$Stage = 'Prepare',
    [string]$RunName = 'run1',
    [string]$ProjectPath = 'D:\Game\tmp\battle-deck-overview-20261014',
    [string]$TestFilter = 'GuidedTutorial_CompletionNotice;GuidedTutorial_LegacyCompletedCheckpointDoesNotRelockOrGrantRewards'
)
$ErrorActionPreference = 'Stop'
$completionSource = 'D:\Game\UnityClient'
$completionRoot = 'D:\Game\Artifacts\TutorialCompletion20261014\validation'
$completionUnity = 'D:\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe'
function Assert-CompletionChild([string]$Path, [string]$Root) {
    $resolved = [IO.Path]::GetFullPath($Path).TrimEnd('\')
    $parent = [IO.Path]::GetFullPath($Root).TrimEnd('\')
    if (-not $resolved.StartsWith($parent + '\', [StringComparison]::OrdinalIgnoreCase)) { throw ('Path outside allowed root: ' + $resolved) }
    $cursor = $resolved
    while ($cursor -and $cursor.Length -ge $parent.Length) {
        if ((Test-Path -LiteralPath $cursor) -and ((Get-Item -LiteralPath $cursor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw ('Reparse path is not allowed: ' + $cursor) }
        $cursor = Split-Path -Path $cursor -Parent
    }
    return $resolved
}
$completionProject = Assert-CompletionChild $ProjectPath 'D:\Game\tmp'
if ($RunName -notmatch '^[a-zA-Z0-9_-]+$') { throw 'RunName must be a simple identifier.' }
if (-not (Test-Path -LiteralPath $completionUnity)) { throw 'The required Unity editor is unavailable.' }
if (-not (Test-Path -LiteralPath (Join-Path $completionProject 'Library'))) { throw 'An existing warm isolated project is required.' }
if (@(Get-CimInstance Win32_Process -Filter "name='Unity.exe'" | Where-Object { $_.CommandLine -and $_.CommandLine.Replace('/','\').IndexOf($completionProject, [StringComparison]::OrdinalIgnoreCase) -ge 0 }).Count) { throw 'Isolated project is already running.' }
$completionRetired = Assert-CompletionChild (Join-Path $completionProject 'Assets\_Project\Scenes\UIPreviewsForMe') $completionProject
if (Test-Path -LiteralPath $completionRetired) { throw 'Choose an isolated project without retired preview assets.' }
$completionUnityLock = Assert-CompletionChild (Join-Path $completionProject 'Temp\UnityLockfile') $completionProject
if (Test-Path -LiteralPath $completionUnityLock) {
    try { $completionCheck = [IO.File]::Open($completionUnityLock, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::None); $completionCheck.Dispose() }
    catch { throw 'The isolated Unity project is already open.' }
}
$completionLeases = [Collections.Generic.List[IDisposable]]::new()
try {
    foreach ($existingLock in @(Get-ChildItem -LiteralPath $completionProject -Filter '*.lock' -File)) {
        $safeLock = Assert-CompletionChild $existingLock.FullName $completionProject
        $completionLeases.Add([IO.File]::Open($safeLock, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None))
    }
    $completionLeasePath = Assert-CompletionChild (Join-Path $completionProject 'TutorialCompletion20261014.lock') $completionProject
    if (-not (Test-Path -LiteralPath $completionLeasePath)) { $completionLeases.Add([IO.File]::Open($completionLeasePath, [IO.FileMode]::CreateNew, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)) }
    $completionSettingsPath = Assert-CompletionChild (Join-Path $completionProject 'ProjectSettings\ProjectSettings.asset') $completionProject
    if ($Stage -eq 'Prepare') {
        foreach ($part in @('Assets','Packages','ProjectSettings')) {
            $destination = Assert-CompletionChild (Join-Path $completionProject $part) $completionProject
            $copy = @((Join-Path $completionSource $part), $destination, '/E','/R:1','/W:1','/NFL','/NDL','/NP','/NJH','/NJS','/XJ')
            if ($part -eq 'Assets') { $copy += @('/XD', (Join-Path $completionSource 'Assets\_Project\Scenes\UIPreviewsForMe'), '/XF', 'UIPreviewsForMe.meta') }
            & robocopy @copy | Out-Null
            if ($LASTEXITCODE -ge 8) { throw ('Copy failed: ' + $part) }
        }
        # Remove only stale test source files from the isolated copy. Production source is never deleted.
        $testRoot = Assert-CompletionChild (Join-Path $completionProject 'Assets\_Project\Tests') $completionProject
        foreach ($oldTest in @(Get-ChildItem -LiteralPath $testRoot -Filter '*.cs' -File -Recurse)) {
            $relative = $oldTest.FullName.Substring($testRoot.Length + 1)
            if (Test-Path -LiteralPath (Join-Path $completionSource ('Assets\_Project\Tests\' + $relative))) { continue }
            $safeTest = Assert-CompletionChild $oldTest.FullName $testRoot
            Remove-Item -LiteralPath $safeTest -Force
            $safeMeta = Assert-CompletionChild ($safeTest + '.meta') $testRoot
            if (Test-Path -LiteralPath $safeMeta) { Remove-Item -LiteralPath $safeMeta -Force }
            Write-Output ('Removed stale isolated test: ' + $relative)
        }
        $identity = 'TutorialCompletionValidation20261014_' + [Guid]::NewGuid().ToString('N')
        $settings = [IO.File]::ReadAllText($completionSettingsPath)
        $settings = [regex]::Replace($settings, '(?m)^  companyName:.*$', ('  companyName: ' + $identity))
        $settings = [regex]::Replace($settings, '(?m)^  productName:.*$', ('  productName: ' + $identity))
        [IO.File]::WriteAllText($completionSettingsPath, $settings, [Text.UTF8Encoding]::new($false))
        if (Test-Path -LiteralPath $completionRetired) { throw 'Retired preview assets must remain absent.' }
        Write-Output ('Prepared current source with isolated PlayerPrefs identity: ' + $identity + '; project=' + $completionProject + '. No scenes generated.')
        exit 0
    }
    $settings = [IO.File]::ReadAllText($completionSettingsPath)
    foreach ($field in @('companyName','productName')) {
        if ($settings -notmatch ('(?m)^  ' + $field + ': TutorialCompletionValidation20261014_[a-f0-9]{32}\r?$')) { throw 'Run Prepare first; production save identity is forbidden.' }
    }
    $completionOutput = Assert-CompletionChild (Join-Path $completionRoot $RunName) $completionRoot
    if (Test-Path -LiteralPath $completionOutput) { throw 'Use a fresh RunName to preserve previous output.' }
    [IO.Directory]::CreateDirectory($completionOutput) | Out-Null
    $log = Assert-CompletionChild (Join-Path $completionOutput ($Stage + '.log')) $completionOutput
    $completionArgs = @('-batchmode','-force-d3d11','-projectPath',$completionProject,'-logFile',$log)
    if ($Stage -eq 'Compile') { $completionArgs += '-quit' }
    else {
        if ([string]::IsNullOrWhiteSpace($TestFilter)) { throw 'An explicit test filter is required.' }
        $results = Assert-CompletionChild (Join-Path $completionOutput 'results.xml') $completionOutput
        $completionArgs += @('-runTests','-testPlatform',$Stage,'-testFilter',$TestFilter,'-testResults',$results)
        if ($Stage -eq 'PlayMode') {
            $screens = Assert-CompletionChild (Join-Path $completionOutput 'Screens') $completionOutput
            [IO.Directory]::CreateDirectory($screens) | Out-Null
            $completionArgs += @('-navalVisualCapturePath',$screens)
        }
    }
    $quoted = $completionArgs | ForEach-Object { '"' + $_.Replace('"','\"') + '"' }
    $process = Start-Process -FilePath $completionUnity -WindowStyle Hidden -ArgumentList $quoted -PassThru
    $launch = Assert-CompletionChild (Join-Path $completionOutput 'launch.json') $completionOutput
    [pscustomobject]@{ Stage=$Stage; ProcessId=$process.Id; Project=$completionProject; Arguments=$completionArgs } | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath $launch -Encoding utf8
    Write-Output ('Unity ' + $Stage + ' PID ' + $process.Id + '; output=' + $completionOutput)
    $process.WaitForExit(); $process.Refresh()
    if (Test-Path -LiteralPath $completionRetired) { throw ('Unity unexpectedly created retired preview assets; inspect ' + $completionOutput) }
    if ($process.ExitCode -ne 0) { throw ('Unity failed with exit ' + $process.ExitCode + '; inspect ' + $completionOutput) }
    if ($Stage -eq 'EditMode' -or $Stage -eq 'PlayMode') {
        [xml]$result = Get-Content -Raw -LiteralPath $results
        if ($result.'test-run'.result -ne 'Passed' -or [int]$result.'test-run'.passed -lt 1) { throw ('No passing tests or test failure; inspect ' + $completionOutput) }
        Write-Output ('Passed ' + $result.'test-run'.passed + ' tests.')
    }
    Write-Output ('Completed ' + $Stage + ': ' + $completionOutput)
} finally { foreach ($completionLease in $completionLeases) { $completionLease.Dispose() } }
