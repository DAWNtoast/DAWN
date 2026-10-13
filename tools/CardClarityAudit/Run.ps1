param(
    [Parameter(Mandatory=$true)][string]$Stage,
    [string]$Variant,
    [string]$TestFilter,
    [ValidateSet("EditMode","PlayMode")][string]$Platform = "EditMode"
)
$ErrorActionPreference = 'Stop'
$taskProject = 'D:\Game\tmp\card-clarity-20261013'
$taskOutput = 'D:\Game\Artifacts\CardClarity20261013'
$taskEditor = 'D:\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe'
$taskLog = Join-Path $taskOutput ($Stage + '.log')
if (Test-Path -LiteralPath $taskLog) { throw 'Use a unique stage name.' }
$taskArguments = @('-batchmode','-projectPath',$taskProject,'-logFile',$taskLog,'-force-d3d11')
if ($TestFilter) {
    $taskArguments += @('-runTests','-testPlatform',$Platform,'-testResults',(Join-Path $taskOutput ($Stage + '.xml')),'-testFilter',$TestFilter)
} else {
    $taskArguments += @('-executeMethod','ShatteredSail.Editor.CardClarityCapture.CaptureFromCommandLine','-cardClarityOutput',(Join-Path $taskOutput $Stage),'-cardClarityVariant',$Variant,'-quit')
}
$taskProcess = Start-Process -FilePath $taskEditor -ArgumentList $taskArguments -WindowStyle Hidden -PassThru
[ordered]@{ stage=$Stage; processId=$taskProcess.Id; executable=$taskEditor; arguments=$taskArguments; startedUtc=[DateTime]::UtcNow.ToString('o') } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $taskOutput ($Stage + '-launch.json')) -Encoding utf8
Write-Output ('Started '+$Stage+' PID '+$taskProcess.Id)