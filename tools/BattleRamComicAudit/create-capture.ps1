$ErrorActionPreference = 'Stop'
$taskSource = Get-Content -LiteralPath 'D:/Game/UnityClient/Assets/_Project/Editor/UiAuditReadOnlyCapture.cs' -Raw
$taskSource = $taskSource.Replace('class UiAuditReadOnlyCapture','class RamComicAuditCapture')
$taskStart = $taskSource.IndexOf('        public static void CaptureAuthoredFromCommandLine()')
$taskEnd = $taskSource.IndexOf('        private static void CaptureMenu(')
$taskEntry = @'
        public static void CaptureFromCommandLine()
        {
            RequireEditor();
            string output = Output();
            if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any()) throw new IOException("Use fresh output.");
            Directory.CreateDirectory(output);
            var report = new MenuReport { capturedAtUtc = DateTime.UtcNow.ToString("O"), scope = "Real ram-comic builder, four full pages and four impact panels at two verified production CanvasScaler targets. No gameplay commits or saved scene changes." };
            foreach (string art in new[] {"ram_player_head_on_v1","ram_player_broadside_v1","ram_boss_head_on_v1","ram_boss_broadside_v1"})
                foreach (string view in new[] { "page", "hit" })
                    foreach (Vector2Int size in new[] {new Vector2Int(1920,1080),new Vector2Int(1280,720)})
                    {
                        string state = art + "_" + view;
                        var frame = new MenuFrame {state=state,width=size.x,height=size.y,file=state+"_"+size.x+"x"+size.y+".png"};
                        report.frames.Add(frame);
                        try { CaptureMenu(output,frame); }
                        catch(Exception error) { frame.error=error.ToString(); }
                    }
            File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(report,true));
            if(report.frames.Count != 16 || report.frames.Any(frame => !frame.verifiedScaler || !string.IsNullOrEmpty(frame.error)))
                throw new InvalidOperationException("Ram comic capture failed.");
        }

'@
if ($taskStart -lt 0 -or $taskEnd -le $taskStart) { throw 'Capture source changed.' }
$taskSource = $taskSource.Substring(0,$taskStart) + $taskEntry + $taskSource.Substring($taskEnd)
$taskOld = @'
                bool shop = Enum.TryParse(frame.state, out CoreGameplayPreviewScreen screen);
                if (shop) host.AddComponent<CoreGameplayDemoController>().ConfigureEditorPreview(screen, CoreGameplayPreviewPort.GrayGull, 0);
                else host.AddComponent<MainMenuController>();
                host.SetActive(true);
'@
$taskNew = @'
                bool shop = true;
                var controller = host.AddComponent<CoreGameplayDemoController>();
                controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Battle, CoreGameplayPreviewPort.GrayGull, 0);
                host.SetActive(true);
                bool page = frame.state.EndsWith("_page",StringComparison.Ordinal);
                string resource = "BattleComics/" + frame.state.Substring(0,frame.state.Length-(page?5:4));
                Texture2D art = Resources.Load<Texture2D>(resource);
                if(art==null) throw new InvalidOperationException("Missing comic: "+resource);
                const BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
                var build = typeof(CoreGameplayDemoController).GetMethod("BuildRamComicOverlay",privateInstance);
                var panel = typeof(CoreGameplayDemoController).GetMethod("SetRamComicPanel",privateInstance);
                build.Invoke(controller,new object[] {art,frame.state.Contains("_boss_"),frame.state.Contains("_head_on_"),"敌舰船体 -18　·　我方船体 -7\n船只已连接，转入接舷近战"});
                panel.Invoke(controller,new object[] {page?4:1});
'@
$taskSource = $taskSource.Replace("`r`n","`n")
$taskOld = $taskOld.Replace("`r`n","`n")
$taskNew = $taskNew.Replace("`r`n","`n")
if (!$taskSource.Contains($taskOld)) { throw 'Cannot find disposable preview creation block.' }
$taskSource = $taskSource.Replace($taskOld,$taskNew)
[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'RamComicAuditCapture.cs'),$taskSource,[Text.UTF8Encoding]::new($false))
