$ErrorActionPreference = 'Stop'
$crewProject = 'D:\Game\tmp\card-clarity-20261013'
$crewOutput = 'D:\Game\Builds\CrewAtlasComparison20261013\Windows\第七港口.exe'
$crewLog = 'D:\Game\Artifacts\BrokenAnchorCrewAtlas4K20261013\verification\player-build.log'
if (@(Get-Process -Name Unity -ErrorAction SilentlyContinue).Count -gt 0) {
    throw 'A Unity editor is still running; finish validation before building.'
}
if (Test-Path -LiteralPath (Join-Path $crewProject 'Temp\UnityLockfile')) {
    throw 'Validation project is locked.'
}
if (Test-Path -LiteralPath $crewOutput) { throw 'The comparison player already exists.' }
$crewArgs = @('-batchmode','-quit','-force-d3d11','-buildTarget','Win64',
    '-projectPath',$crewProject,'-logFile',$crewLog,
    '-executeMethod','ShatteredSail.Editor.WindowsReleaseBuilder.BuildFromCommandLine',
    '-releaseOutput',$crewOutput)
$crewQuoted = $crewArgs | ForEach-Object { '"' + $_.Replace('"','\"') + '"' }
$crewBuild = Start-Process -FilePath 'D:\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe' -ArgumentList $crewQuoted -WindowStyle Hidden -PassThru
Write-Output ('Building comparison player PID=' + $crewBuild.Id)
$crewBuild.WaitForExit()
$crewBuild.Refresh()
if ($crewBuild.ExitCode -ne 0) { throw ('Comparison build failed, exit=' + $crewBuild.ExitCode) }
Get-Content -LiteralPath (Join-Path (Split-Path -Parent $crewOutput) 'build-report.txt')
