using System.Collections;
using System.Collections.Generic;
using ShatteredSail.Content;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private const float BattleShipDesignWidth = 320f;

        private void BuildStagedBattleScene(RectTransform root, CoreBattleState battle, Texture2D enemySheet, Rect enemyUv)
        {
            bool boarding = battle.Stage == NavalBattleStage.Boarding;
            float width = BattleViewLayout.ShipWidth(battle.DistanceMeters, battle.Stage);
            float separation = BattleViewLayout.ShipX(battle.DistanceMeters, battle.Stage);
            float waterY = BattleViewLayout.WaterY(battle.DistanceMeters, battle.Stage);
            if (boarding)
            {
                BuildBoardingParty(root, "Player Ship Art", BoardingBattleLayout.PartyPosition(false).x, false);
                BuildBoardingParty(root, "Enemy Ship Art", BoardingBattleLayout.PartyPosition(true).x, true);
            }
            else
            {
                CreateStagedWater(root, "Player Ship Water Shadow", -separation, waterY, width, false, 0f);
                CreateStagedWater(root, "Enemy Ship Water Shadow", separation, waterY, width, false, 1.4f);
                // Keep the root's design-size constant. Animated zoom scales the
                // complete modular hierarchy, not just its empty RectTransform.
                RectTransform player = BuildActiveShipPreview("Player Ship Art", root, new Vector2(BattleShipDesignWidth, BattleShipDesignWidth * 0.68f), Vector2.zero);
                foreach (RawImage layer in player.GetComponentsInChildren<RawImage>())
                    if (layer.texture == portArt.ShipSheet) BattleShipCutout.Attach(layer);
                RawImage enemy = CreateRawImage("Enemy Ship Art", root,
                    UiImageAspect.FitSize(enemySheet, enemyUv, new Vector2(BattleShipDesignWidth, BattleShipDesignWidth * 0.75f)),
                    Vector2.zero, enemySheet, enemyUv);
                BattleShipCutout.Attach(enemy);
                enemy.raycastTarget = false;
                CreateStagedWater(root, "Player Ship Water Contact", -separation, waterY, width, true, 0f);
                CreateStagedWater(root, "Enemy Ship Water Contact", separation, waterY, width, true, 1.4f);
                // Legacy idle writes identity rotation every Update and would
                // cancel the enemy's actual facing. The water still moves.
            }
            ApplyBattleViewPose(root, battle.DistanceMeters, battle.Stage, battle.Alignment);
            Vector2 playerBarPosition = BattleHullBarPosition(false, battle.DistanceMeters, battle.Stage, battle.BoardingApproach);
            Vector2 enemyBarPosition = BattleHullBarPosition(true, battle.DistanceMeters, battle.Stage, battle.BoardingApproach);
            CreateBattleHullBar("Player Battle Hull Bar", root, new Vector2(310f, 16f), playerBarPosition,
                boarding ? battle.PlayerBoardingHealth : session.Hull, boarding ? battle.PlayerMaxBoardingHealth : session.MaxHull, Green);
            CreateBattleHullBar("Enemy Battle Hull Bar", root, new Vector2(310f, 16f), enemyBarPosition,
                boarding ? battle.EnemyBoardingHealth : battle.EnemyHull, boarding ? battle.EnemyMaxBoardingHealth : battle.EnemyMaxHull, Coral);
        }

        private void ApplyBattleViewPose(RectTransform root, float distance, NavalBattleStage stage, NavalBattleAlignment alignment)
        {
            ApplyBattleViewPose(root, distance, stage, BattleViewLayout.EnemyYaw(alignment));
        }

        private void ApplyBattleViewPose(RectTransform root, float distance, NavalBattleStage stage, float enemyYaw)
        {
            if (root == null) return;
            RawImage backdrop = root.Find("Battle Scene Art")?.GetComponent<RawImage>();
            if (backdrop != null) backdrop.uvRect = stage == NavalBattleStage.Boarding
                ? BoardingBattleLayout.CoverUv(backdrop.texture, backdrop.rectTransform.rect.size)
                : BattleViewLayout.BackdropUv(distance, backdrop.rectTransform.rect.size);
            if (stage == NavalBattleStage.Boarding)
            {
                ApplyBattleHullBarPose(root, distance, stage);
                return;
            }
            float width = BattleViewLayout.ShipWidth(distance, stage);
            float separation = BattleViewLayout.ShipX(distance, stage);
            float waterY = BattleViewLayout.WaterY(distance, stage);
            ApplyBattleHullBarPose(root, distance, stage);
            foreach (string side in new[] { "Player", "Enemy" })
            {
                bool enemy = side == "Enemy";
                float x = enemy ? separation : -separation;
                RectTransform ship = root.Find(side + " Ship Art") as RectTransform;
                if (ship != null)
                {
                    // Zoom the authored frame, not the fitted hull's width;
                    // normalizing by the hull would undo its aspect fit.
                    float scale = width / BattleShipDesignWidth;
                    ship.localScale = new Vector3(scale, scale, 1f);
                    ship.anchoredPosition = new Vector2(x, waterY);
                    ship.localRotation = Quaternion.Euler(0f, enemy ? enemyYaw : 0f, 0f);
                    RawImage hull = ship.GetComponentInChildren<RawImage>();
                    if (hull != null)
                    {
                        Vector3 contact = root.InverseTransformPoint(hull.rectTransform.TransformPoint(ShipWaterlineLayout.HullContactLocal(hull)));
                        ship.anchoredPosition += Vector2.up * (waterY - contact.y);
                    }
                }
                foreach (string effect in new[] { " Shadow", " Contact" })
                {
                    RectTransform water = root.Find(side + " Ship Water" + effect) as RectTransform;
                    if (water == null) continue;
                    water.anchoredPosition = new Vector2(x, waterY);
                    // The wake follows the projected hull width while turning.
                    float projectedWidth = enemy ? Mathf.Max(0.16f, Mathf.Abs(Mathf.Cos(enemyYaw * Mathf.Deg2Rad))) : 1f;
                    RawImage hull = ship == null ? null : ship.GetComponentInChildren<RawImage>();
                    float fittedWidth = hull == null ? BattleShipDesignWidth : hull.rectTransform.rect.width;
                    water.sizeDelta = new Vector2(width * fittedWidth / BattleShipDesignWidth * 0.93f * projectedWidth, 48f);
                }
            }
        }

        private static Vector2 BattleHullBarPosition(bool enemy, float distance, NavalBattleStage stage, NavalBoardingApproach approach)
        {
            if (stage == NavalBattleStage.Boarding)
            {
                return BoardingBattleLayout.HealthPosition(enemy);
            }
            float width = BattleViewLayout.ShipWidth(distance, stage);
            float separation = BattleViewLayout.ShipX(distance, stage);
            float waterY = BattleViewLayout.WaterY(distance, stage);
            return new Vector2(enemy ? separation : -separation, waterY - width * 0.07f - 10f);
        }

        private static void ApplyBattleHullBarPose(RectTransform root, float distance, NavalBattleStage stage)
        {
            if (root == null) return;
            NavalBoardingApproach approach = NavalBoardingApproach.Alongside;
            CoreGameplayDemoController controller = root.GetComponentInParent<CoreGameplayDemoController>();
            if (controller != null && controller.Session != null && controller.Session.Battle != null)
                approach = controller.Session.Battle.BoardingApproach;
            foreach (bool enemy in new[] { false, true })
            {
                string side = enemy ? "Enemy" : "Player";
                RectTransform track = root.Find(side + " Battle Hull Bar Track") as RectTransform;
                if (track == null) continue;
                track.anchoredPosition = BattleHullBarPosition(enemy, distance, stage, approach);
            }
        }

        private void CreateStagedWater(RectTransform root, string name, float x, float y, float width, bool foam, float phase)
        {
            CreateBattleShipWater(name, root, new Vector2(x, y), foam, phase);
            root.Find(name).GetComponent<RectTransform>().sizeDelta = new Vector2(width * 0.93f, 48f);
        }

        private void BuildBoardingDeck(RectTransform root, string name, Vector2 position, bool enemy)
        {
            RectTransform deck = CreateRect(name, root, new Vector2(650f, 160f), position);
            // Reuse the actual ship's lower hull and gun ports, with the mast
            // cropped out for the deck-level camera. This keeps two recognisable
            // vessels under the fighting parties, rather than floating stages.
            int shipIndex = enemy ? (session.Battle.IsBoss ? Mathf.Clamp(session.SelectedRouteIndex + 1, 1, 3) : session.SelectedRouteIndex == 2 ? 3 : 1) : session.SelectedShipIndex;
            Texture2D sheet = portArt.ShipTexture(shipIndex) != null ? portArt.ShipTexture(shipIndex) : art.ShipSheet;
            Rect source = portArt.ShipSheet != null ? portArt.ShipUv(shipIndex) : art.ShipUv(shipIndex, 0);
            float insetX = sheet == null ? 0f : 10f / sheet.width;
            float insetY = sheet == null ? 0f : 10f / sheet.height;
            Rect originalHullUv = new Rect(source.x + insetX, source.y + insetY + source.height * 0.10f,
                source.width - 2f * insetX, source.height * 0.34f);
            Vector2 originalHullSize = UiImageAspect.FitSize(sheet, originalHullUv, new Vector2(650f, 184f));
            // Keep the deck's established width and the top of its source crop.
            // Extend downward to the safe cell edge: shallow regional keels sit
            // below the old 10% crop and otherwise disappear before placement.
            Rect hullUv = new Rect(originalHullUv.x, source.y + insetY, originalHullUv.width, source.height * 0.44f);
            Vector2 hullSize = new Vector2(originalHullSize.x, originalHullSize.y * hullUv.height / originalHullUv.height);
            RawImage hull = CreateRawImage(name + " Hull Silhouette", deck, hullSize,
                new Vector2(0f, -96f + originalHullSize.y - hullSize.y * 0.5f), sheet, hullUv);
            hull.raycastTarget = false;
            hull.color = enemy ? new Color(0.84f, 0.80f, 0.77f) : portArt.ShipTint(shipIndex);
            BattleShipCutout.Attach(hull);
            // Preserve the upper hull's exact alignment with the deck/rails;
            // only the water contact moves to the newly retained timber keel.
            Vector2 waterContact = position + hull.rectTransform.anchoredPosition + ShipWaterlineLayout.HullContactLocal(hull);
            CreateStagedWater(root, (enemy ? "Enemy" : "Player") + " Boarding Water Contact", waterContact.x, waterContact.y, hullSize.x * (590f / 650f), true, enemy ? 1.4f : 0f);
            Color timberTint = enemy ? new Color(0.92f, 0.88f, 0.83f) : Color.white;
            RectTransform deckSurface = CreateRect(name + " Deck Surface", deck, new Vector2(500f, 52f), new Vector2(0f, 41f));
            deckSurface.gameObject.AddComponent<BoardingTimberGraphic>().Configure(BoardingTimberKind.Deck, timberTint);
            for (int post = 0; post < 6; post++)
            {
                CreateDeckTimber(name + " Rail " + post, deck, new Vector2(6f, 26f), new Vector2(-204f + post * 81.6f, 74f), timberTint);
            }
            CreateDeckTimber(name + " Gunwale", deck, new Vector2(419f, 5f), new Vector2(0f, 87f), timberTint);
            // Short cleats anchor the ropes on the inward-facing deck corners.
            float inside = enemy ? -225f : 225f;
            for (int cleat = 0; cleat < 2; cleat++)
            {
                CreateDeckTimber(name + " Cleat Post " + cleat, deck, new Vector2(7f, 15f), new Vector2(inside, 26f + cleat * 27f), timberTint);
                CreateDeckTimber(name + " Cleat Cap " + cleat, deck, new Vector2(22f, 5f), new Vector2(inside, 33f + cleat * 27f), timberTint);
            }
            ConfigureBoardingDeckGeometry(deck, hullSize.x, enemy);
        }

        private static void FollowBattleShipWaterHorizontal(RectTransform ship)
        {
            // Cannon recoil and ram pitch can lift the keel a few pixels. Keep
            // the foam horizontal on the sea, but follow actual horizontal travel.
            if (ship == null || (ship.name != "Player Ship Art" && ship.name != "Enemy Ship Art")) return;
            RectTransform root = ship.parent as RectTransform;
            RawImage hull = ship.GetComponentInChildren<RawImage>();
            if (root == null || hull == null) return;
            Vector3 contact = root.InverseTransformPoint(hull.rectTransform.TransformPoint(ShipWaterlineLayout.HullContactLocal(hull)));
            string side = ship.name == "Player Ship Art" ? "Player" : "Enemy";
            foreach (string effect in new[] { " Shadow", " Contact" })
            {
                RectTransform water = root.Find(side + " Ship Water" + effect) as RectTransform;
                if (water != null) water.anchoredPosition = new Vector2(contact.x, water.anchoredPosition.y);
            }
        }

        private static void ConfigureBoardingDeckGeometry(RectTransform deck, float hullWidth, bool enemy)
        {
            if (deck == null || hullWidth <= 0f || float.IsNaN(hullWidth) || float.IsInfinity(hullWidth)) return;
            float scale = hullWidth / 650f;
            // Set from design dimensions so editor sync is safe to repeat. Only
            // timber changes width; portraits retain their own aspect and size.
            SetBoardingDeckPart(deck, " Deck Surface", 500f * scale, 0f);
            for (int post = 0; post < 6; post++)
                SetBoardingDeckPart(deck, " Rail " + post, 6f * scale, (-204f + post * 81.6f) * scale);
            SetBoardingDeckPart(deck, " Gunwale", 419f * scale, 0f);
            float inside = (enemy ? -225f : 225f) * scale;
            for (int cleat = 0; cleat < 2; cleat++)
            {
                SetBoardingDeckPart(deck, " Cleat Post " + cleat, 7f * scale, inside);
                SetBoardingDeckPart(deck, " Cleat Cap " + cleat, 22f * scale, inside);
            }
        }

        private static void SetBoardingDeckPart(RectTransform deck, string suffix, float width, float x)
        {
            RectTransform part = deck.Find(deck.name + suffix) as RectTransform;
            if (part == null) return;
            part.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            part.anchoredPosition = new Vector2(x, part.anchoredPosition.y);
        }

        private static void UpdateBoardingConnections(RectTransform battleRoot)
        {
            if (battleRoot == null) return;
            RectTransform playerDeck = battleRoot.Find("Player Boarding Deck") as RectTransform;
            RectTransform enemyDeck = battleRoot.Find("Enemy Boarding Deck") as RectTransform;
            if (playerDeck == null || enemyDeck == null) return;
            Vector2 playerMiddle = Vector2.zero;
            Vector2 enemyMiddle = Vector2.zero;
            for (int rope = 0; rope < 2; rope++)
            {
                RectTransform playerCleat = playerDeck.Find(playerDeck.name + " Cleat Cap " + rope) as RectTransform;
                RectTransform enemyCleat = enemyDeck.Find(enemyDeck.name + " Cleat Cap " + rope) as RectTransform;
                if (playerCleat == null || enemyCleat == null) return;
                Vector2 from = battleRoot.InverseTransformPoint(playerCleat.TransformPoint(playerCleat.rect.center));
                Vector2 to = battleRoot.InverseTransformPoint(enemyCleat.TransformPoint(enemyCleat.rect.center));
                playerMiddle += from * 0.5f;
                enemyMiddle += to * 0.5f;
                SetBoardingConnection(battleRoot.Find("Boarding Grapple Rope " + rope) as RectTransform, from, to);
            }
            // The walkway meets the middle of each pair of cleats. Its angle
            // follows the actual fore/aft offset, including quarter approaches.
            SetBoardingConnection(battleRoot.Find("Boarding Gangplank") as RectTransform, playerMiddle, enemyMiddle);
        }

        private static void SetBoardingConnection(RectTransform connection, Vector2 from, Vector2 to)
        {
            if (connection == null) return;
            Vector2 direction = to - from;
            connection.localScale = Vector3.one;
            connection.pivot = new Vector2(0.5f, 0.5f);
            connection.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, direction.magnitude);
            Vector2 middle = (from + to) * 0.5f;
            connection.localPosition = new Vector3(middle.x, middle.y, connection.localPosition.z);
            connection.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        }

        private void CreateDeckTimber(string name, Transform parent, Vector2 size, Vector2 position, Color tint)
        {
            RectTransform timber = CreateRect(name, parent, size, position);
            BoardingTimberKind kind = name == "Boarding Gangplank" ? BoardingTimberKind.Gangplank :
                name.Contains("Grapple Rope") ? BoardingTimberKind.Rope : BoardingTimberKind.Beam;
            timber.gameObject.AddComponent<BoardingTimberGraphic>().Configure(kind, tint);
        }

        private void BuildBoardingParty(RectTransform root, string name, float x, bool enemy)
        {
            Vector2 bounds = BoardingBattleLayout.PartySize;
            RectTransform party = CreateRect(name, root, bounds, new Vector2(x, BoardingBattleLayout.PartyPosition(enemy).y));
            party.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;
            Color teamColor = enemy ? new Color(1f, .72f, .64f) : new Color(.66f, .94f, .93f);
            Text formationLabel = CreateText(name + " Formation Label", party, new Vector2(440f, 20f), new Vector2(0f, 167f),
                16, FontStyle.Bold, TextAnchor.MiddleCenter, teamColor);
            bool attacking = enemy == session.Battle.BoardingInitiatedByEnemy;
            formationLabel.text = attacking ? "登船队 · 白刃编队" : "守备队 · 白刃防线";
            formationLabel.raycastTarget = false;
            AddSceneLabelOutline(formationLabel);

            List<CrewVariantDefinition> roster = BuildBoardingCrewRoster(enemy);
            int specialCount = 0;
            if (!enemy)
                foreach (CrewVariantDefinition crew in roster)
                    if (crew != null) specialCount++;

            // Ordinary crew are one complete transparent cast, never cropped
            // into head cards or relabelled as recruited special characters.
            if (specialCount == 0)
            {
                Texture2D texture = enemy ? BoardingBattleArtCatalog.ForEnemy(session.Battle, session.ActiveEncounter, session.SelectedRouteIndex)
                    : BoardingBattleArtCatalog.ForParty(false);
                if (texture != null)
                    CreateBoardingFigure(name + " Party Illustration", party, texture, new Rect(0f, 0f, 1f, 1f), bounds, 0f);
                return;
            }

            // The existing voyage roster remains authoritative. A selected
            // specialist uses its full-body art and name, not a generic portrait.
            int visibleIndex = 0;
            foreach (CrewVariantDefinition crew in roster)
            {
                if (crew == null) continue;
                float localX = (visibleIndex - (specialCount - 1) * .5f) * 175f;
                Texture2D texture = BoardingBattleArtCatalog.ForCrew(crew);
                CreateBoardingFigure(name + " Sailor " + visibleIndex, party, texture, BoardingBattleArtCatalog.CrewUv(crew),
                    new Vector2(230f, bounds.y), localX);
                Text role = CreateText(name + " Boarding Crew Role " + visibleIndex, party, new Vector2(170f, 18f),
                    new Vector2(localX, -bounds.y * .5f - 13f), 13, FontStyle.Bold, TextAnchor.MiddleCenter, Ink);
                role.text = crew.DisplayName;
                role.raycastTarget = false;
                AddSceneLabelOutline(role);
                visibleIndex++;
            }
        }

        private RawImage CreateBoardingFigure(string name, RectTransform party, Texture2D texture, Rect uv, Vector2 bounds, float x)
        {
            if (texture == null) return null;
            Vector2 size = UiImageAspect.FitSize(texture, uv, bounds);
            RawImage figure = CreateRawImage(name, party, size,
                new Vector2(x, (size.y - BoardingBattleLayout.PartySize.y) * .5f), texture, uv);
            figure.raycastTarget = false;
            figure.color = Color.white;
            return figure;
        }

        private List<CrewVariantDefinition> BuildBoardingCrewRoster(bool enemy)
        {
            List<CrewVariantDefinition> roster = new List<CrewVariantDefinition>(3);
            if (!enemy)
            {
                bool liveBattle = Application.isPlaying && session != null && session.Phase == CoreDemoPhase.Battle && session.Battle != null;
                var activeSources = new HashSet<string>(System.StringComparer.Ordinal);
                var dispatchedIdentities = new HashSet<string>(System.StringComparer.Ordinal);
                List<int> legacyDispatchedProfessions = null;
                if (liveBattle)
                {
                    // The core's card availability is backed by the committed
                    // active crew IDs. Unlike the port selector, it remains
                    // valid after PersistCrewAtSea changes their location.
                    foreach (NavalBattleStage stage in new[] { NavalBattleStage.Preparation, NavalBattleStage.Naval, NavalBattleStage.Boarding })
                        foreach (int index in session.GetCrewBattleCards(stage))
                            activeSources.Add(CoreVoyageSession.CardLibrary[index].RequiredCrew);
                    if (PlayerPrefs.GetInt(DispatchMissionKey, -1) >= 0)
                    {
                        foreach (string id in PlayerPrefs.GetString(DispatchCrewIdentityKey, string.Empty).Split(','))
                            if (!string.IsNullOrWhiteSpace(id)) dispatchedIdentities.Add(id.Trim());
                        if (dispatchedIdentities.Count == 0)
                            legacyDispatchedProfessions = ParseDispatchCrewCsv(PlayerPrefs.GetString(DispatchCrewKey, string.Empty));
                    }
                }

                bool Available(CrewVariantDefinition crew, bool selected)
                {
                    if (crew == null || !IsCrewRecruited(crew)) return false;
                    if (!liveBattle) return IsCrewAvailableAtCurrentPort(crew) && IsCrewAvailableForVoyage(crew.ProfessionIndex);
                    if (dispatchedIdentities.Contains(crew.Id) ||
                        (legacyDispatchedProfessions != null && legacyDispatchedProfessions.Contains(crew.ProfessionIndex))) return false;
                    if (activeSources.Contains(crew.Id)) return true;
                    // Offline/legacy sessions expose profession sources rather
                    // than IDs. Match only the selected or original identity,
                    // never every recruited person sharing that profession.
                    string profession = crew.ProfessionIndex == 0 ? "navigator" : crew.ProfessionIndex == 1 ? "carpenter" :
                        crew.ProfessionIndex == 2 ? "doctor" : crew.ProfessionIndex == 3 ? "gunner" :
                        crew.ProfessionIndex == 4 ? "cook" : "diver";
                    return (selected || crew.IsLegacy) && activeSources.Contains(profession);
                }

                foreach (string id in selectedDepartureSpecialCrew)
                {
                    CrewVariantDefinition crew = CrewNarrativeCatalog.FindById(id);
                    if (Available(crew, true) && !roster.Contains(crew)) roster.Add(crew);
                    if (roster.Count == 3) break;
                }

                // A saved voyage may predate the departure selector. Recover
                // the currently active, recruited identities before falling
                // back to ordinary sailors, never inventing a special crew.
                if (roster.Count < 3 && (!liveBattle || selectedDepartureSpecialCrew.Count == 0))
                {
                    foreach (CrewVariantDefinition crew in CrewNarrativeCatalog.AllCrew)
                    {
                        if (!Available(crew, false) || roster.Contains(crew)) continue;
                        roster.Add(crew);
                        if (roster.Count == 3) break;
                    }
                }
            }
            while (roster.Count < 3) roster.Add(null);
            return roster;
        }

        private static string BoardingProfessionLabel(int profession)
        {
            switch (Mathf.Clamp(profession, 0, 5))
            {
                case 0: return "\u9886\u822A\u5458";
                case 1: return "\u8239\u5320";
                case 2: return "\u8239\u533B";
                case 3: return "\u70AE\u624B";
                case 4: return "\u53A8\u5E08";
                default: return "\u6F5C\u6C34\u5458";
            }
        }

        private void BuildBoardingMeleeMarker(RectTransform root, CoreBattleState battle)
        {
            NavalBoardingConnection connection = battle == null ? NavalBoardingConnection.Planned : battle.BoardingConnection;
            string connectionLabel = connection == NavalBoardingConnection.RamBowToBow ? "\u89D2\u5BF9\u89D2\u9501\u5B9A" :
                connection == NavalBoardingConnection.RamBowToBroadside ? "\u89D2\u5BF9\u4FA7\u8237\u5361\u9501" : "\u8DF3\u677F\u63A5\u8237";
            Color accent = connection == NavalBoardingConnection.RamBowToBroadside ? Coral : Gold;
            RectTransform zone = CreateFixedPanel("Boarding Clash Zone", root, new Vector2(244f, 68f), new Vector2(0f, 68f),
                new Color(0.02f, 0.04f, 0.05f, 0.96f));
            ConfigureBoardingMeleeMarker(zone, connectionLabel, accent);
        }

        private void ConfigureBoardingMeleeMarker(RectTransform zone, string connectionLabel, Color accent)
        {
            zone.sizeDelta = new Vector2(244f, 68f);
            Image zoneImage = zone.GetComponent<Image>();
            zoneImage.sprite = null;
            zoneImage.overrideSprite = null;
            zoneImage.type = Image.Type.Simple;
            zoneImage.color = new Color(0.02f, 0.04f, 0.05f, 0.96f);
            zoneImage.raycastTarget = false;
            Text clash = CreateText("Boarding Clash Label", zone, new Vector2(158f, 50f), new Vector2(29f, 0f),
                17, FontStyle.Bold, TextAnchor.MiddleLeft, Ink);
            clash.text = connectionLabel + "\n\u767D\u5203\u4EA4\u6218";
            clash.lineSpacing = 1.1f;
            clash.horizontalOverflow = HorizontalWrapMode.Wrap;
            clash.verticalOverflow = VerticalWrapMode.Truncate;
            clash.raycastTarget = false;
            // Geometry avoids relying on the font's absent crossed-swords glyph.
            // Keep the icon wholly to the left of the two-line label.
            RectTransform icon = CreateRect("Boarding Clash Mark", zone, new Vector2(40f, 42f), new Vector2(-88f, 0f));
            for (int index = 0; index < 2; index++)
            {
                RectTransform sword = CreateRect("Boarding Clash Sword " + index, icon, new Vector2(12f, 38f), Vector2.zero);
                sword.localRotation = Quaternion.Euler(0f, 0f, index == 0 ? -40f : 40f);
                CreateBoardingMarkerPart("Blade", sword, new Vector2(4f, 26f), new Vector2(0f, 5f), Ink);
                CreateBoardingMarkerPart("Guard", sword, new Vector2(13f, 3f), new Vector2(0f, -8f), accent);
                CreateBoardingMarkerPart("Grip", sword, new Vector2(4f, 9f), new Vector2(0f, -13f), accent);
            }
        }

        private void CreateBoardingMarkerPart(string name, Transform parent, Vector2 size, Vector2 position, Color color)
        {
            Image part = CreateRect(name, parent, size, position).gameObject.AddComponent<Image>();
            part.color = color;
            part.raycastTarget = false;
        }

        private void BuildNavalBattleTactics(RectTransform root, CoreBattleState battle, RectTransform readoutRoot)
        {
            bool boarding = battle.Stage == NavalBattleStage.Boarding;
            // Stage progress and instruction banners were removed from the authored layout. The compact range/alignment readouts live in their own top-level overlay so they can render above Voyage Header.
            Text range = CreateText("Battle Range", readoutRoot, new Vector2(250f, 62f) / readoutRoot.localScale.x, new Vector2(-250f, 540f - PhaseSurfaceLayout.GetHeaderHeight(tutorialActive) * .5f) / readoutRoot.localScale.x, 29, FontStyle.Bold, TextAnchor.MiddleCenter, Gold);
            range.text = boarding ? "接舷交战" : battle.DistanceMeters.ToString("0") + " 米";
            Text alignment = CreateText("Battle Alignment", readoutRoot, new Vector2(250f, 62f) / readoutRoot.localScale.x, new Vector2(250f, 540f - PhaseSurfaceLayout.GetHeaderHeight(tutorialActive) * .5f) / readoutRoot.localScale.x, 17, FontStyle.Bold, TextAnchor.MiddleCenter, Ink);
            alignment.text = boarding ? BoardingConnectionLabel(battle) : battle.Alignment == NavalBattleAlignment.HeadOn ? "→ 舰艏 ⇄ 舰艏 ←\n迎头相对" : battle.Alignment == NavalBattleAlignment.EnemyBroadside ? "→ 舰艏 ⊥ 侧舷\n我方对准敌舰侧面" : "舰艏 →    → 舰艏\n并排行船";
            RectTransform buffPanel = CreateFixedPanel("Battle Buff Panel", root, new Vector2(230f, 90f), new Vector2(-735f, -181f), PanelRaised);
            ApplyRetreatBattleWoodBoard(buffPanel.GetComponent<Image>());
            buffPanel.GetComponent<Image>().raycastTarget = false;
            Text buff = CreateText("Battle Active Buffs", buffPanel, new Vector2(212f, 80f), Vector2.zero, 13, FontStyle.Normal, TextAnchor.MiddleCenter, Ink);
            buff.text = BattleActiveBuffText(battle);
            RectTransform rulePlate = CreateRect("Battle Card Cost Rule Panel", root,
                new Vector2(800f, 36f), new Vector2(0f, 351f));
            Text cardRule = CreateText("Battle Card Cost Rule", rulePlate, new Vector2(784f, 28f), Vector2.zero,
                14, FontStyle.Bold, TextAnchor.MiddleCenter, Gold);
            cardRule.text = "同名牌连用 +1 费 · 副本共享 · 下回合或切换阶段重置";
            cardRule.raycastTarget = false;
            UiLabelBacking.Apply(cardRule);
            RectTransform cursePanel = CreateRect("Battle Curse Panel", root,
                new Vector2(800f, 30f), new Vector2(0f, 316f));
            Text curse = CreateText("Battle Curse Status", cursePanel, new Vector2(784f, 28f),
                Vector2.zero, 16, FontStyle.Bold, TextAnchor.MiddleCenter,
                session.CurseCount > 0 ? Coral : MutedInk);
            curse.text = CurseBattleStatus;
            curse.raycastTarget = false;
            UiLabelBacking.Apply(curse, session.CurseCount > 0);
            if (battle.Stage == NavalBattleStage.Naval)
            {
                bool ramAllowed = session.CanRam(out string ramReason);
                bool boardAllowed = session.CanBoard(out string boardReason);
                Button ram = CreateButton("Battle Ram", root, new Vector2(230f, 48f), new Vector2(735f, -115f), Gold, Ink, 17);
                ram.GetComponentInChildren<Text>().text = "冲撞并接舷 · 1 能量";
                ram.interactable = ramAllowed;
                ram.onClick.AddListener(() => BeginNavalSpecialAction(false));
                Button board = CreateButton("Battle Board", root, new Vector2(230f, 48f), new Vector2(735f, -185f), Teal, Ink, 17);
                board.GetComponentInChildren<Text>().text = "靠近接舷 · 切换近战";
                board.interactable = boardAllowed;
                board.onClick.AddListener(() => BeginNavalSpecialAction(true));
                Text ramHint = CreateText("Battle Ram Condition", root, new Vector2(230f, 25f), new Vector2(735f, -149f), 12, FontStyle.Normal, TextAnchor.MiddleCenter, Ink);
                ramHint.text = ramAllowed ? "撞击方先手 · 舰艏品质对拼" : ramReason;
                Text boardHint = CreateText("Battle Board Condition", root, new Vector2(230f, 25f), new Vector2(735f, -219f), 12, FontStyle.Normal, TextAnchor.MiddleCenter, Ink);
                boardHint.text = boardAllowed ? "普通水手数影响接舷生命上限" : boardReason;
                Text quality = CreateText("Battle Collision Quality", root, new Vector2(1000f, 24f), new Vector2(0f, -51f), 13, FontStyle.Normal, TextAnchor.MiddleCenter, Ink);
                quality.rectTransform.anchoredPosition = new Vector2(0f, -19f);
                quality.text = "舰艏对拼 " + battle.PlayerBowQuality.ToString("0.0") + " / " + battle.EnemyBowQuality.ToString("0.0") +
                    "     船身品质 " + battle.PlayerHullQuality.ToString("0.0") + " / " + battle.EnemyHullQuality.ToString("0.0");
            }
        }

        private static string BoardingConnectionLabel(CoreBattleState battle)
        {
            if (battle == null) return "船体 → 船员生命\n甲板近战视角";
            string initiative = battle.BoardingInitiatedByEnemy ? "接舷先手：敌方" : "接舷先手：我方";
            switch (battle.BoardingConnection)
            {
                case NavalBoardingConnection.RamBowToBow: return "角对角冲撞 · 横向接舷\n" + initiative;
                case NavalBoardingConnection.RamBowToBroadside: return "角对侧舷冲撞 · 斜向接舷\n" + initiative;
                default: return "船体 → 船员生命\n" + initiative;
            }
        }

        private static string BoardingConnectionInstructions(CoreBattleState battle)
        {
            if (battle != null && battle.BoardingInitiatedByEnemy)
                return (battle.BoardingConnection == NavalBoardingConnection.RamBowToBroadside
                    ? "敌舰舰艏卡住我方侧舷" : "敌舰迎头撞击并锁定船体") + " · 敌方先手 · 白刃牌组已切换";
            if (battle != null && battle.BoardingConnection == NavalBoardingConnection.RamBowToBow)
                return "角对角冲撞已锁定船体 · 横向跳板连接 · 白刃牌组已切换";
            if (battle != null && battle.BoardingConnection == NavalBoardingConnection.RamBowToBroadside)
                return "角对侧舷冲撞已卡住敌舰 · 斜向钩索连接 · 白刃牌组已切换";
            return "白刃牌组已切换 · 击败守备队即可夺船，无需击沉";
        }

        private void BeginNavalSpecialAction(bool boarding)
        {
            if (!isActiveAndEnabled || battleAnimationPlaying || session.Battle == null) return;
            if (boarding ? !session.CanBoard(out _) : !session.CanRam(out _)) return;
            StartCoroutine(AnimateNavalSpecialAction(boarding));
        }

        private IEnumerator AnimateNavalSpecialAction(bool boarding)
        {
            battleAnimationPlaying = true;
            try
            {
                CoreBattleState battle = session.Battle;
                BattleAnimationSnapshot before = new BattleAnimationSnapshot(session, battle);
                RectTransform root = GameObject.Find("Battle Card").GetComponent<RectTransform>();
                RectTransform player = GameObject.Find("Player Ship Art").GetComponent<RectTransform>();
                RectTransform enemy = GameObject.Find("Enemy Ship Art").GetComponent<RectTransform>();
                LockBattleInput(root);
                if (!(boarding ? session.TryBoard() : session.TryRam())) yield break;
                BattleAnimationSnapshot after = new BattleAnimationSnapshot(session, session.Battle ?? battle);
                if (boarding)
                {
                    yield return AnimateBoardingApproach(root, player, enemy, battle.BoardingApproach);
                    yield return AnimateFloatingText(root, new Vector2(0f, 110f), "抛出钩索 · 登上敌舰", Gold, "Boarding Transition");
                    yield return AnimateBoardingSceneTransition();
                }
                else
                {
                    Texture2D ramComic = RamComicTexture(false, before.Alignment);
                    if (ramComic != null) yield return AnimateRamComic(ramComic, false, before, after, 0);
                    else
                    {
                        // A missing optional comic retains the existing physical
                        // presentation and never prevents the committed action.
                        yield return AnimateShipWindup(player, DirectionToward(player, enemy), true);
                        yield return AnimateImpact(root, enemy, Coral, "冲撞 -" + (before.EnemyHull - after.EnemyHull), "Enemy Ram Impact");
                        yield return AnimateHealthBar(root.Find("Enemy Battle Hull Bar Track/Enemy Battle Hull Bar")?.GetComponent<Image>(), before.EnemyHull, after.EnemyHull, before.EnemyMaxHull, Coral);
                        yield return AnimateImpact(root, player, Coral, "反伤 -" + (before.PlayerHull - after.PlayerHull), "Player Ram Recoil");
                        yield return AnimateHealthBar(root.Find("Player Battle Hull Bar Track/Player Battle Hull Bar")?.GetComponent<Image>(), before.PlayerHull, after.PlayerHull, before.PlayerMaxHull, Coral);
                        if (session.Phase == CoreDemoPhase.Battle && after.Stage == NavalBattleStage.Boarding)
                        {
                            yield return AnimateBoardingApproach(root, player, enemy, battle.BoardingApproach);
                            yield return AnimateFloatingText(root, new Vector2(0f, 110f),
                                battle.BoardingConnection == NavalBoardingConnection.RamBowToBow ? "角对角锁定 · 接舷近战" : "角对侧舷卡锁 · 接舷近战",
                                Gold, "Ram Boarding Transition");
                            yield return AnimateBoardingSceneTransition();
                        }
                    }
                }
                if (session.Phase != CoreDemoPhase.Battle)
                    yield return AnimateStagedBattleResolution(root, player, enemy, before, after);
            }
            finally { CompleteStagedBattleAnimation(); }
        }
    }
}

