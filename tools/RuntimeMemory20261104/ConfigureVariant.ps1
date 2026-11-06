param(
    [Parameter(Mandatory=$true)][ValidateSet('Baseline','Optimized')][string]$Variant,
    [string]$EvidenceSet = 'validation'
)
$ErrorActionPreference='Stop'
$workspace='D:\Game'
$scratch=Join-Path $workspace 'tmp\prefab-runtime-20261014'
$output=Join-Path $workspace ('Artifacts\RuntimeMemory20261104\'+$EvidenceSet)
$lease=Join-Path $scratch 'RuntimeMemory20261104.lock'
$journal=Join-Path $output 'scratch-journal.json'
if(!(Test-Path -LiteralPath $lease) -or [IO.File]::ReadAllText($lease) -ne $output){throw 'Missing task lease.'}
if(@(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'").Count){throw 'Wait for Unity to exit.'}
$changes=[Collections.Generic.List[object]]::new()
foreach($entry in @(Get-Content -LiteralPath $journal -Raw | ConvertFrom-Json)){$changes.Add($entry)}
$before=Join-Path $workspace 'outputs\runtime-memory-20261104\source-before\UnityClient\Assets\_Project\Presentation\Runtime'
$originals=@(Get-ChildItem -LiteralPath $before -File -Filter '*.cs')
if($originals.Count -ne 9){throw 'Expected exactly nine backed-up production files.'}
$snapshot=Join-Path $output ($Variant+'-source')
if(Test-Path -LiteralPath $snapshot){throw 'Preserve existing variant snapshot.'}
New-Item -ItemType Directory -Path $snapshot | Out-Null
$manifest=@()
foreach($original in $originals){
    $relative='Assets\_Project\Presentation\Runtime\'+$original.Name
    $source=if($Variant -eq 'Baseline'){$original.FullName}else{Join-Path (Join-Path $workspace 'UnityClient') $relative}
    $target=[IO.Path]::GetFullPath((Join-Path $scratch $relative))
    if(!$target.StartsWith($scratch+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Unsafe variant path.'}
    for($at=$target;$at -and $at.Length -ge $scratch.Length;$at=Split-Path $at -Parent){
        if((Test-Path -LiteralPath $at) -and ((Get-Item -LiteralPath $at -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)){throw 'Linked variant target.'}
    }
    if(!@($changes | Where-Object Target -eq $target).Count){
        $backup=Join-Path $output ('OriginalScratch\'+$relative)
        New-Item -ItemType Directory -Path (Split-Path $backup -Parent) -Force | Out-Null
        Copy-Item -LiteralPath $target -Destination $backup
        $changes.Add([pscustomobject]@{Target=$target;Backup=$backup;Added=$false})
        ConvertTo-Json -InputObject @($changes.ToArray()) -Depth 4 | Set-Content -LiteralPath $journal -Encoding utf8
    }
    Copy-Item -LiteralPath $source -Destination $target -Force
    Copy-Item -LiteralPath $source -Destination (Join-Path $snapshot $original.Name)
    $hash=(Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
    if((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne $hash){throw 'Variant copy mismatch.'}
    $manifest+=[pscustomobject]@{Path=$relative;SHA256=$hash}
}
$manifest | Export-Csv -LiteralPath (Join-Path $snapshot 'files.csv') -NoTypeInformation -Encoding utf8
Write-Output ($Variant+' applied to isolated project: '+$manifest.Count+' files. Main project unchanged.')
