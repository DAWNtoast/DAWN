using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using ShatteredSail.Core.Gameplay;

internal static class ExportBossAudit
{
    private const BindingFlags PublicInstance = BindingFlags.Instance | BindingFlags.Public;
    private const BindingFlags AnyInstance = PublicInstance | BindingFlags.NonPublic;
    private static readonly List<string> Rows = new List<string> { "Boss\tKey\tValue" };

    private static int Main(string[] args)
    {
        var ids = CoreRouteBossCatalog.All.Select(x => x.Id)
            .Concat(CoreLegendaryWarships.All.Select(x => x.Id))
            .Concat(new[] { CoreBattleBalance.TreasureBossId }).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        if (ids.Length != 13) throw new Exception("Expected exactly thirteen non-tutorial bosses.");
        foreach (string id in ids)
        {
            CoreRouteBoss route = CoreRouteBossCatalog.Find(id);
            CoreLegendaryWarship legendary = CoreLegendaryWarships.Find(id);
            CoreEncounter encounter = route?.CreateEncounter() ?? legendary?.CreateEncounter() ?? CoreLegendaryWarships.CreateTreasureEncounter();
            CoreBattleProfile profile = CoreBattleBalance.Resolve(encounter, 2, true);
            Scalars(id, "Profile", profile);
            if (route != null)
            {
                Scalars(id, "RouteBoss", route);
                Row(id, "Components", string.Join(",", route.ComponentIds));
            }
            else if (legendary != null) Scalars(id, "LegendaryBoss", legendary);
            CoreBoardingEnemyDeck deck = CoreBoardingEnemyCatalog.ForEncounter(id);
            Scalars(id, "Boarding", deck);
            Row(id, "Boarding.TurnCount", deck.Turns.Count);
            for (int t = 0; t < deck.Turns.Count; t++)
            {
                string prefix = "Boarding.Turn" + (t + 1);
                Scalars(id, prefix, deck.Turns[t]);
                Row(id, prefix + ".CardCount", deck.Turns[t].Cards.Count);
                for (int c = 0; c < deck.Turns[t].Cards.Count; c++)
                    Scalars(id, prefix + ".Card" + (c + 1), deck.Turns[t].Cards[c]);
            }
            foreach (int distance in new[] { 100, 400 })
            {
                int length = 0;
                for (int step = 1; step == 1 || step <= length; step++)
                {
                    var session = new CoreVoyageSession();
                    session.SetSeed("CHART-BOSS-DIFFICULTY-AUDIT-20261027");
                    session.StartVoyage();
                    Invoke(session, "StartBattle", encounter, true);
                    session.Battle.Stage = NavalBattleStage.Naval;
                    session.Battle.PhaseTurn = 2;
                    session.Battle.EnemyNavalBossActionsCompleted = step - 1;
                    session.Battle.EnemyHull = session.Battle.EnemyMaxHull = 10000;
                    session.Battle.PlayerBlock = session.Battle.EnemyBlock = session.Battle.EnemyLoadedShot = 0;
                    typeof(CoreVoyageSession).GetProperty("Hull").SetValue(session, 10000);
                    Invoke(session.Battle, "SetTacticalPose", true, new VoyagePoint(0, 0), 90f);
                    Invoke(session.Battle, "SetTacticalPose", false, new VoyagePoint(distance, 0), 90f);
                    object[] arguments = { (float)distance, null };
                    bool built = (bool)typeof(CoreVoyageSession).GetMethod("TryBuildNavalBossPlan", AnyInstance).Invoke(session, arguments);
                    if (!built) throw new Exception("Missing naval boss plan for " + id);
                    object plan = arguments[1];
                    var move = (CoreNavalBossMove)plan.GetType().GetField("BossMove", AnyInstance).GetValue(plan);
                    length = move.CycleLength;
                    string prefix = "Naval.Distance" + distance + ".Step" + step;
                    Scalars(id, prefix + ".Plan", plan);
                    Scalars(id, prefix + ".Move", move);
                }
                Row(id, "Naval.Distance" + distance + ".CycleLength", length);
            }
        }
        File.WriteAllLines(args[0], Rows, new UTF8Encoding(false));
        Console.WriteLine("Exported " + ids.Length + " bosses and " + (Rows.Count - 1) + " scalar records.");
        return 0;
    }

    private static object Invoke(object instance, string name, params object[] args) =>
        instance.GetType().GetMethod(name, AnyInstance).Invoke(instance, args);
    private static bool Scalar(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        return type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal);
    }
    private static void Scalars(string id, string prefix, object value)
    {
        foreach (var property in value.GetType().GetProperties(PublicInstance).OrderBy(x => x.Name, StringComparer.Ordinal))
            if (property.GetIndexParameters().Length == 0 && Scalar(property.PropertyType)) Row(id, prefix + "." + property.Name, property.GetValue(value));
        foreach (var field in value.GetType().GetFields(PublicInstance).OrderBy(x => x.Name, StringComparer.Ordinal))
            if (Scalar(field.FieldType)) Row(id, prefix + "." + field.Name, field.GetValue(value));
    }
    private static void Row(string id, string key, object value)
    {
        string rendered = value == null ? "<null>" : value is IFormattable formattable
            ? formattable.ToString(null, CultureInfo.InvariantCulture) : value.ToString();
        Rows.Add(id + "\t" + key + "\t" + rendered.Replace("\t", " ").Replace("\r", " ").Replace("\n", " "));
    }
}
