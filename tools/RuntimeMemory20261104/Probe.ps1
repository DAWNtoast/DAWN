param(
    [Parameter(Mandatory=$true)][ValidateSet('Install','Build','Snapshot','Remove')][string]$Stage,
    [ValidatePattern('^[a-zA-Z0-9_-]+$')][string]$Run='baseline',
    [ValidatePattern('^[a-zA-Z0-9_-]+$')][string]$EvidenceSet='validation'
)
$ErrorActionPreference='Stop'
$scratch='D:\Game\tmp\prefab-runtime-20261014'
$task='D:\Game\Artifacts\RuntimeMemory20261104'
$output=Join-Path $task $EvidenceSet
$probe=Join-Path $scratch 'Assets\RuntimeMemory20261104Probe'
$journal=Join-Path $output 'scratch-journal.json'
$lease=Join-Path $scratch 'RuntimeMemory20261104.lock'
if($Stage -ne 'Remove' -and (!(Test-Path -LiteralPath $lease) -or [IO.File]::ReadAllText($lease) -ne $output)){throw 'Missing task lease.'}
if($Stage -eq 'Remove' -and (!(Test-Path -LiteralPath (Join-Path $output 'cleanup-verification.json')) -or (Test-Path -LiteralPath $lease))){throw 'Restore the borrowed project first.'}
if(@(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'").Count){throw 'Wait for Unity to exit.'}
function Assert-Safe([string]$target){
    $resolved=[IO.Path]::GetFullPath($target)
    if(!$resolved.StartsWith($scratch+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Target escapes borrowed project.'}
    for($at=$resolved;$at -and $at.Length -ge $scratch.Length;$at=Split-Path $at -Parent){
        if((Test-Path -LiteralPath $at) -and ((Get-Item -LiteralPath $at -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)){throw 'Linked target.'}
    }
}
Assert-Safe $probe
$changes=[Collections.Generic.List[object]]::new()
foreach($entry in @(Get-Content -LiteralPath $journal -Raw | ConvertFrom-Json)){$changes.Add($entry)}
function Record-Target([string]$target){
    Assert-Safe $target
    if(@($changes | Where-Object Target -eq $target).Count){return}
    $backup=$null
    if(Test-Path -LiteralPath $target){
        $backup=Join-Path $output ('OriginalScratch\'+$target.Substring($scratch.Length+1))
        New-Item -ItemType Directory -Path (Split-Path $backup -Parent) -Force | Out-Null
        Copy-Item -LiteralPath $target -Destination $backup
    }
    $changes.Add([pscustomobject]@{Target=$target;Backup=$backup;Added=($null -eq $backup)})
    ConvertTo-Json -InputObject @($changes.ToArray()) -Depth 4 | Set-Content -LiteralPath $journal -Encoding utf8
}
if($Stage -eq 'Install'){
    if(Test-Path -LiteralPath $probe){throw 'Probe directory already exists.'}
    New-Item -ItemType Directory -Path (Join-Path $probe 'Editor') -Force | Out-Null
    foreach($relative in @('', 'Editor')){
        $folder=if($relative){Join-Path $probe $relative}else{$probe}
        $meta=$folder+'.meta'; Record-Target $meta
        @('fileFormatVersion: 2',('guid: '+[guid]::NewGuid().ToString('N')),'folderAsset: yes','DefaultImporter:','  externalObjects: {}') | Set-Content -LiteralPath $meta -Encoding utf8
    }
    $files=@{
        'RuntimeMemory20261104Probe.cs'='RuntimeMemory20261104Probe.cs'
        'ShatteredSail.RuntimeMemory20261104.asmdef'='ShatteredSail.RuntimeMemory20261104.asmdef'
        'RuntimeMemory20261104Builder.cs'='Editor\RuntimeMemory20261104Builder.cs'
        'ShatteredSail.RuntimeMemory20261104.Editor.asmdef'='Editor\ShatteredSail.RuntimeMemory20261104.Editor.asmdef'
    }
    foreach($name in $files.Keys){
        $target=Join-Path $probe $files[$name]; Record-Target $target
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination $target
        $meta=$target+'.meta'; Record-Target $meta
        @('fileFormatVersion: 2',('guid: '+[guid]::NewGuid().ToString('N'))) | Set-Content -LiteralPath $meta -Encoding utf8
    }
    foreach($file in Get-ChildItem -LiteralPath (Join-Path $scratch 'ProjectSettings') -File){Record-Target $file.FullName}
    'Probe installed and all task additions journaled. Main project unchanged.'; exit
}
if($Stage -eq 'Build'){
    if(!(Test-Path -LiteralPath $probe)){throw 'Install probe first.'}
    $build=Join-Path $task ('builds\'+$Run)
    if(Test-Path -LiteralPath $build){throw 'Preserve prior build; select fresh name.'}
    New-Item -ItemType Directory -Path $build | Out-Null
    $arguments=@('-batchmode','-nographics','-quit','-projectPath',$scratch,'-executeMethod','ShatteredSail.Editor.RuntimeMemory20261104Builder.BuildFromCommandLine','-runtimeMemory20261104BuildOutput',(Join-Path $build 'Windows\MemoryProbe.exe'),'-logFile',(Join-Path $build 'unity.log'))
    $process=Start-Process -FilePath 'D:\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe' -ArgumentList ($arguments|ForEach-Object {'"'+$_+'"'}) -WindowStyle Hidden -PassThru
    [pscustomobject]@{PID=$process.Id;Arguments=$arguments;StartedAtUtc=[DateTime]::UtcNow.ToString('O')} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $build 'launch.json') -Encoding utf8
    'Build started PID '+$process.Id; exit
}
if($Stage -eq 'Snapshot'){
    $manifest=Join-Path $output ($Run+'-full-inputs.csv')
    if(Test-Path -LiteralPath $manifest){throw 'Preserve input manifest.'}
    $inputs=[Collections.Generic.List[object]]::new()
    $pending=[Collections.Generic.Stack[string]]::new()
    foreach($part in @('Assets','Packages','ProjectSettings')){$pending.Push((Join-Path $scratch $part))}
    while($pending.Count){
        $directory=$pending.Pop(); Assert-Safe $directory
        foreach($item in Get-ChildItem -LiteralPath $directory -Force){
            if($item.Name -eq 'UIPreviewsForMe' -or $item.Name -eq 'UIPreviewsForMe.meta'){continue}
            Assert-Safe $item.FullName
            if($item.PSIsContainer){$pending.Push($item.FullName)}else{
                $inputs.Add([pscustomobject]@{Path=$item.FullName.Substring($scratch.Length+1);SHA256=(Get-FileHash -LiteralPath $item.FullName).Hash;Bytes=$item.Length})
            }
        }
    }
    $inputs | Sort-Object Path | Export-Csv -LiteralPath $manifest -NoTypeInformation -Encoding utf8
    'Recorded '+$inputs.Count+' frozen inputs.'; exit
}
if($Stage -eq 'Remove'){
    # Only after validation cleanup has removed journaled files. Never traverse unknown remnants.
    if(Test-Path -LiteralPath $probe){
        foreach($item in Get-ChildItem -LiteralPath $probe -Force){
            Assert-Safe $item.FullName
            if(!$item.PSIsContainer -or $item.Name -ne 'Editor'){throw 'Unexpected probe remnant; preserve it.'}
            if(@(Get-ChildItem -LiteralPath $item.FullName -Force).Count){throw 'Run Validate Cleanup before removing empty probe folders.'}
        }
        foreach($dir in @((Join-Path $probe 'Editor'),$probe)){
            Assert-Safe $dir
            if(Test-Path -LiteralPath $dir){Remove-Item -LiteralPath $dir -Force}
        }
    }
}
