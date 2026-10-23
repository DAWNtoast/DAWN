param([ValidateSet('PrepareCapture','Capture','Edit')][string]$Stage, [string]$Run = 'review02')
$ErrorActionPreference = 'Stop'
$royalProject = 'D:\Game\tmp\shipyard-assembly-20261017'
$royalOutput = 'D:\Game\Artifacts\RoyalShipyard20261022\' + $Run
if ($Run -notmatch '^[A-Za-z0-9_-]+$') { throw 'Use a simple Run directory name.' }
if (@(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object { $_.CommandLine -and $_.CommandLine.Replace('/','\').Contains($royalProject) }).Count) { throw 'Isolated project is occupied.' }
if ($Stage -eq 'PrepareCapture') {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Capture.cs') -Destination (Join-Path $royalProject 'Assets/_Project/Editor/RoyalShipyardCapture.cs') -Force
    $royalHelper = Join-Path $royalProject 'Assets/_Project/Editor/UiAuditReadOnlyCapture.cs'
    $royalOriginal = [IO.File]::ReadAllText('D:\Game\UnityClient\Assets\_Project\Editor\UiAuditReadOnlyCapture.cs')
    $royalNeedle = "        private static void ArrangeParchmentVariant(GameObject host, MenuFrame frame)"
    $royalBranch = @'
            if (frame.variant != null && frame.variant.StartsWith("RoyalShipyard_", StringComparison.Ordinal))
            {
                int ship = int.Parse(frame.variant.Substring("RoyalShipyard_".Length));
                var royal = host.GetComponent<CoreGameplayDemoController>();
                royal.ConfigureEditorPreview(CoreGameplayPreviewScreen.Shipyard, (CoreGameplayPreviewPort)ShatteredSail.Core.Gameplay.CoreShipMarketCatalog.HomePortIndex(ship), 0);
                royal.ConfigureEditorShipyard(ship, false);
                return;
            }
'@
    $royalStart = $royalOriginal.IndexOf($royalNeedle, [StringComparison]::Ordinal)
    if ($royalStart -lt 0) { throw 'Capture helper signature changed.' }
    $royalBrace = $royalOriginal.IndexOf('{', $royalStart) + 1
    [IO.File]::WriteAllText($royalHelper, $royalOriginal.Insert($royalBrace, "`n" + $royalBranch), [Text.UTF8Encoding]::new($false))
    Write-Output 'Installed capture and in-memory variant hook only in the isolated project.'
    return
}
if ($Stage -eq 'Edit') {
    & 'D:/Game/tools/ShipyardAssembly20261017/Run.ps1' -Stage Edit -Output $royalOutput -TestFilter 'RoyalShipyardGalleryTests;ShipyardGalleryTests;ShipyardComponentComparisonTests;CoreShipMarketCatalogTests;CoreShipPortProgressTests;RegionalPortFacilityArtCatalogTests;PortFacilityHeaderTests'
    return
}
[IO.Directory]::CreateDirectory($royalOutput) | Out-Null
$royalLog = Join-Path $royalOutput 'capture.log'
if (Test-Path -LiteralPath $royalLog) { throw 'Use a new Run directory to preserve existing logs.' }
$royalArgs = @('-batchmode','-force-d3d11','-projectPath',$royalProject,'-logFile',$royalLog,'-executeMethod','ShatteredSail.Editor.RoyalShipyardCapture.CaptureFromCommandLine','-royalShipyardOutput',$royalOutput,'-quit')
$royalProcess = Start-Process -FilePath 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Unity.exe' -ArgumentList $royalArgs -WindowStyle Hidden -PassThru
Write-Output ('Started Capture PID ' + $royalProcess.Id + ', log ' + $royalLog)
