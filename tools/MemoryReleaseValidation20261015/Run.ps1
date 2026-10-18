param(
 [Parameter(Mandatory=$true)][ValidateSet('EditMode','PlayMode','Windows','Mac')][string]$Stage,
 [Parameter(Mandatory=$true)][ValidatePattern('^[A-Za-z0-9_-]+$')][string]$Name,
 [string]$TestFilter='',
 [string]$ReleaseOutput=''
)
$ErrorActionPreference='Stop'
$validationProject='D:/Game/tmp/runtime-memory-release-20261014'
$validationOutput=Join-Path 'D:/Game/Artifacts/MemoryRelease20261015/Validation' $Name
if(-not(Test-Path -LiteralPath (Join-Path $validationProject '.runtime-memory-release-validation.json'))){throw 'Owned validation clone required.'}
if(@(Get-CimInstance Win32_Process | Where-Object {$_.Name -eq 'Unity.exe' -or $_.Name -eq 'MemoryAudit.exe'}).Count){throw 'Unity/Player measurement slot is occupied.'}
if(Test-Path -LiteralPath $validationOutput){throw 'Use a new output directory; previous evidence is preserved.'}
[IO.Directory]::CreateDirectory($validationOutput)|Out-Null
$validationArgs=@('-batchmode','-force-d3d11','-projectPath',$validationProject,'-logFile',(Join-Path $validationOutput 'unity.log'))
if($Stage -in @('EditMode','PlayMode')) {
 $validationArgs+=@('-runTests','-testPlatform',$Stage,'-testResults',(Join-Path $validationOutput 'results.xml'))
 if($TestFilter){$validationArgs+=@('-testFilter',$TestFilter)}
 if($Stage -eq 'PlayMode'){$validationArgs+=@('-audioCapturePath',(Join-Path $validationOutput 'AudioCapture'))}
} else {
 if(-not [IO.Path]::IsPathRooted($ReleaseOutput)){throw 'Absolute release output required.'}
 $validationMethod=if($Stage -eq 'Windows'){'ShatteredSail.Editor.WindowsReleaseBuilder.BuildFromCommandLine'}else{'ShatteredSail.Editor.MacReleaseBuilder.BuildFromCommandLine'}
 $validationArgs+=@('-nographics','-quit','-executeMethod',$validationMethod,'-releaseOutput',$ReleaseOutput)
}
$validationQuoted=foreach($validationArg in $validationArgs){$validationEscaped=[regex]::Replace($validationArg,'(\\*)"','$1$1\"');'"'+[regex]::Replace($validationEscaped,'(\\+)$','$1$1')+'"'}
$validationProcess=Start-Process -FilePath 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Unity.exe' -ArgumentList ($validationQuoted -join ' ') -WorkingDirectory $validationProject -WindowStyle Hidden -PassThru
[pscustomobject]@{Stage=$Stage;PID=$validationProcess.Id;StartedUtc=[DateTime]::UtcNow.ToString('o');Arguments=$validationArgs}|ConvertTo-Json -Depth 4|Set-Content -LiteralPath (Join-Path $validationOutput 'launch.json') -Encoding utf8
Write-Output ('Started '+$Stage+' PID '+$validationProcess.Id+'; inspect results and process completion before continuing.')
$validationProcess.WaitForExit()
[pscustomobject]@{Stage=$Stage;ExitCode=$validationProcess.ExitCode;CompletedUtc=[DateTime]::UtcNow.ToString('o')}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $validationOutput 'completion.json') -Encoding utf8
if($validationProcess.ExitCode -ne 0){throw ('Unity '+$Stage+' failed; evidence preserved.')}
if($Stage -in @('EditMode','PlayMode')){
 $validationResults=Join-Path $validationOutput 'results.xml'
 if(-not(Test-Path -LiteralPath $validationResults)){throw 'Unity exited without a test report.'}
 [xml]$validationXml=Get-Content -LiteralPath $validationResults -Raw
 $validationRun=$validationXml.'test-run'
 if([int]$validationRun.failed -ne 0 -or [int]$validationRun.passed -le 0){throw 'Test report did not pass.'}
 Write-Output ($Stage+': '+$validationRun.passed+' passed; '+$validationRun.skipped+' skipped; '+$validationRun.failed+' failed.')
}
