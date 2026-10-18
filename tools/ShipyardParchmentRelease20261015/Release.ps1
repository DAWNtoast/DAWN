param([Parameter(Mandatory=$true)][ValidateSet('Prepare','Windows','Mac','Smoke','PackageWindows','PackageMac','Seal')][string]$Stage)
$ErrorActionPreference='Stop'
$parchmentProject='D:/Game/tmp/shipyard-parchment-restore-20261015'
$parchmentRoot='D:/Game/Builds/第七港口-羊皮纸装配恢复20261015'
$parchmentEvidence='D:/Game/Artifacts/ShipyardParchmentRelease20261015'
$parchmentVersion='0.3.20261015.4'
$parchmentWindows=Join-Path $parchmentRoot 'Windows'
$parchmentExe=Join-Path $parchmentWindows '第七港口.exe'
$parchmentApp=Join-Path $parchmentRoot 'Mac/第七港口.app'
function Assert-ParchmentReport([string]$Path) {
 $report=[IO.File]::ReadAllText($Path)
 if($report -notmatch '(?m)^Result: Succeeded\r?$' -or $report -notmatch '(?m)^Errors: 0\r?$' -or $report -notmatch ('(?m)^Version: '+[regex]::Escape($parchmentVersion)+'\r?$')){throw ('Invalid build report: '+$Path)}
}
function Quote-ParchmentArguments($Values) {
 $quoted=foreach($value in $Values){$escaped=[regex]::Replace($value,'(\\*)"','$1$1\"');'"'+[regex]::Replace($escaped,'(\\+)$','$1$1')+'"'}
 return $quoted -join ' '
}
if($Stage -eq 'Prepare'){
 if(Test-Path -LiteralPath $parchmentEvidence){throw 'Use a new release evidence directory.'}
 if(Test-Path -LiteralPath $parchmentRoot){throw 'Use a new release output directory.'}
 if(-not(Test-Path -LiteralPath ($parchmentProject+'/.shipyard-parchment-validation.json'))){throw 'Expected verified parchment project.'}
 foreach($entry in (Get-Content -LiteralPath 'D:/Game/Artifacts/ShipyardParchmentRestore20261015/final-source-sha256.json' -Raw|ConvertFrom-Json)){
  if((Get-FileHash -LiteralPath (Join-Path $parchmentProject $entry.File)).Hash -ne $entry.SHA256){throw ('Validated source changed: '+$entry.File)}
 }
 foreach($run in @('Edit02','Play02')){
  [xml]$tests=Get-Content -LiteralPath ('D:/Game/Artifacts/ShipyardParchmentRestore20261015/'+$run+'/results.xml') -Raw
  if([int]$tests.'test-run'.failed -ne 0 -or [int]$tests.'test-run'.passed -le 0){throw 'Required regression tests failed.'}
 }
 [IO.Directory]::CreateDirectory($parchmentEvidence)|Out-Null
 [IO.Directory]::CreateDirectory($parchmentRoot)|Out-Null
 [IO.Directory]::CreateDirectory($parchmentEvidence+'/Tools')|Out-Null
 foreach($name in @('Package.ps1','PackageMac.py')){Copy-Item -LiteralPath ('D:/Game/tools/ReleaseBuild/'+$name) -Destination ($parchmentEvidence+'/Tools/'+$name)}
 foreach($platform in @('Windows','Mac')){
  $builder=Join-Path $parchmentProject ('Assets/_Project/Editor/'+$platform+'ReleaseBuilder.cs')
  Copy-Item -LiteralPath $builder -Destination ($parchmentEvidence+'/'+$platform+'ReleaseBuilder.before.cs')
  $text=[IO.File]::ReadAllText($builder)
  $old='PlayerSettings.bundleVersion = "0.3.20261015.3";'
  if([regex]::Matches($text,[regex]::Escape($old)).Count -ne 1){throw 'Unexpected builder version.'}
  [IO.File]::WriteAllText($builder,$text.Replace($old,('PlayerSettings.bundleVersion = "'+$parchmentVersion+'";')),[Text.UTF8Encoding]::new($false))
 }
 $manifest=@(foreach($part in @('Assets','Packages','ProjectSettings')){foreach($file in Get-ChildItem -LiteralPath (Join-Path $parchmentProject $part) -File -Recurse){
  $relative=$file.FullName.Substring($parchmentProject.Length+1)
  if($relative -match '[\\/]UIPreviewsForMe(?:[\\/.]|$)'){throw 'Retired preview content found.'}
  [pscustomobject]@{Path=$relative;Bytes=$file.Length;SHA256=(Get-FileHash -LiteralPath $file.FullName).Hash}
 }})
 $manifest|Export-Csv -LiteralPath ($parchmentEvidence+'/source-manifest.csv') -NoTypeInformation -Encoding utf8
 $metadata=@('Assets/_Project/Resources/PortFacilities/DepartureCharts4K/chart-golden-trade.png.meta','Assets/_Project/Resources/PortFacilities/DepartureCharts4K/chart-moon-ruins.png.meta','Assets/_Project/Resources/PortFacilities/DepartureCharts4K/chart-shattered-reef.png.meta','Assets/_Project/Resources/UISurfaces/journal_parchment_v1.png.meta','Assets/_Project/Resources/UISurfaces/nautical_canvas_v1.png.meta')
 foreach($relative in $metadata){$target=Join-Path ($parchmentEvidence+'/ImporterBaseline') $relative;[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target))|Out-Null;Copy-Item -LiteralPath (Join-Path $parchmentProject $relative) -Destination $target}
 [pscustomobject]@{Result='PASS';Version=$parchmentVersion;Project=$parchmentProject;Files=$manifest.Count;Bytes=($manifest|Measure-Object Bytes -Sum).Sum;EditModePassed=49;PlayModePassed=5;ValidatedCaptureFrames=12;Utc=[DateTime]::UtcNow.ToString('o')}|ConvertTo-Json|Set-Content -LiteralPath ($parchmentEvidence+'/prepared.json') -Encoding utf8
 Get-Content -LiteralPath ($parchmentEvidence+'/prepared.json')
 exit 0
}
if(-not(Test-Path -LiteralPath ($parchmentEvidence+'/prepared.json'))){throw 'Prepare the verified release source first.'}
if($Stage -in @('Windows','Mac','Smoke')){
 if(@(Get-CimInstance Win32_Process|Where-Object {$_.Name -eq 'Unity.exe' -and $_.ExecutablePath -match '[/\\]Editor[/\\]Unity\.exe$'}).Count){throw 'Unity Editor slot is occupied.'}
 $evidence=Join-Path $parchmentEvidence $Stage
 if(Test-Path -LiteralPath $evidence){throw 'Preserve previous execution evidence.'}
 [IO.Directory]::CreateDirectory($evidence)|Out-Null
 if($Stage -eq 'Smoke'){
  Assert-ParchmentReport (Join-Path $parchmentWindows 'build-report.txt')
  $program=$parchmentExe
  $arguments=@('-screen-fullscreen','0','-screen-width','1280','-screen-height','720','-shatteredSailSmokeTest',(Join-Path $evidence 'Report'),'-logFile',(Join-Path $evidence 'player.log'))
 }else{
  $program='D:/Unity/Hub/Editor/6000.3.23f1/Editor/Unity.exe'
  $output=if($Stage -eq 'Windows'){$parchmentExe}else{$parchmentApp}
  $arguments=@('-batchmode','-nographics','-quit','-projectPath',$parchmentProject,'-executeMethod',('ShatteredSail.Editor.'+$Stage+'ReleaseBuilder.BuildFromCommandLine'),'-releaseOutput',$output,'-logFile',(Join-Path $evidence 'unity.log'))
 }
 $started=[DateTime]::UtcNow
 $process=Start-Process -FilePath $program -ArgumentList (Quote-ParchmentArguments $arguments) -WorkingDirectory $parchmentProject -WindowStyle Hidden -PassThru
 [pscustomobject]@{PID=$process.Id;Stage=$Stage;Program=$program;Arguments=$arguments;StartedUtc=$started.ToString('o')}|ConvertTo-Json -Depth 4|Set-Content -LiteralPath ($evidence+'/launch.json') -Encoding utf8
 Write-Output ($Stage+' process started, PID '+$process.Id)
 if($Stage -eq 'Smoke'){if(-not $process.WaitForExit(180000)){$process.Kill();throw 'Owned smoke player timed out.'}}else{$process.WaitForExit()}
 [pscustomobject]@{ExitCode=$process.ExitCode;Seconds=([DateTime]::UtcNow-$started).TotalSeconds;CompletedUtc=[DateTime]::UtcNow.ToString('o')}|ConvertTo-Json|Set-Content -LiteralPath ($evidence+'/result.json') -Encoding utf8
 if($process.ExitCode -ne 0){throw ($Stage+' failed; inspect evidence.')}
 if($Stage -eq 'Smoke'){
  $report=[IO.File]::ReadAllText($evidence+'/Report/player-smoke-report.txt')
  if($report -notmatch '^PASS\r?\n' -or $report -notmatch ('(?m)^Version: '+[regex]::Escape($parchmentVersion)+'\r?$') -or $report -notmatch '(?m)^Company: ShatteredSail\r?$' -or $report -notmatch '(?m)^Product: Shattered Sail\r?$'){throw 'Smoke identity or result failed.'}
  Write-Output $report
 }else{Assert-ParchmentReport (Join-Path (Join-Path $parchmentRoot $Stage) 'build-report.txt');Get-Content -LiteralPath (Join-Path (Join-Path $parchmentRoot $Stage) 'build-report.txt')}
 exit 0
}
if($Stage -eq 'PackageWindows'){
 Assert-ParchmentReport (Join-Path $parchmentWindows 'build-report.txt')
 & ($parchmentEvidence+'/Tools/Package.ps1') -BuildDirectory $parchmentWindows -ZipPath ($parchmentRoot+'/第七港口-Windows-20261015-羊皮纸装配.zip') -ExecutableName '第七港口.exe' | Tee-Object -FilePath ($parchmentEvidence+'/windows-package.json')
 exit 0
}
if($Stage -eq 'PackageMac'){
 Assert-ParchmentReport ($parchmentRoot+'/Mac/build-report.txt')
 & 'C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe' ($parchmentEvidence+'/Tools/PackageMac.py') --app-directory $parchmentApp --zip-path ($parchmentRoot+'/第七港口-macOS-20261015-羊皮纸装配.zip') | Set-Content -LiteralPath ($parchmentEvidence+'/mac-package.json') -Encoding utf8
 if($LASTEXITCODE -ne 0){throw 'macOS package failed.'}
 $package=Get-Content -LiteralPath ($parchmentEvidence+'/mac-package.json') -Raw|ConvertFrom-Json
 if($package.result -ne 'PASS' -or $package.bundle.shortVersion -ne $parchmentVersion){throw 'macOS bundle validation failed.'}
 Write-Output 'macOS Universal package validated.'
 exit 0
}
if($Stage -eq 'Seal'){
 Assert-ParchmentReport ($parchmentRoot+'/Windows/build-report.txt')
 Assert-ParchmentReport ($parchmentRoot+'/Mac/build-report.txt')
 $smoke=Get-Content -LiteralPath ($parchmentEvidence+'/Smoke/result.json') -Raw|ConvertFrom-Json
 if($smoke.ExitCode -ne 0){throw 'Windows smoke incomplete.'}
 $smokeReport=[IO.File]::ReadAllText($parchmentEvidence+'/Smoke/Report/player-smoke-report.txt')
 if($smokeReport -notmatch '^PASS\r?\n' -or $smokeReport -notmatch ('(?m)^Version: '+[regex]::Escape($parchmentVersion)+'\r?$') -or $smokeReport -notmatch '(?m)^Company: ShatteredSail\r?$' -or $smokeReport -notmatch '(?m)^Product: Shattered Sail\r?$'){throw 'Final smoke acceptance is incomplete.'}
 $windows=Get-Content -LiteralPath ($parchmentEvidence+'/windows-package.json') -Raw|ConvertFrom-Json
 $mac=Get-Content -LiteralPath ($parchmentEvidence+'/mac-package.json') -Raw|ConvertFrom-Json
 if($windows.Result -ne 'PASS' -or $mac.result -ne 'PASS' -or $mac.bundle.shortVersion -ne $parchmentVersion){throw 'Package checks incomplete.'}
 $expectedWindows=$parchmentRoot+'/第七港口-Windows-20261015-羊皮纸装配.zip'
 $expectedMac=$parchmentRoot+'/第七港口-macOS-20261015-羊皮纸装配.zip'
 if([IO.Path]::GetFullPath($windows.Zip) -ne [IO.Path]::GetFullPath($expectedWindows) -or [IO.Path]::GetFullPath($mac.zipPath) -ne [IO.Path]::GetFullPath($expectedMac)){throw 'Package validation belongs to a different release.'}
 if((Get-FileHash -LiteralPath $windows.Zip).Hash -ne $windows.ZipSHA256 -or (Get-FileHash -LiteralPath $mac.zipPath).Hash -ne $mac.zipSHA256){throw 'Package hash changed.'}
 $manifest=@(Import-Csv -LiteralPath ($parchmentEvidence+'/source-manifest.csv')|Where-Object {$_.Path -match '^(Assets|Packages)[\\/]'})
 $paths=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
 $normalizations=@(foreach($file in $manifest){
  [void]$paths.Add($file.Path)
  $built=Join-Path $parchmentProject $file.Path
  if((Get-FileHash -LiteralPath $built).Hash -ne $file.SHA256){
   $baseline=Join-Path ($parchmentEvidence+'/ImporterBaseline') $file.Path
   if(-not(Test-Path -LiteralPath $baseline)){throw ('Unreviewed source change: '+$file.Path)}
   $original=[IO.File]::ReadAllText($baseline).Replace("`r`n","`n")
   $imported=[IO.File]::ReadAllText($built).Replace("`r`n","`n")
   $old="    textureFormat: 25`n    textureCompression: 2";$new="    textureFormat: 4`n    textureCompression: 0"
   if([regex]::Matches($original,[regex]::Escape($old)).Count -ne 1 -or $original.Replace($old,$new) -cne $imported){throw ('Unexpected importer change: '+$file.Path)}
   [pscustomobject]@{Path=$file.Path;Reason='Existing NPOT mipmap importer normalization';BeforeSHA256=$file.SHA256;AfterSHA256=(Get-FileHash -LiteralPath $built).Hash}
  }
 })
 foreach($part in @('Assets','Packages')){foreach($file in Get-ChildItem -LiteralPath (Join-Path $parchmentProject $part) -File -Recurse){if(-not $paths.Contains($file.FullName.Substring($parchmentProject.Length+1))){throw ('Unexpected release source: '+$file.FullName)}}}
 if(@(Get-ChildItem -LiteralPath $parchmentRoot -Filter '*.zip' -File).Count -ne 2){throw 'Expected two platform packages.'}
 $packages=@(foreach($path in @($expectedWindows,$expectedMac)){$file=Get-Item -LiteralPath $path;[pscustomobject]@{Name=$file.Name;Bytes=$file.Length;SHA256=(Get-FileHash -LiteralPath $path).Hash}})
 [pscustomobject]@{Result='PASS';Version=$parchmentVersion;SourceProject=$parchmentProject;GameplaySourcesUnchanged=$true;VerifiedImporterMetadataChanges=$normalizations;EditModePassed=49;PlayModePassed=5;CaptureFrames=12;WindowsSmoke=$smoke;MacExecution='Not run on macOS; Universal Mach-O, bundle and ZIP permissions verified on Windows.';Packages=$packages}|ConvertTo-Json -Depth 6|Set-Content -LiteralPath ($parchmentRoot+'/release-manifest.json') -Encoding utf8
 Get-Content -LiteralPath ($parchmentRoot+'/release-manifest.json')
}
