param(
    [string]$Output = '',
    [string]$UnityData = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data',
    [ValidateRange(5,60)][int]$ProcessTimeoutSeconds = 30
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$auditWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$auditAllowedRoot = [IO.Path]::GetFullPath((Join-Path $auditWorkspace 'Artifacts/SequentialOptimization20261008/02-Stability/CheckpointCrashAudit'))
if (-not $Output) { $Output = Join-Path $auditAllowedRoot ('Run-' + [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfff') + '-' + [Guid]::NewGuid().ToString('N').Substring(0,8)) }
$auditOutput = [IO.Path]::GetFullPath($Output)
if (-not $auditOutput.StartsWith($auditAllowedRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Output must be a new child directory of $auditAllowedRoot"
}
if (Test-Path -LiteralPath $auditOutput) { throw "Refusing existing output: $auditOutput" }
# Reject reparse-point ancestors rather than following a junction outside the audit root.
$auditAncestor = $auditOutput
while ($auditAncestor) {
    if ((Test-Path -LiteralPath $auditAncestor) -and ((Get-Item -LiteralPath $auditAncestor).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw "Reparse-point output ancestor is not allowed: $auditAncestor"
    }
    $auditAncestor = [IO.Path]::GetDirectoryName($auditAncestor)
}
$auditSource = Join-Path $auditWorkspace 'UnityClient/Assets/_Project/Persistence/Runtime'
$auditInputs = @('ProtectedCheckpointStore.cs','SaveFormat.cs')
$auditHashes = @($auditInputs | ForEach-Object {
    $path = Join-Path $auditSource $_
    [pscustomobject]@{ name=$_; source=$path; sha256=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash }
})
$auditOriginal = [IO.File]::ReadAllText((Join-Path $auditSource 'ProtectedCheckpointStore.cs')).Replace("`r`n", "`n")
$auditAnchors = @(
    @{ name='after_temp_flush'; before="                    stream.Flush(true);`n                }`n                // Verify the complete bytes"; after="                    stream.Flush(true);`n                    global::CheckpointAuditStop.Reach(`"after_temp_flush`", destination, temporary);`n                }`n                // Verify the complete bytes" },
    @{ name='after_primary_commit'; before='                else File.Move(temporary, destination);'; after="                else File.Move(temporary, destination);`n                global::CheckpointAuditStop.Reach(`"after_primary_commit`", destination, temporary);" },
    @{ name='after_clear_commit'; before='                        WriteAtomic(ClearedPath, Encode(string.Empty, "cleared"));'; after="                        WriteAtomic(ClearedPath, Encode(string.Empty, `"cleared`"));`n                        global::CheckpointAuditStop.Reach(`"after_clear_commit`", ClearedPath, null);" },
    @{ name='before_clear_remove'; before='                            File.Delete(ClearedPath);'; after="                            global::CheckpointAuditStop.Reach(`"before_clear_remove`", ClearedPath, null);`n                            File.Delete(ClearedPath);" }
)
$auditInstrumented = $auditOriginal
foreach ($anchor in $auditAnchors) {
    $count = [regex]::Matches($auditInstrumented, [regex]::Escape($anchor.before)).Count
    if ($count -ne 1) { throw "Source anchor '$($anchor.name)' matched $count times; expected exactly one. No audit generated." }
    $auditInstrumented = $auditInstrumented.Replace($anchor.before,$anchor.after)
}
$auditCompiler = Join-Path $UnityData 'DotNetSdkRoslyn/csc.dll'
$auditDotnet = Join-Path $UnityData 'NetCoreRuntime/dotnet.exe'
$auditMono = Join-Path $UnityData 'MonoBleedingEdge/bin/mono.exe'
$auditFramework = Join-Path $UnityData 'MonoBleedingEdge/lib/mono/4.7.2-api'
foreach ($path in @($auditCompiler,$auditDotnet,$auditMono,(Join-Path $auditFramework 'System.Runtime.Serialization.dll'))) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing compiler/runtime dependency: $path" }
}
New-Item -ItemType Directory -Path $auditOutput | Out-Null
$auditUtf8 = [Text.UTF8Encoding]::new($false)
function Write-AuditJson([string]$Path, $Value) {
    if (Test-Path -LiteralPath $Path) { throw "Refusing to overwrite evidence: $Path" }
    [IO.File]::WriteAllText($Path, ($Value | ConvertTo-Json -Depth 12), $auditUtf8)
}
$auditOriginalDir = Join-Path $auditOutput 'Source/Original'
$auditInstrumentedDir = Join-Path $auditOutput 'Source/Instrumented'
$auditBuild = Join-Path $auditOutput 'Build'
New-Item -ItemType Directory -Path $auditOriginalDir,$auditInstrumentedDir,$auditBuild | Out-Null
foreach ($input in $auditHashes) {
    Copy-Item -LiteralPath $input.source -Destination (Join-Path $auditOriginalDir $input.name)
    if ((Get-FileHash -LiteralPath (Join-Path $auditOriginalDir $input.name)).Hash -ne $input.sha256) { throw 'Source changed during snapshot.' }
}
if ([IO.File]::ReadAllText((Join-Path $auditOriginalDir 'ProtectedCheckpointStore.cs')).Replace("`r`n", "`n") -cne $auditOriginal) {
    throw 'The source used for instrumentation differs from the verified original snapshot.'
}
[IO.File]::WriteAllText((Join-Path $auditInstrumentedDir 'ProtectedCheckpointStore.cs'),$auditInstrumented,$auditUtf8)
Copy-Item -LiteralPath (Join-Path $auditOriginalDir 'SaveFormat.cs') -Destination (Join-Path $auditInstrumentedDir 'SaveFormat.cs')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Worker.cs') -Destination (Join-Path $auditBuild 'Worker.cs')
Copy-Item -LiteralPath $PSCommandPath -Destination (Join-Path $auditBuild 'Run.ps1')
Write-AuditJson (Join-Path $auditOutput 'source-provenance.json') ([ordered]@{
    utc=[DateTime]::UtcNow.ToString('O'); originals=$auditHashes
    anchors=@($auditAnchors | ForEach-Object { [ordered]@{ name=$_.name; expectedMatches=1; before=$_.before; after=$_.after } })
    scope='Windows process termination at deterministic store commit boundaries. No Unity, no production edits, no power-loss claim, no PlayerPrefs/SaveProgress transaction claim.'
})
foreach ($variant in @('Original','Instrumented')) {
    $auditExe = Join-Path $auditBuild ($variant + '.exe')
    $arguments = @($auditCompiler,'-nologo','-target:exe','-langversion:9','-noconfig','-nostdlib',"-out:$auditExe")
    foreach ($assembly in @('mscorlib.dll','System.dll','System.Core.dll','System.Runtime.Serialization.dll','System.Xml.dll')) {
        $arguments += '-r:' + (Join-Path $auditFramework $assembly)
    }
    $arguments += @((Join-Path $auditOutput ('Source/' + $variant + '/ProtectedCheckpointStore.cs')),
        (Join-Path $auditOutput ('Source/' + $variant + '/SaveFormat.cs')),(Join-Path $auditBuild 'Worker.cs'))
    & $auditDotnet @arguments 2>&1 | Tee-Object -FilePath (Join-Path $auditBuild ($variant + '-compile.log'))
    if ($LASTEXITCODE -ne 0) { throw "$variant compilation failed." }
}

# Start-Process joins ArgumentList on Windows. Quote each value using the native
# backslash-before-quote convention, without invoking cmd.exe or another shell.
function Quote-AuditArgument([string]$Value) {
    '"' + [regex]::Replace([regex]::Replace($Value, '(\\*)"', '$1$1\"'), '(\\+)$', '$1$1') + '"'
}
function Stop-OwnedAuditProcess($Owned) {
    $Owned.process.Refresh()
    if ($Owned.process.HasExited) { return $false }
    if ($Owned.process.Id -ne $Owned.pid -or $Owned.process.StartTime.ToUniversalTime().Ticks -ne $Owned.startTicks) {
        throw 'Refusing to stop a process with a different identity.'
    }
    # The Process handle was opened immediately after our own Start-Process.
    # Kill targets only that handle, never a process-name search or process tree.
    $Owned.process.Kill()
    if (-not $Owned.process.WaitForExit(10000)) { throw 'Owned child did not terminate within ten seconds.' }
    return $true
}
function Invoke-AuditChild([string]$Directory,[string]$Scenario,[string]$Token,[string]$Label,
    [string]$Operation,[int]$Generation,[string]$StopPoint='',[string]$Variant='Original') {
    $requestPath = Join-Path $Directory ($Label + '-request.json')
    $resultPath = Join-Path $Directory ($Label + '-result.json')
    $handshakePath = Join-Path $Directory ($Label + '-handshake.json')
    Write-AuditJson $requestPath ([ordered]@{token=$Token;scenario=$Scenario;directory=$Directory;
        operation=$Operation;generation=$Generation;stopPoint=$StopPoint;handshakePath=$handshakePath;
        resultPath=$resultPath;holdTimeoutMs=($ProcessTimeoutSeconds + 20)*1000})
    $exe = Join-Path $auditBuild ($Variant + '.exe')
    $argumentLine = (@($exe,$requestPath,$Token) | ForEach-Object { Quote-AuditArgument $_ }) -join ' '
    $process = Start-Process -FilePath $auditMono -ArgumentList $argumentLine -WorkingDirectory $Directory -WindowStyle Hidden -PassThru `
        -RedirectStandardOutput (Join-Path $Directory ($Label + '-stdout.log')) -RedirectStandardError (Join-Path $Directory ($Label + '-stderr.log'))
    $owned = [pscustomobject]@{process=$process;pid=$process.Id;startTicks=$process.StartTime.ToUniversalTime().Ticks;handle=$process.Handle}
    Write-AuditJson (Join-Path $Directory ($Label + '-process.json')) ([ordered]@{pid=$owned.pid;startTicks=$owned.startTicks;token=$Token;
        executable=$auditMono;managedAssembly=$exe;workingDirectory=$Directory;argumentLine=$argumentLine})
    try {
        if ($StopPoint) {
            $timer = [Diagnostics.Stopwatch]::StartNew()
            while (-not (Test-Path -LiteralPath $handshakePath)) {
                $process.Refresh()
                if ($process.HasExited) { throw "Worker exited before handshake: $Label, exit $($process.ExitCode)" }
                if ($timer.Elapsed.TotalSeconds -ge $ProcessTimeoutSeconds) { throw "Handshake timed out: $Label" }
                Start-Sleep -Milliseconds 30
            }
            $handshake = Get-Content -LiteralPath $handshakePath -Raw | ConvertFrom-Json
            if ($handshake.pid -ne $owned.pid -or $handshake.token -ne $Token -or $handshake.scenario -ne $Scenario -or
                $handshake.point -ne $StopPoint -or $handshake.directory -ne $Directory -or
                $handshake.processStartTicks -ne $owned.startTicks) { throw "Handshake identity mismatch: $Label" }
            $expectedDestination = Join-Path $Directory 'data/voyage.checkpoint'
            if ($StopPoint -eq 'after_clear_commit' -or $StopPoint -eq 'before_clear_remove') { $expectedDestination += '.cleared' }
            if ([IO.Path]::GetFullPath($handshake.destination) -ne [IO.Path]::GetFullPath($expectedDestination)) {
                throw "Handshake commit destination mismatch: $Label"
            }
            $killed = Stop-OwnedAuditProcess $owned
            if (-not $killed -or (Test-Path -LiteralPath $resultPath)) { throw 'The worker completed or exited before the intended forced termination.' }
            Write-AuditJson (Join-Path $Directory ($Label + '-termination.json')) ([ordered]@{forced=$true;pid=$owned.pid;startTicks=$owned.startTicks;
                point=$StopPoint;utc=[DateTime]::UtcNow.ToString('O');exitCode=$process.ExitCode;method='Retained handle from own Start-Process; Process.Kill'})
            return $handshake
        }
        if (-not $process.WaitForExit($ProcessTimeoutSeconds*1000)) { throw "Child timed out: $Label" }
        if ($process.ExitCode -ne 0) { throw "Child failed: $Label, exit $($process.ExitCode); see stderr log." }
        if (-not (Test-Path -LiteralPath $resultPath)) { throw "Missing child result: $Label" }
        $result = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
        if (-not $result.success -or $result.pid -ne $owned.pid -or $result.token -ne $Token -or $result.scenario -ne $Scenario) {
            throw "Invalid child result: $Label"
        }
        return $result
    }
    finally {
        try { [void](Stop-OwnedAuditProcess $owned) } finally { $process.Dispose() }
    }
}

$auditCases = @(
    @{name='save-temp-flush';operation='save';point='after_temp_flush';expected=3;cleared=$false},
    @{name='save-primary-commit';operation='save';point='after_primary_commit';expected=4;cleared=$false},
    @{name='clear-marker-commit';operation='clear';point='after_clear_commit';expected=0;cleared=$false},
    @{name='new-save-after-clear-temp-flush';operation='save';point='after_temp_flush';expected=0;cleared=$true},
    @{name='new-save-after-clear-primary-commit';operation='save';point='after_primary_commit';expected=0;cleared=$true},
    @{name='new-save-before-clear-removal';operation='save';point='before_clear_remove';expected=0;cleared=$true}
)
$auditResults = [Collections.Generic.List[object]]::new()
try {
    foreach ($case in $auditCases) {
        $directory = Join-Path $auditOutput $case.name
        New-Item -ItemType Directory -Path $directory | Out-Null
        $token = [Guid]::NewGuid().ToString('N')
        [IO.File]::WriteAllText((Join-Path $directory 'owner.txt'),$token+'|'+$case.name,$auditUtf8)
        [void](Invoke-AuditChild $directory $case.name $token '01-seed' 'seed' 3)
        [void](Invoke-AuditChild $directory $case.name $token '02-baseline-read' 'read' 3)
        if ($case.cleared) { [void](Invoke-AuditChild $directory $case.name $token '03-clear' 'clear' 0) }
        $handshake = Invoke-AuditChild $directory $case.name $token '04-crash' $case.operation 4 $case.point 'Instrumented'
        if (-not $handshake.primaryExists -and $case.name -ne 'new-save-after-clear-temp-flush') { throw 'Expected a committed primary at the selected stop.' }
        if (($case.cleared -or $case.operation -eq 'clear') -and -not $handshake.clearedExists) { throw 'Clear marker missing at stop.' }
        $data = Join-Path $directory 'data'
        $snapshot = Join-Path $directory 'AfterKill'
        New-Item -ItemType Directory -Path $snapshot | Out-Null
        $inventory = @(Get-ChildItem -LiteralPath $data -File | ForEach-Object {
            Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $snapshot $_.Name)
            [pscustomobject]@{name=$_.Name;bytes=$_.Length;sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}
        })
        Write-AuditJson (Join-Path $directory '05-post-kill-files.json') $inventory
        $read = Invoke-AuditChild $directory $case.name $token '06-restart-read' 'read' $case.expected
        # The uninstrumented production class must also recover write access after
        # the killed lock holder and commit a wholly new generation on retry.
        [void](Invoke-AuditChild $directory $case.name $token '07-retry-save' 'save' 5)
        $retry = Invoke-AuditChild $directory $case.name $token '08-retry-read' 'read' 5
        $auditResults.Add([ordered]@{scenario=$case.name;point=$case.point;passed=$true;forcedPid=$handshake.pid;
            expectedGeneration=$case.expected;restartRead=$read;retryRead=$retry})
        Write-Output ("PASS " + $case.name + ": original-class reader saw " + $(if($case.expected -eq 0){'no active checkpoint'}else{'complete generation '+$case.expected}))
    }
}
catch {
    Write-AuditJson (Join-Path $auditOutput 'failure.json') ([ordered]@{utc=[DateTime]::UtcNow.ToString('O');message=$_.Exception.ToString();completed=$auditResults.ToArray()})
    throw
}
finally {
    $after = @($auditHashes | ForEach-Object {
        $hash = (Get-FileHash -LiteralPath $_.source -Algorithm SHA256).Hash
        [pscustomobject]@{name=$_.name;before=$_.sha256;after=$hash;unchanged=($hash -eq $_.sha256)}
    })
    Write-AuditJson (Join-Path $auditOutput 'source-after.json') $after
}
if (@($after | Where-Object { -not $_.unchanged }).Count -gt 0) { throw 'Production source changed during the audit; result is not valid for a single source snapshot.' }
Write-AuditJson (Join-Path $auditOutput 'summary.json') ([ordered]@{passed=$auditResults.Count;failed=0;utc=[DateTime]::UtcNow.ToString('O');
    scenarios=$auditResults.ToArray();productionUnchanged=$true;reader='Original.exe compiled from byte-identical production source copies';
    limitation='Process termination, not machine power failure. Does not validate Unity SaveProgress, PlayerPrefs, game payload semantics, or packaged game input.'})
Write-AuditJson (Join-Path $auditOutput 'file-hashes.json') @(Get-ChildItem -LiteralPath (Join-Path $auditOutput 'Source'),$auditBuild -File -Recurse | ForEach-Object {
    [pscustomobject]@{file=$_.FullName.Substring($auditOutput.Length+1);sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}
})
Write-Output "Checkpoint crash audit: $($auditResults.Count) passed. Evidence: $auditOutput"
