param([string]$Output = 'D:/Game/Artifacts/NarrativeTextFix20261022/Tests')
$ErrorActionPreference = 'Stop'
$narrativeWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
& (Join-Path $PSScriptRoot 'PrepareValidation.ps1')
$narrativeConfig = Get-Content -LiteralPath (Join-Path $narrativeWorkspace 'tmp/narrative-component-validation-20261022/validation.json') -Raw | ConvertFrom-Json
& (Join-Path $narrativeWorkspace 'tools/ReleaseBuild/RunTests.ps1') -Platform EditMode -ProjectPath $narrativeConfig.ProjectPath -Output $Output -TestFilter $narrativeConfig.TestFilter
