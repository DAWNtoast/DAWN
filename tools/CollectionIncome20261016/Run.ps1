param(
    [ValidateSet('Prepare','Compile','EditMode','PlayMode')][string]$Stage = 'Prepare',
    [string]$RunName = 'run1',
    [string]$ProjectPath = 'D:\Game\tmp\departure-chart-inline-20261014',
    [string]$TestFilter = ''
)
$ErrorActionPreference = 'Stop'
$incomeSource = 'D:\Game\UnityClient'
$incomeRoot = 'D:\Game\Artifacts\CollectionIncome20261016\validation'
$incomeUnity = 'D:\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe'
function Assert-IncomeChild([string]$Path, [string]$Root) {
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
$incomeProject = Assert-IncomeChild $ProjectPath 'D:\Game\tmp'
if ($RunName -notmatch '^[a-zA-Z0-9_-]+$') { throw 'RunName must be a simple identifier.' }
if (-not (Test-Path -LiteralPath $incomeUnity)) { throw 'The required Unity editor is unavailable.' }
if (-not (Test-Path -LiteralPath (Join-Path $incomeProject 'Library'))) { throw 'An existing warm isolated project is required.' }
if (@(Get-CimInstance Win32_Process -Filter "name='Unity.exe'" | Where-Object { $_.CommandLine -and $_.CommandLine.Replace('/','\').IndexOf($incomeProject, [StringComparison]::OrdinalIgnoreCase) -ge 0 }).Count) { throw 'Isolated project is already running.' }
$incomeRetired = Assert-IncomeChild (Join-Path $incomeProject 'Assets\_Project\Scenes\UIPreviewsForMe') $incomeProject
if (Test-Path -LiteralPath $incomeRetired) { throw 'Choose an isolated project without retired preview assets.' }
$incomeUnityLock = Assert-IncomeChild (Join-Path $incomeProject 'Temp\UnityLockfile') $incomeProject
if (Test-Path -LiteralPath $incomeUnityLock) {
    try { $incomeCheck = [IO.File]::Open($incomeUnityLock, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::None); $incomeCheck.Dispose() }
    catch { throw 'The isolated Unity project is already open.' }
}
$incomeLeases = [Collections.Generic.List[IDisposable]]::new()
try {
    foreach ($existingLock in @(Get-ChildItem -LiteralPath $incomeProject -Filter '*.lock' -File)) {
        $safeLock = Assert-IncomeChild $existingLock.FullName $incomeProject
        $incomeLeases.Add([IO.File]::Open($safeLock, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None))
    }
    $incomeLeasePath = Assert-IncomeChild (Join-Path $incomeProject 'CollectionIncome20261016.lock') $incomeProject
    if (-not (Test-Path -LiteralPath $incomeLeasePath)) { $incomeLeases.Add([IO.File]::Open($incomeLeasePath, [IO.FileMode]::CreateNew, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)) }
    $incomeSettingsPath = Assert-IncomeChild (Join-Path $incomeProject 'ProjectSettings\ProjectSettings.asset') $incomeProject
    if ($Stage -eq 'Prepare') {
        # Reuse the imported art cache. Sync current source, tests, resources and
        # the three playable scenes only; never touch or generate preview scenes.
        foreach ($part in @('Packages','ProjectSettings','Assets\_Project\Core',
            'Assets\_Project\Content','Assets\_Project\Persistence','Assets\_Project\Presentation\Runtime',
            'Assets\_Project\Editor','Assets\_Project\Tests','Assets\_Project\Resources')) {
            $destination = Assert-IncomeChild (Join-Path $incomeProject $part) $incomeProject
            $copy = @((Join-Path $incomeSource $part), $destination, '/E','/R:1','/W:1','/NFL','/NDL','/NP','/NJH','/NJS','/XJ')
            & robocopy @copy | Out-Null
            if ($LASTEXITCODE -ge 8) { throw ('Copy failed: ' + $part) }
        }
        foreach ($scene in @('CoreGameplayDemo.unity','MainMenu.unity','TutorialVoyage.unity')) {
            foreach ($suffix in @('', '.meta')) {
                $relative = 'Assets\_Project\Scenes\' + $scene + $suffix
                $destination = Assert-IncomeChild (Join-Path $incomeProject $relative) $incomeProject
                Copy-Item -LiteralPath (Join-Path $incomeSource $relative) -Destination $destination -Force
            }
        }
        # Remove only stale test source files from the isolated copy. Production source is never deleted.
        $testRoot = Assert-IncomeChild (Join-Path $incomeProject 'Assets\_Project\Tests') $incomeProject
        foreach ($oldTest in @(Get-ChildItem -LiteralPath $testRoot -Filter '*.cs' -File -Recurse)) {
            $relative = $oldTest.FullName.Substring($testRoot.Length + 1)
            if (Test-Path -LiteralPath (Join-Path $incomeSource ('Assets\_Project\Tests\' + $relative))) { continue }
            $safeTest = Assert-IncomeChild $oldTest.FullName $testRoot
            Remove-Item -LiteralPath $safeTest -Force
            $safeMeta = Assert-IncomeChild ($safeTest + '.meta') $testRoot
            if (Test-Path -LiteralPath $safeMeta) { Remove-Item -LiteralPath $safeMeta -Force }
            Write-Output ('Removed stale isolated test: ' + $relative)
        }
        $identity = 'CollectionIncomeValidation20261016_' + [Guid]::NewGuid().ToString('N')
        $settings = [IO.File]::ReadAllText($incomeSettingsPath)
        $settings = [regex]::Replace($settings, '(?m)^  companyName:.*$', ('  companyName: ' + $identity))
        $settings = [regex]::Replace($settings, '(?m)^  productName:.*$', ('  productName: ' + $identity))
        [IO.File]::WriteAllText($incomeSettingsPath, $settings, [Text.UTF8Encoding]::new($false))
        if (Test-Path -LiteralPath $incomeRetired) { throw 'Retired preview assets must remain absent.' }
        Write-Output ('Prepared current source with isolated PlayerPrefs identity: ' + $identity + '; project=' + $incomeProject + '. No scenes generated.')
        exit 0
    }
    $settings = [IO.File]::ReadAllText($incomeSettingsPath)
    foreach ($field in @('companyName','productName')) {
        if ($settings -notmatch ('(?m)^  ' + $field + ': CollectionIncomeValidation20261016_[a-f0-9]{32}\r?$')) { throw 'Run Prepare first; production save identity is forbidden.' }
    }
    $incomeOutput = Assert-IncomeChild (Join-Path $incomeRoot $RunName) $incomeRoot
    if (Test-Path -LiteralPath $incomeOutput) { throw 'Use a fresh RunName to preserve previous output.' }
    [IO.Directory]::CreateDirectory($incomeOutput) | Out-Null
    $log = Assert-IncomeChild (Join-Path $incomeOutput ($Stage + '.log')) $incomeOutput
    $incomeArgs = @('-batchmode','-force-d3d11','-projectPath',$incomeProject,'-logFile',$log)
    if ($Stage -eq 'Compile') { $incomeArgs += '-quit' }
    else {
        if ([string]::IsNullOrWhiteSpace($TestFilter)) {
            if ($Stage -eq 'EditMode') {
                $TestFilter = 'CoreWarehouseIncomeTests;ProfileSaveJournalTests;CoreVoyageSessionTests'
            } else {
                # These files extend a shared NUnit fixture, so filter by their
                # actual test method names instead of the source file names.
                $testNames = [Collections.Generic.List[string]]::new()
                foreach ($file in @('GuidedTutorialFlowTests.cs','WarehouseTreasureDragFlowTests.cs')) {
                    $testPath = Assert-IncomeChild (Join-Path $incomeProject ('Assets\_Project\Tests\PlayMode\' + $file)) $incomeProject
                    if (-not (Test-Path -LiteralPath $testPath)) { throw ('Missing test source; run Prepare after integration: ' + $file) }
                    foreach ($match in [regex]::Matches([IO.File]::ReadAllText($testPath), 'public\s+IEnumerator\s+([A-Za-z0-9_]+)\s*\(')) {
                        $testNames.Add($match.Groups[1].Value)
                    }
                }
                if ($testNames.Count -lt 1) { throw 'No PlayMode test methods found.' }
                # The income fixture also contains synchronous NUnit Test methods.
                $TestFilter = 'CollectionIncomeFlowTests;' + ($testNames -join ';')
            }
        }
        $results = Assert-IncomeChild (Join-Path $incomeOutput 'results.xml') $incomeOutput
        $incomeArgs += @('-runTests','-testPlatform',$Stage,'-testFilter',$TestFilter,'-testResults',$results)
        if ($Stage -eq 'PlayMode') {
            $screens = Assert-IncomeChild (Join-Path $incomeOutput 'Screens') $incomeOutput
            [IO.Directory]::CreateDirectory($screens) | Out-Null
            $incomeArgs += @('-navalVisualCapturePath',$screens)
        }
    }
    $quoted = $incomeArgs | ForEach-Object { '"' + $_.Replace('"','\"') + '"' }
    $process = Start-Process -FilePath $incomeUnity -WindowStyle Hidden -ArgumentList $quoted -PassThru
    $launch = Assert-IncomeChild (Join-Path $incomeOutput 'launch.json') $incomeOutput
    [pscustomobject]@{ Stage=$Stage; ProcessId=$process.Id; Project=$incomeProject; Arguments=$incomeArgs } | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath $launch -Encoding utf8
    Write-Output ('Unity ' + $Stage + ' PID ' + $process.Id + '; output=' + $incomeOutput)
    $process.WaitForExit(); $process.Refresh()
    if (Test-Path -LiteralPath $incomeRetired) { throw ('Unity unexpectedly created retired preview assets; inspect ' + $incomeOutput) }
    if ($process.ExitCode -ne 0) { throw ('Unity failed with exit ' + $process.ExitCode + '; inspect ' + $incomeOutput) }
    if ($Stage -eq 'EditMode' -or $Stage -eq 'PlayMode') {
        [xml]$result = Get-Content -Raw -LiteralPath $results
        if ($result.'test-run'.result -ne 'Passed' -or [int]$result.'test-run'.passed -lt 1) { throw ('No passing tests or test failure; inspect ' + $incomeOutput) }
        Write-Output ('Passed ' + $result.'test-run'.passed + ' tests.')
    }
    Write-Output ('Completed ' + $Stage + ': ' + $incomeOutput)
} finally { foreach ($incomeLease in $incomeLeases) { $incomeLease.Dispose() } }
