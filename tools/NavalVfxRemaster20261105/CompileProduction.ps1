param(
    [ValidatePattern('^[a-zA-Z0-9_-]+$')][string]$Run = 'compile1',
    [string]$ReferenceResponses = 'D:/Game/Artifacts/CardNumbering20261017/UiCompile',
    [string]$UnityData = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data',
    [string]$PlayModeResponse = 'D:/Game/UnityClient/Library/Bee/artifacts/1900b0aE.dag/ShatteredSail.Presentation.PlayModeTests.rsp',
    [string]$EditModeResponse = 'D:/Game/UnityClient/Library/Bee/artifacts/1900b0aE.dag/ShatteredSail.Core.Tests.rsp',
    [switch]$IncludePlayMode,
    [switch]$IncludeEditMode,
    [switch]$IncludeCapture,
    [switch]$PrepareOnly
)
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$main = Join-Path $workspace 'UnityClient'
$taskRoot = Join-Path $workspace 'outputs/naval-vfx-remaster-20261105'
$output = [IO.Path]::GetFullPath((Join-Path $taskRoot ('compile/' + $Run)))
$outputSlash = $output.Replace('\','/')
$referenceSlash = [IO.Path]::GetFullPath($ReferenceResponses).Replace('\','/')
$assemblies = @('Core','Content','Persistence','Presentation')
$sources = [Collections.Generic.List[IO.FileInfo]]::new()
function Assert-Output {
    $expected = [IO.Path]::GetFullPath((Join-Path $taskRoot ('compile/' + $Run)))
    if ($output -ne $expected) { throw 'Unexpected compiler output.' }
    for ($at=$output; $at -and $at.Length -ge $workspace.Length; $at=Split-Path $at -Parent) {
        if ((Test-Path -LiteralPath $at) -and ((Get-Item -LiteralPath $at -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw ('Linked compiler path: '+$at) }
    }
}
function Write-Response([string]$Name, [string[]]$Lines) {
    foreach ($line in @($Lines | Where-Object { $_ -match '^-(out|refout|pdb):' })) {
        if (!$line.Contains($outputSlash+'/')) { throw ('Compiler destination escapes task: '+$line) }
    }
    [IO.File]::WriteAllLines((Join-Path $output ($Name+'.rsp')),$Lines,[Text.UTF8Encoding]::new($false))
}
Assert-Output
if (Test-Path -LiteralPath $output) { throw 'Choose a new compile run; prior evidence is preserved.' }
New-Item -ItemType Directory -Path $output -Force | Out-Null
Assert-Output
foreach ($assembly in $assemblies) {
    $response = @([IO.File]::ReadAllLines((Join-Path $ReferenceResponses ($assembly+'.rsp'))) | ForEach-Object { $_.Replace($referenceSlash,$outputSlash) })
    $pattern = '^"?(?:'+[Regex]::Escape($main.Replace('\','/')+'/')+')?Assets/_Project/'+$assembly+'/Runtime/.*\.cs"?$'
    $response = @($response | Where-Object { $_ -notmatch $pattern })
    $current = @(Get-ChildItem -LiteralPath (Join-Path $main ('Assets/_Project/'+$assembly+'/Runtime')) -File -Filter '*.cs' -Recurse | Sort-Object FullName)
    if (!$current.Count) { throw ('No production sources: '+$assembly) }
    foreach($file in $current) { $sources.Add($file) }
    $response += @($current | ForEach-Object { '"'+$_.FullName.Replace('\','/')+'"' })
    if ($response | Where-Object { $_.Contains($referenceSlash) }) { throw 'Old compiler outputs remain referenced.' }
    Write-Response $assembly $response
}
foreach ($test in @(
    @{Enabled=$IncludeEditMode;Name='Core.Tests';Folder='EditMode';Reference=$EditModeResponse},
    @{Enabled=$IncludePlayMode;Name='Presentation.PlayModeTests';Folder='PlayMode';Reference=$PlayModeResponse}
)) {
    if (!$test.Enabled) { continue }
    $pattern = '^"?(?:'+[Regex]::Escape($main.Replace('\','/')+'/')+')?Assets/_Project/Tests/'+$test.Folder+'/.*\.cs"?$'
    $response = @([IO.File]::ReadAllLines($test.Reference) | Where-Object { $_ -notmatch $pattern } | ForEach-Object {
        if ($_ -match '^-(out|refout|pdb):') {
            $kind=$Matches[1]
            $suffix=if($kind -eq 'refout'){'.ref.dll'}elseif($kind -eq 'pdb'){'.pdb'}else{'.dll'}
            '-'+$kind+':"'+$outputSlash+'/ShatteredSail.'+$test.Name+$suffix+'"'
        } elseif ($_ -match '^-r:"[^"]*/(ShatteredSail\.(Core|Content|Persistence|Presentation)\.ref\.dll)"$') {
            '-r:"'+$outputSlash+'/'+$Matches[1]+'"'
        } else { $_ }
    })
    $current = @(Get-ChildItem -LiteralPath (Join-Path $main ('Assets/_Project/Tests/'+$test.Folder)) -File -Filter '*.cs' -Recurse | Sort-Object FullName)
    foreach($file in $current) { $sources.Add($file) }
    $response += @($current | ForEach-Object { '"'+$_.FullName.Replace('\','/')+'"' })
    Write-Response $test.Name $response
    $assemblies += $test.Name
}
if($IncludeCapture) {
    $editorResponse=Join-Path $main 'Library/Bee/artifacts/1900b0aE.dag/ShatteredSail.Editor.rsp'
    $response=@([IO.File]::ReadAllLines($editorResponse) | Where-Object { $_ -notmatch '^"?[^-@].*\.cs"?$' } | ForEach-Object {
        if($_ -match '^-(out|refout|pdb):') {
            $kind=$Matches[1];$suffix=if($kind -eq 'refout'){'.ref.dll'}elseif($kind -eq 'pdb'){'.pdb'}else{'.dll'}
            '-'+$kind+':"'+$outputSlash+'/ShatteredSail.NavalVfxCapture'+$suffix+'"'
        } elseif($_ -match '^-r:"[^"]*/(ShatteredSail\.(Core|Content|Persistence|Presentation)\.ref\.dll)"$') {
            '-r:"'+$outputSlash+'/'+$Matches[1]+'"'
        } else { $_ }
    })
    $captureSource=Get-Item -LiteralPath (Join-Path $PSScriptRoot 'Capture.cs')
    $sources.Add($captureSource);$response+='"'+$captureSource.FullName.Replace('\','/')+'"'
    Write-Response 'NavalVfxCapture' $response
    $assemblies+='NavalVfxCapture'
}
$hashes = @($sources | ForEach-Object { [pscustomobject]@{Source=$_.FullName;SHA256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash} })
$hashes | Export-Csv -LiteralPath (Join-Path $output 'source-hashes.csv') -NoTypeInformation -Encoding utf8
if($PrepareOnly) { Write-Output ('Prepared responses only: '+$output); return }
$results = [Collections.Generic.List[string]]::new()
try {
    Push-Location $main
    try {
        foreach($assembly in $assemblies) {
            $log=Join-Path $output ($assembly+'.log')
            & (Join-Path $UnityData 'NetCoreRuntime/dotnet.exe') (Join-Path $UnityData 'DotNetSdkRoslyn/csc.dll') ('@'+(Join-Path $output ($assembly+'.rsp'))) 2>&1 | Tee-Object -FilePath $log
            if($LASTEXITCODE -ne 0) { throw ('Production compilation failed: '+$assembly) }
            $results.Add('PASS '+$assembly)
            Write-Output ('PASS '+$assembly)
        }
    } finally { Pop-Location }
    foreach($entry in $hashes) {
        if((Get-FileHash -LiteralPath $entry.Source -Algorithm SHA256).Hash -ne $entry.SHA256) { throw ('Source changed during compile: '+$entry.Source) }
    }
    $results.Add('PASS source hashes stable; Unity Editor and main project were never written by validation.')
    [IO.File]::WriteAllLines((Join-Path $output 'validation.txt'),$results,[Text.UTF8Encoding]::new($false))
} finally {
    Assert-Output
    $consumers=@(Get-CimInstance Win32_Process -Filter "Name='mono.exe' OR Name='dotnet.exe'" | Where-Object { $_.CommandLine -and $_.CommandLine.Replace('\','/').Contains($outputSlash) })
    if($consumers.Count) { throw 'Compiler outputs still in use; preserve pending cleanup.' }
    $freed=0L; $removed=@()
    foreach($assembly in $assemblies) {
        foreach($suffix in @('.dll','.pdb','.ref.dll')) {
            $file=[IO.Path]::GetFullPath((Join-Path $output ('ShatteredSail.'+$assembly+$suffix)))
            if([IO.Path]::GetDirectoryName($file) -ne $output) { throw 'Unexpected artifact cleanup target.' }
            if(!(Test-Path -LiteralPath $file)) { continue }
            $item=Get-Item -LiteralPath $file -Force
            if($item.PSIsContainer -or ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw ('Unexpected compiler artifact: '+$file) }
            $freed+=$item.Length
            Remove-Item -LiteralPath $file -Force
            if(Test-Path -LiteralPath $file) { throw ('Artifact cleanup failed: '+$file) }
            $removed+=$item.Name
        }
    }
    [pscustomobject]@{ReleasedBytes=$freed;RemovedFiles=$removed;EvidenceRetained=$true;MainProjectCleaned=$false;SharedReferencesRetained=$ReferenceResponses} | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $output 'cleanup.json') -Encoding utf8
    Write-Output ('Task compiler artifacts cleaned: '+$freed+' bytes; logs and source hashes retained.')
}
Write-Output ('PASS production validation: '+$output)
