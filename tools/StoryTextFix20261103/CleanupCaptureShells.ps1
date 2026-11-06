param([Parameter(Mandatory=$true)][string[]]$RunIds)
$ErrorActionPreference='Stop'
$storyWorkspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$storyTmp=Join-Path $storyWorkspace 'tmp'
$storyEvidence=Join-Path $storyWorkspace 'outputs/card-game-view-20261030/runs'
$storySummaryPath=Join-Path $storyWorkspace 'outputs/story-text-fix-20261103/capture-shell-cleanup.json'
$storyRows=@()
if(Test-Path -LiteralPath $storySummaryPath){$storyRows=@(Get-Content -LiteralPath $storySummaryPath -Raw | ConvertFrom-Json)}
foreach($storyRun in $RunIds){
    if($storyRun -notmatch '^[a-f0-9]{32}$'){throw 'Invalid task capture run ID.'}
    $storyTarget=[IO.Path]::GetFullPath((Join-Path $storyTmp ('card-game-view-20261030-'+$storyRun)))
    $storyArchive=Join-Path $storyEvidence ($storyRun+'/retained-shell')
    if([IO.Path]::GetDirectoryName($storyTarget) -ne $storyTmp){throw 'Cleanup escaped the task temporary root.'}
    if(!(Test-Path -LiteralPath $storyTarget)){continue}
    if(!(Test-Path -LiteralPath (Join-Path $storyTarget 'OWNED_CARD_GAME_VIEW.txt'))){throw 'Task ownership marker missing.'}
    $storyRunCleanup=Get-Content -LiteralPath (Join-Path $storyEvidence ($storyRun+'/cleanup.json')) -Raw | ConvertFrom-Json
    if(Test-Path -LiteralPath (Join-Path $storyTarget 'Library')){throw 'Capture still has a Library; let the capture exporter clean its own cache first.'}
    for($storyAt=$storyTarget;$storyAt;$storyAt=Split-Path $storyAt -Parent){if((Get-Item -LiteralPath $storyAt -Force).Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Linked cleanup ancestor.'}}
    $storyConsumers=@(Get-CimInstance Win32_Process | Where-Object {$_.ProcessId -ne $PID -and $_.CommandLine -and $_.CommandLine.IndexOf($storyTarget,[StringComparison]::OrdinalIgnoreCase) -ge 0})
    if($storyConsumers.Count){throw 'Capture shell still referenced by a process.'}
    $storyFiles=@();$storyDirectories=@();$storyPending=[Collections.Generic.Stack[string]]::new();$storyPending.Push($storyTarget)
    while($storyPending.Count){
        $storyDirectory=$storyPending.Pop();$storyDirectories+=$storyDirectory
        foreach($storyItem in Get-ChildItem -LiteralPath $storyDirectory -Force){
            if($storyItem.Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Linked capture entry.'}
            if($storyItem.PSIsContainer){$storyPending.Push($storyItem.FullName)}else{$storyFiles+=$storyItem}
        }
    }
    $storyBytes=0L;$storyManifest=@()
    foreach($storyFile in $storyFiles){
        $storyRelative=$storyFile.FullName.Substring($storyTarget.Length+1)
        $storyDestination=Join-Path $storyArchive $storyRelative
        New-Item -ItemType Directory -Force -Path (Split-Path $storyDestination -Parent) | Out-Null
        $storyHash=(Get-FileHash -LiteralPath $storyFile.FullName).Hash
        if(Test-Path -LiteralPath $storyDestination){if((Get-FileHash -LiteralPath $storyDestination).Hash -ne $storyHash){throw 'Existing shell archive differs.'}}
        else{Copy-Item -LiteralPath $storyFile.FullName -Destination $storyDestination}
        if((Get-FileHash -LiteralPath $storyDestination).Hash -ne $storyHash){throw 'Archived input differs.'}
        $storyManifest+=[pscustomobject]@{Relative=$storyRelative;SHA256=$storyHash;Bytes=$storyFile.Length}
        $storyBytes+=$storyFile.Length
    }
    $storyManifest | Export-Csv -LiteralPath (Join-Path $storyEvidence ($storyRun+'/retained-shell-hashes.csv')) -NoTypeInformation -Encoding UTF8
    foreach($storyFile in $storyFiles){
        $storyExpected=$storyManifest | Where-Object Relative -eq $storyFile.FullName.Substring($storyTarget.Length+1)
        if((Get-FileHash -LiteralPath $storyFile.FullName).Hash -ne $storyExpected.SHA256){throw 'Shell changed after archiving.'}
        $storyProbe=[IO.File]::Open($storyFile.FullName,[IO.FileMode]::Open,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None);$storyProbe.Dispose()
        Remove-Item -LiteralPath $storyFile.FullName -Force
    }
    foreach($storyDirectory in @($storyDirectories | Sort-Object Length -Descending)){[IO.Directory]::Delete($storyDirectory,$false)}
    if(Test-Path -LiteralPath $storyTarget){throw 'Capture shell remains.'}
    $storyRows+=[pscustomobject]@{Run=$storyRun;Removed=$storyTarget;ArchivedFiles=$storyFiles.Count;ArchivedBytes=$storyBytes;NetReleasedBytes=0;CaptureCacheAlreadyReleasedBytes=$storyRunCleanup.ReleasedBytes}
}
$storyRows | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $storySummaryPath -Encoding UTF8
$storyRows | Select-Object Run,ArchivedFiles,ArchivedBytes,NetReleasedBytes
