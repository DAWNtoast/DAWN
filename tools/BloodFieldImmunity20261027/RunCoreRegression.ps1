$ErrorActionPreference = 'Stop'
$bloodWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$bloodTag = 'BloodFieldImmunity20261027'
$bloodOutput = Join-Path $bloodWorkspace ('Artifacts/BossCalibration20261024/' + $bloodTag)
$bloodBuild = [IO.Path]::GetFullPath((Join-Path $bloodOutput 'Build'))
try {
    & (Join-Path $bloodWorkspace 'tools/BossCalibration20261024/RunCoreTests.ps1') -RunTag $bloodTag -Fixtures @(
        'CoreBloodFieldRevision20261024Tests', 'CoreBloodNavalRevision20261024Tests',
        'CoreBloodCatalogRevisionTests', 'CoreComponentFieldCardsTests',
        'CoreBattleCardChineseTextTests', 'CoreNavalFieldsTests',
        'CoreNavalDualField20261026Tests', 'CoreBattleStatusPresentationTests'
    )
}
finally {
    $bloodFreed = [long]0
    if (Test-Path -LiteralPath $bloodBuild) {
        $bloodExpected = [IO.Path]::GetFullPath((Join-Path $bloodWorkspace 'Artifacts/BossCalibration20261024/BloodFieldImmunity20261027/Build'))
        if ($bloodBuild -ne $bloodExpected -or -not $bloodBuild.StartsWith($bloodWorkspace + [IO.Path]::DirectorySeparatorChar)) {
            throw 'Refusing cleanup outside the exact task Build directory.'
        }
        $bloodAncestor = Get-Item -LiteralPath $bloodBuild -Force
        while ($null -ne $bloodAncestor) {
            if (($bloodAncestor.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw 'Refusing cleanup through a reparse point.' }
            $bloodAncestor = $bloodAncestor.Parent
        }
        $bloodItems = @(Get-ChildItem -LiteralPath $bloodBuild -Force -Recurse)
        if (@($bloodItems | Where-Object { ($_.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0 }).Count -gt 0) {
            throw 'Refusing cleanup with linked Build contents.'
        }
        $bloodUsers = @(Get-CimInstance Win32_Process | Where-Object {
            $_.Name -in @('mono.exe', 'dotnet.exe', 'Unity.exe') -and $_.CommandLine -and
            $_.CommandLine.IndexOf($bloodBuild, [StringComparison]::OrdinalIgnoreCase) -ge 0
        })
        if ($bloodUsers.Count -gt 0) { throw 'Build is still used by a compiler or runtime.' }
        $bloodFreed = [long](($bloodItems | Where-Object { -not $_.PSIsContainer } | Measure-Object -Property Length -Sum).Sum)
        Remove-Item -LiteralPath $bloodBuild -Recurse -Force
    }
    if (Test-Path -LiteralPath $bloodOutput) {
        [pscustomobject]@{
            ReleasedBytes = $bloodFreed
            MainProjectCleaned = $false
            BuildRemaining = (Test-Path -LiteralPath $bloodBuild)
            EvidenceRetained = (Test-Path -LiteralPath (Join-Path $bloodOutput 'source-hashes.csv'))
        } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $bloodOutput 'cleanup.json') -Encoding utf8
    }
}
