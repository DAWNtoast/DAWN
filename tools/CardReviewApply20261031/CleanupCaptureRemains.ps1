param(
    [Parameter(Mandatory=$true)]
    [ValidateSet('8ffcc3134ff345cd80f4717b5bbd7021')]
    [string[]]$RunIds
)
$ErrorActionPreference='Stop'
$taskWorkspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskTemporary=[IO.Path]::GetFullPath((Join-Path $taskWorkspace 'tmp'))
$taskEvidenceRoot=Join-Path $taskWorkspace 'outputs/card-game-view-20261030/runs'
$taskSummaryPath=Join-Path $taskWorkspace 'outputs/card-review-apply-20261031/validation/cleanup-summary.json'
$taskRecords=@()
if(Test-Path -LiteralPath $taskSummaryPath) {$taskRecords=@((Get-Content -LiteralPath $taskSummaryPath -Raw | ConvertFrom-Json).Runs)}
Add-Type -AssemblyName System.IO.Compression.FileSystem

function Assert-NoDirectoryLinks([string]$Path) {
    $taskAncestor=Get-Item -LiteralPath $Path -Force
    while($null -ne $taskAncestor) {
        if($taskAncestor.Attributes -band [IO.FileAttributes]::ReparsePoint){throw ('Directory link: '+$taskAncestor.FullName)}
        $taskAncestor=$taskAncestor.Parent
    }
}
function Get-StreamHash($Stream) {
    $taskHasher=[Security.Cryptography.SHA256]::Create()
    try {[BitConverter]::ToString($taskHasher.ComputeHash($Stream)).Replace('-','')} finally {$taskHasher.Dispose()}
}

foreach($taskRunId in $RunIds) {
    $taskProject=[IO.Path]::GetFullPath((Join-Path $taskTemporary ('card-game-view-20261030-'+$taskRunId)))
    $taskEvidence=[IO.Path]::GetFullPath((Join-Path $taskEvidenceRoot $taskRunId))
    if([IO.Path]::GetDirectoryName($taskProject) -ne $taskTemporary -or
        [IO.Path]::GetDirectoryName($taskEvidence) -ne [IO.Path]::GetFullPath($taskEvidenceRoot) -or
        $taskProject.StartsWith((Join-Path $taskWorkspace 'UnityClient'),[StringComparison]::OrdinalIgnoreCase)) {throw 'Unexpected cleanup path.'}
    Assert-NoDirectoryLinks $taskEvidence
    $taskExportCleanup=Get-Content -LiteralPath (Join-Path $taskEvidence 'cleanup.json') -Raw | ConvertFrom-Json
    if(!(Test-Path -LiteralPath $taskProject)) {
        if(@($taskRecords | Where-Object {$_.RunId -eq $taskRunId -and $_.Removed}).Count -eq 1){continue}
        throw 'Project is missing without a verified cleanup record.'
    }
    Assert-NoDirectoryLinks $taskProject
    if(!(Test-Path -LiteralPath (Join-Path $taskProject 'OWNED_CARD_GAME_VIEW.txt'))){throw 'Missing task ownership marker.'}
    $taskProjectRecord=Get-Content -LiteralPath (Join-Path $taskEvidence 'project.json') -Raw | ConvertFrom-Json
    if([IO.Path]::GetFullPath($taskProjectRecord.Project) -ne $taskProject){throw 'Project evidence mismatch.'}
    foreach($taskLarge in @('Library','Temp','Assets/Plugins','Assets/Resources')) {
        if(Test-Path -LiteralPath (Join-Path $taskProject $taskLarge)){throw ('Exporter has not finished cache cleanup: '+$taskLarge)}
    }
    $taskUsers=@(Get-CimInstance Win32_Process | Where-Object {
        $_.ProcessId -ne $PID -and $_.CommandLine -and
        $_.CommandLine.Replace('/','\').IndexOf($taskProject,[StringComparison]::OrdinalIgnoreCase) -ge 0
    })
    if($taskUsers.Count){throw 'Project is referenced by a running process.'}
    $taskItems=@(Get-ChildItem -LiteralPath $taskProject -Force -Recurse)
    if(@($taskItems | Where-Object {$_.Attributes -band [IO.FileAttributes]::ReparsePoint}).Count){throw 'Project contains directory/file links.'}
    $taskFiles=@($taskItems | Where-Object {!$_.PSIsContainer})
    $taskFilesBytes=($taskFiles | Measure-Object Length -Sum).Sum
    if($taskFilesBytes -gt 10MB){throw 'Expected only small archived project inputs.'}
    $taskHashes=@($taskFiles | ForEach-Object {
        $taskProbe=[IO.File]::Open($_.FullName,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::None)
        try {$taskHash=Get-StreamHash $taskProbe} finally {$taskProbe.Dispose()}
        [pscustomobject]@{Path=$_.FullName.Substring($taskProject.Length+1).Replace('\','/');SHA256=$taskHash;Bytes=$_.Length}
    })
    $taskArchive=Join-Path $taskEvidence 'complete-small-project-inputs.zip'
    if(!(Test-Path -LiteralPath $taskArchive)) {[IO.Compression.ZipFile]::CreateFromDirectory($taskProject,$taskArchive,[IO.Compression.CompressionLevel]::Optimal,$false)}
    $taskZip=[IO.Compression.ZipFile]::OpenRead($taskArchive)
    try {
        $taskZipFiles=@($taskZip.Entries | Where-Object {$_.Name})
        if($taskZipFiles.Count -ne $taskHashes.Count){throw 'Archive file count differs.'}
        foreach($taskHash in $taskHashes) {
            $taskEntry=$taskZip.GetEntry($taskHash.Path)
            if($null -eq $taskEntry -or $taskEntry.Length -ne $taskHash.Bytes){throw ('Archive size/path differs: '+$taskHash.Path)}
            $taskStream=$taskEntry.Open()
            try {if((Get-StreamHash $taskStream) -ne $taskHash.SHA256){throw ('Archive hash differs: '+$taskHash.Path)}} finally {$taskStream.Dispose()}
        }
    } finally {$taskZip.Dispose()}
    $taskHashes | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $taskEvidence 'complete-small-project-inputs-hashes.json') -Encoding utf8
    foreach($taskHash in $taskHashes) {
        if((Get-FileHash -LiteralPath (Join-Path $taskProject $taskHash.Path) -Algorithm SHA256).Hash -ne $taskHash.SHA256){throw 'Project changed after archiving.'}
    }
    Remove-Item -LiteralPath $taskProject -Recurse -Force
    if(Test-Path -LiteralPath $taskProject){throw 'Project removal failed.'}
    if(!(Test-Path -LiteralPath $taskArchive) -or !(Test-Path -LiteralPath (Join-Path $taskEvidence 'Unity.log'))){throw 'Required evidence is missing.'}
    $taskRecords=@($taskRecords | Where-Object {$_.RunId -ne $taskRunId})+@([pscustomobject]@{
        RunId=$taskRunId;Removed=$true;Project=$taskProject;Archive=$taskArchive;ArchivedFiles=$taskHashes.Count;
        SmallProjectReleasedBytes=$taskFilesBytes;ExporterReleasedBytes=$taskExportCleanup.ReleasedBytes;
        ReleasedBytes=($taskFilesBytes+$taskExportCleanup.ReleasedBytes);ExportSucceeded=$taskExportCleanup.Success
    })
}
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $taskSummaryPath) | Out-Null
[pscustomobject]@{
    Runs=$taskRecords;ReleasedBytes=($taskRecords | Measure-Object ReleasedBytes -Sum).Sum;
    MainProjectTouched=$false;Preserved='User inputs, current catalog, card PNGs, source/resource hashes, logs, capture plans and complete small project input archives.'
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $taskSummaryPath -Encoding utf8
Get-Content -LiteralPath $taskSummaryPath
