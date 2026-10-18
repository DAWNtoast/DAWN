param(
    [ValidateSet('Sync','PlayMode','EditMode')][string]$Stage='Sync',
    [string]$RunTag='Current',
    [string]$TestFilter='',
    [switch]$ExcludeUnrelatedCrewTest,
    [string[]]$AdditionalEditModeSources=@(),
    [string[]]$AdditionalPlayModeSources=@()
)
$ErrorActionPreference='Stop'
$speedWorkspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if($Stage-eq'Sync'){
    foreach($speedPart in @('Core/Runtime','Content/Runtime','Persistence/Runtime','Presentation/Runtime','Tests/PlayMode')){
        foreach($speedMeta in Get-ChildItem -LiteralPath (Join-Path $speedWorkspace ('UnityClient/Assets/_Project/'+$speedPart)) -Filter '*.cs.meta' -File -Recurse){
            if((Get-Content -LiteralPath $speedMeta.FullName -Raw)-notmatch'(?m)^guid: [0-9a-fA-F]{32}\r?$'){throw ('Malformed source C# meta GUID: '+$speedMeta.FullName)}
        }
    }
}
elseif(-not$TestFilter){throw 'An explicit task-specific test filter is required.'}
$speedArguments=@{
    Stage=$Stage;RunTag=$RunTag;ArtifactDirectory='Artifacts/SpeedBasedVoyage20261014';TestFilter=$TestFilter;
    AdditionalEditModeSources=$AdditionalEditModeSources;AdditionalPlayModeSources=$AdditionalPlayModeSources;
    ExcludeUnrelatedCrewTest=$ExcludeUnrelatedCrewTest;
    RetireValidationSources=@('Assets/_Project/Tests/EditMode/CourseTargetLockJsonTests.cs','Assets/_Project/Tests/PlayMode/EventRiskHaloVisualTests.cs')
}
& (Join-Path $speedWorkspace 'tools/PortBossGuidance20261014/RunValidation.ps1') @speedArguments
if($Stage-eq'Sync'){
    'Full current Runtime compilation, selected dynamic voyage-distance/target-lock/port/checkpoint and live UI validation. Default 12-hour and maximum 24-hour distance must follow the authoritative current sailing speed. Real JSON compatibility is verified with Unity JsonUtility. Prior task-only CourseTargetLockJsonTests and EventRiskHaloVisualTests are removed only from the owned isolation project to prevent stale API dependencies; their original tool files and evidence are preserved. No old event-density benchmark, player package, retired preview generator or player profile is used.'|
        Set-Content -LiteralPath (Join-Path $speedWorkspace ('Artifacts/SpeedBasedVoyage20261014/'+$RunTag+'/scope.txt')) -Encoding utf8
}
