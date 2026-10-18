param(
    [string]$RunTag='CurrentCore',
    [string[]]$AdditionalFixtures=@('CoreDirectCourseTargetTests.cs','CrewSpeedRulesTests.cs')
)
$ErrorActionPreference='Stop'
$speedWorkspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
& (Join-Path $speedWorkspace 'tools/CourseTargetLock20261014/RunCoreValidation.ps1') `
    -RunTag $RunTag -ArtifactDirectory 'Artifacts/SpeedBasedVoyage20261014' -AdditionalFixtures $AdditionalFixtures
