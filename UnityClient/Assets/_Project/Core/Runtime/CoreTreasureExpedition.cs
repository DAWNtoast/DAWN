using System;
using System.Collections.Generic;
using System.Linq;

namespace ShatteredSail.Core.Gameplay
{
    public sealed partial class CoreVoyageSession
    {
        private readonly HashSet<string> treasureExpeditionMarks = new HashSet<string>(StringComparer.Ordinal);
        private int treasureExpeditionCredit, treasureExpeditionHeat, treasureExpeditionResonance;
        public bool IsTreasureExpedition => IsTreasureVoyage && ActiveTreasureMap?.ContentVersion == 1;
        public int TreasureExpeditionCredit => treasureExpeditionCredit;
        public int TreasureExpeditionHeat => treasureExpeditionHeat;
        public int TreasureExpeditionResonance => treasureExpeditionResonance;
        public bool HasTreasureMark(string mark) => mark != null && treasureExpeditionMarks.Contains(mark);

        // New expedition rituals follow the visible voyage clock: day 06–17,
        // dusk 17–20, night 20–06. Keep the legacy four-band rules unchanged
        // for existing events and component environmental bonuses.
        public CoreVoyageTimeBand CurrentTreasureExpeditionTimeBand
        {
            get
            {
                double hour = GameDateTime.TimeOfDay.TotalHours;
                return hour >= 17d && hour < 20d ? CoreVoyageTimeBand.Dusk :
                    hour >= 20d || hour < 6d ? CoreVoyageTimeBand.Night : CoreVoyageTimeBand.Day;
            }
        }

        public static string TreasureMarkLabel(string mark)
        {
            switch (mark)
            {
                case "weathered_winch": return "吊装绞盘";
                case "weathered_bell": return "潜水钟";
                case "weathered_camp": return "潮工营地";
                case "gilded_manifest": return "公证货单";
                case "gilded_witness": return "证人证词";
                case "moonlit_dawn": return "昼辉相";
                case "moonlit_dusk": return "暮潮相";
                case "moonlit_midnight": return "夜幕相";
                default: return mark ?? string.Empty;
            }
        }

        public string TreasureExpeditionStatusSummary
        {
            get
            {
                if (!IsTreasureExpedition) return string.Empty;
                string Tool(string mark) => TreasureMarkLabel(mark) + (HasTreasureMark(mark) ? "✓" : "未备");
                switch (ActiveTreasureMap.RankId)
                {
                    case "weathered": return "沉船工坊 · " + Tool("weathered_winch") + " / " + Tool("weathered_bell") +
                        "；双工具开启主机舱";
                    case "gilded": return "商盟密约 · 信用 " + treasureExpeditionCredit + "/6 · 通缉 " + treasureExpeditionHeat +
                        "/5 · " + Tool("gilded_manifest") + "；兑付需信用≥3、通缉≤1";
                    case "moonlit": return "三相仪式 · 共鸣 " + treasureExpeditionResonance + "/3 · 诅咒 " + Cargo.Curse +
                        " · 当前" + TimeBandLabel(CurrentTreasureExpeditionTimeBand) + "；完整仪式需3相、诅咒≤1";
                    default: return string.Empty;
                }
            }
        }

        public static string TreasureExpeditionEffectSummary(VoyageEffect effect)
        {
            if (effect == null) return string.Empty;
            var parts = new List<string>();
            string Signed(int value) => value > 0 ? "+" + value : value.ToString();
            if (!string.IsNullOrEmpty(effect.TreasureMark)) parts.Add("取得" + TreasureMarkLabel(effect.TreasureMark));
            if (effect.TreasureCredit != 0) parts.Add("信用 " + Signed(effect.TreasureCredit));
            if (effect.TreasureHeat != 0) parts.Add("通缉 " + Signed(effect.TreasureHeat));
            if (effect.TreasureResonance != 0) parts.Add("共鸣 " + Signed(effect.TreasureResonance));
            return string.Join("、", parts);
        }

        private void ResetTreasureExpeditionState()
        {
            treasureExpeditionMarks.Clear();
            treasureExpeditionCredit = treasureExpeditionHeat = treasureExpeditionResonance = 0;
        }

        private static bool IsTreasureExpeditionEvent(string id) => id != null &&
            (id.StartsWith("weathered_", StringComparison.Ordinal) || id.StartsWith("gilded_", StringComparison.Ordinal) ||
                id.StartsWith("moonlit_", StringComparison.Ordinal));

        private static string TreasureMarkRank(string mark)
        {
            switch (mark)
            {
                case "weathered_winch": case "weathered_bell": case "weathered_camp": return "weathered";
                case "gilded_manifest": case "gilded_witness": return "gilded";
                case "moonlit_dawn": case "moonlit_dusk": case "moonlit_midnight": return "moonlit";
                default: return string.Empty;
            }
        }

        private string TreasureExpeditionConditionReason(CoreEncounterChoice choice)
        {
            VoyageEffect effect = choice.Effect;
            bool usesState = !string.IsNullOrEmpty(choice.RequiredTreasureMark) || choice.MinimumTreasureCredit > 0 ||
                choice.MaximumTreasureHeat < 5 || choice.MinimumTreasureResonance > 0 || !string.IsNullOrEmpty(effect.TreasureMark) ||
                effect.TreasureCredit != 0 || effect.TreasureHeat != 0 || effect.TreasureResonance != 0;
            if (!usesState) return string.Empty;
            if (!IsTreasureExpedition) return "需要本航次的专属藏宝海图。";
            foreach (string mark in choice.RequiredTreasureMark.Split(new[] { '+' }, StringSplitOptions.RemoveEmptyEntries))
                if (!HasTreasureMark(mark)) return "需要先探索取得：" + TreasureMarkLabel(mark) + "。";
            if (treasureExpeditionCredit < choice.MinimumTreasureCredit || (long)treasureExpeditionCredit + effect.TreasureCredit < 0)
                return "商盟信用不足，需要 " + Math.Max(choice.MinimumTreasureCredit, -(long)effect.TreasureCredit) + "（当前 " + treasureExpeditionCredit + "）。";
            if (treasureExpeditionHeat > choice.MaximumTreasureHeat)
                return "通缉过高，需要降至 " + choice.MaximumTreasureHeat + " 或以下（当前 " + treasureExpeditionHeat + "）。";
            if (treasureExpeditionResonance < choice.MinimumTreasureResonance || (long)treasureExpeditionResonance + effect.TreasureResonance < 0)
                return "月潮共鸣不足，需要 " + Math.Max(choice.MinimumTreasureResonance, -(long)effect.TreasureResonance) + "（当前 " + treasureExpeditionResonance + "）。";
            if (!string.IsNullOrEmpty(effect.TreasureMark))
            {
                if (TreasureMarkRank(effect.TreasureMark) != ActiveTreasureMap.RankId) return "此印记不属于本藏宝海图。";
                if (HasTreasureMark(effect.TreasureMark)) return "已经取得" + TreasureMarkLabel(effect.TreasureMark) + "，无法重复领取。";
            }
            if ((effect.TreasureCredit != 0 || effect.TreasureHeat != 0) && ActiveTreasureMap.RankId != "gilded" ||
                effect.TreasureResonance != 0 && ActiveTreasureMap.RankId != "moonlit") return "此进度不属于本藏宝海图。";
            return string.Empty;
        }

        private static void AppendTreasureExpeditionRequirements(List<string> requirements, CoreEncounterChoice choice)
        {
            foreach (string mark in choice.RequiredTreasureMark.Split(new[] { '+' }, StringSplitOptions.RemoveEmptyEntries))
                requirements.Add("需要" + TreasureMarkLabel(mark));
            if (choice.MinimumTreasureCredit > 0) requirements.Add("信用≥" + choice.MinimumTreasureCredit);
            if (choice.MaximumTreasureHeat < 5) requirements.Add("通缉≤" + choice.MaximumTreasureHeat);
            if (choice.MinimumTreasureResonance > 0) requirements.Add("共鸣≥" + choice.MinimumTreasureResonance);
        }

        private void ApplyTreasureExpeditionEffect(VoyageEffect effect)
        {
            if (!IsTreasureExpedition || effect == null) return;
            string rank = ActiveTreasureMap.RankId;
            if (TreasureMarkRank(effect.TreasureMark) == rank && treasureExpeditionMarks.Add(effect.TreasureMark))
                AddLog("探索取得「" + TreasureMarkLabel(effect.TreasureMark) + "」，本航次可使用。");
            if (rank == "gilded")
            {
                treasureExpeditionCredit = (int)Math.Max(0L, Math.Min(6L, (long)treasureExpeditionCredit + effect.TreasureCredit));
                treasureExpeditionHeat = (int)Math.Max(0L, Math.Min(5L, (long)treasureExpeditionHeat + effect.TreasureHeat));
            }
            if (rank == "moonlit") treasureExpeditionResonance = (int)Math.Max(0L, Math.Min(3L, (long)treasureExpeditionResonance + effect.TreasureResonance));
            string summary = TreasureExpeditionEffectSummary(effect);
            if (!string.IsNullOrEmpty(summary)) AddLog(summary + "；" + TreasureExpeditionStatusSummary);
        }

        private bool ValidateTreasureExpeditionCheckpoint(CoreVoyageCheckpoint checkpoint)
        {
            string[] marks = checkpoint.treasureExpeditionMarks ?? Array.Empty<string>();
            int credit = checkpoint.treasureExpeditionCredit, heat = checkpoint.treasureExpeditionHeat,
                resonance = checkpoint.treasureExpeditionResonance;
            if (credit < 0 || credit > 6 || heat < 0 || heat > 5 || resonance < 0 || resonance > 3 ||
                marks.Length > 3 || marks.Distinct(StringComparer.Ordinal).Count() != marks.Length) return false;
            if (!checkpoint.isTreasureVoyage || ActiveTreasureMap?.ContentVersion != 1)
                return credit == 0 && heat == 0 && resonance == 0 && marks.Length == 0;
            string rank = ActiveTreasureMap.RankId;
            if (marks.Any(mark => string.IsNullOrEmpty(mark) || TreasureMarkRank(mark) != rank)) return false;
            if (rank != "gilded" && (credit != 0 || heat != 0) || rank != "moonlit" && resonance != 0) return false;
            // Each of the three altars is one-use. A checkpoint cannot invent or
            // drop resonance independently from its already resolved rituals.
            return rank != "moonlit" || resonance == marks.Length;
        }
    }
}
