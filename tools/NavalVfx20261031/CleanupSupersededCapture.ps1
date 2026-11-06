param(
    [ValidatePattern('^[a-zA-Z0-9_-]+$')][string]$EvidenceSet='validation',
    [ValidatePattern('^[a-zA-Z0-9_-]+$')][string]$SupersededRun='capture1',
    [ValidatePattern('^[a-zA-Z0-9_-]+$')][string]$FinalRun='capture2'
)
$ErrorActionPreference='Stop'
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$evidence=[IO.Path]::GetFullPath((Join-Path $workspace ('outputs/naval-vfx-20261031/'+$EvidenceSet)))
$old=[IO.Path]::GetFullPath((Join-Path $evidence $SupersededRun))
$final=[IO.Path]::GetFullPath((Join-Path $evidence $FinalRun))
if($old-eq $final -or [IO.Path]::GetDirectoryName($old)-ne $evidence -or [IO.Path]::GetDirectoryName($final)-ne $evidence){throw 'Unexpected capture cleanup paths.'}
foreach($target in @($old,$final)){
    for($at=$target;$at -and $at.Length -ge $workspace.Length;$at=Split-Path $at -Parent){
        if((Test-Path -LiteralPath $at)-and((Get-Item -LiteralPath $at -Force).Attributes-band [IO.FileAttributes]::ReparsePoint)){throw ('Linked capture path: '+$at)}
    }
}
$active=@(Get-CimInstance Win32_Process -Filter "Name='Unity.exe' OR Name LIKE 'ffmpeg%'"|Where-Object{$_.CommandLine -and $_.CommandLine.Replace('\','/').Contains($old.Replace('\','/'))})
if($active.Count){throw 'Superseded capture still has a consumer.'}
$finalVerification=Get-Content -LiteralPath (Join-Path $final 'frame-cleanup.json') -Raw|ConvertFrom-Json
$viewer=Get-Content -LiteralPath (Join-Path $final 'viewer-validation.json') -Raw|ConvertFrom-Json
if(!$finalVerification.DecodeVerified -or $viewer.FinalMediaReferences-ne 'PASS'){throw 'Final capture media has not been verified; preserve retries.'}
$oldReport=Get-Content -LiteralPath (Join-Path $old 'report.json') -Raw|ConvertFrom-Json
$finalReport=Get-Content -LiteralPath (Join-Path $final 'encoded-report.json') -Raw|ConvertFrom-Json
if($oldReport.sequences.Count-ne $finalReport.sequences.Count){throw 'Final capture does not replace the same sequence set.'}
$keep='11_Area_Fire_contact.png'
$keepHash=(Get-FileHash -LiteralPath (Join-Path $old $keep)).Hash
$candidates=@($oldReport.sequences|ForEach-Object{$_.frames.file;$_.keyframes})|Where-Object{$_-ne $keep}|Select-Object -Unique
$removed=[Collections.Generic.List[object]]::new();$freed=0L
foreach($name in $candidates){
    if([IO.Path]::GetFileName($name)-ne $name -or $name-notmatch '^\d{2}_[A-Za-z]+_[A-Za-z]+_(\d{3}\.jpg|(start|contact|after)\.png)$'){throw ('Unexpected retry artifact: '+$name)}
    $path=[IO.Path]::GetFullPath((Join-Path $old $name))
    if([IO.Path]::GetDirectoryName($path)-ne $old){throw 'Retry cleanup escaped its exact capture directory.'}
    if(!(Test-Path -LiteralPath $path)){continue}
    $item=Get-Item -LiteralPath $path -Force
    if($item.PSIsContainer -or ($item.Attributes-band [IO.FileAttributes]::ReparsePoint)){throw ('Unexpected retry item: '+$path)}
    $removed.Add([pscustomobject]@{File=$name;SHA256=(Get-FileHash -LiteralPath $path).Hash;Length=$item.Length})
    $freed+=$item.Length;Remove-Item -LiteralPath $path -Force
    if(Test-Path -LiteralPath $path){throw 'Retry cleanup failed.'}
}
if((Get-FileHash -LiteralPath (Join-Path $old $keep)).Hash-ne $keepHash){throw 'Diagnostic keyframe changed.'}
foreach($sequence in $finalReport.sequences){foreach($file in @($sequence.video)+@($sequence.keyframes)){if(!(Test-Path -LiteralPath (Join-Path $final $file))){throw 'Final media missing after cleanup.'}}}
$removed|Export-Csv -LiteralPath (Join-Path $old 'superseded-frame-hashes.csv') -NoTypeInformation -Encoding utf8
[pscustomobject]@{SupersededBy=$final;Reason='Offline area sample corrected from entire 3x3 source field to actual selected 2x2 card region; production gameplay was already correct.';ReleasedBytes=$freed;RemovedFiles=$removed.Count;DiagnosticKeyframeRetained=$keep;LogsAndReportRetained=$true;MainProjectTouched=$false}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $old 'cleanup.json') -Encoding utf8
Write-Output ('Superseded capture cleanup complete: '+$freed+' bytes reclaimed; final media and diagnostic evidence retained.')
