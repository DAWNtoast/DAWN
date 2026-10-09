using System;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        public const string TreasureProgressKey = "ShatteredSail.TreasureProgress.v1";
        private bool treasureProgressReadFailed;

        private void RestoreTreasureProgress()
        {
            treasureProgressReadFailed = false;
            if (!PlayerPrefs.HasKey(TreasureProgressKey)) return;
            string json = PlayerPrefs.GetString(TreasureProgressKey, string.Empty);
            try
            {
                CoreTreasureProgress progress = JsonUtility.FromJson<CoreTreasureProgress>(json);
                if (progress != null) session.RestoreTreasureProgress(progress);
                else treasureProgressReadFailed = true;
            }
            catch (ArgumentException)
            {
                // An invalid optional record must not prevent an existing captain
                // from opening the rest of their profile. Keep the original JSON
                // untouched until a later load successfully reads a repaired record.
                treasureProgressReadFailed = true;
            }
            if (treasureProgressReadFailed)
                portNotice = "藏宝档案无法读取，原始档案已保留，本次不会覆盖。请修复后重新载入。";
        }

        private void SaveTreasureProgress()
        {
            if (treasureProgressReadFailed) return;
            PlayerPrefs.SetString(TreasureProgressKey, JsonUtility.ToJson(session.CaptureTreasureProgress()));
        }

        private void BuildTreasureDepartureRoute(RectTransform panel)
        {
            CoreTreasureMapRecord map = session.SelectedTreasureMap;
            if (map == null) return;
            Text title = CreateText("Treasure Departure Title", panel, new Vector2(1050f, 48f),
                new Vector2(0f, 240f), 29, FontStyle.Bold, TextAnchor.MiddleCenter, Gold);
            title.text = map.Name + " · 隐藏海域";
            RawImage illustration = CreateRawImage("Treasure Departure Art", panel, new Vector2(580f, 326f),
                new Vector2(-235f, 48f), Resources.Load<Texture2D>("TreasureHunt/treasure_hunt_route_map"), new Rect(0, 0, 1, 1));
            illustration.raycastTarget = false;
            Text detail = CreateText("Treasure Departure Detail", panel, new Vector2(430f, 270f),
                new Vector2(315f, 50f), 22, FontStyle.Normal, TextAnchor.MiddleLeft, Ink);
            detail.text = "品阶：" + map.Rank.Name + "\n推荐战力：" + map.Rank.RecommendedPower +
                "\n开启秘藏需要 " + map.Rank.RequiredClues + " 条线索\n种子：" + map.Seed +
                "\n探险结束返回：" + session.CurrentPort;
            Text consumption = CreateText("Treasure Departure Consumption", panel, new Vector2(1030f, 90f),
                new Vector2(0f, -184f), 21, FontStyle.Bold, TextAnchor.MiddleCenter, Gold);
            bool canStart = session.CanStartTreasureVoyage(out string reason);
            consumption.text = "出航时消耗这张藏宝图 · 中途撤离或失败不返还\n" +
                (canStart ? "整备校验通过。开启秘藏后以 95% 基础保护率返港。" : reason + "；藏宝图仍保留。") +
                "\n这是一条一次性隐藏航路，不能绘制成普通海图。";
            Button cancel = CreateButton("Cancel Treasure Voyage", panel, new Vector2(440f, 56f),
                new Vector2(-260f, -282f), PanelRaised, Ink, 21);
            cancel.GetComponentInChildren<Text>().text = "卸下藏宝图 · 改为普通航线";
            cancel.onClick.AddListener(() => { session.ClearSelectedTreasureMap(); SaveProgress(); Render(); });
            Button table = CreateButton("Return To Treasure Table", panel, new Vector2(440f, 56f),
                new Vector2(260f, -282f), PanelRaised, Gold, 21);
            table.GetComponentInChildren<Text>().text = "返回藏宝图桌";
            table.onClick.AddListener(() => { activePortFacility = 6; Render(); });
        }

        private CoreMapNode TreasureCoreNode()
        {
            if (!session.IsTreasureVoyage || session.Map == null) return null;
            foreach (CoreMapNode node in session.Map.Nodes)
                if (node.Encounter.Id == "treasure_core") return node;
            return null;
        }

        private static Rect TreasureEncounterUv(string encounterId)
        {
            string[] cells = {
                "treasure_clue_bottle", "treasure_clue_whalebone", "treasure_clue_star", "treasure_clue_fogbell", "treasure_clue_twinmoon",
                "treasure_false_cache", "treasure_reef_snare", "treasure_rival_sloop", "treasure_drowned_shrine", "treasure_salt_spring",
                "treasure_siren_conch", "treasure_ghost_fleet", "treasure_smuggler_jetty", "treasure_brine_geyser", "treasure_coral_maze",
                "treasure_castaway_cartographer", "treasure_pearl_crab", "treasure_core", "random_food_spoil", "random_ambush"
            };
            int index;
            switch (encounterId)
            {
                // Reuse the atlas's ship and storm cells for the five
                // legendary encounters so treasure battles never fall back to
                // a generic supply illustration.
                case "legendary_pirate_iron_oath": index = 11; break;
                case "legendary_pirate_red_widow": index = 6; break;
                case "legendary_pirate_thunder_crown": index = 2; break;
                case "legendary_pirate_ember_ark": index = 13; break;
                case "legendary_pirate_tide_requiem": index = 19; break;
                default: index = Array.IndexOf(cells, encounterId); break;
            }
            if (index < 0) index = encounterId != null && encounterId.Contains("ambush") ? 19 : 18;
            return new Rect((index % 5) / 5f, 1f - (index / 5 + 1) / 4f, 1f / 5f, 1f / 4f);
        }

        private string TreasureNavigationReadout()
        {
            CoreMapNode core = TreasureCoreNode();
            string distance = string.Empty;
            if (core != null)
            {
                float dx = core.Position.X - session.Position.X;
                float dy = core.Position.Y - session.Position.Y;
                string bearing = (Mathf.Abs(dy) < 1f ? string.Empty : dy > 0 ? "北" : "南") +
                    (Mathf.Abs(dx) < 1f ? string.Empty : dx > 0 ? "东" : "西");
                if (bearing.Length == 2) bearing = bearing.Substring(1, 1) + bearing.Substring(0, 1);
                if (bearing.Length == 0) bearing = "当前位置";
                distance = " · 秘藏 " + bearing + " " +
                    (Mathf.Sqrt(dx * dx + dy * dy) * session.Map.Route.NauticalMilesPerMapUnit).ToString("0") + " 海里";
            }
            return "藏宝线索 " + session.TreasureClues + " / " + session.TreasureRequiredClues + distance;
        }
    }
}
