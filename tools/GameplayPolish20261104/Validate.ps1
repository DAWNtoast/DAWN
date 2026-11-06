param(
    [Parameter(Mandatory=$true)][ValidateSet('Prepare','Sync','Test','Status','Cleanup')][string]$Stage,
    [ValidatePattern('^[A-Za-z0-9_-]+$')][string]$Run = 'run1',
    [ValidateSet('EditMode','PlayMode')][string]$TestPlatform = 'EditMode',
    [string]$TestFilter = '',
    [ValidateRange(1,120)][int]$TimeoutMinutes = 30,
    [string]$UnityEditor = 'D:\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe'
)
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..')).TrimEnd('\','/')
$main = Join-Path $workspace 'UnityClient'
$scratch = Join-Path $workspace 'tmp\prefab-runtime-20261014'
$output = Join-Path $workspace 'outputs\gameplay-polish-20261104\validation'
$lease = Join-Path $scratch 'GameplayPolish20261104.lock'
$journalPath = Join-Path $output 'scratch-journal.json'
$statePath = Join-Path $output 'state.json'
$parts = @('Assets','Packages','ProjectSettings')
$changes = [Collections.Generic.List[object]]::new()
$addedDirectories = [Collections.Generic.List[string]]::new()
$removedDirectories = [Collections.Generic.List[string]]::new()
$utf8 = [Text.UTF8Encoding]::new($false)

function Assert-Path([string]$Path, [string]$Root, [switch]$AllowRoot) {
    $resolved = [IO.Path]::GetFullPath($Path).TrimEnd('\','/')
    $allowed = [IO.Path]::GetFullPath($Root).TrimEnd('\','/')
    if ((!$AllowRoot -or $resolved -ne $allowed) -and
        !$resolved.StartsWith($allowed + '\', [StringComparison]::OrdinalIgnoreCase)) {
        throw ('Path escaped allowed task root: ' + $resolved)
    }
    for ($at=$resolved; $at; $at=Split-Path $at -Parent) {
        if ((Test-Path -LiteralPath $at) -and
            ((Get-Item -LiteralPath $at -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw ('Linked path is not allowed: ' + $at)
        }
    }
}
function Write-Json([string]$Path, $Value) {
    Assert-Path $Path $output
    [IO.File]::WriteAllText($Path, (ConvertTo-Json -InputObject $Value -Depth 9), $utf8)
}
function Get-Tree([string]$Root) {
    Assert-Path $Root $workspace
    $pending = [Collections.Generic.Stack[string]]::new()
    $pending.Push([IO.Path]::GetFullPath($Root))
    while ($pending.Count) {
        $directory = $pending.Pop()
        Assert-Path $directory $Root -AllowRoot
        foreach ($item in Get-ChildItem -LiteralPath $directory -Force) {
            if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw ('Linked tree entry: ' + $item.FullName) }
            if ($item.Name -in @('UIPreviewsForMe','UIPreviewsForMe.meta')) {
                if ($item.PSIsContainer -and @(Get-ChildItem -LiteralPath $item.FullName -Force).Count) {
                    throw ('Retired previews must remain empty: ' + $item.FullName)
                }
                continue
            }
            $item
            if ($item.PSIsContainer) { $pending.Push($item.FullName) }
        }
    }
}
function Get-Manifest([string]$Project) {
    foreach ($part in $parts) {
        foreach ($file in Get-Tree (Join-Path $Project $part) | Where-Object { !$_.PSIsContainer }) {
            [pscustomobject]@{ Path=$file.FullName.Substring($Project.Length+1); Bytes=$file.Length; SHA256=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash }
        }
    }
}
function Compare-Manifest($Expected, $Actual) {
    $lookup = @{}
    foreach ($entry in @($Expected)) { $lookup[$entry.Path] = $entry.SHA256 }
    foreach ($entry in @($Actual)) {
        if (!$lookup.ContainsKey($entry.Path)) { [pscustomobject]@{Path=$entry.Path;Kind='Added'} }
        elseif ($lookup[$entry.Path] -ne $entry.SHA256) { [pscustomobject]@{Path=$entry.Path;Kind='Modified'} }
        $lookup.Remove($entry.Path)
    }
    foreach ($key in $lookup.Keys) { [pscustomobject]@{Path=$key;Kind='Missing'} }
}
function Assert-Idle {
    $users = @(Get-CimInstance Win32_Process | Where-Object {
        $_.ProcessId -ne $PID -and $_.CommandLine -and
        $_.CommandLine.Replace('/','\').IndexOf($scratch,[StringComparison]::OrdinalIgnoreCase) -ge 0
    })
    if ($users.Count) { throw ('Borrowed project is referenced by active processes: ' + ($users.ProcessId -join ',')) }
    if (Test-Path -LiteralPath (Join-Path $scratch 'Temp\UnityLockfile')) { throw 'Borrowed project has UnityLockfile; preserve it.' }
}
function Assert-Lease {
    if (!(Test-Path -LiteralPath $lease) -or [IO.File]::ReadAllText($lease) -ne $output) { throw 'Task lease is missing or belongs to another task.' }
    if (@(Get-ChildItem -LiteralPath $scratch -Filter '*.lock' -File | Where-Object FullName -ne $lease).Count) { throw 'Another task lease is present; preserve both and resolve ownership.' }
}
function Save-Journal {
    Write-Json $journalPath ([pscustomobject]@{Changes=@($changes.ToArray());AddedDirectories=@($addedDirectories.ToArray());RemovedDirectories=@($removedDirectories.ToArray())})
}
function Load-Journal {
    $saved = Get-Content -LiteralPath $journalPath -Raw | ConvertFrom-Json
    foreach ($entry in @($saved.Changes)) { $changes.Add($entry) }
    foreach ($directory in @($saved.AddedDirectories)) { $addedDirectories.Add($directory) }
    foreach ($directory in @($saved.RemovedDirectories)) { if ($directory) { $removedDirectories.Add($directory) } }
}
function Record-Target([string]$Target) {
    Assert-Path $Target $scratch
    if (@($changes | Where-Object Target -eq $Target).Count) { return }
    $backup = $null
    $beforeHash = $null
    if (Test-Path -LiteralPath $Target) {
        if ((Get-Item -LiteralPath $Target -Force).PSIsContainer) { throw ('Expected file: ' + $Target) }
        $beforeHash = (Get-FileHash -LiteralPath $Target).Hash
        $backup = Join-Path $output ('OriginalScratch\' + $Target.Substring($scratch.Length+1))
        Assert-Path $backup $output
        New-Item -ItemType Directory -Path (Split-Path $backup -Parent) -Force | Out-Null
        Copy-Item -LiteralPath $Target -Destination $backup
        if ((Get-FileHash -LiteralPath $backup).Hash -ne $beforeHash) { throw ('Backup verification failed: ' + $Target) }
    }
    $changes.Add([pscustomobject]@{Target=$Target;Backup=$backup;Added=($null -eq $backup);BeforeSHA256=$beforeHash})
    Save-Journal
}
function Ensure-Directory([string]$Directory) {
    Assert-Path $Directory $scratch -AllowRoot
    if (Test-Path -LiteralPath $Directory) { return }
    Ensure-Directory (Split-Path $Directory -Parent)
    # Unity may create a folder meta during import; journal it before launching.
    if ($Directory.StartsWith((Join-Path $scratch 'Assets')+'\',[StringComparison]::OrdinalIgnoreCase)) { Record-Target ($Directory+'.meta') }
    if (!$removedDirectories.Contains($Directory) -and !$addedDirectories.Contains($Directory)) { $addedDirectories.Add($Directory) }
    Save-Journal
    New-Item -ItemType Directory -Path $Directory | Out-Null
}
function Test-TaskSourcePath([string]$Relative, $Scope) {
    $normalized = $Relative.Replace('\','/')
    if (@($Scope.Paths | Where-Object { $_.Replace('\','/') -eq $normalized }).Count) { return $true }
    foreach ($pattern in $Scope.Patterns) { if ($normalized -match $pattern) { return $true } }
    return $false
}
function New-TaskSourceScope($SourceInputs) {
    $explicit = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $sources = @{}
    foreach ($entry in $SourceInputs) { $sources[$entry.Path.Replace('\','/')]=$entry }
    $beforeRoot = Join-Path (Split-Path $output -Parent) 'source-before\UnityClient'
    if (!(Test-Path -LiteralPath $beforeRoot)) { throw 'Task source-before evidence is required to identify modified input files.' }
    foreach ($file in Get-Tree $beforeRoot | Where-Object { !$_.PSIsContainer }) {
        $relative = $file.FullName.Substring($beforeRoot.Length+1).Replace('\','/')
        if (!$sources.ContainsKey($relative) -or (Get-FileHash -LiteralPath $file.FullName).Hash -ne $sources[$relative].SHA256) {
            [void]$explicit.Add($relative)
            if ($sources.ContainsKey($relative+'.meta')) { [void]$explicit.Add($relative+'.meta') }
        }
    }
    $changedPath = Join-Path (Split-Path $output -Parent) 'changed-existing-files.csv'
    if (Test-Path -LiteralPath $changedPath) {
        foreach ($entry in Import-Csv -LiteralPath $changedPath) {
            $relative = $entry.Path.Replace('\','/')
            if (!$relative.StartsWith('UnityClient/',[StringComparison]::OrdinalIgnoreCase)) { continue }
            $relative = $relative.Substring('UnityClient/'.Length)
            if (!$sources.ContainsKey($relative) -or $sources[$relative].SHA256 -ne $entry.SHA256) { throw ('Task input differs from changed-existing-files.csv: '+$relative) }
            [void]$explicit.Add($relative)
            if ($sources.ContainsKey($relative+'.meta')) { [void]$explicit.Add($relative+'.meta') }
        }
    }
    [pscustomobject]@{
        Paths=@($explicit | Sort-Object)
        Patterns=@(
            '^Assets/_Project/.*(?:Naval|CompactBattle|BattleCompactHud|BattleHud|ShipMarket|ShipPort|CardPack)[^/]*\.cs(?:\.meta)?$',
            '^Assets/_Project/Resources/(?:NavalTactics|BattleIcons|PortHeaders|HeaderIcons|Fonts|Shaders)(?:/|\.meta$)',
            '\.(?:asmdef|asmref)(?:\.meta)?$',
            '^(?:Packages|ProjectSettings)/'
        )
        ChangedFilesEvidenceSHA256=if(Test-Path -LiteralPath $changedPath){(Get-FileHash -LiteralPath $changedPath).Hash}else{$null}
        Reason='Protect this task''s changed source-before inputs, new card-pack guarantee tests, direct naval/HUD/ship-market/card-pack code and naval art/build dependencies. Other live-source changes are recorded while the isolated snapshot remains immutable.'
    }
}
function Require-TaskSourceStable($Differences, $Scope) {
    $protected = @($Differences | Where-Object { Test-TaskSourcePath $_.Path $Scope })
    if ($protected.Count) { throw ('Task/direct-dependency sources changed: '+(($protected | ForEach-Object Path) -join ', ')+'. Use a fresh Sync snapshot before testing.') }
}
function Assert-FrozenInputs([string]$Label) {
    $state = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
    foreach ($item in @(@($state.SourceManifest,$state.SourceManifestSHA256),@($state.ProjectManifest,$state.ProjectManifestSHA256),@($state.ProtectedSourceScope,$state.ProtectedSourceScopeSHA256))) {
        Assert-Path $item[0] $output
        if ((Get-FileHash -LiteralPath $item[0]).Hash -ne $item[1]) { throw 'Frozen evidence manifest changed.' }
    }
    $scope = Get-Content -LiteralPath $state.ProtectedSourceScope -Raw | ConvertFrom-Json
    $sourceDiff = @(Compare-Manifest (Import-Csv -LiteralPath $state.SourceManifest) @(Get-Manifest $main))
    $projectDiff = @(Compare-Manifest (Import-Csv -LiteralPath $state.ProjectManifest) @(Get-Manifest $scratch))
    $protectedDiff = @($sourceDiff | Where-Object { Test-TaskSourcePath $_.Path $scope })
    $check = [pscustomobject]@{Label=$Label;CheckedUtc=[DateTime]::UtcNow.ToString('o');SourceDifferences=$sourceDiff;SourceDrift=$sourceDiff;SourceMatchesCurrentMain=($sourceDiff.Count -eq 0);ProtectedSourceDifferences=$protectedDiff;TaskSourceStable=($protectedDiff.Count -eq 0);ProjectDifferences=$projectDiff;SourceManifestSHA256=$state.SourceManifestSHA256;ProjectManifestSHA256=$state.ProjectManifestSHA256;ProtectedSourceScopeSHA256=$state.ProtectedSourceScopeSHA256}
    Write-Json (Join-Path $output ($Label+'-input-check.json')) $check
    if ($projectDiff.Count) { throw ('Isolated snapshot inputs changed; inspect '+$Label+'-input-check.json. Preserve results and use a fresh Sync/Test run.') }
    Require-TaskSourceStable $sourceDiff $scope
    $state | Add-Member -NotePropertyName SourceMatchesCurrentMain -NotePropertyValue $check.SourceMatchesCurrentMain -Force
    $state | Add-Member -NotePropertyName ObservedSourceDrift -NotePropertyValue $sourceDiff -Force
    return $state
}

Assert-Path $scratch (Join-Path $workspace 'tmp')
Assert-Path $output (Join-Path $workspace 'outputs')
if ($Stage -eq 'Status') {
    $processes = @(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object { $_.CommandLine -and $_.CommandLine.Replace('/','\').IndexOf($scratch,[StringComparison]::OrdinalIgnoreCase) -ge 0 } | Select-Object ProcessId,CommandLine)
    [pscustomobject]@{Project=$scratch;LeasePresent=(Test-Path -LiteralPath $lease);Processes=$processes;State=if(Test-Path -LiteralPath $statePath){Get-Content -LiteralPath $statePath -Raw|ConvertFrom-Json}else{$null}} | ConvertTo-Json -Depth 5
    $log = Join-Path $output ($Run+'\unity.log')
    if (Test-Path -LiteralPath $log) { Get-Content -LiteralPath $log -Tail 22 }
    exit
}
Assert-Idle
foreach ($part in $parts) { Assert-Path (Join-Path $scratch $part) $scratch }
Assert-Path (Join-Path $scratch 'Library') $scratch
$settingsPath = Join-Path $scratch 'ProjectSettings\ProjectSettings.asset'
$settings = [IO.File]::ReadAllText($settingsPath)
if ($settings -notmatch '(?m)^  companyName: ShatteredSailPrefabValidation\r?$' -or
    $settings -notmatch '(?m)^  productName: PrefabRuntimeOptimization20261014\r?$') { throw 'Borrowed project must retain its isolated PlayerPrefs identity.' }

if ($Stage -eq 'Prepare') {
    if ((Test-Path -LiteralPath $journalPath) -or (Test-Path -LiteralPath $statePath)) { throw 'Prior task evidence exists. Use Sync for the current lease; do not overwrite archived runs.' }
    if (@(Get-ChildItem -LiteralPath $scratch -Filter '*.lock' -File).Count) { throw 'Borrowed project has a task lease; preserve it.' }
    if (!(Test-Path -LiteralPath (Join-Path $scratch 'Library'))) { throw 'Expected pre-existing shared warm Library.' }
    New-Item -ItemType Directory -Path $output -Force | Out-Null
    $leaseStream = [IO.File]::Open($lease,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
    try { $bytes=$utf8.GetBytes($output); $leaseStream.Write($bytes,0,$bytes.Length) } finally { $leaseStream.Dispose() }
    if (@(Get-ChildItem -LiteralPath $scratch -Filter '*.lock' -File | Where-Object FullName -ne $lease).Count) {
        Remove-Item -LiteralPath $lease -Force
        throw 'Another task acquired a lease during Prepare; no borrowed inputs were changed.'
    }
    Save-Journal
    $original = @(Get-Manifest $scratch)
    $original | Export-Csv -LiteralPath (Join-Path $output 'original-project-inputs.csv') -NoTypeInformation -Encoding utf8
    Write-Json (Join-Path $output 'preparation.json') ([pscustomobject]@{Project=$scratch;StartedUtc=[DateTime]::UtcNow.ToString('o');HadTemp=(Test-Path -LiteralPath (Join-Path $scratch 'Temp'));OriginalManifestSHA256=(Get-FileHash -LiteralPath (Join-Path $output 'original-project-inputs.csv')).Hash})
} else {
    Assert-Lease
    Load-Journal
}

if ($Stage -in @('Prepare','Sync')) {
    $snapshotRoot = Join-Path $output ('input-snapshots\'+$Run)
    if (Test-Path -LiteralPath $snapshotRoot) { throw 'Use a fresh Sync run name; previous input evidence is immutable.' }
    New-Item -ItemType Directory -Path $snapshotRoot -Force | Out-Null
    $sourceInputs = @(Get-Manifest $main)
    $sourceInputs | Export-Csv -LiteralPath (Join-Path $snapshotRoot 'source-manifest.csv') -NoTypeInformation -Encoding utf8
    $scope = New-TaskSourceScope $sourceInputs
    $scopePath = Join-Path $snapshotRoot 'protected-source-scope.json'
    Write-Json $scopePath $scope
    $expectedProjectInputs = [Collections.Generic.List[object]]::new()
    $sourcePaths = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($entry in $sourceInputs) {
        [void]$sourcePaths.Add($entry.Path)
        $source = Join-Path $main $entry.Path
        $target = Join-Path $scratch $entry.Path
        Assert-Path $source $main
        Assert-Path $target $scratch
        $desiredHash = $entry.SHA256
        $isolatedSettings = $null
        if ($entry.Path.Replace('\','/') -eq 'ProjectSettings/ProjectSettings.asset') {
            $isolatedSettings = [IO.File]::ReadAllText($source)
            if ($isolatedSettings -notmatch '(?m)^  companyName: ' -or $isolatedSettings -notmatch '(?m)^  productName: ') { throw 'Production PlayerSettings identity fields were not found.' }
            $isolatedSettings = [regex]::Replace($isolatedSettings,'(?m)^  companyName: [^\r\n]*','  companyName: ShatteredSailPrefabValidation')
            $isolatedSettings = [regex]::Replace($isolatedSettings,'(?m)^  productName: [^\r\n]*','  productName: PrefabRuntimeOptimization20261014')
            $desiredBytes = $utf8.GetBytes($isolatedSettings)
            $hasher = [Security.Cryptography.SHA256]::Create()
            try { $desiredHash = [BitConverter]::ToString($hasher.ComputeHash($desiredBytes)).Replace('-','') } finally { $hasher.Dispose() }
        }
        if (!(Test-Path -LiteralPath $target) -or (Get-FileHash -LiteralPath $target).Hash -ne $desiredHash) {
            Record-Target $target
            Ensure-Directory (Split-Path $target -Parent)
            if ($null -ne $isolatedSettings) { [IO.File]::WriteAllBytes($target,$desiredBytes) }
            else { Copy-Item -LiteralPath $source -Destination $target -Force }
            if ((Get-FileHash -LiteralPath $target).Hash -ne $desiredHash) { throw ('Source changed during copy or target verification failed: '+$entry.Path) }
        }
        $expectedProjectInputs.Add([pscustomobject]@{Path=$entry.Path;Bytes=(Get-Item -LiteralPath $target).Length;SHA256=$desiredHash})
        # Retain exact text/configuration evidence; images stay represented by hashes.
        if ($entry.Path -notmatch '^Assets[\\/]' -or [IO.Path]::GetExtension($entry.Path) -in @('.cs','.asmdef','.asmref','.meta')) {
            $archive = Join-Path $snapshotRoot ('validated-source\'+$entry.Path)
            Assert-Path $archive $output
            New-Item -ItemType Directory -Path (Split-Path $archive -Parent) -Force | Out-Null
            Copy-Item -LiteralPath $target -Destination $archive
            if ((Get-FileHash -LiteralPath $archive).Hash -ne $desiredHash) { throw ('Frozen input changed while archiving: '+$entry.Path) }
        }
    }
    # Remove all stale borrowed inputs, including old art, only after backup.
    foreach ($entry in @(Get-Manifest $scratch)) {
        if ($sourcePaths.Contains($entry.Path)) { continue }
        $target = Join-Path $scratch $entry.Path
        Record-Target $target
        Remove-Item -LiteralPath $target -Force
    }
    # Leaving an obsolete empty Assets directory makes Unity recreate its meta.
    # Remove only empty directories absent from the source, and restore them later.
    foreach ($part in $parts) {
        $staleDirectories = @(Get-Tree (Join-Path $scratch $part) | Where-Object PSIsContainer | Sort-Object { $_.FullName.Length } -Descending)
        foreach ($directory in $staleDirectories) {
            $relative = $directory.FullName.Substring($scratch.Length+1)
            if ((Test-Path -LiteralPath (Join-Path $main $relative)) -or @(Get-ChildItem -LiteralPath $directory.FullName -Force).Count) { continue }
            Assert-Path $directory.FullName $scratch
            if (!$addedDirectories.Contains($directory.FullName) -and !$removedDirectories.Contains($directory.FullName)) { $removedDirectories.Add($directory.FullName); Save-Journal }
            Remove-Item -LiteralPath $directory.FullName -Force
        }
    }
    $sourceDiff = @(Compare-Manifest $sourceInputs @(Get-Manifest $main))
    $protectedDiff = @($sourceDiff | Where-Object { Test-TaskSourcePath $_.Path $scope })
    Write-Json (Join-Path $snapshotRoot 'source-stability.json') ([pscustomobject]@{Differences=$sourceDiff;SourceDrift=$sourceDiff;SourceMatchesCurrentMain=($sourceDiff.Count -eq 0);ProtectedSourceDifferences=$protectedDiff;TaskSourceStable=($protectedDiff.Count -eq 0);CheckedUtc=[DateTime]::UtcNow.ToString('o')})
    Require-TaskSourceStable $sourceDiff $scope
    $projectInputs = @(Get-Manifest $scratch)
    $copyDiff = @(Compare-Manifest $expectedProjectInputs.ToArray() $projectInputs)
    Write-Json (Join-Path $snapshotRoot 'frozen-copy-verification.json') ([pscustomobject]@{Differences=$copyDiff;Result=if($copyDiff.Count){'FAIL'}else{'PASS'};SettingsIdentityOverride='Only companyName/productName preserve the borrowed validation identity.'})
    if ($copyDiff.Count) { throw 'Isolated snapshot differs from the exact source bytes verified during copying; preserve the lease and retry Sync.' }
    $projectInputs | Export-Csv -LiteralPath (Join-Path $snapshotRoot 'project-input-manifest.csv') -NoTypeInformation -Encoding utf8
    $sourceManifest = Join-Path $snapshotRoot 'source-manifest.csv'
    $projectManifest = Join-Path $snapshotRoot 'project-input-manifest.csv'
    Write-Json $statePath ([pscustomobject]@{Result='READY';SnapshotRun=$Run;SourceManifest=$sourceManifest;SourceManifestSHA256=(Get-FileHash -LiteralPath $sourceManifest).Hash;ProjectManifest=$projectManifest;ProjectManifestSHA256=(Get-FileHash -LiteralPath $projectManifest).Hash;ProtectedSourceScope=$scopePath;ProtectedSourceScopeSHA256=(Get-FileHash -LiteralPath $scopePath).Hash;SourceMatchesCurrentMain=($sourceDiff.Count -eq 0);SourceDrift=$sourceDiff;TaskSourceStable=$true;InputCount=$sourceInputs.Count;Project=$scratch;SyncedUtc=[DateTime]::UtcNow.ToString('o');ValidationScope='Frozen isolated snapshot; unrelated live source drift is recorded and does not claim validation of newer main inputs.'})
    Write-Output ('Prepared '+$sourceInputs.Count+' inputs; '+$changes.Count+' borrowed files journaled. Shared Library retained; Unity not started.')
    exit
}

if ($Stage -eq 'Test') {
    if ([string]::IsNullOrWhiteSpace($TestFilter) -or $TestFilter.Trim() -in @('*','.*')) { throw 'Provide an explicit non-empty targeted TestFilter.' }
    if (!(Test-Path -LiteralPath $UnityEditor -PathType Leaf)) { throw 'Unity Editor executable not found.' }
    $testOutput = Join-Path $output $Run
    if (Test-Path -LiteralPath $testOutput) { throw 'Use a fresh Test run name; prior results must be preserved.' }
    $state = Assert-FrozenInputs ($Run+'-before')
    & (Join-Path $workspace 'tools\ReleaseBuild\AssertMetaGuids.ps1') -AssetsPath (Join-Path $scratch 'Assets')
    New-Item -ItemType Directory -Path $testOutput | Out-Null
    $resultsPath = Join-Path $testOutput 'results.xml'
    $arguments = @('-batchmode','-force-d3d11','-projectPath',$scratch,'-runTests','-testPlatform',$TestPlatform,
        '-testFilter',$TestFilter,'-testResults',$resultsPath,'-logFile',(Join-Path $testOutput 'unity.log'))
    if ($TestPlatform -eq 'PlayMode') { $arguments += @('-navalVisualCapturePath',(Join-Path $testOutput 'Artifacts'),'-auditActionLogPath',(Join-Path $testOutput 'Actions')) }
    $quoted = foreach ($value in $arguments) { $escaped=[regex]::Replace($value,'(\\*)"','$1$1\"'); '"'+[regex]::Replace($escaped,'(\\+)$','$1$1')+'"' }
    $started = [DateTime]::UtcNow
    $process = Start-Process -FilePath $UnityEditor -ArgumentList ($quoted -join ' ') -WorkingDirectory $scratch -WindowStyle Hidden -PassThru
    $null = $process.Handle
    Write-Json (Join-Path $testOutput 'launch.json') ([pscustomobject]@{PID=$process.Id;Arguments=$arguments;StartedUtc=$started.ToString('o');TimeoutMinutes=$TimeoutMinutes;SourceManifestSHA256=$state.SourceManifestSHA256;ProjectManifestSHA256=$state.ProjectManifestSHA256;ProtectedSourceScopeSHA256=$state.ProtectedSourceScopeSHA256;SourceMatchesCurrentMain=$state.SourceMatchesCurrentMain;SourceDrift=$state.ObservedSourceDrift;SnapshotRun=$state.SnapshotRun})
    $timedOut = $false
    Write-Output ($TestPlatform+' started, PID '+$process.Id)
    while (!$process.WaitForExit(15000)) {
        Write-Output ($TestPlatform+' running: '+[int]([DateTime]::UtcNow-$started).TotalSeconds+'s')
        if (([DateTime]::UtcNow-$started).TotalMinutes -gt $TimeoutMinutes) {
            $timedOut = $true
            $process.Kill() # Only the exact process this invocation launched.
            if (!$process.WaitForExit(15000)) { throw 'Owned Unity process did not exit after timeout termination; retain lease and evidence.' }
            break
        }
    }
    $inputError = $null
    try { $null=Assert-FrozenInputs ($Run+'-after') } catch { $inputError=$_.Exception.Message }
    $afterCheckPath = Join-Path $output ($Run+'-after-input-check.json')
    $afterCheck = if (Test-Path -LiteralPath $afterCheckPath) { Get-Content -LiteralPath $afterCheckPath -Raw | ConvertFrom-Json } else { $null }
    $completion = [pscustomobject]@{ExitCode=$process.ExitCode;TimedOut=$timedOut;Seconds=([DateTime]::UtcNow-$started).TotalSeconds;SourceManifestSHA256=$state.SourceManifestSHA256;ProjectManifestSHA256=$state.ProjectManifestSHA256;InputValidationResult=if($inputError){'FAIL'}else{'PASS'};InputValidationError=$inputError;SourceMatchesCurrentMain=if($afterCheck){$afterCheck.SourceMatchesCurrentMain}else{$false};SourceDrift=if($afterCheck){$afterCheck.SourceDrift}else{@()};TaskSourceStable=if($afterCheck){$afterCheck.TaskSourceStable}else{$false};ResultsSHA256=if(Test-Path -LiteralPath $resultsPath){(Get-FileHash -LiteralPath $resultsPath).Hash}else{$null}}
    Write-Json (Join-Path $testOutput 'completion.json') $completion
    if (!(Test-Path -LiteralPath $resultsPath -PathType Leaf)) { throw ('Unity exited without results.xml. Exit='+$process.ExitCode+'; timeout='+$timedOut+'; inspect '+$testOutput) }
    [xml]$xml = Get-Content -LiteralPath $resultsPath -Raw
    $runResult = $xml.'test-run'
    $cases = @($xml.SelectNodes('//test-case'))
    $failures = @($cases | Where-Object { $_.result -notin @('Passed','Skipped') } | ForEach-Object { [pscustomobject]@{Name=$_.fullname;Result=$_.result;Details=$_.InnerText} })
    Write-Json (Join-Path $testOutput 'failures.json') $failures
    $consistent = $runResult -and $cases.Count -eq [int]$runResult.total -and
        @($cases | Where-Object result -eq 'Passed').Count -eq [int]$runResult.passed -and
        @($cases | Where-Object result -eq 'Skipped').Count -eq [int]$runResult.skipped -and
        [int]$runResult.total -eq ([int]$runResult.passed+[int]$runResult.skipped)
    $passed = !$timedOut -and $process.ExitCode -eq 0 -and !$inputError -and $consistent -and
        $runResult.result -in @('Passed','Skipped:Ignored') -and
        [int]$runResult.failed -eq 0 -and [int]$runResult.inconclusive -eq 0 -and [int]$runResult.passed -gt 0 -and !$failures.Count
    $summary = [pscustomobject]@{Result=if($passed){'PASS'}else{'FAIL'};UnityResult=$runResult.result;Total=[int]$runResult.total;Passed=[int]$runResult.passed;Failed=[int]$runResult.failed;Skipped=[int]$runResult.skipped;Consistent=$consistent;InputValidationResult=$completion.InputValidationResult;SourceMatchesCurrentMain=$completion.SourceMatchesCurrentMain;SourceDriftCount=@($completion.SourceDrift).Count;TaskSourceStable=$completion.TaskSourceStable;ExitCode=$process.ExitCode;TimedOut=$timedOut;Scope='Frozen isolated snapshot, not unrelated newer main-project inputs.'}
    Write-Json (Join-Path $testOutput 'summary.json') $summary
    $summary | ConvertTo-Json
    if (!$passed) { throw ('Validation failed; inspect '+$testOutput) }
    exit
}

# Cleanup is deliberately independent of current main/source identity: unrelated
# production work may continue, while before/after hashes protect it from cleanup.
$preparation = Get-Content -LiteralPath (Join-Path $output 'preparation.json') -Raw | ConvertFrom-Json
$originalPath = Join-Path $output 'original-project-inputs.csv'
if ((Get-FileHash -LiteralPath $originalPath).Hash -ne $preparation.OriginalManifestSHA256) { throw 'Original borrowed-input evidence changed; preserve backups.' }
$mainBefore = @(Get-Manifest $main)
$mainBefore | Export-Csv -LiteralPath (Join-Path $output 'pre-cleanup-main-inputs.csv') -NoTypeInformation -Encoding utf8
$freed = 0L
foreach ($change in $changes) {
    Assert-Path $change.Target $scratch
    if ($change.Added) {
        if (Test-Path -LiteralPath $change.Target) { $freed+=(Get-Item -LiteralPath $change.Target).Length; Remove-Item -LiteralPath $change.Target -Force }
    } else {
        # Cleanup can safely resume after an interrupted duplicate-backup removal.
        if ((Test-Path -LiteralPath $change.Target) -and (Get-FileHash -LiteralPath $change.Target).Hash -eq $change.BeforeSHA256) { continue }
        Assert-Path $change.Backup (Join-Path $output 'OriginalScratch')
        if ((Get-FileHash -LiteralPath $change.Backup).Hash -ne $change.BeforeSHA256) { throw ('Backup hash changed: '+$change.Backup) }
        $restoreParent = Split-Path $change.Target -Parent
        Assert-Path $restoreParent $scratch
        New-Item -ItemType Directory -Path $restoreParent -Force | Out-Null
        Copy-Item -LiteralPath $change.Backup -Destination $change.Target -Force
        if ((Get-FileHash -LiteralPath $change.Target).Hash -ne $change.BeforeSHA256) { throw ('Restore verification failed: '+$change.Target) }
    }
}
foreach ($directory in @($removedDirectories) | Sort-Object Length) {
    Assert-Path $directory $scratch
    if (!(Test-Path -LiteralPath $directory)) { New-Item -ItemType Directory -Path $directory -Force | Out-Null }
}
foreach ($directory in @($addedDirectories) | Sort-Object Length -Descending) {
    Assert-Path $directory $scratch
    if ((Test-Path -LiteralPath $directory) -and @(Get-ChildItem -LiteralPath $directory -Force).Count -eq 0) { Remove-Item -LiteralPath $directory -Force }
}
$restoreDiff = @(Compare-Manifest (Import-Csv -LiteralPath $originalPath) @(Get-Manifest $scratch))
Write-Json (Join-Path $output 'restore-check.json') ([pscustomobject]@{Differences=$restoreDiff;CheckedUtc=[DateTime]::UtcNow.ToString('o')})
if ($restoreDiff.Count) { throw 'Borrowed inputs are not fully restored; preserve backups and lease for diagnosis.' }
Assert-Idle
$temp = Join-Path $scratch 'Temp'
if ((Test-Path -LiteralPath $temp) -and !$preparation.HadTemp) {
    Assert-Path $temp $scratch
    $items = @(Get-Tree $temp)
    $freed += ($items | Where-Object { !$_.PSIsContainer } | Measure-Object Length -Sum).Sum
    Remove-Item -LiteralPath $temp -Recurse -Force
    if (Test-Path -LiteralPath $temp) { throw 'Task Temp was not removed.' }
}
$backupRoot = Join-Path $output 'OriginalScratch'
if (Test-Path -LiteralPath $backupRoot) {
    Assert-Path $backupRoot $output
    $items = @(Get-Tree $backupRoot)
    $freed += ($items | Where-Object { !$_.PSIsContainer } | Measure-Object Length -Sum).Sum
    Remove-Item -LiteralPath $backupRoot -Recurse -Force
    if (Test-Path -LiteralPath $backupRoot) { throw 'Verified duplicate restore backups were not removed.' }
}
$mainDiff = @(Compare-Manifest $mainBefore @(Get-Manifest $main))
Write-Json (Join-Path $output 'cleanup-verification.json') ([pscustomobject]@{Result=if($mainDiff.Count){'INPUT_DRIFT'}else{'PASS'};MainDifferencesDuringCleanup=$mainDiff;BorrowedInputsRestored=$true;ReclaimedBytes=$freed;SharedLibraryRetained=$true;PreExistingTempRetained=$preparation.HadTemp;CheckedUtc=[DateTime]::UtcNow.ToString('o')})
if ($mainDiff.Count) { throw 'Main inputs changed during cleanup; inspect evidence before removing the task lease. Cleanup never writes to UnityClient.' }
Remove-Item -LiteralPath $lease -Force
if (Test-Path -LiteralPath $lease) { throw 'Task lease was not removed.' }
Write-Output ('Cleanup complete. Borrowed inputs restored; reclaimed '+$freed+' bytes. Shared warm Library retained for existing validation tools; UnityClient was never written.')
