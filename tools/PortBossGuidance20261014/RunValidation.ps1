param(
    [ValidateSet('Sync','Import','PlayMode','EditMode')][string]$Stage='Sync',
    [string]$RunTag='Current',
    [string]$TestFilter='',
    [switch]$ExcludeUnrelatedCrewTest,
    [string]$ArtifactDirectory='Artifacts/PortBossGuidance20261014',
    [string[]]$AdditionalEditModeSources=@(),
    [string[]]$AdditionalPlayModeSources=@(),
    [string[]]$RetireValidationSources=@(),
    [string]$UnityEditor='D:/Unity/Hub/Editor/6000.3.23f1/Editor/Unity.exe'
)
$ErrorActionPreference='Stop'
if($RunTag -notmatch '^[A-Za-z0-9_-]+$'){throw 'RunTag must be a simple identifier.'}
$guideWorkspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$guideSource=Join-Path $guideWorkspace 'UnityClient'
$guideProject=Join-Path $guideWorkspace 'tmp/port-boss-guidance-20261014'
$guideOutput=[IO.Path]::GetFullPath((Join-Path $guideWorkspace $ArtifactDirectory))
if(-not$guideOutput.StartsWith((Join-Path $guideWorkspace 'Artifacts')+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Artifacts must stay inside the workspace Artifacts directory.'}
$guideRunOutput=Join-Path $guideOutput $RunTag
$guideMarker=Join-Path $guideProject 'PORT_BOSS_GUIDANCE_VALIDATION_20261014.txt'
if((Test-Path -LiteralPath $guideProject)-and-not(Test-Path -LiteralPath $guideMarker)){throw 'Refuse to use an unowned isolation directory.'}
if(Test-Path -LiteralPath (Join-Path $guideProject 'Temp/UnityLockfile')){
    try{$guideLock=[IO.File]::Open((Join-Path $guideProject 'Temp/UnityLockfile'),[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::None);$guideLock.Dispose()}catch{throw 'This dedicated isolation project is already running.'}
}
New-Item -ItemType Directory -Force -Path $guideProject,$guideOutput,$guideRunOutput | Out-Null
'Owned by tools/PortBossGuidance20261014/RunValidation.ps1. Only active runtime, current scenes and explicitly selected assets; no retired preview contents.'|Set-Content -LiteralPath $guideMarker -Encoding utf8
$guideInputs=[Collections.Generic.List[object]]::new()
function Copy-GuideFile([string]$from,[string]$relative){
    if($relative -match 'UIPreviewsForMe'){throw 'Retired preview content is forbidden.'}
    $to=[IO.Path]::GetFullPath((Join-Path $guideProject $relative))
    if(-not $to.StartsWith($guideProject+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Copy target escaped dedicated project.'}
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($to))|Out-Null
    $hash=(Get-FileHash -LiteralPath $from -Algorithm SHA256).Hash
    $guideInputs.Add([pscustomobject]@{Source=$from;Destination=$to;SHA256=$hash})
    if((Test-Path -LiteralPath $to)-and(Get-Item -LiteralPath $from).Length-eq(Get-Item -LiteralPath $to).Length-and(Get-FileHash -LiteralPath $to -Algorithm SHA256).Hash-eq$hash){return}
    Copy-Item -LiteralPath $from -Destination $to -Force
    if((Get-FileHash -LiteralPath $to -Algorithm SHA256).Hash-ne$hash){throw "Source changed during copy: $from"}
}
function Copy-GuidePart([string]$relative,[switch]$CodeOnly){
    $directory=Join-Path $guideSource $relative
    foreach($file in Get-ChildItem -LiteralPath $directory -File -Recurse){
        if($CodeOnly-and$file.Extension-notin@('.cs','.asmdef','.meta')){continue}
        if($ExcludeUnrelatedCrewTest-and$file.Name-in@('DepartureCrewOverlayFlowTests.cs','DepartureCrewOverlayFlowTests.cs.meta')){continue}
        Copy-GuideFile $file.FullName $file.FullName.Substring($guideSource.Length+1)
    }
    if(Test-Path -LiteralPath ($directory+'.meta')){Copy-GuideFile ($directory+'.meta') ($relative+'.meta')}
}
if($Stage-eq'Sync'){
    foreach($relative in $RetireValidationSources){
        $retired=[IO.Path]::GetFullPath((Join-Path $guideProject $relative))
        $testRoot=Join-Path $guideProject 'Assets/_Project/Tests'
        if(-not$retired.StartsWith($testRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)-or[IO.Path]::GetExtension($retired)-ne'.cs'){throw 'Only explicit owned isolation test sources can be retired.'}
        if(Test-Path -LiteralPath $retired){Remove-Item -LiteralPath $retired}
        if(Test-Path -LiteralPath ($retired+'.meta')){Remove-Item -LiteralPath ($retired+'.meta')}
    }
    foreach($part in @('Core/Runtime','Content/Runtime','Persistence/Runtime','Presentation/Runtime','Tests/PlayMode')){Copy-GuidePart ('Assets/_Project/'+$part) -CodeOnly}
    foreach($fixtureSource in $AdditionalPlayModeSources){
        $fixtureFile=Get-Item -LiteralPath $fixtureSource
        Copy-GuideFile $fixtureFile.FullName ('Assets/_Project/Tests/PlayMode/'+$fixtureFile.Name)
        if(Test-Path -LiteralPath ($fixtureFile.FullName+'.meta')){Copy-GuideFile ($fixtureFile.FullName+'.meta') ('Assets/_Project/Tests/PlayMode/'+$fixtureFile.Name+'.meta')}
    }
    if($ExcludeUnrelatedCrewTest){
        foreach($name in @('DepartureCrewOverlayFlowTests.cs','DepartureCrewOverlayFlowTests.cs.meta')){
            $excluded=[IO.Path]::GetFullPath((Join-Path $guideProject ('Assets/_Project/Tests/PlayMode/'+$name)))
            if(-not$excluded.StartsWith($guideProject+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Excluded test escaped dedicated project.'}
            if(Test-Path -LiteralPath $excluded){Remove-Item -LiteralPath $excluded}
        }
        'Only unrelated DepartureCrewOverlayFlowTests.cs is excluded from this isolated validation. Concurrent source currently applies Assert.That to void RestoreTreasureProgress (CS1503). Main sources and all Runtime files are unchanged and fully compiled.'|Set-Content -LiteralPath (Join-Path $guideRunOutput 'excluded-test.txt') -Encoding utf8
    }
    foreach($fixture in @('CoreDestinationBossGuidanceTests.cs','CoreDestinationBossGuidanceTests.cs.meta')){
        $relative='Assets/_Project/Tests/EditMode/'+$fixture
        Copy-GuideFile (Join-Path $guideSource $relative) $relative
    }
    Copy-GuideFile (Join-Path $PSScriptRoot 'PortBossGuidanceJsonTests.cs') 'Assets/_Project/Tests/EditMode/PortBossGuidanceJsonTests.cs'
    foreach($fixtureSource in $AdditionalEditModeSources){
        $fixtureFile=Get-Item -LiteralPath $fixtureSource
        Copy-GuideFile $fixtureFile.FullName ('Assets/_Project/Tests/EditMode/'+$fixtureFile.Name)
        if(Test-Path -LiteralPath ($fixtureFile.FullName+'.meta')){Copy-GuideFile ($fixtureFile.FullName+'.meta') ('Assets/_Project/Tests/EditMode/'+$fixtureFile.Name+'.meta')}
    }
    @{name='ShatteredSail.PortBossGuidanceValidation.EditMode';references=@('ShatteredSail.Core','ShatteredSail.Content','ShatteredSail.Persistence','ShatteredSail.Presentation','Unity.UGUI');includePlatforms=@('Editor');optionalUnityReferences=@('TestAssemblies');autoReferenced=$false}|ConvertTo-Json -Depth 4|Set-Content -LiteralPath (Join-Path $guideProject 'Assets/_Project/Tests/EditMode/PortBossGuidanceValidation.asmdef') -Encoding utf8
    foreach($scene in @('MainMenu','CoreGameplayDemo','TutorialVoyage')){foreach($extension in @('.unity','.unity.meta')){$relative='Assets/_Project/Scenes/'+$scene+$extension;Copy-GuideFile (Join-Path $guideSource $relative) $relative}}
    # Navigation, live port startup, shared HUD, active ship and audio dependencies only.
    $guideResources=@('Fonts','UI','UIWood','UISurfaces','LegacyDemo','VoyageMapV2','NavigationUIV3','NavigationLookout','PortArt','PortFacilities','PortHeaders','PortIcons','ShipArtV2','ModularShip','ComponentArt','ItemIcons','Data','Shaders','MainMenu','EncounterV2','AchievementIcons','Audio','OpeningComics','BattleEnvironments')
    foreach($resource in $guideResources){Copy-GuidePart ('Assets/_Project/Resources/'+$resource)}
    foreach($file in Get-ChildItem -LiteralPath (Join-Path $guideSource 'ProjectSettings') -File){Copy-GuideFile $file.FullName ('ProjectSettings/'+$file.Name)}
    foreach($relative in @('Assets/Settings/InputSystem_Actions.inputactions','Assets/Settings/InputSystem_Actions.inputactions.meta')){Copy-GuideFile (Join-Path $guideSource $relative) $relative}
    $guideSettings=Join-Path $guideProject 'ProjectSettings/ProjectSettings.asset'
    $guideSettingsText=[IO.File]::ReadAllText($guideSettings)
    $guideSettingsText=[regex]::Replace($guideSettingsText,'(?m)^  companyName:.*$','  companyName: CodexPortBossGuidanceValidation')
    $guideSettingsText=[regex]::Replace($guideSettingsText,'(?m)^  productName:.*$','  productName: PortBossGuidance20261014')
    [IO.File]::WriteAllText($guideSettings,$guideSettingsText,[Text.UTF8Encoding]::new($false))
    # Use existing local package sources; recursively include their dependencies.
    $guidePackageCache=Join-Path $guideSource 'Library/PackageCache'
    $guidePackages=@{}
    foreach($directory in Get-ChildItem -LiteralPath $guidePackageCache -Directory){$json=Join-Path $directory.FullName 'package.json';if(Test-Path -LiteralPath $json){$definition=Get-Content -LiteralPath $json -Raw|ConvertFrom-Json;$guidePackages[$definition.name]=[pscustomobject]@{Directory=$directory.FullName;Definition=$definition}}}
    $guideDependencies=@{}
    $guideOriginalManifest=Get-Content -LiteralPath (Join-Path $guideSource 'Packages/manifest.json') -Raw|ConvertFrom-Json
    foreach($property in $guideOriginalManifest.dependencies.PSObject.Properties){if($property.Name.StartsWith('com.unity.modules.')){$guideDependencies[$property.Name]=$property.Value}}
    $guideQueue=[Collections.Generic.Queue[string]]::new()
    foreach($package in @('com.unity.test-framework','com.unity.ext.nunit','com.unity.inputsystem','com.unity.ugui')){$guideQueue.Enqueue($package)}
    while($guideQueue.Count-gt0){$package=$guideQueue.Dequeue();if($guideDependencies.ContainsKey($package)){continue};if(-not$guidePackages.ContainsKey($package)){throw "Required cached package missing: $package"};$cached=$guidePackages[$package];$guideDependencies[$package]='file:'+$cached.Directory.Replace('\','/');foreach($dependency in $cached.Definition.dependencies.PSObject.Properties){if(-not$guideDependencies.ContainsKey($dependency.Name)){$guideQueue.Enqueue($dependency.Name)}}}
    [IO.Directory]::CreateDirectory((Join-Path $guideProject 'Packages'))|Out-Null
    @{dependencies=$guideDependencies}|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $guideProject 'Packages/manifest.json') -Encoding utf8
    $guideInputs|Export-Csv -LiteralPath (Join-Path $guideRunOutput 'sync-input-hashes.csv') -NoTypeInformation -Encoding utf8
    $guideResources|Set-Content -LiteralPath (Join-Path $guideRunOutput 'resource-directories.txt') -Encoding utf8
    Write-Output ('Synchronized '+$guideInputs.Count+' inputs into '+$guideProject)
    return
}
if(-not(Test-Path -LiteralPath (Join-Path $guideProject 'Assets/_Project/Scenes/CoreGameplayDemo.unity'))){throw 'Run Stage Sync first.'}
$guideArguments=@('-batchmode','-projectPath',$guideProject,'-logFile',(Join-Path $guideRunOutput ($Stage+'.log')))
if($Stage-eq'Import'){$guideArguments+=@('-quit')}
else{
    if(-not$TestFilter){$TestFilter=if($Stage-eq'PlayMode'){'DestinationBossGuidance_;DestinationPort_;NavigationArrival_OrdinaryStop;NavigationArrival_EventAndResolution'}else{'CoreDestinationBossGuidanceTests;PortBossGuidanceJsonTests'}}
    $guideArguments+=@('-runTests','-testPlatform',$Stage,'-testFilter',$TestFilter,'-testResults',(Join-Path $guideRunOutput ($Stage+'.xml')))
    if($Stage-eq'PlayMode'){$guideScreens=Join-Path $guideRunOutput 'Screens';if(Test-Path -LiteralPath $guideScreens){throw 'Use a new RunTag for captures; never overwrite prior images.'};$guideArguments+=@('-navalVisualCapturePath',$guideScreens)}
}
$guideQuotedArguments=$guideArguments|ForEach-Object{'"'+$_.Replace('"','\"')+'"'}
$guideProcess=Start-Process -FilePath $UnityEditor -ArgumentList $guideQuotedArguments -WindowStyle Hidden -PassThru
@{processId=$guideProcess.Id;arguments=$guideArguments;startedUtc=[DateTime]::UtcNow.ToString('o')}|ConvertTo-Json -Depth 4|Set-Content -LiteralPath (Join-Path $guideRunOutput ($Stage+'-launch.json')) -Encoding utf8
Write-Output ('Unity PID '+$guideProcess.Id+' '+$Stage)
$guideProcess.WaitForExit()
$guideProcess.Refresh()
if($guideProcess.ExitCode-ne0){throw ('Unity '+$Stage+' failed with exit '+$guideProcess.ExitCode+'. Inspect its log.')}
if($Stage-ne'Import'){
    [xml]$guideResults=Get-Content -LiteralPath (Join-Path $guideRunOutput ($Stage+'.xml'))
    Write-Output ($Stage+': '+$guideResults.'test-run'.result+' total='+$guideResults.'test-run'.total+' passed='+$guideResults.'test-run'.passed+' failed='+$guideResults.'test-run'.failed)
    if($guideResults.'test-run'.failed-ne'0'-or[int]$guideResults.'test-run'.total-le0){throw 'No passing test result was produced.'}
}else{Write-Output 'Initial graphics-enabled import and all-runtime compilation completed.'}
