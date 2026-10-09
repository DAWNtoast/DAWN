        [Serializable] public sealed class ActorGeometry
        {
            public string name, texture;
            public Rect uv, rectPixels;
            public Vector2 logicalSize;
            public bool keepsAspect, insideTarget, raycastDisabled;
        }

        [Serializable] public sealed class WipeDiagnostic
        {
            public bool exists, active, enabled, hasRenderer, cull;
            public float progress, rendererAlpha;
            public Color color, rendererColor;
            public Rect rect;
            public Vector2 sizeDelta, anchorMin, anchorMax;
            public Vector3 position, scale;
            public int layer, materialCount, absoluteDepth, meshVertexCount, meshIndexCount;
            public Bounds meshBounds;
            public string material, canvases, groups;
        }

        private static WipeDiagnostic InspectWipe(CoreGameplayDemoController controller)
        {
            var result = new WipeDiagnostic();
            BoardingSceneWipeGraphic wipe = controller.GetComponentsInChildren<BoardingSceneWipeGraphic>(true).SingleOrDefault();
            if (wipe == null) return result;
            result.exists = true; result.active = wipe.gameObject.activeInHierarchy; result.enabled = wipe.enabled;
            result.progress = wipe.Progress; result.color = wipe.color; result.rect = wipe.rectTransform.rect;
            result.sizeDelta = wipe.rectTransform.sizeDelta; result.anchorMin = wipe.rectTransform.anchorMin; result.anchorMax = wipe.rectTransform.anchorMax;
            result.position = wipe.transform.position; result.scale = wipe.transform.lossyScale; result.layer = wipe.gameObject.layer;
            // Read the component directly: Graphic.canvasRenderer's getter can
            // create one and would itself change the lifecycle being diagnosed.
            CanvasRenderer renderer = wipe.GetComponent<CanvasRenderer>();
            result.hasRenderer = renderer != null;
            if (renderer != null)
            {
                result.cull = renderer.cull; result.rendererAlpha = renderer.GetAlpha(); result.rendererColor = renderer.GetColor();
                result.materialCount = renderer.materialCount; result.absoluteDepth = renderer.absoluteDepth;
                Material material = renderer.materialCount > 0 ? renderer.GetMaterial(0) : null;
                result.material = material == null ? string.Empty : material.name + "/" + material.shader.name;
                Mesh mesh = renderer.GetMesh();
                if (mesh != null)
                {
                    result.meshVertexCount = mesh.vertexCount; result.meshIndexCount = mesh.triangles.Length; result.meshBounds = mesh.bounds;
                }
            }
            result.canvases = string.Join(" | ", wipe.GetComponentsInParent<Canvas>(true).Select(canvas => canvas.name +
                ":active=" + canvas.isActiveAndEnabled + ",mode=" + canvas.renderMode + ",override=" + canvas.overrideSorting +
                ",order=" + canvas.sortingOrder + ",root=" + canvas.isRootCanvas + ",camera=" + (canvas.worldCamera == null ? "none" : canvas.worldCamera.name)));
            result.groups = string.Join(" | ", wipe.GetComponentsInParent<CanvasGroup>(true).Select(group => group.name +
                ":alpha=" + group.alpha + ",ignoreParents=" + group.ignoreParentGroups));
            return result;
        }

        private static void VerifyWipePixels(MenuFrame frame, Color32[] pixels)
        {
            if (!frame.state.StartsWith("wipe_", StringComparison.Ordinal)) return;
            WipeDiagnostic wipe = frame.wipeAfterRender;
            if (wipe == null || !wipe.exists || !wipe.hasRenderer || wipe.meshVertexCount < 4 || wipe.meshIndexCount < 6 ||
                !wipe.active || !wipe.enabled || wipe.cull || wipe.color.a < .99f || wipe.rendererAlpha < .99f)
                throw new InvalidOperationException("Wipe graphic did not render an active opaque quad; see wipe diagnostics.");
            if (frame.state != "wipe_100") return;
            int minR = 255, minG = 255, minB = 255, maxR = 0, maxG = 0, maxB = 0;
            foreach (Color32 pixel in pixels)
            {
                minR = Math.Min(minR, pixel.r); minG = Math.Min(minG, pixel.g); minB = Math.Min(minB, pixel.b);
                maxR = Math.Max(maxR, pixel.r); maxG = Math.Max(maxG, pixel.g); maxB = Math.Max(maxB, pixel.b);
            }
            frame.wipePixelMinimum = new Color32((byte)minR, (byte)minG, (byte)minB, 255);
            frame.wipePixelMaximum = new Color32((byte)maxR, (byte)maxG, (byte)maxB, 255);
            frame.wipePixelSpread = Math.Max(maxR - minR, Math.Max(maxG - minG, maxB - minB));
            Color sample = new Color((minR + maxR) / 510f, (minG + maxG) / 510f, (minB + maxB) / 510f, 1f);
            Color authored = new Color(.88f, .84f, .70f, 1f);
            float rawDistance = ColorDistance(sample, authored), linearDistance = ColorDistance(sample, authored.gamma);
            frame.verifiedFullWipe = frame.wipePixelSpread <= 4 && Math.Min(rawDistance, linearDistance) <= 6f / 255f;
            if (!frame.verifiedFullWipe)
                throw new InvalidOperationException("100% wipe did not uniformly cover every target pixel in the authored opaque cream color; see pixel min/max and spread.");
        }

        private static float ColorDistance(Color left, Color right) => Mathf.Max(Mathf.Abs(left.r - right.r),
            Mathf.Abs(left.g - right.g), Mathf.Abs(left.b - right.b));

        private sealed class EnemyFixture
        {
            public readonly string key, encounterId;
            public readonly int route;
            public EnemyFixture(string key, string encounterId, int route)
            { this.key = key; this.encounterId = encounterId; this.route = route; }
            public string Resource => "BoardingBattle/Characters/boarding_enemy_" + key + "_v1";
        }

        // Independent expected identities: actual authored encounter factories
        // create the battle, while this table detects wrong/fallback art.
        private static readonly EnemyFixture[] EnemyFixtures =
        {
            new EnemyFixture("party", "tutorial_boss_event", 0),
            new EnemyFixture("imperial", "gold_customs", 1),
            new EnemyFixture("ghost", "moon_ghost", 2),
            new EnemyFixture("route_reef", "boss_route_shattered_sail_reef", 0),
            new EnemyFixture("route_gold", "boss_route_golden_tide_trade", 1),
            new EnemyFixture("route_moon", "boss_route_moon_tide_ruins", 2),
            new EnemyFixture("treasure", "treasure_boss_moon_vault", 2),
            new EnemyFixture("iron_oath", "legendary_pirate_iron_oath", 2),
            new EnemyFixture("thunder_crown", "legendary_pirate_thunder_crown", 2),
            new EnemyFixture("red_widow", "legendary_pirate_red_widow", 2),
            new EnemyFixture("ember_ark", "legendary_pirate_ember_ark", 2),
            new EnemyFixture("tide_requiem", "legendary_pirate_tide_requiem", 2),
        };

        public static void CaptureFromCommandLine()
        {
            RequireEditor();
            string output = Output();
            if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
                throw new IOException("Use a fresh capture output directory.");
            Directory.CreateDirectory(output);
            var report = new MenuReport
            {
                capturedAtUtc = DateTime.UtcNow.ToString("O"),
                scope = "Production EditorPreview sessions, real EnterBattleStage and Render: 18 terrain/time boarding views, two 720p views, one ultrawide view and one naval HUD view. Twelve additional enemy-identity views use actual authored encounter factories and real StartBattle, then explicit boarding layout samples; their expected resource is checked independently and must not fall back. Three static frames advance the real production transition iterator once and set only its wipe progress; they do not prove animation timing. Session health/guard/hand/time are explicit layout samples, not played progression. No PlayerPrefs or saved scene writes. Ordinary-cast assertions require the isolated editor identity to have no recruited special crew. Actual camera/RT/CanvasScaler and full text geometry recorded."
            };
            foreach (string terrain in new[] { "open", "coast", "strait", "reef", "fog", "storm" })
                foreach (string time in new[] { "day", "dusk", "night" })
                    AddCapture(report, output, terrain + "_" + time, 1920, 1080);
            AddCapture(report, output, "open_day", 1280, 720);
            AddCapture(report, output, "storm_night", 1280, 720);
            AddCapture(report, output, "open_day", 2560, 1080);
            AddCapture(report, output, "naval_day", 1920, 1080);
            foreach (string progress in new[] { "20", "65", "100" })
                AddCapture(report, output, "wipe_" + progress, 1920, 1080);
            foreach (EnemyFixture enemy in EnemyFixtures)
                AddCapture(report, output, "enemy_" + enemy.key, 1920, 1080);
            File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true));
            if (report.frames.Count != 37 || report.frames.Any(frame => !frame.verifiedScaler ||
                !frame.verifiedBattleGeometry || !string.IsNullOrEmpty(frame.error) ||
                frame.state.StartsWith("enemy_", StringComparison.Ordinal) && !frame.verifiedEnemyIdentity))
                throw new InvalidOperationException("Boarding capture failed; inspect report.json.");
            Debug.Log("BOARDING_CINEMATIC_CAPTURE_OK: " + report.frames.Count + " frames at " + output);
        }

        private static void AddCapture(MenuReport report, string output, string state, int width, int height)
        {
            var frame = new MenuFrame { state = state, width = width, height = height,
                file = state + "_" + width + "x" + height + ".png" };
            report.frames.Add(frame);
            try { CaptureMenu(output, frame); }
            catch (Exception error) { frame.error = error.ToString(); Debug.LogWarning(frame.file + ": " + error.Message); }
            File.WriteAllText(Path.Combine(output, "report.json"), JsonUtility.ToJson(report, true));
        }

        internal static void ConfigureFixture(CoreGameplayDemoController controller, string state)
        {
            if (Application.isPlaying) throw new InvalidOperationException("Editor fixture only.");
            CoreVoyageSession session = controller.Session;
            if (session == null || session.Phase != CoreDemoPhase.Battle || session.Battle == null)
                throw new InvalidOperationException("Production battle preview did not construct a session.");
            if (state.StartsWith("enemy_", StringComparison.Ordinal))
                ConfigureEnemyFixture(session, EnemyFixtures.Single(item => "enemy_" + item.key == state));
            bool naval = state == "naval_day";
            SeaTerrainType terrain = state.StartsWith("coast_", StringComparison.Ordinal) ? SeaTerrainType.Beach :
                state.StartsWith("strait_", StringComparison.Ordinal) ? SeaTerrainType.Strait :
                state.StartsWith("reef_", StringComparison.Ordinal) ? SeaTerrainType.Reef :
                state.StartsWith("fog_", StringComparison.Ordinal) ? SeaTerrainType.FogBank :
                state.StartsWith("storm_", StringComparison.Ordinal) ? SeaTerrainType.StormFront : SeaTerrainType.OpenWater;
            double hours = state.EndsWith("_night", StringComparison.Ordinal) ? 14d :
                state.EndsWith("_dusk", StringComparison.Ordinal) ? 10d : 0d;
            Set(session, "BattleTerrain", terrain);
            Set(session, "ElapsedGameHours", hours);
            var crew = (IList)Get(controller, "selectedDepartureSpecialCrew");
            crew.Clear();
            Invoke(session, "EnterBattleStage", naval ? NavalBattleStage.Naval : NavalBattleStage.Boarding);
            CoreBattleState battle = session.Battle;
            battle.Alignment = NavalBattleAlignment.Parallel;
            battle.DistanceMeters = naval ? 250f : 0f;
            battle.BoardingApproach = NavalBoardingApproach.Alongside;
            battle.PlannedBoardingApproach = battle.BoardingApproach;
            battle.BoardingConnection = NavalBoardingConnection.Planned;
            Set(battle, "BoardingInitiatedByEnemy", false);
            battle.PlayerMovingTowardEnemy = battle.EnemyMovingTowardPlayer = false;
            battle.PlayerBlock = 17;
            battle.EnemyBlock = 23;
            battle.PlayerBoardingHealth = PartialHealth(battle.PlayerMaxBoardingHealth);
            battle.EnemyBoardingHealth = PartialHealth(battle.EnemyMaxBoardingHealth);
            if (naval)
            {
                Set(session, "Hull", PartialHealth(session.MaxHull));
                battle.EnemyHull = PartialHealth(battle.EnemyMaxHull);
            }
            else
            {
                battle.Hand.Clear();
                foreach (string id in new[] { "card_boarding_hook", "card_cutlass_raid", "card_crew_guard", "card_crew_bandage", "card_crew_rally" })
                {
                    int index = Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count).Single(i => CoreVoyageSession.CardLibrary[i].Id == id);
                    if (!session.GetBattleCard(index).IsAllowedInStage(NavalBattleStage.Boarding))
                        throw new InvalidOperationException("Wrong-stage fixture card " + id);
                    battle.Hand.Add(index);
                }
            }
            battle.LastAction = naval ? "炮战展示样本 · 船体与格挡" : "接舷展示样本 · 十点生命一格 · 格挡包裹血条";
            Invoke(controller, "Render");
        }

        private static void ConfigureEnemyFixture(CoreVoyageSession session, EnemyFixture fixture)
        {
            Set(session, "SelectedRouteIndex", fixture.route);
            CoreEncounter encounter;
            if (fixture.encounterId == CoreGuidedTutorialContent.BossEventId)
            {
                MethodInfo factory = typeof(CoreGuidedTutorialContent).GetMethod("BossEvent", BindingFlags.Static | BindingFlags.NonPublic);
                if (factory == null) throw new MissingMethodException(typeof(CoreGuidedTutorialContent).FullName, "BossEvent");
                encounter = (CoreEncounter)factory.Invoke(null, null);
            }
            else if (fixture.encounterId == CoreBattleBalance.TreasureBossId)
                encounter = CoreLegendaryWarships.CreateTreasureEncounter();
            else if (fixture.encounterId.StartsWith("legendary_pirate_", StringComparison.Ordinal))
                encounter = CoreLegendaryWarships.Find(fixture.encounterId).CreateEncounter();
            else if (fixture.encounterId.StartsWith("boss_route_", StringComparison.Ordinal))
                encounter = (CoreEncounter)Invoke(session, "CreateBossEncounter", CoreVoyageSession.Routes[fixture.route]);
            else
                encounter = ((IReadOnlyList<CoreEncounter>)Invoke(session, "CreateEncounterPool", CoreVoyageSession.Routes[fixture.route]))
                    .Single(item => item.Id == fixture.encounterId);
            if (encounter.Id != fixture.encounterId)
                throw new InvalidOperationException("Authored encounter factory identity changed: " + fixture.encounterId);
            Set(session, "ActiveEncounter", encounter);
            Invoke(session, "StartBattle", encounter, encounter.BossKind != CoreBossKind.None || encounter.Id == CoreGuidedTutorialContent.BossEventId);
            if (session.Battle == null || session.Battle.EnemyName != encounter.Name)
                throw new InvalidOperationException("Real StartBattle did not keep authored enemy identity.");
        }

        private static int PartialHealth(int maximum) => Math.Max(1, Math.Min(maximum - 1, maximum * 3 / 4 + 3));

        internal static object Get(object instance, string name)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            FieldInfo field = instance.GetType().GetField(name, flags);
            if (field != null) return field.GetValue(instance);
            PropertyInfo property = instance.GetType().GetProperty(name, flags);
            if (property != null) return property.GetValue(instance);
            throw new MissingMemberException(instance.GetType().FullName, name);
        }

        internal static void Set(object instance, string name, object value)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            FieldInfo field = instance.GetType().GetField(name, flags);
            if (field != null) { field.SetValue(instance, value); return; }
            PropertyInfo property = instance.GetType().GetProperty(name, flags);
            if (property?.GetSetMethod(true) != null) { property.SetValue(instance, value); return; }
            throw new MissingMemberException(instance.GetType().FullName, name + " writable field/property");
        }

        internal static object Invoke(object instance, string name, params object[] args)
        {
            MethodInfo method = instance.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null) throw new MissingMethodException(instance.GetType().FullName, name);
            try { return method.Invoke(instance, args); }
            catch (TargetInvocationException error) { throw error.InnerException ?? error; }
        }

        private static void InspectBattleGeometry(CoreGameplayDemoController controller, Canvas canvas, Camera camera, MenuFrame frame)
        {
            CoreBattleState battle = controller.Session.Battle;
            bool naval = battle.Stage == NavalBattleStage.Naval;
            Transform root = canvas.GetComponentsInChildren<Transform>(false).Single(t => t.name == "Battle Card");
            RawImage background = root.Find("Battle Scene Art").GetComponent<RawImage>();
            frame.backgroundPath = AssetDatabase.GetAssetPath(background.texture);
            frame.playerHealth = naval ? controller.Session.Hull : battle.PlayerBoardingHealth;
            frame.playerMaximum = naval ? controller.Session.MaxHull : battle.PlayerMaxBoardingHealth;
            frame.playerGuard = battle.PlayerBlock;
            frame.enemyHealth = naval ? battle.EnemyHull : battle.EnemyBoardingHealth;
            frame.enemyMaximum = naval ? battle.EnemyMaxHull : battle.EnemyMaxBoardingHealth;
            frame.enemyEncounterId = controller.Session.ActiveEncounter?.Id;
            frame.enemyName = battle.EnemyName;
            if (!naval)
            {
                Texture2D expected = BoardingBattleArtCatalog.ForEnvironment(controller.Session.BattleTerrain,
                    NavigationTimeOfDayUtility.FromVoyageHours(controller.Session.ElapsedGameHours));
                if (expected == null || background.texture != expected)
                    throw new InvalidOperationException("Missing or wrong production boarding background.");
                float backgroundAspect = background.texture.width * background.uvRect.width / (background.texture.height * background.uvRect.height);
                if (Mathf.Abs(backgroundAspect - background.rectTransform.rect.width / background.rectTransform.rect.height) > .01f)
                    throw new InvalidOperationException("Boarding background is stretched.");
                foreach (string side in new[] { "Player", "Enemy" })
                {
                    Transform party = root.Find(side + " Ship Art");
                    RawImage[] figures = party.GetComponentsInChildren<RawImage>(false);
                    int expectedCount = 1;
                    if (figures.Length != expectedCount)
                        throw new InvalidOperationException(side + " has " + figures.Length + " figures; expected " + expectedCount + ". Fixture must not depend on saved recruitment.");
                    foreach (RawImage figure in figures)
                    {
                        if (figure.name != side + " Ship Art Party Illustration")
                            throw new InvalidOperationException("Ordinary-cast preview inherited saved recruitment. Use a clean isolated editor identity, never modify player preferences for capture.");
                        var corners = new Vector3[4]; figure.rectTransform.GetWorldCorners(corners);
                        var item = new ActorGeometry { name = figure.name, texture = AssetDatabase.GetAssetPath(figure.texture),
                            uv = figure.uvRect, logicalSize = figure.rectTransform.rect.size,
                            rectPixels = Bounds(corners.Select(p => (Vector2)camera.WorldToScreenPoint(p))), raycastDisabled = !figure.raycastTarget };
                        item.insideTarget = !Outside(item.rectPixels, frame.width, frame.height);
                        item.keepsAspect = figure.texture != null && Mathf.Abs(item.logicalSize.x / item.logicalSize.y -
                            figure.texture.width * Mathf.Abs(item.uv.width) / (figure.texture.height * Mathf.Abs(item.uv.height))) < .01f;
                        frame.actors.Add(item);
                        if (!item.insideTarget || !item.keepsAspect || !item.raycastDisabled || item.uv != new Rect(0, 0, 1, 1) || item.logicalSize.y < 300f)
                            throw new InvalidOperationException("Actor aspect, full-body size, full UV, or input geometry failed: " + figure.name);
                    }
                }
                if (root.Find("Player Boarding Deck") != null || root.Find("Boarding Gangplank") != null)
                    throw new InvalidOperationException("Legacy small deck still present.");
                if (frame.state.StartsWith("enemy_", StringComparison.Ordinal))
                {
                    EnemyFixture fixture = EnemyFixtures.Single(item => "enemy_" + item.key == frame.state);
                    frame.expectedEnemyResource = fixture.Resource;
                    RawImage figure = root.Find("Enemy Ship Art/Enemy Ship Art Party Illustration").GetComponent<RawImage>();
                    frame.enemyResource = AssetDatabase.GetAssetPath(figure.texture);
                    Texture2D expectedEnemy = Resources.Load<Texture2D>(fixture.Resource);
                    string actualPath = BoardingBattleArtCatalog.EnemyResourcePath(battle, controller.Session.ActiveEncounter, controller.Session.SelectedRouteIndex);
                    if (expectedEnemy == null || figure.texture != expectedEnemy || actualPath != fixture.Resource ||
                        frame.enemyEncounterId != fixture.encounterId || frame.enemyName != controller.Session.ActiveEncounter.Name ||
                        BoardingBattleArtCatalog.ForEnemy(battle, controller.Session.ActiveEncounter, controller.Session.SelectedRouteIndex) != expectedEnemy)
                        throw new InvalidOperationException("Enemy identity art is missing, wrong, or using fallback: " + fixture.encounterId + " expected " + fixture.Resource + " actual " + frame.enemyResource);
                    frame.verifiedEnemyIdentity = true;
                }
            }
            foreach (string side in new[] { "Player", "Enemy" })
            {
                BattleDefenseHud hud = root.Find(side + " Battle Hull Bar Track").GetComponent<BattleDefenseHud>();
                BattleHealthSegmentsGraphic segments = hud.HullBar.GetComponentInChildren<BattleHealthSegmentsGraphic>();
                int maximum = side == "Player" ? frame.playerMaximum : frame.enemyMaximum;
                if (segments == null || segments.MaximumHealth != maximum || segments.raycastTarget)
                    throw new InvalidOperationException("Health segment contract failed for " + side);
            }
            frame.verifiedBattleGeometry = true;
        }
