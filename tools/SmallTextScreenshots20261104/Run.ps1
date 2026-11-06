param([ValidateSet('Prepare','Capture','Status','Cleanup')][string]$Stage='Prepare', [string]$Run='run1', [string]$Only='')
$ErrorActionPreference='Stop'
$workspace='D:\Game'
$main=Join-Path $workspace 'UnityClient'
$scratch=Join-Path $workspace 'tmp\component-rarity-halo-20261019'
$output=Join-Path $workspace 'outputs\small-text-screenshots-20261104'
$diagnostics=Join-Path $output 'diagnostics'
$journal=Join-Path $diagnostics 'scratch-journal.json'
$lease=Join-Path $scratch 'SmallTextScreenshots20261104.lock'
function Assert-Idle {
    if(@(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object {$_.CommandLine -and $_.CommandLine.Replace('/','\').Contains($scratch)}).Count){throw 'Shared screenshot project is running.'}
    if(Test-Path -LiteralPath (Join-Path $scratch 'Temp\UnityLockfile')){throw 'Shared project has a Unity lockfile.'}
}
function Assert-Safe([string]$path) {
    $full=[IO.Path]::GetFullPath($path)
    if(!$full.StartsWith($scratch+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Scratch path escaped.'}
    for($at=$full;$at;$at=Split-Path $at -Parent){if((Test-Path -LiteralPath $at) -and ((Get-Item -LiteralPath $at -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)){throw ('Linked path: '+$at)}}
}
function Save-Journal { ConvertTo-Json -InputObject @($changes.ToArray()) -Depth 4 | Set-Content -LiteralPath $journal -Encoding utf8 }
function Record-Target([string]$target) {
    Assert-Safe $target
    if(@($changes | Where-Object Target -eq $target).Count){return}
    $backup=$null
    if(Test-Path -LiteralPath $target){
        $backup=Join-Path $diagnostics ('OriginalScratch\'+$target.Substring($scratch.Length+1))
        New-Item -ItemType Directory -Path (Split-Path $backup -Parent) -Force | Out-Null
        Copy-Item -LiteralPath $target -Destination $backup
    }
    $changes.Add([pscustomobject]@{Target=$target;Backup=$backup;Added=($null -eq $backup)})
    Save-Journal
}
if($Stage -eq 'Status') {
    Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object {$_.CommandLine -and $_.CommandLine.Replace('/','\').Contains($scratch)} | Select-Object ProcessId,CommandLine
    $log=Join-Path $output ($Run+'\unity.log'); if(Test-Path -LiteralPath $log){Get-Content -LiteralPath $log -Tail 20}
    exit
}
Assert-Idle
Assert-Safe (Join-Path $scratch 'Assets')
$settings=Get-Content -LiteralPath (Join-Path $scratch 'ProjectSettings\ProjectSettings.asset') -Raw
if($settings -notmatch 'companyName: CodexCardSupplyValidation' -or $settings -notmatch 'productName: CardSupplyFrames20261021'){throw 'Isolated profile identity required.'}
$changes=[Collections.Generic.List[object]]::new()
if($Stage -eq 'Prepare') {
    if(@(Get-ChildItem -LiteralPath $scratch -Filter '*.lock' -File).Count){throw 'Another task has a lease.'}
    if(Test-Path -LiteralPath $journal){throw 'Preserve existing task journal.'}
    New-Item -ItemType Directory -Path $diagnostics -Force | Out-Null
    $lock=[IO.File]::Open($lease,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
    try{$data=[Text.Encoding]::UTF8.GetBytes($output);$lock.Write($data,0,$data.Length)}finally{$lock.Dispose()}
    Save-Journal
    [pscustomobject]@{Project=$scratch;HadTemp=(Test-Path -LiteralPath (Join-Path $scratch 'Temp'));StartedAtUtc=[DateTime]::UtcNow.ToString('O')} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $diagnostics 'preparation.json') -Encoding utf8
    $inputs=[Collections.Generic.List[object]]::new()
    foreach($part in @('Assets','Packages')) {
        foreach($file in Get-ChildItem -LiteralPath (Join-Path $main $part) -File -Recurse) {
            if($file.FullName -like '*\UIPreviewsForMe*'){continue}
            if($file.Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Linked source.'}
            $relative=$file.FullName.Substring($main.Length+1)
            $target=Join-Path $scratch $relative; Assert-Safe $target
            $hash=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
            $inputs.Add([pscustomobject]@{Path=$relative;SHA256=$hash;Length=$file.Length})
            if((Test-Path -LiteralPath $target) -and (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -eq $hash){continue}
            Record-Target $target
            if(!(Test-Path -LiteralPath ($target+'.meta')) -and $file.Extension -ne '.meta'){Record-Target ($target+'.meta')}
            New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force | Out-Null
            Copy-Item -LiteralPath $file.FullName -Destination $target -Force
        }
    }
    foreach($file in Get-ChildItem -LiteralPath (Join-Path $scratch 'Assets') -Filter '*.cs' -File -Recurse) {
        if($file.FullName -like '*\UIPreviewsForMe*'){continue}
        if(Test-Path -LiteralPath (Join-Path $main $file.FullName.Substring($scratch.Length+1))){continue}
        Record-Target $file.FullName
        Remove-Item -LiteralPath $file.FullName -Force
    }
    $inputs | Export-Csv -LiteralPath (Join-Path $diagnostics 'source-manifest.csv') -NoTypeInformation -Encoding utf8
    Write-Output ('Prepared '+$changes.Count+' changes; shared Library reused, no main-project writes.')
    exit
}
if(!(Test-Path -LiteralPath $lease) -or [IO.File]::ReadAllText($lease) -ne $output){throw 'Task lease missing or mismatched.'}
foreach($entry in @(Get-Content -LiteralPath $journal -Raw | ConvertFrom-Json)){$changes.Add($entry)}
if($Stage -eq 'Capture') {
    if($Run -notmatch '^[A-Za-z0-9_-]+$'){throw 'Simple run name required.'}
    $runOutput=Join-Path $output $Run
    if(Test-Path -LiteralPath $runOutput){throw 'Choose a fresh capture run.'}
    New-Item -ItemType Directory -Path $runOutput | Out-Null
    foreach($file in Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs' -File) {
        $target=Join-Path $scratch ('Assets\_Project\Editor\SmallText'+$file.Name)
        Record-Target $target; Record-Target ($target+'.meta')
        Copy-Item -LiteralPath $file.FullName -Destination $target -Force
    }
    $arguments=@('-batchmode','-force-d3d11','-projectPath',$scratch,'-executeMethod','ShatteredSail.Editor.SmallTextCapture.Run','-smallTextOutput',$runOutput,'-logFile',(Join-Path $runOutput 'unity.log'),'-quit')
    if($Only){$arguments+=@('-smallTextOnly',$Only)}
    $process=Start-Process -FilePath 'D:\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe' -ArgumentList ($arguments|ForEach-Object {'"'+$_+'"'}) -WindowStyle Hidden -PassThru
    [pscustomobject]@{PID=$process.Id;Project=$scratch;Output=$runOutput;Arguments=$arguments} | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $runOutput 'launch.json') -Encoding utf8
    Write-Output ('Capture started: '+$process.Id+'; '+$runOutput)
    exit
}
$before=@(Import-Csv -LiteralPath (Join-Path $diagnostics 'source-manifest.csv') | Where-Object {$_.Path -match '\.(cs|asmdef|asmref)$'} | ForEach-Object {$path=Join-Path $main $_.Path;[pscustomobject]@{Path=$path;SHA256=(Get-FileHash -LiteralPath $path).Hash}})
$freed=0L
foreach($entry in $changes) {
    Assert-Safe $entry.Target
    if($entry.Added){if(Test-Path -LiteralPath $entry.Target){$freed+=(Get-Item -LiteralPath $entry.Target).Length;Remove-Item -LiteralPath $entry.Target -Force}}
    else{Copy-Item -LiteralPath $entry.Backup -Destination $entry.Target -Force;if((Get-FileHash -LiteralPath $entry.Backup).Hash -ne (Get-FileHash -LiteralPath $entry.Target).Hash){throw 'Scratch restore mismatch.'}}
}
$prep=Get-Content -LiteralPath (Join-Path $diagnostics 'preparation.json') -Raw | ConvertFrom-Json
$cleanupPaths=@((Join-Path $diagnostics 'OriginalScratch'))
if(!$prep.HadTemp){$cleanupPaths+=(Join-Path $scratch 'Temp')}
foreach($path in $cleanupPaths) {
    if(!(Test-Path -LiteralPath $path)){continue}
    $resolved=[IO.Path]::GetFullPath($path)
    if($resolved -ne (Join-Path $diagnostics 'OriginalScratch') -and $resolved -ne (Join-Path $scratch 'Temp')){throw 'Unsafe cleanup.'}
    for($at=$resolved;$at;$at=Split-Path $at -Parent){if((Get-Item -LiteralPath $at -Force).Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Linked cleanup ancestor.'}}
    $items=@(Get-ChildItem -LiteralPath $resolved -Recurse -Force)
    if(@($items|Where-Object {$_.Attributes -band [IO.FileAttributes]::ReparsePoint}).Count){throw 'Linked cleanup content.'}
    if(@(Get-CimInstance Win32_Process | Where-Object {$_.ProcessId -ne $PID -and $_.CommandLine -and $_.CommandLine.Contains($resolved)}).Count){throw 'Cleanup target still in use.'}
    $freed+=($items|Where-Object {!$_.PSIsContainer}|Measure-Object Length -Sum).Sum
    Remove-Item -LiteralPath $resolved -Recurse -Force
    if(Test-Path -LiteralPath $resolved){throw 'Cleanup incomplete.'}
}
$mainChanges=@($before|Where-Object {(Get-FileHash -LiteralPath $_.Path).Hash -ne $_.SHA256}).Count
Remove-Item -LiteralPath $lease -Force
[pscustomobject]@{ReclaimedBytes=$freed;MainSourceChangesDuringCleanup=$mainChanges;SharedLibraryRetained='ComponentRarityHalo/CardSupplyFrames and other existing validation tools';PreexistingTempRetained=$prep.HadTemp;ScratchRestored=$true} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $diagnostics 'cleanup.json') -Encoding utf8
Write-Output ('Restored scratch; reclaimed '+$freed+' bytes. Shared Library retained.')
