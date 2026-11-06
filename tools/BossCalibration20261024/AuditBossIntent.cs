using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using ShatteredSail.Core.Gameplay;

// Controlled one-turn consistency checks, not a difficulty or victory simulation.
// The caller compiles a frozen Core snapshot. No production files are modified.
public static class AuditBossIntent
{
    static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    public static int Main(string[] args)
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        Directory.CreateDirectory(args[0]);
        var rows = new List<string> { Csv("boss","phaseTurn","distanceCells","seed","action","bossCycleStep","quotedHullLoss","actualHullLoss","quotedGuard","actualGuard","expectedLoad","actualLoad","expectedEnemyHull","actualEnemyHull","expectedMana","actualMana","expectedChain","actualChain","expectedCompletedActions","actualCompletedActions","readOnly","pass","lastAction") };
        int failed = 0;
        var encounters = CoreRouteBossCatalog.All.Select(x => x.CreateEncounter())
            .Concat(CoreLegendaryWarships.All.Select(x => x.CreateEncounter()))
            .Concat(new[] { CoreLegendaryWarships.CreateTreasureEncounter() }).ToArray();
        foreach (var encounter in encounters)
        foreach (int turn in Enumerable.Range(0, 10))
        foreach (int distance in new[] { 1, 4, 6 })
        foreach (int seed in new[] { 1, 2 })
        {
            var s = new CoreVoyageSession();
            s.SetSeed("BOSS-INTENT-AUDIT-" + seed); s.StartVoyage();
            typeof(CoreVoyageSession).GetMethod("StartBattle", Private).Invoke(s, new object[] { encounter, true });
            var b = s.Battle;
            b.Stage = turn == 0 ? NavalBattleStage.Preparation : NavalBattleStage.Naval;
            b.PhaseTurn = Math.Max(1, turn); b.Turn = Math.Max(1, turn);
            var completed = b.GetType().GetField("EnemyNavalBossActionsCompleted");
            if (completed != null) completed.SetValue(b, Math.Max(0, turn - 1));
            // High fixture HP prevents censoring by death; it does not change armor or attack.
            typeof(CoreVoyageSession).GetProperty("Hull").SetValue(s, 10000);
            b.PlayerBlock = 7; b.EnemyBlock = 0; b.EnemyLoadedShot = 4;
            b.EnemyHull = Math.Max(1, b.EnemyMaxHull - 20);
            var pose = typeof(CoreBattleState).GetMethod("SetTacticalPose", Private);
            pose.Invoke(b, new object[] { true, new VoyagePoint(0, 0), 90f });
            pose.Invoke(b, new object[] { false, new VoyagePoint(distance * 100, 0), 180f });
            string before = State(s), intent = s.GetEnemyIntent();
            var preview = CoreBattleIntentPresentation.Build(s);
            string repeat = s.GetEnemyIntent(); CoreBattleIntentPresentation.Build(s);
            bool readOnly = before == State(s) && intent == repeat;
            object plan = typeof(CoreVoyageSession).GetMethod("BuildEnemyPlan", Private).Invoke(s, null);
            string action = Field(plan, "Action").ToString();
            object move = MaybeField(plan, "BossMove");
            int rawAttack = preview.Where(x => x.Kind == CoreBattleIntentKind.Cannon || x.Kind == CoreBattleIntentKind.Ram).Sum(x => x.Amount ?? 0);
            int piercing = Math.Min(rawAttack, Number(move, "PiercingDamage"));
            int expectedLoss = action == "Ram" ? rawAttack : rawAttack - Math.Min(b.PlayerBlock, rawAttack - piercing);
            int expectedGuard = preview.Where(x => x.Kind == CoreBattleIntentKind.Guard).Sum(x => x.Amount ?? 0);
            int expectedLoad = action == "Cannon" ? 0 : Math.Min(12, b.EnemyLoadedShot + (int)Field(plan, "Load"));
            int bloodCost = Number(move, "BloodCost");
            int expectedEnemyHull = b.EnemyHull - (b.EnemyHull > bloodCost ? bloodCost : 0);
            expectedEnemyHull = Math.Min(b.EnemyMaxHull, expectedEnemyHull + expectedLoss * Number(move, "LifeStealPercent") / 100);
            int expectedMana = Flag(move, "SpendMana") ? 0 : b.NavalFields.Enemy.ManaLimit + Number(move, "ManaGain");
            int expectedChain = Flag(move, "SpendChain") && !Flag(move, "PlaceThunderField") ? 0 : Number(move, "ChainGain");
            int expectedCompleted = completed == null ? 0 : (int)completed.GetValue(b) + (move == null ? 0 : 1);
            int hull = s.Hull;
            if (!s.EndBattleTurn()) throw new Exception("End turn rejected: " + encounter.Id);
            int actualLoss = hull - s.Hull;
            // Ram changes combat stage and resets the shields; fixture preview describes that transition.
            if (action == "Ram") expectedGuard = 0;
            bool navalResources = action == "Ram" || expectedEnemyHull == b.EnemyHull && expectedMana == b.NavalFields.Enemy.Mana && expectedChain == b.NavalFields.Enemy.ThunderChain;
            int actualCompleted = completed == null ? 0 : (int)completed.GetValue(b);
            bool passed = readOnly && expectedLoss == actualLoss && expectedGuard == b.EnemyBlock && expectedLoad == b.EnemyLoadedShot && navalResources && expectedCompleted == actualCompleted;
            if (!passed) failed++;
            rows.Add(Csv(encounter.Id,turn,distance,seed,action,Number(move,"CycleStep"),expectedLoss,actualLoss,expectedGuard,b.EnemyBlock,expectedLoad,b.EnemyLoadedShot,action=="Ram"?(object)"not-audited":expectedEnemyHull,b.EnemyHull,action=="Ram"?(object)"not-audited":expectedMana,b.NavalFields.Enemy.Mana,action=="Ram"?(object)"not-audited":expectedChain,b.NavalFields.Enemy.ThunderChain,expectedCompleted,actualCompleted,readOnly,passed,b.LastAction));
        }
        File.WriteAllLines(Path.Combine(args[0], "intent-consistency.csv"), rows, Encoding.UTF8);
        File.WriteAllText(Path.Combine(args[0], "intent-consistency.txt"), (rows.Count - 1) + " controlled one-turn cases; " + failed + " failed. No fields, cards, burn, marks or enemy status injected. 1/4/6-cell poses include legal ram geometry. Player HP is deliberately raised to prevent death censoring; enemy HP starts 20 below maximum to exercise actual lifesteal. Boss action count, when present, is set independently of PhaseTurn. Checks public attack/guard intents, piercing, blood payment/healing, mana spending, chain spending/gain and completed action count. Ram naval resource aftermath is not audited; ram hull damage is. This is not a difficulty sample.\n", Encoding.UTF8);
        Console.WriteLine((rows.Count - 1) + " consistency cases; " + failed + " failures.");
        return failed == 0 ? 0 : 1;
    }
    static object Field(object item, string name) => item.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(item);
    static object MaybeField(object item, string name) => item?.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(item);
    static int Number(object item, string name) => (int?)MaybeField(item, name) ?? 0;
    static bool Flag(object item, string name) => (bool?)MaybeField(item, name) ?? false;
    static string State(CoreVoyageSession s) => string.Join("|", new object[] { s.Hull,s.Battle.Stage,s.Battle.Turn,s.Battle.PhaseTurn,s.Battle.EnemyHull,s.Battle.PlayerBlock,s.Battle.EnemyBlock,s.Battle.EnemyLoadedShot,s.Battle.PlayerPositionMeters.X,s.Battle.PlayerPositionMeters.Y,s.Battle.EnemyPositionMeters.X,s.Battle.EnemyPositionMeters.Y,s.Battle.Energy,s.Battle.Schools.Mana,s.Battle.Schools.ThunderChain,s.Battle.Schools.CurseMarks,s.Battle.NavalFields.Enemy.Mana,s.Battle.NavalFields.Enemy.ManaLimit,s.Battle.NavalFields.Enemy.ThunderChain,s.Battle.NavalFields.Cells.Count,Number(s.Battle,"EnemyNavalBossActionsCompleted"),Number(s.Battle,"EnemyTidalGuard"),Number(s.Battle,"EnemyTidePower") });
    static string Csv(params object[] cells) => string.Join(",", cells.Select(x => "\"" + Convert.ToString(x, CultureInfo.InvariantCulture).Replace("\"", "\"\"").Replace("\r", " ").Replace("\n", " ") + "\""));
}
