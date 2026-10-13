using System;
using System.Collections.Generic;

namespace ShatteredSail.Core.Tutorial
{
    public enum TutorialVoyagePhase
    {
        AwaitingDeparture,
        ReadyToSail,
        ResolvingEncounter,
        Battle,
        FirstLegComplete,
        Complete,
    }

    public enum TutorialBattleAction
    {
        FireCannons,
        Brace,
        EmergencyRepair,
    }

    public sealed class TutorialEffect
    {
        public int Hull { get; set; }
        public int Water { get; set; }
        public int Food { get; set; }
        public int Morale { get; set; }
        public int Gold { get; set; }
        public int Wood { get; set; }
        public int Beer { get; set; }
        public int Fame { get; set; }
        public bool RevealHidden { get; set; }
        public float SpeedBoost { get; set; }
        public int SpeedBoostLegs { get; set; }
    }

    public sealed class TutorialChoice
    {
        public TutorialChoice(
            string label,
            string detail,
            double durationHours,
            TutorialEffect effect,
            string requiredCrew = "",
            bool startsBattle = false,
            bool returnsToPort = false)
        {
            Label = label;
            Detail = detail;
            DurationHours = durationHours;
            Effect = effect ?? new TutorialEffect();
            RequiredCrew = requiredCrew ?? string.Empty;
            StartsBattle = startsBattle;
            ReturnsToPort = returnsToPort;
        }

        public string Label { get; }
        public string Detail { get; }
        public double DurationHours { get; }
        public TutorialEffect Effect { get; }
        public string RequiredCrew { get; }
        public bool StartsBattle { get; }
        public bool ReturnsToPort { get; }
    }

    public sealed class TutorialEncounter
    {
        public TutorialEncounter(
            string id,
            string name,
            string category,
            string description,
            bool optional,
            bool hidden,
            params TutorialChoice[] choices)
        {
            Id = id;
            Name = name;
            Category = category;
            Description = description;
            Optional = optional;
            Hidden = hidden;
            Choices = Array.AsReadOnly(choices ?? Array.Empty<TutorialChoice>());
        }

        public string Id { get; }
        public string Name { get; }
        public string Category { get; }
        public string Description { get; }
        public bool Optional { get; }
        public bool Hidden { get; }
        public IReadOnlyList<TutorialChoice> Choices { get; }
    }

    /// <summary>
    /// Deterministic, Unity-independent vertical slice of the two browser-demo
    /// tutorial voyages. Presentation code may be replaced without changing
    /// tutorial progression or its resource rules.
    /// </summary>
    public sealed class TutorialVoyageSession
    {
        public const int MaxMorale = 20;
        public const int DefaultStartingMorale = 10;

        public const string FirstLegSeed = "TUTOR-FIRST-SAIL-01";
        public const string AdvancedLegSeed = "TUTOR-NAVIGATOR-02";
        public const int CrewCount = 13;
        public const float BaseSpeedKnots = 10f;
        public const float SegmentDistanceNm = 150f;
        public const int MaxHull = 100;

        private static readonly IReadOnlyList<TutorialEncounter> firstLegEncounters = Array.AsReadOnly(new[]
        {
            new TutorialEncounter(
                "tutorial_first_event",
                "漂流补给箱",
                "不可跳过事件",
                "一只钉着断锚港印记的补给箱随浪漂来。打捞需要时间，也会消耗全船口粮。",
                false,
                false,
                new TutorialChoice("打捞补给箱", "木材 +6、金币 +20", 3, new TutorialEffect { Wood = 6, Gold = 20 }),
                new TutorialChoice("观察后离开", "声望 +2", 1, new TutorialEffect { Fame = 2 })),
            new TutorialEncounter(
                "tutorial_optional_event",
                "搁浅的酒桶",
                "可跳过事件",
                "几只酒桶搁在浅滩上。靠近检查可能有收获，也可以保持航速直接离开。",
                true,
                false,
                new TutorialChoice("靠岸检查", "啤酒 +6、木材 +3", 2, new TutorialEffect { Beer = 6, Wood = 3 }),
                new TutorialChoice("远处打捞", "啤酒 +2", 1, new TutorialEffect { Beer = 2 })),
            new TutorialEncounter(
                "tutorial_crew_requirement_event",
                "陌生海图暗语",
                "特殊船员需求",
                "一张浸水海图写满陌生暗语。航海士能迅速辨认，但首航尚未开放特殊船员编制。",
                false,
                false,
                new TutorialChoice(
                    "航海士解读暗语",
                    "显示邻近情报、声望 +3",
                    1,
                    new TutorialEffect { RevealHidden = true, Fame = 3 },
                    "navigator"),
                new TutorialChoice("让普通水手逐行核对", "金币 +12", 4, new TutorialEffect { Gold = 12 })),
            new TutorialEncounter(
                "tutorial_ambush_event",
                "黑帆伏击预警",
                "伏击预警",
                "瞭望手发现海盗正借礁影包抄。首次航行不适合接战，只能立刻转向并返回断锚港。",
                false,
                true,
                new TutorialChoice(
                    "避开伏击，立即返航",
                    "强制结束首航并返回出发港",
                    1,
                    new TutorialEffect(),
                    returnsToPort: true)),
        });

        private static readonly IReadOnlyList<TutorialEncounter> advancedLegEncounters = Array.AsReadOnly(new[]
        {
            new TutorialEncounter(
                "tutorial_navigator_event",
                "偏航的星盘",
                "特殊船员事件",
                "海面浮着一座失准星盘。疯帽航海士辨认出它正在指向一处被雾隐藏的灯标。",
                false,
                false,
                new TutorialChoice(
                    "航海士校准星位",
                    "显示隐藏地点，后续 2 航段航速 +1",
                    2,
                    new TutorialEffect { RevealHidden = true, SpeedBoost = 1f, SpeedBoostLegs = 2 },
                    "navigator")),
            new TutorialEncounter(
                "tutorial_hidden_event",
                "雾礁暗灯",
                "隐藏事件",
                "雾中的假灯把船引向礁缝，灯座下压着一箱尚未受潮的修船木料。",
                false,
                true,
                new TutorialChoice("熄灯并回收木料", "木材 +10、船体 -2", 2, new TutorialEffect { Wood = 10, Hull = -2 }),
                new TutorialChoice(
                    "老木匠拆除灯架",
                    "木材 +8、船体 +2",
                    1,
                    new TutorialEffect { Wood = 8, Hull = 2 },
                    "carpenter")),
            new TutorialEncounter(
                "tutorial_boss_event",
                "裂桅号演习舰",
                "教学首领",
                "裂桅号的旧演习舰封住港口水道。只有击破它，另一端港口才会准许靠泊。",
                false,
                false,
                new TutorialChoice(
                    "进入卡牌对战",
                    "挑战教学首领",
                    1,
                    new TutorialEffect(),
                    startsBattle: true)),
        });

        private readonly List<string> logEntries = new List<string>();
        private double accumulatedCrewDays;
        private float speedBoost;
        private int speedBoostLegs;
        private int battleTurn;

        public TutorialVoyageSession()
        {
            Reset();
        }

        public TutorialVoyagePhase Phase { get; private set; }
        public int Leg { get; private set; }
        public int CurrentNodeIndex { get; private set; }
        public TutorialEncounter CurrentEncounter { get; private set; }
        public bool HasNavigator { get; private set; }
        public bool HasCarpenter { get; private set; }
        public bool HiddenLocationRevealed { get; private set; }
        public bool ChartSaved { get; private set; }
        public int Hull { get; private set; }
        public int Water { get; private set; }
        public int Food { get; private set; }
        public int Morale { get; private set; }
        public int Gold { get; private set; }
        public int Wood { get; private set; }
        public int Beer { get; private set; }
        public int Fame { get; private set; }
        public float TotalNauticalMiles { get; private set; }
        public double TotalHours { get; private set; }
        public double LastTravelHours { get; private set; }
        public int BattleEnemyHull { get; private set; }
        public int BattleEnemyMaxHull { get; private set; }
        public int BattleBlock { get; private set; }
        public string LastBattleMessage { get; private set; }
        public IReadOnlyList<string> LogEntries => logEntries;
        public string Seed => Leg == 2 ? AdvancedLegSeed : FirstLegSeed;
        public float CurrentSpeedKnots => BaseSpeedKnots + (speedBoostLegs > 0 ? speedBoost : 0f);
        public int NextEnemyDamage => new[] { 6, 8, 10 }[battleTurn % 3];
        public IReadOnlyList<TutorialEncounter> CurrentLegEncounters => Leg == 2 ? advancedLegEncounters : firstLegEncounters;

        public void Reset()
        {
            Phase = TutorialVoyagePhase.AwaitingDeparture;
            Leg = 0;
            CurrentNodeIndex = -1;
            CurrentEncounter = null;
            HasNavigator = false;
            HasCarpenter = false;
            HiddenLocationRevealed = false;
            ChartSaved = false;
            Hull = MaxHull;
            Water = 70;
            Food = 70;
            Morale = DefaultStartingMorale;
            Gold = 100;
            Wood = 8;
            Beer = 4;
            Fame = 0;
            TotalNauticalMiles = 0f;
            TotalHours = 0d;
            LastTravelHours = 0d;
            accumulatedCrewDays = 0d;
            speedBoost = 0f;
            speedBoostLegs = 0;
            BattleEnemyHull = 0;
            BattleEnemyMaxHull = 0;
            BattleBlock = 0;
            battleTurn = 0;
            LastBattleMessage = string.Empty;
            logEntries.Clear();
            AddLog("已在断锚港完成轻帆船首航整备。");
        }

        public void BeginFirstLeg()
        {
            if (Phase != TutorialVoyagePhase.AwaitingDeparture)
            {
                throw new InvalidOperationException("The first tutorial leg can only begin from the departure screen.");
            }

            Leg = 1;
            CurrentNodeIndex = -1;
            Phase = TutorialVoyagePhase.ReadyToSail;
            AddLog("驶离断锚港：特殊船员系统暂时锁定。");
        }

        public void BeginAdvancedLeg()
        {
            if (Phase != TutorialVoyagePhase.FirstLegComplete)
            {
                throw new InvalidOperationException("The advanced tutorial leg requires the first leg to be complete.");
            }

            Leg = 2;
            CurrentNodeIndex = -1;
            CurrentEncounter = null;
            HasNavigator = true;
            HiddenLocationRevealed = false;
            Hull = MaxHull;
            Water = Math.Max(Water, 70);
            Food = Math.Max(Food, 70);
            accumulatedCrewDays = 0d;
            speedBoost = 0f;
            speedBoostLegs = 0;
            Phase = TutorialVoyagePhase.ReadyToSail;
            AddLog("疯帽航海士加入编队，进阶训练开始。");
        }

        public TutorialEncounter SailToNext()
        {
            if (Phase != TutorialVoyagePhase.ReadyToSail)
            {
                throw new InvalidOperationException("Sailing is only allowed while the session is ready for the next leg.");
            }

            CurrentNodeIndex += 1;
            ConsumeTravelSegment();

            if (Leg == 2 && CurrentNodeIndex >= advancedLegEncounters.Count)
            {
                CurrentEncounter = null;
                ChartSaved = true;
                Phase = TutorialVoyagePhase.Complete;
                AddLog("抵达灰鸥港，航海士已保存本次教学海图。");
                return null;
            }

            CurrentEncounter = CurrentLegEncounters[CurrentNodeIndex];
            Phase = TutorialVoyagePhase.ResolvingEncounter;
            AddLog("遭遇「" + CurrentEncounter.Name + "」。");
            return CurrentEncounter;
        }

        public bool IsChoiceAvailable(int choiceIndex)
        {
            if (CurrentEncounter == null || choiceIndex < 0 || choiceIndex >= CurrentEncounter.Choices.Count)
            {
                return false;
            }

            string requiredCrew = CurrentEncounter.Choices[choiceIndex].RequiredCrew;
            if (requiredCrew == "navigator")
            {
                return HasNavigator;
            }

            if (requiredCrew == "carpenter")
            {
                return HasCarpenter;
            }

            return true;
        }

        public bool ResolveChoice(int choiceIndex)
        {
            if (Phase != TutorialVoyagePhase.ResolvingEncounter || !IsChoiceAvailable(choiceIndex))
            {
                return false;
            }

            TutorialChoice choice = CurrentEncounter.Choices[choiceIndex];
            ConsumeHours(choice.DurationHours);
            ApplyEffect(choice.Effect);
            AddLog("选择「" + choice.Label + "」：" + choice.Detail + "。");

            if (choice.StartsBattle)
            {
                StartBattle();
                return true;
            }

            if (choice.ReturnsToPort)
            {
                Water = Math.Max(0, Water - 2);
                Food = Math.Max(0, Food - 1);
                CurrentEncounter = null;
                Phase = TutorialVoyagePhase.FirstLegComplete;
                AddLog("安全返航消耗淡水 2、食物 1；酒馆与特殊船员系统已解锁。");
                return true;
            }

            CurrentEncounter = null;
            Phase = TutorialVoyagePhase.ReadyToSail;
            return true;
        }

        public bool SkipCurrentEncounter()
        {
            if (Phase != TutorialVoyagePhase.ResolvingEncounter || CurrentEncounter == null || !CurrentEncounter.Optional)
            {
                return false;
            }

            AddLog("略过「" + CurrentEncounter.Name + "」：没有推进事件时间，也没有获得奖励。");
            CurrentEncounter = null;
            Phase = TutorialVoyagePhase.ReadyToSail;
            return true;
        }

        public bool IsBattleActionAvailable(TutorialBattleAction action)
        {
            return Phase == TutorialVoyagePhase.Battle &&
                (action != TutorialBattleAction.EmergencyRepair || Wood >= 2);
        }

        public bool PerformBattleAction(TutorialBattleAction action)
        {
            if (!IsBattleActionAvailable(action))
            {
                return false;
            }

            switch (action)
            {
                case TutorialBattleAction.FireCannons:
                    BattleEnemyHull = Math.Max(0, BattleEnemyHull - 9);
                    Morale = ClampMorale(Morale + 2);
                    LastBattleMessage = "火炮齐射造成 9 点伤害。";
                    break;
                case TutorialBattleAction.Brace:
                    BattleBlock += 8;
                    LastBattleMessage = "全员防御，获得 8 点格挡。";
                    break;
                case TutorialBattleAction.EmergencyRepair:
                    Wood -= 2;
                    Hull = Math.Min(MaxHull, Hull + 6);
                    LastBattleMessage = "消耗木材 2，紧急修复 6 点船体。";
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(action), action, null);
            }

            if (BattleEnemyHull <= 0)
            {
                Gold += 40;
                Fame += 10;
                CurrentEncounter = null;
                Phase = TutorialVoyagePhase.ReadyToSail;
                LastBattleMessage += " 演习舰失去战斗能力，航道已经打开。";
                AddLog("击败裂桅号演习舰：金币 +40、声望 +10。");
                return true;
            }

            int incoming = NextEnemyDamage;
            int absorbed = Math.Min(BattleBlock, incoming);
            int damage = incoming - absorbed;
            BattleBlock = 0;
            Hull = Math.Max(1, Hull - damage);
            if (damage > 0)
                Morale = ClampMorale(Morale - 1);
            battleTurn += 1;
            LastBattleMessage += " 敌舰反击 " + incoming + "，格挡 " + absorbed + "，船体损失 " + damage + "。";
            return true;
        }

        public string GetObjective()
        {
            switch (Phase)
            {
                case TutorialVoyagePhase.AwaitingDeparture:
                    return "保持默认轻帆船、13 名水手与补给，开始第一次出航。";
                case TutorialVoyagePhase.FirstLegComplete:
                    return "首航完成。招募疯帽航海士，准备进阶航行。";
                case TutorialVoyagePhase.Complete:
                    return "教学完成：新港已解锁，种子与航迹已经保存为海图。";
                case TutorialVoyagePhase.Battle:
                    return "观察敌舰下一次伤害，在炮击、防御与修理之间选择。";
                case TutorialVoyagePhase.ResolvingEncounter:
                    return CurrentEncounter == null ? string.Empty : GetEncounterObjective(CurrentEncounter.Id);
                case TutorialVoyagePhase.ReadyToSail:
                    return GetSailingObjective();
                default:
                    return string.Empty;
            }
        }

        private string GetSailingObjective()
        {
            if (Leg == 1)
            {
                string[] objectives =
                {
                    "驶向第一处信号，学习处理不可跳过事件。",
                    "驶向浅滩信号，学习直接略过可选事件。",
                    "检查陌生海图，观察职业船员选项的限制。",
                    "沿警戒航向前进；伏击属于无地图坐标的随机事件。",
                };
                return objectives[Math.Min(CurrentNodeIndex + 1, objectives.Length - 1)];
            }

            string[] advancedObjectives =
            {
                "前往星盘信号，使用航海士职业选项。",
                "根据新情报驶向刚刚显露的隐藏地点。",
                "驶向首领信号，进入简化卡牌战斗。",
                "航道已经开放，驶入灰鸥港并保存海图。",
            };
            return advancedObjectives[Math.Min(CurrentNodeIndex + 1, advancedObjectives.Length - 1)];
        }

        private static string GetEncounterObjective(string encounterId)
        {
            switch (encounterId)
            {
                case "tutorial_first_event":
                    return "强制事件没有略过按钮，选择一种处理方式。";
                case "tutorial_optional_event":
                    return "点击“略过事件”，确认略过不会推进事件时间。";
                case "tutorial_crew_requirement_event":
                    return "航海士尚未加入；观察禁用的职业选项，再让普通水手解读。";
                case "tutorial_ambush_event":
                    return "选择避开伏击并返航，解锁特殊船员系统。";
                case "tutorial_navigator_event":
                    return "使用航海士校准星位，揭示隐藏事件坐标。";
                case "tutorial_hidden_event":
                    return "处理雾礁暗灯；木匠未编入船队，因此其职业选项不可用。";
                case "tutorial_boss_event":
                    return "进入卡牌战斗，击败封锁航道的演习舰。";
                default:
                    return string.Empty;
            }
        }

        private void ConsumeTravelSegment()
        {
            float speed = CurrentSpeedKnots;
            LastTravelHours = SegmentDistanceNm / speed;
            ConsumeHours(LastTravelHours);
            TotalNauticalMiles += SegmentDistanceNm;
            AddLog("航行 " + SegmentDistanceNm.ToString("0") + " 海里，用时 " + LastTravelHours.ToString("0.0") + " 小时。" );

            if (speedBoostLegs > 0)
            {
                speedBoostLegs -= 1;
                if (speedBoostLegs == 0)
                {
                    speedBoost = 0f;
                }
            }
        }

        private void ConsumeHours(double hours)
        {
            int before = (int)Math.Floor(accumulatedCrewDays);
            accumulatedCrewDays += CrewCount * Math.Max(0d, hours) / 24d;
            int after = (int)Math.Floor(accumulatedCrewDays);
            int rationCost = Math.Max(0, after - before);
            Water = Math.Max(0, Water - rationCost);
            Food = Math.Max(0, Food - rationCost);
            TotalHours += Math.Max(0d, hours);
        }

        private void ApplyEffect(TutorialEffect effect)
        {
            Hull = Math.Max(0, Math.Min(MaxHull, Hull + effect.Hull));
            Water = Math.Max(0, Water + effect.Water);
            Food = Math.Max(0, Food + effect.Food);
            Morale = ClampMorale((long)Morale + effect.Morale);
            Gold = Math.Max(0, Gold + effect.Gold);
            Wood = Math.Max(0, Wood + effect.Wood);
            Beer = Math.Max(0, Beer + effect.Beer);
            Fame = Math.Max(0, Fame + effect.Fame);

            if (effect.RevealHidden)
            {
                HiddenLocationRevealed = true;
            }

            if (effect.SpeedBoostLegs > 0)
            {
                speedBoost = effect.SpeedBoost;
                speedBoostLegs = effect.SpeedBoostLegs;
            }
        }

        private static int ClampMorale(long value)
        {
            return (int)Math.Max(0L, Math.Min((long)MaxMorale, value));
        }

        private void StartBattle()
        {
            BattleEnemyMaxHull = 36;
            BattleEnemyHull = BattleEnemyMaxHull;
            BattleBlock = 0;
            battleTurn = 0;
            LastBattleMessage = "敌舰正在装填：下一次攻击造成 6 点伤害。";
            Phase = TutorialVoyagePhase.Battle;
            AddLog("与裂桅号演习舰进入教学卡牌战斗。");
        }

        private void AddLog(string message)
        {
            logEntries.Add(message);
            if (logEntries.Count > 24)
            {
                logEntries.RemoveAt(0);
            }
        }
    }
}
