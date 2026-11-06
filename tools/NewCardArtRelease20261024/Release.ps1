param(
    [Parameter(Mandatory=$true)]
    [ValidateSet('Plan','Snapshot','EditMode','PlayMode','Verify','Windows','Smoke','Package','Seal')]
    [string]$Stage,
    [ValidatePattern('^[0-9]{2}$')][string]$Revision = '01',
    [ValidatePattern('^\d+\.\d+\.\d+\.\d+$')][string]$Version = '0.3.20261024.1',
    [ValidateRange(1,240)][int]$TimeoutMinutes = 90
)
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..')).TrimEnd('\','/')
$source = Join-Path $workspace 'UnityClient'
$runRoot = Join-Path $workspace 'Artifacts/NewCardArtRelease20261024'
$frozen = Join-Path $runRoot ('FrozenSource' + $Revision + '/UnityClient')
$project = Join-Path $workspace ('tmp/new-card-art-release-20261024-' + $Revision)
$snapshot = Join-Path $runRoot ('Snapshot' + $Revision)
$edit = Join-Path $runRoot ('Edit' + $Revision)
$play = Join-Path $runRoot ('Play' + $Revision)
$verify = Join-Path $runRoot ('Verification' + $Revision)
$release = Join-Path $workspace ('Builds/第七港口-新卡卡图20261024-r' + $Revision)
$entry = Join-Path $workspace 'tools/ReleaseBuild/DesktopRelease.ps1'
$arguments = @{
    ReleaseDirectory = $release
    EvidenceDirectory = $snapshot
    Version = $Version
    ZipFileName = '第七港口-Windows-新卡卡图20261024.zip'
    ValidationSummary = (Join-Path $verify 'verification-summary.json')
    ProjectPath = $project
    SourceProjectPath = $frozen
}

function Assert-InWorkspace([string]$Path) {
    $fullPath = [IO.Path]::GetFullPath($Path)
    if (-not $fullPath.StartsWith($workspace + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) {
        throw ('Path escaped workspace: ' + $fullPath)
    }
}

if ($Stage -eq 'Plan') {
    [pscustomobject]@{
        Version=$Version; Source=$source; FrozenSource=$frozen; Project=$project;
        Snapshot=$snapshot; EditMode=$edit; PlayMode=$play; Verification=$verify;
        Release=$release; Executable=(Join-Path $release 'Windows/第七港口.exe');
        Package=(Join-Path $release $arguments.ZipFileName);
        Stages=@('Snapshot','EditMode','PlayMode','Verify','Windows','Smoke','Package','Seal');
        SourceChanges='None; source is copied into a fresh immutable release mirror.';
        GitChanges='None; this wrapper does not commit or push.'
    } | ConvertTo-Json -Depth 3
    exit 0
}

if ($Stage -eq 'Snapshot') {
    foreach ($path in @($frozen,$project,$snapshot,$release,$edit,$play,$verify)) {
        Assert-InWorkspace $path
        if (Test-Path -LiteralPath $path) { throw ('Use a fresh revision; preserve existing directory: ' + $path) }
    }
    $retired = Join-Path $source 'Assets/_Project/Scenes/UIPreviewsForMe'
    if ((Test-Path -LiteralPath $retired) -and @(Get-ChildItem -LiteralPath $retired -Force).Count) {
        throw 'The retired preview directory must remain empty.'
    }
    & (Join-Path $workspace 'tools/ReleaseBuild/AssertMetaGuids.ps1') -AssetsPath (Join-Path $source 'Assets')
    $manifest = [Collections.Generic.List[object]]::new()
    foreach ($part in @('Assets','Packages','ProjectSettings')) {
        foreach ($file in Get-ChildItem -LiteralPath (Join-Path $source $part) -File -Recurse -Force) {
            if ($file.FullName -match '[\\/]UIPreviewsForMe(?:[\\/.]|$)') { continue }
            $relative = $file.FullName.Substring($source.Length+1)
            $target = Join-Path $frozen $relative
            Assert-InWorkspace $target
            $originalHash = (Get-FileHash -LiteralPath $file.FullName).Hash
            [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target)) | Out-Null
            Copy-Item -LiteralPath $file.FullName -Destination $target
            if ((Get-FileHash -LiteralPath $target).Hash -ne $originalHash -or
                (Get-FileHash -LiteralPath $file.FullName).Hash -ne $originalHash) {
                throw ('Source changed while freezing release inputs: ' + $relative)
            }
            $manifest.Add([pscustomobject]@{ Path=$relative; Bytes=$file.Length; SHA256=$originalHash })
        }
    }
    $manifest | Export-Csv -LiteralPath (Join-Path $runRoot ('frozen-source-' + $Revision + '.csv')) -NoTypeInformation -Encoding utf8
    foreach ($part in @('Assets','Packages','ProjectSettings')) {
        [IO.Directory]::CreateDirectory((Join-Path $project $part)) | Out-Null
    }
    Copy-Item -LiteralPath (Join-Path $frozen 'ProjectSettings/ProjectVersion.txt') -Destination (Join-Path $project 'ProjectSettings/ProjectVersion.txt')
    & $entry -Stage Snapshot @arguments
    exit $LASTEXITCODE
}

if ($Stage -in @('EditMode','PlayMode')) {
    $destination = if ($Stage -eq 'EditMode') { $edit } else { $play }
    & (Join-Path $workspace 'tools/ReleaseBuild/RunTests.ps1') -Platform $Stage -ProjectPath $project -Output $destination -SourceManifest (Join-Path $snapshot 'source-manifest.csv') -TimeoutMinutes $TimeoutMinutes
    exit $LASTEXITCODE
}

if ($Stage -eq 'Verify') {
    & (Join-Path $workspace 'tools/ReleaseBuild/Verify.ps1') -EditRun $edit -PlayRun $play -SnapshotDirectory $snapshot -OutputDirectory $verify
    exit 0
}

& $entry -Stage $Stage @arguments
exit $LASTEXITCODE
