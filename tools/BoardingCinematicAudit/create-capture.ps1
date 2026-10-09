$ErrorActionPreference = 'Stop'
$sourcePath = 'D:/Game/UnityClient/Assets/_Project/Editor/UiAuditReadOnlyCapture.cs'
$source = [IO.File]::ReadAllText($sourcePath).Replace("`r`n", "`n")
$source = $source.Replace('class UiAuditReadOnlyCapture', 'class BoardingCinematicAuditCapture')
$source = $source.Replace('using ShatteredSail.Presentation;', "using ShatteredSail.Presentation;`nusing ShatteredSail.Core.Gameplay;`nusing ShatteredSail.Content;`nusing System.Collections;")
$start = $source.IndexOf('        public static void CaptureAuthoredFromCommandLine()')
$end = $source.IndexOf('        private static void CaptureMenu(')
if ($start -lt 0 -or $end -le $start) { throw 'Capture source changed.' }
$entry = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'CaptureFixture.cs.inc')).Replace("`r`n", "`n")
$source = $source.Substring(0, $start) + $entry + "`n" + $source.Substring($end)
$old = @'
                bool shop = Enum.TryParse(frame.state, out CoreGameplayPreviewScreen screen);
                if (shop) host.AddComponent<CoreGameplayDemoController>().ConfigureEditorPreview(screen, CoreGameplayPreviewPort.GrayGull, 0);
                else host.AddComponent<MainMenuController>();
                host.SetActive(true);
'@
$new = @'
                bool shop = true;
                var controller = host.AddComponent<CoreGameplayDemoController>();
                controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Battle, CoreGameplayPreviewPort.GrayGull, 0);
                host.SetActive(true);
                ConfigureFixture(controller, frame.state);
                if (frame.state.StartsWith("wipe_", StringComparison.Ordinal))
                {
                    wipeRoutine = (IEnumerator)Invoke(controller, "AnimateBoardingSceneTransition");
                    if (!wipeRoutine.MoveNext()) throw new InvalidOperationException("Transition did not create a wipe.");
                    float progress = float.Parse(frame.state.Substring(5), System.Globalization.CultureInfo.InvariantCulture) / 100f;
                    var wipe = controller.GetComponentsInChildren<BoardingSceneWipeGraphic>(true).Single();
                    wipe.SetProgress(progress);
                    wipe.transform.parent.GetComponent<Image>().color = new Color(.015f, .025f, .032f, progress * .85f);
                    frame.wipeBeforeRender = InspectWipe(controller);
                }
'@
$old = $old.Replace("`r`n", "`n")
$new = $new.Replace("`r`n", "`n")
if (!$source.Contains($old)) { throw 'Cannot find disposable preview creation block.' }
$source = $source.Replace($old, $new)
$source = $source.Replace('            Scene scene = default;', "            Scene scene = default;`n            IEnumerator wipeRoutine = null;")
$source = $source.Replace('                RenderTexture.active = previousActive;', "                (wipeRoutine as IDisposable)?.Dispose();`n                RenderTexture.active = previousActive;")
$source = $source.Replace('                RenderTexture.active = rt;', "                InspectBattleGeometry(controller, canvas, camera, frame);`n                if (frame.state.StartsWith(`"wipe_`", StringComparison.Ordinal)) frame.wipeAfterRender = InspectWipe(controller);`n                RenderTexture.active = rt;")
$source = $source.Replace('                if (high-low<20)', '                if (frame.state != "wipe_100" && high-low<20)')
$source = $source.Replace('                File.WriteAllBytes(Path.Combine(output,frame.file), pixels.EncodeToPNG());', "                File.WriteAllBytes(Path.Combine(output,frame.file), pixels.EncodeToPNG());`n                VerifyWipePixels(frame, samples);")
$source = $source.Replace('            public string state, file, error;', @'
            public string state, file, error, backgroundPath;
            public string enemyEncounterId, enemyName, expectedEnemyResource, enemyResource;
            public bool verifiedBattleGeometry;
            public bool verifiedEnemyIdentity;
            public WipeDiagnostic wipeBeforeRender, wipeAfterRender;
            public bool verifiedFullWipe;
            public Color32 wipePixelMinimum, wipePixelMaximum;
            public int wipePixelSpread;
            public int playerHealth, playerMaximum, playerGuard, enemyHealth, enemyMaximum;
            public List<ActorGeometry> actors = new List<ActorGeometry>();
'@.Replace("`r`n", "`n"))
$source = $source.Replace('// Never saves scenes/assets and never enters PlayMode or invokes gameplay callbacks.', '// Capture is read-only: disposable production preview sessions and explicit visual fixture data. Sync is a separate guarded entry point.')
[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'BoardingCinematicAuditCapture.cs'), $source, [Text.UTF8Encoding]::new($false))
