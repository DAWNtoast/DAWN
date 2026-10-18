param(
    [ValidateSet('Sync','Import','PlayMode','EditMode')][string]$Stage='Sync',
    [string]$RunTag='Current',
    [string]$TestFilter='',
    [switch]$ExcludeUnrelatedCrewTest,
    [string[]]$AdditionalEditModeSources=@()
)
$ErrorActionPreference='Stop'
$courseWorkspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if($Stage-eq'Sync'){
    $courseMetaRoots=@('Core/Runtime','Content/Runtime','Persistence/Runtime','Presentation/Runtime','Tests/PlayMode')
    $courseMetaFiles=@($courseMetaRoots|ForEach-Object{Get-ChildItem -LiteralPath (Join-Path $courseWorkspace ('UnityClient/Assets/_Project/'+$_)) -Filter '*.cs.meta' -File -Recurse})
    foreach($courseAdditional in $AdditionalEditModeSources){if(Test-Path -LiteralPath ($courseAdditional+'.meta')){$courseMetaFiles+=Get-Item -LiteralPath ($courseAdditional+'.meta')}}
    foreach($courseMeta in $courseMetaFiles){
        $courseMetaText=Get-Content -LiteralPath $courseMeta.FullName -Raw
        if($courseMetaText-notmatch'(?m)^guid: [0-9a-fA-F]{32}\r?$'){throw ('Malformed source GUID before sync: '+$courseMeta.FullName)}
    }
    Write-Output ('Validated '+$courseMetaFiles.Count+' source C# meta GUIDs before synchronization.')
}
$courseArguments=@{
    Stage=$Stage;RunTag=$RunTag;ArtifactDirectory='Artifacts/CourseTargetLock20261014';
    AdditionalEditModeSources=$AdditionalEditModeSources;
    ExcludeUnrelatedCrewTest=$ExcludeUnrelatedCrewTest
}
if($TestFilter){$courseArguments.TestFilter=$TestFilter}
elseif($Stage-in@('PlayMode','EditMode')){throw 'Specify the relevant course-lock test filter explicitly; this wrapper never runs unrelated broad suites.'}
& (Join-Path $courseWorkspace 'tools/PortBossGuidance20261014/RunValidation.ps1') @courseArguments
