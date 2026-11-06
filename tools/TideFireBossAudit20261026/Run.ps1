$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskOutput=Join-Path $taskRoot 'outputs/tide-fire-boss-audit-20261026'
$taskCompile=Join-Path $taskOutput 'Compile'
$taskUnity='D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data'
New-Item -ItemType Directory -Force -Path $taskOutput,$taskCompile | Out-Null
$taskFiles=@(Get-ChildItem -LiteralPath (Join-Path $taskRoot 'UnityClient/Assets/_Project/Core/Runtime') -Filter '*.cs' -File | Sort-Object Name)
$taskHashes=@($taskFiles|ForEach-Object{[pscustomobject]@{Source=$_.FullName;SHA256=(Get-FileHash -LiteralPath $_.FullName).Hash}})
$taskHashes|Export-Csv -LiteralPath (Join-Path $taskOutput 'source-hashes.csv') -NoTypeInformation -Encoding utf8
$taskExe=Join-Path $taskCompile 'Probe.exe'
$taskArgs=@("$taskUnity/DotNetSdkRoslyn/csc.dll",'-nologo','-target:exe','-main:TideFireBossProbe','-langversion:9','-noconfig','-nostdlib',"-out:$taskExe")
foreach($taskAssembly in @('mscorlib.dll','System.dll','System.Core.dll')){$taskArgs+="-r:$taskUnity/MonoBleedingEdge/lib/mono/4.7.2-api/$taskAssembly"}
$taskArgs+=@($taskFiles.FullName)
$taskArgs+=(Join-Path $PSScriptRoot 'Probe.cs')
try {
    & "$taskUnity/NetCoreRuntime/dotnet.exe" @taskArgs 2>&1 | Tee-Object -FilePath (Join-Path $taskOutput 'compile.log')
    if($LASTEXITCODE -ne 0){throw 'Compilation failed'}
    & "$taskUnity/MonoBleedingEdge/bin/mono.exe" $taskExe $taskOutput 2>&1 | Tee-Object -FilePath (Join-Path $taskOutput 'run.log')
    if($LASTEXITCODE -ne 0){throw 'Probe failed'}
    $taskRows=Import-Csv (Join-Path $taskOutput 'boss-scenarios.csv')
    $taskRows|Group-Object boss,scenario|ForEach-Object{
        $taskGroup=$_.Group;$taskFirst=$taskGroup[0];$taskLast=$taskGroup[-1]
        [pscustomobject]@{boss=$taskFirst.boss;scenario=$taskFirst.scenario;turns=$taskGroup.Count;
            firstAttack=($taskGroup|Where-Object{$_.plan -in 'Cannon','Ram'}|Select-Object -First 1 -ExpandProperty round);
            enemyNetHullLoss=([int]$taskFirst.enemyHullBefore-[int]$taskLast.enemyHullAfter);
            playerLoss=($taskGroup|Measure-Object playerLoss -Sum).Sum;bossActions=$taskLast.bossActions}
    }|Export-Csv -LiteralPath (Join-Path $taskOutput 'boss-summary.csv') -NoTypeInformation -Encoding utf8
    foreach($taskHash in $taskHashes){if((Get-FileHash -LiteralPath $taskHash.Source).Hash -ne $taskHash.SHA256){throw "Core changed during audit: $($taskHash.Source)"}}
    'PASS: current Core source hashes remained unchanged.'|Add-Content -LiteralPath (Join-Path $taskOutput 'run.log')
} finally {
    # Only this script's exact executable is disposable. Never touch UnityClient.
    $taskResolved=[IO.Path]::GetFullPath($taskExe)
    if($taskResolved -ne [IO.Path]::GetFullPath((Join-Path $taskRoot 'outputs/tide-fire-boss-audit-20261026/Compile/Probe.exe'))){throw 'Unexpected cleanup target'}
    $taskDir=Get-Item -LiteralPath $taskCompile
    if(($taskDir.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){throw 'Do not follow a compile directory link'}
    if(Test-Path -LiteralPath $taskResolved){
        $taskBytes=(Get-Item -LiteralPath $taskResolved).Length
        Remove-Item -LiteralPath $taskResolved -Force
        "Cleaned disposable probe executable: $taskBytes bytes."|Add-Content -LiteralPath (Join-Path $taskOutput 'cleanup.log')
    }
    if(@(Get-ChildItem -LiteralPath $taskCompile -Force).Count -eq 0){Remove-Item -LiteralPath $taskCompile}
}
