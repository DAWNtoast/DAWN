param(
    [Parameter(Mandatory = $true)][string]$Project,
    [string]$SourceProject = 'D:\Game\UnityClient'
)
$ErrorActionPreference = 'Stop'
$auditProject = [IO.Path]::GetFullPath($Project)
$auditSource = [IO.Path]::GetFullPath($SourceProject)
if ($auditProject.TrimEnd('\','/') -eq $auditSource.TrimEnd('\','/')) { throw 'Only an isolated project is allowed.' }
if (-not (Test-Path -LiteralPath (Join-Path $auditProject 'ProjectSettings/ProjectVersion.txt'))) { throw 'Not a Unity project.' }
$auditEditor = Join-Path $auditProject 'Assets/_Project/Editor'
if (-not (Test-Path -LiteralPath $auditEditor)) { throw 'Sync current production sources before preparing capture.' }
$auditHelperSource = Join-Path $auditSource 'Assets/_Project/Editor/UiAuditReadOnlyCapture.cs'
$auditOriginal = [IO.File]::ReadAllText($auditHelperSource)
$auditSignature = '        private static void ArrangeParchmentVariant(GameObject host, MenuFrame frame)'
$auditStart = $auditOriginal.IndexOf($auditSignature, [StringComparison]::Ordinal)
if ($auditStart -lt 0) { throw 'Capture helper signature changed.' }
$auditBrace = $auditOriginal.IndexOf('{', $auditStart) + 1
$auditHook = @'
            if (frame.variant != null && frame.variant.StartsWith("Audit", StringComparison.Ordinal))
            {
                SmallTextAuditVariants.Arrange(host, frame.variant);
                return;
            }
'@
$auditPrepared = $auditOriginal.Insert($auditBrace, "`n" + $auditHook)
$auditRenderNeedle = '                    handle.Invoke(scaler, null);'
if (-not $auditPrepared.Contains($auditRenderNeedle)) { throw 'Capture renderer hook changed.' }
$auditPrepared = $auditPrepared.Replace($auditRenderNeedle, $auditRenderNeedle + "`n                    SmallTextAuditVariants.BeforeRender(host, frame.variant, camera, pass);")
[IO.File]::WriteAllText((Join-Path $auditEditor 'UiAuditReadOnlyCapture.cs'), $auditPrepared, [Text.UTF8Encoding]::new($false))
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Capture.cs') -Destination (Join-Path $auditEditor 'SmallTextCapture.cs') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Variants.cs') -Destination (Join-Path $auditEditor 'SmallTextAuditVariants.cs') -Force
Write-Output ('Installed disposable capture helpers in ' + $auditEditor)
