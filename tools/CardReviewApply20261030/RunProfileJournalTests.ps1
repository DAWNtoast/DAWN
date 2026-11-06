param([string]$Unity='D:/Unity/Hub/Editor/6000.3.23f1/Editor/Unity.exe',
    [string]$RunTag='journal-unity',
    [string]$TestFilter='ShatteredSail.Core.Tests.ProfileSaveJournalTests;ShatteredSail.Core.Tests.CardSupplyCostPresentationTests')
$ErrorActionPreference='Stop'
$reviewWorkspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if($RunTag -notmatch '^[a-zA-Z0-9_-]+$'){throw 'RunTag must be a simple evidence directory name.'}
$reviewOutput=Join-Path $reviewWorkspace ('outputs/card-review-apply-20261030/validation/'+$RunTag)
if(Test-Path -LiteralPath (Join-Path $reviewOutput 'project.json')){throw 'Preserve previous run evidence; choose a new RunTag.'}
$reviewId=[Guid]::NewGuid().ToString('N')
$reviewProject=Join-Path $reviewWorkspace ('tmp/card-review-journal-20261030-'+$reviewId)
$reviewCompany='ShatteredSailCardReviewAudit'
$reviewProduct='CardReviewJournal20261030_'+$reviewId
$reviewProcess=$null
if(Test-Path -LiteralPath $reviewProject){throw 'Validation project must be new.'}
foreach($reviewAncestorPath in @($reviewWorkspace,(Join-Path $reviewWorkspace 'tmp'),(Join-Path $reviewWorkspace 'outputs'))){if((Test-Path -LiteralPath $reviewAncestorPath) -and ((Get-Item -LiteralPath $reviewAncestorPath -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){throw 'Validation refuses linked workspace ancestors.'}}
foreach($reviewDirectory in @($reviewOutput,$reviewProject,(Join-Path $reviewProject 'Assets/Plugins'),(Join-Path $reviewProject 'Assets/Tests/Editor'),(Join-Path $reviewProject 'Packages'),(Join-Path $reviewProject 'ProjectSettings'))){New-Item -ItemType Directory -Path $reviewDirectory -Force | Out-Null}
('Owned card-review journal test project. Independent identity: '+$reviewCompany+'/'+$reviewProduct) | Set-Content -LiteralPath (Join-Path $reviewProject 'OWNED_CARD_REVIEW_JOURNAL.txt') -Encoding utf8
[pscustomobject]@{Project=$reviewProject;Company=$reviewCompany;Product=$reviewProduct;StartedUtc=[DateTime]::UtcNow.ToString('o');MainProjectCopied=$false} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $reviewOutput 'project.json') -Encoding utf8
try {
    # Only a small settings file and local package references are reused. No game
    # assets, saved data, scenes, Library or shared validation project are copied.
    $reviewSettings=[IO.File]::ReadAllText((Join-Path $reviewWorkspace 'UnityClient/ProjectSettings/ProjectSettings.asset'))
    $reviewSettings=[Regex]::Replace($reviewSettings,'(?m)^  companyName:.*$','  companyName: '+$reviewCompany)
    $reviewSettings=[Regex]::Replace($reviewSettings,'(?m)^  productName:.*$','  productName: '+$reviewProduct)
    $reviewSettings=[Regex]::Replace($reviewSettings,'(?m)^  productGUID:.*$','  productGUID: '+$reviewId)
    [IO.File]::WriteAllText((Join-Path $reviewProject 'ProjectSettings/ProjectSettings.asset'),$reviewSettings,[Text.UTF8Encoding]::new($false))
    Copy-Item -LiteralPath (Join-Path $reviewWorkspace 'UnityClient/ProjectSettings/ProjectVersion.txt') -Destination (Join-Path $reviewProject 'ProjectSettings/ProjectVersion.txt')
    Copy-Item -LiteralPath (Join-Path $reviewWorkspace 'tmp/card-keyword-tooltip-20261023-validation/Packages/manifest.json') -Destination (Join-Path $reviewProject 'Packages/manifest.json')
    $reviewAssembly=@{name='ShatteredSail.CardReview.Journal.Editor';includePlatforms=@('Editor');references=@('Unity.UGUI','Unity.InputSystem');autoReferenced=$false;overrideReferences=$true;precompiledReferences=@('ShatteredSail.Core.dll','ShatteredSail.Content.dll','ShatteredSail.Persistence.dll','ShatteredSail.Presentation.dll','nunit.framework.dll');optionalUnityReferences=@('TestAssemblies')}
    $reviewAssembly | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $reviewProject 'Assets/Tests/Editor/JournalTests.asmdef') -Encoding utf8
    $reviewSource=Join-Path $reviewWorkspace 'UnityClient/Assets/_Project/Tests/EditMode/ProfileSaveJournalTests.cs'
    Copy-Item -LiteralPath $reviewSource -Destination (Join-Path $reviewProject 'Assets/Tests/Editor/ProfileSaveJournalTests.cs')
    Copy-Item -LiteralPath (Join-Path $reviewWorkspace 'UnityClient/Assets/_Project/Tests/EditMode/CardSupplyCostPresentationTests.cs') -Destination (Join-Path $reviewProject 'Assets/Tests/Editor/CardSupplyCostPresentationTests.cs')
    $reviewGuard=@"
using NUnit.Framework;
using UnityEngine;
[SetUpFixture] public sealed class CardReviewJournalIdentityGuard
{
    [OneTimeSetUp] public void RequireIsolatedPlayerPreferences()
    {
        Assert.That(Application.companyName, Is.EqualTo("$reviewCompany"));
        Assert.That(Application.productName, Is.EqualTo("$reviewProduct"));
    }
}
"@
    [IO.File]::WriteAllText((Join-Path $reviewProject 'Assets/Tests/Editor/JournalIdentityGuard.cs'),$reviewGuard,[Text.UTF8Encoding]::new($false))
    & (Join-Path $PSScriptRoot 'CompileProduction.ps1') -JournalPluginDirectory (Join-Path $reviewProject 'Assets/Plugins')
    Get-ChildItem -LiteralPath (Join-Path $reviewProject 'Assets') -File -Recurse | ForEach-Object {[pscustomobject]@{Path=$_.FullName;SHA256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}} | Export-Csv -LiteralPath (Join-Path $reviewOutput 'input-hashes.csv') -NoTypeInformation -Encoding utf8
    $reviewResult=Join-Path $reviewOutput 'EditMode.xml'
    $reviewLog=Join-Path $reviewOutput 'Unity.log'
    $TestFilter | Set-Content -LiteralPath (Join-Path $reviewOutput 'test-filter.txt') -Encoding utf8
    $reviewArgs=@('-batchmode','-nographics','-projectPath',('"'+$reviewProject+'"'),'-runTests','-testPlatform','EditMode','-testFilter',$TestFilter,'-testResults',('"'+$reviewResult+'"'),'-logFile',('"'+$reviewLog+'"'))
    $reviewStarted=[DateTime]::UtcNow
    $reviewProcess=Start-Process -FilePath $Unity -ArgumentList $reviewArgs -WindowStyle Hidden -PassThru
    Write-Output ('Started isolated journal tests PID='+$reviewProcess.Id)
    while(!$reviewProcess.WaitForExit(1000)) {if(([DateTime]::UtcNow-$reviewStarted).TotalMinutes -gt 7){Stop-Process -Id $reviewProcess.Id -Force; $reviewProcess.WaitForExit(10000) | Out-Null; throw 'Journal Unity run timed out; inspect retained log.'}}
    if($reviewProcess.ExitCode -ne 0){throw ('Journal Unity exit '+$reviewProcess.ExitCode+'; inspect '+$reviewLog)}
    if(!(Test-Path -LiteralPath $reviewResult) -or (Get-Item -LiteralPath $reviewResult).LastWriteTimeUtc -lt $reviewStarted){throw 'Fresh journal test results were not produced.'}
    [xml]$reviewXml=Get-Content -LiteralPath $reviewResult
    $reviewRun=$reviewXml.'test-run'
    Write-Output ('Journal EditMode '+$reviewRun.result+' total='+$reviewRun.total+' passed='+$reviewRun.passed+' failed='+$reviewRun.failed)
    if($reviewRun.result -ne 'Passed' -or [int]$reviewRun.failed -ne 0 -or [int]$reviewRun.total -lt 1){throw 'Journal tests failed.'}
} finally {
    # Archive the small independent test inputs and retain diagnostics. Reclaim
    # only this exact owned project's generated caches after all users exit.
    Copy-Item -LiteralPath (Join-Path $reviewProject 'Assets/Tests/Editor') -Destination (Join-Path $reviewOutput 'test-inputs') -Recurse -Force
    $reviewResolved=[IO.Path]::GetFullPath($reviewProject).TrimEnd('\','/')
    if([IO.Path]::GetDirectoryName($reviewResolved) -ne [IO.Path]::GetFullPath((Join-Path $reviewWorkspace 'tmp')) -or [IO.Path]::GetFileName($reviewResolved) -notmatch '^card-review-journal-20261030-[a-f0-9]{32}$' -or !(Test-Path -LiteralPath (Join-Path $reviewResolved 'OWNED_CARD_REVIEW_JOURNAL.txt'))){throw 'Unsafe journal cleanup root.'}
    $reviewLinks=@(Get-ChildItem -LiteralPath $reviewResolved -Recurse -Force | Where-Object {($_.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0})
    $reviewConsumers=@(Get-CimInstance Win32_Process | Where-Object {$_.ProcessId -ne $PID -and $_.CommandLine -and $_.CommandLine.IndexOf($reviewResolved,[StringComparison]::OrdinalIgnoreCase) -ge 0})
    if($reviewLinks.Count -gt 0 -or $reviewConsumers.Count -gt 0){Write-Warning 'Journal caches retained because links or live consumers need inspection.'}
    else {
        $reviewReleased=0L
        $reviewRemoved=@()
        foreach($reviewName in @('Library','Temp','obj','Logs','Assets/Plugins')){
            $reviewTarget=[IO.Path]::GetFullPath((Join-Path $reviewResolved $reviewName))
            if(!$reviewTarget.StartsWith($reviewResolved+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Cleanup target escaped owned project.'}
            if(Test-Path -LiteralPath $reviewTarget){$reviewReleased+=(Get-ChildItem -LiteralPath $reviewTarget -File -Recurse -Force | Measure-Object Length -Sum).Sum; Remove-Item -LiteralPath $reviewTarget -Recurse -Force; if(Test-Path -LiteralPath $reviewTarget){throw 'Journal cleanup failed.'}; $reviewRemoved+=$reviewTarget}
        }
        [pscustomobject]@{ReleasedBytes=$reviewReleased;Removed=$reviewRemoved;MainProjectCleaned=$false;InputsAndEvidenceRetained=$true} | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $reviewOutput 'cleanup.json') -Encoding utf8
        Write-Output ('Cleaned isolated journal caches: '+$reviewReleased+' bytes.')
    }
}
