param([Parameter(Mandatory=$true)][ValidateSet('Start','Finish')][string]$Stage)
$ErrorActionPreference='Stop'
$main='D:\Game\UnityClient'
$scratch='D:\Game\tmp\shipyard-assembly-20261017'
$output='D:\Game\outputs\settlement-click-20261104\sequence-tests'
$lease=Join-Path $scratch 'SettlementClick20261104.lock'
$unity='D:\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe'
$relativePaths=@('Assets\_Project\Presentation\Runtime\SettlementChestSequenceController.cs','Assets\_Project\Tests\PlayMode\SettlementChestSequencePlayModeTests.cs')
function Assert-Safe([string]$Path) {
    $full=[IO.Path]::GetFullPath($Path)
    if (!$full.StartsWith($scratch+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Target escaped borrowed project.' }
    for($at=$full;$at -and $at.Length -ge $scratch.Length;$at=Split-Path $at -Parent) {
        if ((Test-Path -LiteralPath $at) -and ((Get-Item -LiteralPath $at -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) {throw ('Linked target: '+$at)}
    }
}
function Assert-Idle {
    if (@(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object {$_.CommandLine -and $_.CommandLine.Replace('/','\').Contains($scratch)}).Count) {throw 'Borrowed project is active.'}
    if(Test-Path -LiteralPath (Join-Path $scratch 'Temp\UnityLockfile')) {throw 'Borrowed project has UnityLockfile.'}
}
Assert-Idle
Assert-Safe (Join-Path $scratch 'Library')
if($Stage -eq 'Start') {
    if(Test-Path -LiteralPath $output) {throw 'Preserve previous sequence evidence.'}
    if(@(Get-ChildItem -LiteralPath $scratch -Filter '*.lock' -File).Count) {throw 'Borrowed project has another task lease.'}
    $settings=[IO.File]::ReadAllText((Join-Path $scratch 'ProjectSettings\ProjectSettings.asset'))
    if($settings -notmatch 'companyName: CodexShipyardAssemblyValidation' -or $settings -notmatch 'productName: ShipyardAssemblyValidation20261017') {throw 'Expected isolated PlayerPrefs identity.'}
    New-Item -ItemType Directory -Path $output -Force | Out-Null
    [IO.File]::WriteAllText($lease,$output)
    $journal=@(foreach($relative in $relativePaths) {
        $source=Join-Path $main $relative; $target=Join-Path $scratch $relative; Assert-Safe $target
        if(!(Test-Path -LiteralPath $target)) {throw 'Expected existing source; no new asset imports are authorized.'}
        $backup=Join-Path $output ('original\'+$relative); $snapshot=Join-Path $output ('source\'+$relative)
        New-Item -ItemType Directory -Path (Split-Path $backup -Parent),(Split-Path $snapshot -Parent) -Force | Out-Null
        Copy-Item -LiteralPath $target -Destination $backup
        Copy-Item -LiteralPath $source -Destination $snapshot
        [pscustomobject]@{Relative=$relative;Source=$source;Target=$target;Backup=$backup;Snapshot=$snapshot;OriginalSHA256=(Get-FileHash -LiteralPath $target).Hash;CurrentSHA256=(Get-FileHash -LiteralPath $source).Hash}
    })
    [pscustomobject]@{HadTemp=(Test-Path -LiteralPath (Join-Path $scratch 'Temp'));Project=$scratch;Files=$journal} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $output 'journal.json') -Encoding utf8
    foreach($entry in $journal) {Copy-Item -LiteralPath $entry.Snapshot -Destination $entry.Target -Force}
    $arguments=@('-batchmode','-force-d3d11','-projectPath',$scratch,'-runTests','-testPlatform','PlayMode','-testFilter','SettlementChestSequencePlayModeTests','-testResults',(Join-Path $output 'results.xml'),'-logFile',(Join-Path $output 'unity.log'))
    $process=Start-Process -FilePath $unity -ArgumentList ($arguments|ForEach-Object {'"'+$_+'"'}) -WindowStyle Hidden -PassThru
    [pscustomobject]@{PID=$process.Id;Arguments=$arguments;StartedUtc=[DateTime]::UtcNow.ToString('o');Scope='Current sequence controller and six isolated sequence tests; shared project dependencies retained. Current complete production and test sources separately compiled by Roslyn.'} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $output 'launch.json') -Encoding utf8
    Write-Output ('Started sequence tests PID '+$process.Id)
    exit
}
if(!(Test-Path -LiteralPath $lease) -or [IO.File]::ReadAllText($lease) -ne $output) {throw 'Task lease missing or mismatched.'}
$journal=Get-Content -LiteralPath (Join-Path $output 'journal.json') -Raw | ConvertFrom-Json
$restored=@(foreach($entry in $journal.Files) {
    Assert-Safe $entry.Target
    if((Get-FileHash -LiteralPath $entry.Target).Hash -ne $entry.CurrentSHA256) {throw ('Borrowed source changed: '+$entry.Target)}
    if((Get-FileHash -LiteralPath $entry.Backup).Hash -ne $entry.OriginalSHA256) {throw 'Backup hash changed.'}
    Copy-Item -LiteralPath $entry.Backup -Destination $entry.Target -Force
    if((Get-FileHash -LiteralPath $entry.Target).Hash -ne $entry.OriginalSHA256) {throw 'Failed to restore borrowed source.'}
    [pscustomobject]@{Target=$entry.Target;Restored=$true;MainUnchanged=((Get-FileHash -LiteralPath $entry.Source).Hash -eq $entry.CurrentSHA256)}
})
$freed=0L
$temp=Join-Path $scratch 'Temp';Assert-Safe $temp
if(!$journal.HadTemp -and (Test-Path -LiteralPath $temp)) {
    $items=@(Get-ChildItem -LiteralPath $temp -Force -Recurse)
    if(@($items|Where-Object {$_.Attributes -band [IO.FileAttributes]::ReparsePoint}).Count) {throw 'Linked temporary cache; preserve for inspection.'}
    $freed=($items|Where-Object {!$_.PSIsContainer}|Measure-Object Length -Sum).Sum
    Remove-Item -LiteralPath $temp -Recurse -Force
    if(Test-Path -LiteralPath $temp) {throw 'Temporary cleanup did not finish.'}
}
Remove-Item -LiteralPath $lease -Force
[pscustomobject]@{Restored=$restored;ReleasedBytes=$freed;SharedLibraryRetained='Existing shipyard assembly validation cache; used by other validation tools. No new full project or Library was created.';MainProjectCleaned=$false} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $output 'cleanup.json') -Encoding utf8
Write-Output ('Restored borrowed inputs; released '+$freed+' temporary bytes.')
