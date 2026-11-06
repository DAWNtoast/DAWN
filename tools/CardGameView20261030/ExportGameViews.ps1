param(
    [string]$Output='D:/Game/outputs/card-list-by-school-20261023',
    [string]$Unity='D:/Unity/Hub/Editor/6000.3.23f1/Editor/Unity.exe',
    [switch]$Force,
    [ValidateRange(0,1017)][int]$Limit=0
)
$ErrorActionPreference='Stop'
$captureWorkspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$captureOutput=[IO.Path]::GetFullPath($Output)
$captureRoot=Join-Path $captureWorkspace 'outputs/card-game-view-20261030'
$captureId=[Guid]::NewGuid().ToString('N')
$captureEvidence=Join-Path $captureRoot ('runs/'+$captureId)
$captureProject=Join-Path $captureWorkspace ('tmp/card-game-view-20261030-'+$captureId)
$captureBuild=Join-Path $captureEvidence 'PlanBuild'
$captureMain=Join-Path $captureWorkspace 'UnityClient/Assets/_Project/Resources'
$captureUnityData=Join-Path (Split-Path -Parent $Unity) 'Data'
$captureProcess=$null
$captureProjectCreated=$false
$captureSuccess=$false
foreach($captureAncestor in @($captureWorkspace,(Join-Path $captureWorkspace 'outputs'),(Join-Path $captureWorkspace 'tmp'))) {
    if((Test-Path -LiteralPath $captureAncestor) -and ((Get-Item -LiteralPath $captureAncestor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) {throw 'Capture refuses directory links.'}
}
New-Item -ItemType Directory -Force -Path $captureOutput,$captureEvidence,$captureBuild | Out-Null
try {
    # Plan with current core definitions and the production frame-path methods.
    $captureCatalogSource=[IO.File]::ReadAllText((Join-Path $captureWorkspace 'UnityClient/Assets/_Project/Presentation/Runtime/CardSchoolArtCatalog.cs'))
    $captureStart=$captureCatalogSource.IndexOf('        public static string NormalizeSchool(string schoolId)')
    $captureEnd=$captureCatalogSource.IndexOf('        public static Texture2D Frame(string schoolId')
    if($captureStart -lt 0 -or $captureEnd -le $captureStart){throw 'Review changed production frame-path method boundaries.'}
    $captureTemplate=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'CapturePlan.cs.template'))
    $captureGenerated=Join-Path $captureBuild 'CapturePlan.cs'
    [IO.File]::WriteAllText($captureGenerated,$captureTemplate.Replace('/* CURRENT_FRAME_PATH_METHODS */',$captureCatalogSource.Substring($captureStart,$captureEnd-$captureStart)),[Text.UTF8Encoding]::new($false))
    $captureSources=@(Get-ChildItem -LiteralPath (Join-Path $captureWorkspace 'UnityClient/Assets/_Project/Core/Runtime') -Recurse -File -Filter '*.cs' | Sort-Object FullName)
    foreach($captureCostSource in @('CardSpecialCostPresentation.cs','CardSupplyCostPresentation.cs','CardOrnamentKind.cs')) {
        $captureSources+=Get-Item -LiteralPath (Join-Path $captureWorkspace ('UnityClient/Assets/_Project/Presentation/Runtime/'+$captureCostSource))
    }
    $captureExe=Join-Path $captureBuild 'CapturePlan.exe'
    $captureFramework=Join-Path $captureUnityData 'MonoBleedingEdge/lib/mono/4.7.2-api'
    $captureArgs=@((Join-Path $captureUnityData 'DotNetSdkRoslyn/csc.dll'),'-nologo','-nowarn:0649','-target:exe','-langversion:9','-noconfig','-nostdlib',('-out:'+$captureExe))
    foreach($captureAssembly in @('mscorlib.dll','System.dll','System.Core.dll','System.Xml.dll','System.Web.Extensions.dll')) {$captureArgs+='-r:'+(Join-Path $captureFramework $captureAssembly)}
    $captureArgs+=$captureSources.FullName;$captureArgs+=$captureGenerated
    & (Join-Path $captureUnityData 'NetCoreRuntime/dotnet.exe') @captureArgs 2>&1 | Tee-Object -FilePath (Join-Path $captureEvidence 'plan-compile.log')
    if($LASTEXITCODE -ne 0){throw 'Capture resource planner compilation failed.'}
    $capturePlanPath=Join-Path $captureEvidence 'capture-plan.json'
    & (Join-Path $captureUnityData 'MonoBleedingEdge/bin/mono.exe') $captureExe (Join-Path $captureOutput 'card-list-data.json') (Join-Path $captureOutput 'card-art-data.json') $capturePlanPath 2>&1 | Tee-Object -FilePath (Join-Path $captureEvidence 'plan.log')
    if($LASTEXITCODE -ne 0){throw 'Capture resource planner failed.'}
    $capturePlan=Get-Content -LiteralPath $capturePlanPath -Raw | ConvertFrom-Json
    if($Limit -gt 0) {
        $capturePlan.Cards=@($capturePlan.Cards | Select-Object -First $Limit)
        $capturePlan.Resources=@(@($capturePlan.Cards | ForEach-Object { $_.ArtResourcePath+'.png'; $_.FrameResourcePath+'.png'; $_.DefaultFrameResourcePath+'.png' })+@($capturePlan.Resources | Where-Object {$_ -notlike 'CardArt/*' -and $_ -notlike 'CardSchools/Art/*' -and $_ -notlike 'CardSchools/Frames/Integrated/*.png'}) | Sort-Object -Unique)
        $capturePlan | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $capturePlanPath -Encoding utf8
    }
    $captureFiles=@()
    foreach($captureRelative in $capturePlan.Resources) {
        foreach($captureSuffix in @('','.meta')) {
            $captureResource=[IO.Path]::GetFullPath((Join-Path $captureMain ($captureRelative+$captureSuffix)))
            if(!$captureResource.StartsWith($captureMain+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase) -or !(Test-Path -LiteralPath $captureResource -PathType Leaf)){throw ('Missing exact production resource: '+$captureResource)}
            $captureFiles += [pscustomobject]@{Relative=$captureRelative+$captureSuffix;Source=$captureResource;SHA256=(Get-FileHash -LiteralPath $captureResource -Algorithm SHA256).Hash;Bytes=(Get-Item -LiteralPath $captureResource).Length}
        }
    }
    $captureFiles | Export-Csv -LiteralPath (Join-Path $captureEvidence 'resource-hashes.csv') -NoTypeInformation -Encoding utf8
    $captureTracked=@()
    foreach($captureAssembly in @('Core','Content','Persistence','Presentation')) {$captureTracked+=@(Get-ChildItem -LiteralPath (Join-Path $captureWorkspace ('UnityClient/Assets/_Project/'+$captureAssembly+'/Runtime')) -Recurse -File -Filter '*.cs' | ForEach-Object FullName)}
    $captureTracked+=@(Get-ChildItem -LiteralPath $PSScriptRoot -File | ForEach-Object FullName)
    $captureTracked+=Join-Path $captureWorkspace 'UnityClient/ProjectSettings/ProjectSettings.asset'
    $captureTracked+=Join-Path $captureWorkspace 'UnityClient/ProjectSettings/ProjectVersion.txt'
    $captureHashes=@($captureTracked | Sort-Object -Unique | ForEach-Object {[pscustomobject]@{Source=$_;SHA256=(Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash}})
    $captureHashes | Export-Csv -LiteralPath (Join-Path $captureEvidence 'source-hashes.csv') -NoTypeInformation -Encoding utf8
    $captureFingerprintText=(@($captureHashes | ForEach-Object {$_.Source+':'+$_.SHA256})+@($captureFiles | ForEach-Object {$_.Relative+':'+$_.SHA256})+@((Get-FileHash -LiteralPath $capturePlanPath -Algorithm SHA256).Hash)) -join "`n"
    $captureHasher=[Security.Cryptography.SHA256]::Create()
    try {$captureFingerprint=[BitConverter]::ToString($captureHasher.ComputeHash([Text.Encoding]::UTF8.GetBytes($captureFingerprintText))).Replace('-','').ToLowerInvariant()} finally {$captureHasher.Dispose()}
    $captureExisting=Join-Path $captureOutput 'card-game-view-data.json'
    if(!$Force -and $Limit -eq 0 -and (Test-Path -LiteralPath $captureExisting)) {
        $captureOld=Get-Content -LiteralPath $captureExisting -Raw | ConvertFrom-Json
        if($captureOld.InputFingerprint -eq $captureFingerprint -and $captureOld.RowCount -eq $capturePlan.RowCount) {
            $captureInvalid=@($captureOld.Cards | Where-Object { $captureImage=Join-Path $captureOutput $_.Image; !(Test-Path -LiteralPath $captureImage) -or (Get-FileHash -LiteralPath $captureImage -Algorithm SHA256).Hash -ine $_.ImageSha256 })
            if($captureInvalid.Count -eq 0) {
                [pscustomobject]@{Reused=$true;InputFingerprint=$captureFingerprint;Rows=$captureOld.RowCount;Evidence=$captureEvidence} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $captureRoot 'latest-run.json') -Encoding utf8
                $captureSuccess=$true;Write-Output ('Reused '+$captureOld.RowCount+' verified production card-face captures.');return
            }
        }
    }
    $captureBytes=($captureFiles | Measure-Object Bytes -Sum).Sum
    Write-Output ('Exact isolated resources: '+$captureFiles.Count+' files incl .meta; '+$captureBytes+' bytes.')
    $captureCompany='ShatteredSailCardGameViewAudit';$captureProduct='CardGameView20261030_'+$captureId
    foreach($captureDirectory in @($captureProject,(Join-Path $captureProject 'Assets/Plugins'),(Join-Path $captureProject 'Assets/Editor'),(Join-Path $captureProject 'Assets/Resources'),(Join-Path $captureProject 'Packages'),(Join-Path $captureProject 'ProjectSettings'))) {New-Item -ItemType Directory -Path $captureDirectory -Force | Out-Null}
    $captureProjectCreated=$true
    'Task-owned actual card renderer; no gameplay source edits.' | Set-Content -LiteralPath (Join-Path $captureProject 'OWNED_CARD_GAME_VIEW.txt') -Encoding utf8
    [pscustomobject]@{Project=$captureProject;Evidence=$captureEvidence;InputFingerprint=$captureFingerprint;ResourcesBytes=$captureBytes;Rows=$capturePlan.RowCount;Limit=$Limit;Company=$captureCompany;Product=$captureProduct} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $captureEvidence 'project.json') -Encoding utf8
    $captureSettings=[IO.File]::ReadAllText((Join-Path $captureWorkspace 'UnityClient/ProjectSettings/ProjectSettings.asset'))
    $captureSettings=[Regex]::Replace($captureSettings,'(?m)^  companyName:.*$','  companyName: '+$captureCompany)
    $captureSettings=[Regex]::Replace($captureSettings,'(?m)^  productName:.*$','  productName: '+$captureProduct)
    $captureSettings=[Regex]::Replace($captureSettings,'(?m)^  productGUID:.*$','  productGUID: '+$captureId)
    [IO.File]::WriteAllText((Join-Path $captureProject 'ProjectSettings/ProjectSettings.asset'),$captureSettings,[Text.UTF8Encoding]::new($false))
    Copy-Item -LiteralPath (Join-Path $captureWorkspace 'UnityClient/ProjectSettings/ProjectVersion.txt') -Destination (Join-Path $captureProject 'ProjectSettings/ProjectVersion.txt')
    Copy-Item -LiteralPath (Join-Path $captureWorkspace 'tmp/card-keyword-tooltip-20261023-validation/Packages/manifest.json') -Destination (Join-Path $captureProject 'Packages/manifest.json')
    foreach($captureResource in $captureFiles) {
        $captureDest=Join-Path $captureProject ('Assets/Resources/'+$captureResource.Relative)
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $captureDest) | Out-Null
        Copy-Item -LiteralPath $captureResource.Source -Destination $captureDest
        if((Get-FileHash -LiteralPath $captureDest -Algorithm SHA256).Hash -ne $captureResource.SHA256){throw 'Copied resource hash differs.'}
    }
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Capture.cs') -Destination (Join-Path $captureProject 'Assets/Editor/Capture.cs')
    Copy-Item -LiteralPath $capturePlanPath -Destination (Join-Path $captureProject 'capture-plan.json')
    [pscustomobject]@{Output=$captureOutput;Evidence=$captureEvidence;InputFingerprint=$captureFingerprint;Company=$captureCompany;Product=$captureProduct;Limit=$Limit} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $captureProject 'capture-config.json') -Encoding utf8
    & (Join-Path $PSScriptRoot 'CompileProduction.ps1') -JournalPluginDirectory (Join-Path $captureProject 'Assets/Plugins')
    $captureLog=Join-Path $captureEvidence 'Unity.log'
    $captureArguments=@('-batchmode','-projectPath',('"'+$captureProject+'"'),'-executeMethod','ShatteredSail.Editor.CardGameViewCapture.Run','-quit','-logFile',('"'+$captureLog+'"'))
    $captureProcess=Start-Process -FilePath $Unity -ArgumentList $captureArguments -WindowStyle Hidden -PassThru
    $captureStarted=[DateTime]::UtcNow
    Write-Output ('Started isolated production card capture PID='+$captureProcess.Id+' log='+$captureLog)
    while(!$captureProcess.WaitForExit(1000)) {
        if(([DateTime]::UtcNow-$captureStarted).TotalMinutes -gt 30){Stop-Process -Id $captureProcess.Id -Force;$captureProcess.WaitForExit(10000) | Out-Null;throw 'Card capture exceeded its bounded run time; see log.'}
    }
    if($captureProcess.ExitCode -ne 0 -or !(Select-String -LiteralPath $captureLog -Pattern 'CARD_GAME_VIEW_CAPTURE_OK' -Quiet)){throw ('Production card capture failed; see '+$captureLog)}
    foreach($captureHash in @($captureHashes)+@($captureFiles)) {if((Get-FileHash -LiteralPath $captureHash.Source -Algorithm SHA256).Hash -ne $captureHash.SHA256){throw ('Source changed during capture: '+$captureHash.Source)}}
    $captureSuccess=$true
    [pscustomobject]@{Reused=$false;InputFingerprint=$captureFingerprint;Rows=$capturePlan.RowCount;Evidence=$captureEvidence;Limit=$Limit} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $captureRoot 'latest-run.json') -Encoding utf8
    Write-Output ('PASS actual production Unity card capture: '+$capturePlan.RowCount+' rows.')
} finally {
    $captureReleased=0L;$captureRemoved=@()
    if(Test-Path -LiteralPath $captureBuild) {
        $capturePlanItems=@(Get-ChildItem -LiteralPath $captureBuild -Force)
        if([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($captureBuild)) -ne $captureEvidence -or @($capturePlanItems | Where-Object {$_.PSIsContainer -or ($_.Attributes -band [IO.FileAttributes]::ReparsePoint) -or $_.Name -notin @('CapturePlan.cs','CapturePlan.exe')}).Count){throw 'Unsafe capture-plan cleanup.'}
        foreach($capturePlanItem in $capturePlanItems) {$captureReleased+=$capturePlanItem.Length;Remove-Item -LiteralPath $capturePlanItem.FullName -Force}
        [IO.Directory]::Delete($captureBuild,$false);$captureRemoved+=$captureBuild
    }
    if($captureProjectCreated -and (Test-Path -LiteralPath $captureProject)) {
        $captureResolved=[IO.Path]::GetFullPath($captureProject).TrimEnd('\','/')
        if([IO.Path]::GetDirectoryName($captureResolved) -ne (Join-Path $captureWorkspace 'tmp') -or [IO.Path]::GetFileName($captureResolved) -notmatch '^card-game-view-20261030-[a-f0-9]{32}$' -or !(Test-Path -LiteralPath (Join-Path $captureResolved 'OWNED_CARD_GAME_VIEW.txt'))){throw 'Unsafe isolated capture cleanup root.'}
        $captureConsumers=@(Get-CimInstance Win32_Process | Where-Object {$_.ProcessId -ne $PID -and $_.CommandLine -and $_.CommandLine.IndexOf($captureResolved,[StringComparison]::OrdinalIgnoreCase) -ge 0})
        $captureLinks=@(Get-ChildItem -LiteralPath $captureResolved -Force -Recurse | Where-Object {($_.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0})
        if($captureConsumers.Count -gt 0 -or $captureLinks.Count -gt 0){throw 'Capture project still used or contains links; retained for inspection.'}
        # Save exact small inputs; original assets are already retained and hash-indexed in the main project.
        foreach($captureSmall in @('ProjectSettings','Packages','Assets/Editor')) {
            $captureSmallSource=Join-Path $captureProject $captureSmall
            if(Test-Path -LiteralPath $captureSmallSource){$captureSmallDest=Join-Path $captureEvidence ('project-inputs/'+$captureSmall);New-Item -ItemType Directory -Force -Path (Split-Path -Parent $captureSmallDest) | Out-Null;Copy-Item -LiteralPath $captureSmallSource -Destination $captureSmallDest -Recurse}
        }
        foreach($captureName in @('Library','Temp','obj','Logs','Assets/Plugins','Assets/Resources')) {
            $captureTarget=[IO.Path]::GetFullPath((Join-Path $captureProject $captureName))
            if(!$captureTarget.StartsWith($captureResolved+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Capture cache cleanup escaped project.'}
            if(Test-Path -LiteralPath $captureTarget){$captureReleased+=(Get-ChildItem -LiteralPath $captureTarget -File -Force -Recurse | Measure-Object Length -Sum).Sum;Remove-Item -LiteralPath $captureTarget -Recurse -Force;if(Test-Path -LiteralPath $captureTarget){throw 'Capture cache was not removed.'};$captureRemoved+=$captureTarget}
        }
    }
    [pscustomobject]@{Success=$captureSuccess;ReleasedBytes=$captureReleased;Removed=$captureRemoved;MainProjectModified=$false;Retained='Source/resource hashes, small project inputs, logs, plan, final card PNGs and manifest.'} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $captureEvidence 'cleanup.json') -Encoding utf8
    Write-Output ('Cleaned task capture intermediates: '+$captureReleased+' bytes.')
}
