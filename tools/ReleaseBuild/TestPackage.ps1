param(
    [string]$OutputDirectory = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$auditWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$auditAllowedRoot = [IO.Path]::GetFullPath((Join-Path $auditWorkspace 'tmp'))
if (-not $OutputDirectory) {
    $OutputDirectory = Join-Path $auditAllowedRoot ('PackageGuardAudit-' + [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfff') + '-' + [Guid]::NewGuid().ToString('N'))
}
$auditRoot = [IO.Path]::GetFullPath($OutputDirectory)
if (-not $auditRoot.StartsWith($auditAllowedRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Use a new test directory under workspace tmp.'
}
if (Test-Path -LiteralPath $auditRoot) { throw "Refusing existing test output: $auditRoot" }
New-Item -ItemType Directory -Path $auditRoot | Out-Null
$auditScript = Join-Path $PSScriptRoot 'Package.ps1'
$auditResults = [Collections.Generic.List[object]]::new()

function New-PackageFixture([string]$Name, [string]$Target = '', [string]$Damage = '', [string]$Report = "Result: Succeeded`nErrors: 0`n", [string]$ExeName = 'ShatteredSail.exe') {
    $build = Join-Path (Join-Path $auditRoot $Name) 'Build'
    New-Item -ItemType Directory -Path $build | Out-Null
    $dependencies = @(
        @{ Id = 'exe'; Name = $ExeName; Directory = $false; Content = 'fixture executable' },
        @{ Id = 'player'; Name = 'UnityPlayer.dll'; Directory = $false; Content = 'fixture player library' },
        @{ Id = 'data'; Name = [IO.Path]::GetFileNameWithoutExtension($ExeName) + '_Data'; Directory = $true; Content = 'fixture assets' },
        @{ Id = 'mono'; Name = 'MonoBleedingEdge'; Directory = $true; Content = 'fixture runtime' },
        @{ Id = 'report'; Name = 'build-report.txt'; Directory = $false; Content = $Report }
    )
    foreach ($dependency in $dependencies) {
        $affected = $dependency.Id -eq $Target
        if ($affected -and $Damage -eq 'missing') { continue }
        $isDirectory = $dependency.Directory
        if ($affected -and $Damage -eq 'wrong-type') { $isDirectory = -not $isDirectory }
        $path = Join-Path $build $dependency.Name
        if ($isDirectory) {
            New-Item -ItemType Directory -Path $path | Out-Null
            if (-not ($affected -and $Damage -eq 'empty')) {
                [IO.File]::WriteAllText((Join-Path $path 'fixture.bin'), $dependency.Content)
            }
        } else {
            [IO.File]::WriteAllText($path, $(if ($affected -and $Damage -eq 'empty') { '' } else { $dependency.Content }))
        }
    }
    return $build
}

function Assert-PackageResult([string]$Name, [string]$Build, [bool]$ExpectedPass, [string]$ExeName = 'ShatteredSail.exe', [string]$Zip = '') {
    if (-not $Zip) { $Zip = Join-Path (Split-Path $Build -Parent) 'package.zip' }
    $result = $null
    $failure = $null
    try { $result = & $auditScript -BuildDirectory $Build -ZipPath $Zip -ExecutableName $ExeName | ConvertFrom-Json }
    catch { $failure = $_.Exception.Message }
    if ($ExpectedPass) {
        if ($failure -or $null -eq $result -or $result.Result -cne 'PASS' -or $result.FilesVerified -ne 5) {
            throw "$Name did not package and verify all five fixture files: $failure"
        }
    } else {
        if (-not $failure) { throw "$Name was accepted unexpectedly." }
        if ($Name -ne 'existing-zip' -and (Test-Path -LiteralPath $Zip)) { throw "$Name created an archive before rejecting the input." }
    }
    $auditResults.Add([ordered]@{ name = $Name; passed = $true; expectedPass = $ExpectedPass; message = $failure; package = $result })
    Write-Output "PASS $Name"
}

foreach ($target in @('exe', 'player', 'data', 'mono', 'report')) {
    foreach ($damage in @('missing', 'wrong-type', 'empty')) {
        $name = $target + '-' + $damage
        $build = New-PackageFixture $name $target $damage
        Assert-PackageResult $name $build $false
    }
}
foreach ($case in @(
    @{ Name = 'failed-report'; Report = "Result: Failed`nErrors: 1`n" },
    @{ Name = 'embedded-success'; Report = "Previous Result: Succeeded`nResult: Failed`nErrors: 4`n" },
    @{ Name = 'duplicate-result'; Report = "Result: Succeeded`nResult: Failed`n" },
    @{ Name = 'duplicate-success'; Report = "Result: Succeeded`nResult: Succeeded`n" },
    @{ Name = 'success-suffix'; Report = "Result: SucceededButInvalid`n" }
)) {
    $build = New-PackageFixture $case.Name '' '' $case.Report
    Assert-PackageResult $case.Name $build $false
}
$build = New-PackageFixture 'valid-default'
Assert-PackageResult 'valid-default' $build $true
$build = New-PackageFixture 'valid-chinese-name' '' '' "Result: Succeeded`r`nErrors: 0`r`n" '第七港口.exe'
Assert-PackageResult 'valid-chinese-name' $build $true '第七港口.exe'
$build = New-PackageFixture 'self-inclusion'
Assert-PackageResult 'self-inclusion' $build $false 'ShatteredSail.exe' (Join-Path $build 'package.zip')
$build = New-PackageFixture 'existing-zip'
$existingZip = Join-Path (Split-Path $build -Parent) 'package.zip'
[IO.File]::WriteAllText($existingZip, 'retained previous archive')
$previousHash = (Get-FileHash -LiteralPath $existingZip -Algorithm SHA256).Hash
Assert-PackageResult 'existing-zip' $build $false 'ShatteredSail.exe' $existingZip
if ((Get-FileHash -LiteralPath $existingZip -Algorithm SHA256).Hash -cne $previousHash) { throw 'Existing archive changed.' }
$build = New-PackageFixture 'executable-path'
Assert-PackageResult 'executable-path' $build $false '../ShatteredSail.exe'
$build = New-PackageFixture 'build-file'
Assert-PackageResult 'build-file' (Join-Path $build 'ShatteredSail.exe') $false

[ordered]@{
    passed = $auditResults.Count
    failed = 0
    packageScriptSha256 = (Get-FileHash -LiteralPath $auditScript -Algorithm SHA256).Hash
    scope = 'Packaging guards and complete ZIP byte verification using isolated synthetic fixtures; does not claim playable Unity runtime validation.'
    cases = $auditResults.ToArray()
} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $auditRoot 'summary.json') -Encoding utf8
Write-Output "Package guard audit: $($auditResults.Count) passed. Evidence: $auditRoot"
