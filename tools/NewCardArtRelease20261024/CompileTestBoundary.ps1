param([string]$RunTag='TestBoundary01')
$ErrorActionPreference='Stop'
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..')).TrimEnd('\','/')
$source=Join-Path $workspace 'UnityClient'
$isolated=Join-Path $workspace 'tmp/new-card-art-release-20261024-01'
$output=Join-Path $workspace ('Artifacts/NewCardArtRelease20261024/'+$RunTag)
if(Test-Path -LiteralPath $output){throw 'Use a fresh compile evidence directory.'}
[IO.Directory]::CreateDirectory($output)|Out-Null
$oldCompile=Join-Path $workspace 'Artifacts/BoardingFieldBalance20261024/Compile'
$coreSources=@(Get-ChildItem -LiteralPath (Join-Path $source 'Assets/_Project/Core/Runtime') -File -Filter '*.cs'|Sort-Object Name)
$core=@([IO.File]::ReadAllLines((Join-Path $oldCompile 'Core.rsp'))|Where-Object{$_ -notmatch '^"?.*\.cs"?$'}|ForEach-Object{$_.Replace($oldCompile.Replace('\','/'),$output.Replace('\','/'))})
$core+=@($coreSources|ForEach-Object{'"'+$_.FullName.Replace('\','/')+'"'})
$response=Get-ChildItem -LiteralPath (Join-Path $isolated 'Library/Bee/artifacts') -Recurse -File -Filter 'ShatteredSail.Core.Tests.rsp'|Sort-Object LastWriteTime -Descending|Select-Object -First 1
$tests=@([IO.File]::ReadAllLines($response.FullName)|Where-Object{$_ -notmatch '^"?.*\.cs"?$' -and $_ -notmatch '^-(out|refout|analyzer):' -and $_ -notmatch '^/additionalfile:' -and $_ -notmatch '^-r:.*ShatteredSail\.Core\.ref\.dll' })
$tests+='-out:"'+(Join-Path $output 'ShatteredSail.Core.Tests.dll').Replace('\','/')+'"'
$tests+='-r:"'+(Join-Path $output 'ShatteredSail.Core.ref.dll').Replace('\','/')+'"'
$testSources=@(Get-ChildItem -LiteralPath (Join-Path $source 'Assets/_Project/Tests/EditMode') -File -Filter '*.cs' -Recurse|Sort-Object FullName)
$tests+=@($testSources|ForEach-Object{'"'+$_.FullName.Replace('\','/')+'"'})
$player=@($core|Where-Object{$_ -notmatch '^-define:UNITY_EDITOR'}|ForEach-Object{$_.Replace('ShatteredSail.Core.dll','ShatteredSail.Core.Player.dll').Replace('ShatteredSail.Core.ref.dll','ShatteredSail.Core.Player.ref.dll')})
$runs=@(@('Core',$core),@('Tests',$tests),@('CorePlayer',$player))
Push-Location $isolated
try{
    foreach($run in $runs){
        $path=Join-Path $output ($run[0]+'.rsp')
        [IO.File]::WriteAllLines($path,[string[]]$run[1],[Text.UTF8Encoding]::new($false))
        & 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data/NetCoreRuntime/dotnet.exe' 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data/DotNetSdkRoslyn/csc.dll' ('@'+$path) 2>&1|Tee-Object -FilePath (Join-Path $output ($run[0]+'.log'))
        if($LASTEXITCODE -ne 0){throw ('Assembly compilation failed: '+$run[0])}
        Write-Output ('PASS '+$run[0])
    }
}finally{Pop-Location}
$changed=@(Import-Csv -LiteralPath (Join-Path $workspace 'Artifacts/NewCardArtRelease20261024/Snapshot01/source-manifest.csv')|
    Where-Object{$_.Path.Replace('\','/') -like 'Assets/_Project/Core/Runtime/*.cs'}|
    Where-Object{(Get-FileHash -LiteralPath (Join-Path $source $_.Path)).Hash -ne $_.SHA256})
if($changed.Count){throw 'Existing gameplay Core sources changed.'}
[pscustomobject]@{Result='PASS';CoreSources=$coreSources.Count;EditModeSources=$testSources.Count;OriginalCoreSourcesChanged=$changed.Count;NewSource='CoreTestAssemblyAccess.cs';Scope='Independent Core and real ShatteredSail.Core.Tests assemblies compile; Player symbol variant compiles without UNITY_EDITOR.'}|ConvertTo-Json|Tee-Object -FilePath (Join-Path $output 'validation.json')
