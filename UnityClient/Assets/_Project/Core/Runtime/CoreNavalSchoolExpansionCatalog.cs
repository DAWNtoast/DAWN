using System;
using System.Collections.Generic;

namespace ShatteredSail.Core.Gameplay
{
    public enum CoreNavalExpansionMechanic
    {
        FireField, MeltingCannon, OilMark, FireWard, MoveFire, HarborExplosion,
        ArcaneField, RuneClamp, StarBeam, CurseField, LoadSeal, CursePeel,
        TideField, TideCannon, TideAnchor, RecedingWave, TideStrip, GreatWave,
        ThunderField, ArcCannon, Precharge, Pulse, Grounding, ThunderCollapse,
        BloodField, HealingBan, DesperateCannon, ManaBurst,
        FireMixedCannon, ArcaneCapacityGuard, ArcaneChargeGuard, ArcaneFinale,
        ThunderRelay, ThunderCircuit, ThunderPayoff
    }
    public sealed class CoreNavalExpansionCardSpec
    {
        public string Key, SchoolId, ResourceLabel;
        public CoreNavalExpansionMechanic Mechanic;
        public int ManaCost, HullCost, WoodCost, ResourceMinimum, ResourceMaximum;
        public bool OncePerTurn, IsField, NeedsEnemyTarget, ConsumesAllResource;
    }

    /// <summary>New stable IDs; existing definitions and their saved library positions are retained.</summary>
    public static class CoreNavalSchoolExpansionCatalog
    {
        private static readonly Dictionary<string, CoreNavalExpansionCardSpec> specs = new Dictionary<string, CoreNavalExpansionCardSpec>(StringComparer.Ordinal);
        public static IReadOnlyList<CoreBattleCard> All { get; } = Build();
        public static CoreNavalExpansionCardSpec FindSpec(string id) => id != null && specs.TryGetValue(id, out CoreNavalExpansionCardSpec spec) ? spec : null;
        private static IReadOnlyList<CoreBattleCard> Build()
        {
            var c = new List<CoreBattleCard>();
            Add(c,"fire",1,"F01","火油封港",2,0,0,1,CoreNavalExpansionMechanic.FireField,"铺设持续时间为 5 回合的 3×3 火场。",field:true);
            Add(c,"fire",2,"F02","熔穿炮",1,8,0,1,CoreNavalExpansionMechanic.MeltingCannon,"造成 {damage} 炮击伤害；目标在火场上时先移除最多 6 格挡。",cannon:true,enemy:true);
            Add(c,"fire",3,"F03","浸油标记",1,0,0,1,CoreNavalExpansionMechanic.OilMark,"施加浸油。",enemy:true);
            Add(c,"fire",4,"F04","隔焰护舷",1,0,4,0,CoreNavalExpansionMechanic.FireWard,"获得 {block} 格挡；直到下个己方回合开始时，免疫火场负面效果影响。");
            Add(c,"fire",5,"F05","拨风移焰",0,0,0,0,CoreNavalExpansionMechanic.MoveFire,"将所有己方火场向上下左右各延伸 1 格。");
            Add(c,"fire",6,"F06","焚港引爆",3,14,0,1,CoreNavalExpansionMechanic.HarborExplosion,"选择含己方火场的 2×2 区域，消耗其中己方火场；区域内双方受到 {damage}＋4×移除格数直接伤害。");
            Add(c,"arcane",1,"A04","星界折射阵",2,0,0,0,CoreNavalExpansionMechanic.ArcaneField,"铺设持续 3 回合的 2×2 折射阵。",field:true);
            Add(c,"arcane",2,"A05","符文钳制",0,0,0,0,CoreNavalExpansionMechanic.RuneClamp,"目标下次获得的格挡减少 6 点。",mana:1,enemy:true);
            Add(c,"arcane",3,"A06","破阵星束",3,26,0,0,CoreNavalExpansionMechanic.StarBeam,"造成 10 点无视格挡伤害；造成 {damage} 点伤害。",mana:2,enemy:true);
            Add(c,"curse",1,"C04","丧钟禁域",2,0,0,0,CoreNavalExpansionMechanic.CurseField,"铺设持续时间为 5 回合的 2×2 禁域。",field:true);
            Add(c,"curse",2,"C05","蚀印封装",0,0,0,0,CoreNavalExpansionMechanic.LoadSeal,"敌方咒印层数 ≥ 1 才可以使用：敌方下一次炮击伤害降低 6 点。",min:1,resource:"咒印",enemy:true);
            Add(c,"curse",3,"C06","终咒剥离",3,14,0,0,CoreNavalExpansionMechanic.CursePeel,"造成 {damage}＋6X 点直接伤害；清除咒印效果。",resource:"咒印",enemy:true,all:true);
            Add(c,"tide",1,"T01","回流漩涡",2,0,0,0,CoreNavalExpansionMechanic.TideField,"铺设持续时间为 5 回合的 4×4 定向水流。",field:true);
            Add(c,"tide",2,"T02","破潮横击",1,6,0,1,CoreNavalExpansionMechanic.TideCannon,"造成 {damage}＋{tideStrikeDamage}X 点船体伤害。",cannon:true,resource:"潮势",enemy:true,all:true);
            Add(c,"tide",3,"T03","潮盾定锚",1,0,8,0,CoreNavalExpansionMechanic.TideAnchor,"消耗 1 木板获得 {block} 蓄潮格挡；直到下个己方回合开始时，免疫定向水流负面效果影响；无木板仅获 2 普通格挡且无水流免疫。",wood:1);
            Add(c,"tide",4,"T04","卸帆逆浪",1,0,0,0,CoreNavalExpansionMechanic.RecedingWave,"使敌方向后移动 1 格，敌方下次移动时至多移动 1 格。",min:3,max:3,resource:"潮势",enemy:true);
            Add(c,"tide",5,"T05","分潮解甲",0,0,0,0,CoreNavalExpansionMechanic.TideStrip,"移除敌方 X 点格挡。",resource:"潮势",enemy:true,all:true);
            Add(c,"tide",6,"T06","巨浪横扫",3,12,0,0,CoreNavalExpansionMechanic.GreatWave,"对定向水流中的角色造成 {damage}+X 点伤害。",resource:"潮势",all:true);
            Add(c,"thunder",1,"L01","雷栅封航",2,0,0,0,CoreNavalExpansionMechanic.ThunderField,"铺设持续时间为 5 回合的 7×7 雷栅。",field:true);
            Add(c,"thunder",2,"L02","跨弧炮",1,6,0,1,CoreNavalExpansionMechanic.ArcCannon,"造成 {damage} 点炮击伤害；连锁 2：额外造成 12 点伤害。",cannon:true,enemy:true);
            Add(c,"thunder",3,"L03","预充线圈",0,0,0,0,CoreNavalExpansionMechanic.Precharge,"本回合下张主动雷霆攻击结算后额外 +2 连锁。");
            Add(c,"thunder",4,"L04","脉冲失准",1,0,0,0,CoreNavalExpansionMechanic.Pulse,"直到下个敌方回合结束，目标造成的伤害减少 4 点；连锁 2：改为减少 8 点。",enemy:true);
            Add(c,"thunder",5,"L05","接地回流",1,0,0,0,CoreNavalExpansionMechanic.Grounding,"抽 {draw} 张牌；本回合第一次使用雷霆牌对敌方造成伤害时，恢复 1 点能量。",draw:2);
            Add(c,"thunder",6,"L06","雷暴收束",3,18,0,0,CoreNavalExpansionMechanic.ThunderCollapse,"范围 1-4 格：选择含己方雷栅的 2×2 区域，消耗其中己方雷栅；区域内双方受到 {damage}+{thunderCollapseChain}X+4×移除雷栅格数 点伤害；结算后清空连锁，出牌时处于雷栅则不清空。",resource:"连锁",all:true);
            Add(c,"blood",1,"B04","血誓锚阵",2,0,0,0,CoreNavalExpansionMechanic.BloodField,"铺设持续时间为 5 回合的 2×2 血池。",hull:4,field:true);
            Add(c,"blood",2,"B05","止愈血契",0,0,0,0,CoreNavalExpansionMechanic.HealingBan,"施加 2 回合禁疗。",hull:3,enemy:true);
            Add(c,"blood",3,"B06","背水重炮",3,35,0,1,CoreNavalExpansionMechanic.DesperateCannon,"造成 {damage} 点炮击伤害；绝境：消耗 5 点船体额外造成 {desperateBonus} 点伤害。",cannon:true,hull:5,enemy:true);
            // Append new definitions after every existing slot to preserve saved library indexes.
            Add(c,"arcane",4,"A07","以太聚束",2,10,0,0,CoreNavalExpansionMechanic.ManaBurst,"造成 {damage}+{manaBurstDamage}X 点船体伤害。",resource:"法力",enemy:true,all:true);
            Add(c,"fire",7,"F07","燃爆齐射",2,18,0,1,CoreNavalExpansionMechanic.FireMixedCannon,"造成 {damage} 点炮击伤害；施加 {synergyBurn} 层燃烧。",cannon:true,enemy:true);
            Add(c,"fire",8,"F08","曳火连射",1,8,0,1,CoreNavalExpansionMechanic.FireMixedCannon,"造成 {damage} 点炮击伤害；施加 {synergyBurn} 层燃烧；抽 {draw} 张牌。",cannon:true,enemy:true,draw:1);
            Add(c,"arcane",5,"A08","星核扩容",1,0,4,0,CoreNavalExpansionMechanic.ArcaneCapacityGuard,"获得 {block} 点格挡；本场法力上限 +{synergyCapacity}，不立即恢复法力。");
            Add(c,"arcane",6,"A09","汇流护壁",1,0,6,0,CoreNavalExpansionMechanic.ArcaneChargeGuard,"获得 {block} 点格挡；获得 {synergyMana} 点法力。");
            Add(c,"arcane",7,"A10","星穹坍缩",3,100,0,0,CoreNavalExpansionMechanic.ArcaneFinale,"造成 {damage} 点船体伤害。",mana:8,enemy:true);
            Add(c,"thunder",7,"L07","电弧引信",0,4,0,0,CoreNavalExpansionMechanic.ThunderRelay,"造成 {damage} 点船体伤害；抽 {draw} 张牌。",enemy:true,draw:1);
            Add(c,"thunder",8,"L08","雷脉续流",0,0,0,0,CoreNavalExpansionMechanic.ThunderCircuit,"连锁至少为 3 才可使用：恢复 {thunderCircuitEnergy} 点能量；抽 {draw} 张牌。",once:true,draw:1);
            Add(c,"thunder",9,"L09","极限放电",2,12,0,0,CoreNavalExpansionMechanic.ThunderPayoff,"造成 {damage} 点船体伤害；连锁 5：额外造成 {thunderPayoffFive} 点伤害；连锁 10：再额外造成 {thunderPayoffTen} 点伤害；结算后清空连锁，出牌时处于雷栅则不清空。",resource:"连锁",enemy:true,all:true);
            return c.AsReadOnly();
        }
        private static void Add(List<CoreBattleCard> cards,string school,int number,string key,string name,int energy,int damage,int block,int powder,
            CoreNavalExpansionMechanic mechanic,string text,bool cannon=false,int mana=0,int hull=0,int wood=0,int min=0,int max=0,string resource="",bool once=false,bool field=false,bool enemy=false,int draw=0,bool all=false)
        {
            string id="card_school_"+school+"_n"+number.ToString("00");
            specs.Add(id,new CoreNavalExpansionCardSpec { Key=key,SchoolId=school,Mechanic=mechanic,ManaCost=mana,HullCost=hull,WoodCost=wood,
                ResourceMinimum=min,ResourceMaximum=max,ResourceLabel=resource,OncePerTurn=once,IsField=field,NeedsEnemyTarget=enemy,ConsumesAllResource=all });
            if(field) text="范围 1-4 格："+text;
            else if(enemy||mechanic==CoreNavalExpansionMechanic.HarborExplosion) text="4 格曼哈顿范围，每一格须合法；"+text;
            if(powder>0) text+="消耗 "+powder+" 火药"+(cannon?"、1 炮弹":"")+"。";
            if(mana>0) text+="消耗 "+mana+" 法力。";
            if(hull>0) text+="支付 "+hull+" 船体，支付后至少留 1 点。";
            if(max>0) text+="消耗 "+min+(min==max?"":"～"+max)+" "+resource+"。";
            if(once) text+="同名每回合一次。";
            string type=cannon?"炮击":field?"场地":damage>0?"法术":block>0?"防御":"策略";
            cards.Add(new CoreBattleCard(id,name,type,energy,damage,block,0,draw,powder,text,NavalBattleStages.Naval,
                cannon?NavalBattleCardAction.Cannon:NavalBattleCardAction.Standard,artId:"card_school_"+school+(damage>0?"_b03":block>0?"_b05":"_b06"),cannonballCost:cannon?1:0,schoolId:school));
        }
    }
}
