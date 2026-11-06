param([string]$Unity='D:/Unity/Hub/Editor/6000.3.23f1/Editor/Unity.exe')
$ErrorActionPreference='Stop'
$crewWorkspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$crewOutput=Join-Path $crewWorkspace 'outputs/crew-review-20261030'
$crewId=[Guid]::NewGuid().ToString('N')
$crewEvidence=Join-Path $crewOutput ('capture-evidence/'+$crewId)
$crewProject=Join-Path $crewWorkspace ('tmp/crew-review-20261030-'+$crewId)
$crewSource=Join-Path $crewWorkspace 'UnityClient'
$crewResources=Join-Path $crewSource 'Assets/_Project/Resources'
$crewSuccess=$false
foreach($crewAncestor in @($crewWorkspace,(Join-Path $crewWorkspace 'outputs'),(Join-Path $crewWorkspace 'tmp'))){if((Test-Path -LiteralPath $crewAncestor) -and ((Get-Item -LiteralPath $crewAncestor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)){throw 'Refuse directory link'}}
New-Item -ItemType Directory -Force -Path $crewEvidence,$crewProject,(Join-Path $crewProject 'Assets/Editor'),(Join-Path $crewProject 'Packages'),(Join-Path $crewProject 'ProjectSettings') | Out-Null
'Task-owned actual production crew renderer; never copy back to UnityClient.' | Set-Content -LiteralPath (Join-Path $crewProject 'OWNED_CREW_REVIEW.txt') -Encoding utf8
$crewInputFiles=[Collections.Generic.List[object]]::new()
function Copy-CrewInput([string]$Source,[string]$Relative){
$dest=Join-Path $crewProject $Relative
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $dest) | Out-Null
Copy-Item -LiteralPath $Source -Destination $dest
$hash=(Get-FileHash -LiteralPath $Source -Algorithm SHA256).Hash
if((Get-FileHash -LiteralPath $dest -Algorithm SHA256).Hash -ne $hash){throw 'Copy differs'}
$crewInputFiles.Add([pscustomobject]@{Source=$Source;Relative=$Relative;SHA256=$hash;Bytes=(Get-Item -LiteralPath $Source).Length})
}
try {
# Preserve source and GUIDs so the actual port-header prefab scripts remain bound.
foreach($assembly in @('Core','Content','Persistence','Presentation')){
$root=Join-Path $crewSource ('Assets/_Project/'+$assembly+'/Runtime')
foreach($file in Get-ChildItem -LiteralPath $root -File -Recurse){$relative=$file.FullName.Substring($crewSource.Length+1);Copy-CrewInput $file.FullName $relative}
}
$selected=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach($folder in @('Data','Fonts','UI','UIWood','HeaderFacilityIcons','PortIcons','PortHeaders','BattleIcons','ItemIcons','Shaders','PortArt/UI','PortArt/CrewRegional','PortArt/CrewFullBody','PortArt/CrewAvatars','PortFacilities/Regional/Tavern','CardSchools/Frames')){
foreach($file in Get-ChildItem -LiteralPath (Join-Path $crewResources $folder) -File -Recurse){[void]$selected.Add($file.FullName)}
}
foreach($file in Get-ChildItem -LiteralPath (Join-Path $crewResources 'PortArt') -File){[void]$selected.Add($file.FullName)}
foreach($file in Get-ChildItem -LiteralPath (Join-Path $crewResources 'PortArt/CrewDossiers') -File | Where-Object {$_.Name -like '*_portrait_v2.png*'}){[void]$selected.Add($file.FullName)}
foreach($file in Get-ChildItem -LiteralPath (Join-Path $crewResources 'LegacyDemo') -File){[void]$selected.Add($file.FullName)}
$cardManifest=Get-Content -LiteralPath (Join-Path $crewWorkspace 'outputs/card-list-by-school-20261023/card-game-view-data.json') -Raw | ConvertFrom-Json
foreach($card in $cardManifest.Cards | Where-Object {$_.Id -match 'crew'}){
foreach($resource in @($card.ArtResourcePath,$card.FrameResourcePath)){
foreach($suffix in @('.png','.png.meta','.jpg','.jpg.meta')){$path=Join-Path $crewResources ($resource+$suffix);if(Test-Path -LiteralPath $path){[void]$selected.Add($path)}}
}}
foreach($file in $selected){Copy-CrewInput $file ('Assets/_Project/Resources/'+$file.Substring($crewResources.Length+1))}
Copy-CrewInput (Join-Path $PSScriptRoot 'Capture.cs') 'Assets/Editor/Capture.cs'
Copy-CrewInput (Join-Path $crewSource 'ProjectSettings/ProjectVersion.txt') 'ProjectSettings/ProjectVersion.txt'
Copy-CrewInput (Join-Path $crewWorkspace 'tmp/card-keyword-tooltip-20261023-validation/Packages/manifest.json') 'Packages/manifest.json'
$company='ShatteredSailCrewReview';$product='CrewReview20261030_'+$crewId
$settings=[IO.File]::ReadAllText((Join-Path $crewSource 'ProjectSettings/ProjectSettings.asset'))
$settings=[Regex]::Replace($settings,'(?m)^  companyName:.*$','  companyName: '+$company)
$settings=[Regex]::Replace($settings,'(?m)^  productName:.*$','  productName: '+$product)
$settings=[Regex]::Replace($settings,'(?m)^  productGUID:.*$','  productGUID: '+$crewId)
[IO.File]::WriteAllText((Join-Path $crewProject 'ProjectSettings/ProjectSettings.asset'),$settings,[Text.UTF8Encoding]::new($false))
$crewInputFiles | Export-Csv -LiteralPath (Join-Path $crewEvidence 'input-hashes.csv') -NoTypeInformation -Encoding utf8
$hasher=[Security.Cryptography.SHA256]::Create();try{$fingerprint=[BitConverter]::ToString($hasher.ComputeHash([Text.Encoding]::UTF8.GetBytes(($crewInputFiles | Sort-Object Relative | ForEach-Object {$_.Relative+':'+$_.SHA256}) -join "`n"))).Replace('-','').ToLowerInvariant()}finally{$hasher.Dispose()}
[pscustomobject]@{Output=$crewOutput;Evidence=$crewEvidence;InputFingerprint=$fingerprint;Company=$company;Product=$product} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $crewProject 'capture-config.json') -Encoding utf8
[pscustomobject]@{Project=$crewProject;Evidence=$crewEvidence;InputFingerprint=$fingerprint;InputFiles=$crewInputFiles.Count;InputBytes=($crewInputFiles | Measure-Object Bytes -Sum).Sum} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $crewOutput 'capture-evidence/latest-run.json') -Encoding utf8
$log=Join-Path $crewEvidence 'Unity.log'
$process=Start-Process -FilePath $Unity -ArgumentList @('-batchmode','-projectPath',('"'+$crewProject+'"'),'-executeMethod','ShatteredSail.Editor.CrewReviewCapture.Run','-quit','-logFile',('"'+$log+'"')) -WindowStyle Hidden -PassThru
Write-Output ('Started production crew capture PID='+$process.Id+' log='+$log)
$started=[DateTime]::UtcNow
while(!$process.WaitForExit(1000)){if(([DateTime]::UtcNow-$started).TotalMinutes -gt 25){Stop-Process -Id $process.Id -Force;throw 'Capture timed out'}}
if($process.ExitCode -ne 0 -or !(Select-String -LiteralPath $log -Pattern 'CREW_REVIEW_CAPTURE_OK' -Quiet)){throw ('Capture failed: '+$log)}
foreach($input in $crewInputFiles){if((Get-FileHash -LiteralPath $input.Source -Algorithm SHA256).Hash -ne $input.SHA256){throw ('Source changed during capture: '+$input.Source)}}
$crewSuccess=$true
Write-Output 'PASS: 36 crew, 144 dossier pages, 6 tavern screenshots'
} finally {
# Keep exact source snapshot and small settings, then reclaim only this owned run.
foreach($relative in @('Assets/_Project/Core','Assets/_Project/Content','Assets/_Project/Persistence','Assets/_Project/Presentation','Assets/Editor','ProjectSettings','Packages','capture-config.json')){
$source=Join-Path $crewProject $relative
if(Test-Path -LiteralPath $source){$dest=Join-Path $crewEvidence ('input-snapshot/'+$relative);New-Item -ItemType Directory -Force -Path (Split-Path -Parent $dest) | Out-Null;Copy-Item -LiteralPath $source -Destination $dest -Recurse}
}
$resolved=[IO.Path]::GetFullPath($crewProject).TrimEnd('\','/')
if([IO.Path]::GetDirectoryName($resolved) -ne (Join-Path $crewWorkspace 'tmp') -or [IO.Path]::GetFileName($resolved) -notmatch '^crew-review-20261030-[a-f0-9]{32}$' -or !(Test-Path -LiteralPath (Join-Path $resolved 'OWNED_CREW_REVIEW.txt'))){throw 'Unsafe cleanup root'}
$users=@(Get-CimInstance Win32_Process | Where-Object {$_.ProcessId -ne $PID -and $_.CommandLine -and $_.CommandLine.IndexOf($resolved,[StringComparison]::OrdinalIgnoreCase) -ge 0})
$links=@(Get-ChildItem -LiteralPath $resolved -Recurse -Force | Where-Object {($_.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0})
if($users.Count -gt 0 -or $links.Count -gt 0){throw 'Task project in use or contains links; cleanup deferred'}
$released=(Get-ChildItem -LiteralPath $resolved -File -Recurse -Force | Measure-Object Length -Sum).Sum
Remove-Item -LiteralPath $resolved -Recurse -Force
if(Test-Path -LiteralPath $resolved){throw 'Cleanup failed'}
[pscustomobject]@{Success=$crewSuccess;ReleasedBytes=$released;Removed=$resolved;MainProjectModified=$false;Retained='Production source snapshot, input hashes, logs, rendered PNGs, capture manifest.'} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $crewEvidence 'cleanup.json') -Encoding utf8
Write-Output ('Cleaned owned capture project: '+$released+' bytes')
}
if($crewSuccess){
    & (Join-Path $PSScriptRoot 'CaptureThumbnails.ps1') -Output $crewOutput
    if(Test-Path -LiteralPath (Join-Path $crewOutput 'crew-data.json')){& (Join-Path $PSScriptRoot 'CaptureArtThumbnails.ps1') -Output $crewOutput}
}
