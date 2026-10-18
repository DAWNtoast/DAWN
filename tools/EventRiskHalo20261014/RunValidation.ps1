param(
    [ValidateSet('Sync','PlayMode')][string]$Stage='Sync',
    [string]$RunTag='Current',
    [switch]$ExcludeUnrelatedCrewTest,
    [string[]]$AdditionalPlayModeSources=@()
)
$ErrorActionPreference='Stop'
$haloWorkspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if($Stage-eq'Sync'){
    foreach($haloPart in @('Core/Runtime','Content/Runtime','Persistence/Runtime','Presentation/Runtime','Tests/PlayMode')){
        foreach($haloMeta in Get-ChildItem -LiteralPath (Join-Path $haloWorkspace ('UnityClient/Assets/_Project/'+$haloPart)) -Filter '*.cs.meta' -File -Recurse){
            if((Get-Content -LiteralPath $haloMeta.FullName -Raw)-notmatch'(?m)^guid: [0-9a-fA-F]{32}\r?$'){throw ('Malformed source C# meta GUID: '+$haloMeta.FullName)}
        }
    }
}
$haloArguments=@{
    Stage=$Stage;RunTag=$RunTag;ArtifactDirectory='Artifacts/EventRiskHalo20261014';
    TestFilter='EventRiskHalo_';AdditionalPlayModeSources=$AdditionalPlayModeSources;
    ExcludeUnrelatedCrewTest=$ExcludeUnrelatedCrewTest
}
& (Join-Path $haloWorkspace 'tools/PortBossGuidance20261014/RunValidation.ps1') @haloArguments
if($Stage-eq'Sync'){
    'Real Unity PlayMode visual/interaction validation for ordinary event risk halos only. Full Runtime compilation in the owned isolation project. Controlled visible safe/danger ordinary events, Boss comparison, undiscovered and completed state, actual pointer target selection. No duplicated production risk classifier and no unrelated suite.'|
        Set-Content -LiteralPath (Join-Path $haloWorkspace ('Artifacts/EventRiskHalo20261014/'+$RunTag+'/scope.txt')) -Encoding utf8
}
