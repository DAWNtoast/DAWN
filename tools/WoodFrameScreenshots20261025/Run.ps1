param([ValidateSet('Prepare','Capture','Cleanup')][string]$Stage='Prepare',[string]$Run='run1',[string]$Only='')
$ErrorActionPreference='Stop'
$workspace='D:\Game'
$main=Join-Path $workspace 'UnityClient'
$scratch=Join-Path $workspace 'tmp\shipyard-assembly-20261017'
$output=Join-Path $workspace 'outputs\wood-frame-screenshots-20261025'
$journal=Join-Path $output 'diagnostics\scratch-journal.json'
$lease=Join-Path $scratch 'WoodFrameScreenshots20261025.lock'
function Assert-Idle {
    $running=@(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object {$_.CommandLine -and $_.CommandLine.Replace('/','\').Contains($scratch)})
    if($running.Count){throw 'Shared screenshot project is running.'}
    if(Test-Path -LiteralPath (Join-Path $scratch 'Temp\UnityLockfile')){throw 'Shared screenshot project has a Unity lockfile.'}
}
function Assert-Safe([string]$path){
    $full=[IO.Path]::GetFullPath($path)
    if(!$full.StartsWith($scratch+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Scratch path escaped.'}
    $at=$full
    while($at -and $at.Length -ge $scratch.Length){
        if((Test-Path -LiteralPath $at) -and ((Get-Item -LiteralPath $at -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)){throw ('Linked scratch path: '+$at)}
        $at=Split-Path $at -Parent
    }
}
Assert-Idle
Assert-Safe (Join-Path $scratch 'Assets')
Assert-Safe (Join-Path $scratch 'Library')
$settings=[IO.File]::ReadAllText((Join-Path $scratch 'ProjectSettings\ProjectSettings.asset'))
if($settings -notmatch 'companyName: CodexShipyardAssemblyValidation' -or $settings -notmatch 'productName: ShipyardAssemblyValidation20261017'){throw 'Shared project must use its isolated player profile.'}
if($Stage -eq 'Prepare'){
    if(Test-Path -LiteralPath $lease){throw 'Screenshot lease already exists.'}
    if(Test-Path -LiteralPath $journal){throw 'Preserve the existing screenshot journal.'}
    New-Item -ItemType Directory -Path (Join-Path $output 'diagnostics') -Force | Out-Null
    [IO.File]::WriteAllText($lease,$output)
    $changes=[Collections.Generic.List[object]]::new()
    $inputs=[Collections.Generic.List[object]]::new()
    foreach($part in @('Assets','Packages')){
        foreach($file in Get-ChildItem -LiteralPath (Join-Path $main $part) -File -Recurse){
            if($file.FullName -like '*\UIPreviewsForMe*'){continue}
            $relative=$file.FullName.Substring($main.Length+1)
            $target=Join-Path $scratch $relative
            Assert-Safe $target
            $sourceHash=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
            $inputs.Add([pscustomobject]@{Path=$relative;SHA256=$sourceHash;Length=$file.Length})
            $exists=Test-Path -LiteralPath $target
            if($exists -and (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -eq $sourceHash){continue}
            $backup=$null
            if($exists){
                $backup=Join-Path $output ('diagnostics\OriginalScratch\'+$relative)
                New-Item -ItemType Directory -Path (Split-Path $backup -Parent) -Force | Out-Null
                Copy-Item -LiteralPath $target -Destination $backup
            }
            $changes.Add([pscustomobject]@{Target=$target;Backup=$backup;Added=(!$exists)})
            $changes | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $journal -Encoding utf8
            New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force | Out-Null
            Copy-Item -LiteralPath $file.FullName -Destination $target -Force
        }
    }
    # Old capture-only scripts may bind older production APIs. Archive them outside Assets.
    foreach($file in Get-ChildItem -LiteralPath (Join-Path $scratch 'Assets') -Filter '*.cs' -File -Recurse){
        if($file.FullName -like '*\UIPreviewsForMe*'){continue}
        $relative=$file.FullName.Substring($scratch.Length+1)
        if(Test-Path -LiteralPath (Join-Path $main $relative)){continue}
        Assert-Safe $file.FullName
        $backup=Join-Path $output ('diagnostics\OriginalScratch\'+$relative)
        New-Item -ItemType Directory -Path (Split-Path $backup -Parent) -Force | Out-Null
        Copy-Item -LiteralPath $file.FullName -Destination $backup
        $changes.Add([pscustomobject]@{Target=$file.FullName;Backup=$backup;Added=$false})
        $changes | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $journal -Encoding utf8
        Remove-Item -LiteralPath $file.FullName -Force
    }
    # Use the actual GM presentation method; only its editor/play guard is adapted in the disposable snapshot.
    $gmRelative='Assets\_Project\Presentation\Runtime\CoreGameplayGameMasterController.cs'
    $gmTarget=Join-Path $scratch $gmRelative
    $gmBackup=Join-Path $output ('diagnostics\OriginalScratch\'+$gmRelative)
    if(@($changes | Where-Object Target -eq $gmTarget).Count -eq 0){
        New-Item -ItemType Directory -Path (Split-Path $gmBackup -Parent) -Force | Out-Null
        Copy-Item -LiteralPath $gmTarget -Destination $gmBackup
        $changes.Add([pscustomobject]@{Target=$gmTarget;Backup=$gmBackup;Added=$false})
        $changes | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $journal -Encoding utf8
    }
    $gmCode=[IO.File]::ReadAllText($gmTarget)
    $gmGuard='private bool CanOpenGameMaster => Application.isPlaying && !editorPreviewBuilding &&'
    if(!$gmCode.Contains($gmGuard)){throw 'Expected GM presentation guard missing.'}
    [IO.File]::WriteAllText($gmTarget,$gmCode.Replace($gmGuard,'private bool CanOpenGameMaster =>'),[Text.UTF8Encoding]::new($false))
    $crewRelative='Assets\_Project\Presentation\Runtime\CoreGameplayPortController.cs'
    $crewTarget=Join-Path $scratch $crewRelative
    if(@($changes | Where-Object Target -eq $crewTarget).Count -eq 0){
        $crewBackup=Join-Path $output ('diagnostics\OriginalScratch\'+$crewRelative)
        New-Item -ItemType Directory -Path (Split-Path $crewBackup -Parent) -Force | Out-Null
        Copy-Item -LiteralPath $crewTarget -Destination $crewBackup
        $changes.Add([pscustomobject]@{Target=$crewTarget;Backup=$crewBackup;Added=$false})
        $changes | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $journal -Encoding utf8
    }
    $crewCode=[IO.File]::ReadAllText($crewTarget)
    $crewAnchor='if (PlayerPrefs.GetInt(CrewVariantRecruitmentKey(crew.Id), 0) == 1) return true;'
    if(!$crewCode.Contains($crewAnchor)){throw 'Expected crew fixture anchor missing.'}
    $crewHook='if (!Application.isPlaying && AppDomain.CurrentDomain.GetData("WoodFrameCaptureCrewIds") is HashSet<string> previewCrew && previewCrew.Contains(crew.Id)) return true;'
    [IO.File]::WriteAllText($crewTarget,$crewCode.Replace($crewAnchor,$crewHook+[Environment]::NewLine+'            '+$crewAnchor),[Text.UTF8Encoding]::new($false))
    'GM editor presentation guard adapted in scratch only. Actual renderer used; no save/transaction callback is invoked.' | Set-Content -LiteralPath (Join-Path $output 'diagnostics\fixture-adaptations.txt') -Encoding utf8
    'Crew preview recruitment uses task-owned in-memory IDs in scratch only; PlayerPrefs untouched.' | Add-Content -LiteralPath (Join-Path $output 'diagnostics\fixture-adaptations.txt') -Encoding utf8
    $inputs | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $output 'diagnostics\source-snapshot.json') -Encoding utf8
    Write-Output ('Prepared shared project; synchronized '+$changes.Count+' files; main project untouched.')
    exit
}
if(!(Test-Path -LiteralPath $lease) -or [IO.File]::ReadAllText($lease) -ne $output){throw 'Screenshot lease missing or mismatched.'}
if($Stage -eq 'Capture'){
    if($Run -notmatch '^[A-Za-z0-9_-]+$'){throw 'Use a simple run name.'}
    $runOutput=Join-Path $output $Run
    if(Test-Path -LiteralPath $runOutput){throw 'Use a fresh run name.'}
    New-Item -ItemType Directory -Path $runOutput | Out-Null
    $changes=@(Get-Content -LiteralPath $journal -Raw | ConvertFrom-Json)
    foreach($file in Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs' -File){
        $target=Join-Path $scratch ('Assets\_Project\Editor\WoodFrame'+$file.Name)
        Assert-Safe $target
        if(@($changes | Where-Object Target -eq $target).Count -eq 0){
            if(Test-Path -LiteralPath $target){throw 'Task helper name collision.'}
            $changes += [pscustomobject]@{Target=$target;Backup=$null;Added=$true}
            $changes += [pscustomobject]@{Target=($target+'.meta');Backup=$null;Added=$true}
            $changes | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $journal -Encoding utf8
        }
        Copy-Item -LiteralPath $file.FullName -Destination $target -Force
    }
    $arguments=@('-batchmode','-force-d3d11','-projectPath',$scratch,'-executeMethod','ShatteredSail.Editor.WoodFrameCapture.CaptureFromCommandLine','-woodFrameOutput',$runOutput,'-logFile',(Join-Path $runOutput 'unity.log'),'-quit')
    if($Only){$arguments+=@('-woodFrameOnly',$Only)}
    $process=Start-Process -FilePath 'D:\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe' -ArgumentList ($arguments|ForEach-Object {'"'+$_+'"'}) -WindowStyle Hidden -PassThru
    [pscustomobject]@{PID=$process.Id;Project=$scratch;Output=$runOutput;Arguments=$arguments} | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $runOutput 'launch.json') -Encoding utf8
    Write-Output ('Screenshot rendering started: '+$process.Id+'; '+$runOutput)
    exit
}
$freed=0L
$changes=@(Get-Content -LiteralPath $journal -Raw | ConvertFrom-Json)
foreach($change in $changes){
    Assert-Safe $change.Target
    if($change.Added){
        if(Test-Path -LiteralPath $change.Target){$freed+=(Get-Item -LiteralPath $change.Target).Length;Remove-Item -LiteralPath $change.Target -Force}
    }else{
        Copy-Item -LiteralPath $change.Backup -Destination $change.Target -Force
        if((Get-FileHash -LiteralPath $change.Backup).Hash -ne (Get-FileHash -LiteralPath $change.Target).Hash){throw 'Scratch restoration failed.'}
    }
}
$temp=Join-Path $scratch 'Temp'
if(Test-Path -LiteralPath $temp){
    Assert-Safe $temp
    $links=@(Get-ChildItem -LiteralPath $temp -Force -Recurse | Where-Object {$_.Attributes -band [IO.FileAttributes]::ReparsePoint})
    if($links.Count){throw 'Refusing linked temporary cache.'}
    $freed+=(Get-ChildItem -LiteralPath $temp -File -Recurse -Force | Measure-Object Length -Sum).Sum
    Remove-Item -LiteralPath $temp -Recurse -Force
    if(Test-Path -LiteralPath $temp){throw 'Temporary cache cleanup failed.'}
}
Remove-Item -LiteralPath $lease -Force
@('Shared scratch source files restored and verified.','No main UnityClient files were written by this task.','Existing shared Library retained: required by SmallTextAudit, WindowFit, ShipyardAssembly and other screenshot tools.',('Removed task helper files and owned Temp cache; bytes reclaimed: '+$freed),'Screenshots, capture scripts, logs and source snapshot retained.') | Set-Content -LiteralPath (Join-Path $output 'diagnostics\cleanup.txt') -Encoding utf8
Write-Output ('Cleanup verified; reclaimed '+$freed+' bytes. Shared Library retained for existing tools.')
