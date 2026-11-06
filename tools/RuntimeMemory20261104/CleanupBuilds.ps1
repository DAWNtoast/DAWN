param(
    [ValidatePattern('^[a-zA-Z0-9_-]+$')][string]$EvidenceSet='validation',
    [ValidatePattern('^[a-zA-Z0-9_-]+$')][string]$EditRun='optimized-edit',
    [ValidatePattern('^[a-zA-Z0-9_-]+$')][string]$PlayRun='optimized-play2'
)
$ErrorActionPreference='Stop'
$task='D:\Game\Artifacts\RuntimeMemory20261104'
$evidence=Join-Path $task $EvidenceSet
if(@(Get-CimInstance Win32_Process | Where-Object { $_.Name -eq 'Unity.exe' -or $_.Name -eq 'MemoryProbe.exe' }).Count){throw 'Wait for Editor and diagnostic Players to exit.'}
foreach($run in @($EditRun,$PlayRun)){
    $platform=if($run -eq $EditRun){'EditMode'}else{'PlayMode'}
    $result=Join-Path $evidence ($run+'\'+$platform+'.xml')
    if(!(Test-Path -LiteralPath $result)){throw ('Missing final test result: '+$run)}
    [xml]$tests=Get-Content -LiteralPath $result
    if($tests.'test-run'.result -ne 'Passed' -or [int]$tests.'test-run'.failed -ne 0){throw 'Keep diagnostics until regression failures are resolved.'}
}
if(!(Test-Path -LiteralPath (Join-Path $task 'comparison\comparison.json'))){throw 'Preserve builds until comparison is recorded.'}
function Safe-Files([string]$root){
    $resolved=[IO.Path]::GetFullPath($root)
    if(!$resolved.StartsWith($task+'\builds\',[StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetFileName($resolved) -ne 'Windows'){throw 'Unexpected diagnostic build root.'}
    for($at=$resolved;$at -and $at.Length -ge $task.Length;$at=Split-Path $at -Parent){
        if((Get-Item -LiteralPath $at -Force).Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Linked diagnostic build root.'}
    }
    $pending=[Collections.Generic.Stack[string]]::new();$pending.Push($resolved)
    while($pending.Count){
        foreach($item in Get-ChildItem -LiteralPath $pending.Pop() -Force){
            if($item.Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Linked diagnostic build content.'}
            if($item.PSIsContainer){$pending.Push($item.FullName)}else{$item}
        }
    }
}
$records=@()
foreach($variant in @('baseline','optimized')){
    $root=Join-Path $task ('builds\'+$variant+'\Windows')
    $archive=Join-Path $task ('measured-assemblies\'+$variant)
    if(Test-Path -LiteralPath $archive){throw 'Preserve existing assembly archive.'}
    $files=@(Safe-Files $root)
    New-Item -ItemType Directory -Path $archive -Force | Out-Null
    $manifest=@($files | ForEach-Object {[pscustomobject]@{Path=$_.FullName.Substring($root.Length+1);Bytes=$_.Length;SHA256=(Get-FileHash -LiteralPath $_.FullName).Hash}})
    $manifest | Export-Csv -LiteralPath (Join-Path $archive 'build-files.csv') -NoTypeInformation -Encoding utf8
    foreach($dll in Get-ChildItem -LiteralPath (Join-Path $root 'MemoryProbe_Data\Managed') -Filter 'ShatteredSail.*.dll' -File){
        $destination=Join-Path $archive $dll.Name
        Copy-Item -LiteralPath $dll.FullName -Destination $destination
        if((Get-FileHash -LiteralPath $destination).Hash -ne (Get-FileHash -LiteralPath $dll.FullName).Hash){throw 'Measured assembly archive mismatch.'}
    }
    Copy-Item -LiteralPath (Join-Path $root 'build-report.txt') -Destination $archive
    $bytes=($files | Measure-Object Length -Sum).Sum
    Remove-Item -LiteralPath $root -Recurse -Force
    if(Test-Path -LiteralPath $root){throw 'Diagnostic build cleanup failed.'}
    $records+=[pscustomobject]@{Path=$root;Files=$files.Count;ReclaimedBytes=$bytes;Preserved=$archive}
}
$records | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $task 'build-cleanup.json') -Encoding utf8
'Diagnostic builds reclaimed: '+(($records | Measure-Object ReclaimedBytes -Sum).Sum)+' bytes. Measured DLLs, manifests, logs, trials and source snapshots retained.'
