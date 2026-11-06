param([string]$Candidate='combined-b')
$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskOutput=Join-Path $taskRoot 'outputs/boarding-field-balance-20261024'
$taskBefore=Join-Path $taskOutput 'Inputs/CoreSnapshot'
$taskCandidate=Join-Path $taskOutput ('experiments/'+$Candidate)
$taskCore=Join-Path $taskRoot 'UnityClient/Assets/_Project/Core/Runtime'
$taskBackup=Join-Path $taskOutput 'Inputs/ProductionBeforeMerge'
if(Test-Path -LiteralPath $taskBackup){throw 'Production merge has already been prepared; inspect its manifest before retrying.'}
$taskRows=[Collections.Generic.List[object]]::new()
foreach($taskFile in (Get-ChildItem -LiteralPath $taskBefore -Filter '*.cs')){
    $taskLive=Join-Path $taskCore $taskFile.Name
    if((Get-FileHash -LiteralPath $taskLive).Hash -ne (Get-FileHash -LiteralPath $taskFile.FullName).Hash){throw ('Production changed since freeze: '+$taskLive)}
}
foreach($taskFile in (Get-ChildItem -LiteralPath $taskCandidate -Filter '*.cs')){
    $taskOriginal=Join-Path $taskBefore $taskFile.Name
    if(!(Test-Path -LiteralPath $taskOriginal) -or (Get-FileHash -LiteralPath $taskOriginal).Hash -ne (Get-FileHash -LiteralPath $taskFile.FullName).Hash){
        $taskRows.Add([pscustomobject]@{Candidate=$taskFile.FullName;Production=(Join-Path $taskCore $taskFile.Name);Backup=(Join-Path $taskBackup $taskFile.Name);BeforeSHA256='';AfterSHA256=(Get-FileHash -LiteralPath $taskFile.FullName).Hash})
    }
}
$taskPresentation=Join-Path $taskOutput 'experiments/card-a/CardSpecialCostPresentation.cs'
$taskRows.Add([pscustomobject]@{Candidate=$taskPresentation;Production=(Join-Path $taskRoot 'UnityClient/Assets/_Project/Presentation/Runtime/CardSpecialCostPresentation.cs');Backup=(Join-Path $taskBackup 'CardSpecialCostPresentation.cs');BeforeSHA256='';AfterSHA256=(Get-FileHash -LiteralPath $taskPresentation).Hash})
New-Item -ItemType Directory -Path $taskBackup | Out-Null
foreach($taskRow in $taskRows){
    if(Test-Path -LiteralPath $taskRow.Production){
        $taskRow.BeforeSHA256=(Get-FileHash -LiteralPath $taskRow.Production).Hash
        Copy-Item -LiteralPath $taskRow.Production -Destination $taskRow.Backup
    }
}
$taskRows | Export-Csv -LiteralPath (Join-Path $taskBackup 'merge-manifest.csv') -NoTypeInformation -Encoding utf8
foreach($taskRow in $taskRows){
    if((Get-FileHash -LiteralPath $taskRow.Candidate).Hash -ne $taskRow.AfterSHA256){throw 'Candidate changed during merge'}
    Copy-Item -LiteralPath $taskRow.Candidate -Destination $taskRow.Production -Force
}
$taskMeta=Join-Path $taskCore 'CoreBoardingCardBalance.cs.meta'
if(Test-Path -LiteralPath $taskMeta){throw 'Unexpected existing metadata for new balance table'}
[IO.File]::WriteAllText($taskMeta,"fileFormatVersion: 2`nguid: 92275cc179124bfeb3945b4bb9c56202`n",[Text.UTF8Encoding]::new($false))
foreach($taskRow in $taskRows){if((Get-FileHash -LiteralPath $taskRow.Production).Hash -ne $taskRow.AfterSHA256){throw 'Production merge mismatch'}}
Write-Output ('Merged '+$taskRows.Count+' source files from frozen '+$Candidate+'; originals retained at '+$taskBackup)
