using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using ShatteredSail.Core.Gameplay;

// Exports actual compiled naval profiles and pure plans, never hand-entered card
// or combat results. The PowerShell entry point compiles a frozen Core snapshot.
public static class ExportBossDesign
{
    static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly Encoding Utf8 = new UTF8Encoding(false);
    sealed class Definition
    {
        public string Id, Name, School, Entry;
        public int Tier;
        public CoreEncounter Encounter;
        public CoreBattleProfile Profile;
        public readonly List<Dictionary<string,object>> Cycles = new List<Dictionary<string,object>>();
        public Dictionary<string,object> Data;
    }

    public static int Main(string[] args)
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        if(args.Length < 2) throw new ArgumentException("Expected output directory and original Core source.");
        Directory.CreateDirectory(args[0]);
        List<Definition> bosses = Definitions().OrderBy(x=>x.Tier).ThenBy(x=>x.Id,StringComparer.Ordinal).ToList();
        if(bosses.Count != 13 || bosses.Select(x=>x.Id).Distinct().Count()!=13) throw new Exception("Expected 13 unique authored bosses.");
        foreach(Definition boss in bosses) Read(boss);
        if(bosses.Sum(x=>x.Cycles.Count)!=52) throw new Exception("Expected 52 four-step cycle rows.");
        string[] caveats = {
            "船体为实际 Profile.EnemyHull，已经包含定义中的生命倍率，不应再次乘1.5。",
            "基础炮击、附加穿盾伤害、法力/潮势/连锁/付血增伤分别列出；装填、装甲、减伤、格挡及当前资源决定实际伤害。",
            "四步按已完成的Boss动作计数，不按阶段回合；超射程追击不推进循环，合法冲撞可优先进入接舷。",
            "机动格数是在400米无场地参考位置计算的整格计划，不代表每次都能移动；实际受朝向、障碍、场地和目标位置约束。",
            "法力每个敌方回合开始恢复到上限；提高上限不立即补满，回合内超上限法力不能跨回合保留。",
            "潮势只来自蓄潮格挡实际吸收，无上限，战内保留；反击按实际潮势增伤并消费。",
            "雷栅内连锁不清空或消耗；达到至少50后每次增加均触发1000伤害，先扣格挡，没有Boss默认免疫。",
            "付血费用不足时只使用基础攻击；付血后先检查咒印即时处决。吸血按目标实际扣血并受缺失生命和禁疗限制。",
            "推荐档位是固定内容分期，不随玩家牌级动态缩放。教学、普通敌人和木桩不在本清单；CSV中的接舷参数仅作保留值核对。",
            "本清单记录实际定义，不是实测胜率或5至8回合目标已经达成的证明；实测结论见独立校准报告。"
        };
        var root = D("schemaVersion",1,"sourceCore",args[1],"generatedUtc",DateTime.UtcNow.ToString("o"),
            "bossCount",bosses.Count,"cycleCount",52,"notes",caveats,"bosses",bosses.Select(x=>x.Data).ToArray());
        Write(args[0],"boss-design.json",Json(root)+"\n");
        string[] profileColumns={"id","name","schoolId","schoolName","tier","tierName","recommendedUpgrade","entry","bossKind","archetype","hull","healthMultiplier","cannonBasis","guardBasis","initialGuard","ramChance","effectiveRamRangeMeters","boardingHealth","boardingDamage","boardingGuard"};
        string[] cycleColumns={"bossId","bossName","schoolId","tier","step","name","nextName","action","baseCannon","guard","load","repair","moveMeters","referenceMoveCells","burn","marks","piercingDamage","manaGain","manaLimitGain","spendMana","manaDamagePerPoint","tidalGuard","spendTide","tideDamagePercent","chainGain","spendChain","chainFiveBonus","chainTenBonus","bloodCost","bloodDamageBonus","lifeStealPercent","placeThunderField","thunderFieldSize","thunderFieldTurns","description"};
        Write(args[0],"boss-design.csv",CsvRows(profileColumns,bosses.Select(x=>x.Data)));
        Write(args[0],"boss-cycles.csv",CsvRows(cycleColumns,bosses.SelectMany(x=>x.Cycles)));
        Write(args[0],"海战Boss设计.md",Markdown(bosses,caveats,args[1]));
        Write(args[0],"海战Boss设计.html",Html(bosses,caveats,args[1]));
        Write(args[0],"definition-validation.txt","PASS 13 unique Boss profiles / 52 actual four-step plans.\nPASS profile reference matches each StartBattle profile.\nPASS each plan is pure and has matching school, cycle step and next action.\nPASS no tutorial, normal enemy or training target included.\nRaw values are not armor-adjusted live damage or measured difficulty.\n");
        Console.WriteLine("Exported 13 actual naval Boss definitions and 52 cycle rows.");
        return 0;
    }

    static IEnumerable<Definition> Definitions()
    {
        foreach(var b in CoreRouteBossCatalog.All)
        {
            int tier=b.RouteId=="route_shattered_sail_reef"?1:b.RouteId=="route_golden_tide_trade"?3:4;
            yield return new Definition{Id=b.Id,Name=b.Name,School=b.SchoolId,Tier=tier,Entry=b.RouteId,Profile=b.Profile,Encounter=b.CreateEncounter()};
        }
        foreach(var b in CoreLegendaryWarships.All)
        {
            int tier=(b.Id=="legendary_pirate_iron_oath"||b.Id=="legendary_pirate_red_widow")?2:
                (b.Id=="legendary_pirate_thunder_crown"||b.Id=="legendary_pirate_ember_ark")?3:5;
            yield return new Definition{Id=b.Id,Name=b.Name,School=b.SchoolId,Tier=tier,
                Entry=tier==2?"藏宝图 weathered（亦见第三海图）":tier==3?"藏宝图 gilded（亦见第三海图）":"藏宝图 moonlit（亦见第三海图）",
                Profile=b.Profile,Encounter=b.CreateEncounter()};
        }
        yield return new Definition{Id=CoreBattleBalance.TreasureBossId,Name="沉月王庭·守藏舰",School="ordinary",Tier=4,
            Entry="第三海图月痕宝库",Profile=CoreBattleBalance.ThirdTreasureBoss,Encounter=CoreLegendaryWarships.CreateTreasureEncounter()};
    }

    static void Read(Definition boss)
    {
        var p=boss.Profile;
        boss.Data=D("id",boss.Id,"name",boss.Name,"schoolId",boss.School,"schoolName",School(boss.School),"tier",boss.Tier,
            "tierName",Tier(boss.Tier),"recommendedUpgrade",boss.Tier==1?0:boss.Tier<=3?1:boss.Tier==4?2:3,"entry",boss.Entry,
            "bossKind",p.BossKind.ToString(),"archetype",p.Archetype.ToString(),"hull",p.EnemyHull,"healthMultiplier",p.HealthMultiplier,
            "cannonBasis",p.CannonDamage,"guardBasis",p.Guard,"initialGuard",p.InitialGuard,"ramChance",p.RamChance,"effectiveRamRangeMeters",100,
            "boardingHealth",p.BoardingHealth,"boardingDamage",p.MeleeDamage,"boardingGuard",p.BoardingGuard,"cycles",boss.Cycles);
        var s=new CoreVoyageSession();s.SetSeed("BOSS-DESIGN-EXPORT");s.StartVoyage();
        typeof(CoreVoyageSession).GetMethod("StartBattle",Private).Invoke(s,new object[]{boss.Encounter,true});
        if(!ReferenceEquals(s.Battle.Profile,p))throw new Exception("Profile mismatch: "+boss.Id);
        s.Battle.Stage=NavalBattleStage.Naval;
        var pose=typeof(CoreBattleState).GetMethod("SetTacticalPose",Private);
        pose.Invoke(s.Battle,new object[]{true,new VoyagePoint(0,0),90f});
        pose.Invoke(s.Battle,new object[]{false,new VoyagePoint(400,0),90f});
        var build=typeof(CoreVoyageSession).GetMethod("BuildEnemyPlan",Private);
        var quantize=typeof(CoreVoyageSession).GetMethod("QuantizeNavalEnemyTravel",BindingFlags.NonPublic|BindingFlags.Static);
        for(int step=0;step<4;step++)
        {
            s.Battle.EnemyNavalBossActionsCompleted=step;
            string before=State(s);
            object plan=build.Invoke(s,null);
            var move=(CoreNavalBossMove)Field(plan,"BossMove");
            if(move==null||move.CycleStep!=step+1||move.SchoolId!=boss.School||before!=State(s))
                throw new Exception("Invalid or mutating Boss plan: "+boss.Id+" step "+(step+1));
            int attack=(int)Field(plan,"Attack"),guard=(int)Field(plan,"Guard"),load=(int)Field(plan,"Load"),repair=(int)Field(plan,"Repair");
            float movement=(float)Field(plan,"Move");
            int cells=(int)((float)quantize.Invoke(null,new object[]{movement})/CoreVoyageSession.NavalGridCellMeters);
            var row=D("bossId",boss.Id,"bossName",boss.Name,"schoolId",boss.School,"tier",boss.Tier,
                "step",step+1,"name",move.Name,"nextName",move.NextName,"action",Field(plan,"Action").ToString(),
                "baseCannon",attack,"guard",guard,"load",load,"repair",repair,"moveMeters",movement,"referenceMoveCells",cells,
                "burn",move.Burn,"marks",move.Marks,"piercingDamage",move.PiercingDamage,
                "manaGain",move.ManaGain,"manaLimitGain",move.ManaLimitGain,"spendMana",move.SpendMana,"manaDamagePerPoint",move.ManaDamagePerPoint,
                "tidalGuard",move.TidalGuard,"spendTide",move.SpendTide,"tideDamagePercent",move.TideDamagePercent,
                "chainGain",move.ChainGain,"spendChain",move.SpendChain,"chainFiveBonus",move.ChainFiveBonus,"chainTenBonus",move.ChainTenBonus,
                "bloodCost",move.BloodCost,"bloodDamageBonus",move.BloodDamageBonus,"lifeStealPercent",move.LifeStealPercent,
                "placeThunderField",move.PlaceThunderField,"thunderFieldSize",move.PlaceThunderField?CoreVoyageSession.GetNavalFieldSize(CoreNavalFieldKind.Thunder):0,
                "thunderFieldTurns",move.ThunderFieldTurns,"description",Describe(attack,guard,load,repair,move));
            boss.Cycles.Add(row);
        }
        for(int i=0;i<4;i++)if(!Equals(boss.Cycles[i]["nextName"],boss.Cycles[(i+1)%4]["name"]))throw new Exception("Broken next action: "+boss.Id);
    }

    static string Describe(int attack,int guard,int load,int repair,CoreNavalBossMove m)
    {
        var p=new List<string>();
        if(attack>0)p.Add("基础炮击 "+attack);
        if(guard>0)p.Add((m.TidalGuard>0?"蓄潮格挡 ":"格挡 ")+guard);
        if(load>0)p.Add("装填 +"+load);
        if(repair>0)p.Add("恢复船体 "+repair);
        if(m.Burn>0)p.Add("燃烧 +"+m.Burn);
        if(m.Marks>0)p.Add("咒印 +"+m.Marks+"，即时判定");
        if(m.PiercingDamage>0)p.Add("另加咒术伤害 "+m.PiercingDamage+"（至多该数值无视格挡）");
        if(m.ManaGain>0)p.Add("获得法力 "+m.ManaGain);
        if(m.ManaLimitGain>0)p.Add("法力上限 +"+m.ManaLimitGain+"，不立即补满");
        if(m.ManaDamagePerPoint>0)p.Add("每点实际法力额外伤害 "+m.ManaDamagePerPoint);
        if(m.SpendMana)p.Add("消耗全部法力");
        if(m.TideDamagePercent>0)p.Add("额外伤害=实际潮势×"+m.TideDamagePercent+"%");
        if(m.SpendTide)p.Add("消耗全部潮势");
        if(m.PlaceThunderField)p.Add("铺设 "+CoreVoyageSession.GetNavalFieldSize(CoreNavalFieldKind.Thunder)+"×"+CoreVoyageSession.GetNavalFieldSize(CoreNavalFieldKind.Thunder)+" 雷栅，持续 "+m.ThunderFieldTurns+" 回合");
        if(m.ChainFiveBonus>0)p.Add("攻击前连锁≥5：额外 "+m.ChainFiveBonus+" 伤害");
        if(m.ChainTenBonus>0)p.Add("攻击前连锁≥10：再额外 "+m.ChainTenBonus+" 伤害");
        if(m.SpendChain)p.Add("终结后消耗全部连锁，雷栅内免耗");
        if(m.ChainGain>0)p.Add("攻击后连锁 +"+m.ChainGain);
        if(m.BloodCost>0)p.Add("付得起时消耗船体 "+m.BloodCost+"，额外伤害 "+m.BloodDamageBonus+"；不足仅基础效果");
        if(m.LifeStealPercent>0)p.Add("吸血=目标实际扣血×"+m.LifeStealPercent+"%");
        return p.Count==0?"本步无攻击":string.Join("；",p);
    }

    static string Markdown(List<Definition> bosses,string[] notes,string source)
    {
        var s=new StringBuilder("# 海战 Boss 设计\n\n指定源码定义导出：13 个 Boss、52 个动作。基础炮击与动态增伤分别列出；实测战斗强度见校准报告。\n\n");
        s.Append("源码来源：`").Append(source).Append("`。\n\n");
        s.Append("## 阅读规则\n\n");foreach(string n in notes)s.Append("- ").Append(n).Append('\n');
        s.Append("\n## 强度分期\n\n| 档位 | Boss | 体系 | 实战船体 | 初始格挡 | 炮击基数 D | 格挡基数 G |\n| --- | --- | --- | ---: | ---: | ---: | ---: |\n");
        foreach(var b in bosses)s.Append("| ").Append(Tier(b.Tier)).Append(" | ").Append(Md(b.Name)).Append(" | ").Append(School(b.School)).Append(" | ").Append(b.Profile.EnemyHull).Append(" | ").Append(b.Profile.InitialGuard).Append(" | ").Append(b.Profile.CannonDamage).Append(" | ").Append(b.Profile.Guard).Append(" |\n");
        foreach(var b in bosses)
        {
            s.Append("\n## ").Append(b.Name).Append("\n\n").Append(Tier(b.Tier)).Append(" · ").Append(School(b.School)).Append(" · 推荐牌级 +").Append(b.Data["recommendedUpgrade"]).Append("（各牌受自身上限限制）\n\n");
            s.Append("身份：`").Append(b.Id).Append("`；入口：").Append(b.Entry).Append("。\n\n");
            s.Append("| 步骤 | 动作 | 基础效果与资源条件 | 参考机动 |\n| ---: | --- | --- | ---: |\n");
            foreach(var c in b.Cycles)s.Append("| ").Append(c["step"]).Append(" | ").Append(Md(c["name"].ToString())).Append(" | ").Append(Md(c["description"].ToString())).Append(" | ").Append(c["referenceMoveCells"]).Append(" 格 |\n");
            s.Append("\n第 4 步完成后回到第 1 步；合法冲撞概率 ").Append((b.Profile.RamChance*100).ToString("0.#")).Append("%，仅相邻船头几何合法时判定，预告可见。\n");
        }
        s.Append("\n[结构化 JSON](boss-design.json) · [Boss 参数 CSV](boss-design.csv) · [52 个动作 CSV](boss-cycles.csv)\n");return s.ToString();
    }

    static string Html(List<Definition> bosses,string[] notes,string source)
    {
        var s=new StringBuilder("<!doctype html><html lang=\"zh-CN\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\"><title>海战 Boss 设计</title><style>body{font:16px/1.65 system-ui,\"Microsoft YaHei\",sans-serif;margin:0;color:#e5edf5;background:#101923}main{max-width:1240px;margin:auto;padding:32px 24px}h1{font-size:32px}h2{margin-top:40px}a{color:#8ed6e9}nav{display:flex;flex-wrap:wrap;gap:8px 18px;margin:20px 0}section{scroll-margin-top:20px;padding-top:4px}table{border-collapse:collapse;width:100%;margin:20px 0}th,td{text-align:left;vertical-align:top;padding:12px 14px;border-bottom:1px solid #354450}th{background:#1c2d3b;color:#a4dcdf}.wrap{overflow-x:auto}.note{color:#b9c8d5}.tag{color:#d9b87c}details{background:#1a2733;padding:16px 20px;border-radius:10px}code{font-size:13px;overflow-wrap:anywhere}.cycle td:first-child{width:48px}.cycle td:nth-child(2){width:130px}.cycle td:last-child{width:76px}@media(max-width:700px){main{padding:20px 14px}th,td{padding:9px;font-size:14px}.cycle{min-width:650px}}</style></head><body><main><h1>海战 Boss 设计</h1><p class=\"note\">从实际 Core 定义导出 · 13 个 Boss · 52 个动作。这里展示确定的设计值和资源条件，实测胜负及战斗回合另见校准报告。</p><nav>");
        foreach(var b in bosses)s.Append("<a href=\"#").Append(E(b.Id)).Append("\">").Append(E(b.Name)).Append("</a>");
        s.Append("</nav><p class=\"note\">源码来源：<code>").Append(E(source)).Append("</code></p><p><a href=\"海战Boss设计.md\">Markdown</a> · <a href=\"boss-design.json\">JSON</a> · <a href=\"boss-design.csv\">参数 CSV</a> · <a href=\"boss-cycles.csv\">动作 CSV</a></p><details open><summary>阅读规则</summary><ul>");
        foreach(string n in notes)s.Append("<li>").Append(E(n)).Append("</li>");s.Append("</ul></details><h2>强度分期</h2><div class=\"wrap\"><table><thead><tr><th>档位</th><th>Boss</th><th>体系</th><th>实战船体</th><th>初始格挡</th><th>炮击基数 D</th><th>格挡基数 G</th></tr></thead><tbody>");
        foreach(var b in bosses)s.Append("<tr><td>").Append(E(Tier(b.Tier))).Append("</td><td><a href=\"#").Append(E(b.Id)).Append("\">").Append(E(b.Name)).Append("</a></td><td>").Append(E(School(b.School))).Append("</td><td>").Append(b.Profile.EnemyHull).Append("</td><td>").Append(b.Profile.InitialGuard).Append("</td><td>").Append(b.Profile.CannonDamage).Append("</td><td>").Append(b.Profile.Guard).Append("</td></tr>");
        s.Append("</tbody></table></div>");
        foreach(var b in bosses)
        {
            s.Append("<section id=\"").Append(E(b.Id)).Append("\"><h2>").Append(E(b.Name)).Append("</h2><p class=\"tag\">").Append(E(Tier(b.Tier))).Append(" · ").Append(E(School(b.School))).Append(" · 推荐牌级 +").Append(b.Data["recommendedUpgrade"]).Append("（各牌受自身上限限制）</p><p class=\"note\"><code>").Append(E(b.Id)).Append("</code><br>入口：").Append(E(b.Entry)).Append("</p><div class=\"wrap\"><table class=\"cycle\"><thead><tr><th>步骤</th><th>动作</th><th>基础效果与资源条件</th><th>参考机动</th></tr></thead><tbody>");
            foreach(var c in b.Cycles)s.Append("<tr><td>").Append(c["step"]).Append("</td><td>").Append(E(c["name"].ToString())).Append("</td><td>").Append(E(c["description"].ToString())).Append("</td><td>").Append(c["referenceMoveCells"]).Append(" 格</td></tr>");
            s.Append("</tbody></table></div><p class=\"note\">第 4 步后回到第 1 步。合法冲撞概率 ").Append((b.Profile.RamChance*100).ToString("0.#")).Append("%，仅相邻船头几何合法时判定；预告可见，追击不会跳过准备动作。</p></section>");
        }
        return s.Append("</main></body></html>\n").ToString();
    }

    static string School(string id){switch(id){case "fire":return "火焰";case "arcane":return "秘术";case "curse":return "咒术";case "tide":return "潮汐";case "thunder":return "雷霆";case "blood":return "血契";default:return "中立重甲";}}
    static string Tier(int tier)=>new[]{"","一档·前期首航","二档·前期成型","三档·中期","四档·后期","五档·终局"}[tier];
    static object Field(object value,string name)=>value.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public).GetValue(value);
    static string State(CoreVoyageSession s)=>string.Join("|",new object[]{s.Hull,s.Battle.EnemyHull,s.Battle.PhaseTurn,s.Battle.EnemyNavalBossActionsCompleted,s.Battle.EnemyTidePower,s.Battle.EnemyTidalGuard,s.Battle.EnemyBlock,s.Battle.EnemyLoadedShot,s.Battle.NavalFields.Enemy.Mana,s.Battle.NavalFields.Enemy.ManaLimit,s.Battle.NavalFields.Enemy.ThunderChain,s.Battle.NavalFields.Cells.Count,s.Battle.EnemyPositionMeters.X,s.Battle.EnemyPositionMeters.Y});
    static Dictionary<string,object> D(params object[] pairs){var d=new Dictionary<string,object>();for(int i=0;i<pairs.Length;i+=2)d.Add((string)pairs[i],pairs[i+1]);return d;}
    static void Write(string dir,string name,string text)=>File.WriteAllText(Path.Combine(dir,name),text,Utf8);
    static string CsvRows(string[] columns,IEnumerable<Dictionary<string,object>> rows)=>Csv(columns.Cast<object>().ToArray())+"\n"+string.Join("\n",rows.Select(r=>Csv(columns.Select(c=>r[c]).ToArray())))+"\n";
    static string Csv(params object[] values)=>string.Join(",",values.Select(v=>"\""+Convert.ToString(v,CultureInfo.InvariantCulture).Replace("\"","\"\"").Replace("\r"," ").Replace("\n"," ")+"\""));
    static string E(string text)=>text.Replace("&","&amp;").Replace("<","&lt;").Replace(">","&gt;").Replace("\"","&quot;");
    static string Md(string text)=>text.Replace("|","\\|").Replace("\r"," ").Replace("\n"," ");
    static string Json(object value)
    {
        if(value==null)return "null";
        if(value is string)return "\""+((string)value).Replace("\\","\\\\").Replace("\"","\\\"").Replace("\r","\\r").Replace("\n","\\n").Replace("\t","\\t")+"\"";
        if(value is bool)return (bool)value?"true":"false";
        if(value is IDictionary){var d=(IDictionary)value;var pairs=new List<string>();foreach(DictionaryEntry e in d)pairs.Add(Json(e.Key.ToString())+":"+Json(e.Value));return "{"+string.Join(",",pairs)+"}";}
        if(value is IEnumerable)return "["+string.Join(",",((IEnumerable)value).Cast<object>().Select(Json))+ "]";
        return Convert.ToString(value,CultureInfo.InvariantCulture);
    }
}
