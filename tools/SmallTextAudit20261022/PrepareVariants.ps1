param([Parameter(Mandatory = $true)][string]$ProjectPath)
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'Prepare.ps1') -Project $ProjectPath
