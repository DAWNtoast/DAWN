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
        BloodField, HealingBan, DesperateCannon
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
            Add(c,"fire",1,"F01","火油封港",2,0,0,1,CoreNavalExpansionMechanic.FireField,"铺设 2×2 火场；双方每船每格每大回合一次 2 场地伤害，结束回合停留补伤；放置不触发。",field:true);
            Add(c,"fire",2,"F02","熔穿炮",1,8,0,1,CoreNavalExpansionMechanic.MeltingCannon,"造成 {damage} 炮击伤害；目标在火场上时先移除最多 6 格挡。",cannon:true,enemy:true);
            Add(c,"fire",3,"F03","浸油标记",1,0,0,1,CoreNavalExpansionMechanic.OilMark,"施加浸油至下个己方回合结束；下张主动火焰攻击实际扣船体后追加 4 直接伤害和 3×2 回合燃烧，随后消耗浸油；不叠加。",enemy:true);
            Add(c,"fire",4,"F04","隔焰护舷",1,0,4,0,CoreNavalExpansionMechanic.FireWard,"获得 {block} 格挡；直到下个己方回合开始前，免疫 2 次火场伤害。");
            Add(c,"fire",5,"F05","拨风移焰",0,0,0,0,CoreNavalExpansionMechanic.MoveFire,"将 1-4 格内的己方 1 格火场移动到相邻的格子。");
            Add(c,"fire",6,"F06","焚港引爆",3,14,0,1,CoreNavalExpansionMechanic.HarborExplosion,"选择含己方火场的 2×2 区域，消耗其中己方火场；区域内双方受到 {damage}＋4×移除格数直接伤害。");
            Add(c,"arcane",1,"A04","星界折射阵",2,0,0,0,CoreNavalExpansionMechanic.ArcaneField,"铺设 2×2 折射阵；己舰自己回合进入或回合开始位于阵内，获得 1 临时法力，下张主动秘术攻击首段最多 4 点穿盾；每大回合一次。",field:true);
            Add(c,"arcane",2,"A05","符文钳制",0,0,0,0,CoreNavalExpansionMechanic.RuneClamp,"目标下次获得的格挡减少 6 点。",mana:1,enemy:true);
            Add(c,"arcane",3,"A06","破阵星束",3,14,0,0,CoreNavalExpansionMechanic.StarBeam,"造成 {damage} 点伤害；造成 10 点无视格挡伤害。",mana:2,enemy:true);
            Add(c,"curse",1,"C04","丧钟禁域",2,0,0,0,CoreNavalExpansionMechanic.CurseField,"铺设 2×2 禁域；敌舰域内结束回合时，本次咒印额外造成 2 点伤害；每大回合一次。",field:true);
            Add(c,"curse",2,"C05","蚀印封装",0,0,0,0,CoreNavalExpansionMechanic.LoadSeal,"装填减少 6 点。",min:1,max:1,resource:"咒印",enemy:true);
            Add(c,"curse",3,"C06","终咒剥离",3,14,0,0,CoreNavalExpansionMechanic.CursePeel,"造成 {damage}＋6X 点直接伤害；清除咒印效果。",resource:"咒印",enemy:true,all:true);
            Add(c,"tide",1,"T01","回流漩涡",2,0,0,0,CoreNavalExpansionMechanic.TideField,"铺设 2×2 定向水流；双方在水流中结束位移后沿所选方向推动 1 格，每船每大回合一次；单结算链最多一次场地推动。",field:true);
            Add(c,"tide",2,"T02","破潮横击",1,6,0,1,CoreNavalExpansionMechanic.TideCannon,"造成 {damage}＋2X 点炮击伤害。",cannon:true,resource:"潮势",enemy:true,all:true);
            Add(c,"tide",3,"T03","潮盾定锚",1,0,6,0,CoreNavalExpansionMechanic.TideAnchor,"消耗 1 木板获得 {block} 蓄潮格挡；至下个己方回合开始可拒绝一次强制位移；无木板仅获 2 普通格挡且无定锚。",wood:1);
            Add(c,"tide",4,"T04","卸帆逆浪",1,0,0,0,CoreNavalExpansionMechanic.RecedingWave,"沿所选远离己舰方向推动目标 1 格；其下次主动移动上限减少 1 格。",min:3,max:3,resource:"潮势",enemy:true);
            Add(c,"tide",5,"T05","分潮解甲",0,0,0,0,CoreNavalExpansionMechanic.TideStrip,"移除敌方 X 点格挡。",resource:"潮势",enemy:true,all:true);
            Add(c,"tide",6,"T06","巨浪横扫",3,12,0,0,CoreNavalExpansionMechanic.GreatWave,"选择波向和与其垂直的 1×3 区域；双方先受 {damage}＋2X 点伤害，再将存活船只沿波向推动 1 格。",resource:"潮势",all:true);
            Add(c,"thunder",1,"L01","雷栅封航",2,0,0,0,CoreNavalExpansionMechanic.ThunderField,"铺设横或竖 1×3 雷栅；双方首次进入受 3 场地伤害并停止本次主动航行；每船每大回合一次。",field:true);
            Add(c,"thunder",2,"L02","跨弧炮",1,6,0,1,CoreNavalExpansionMechanic.ArcCannon,"先造成 {damage} 炮击伤害；目标施放时在雷栅上，再造成 6 雷霆直伤；整牌一次攻击奖励、1 连锁，额外段不吃炮击或预充加成。",cannon:true,enemy:true);
            Add(c,"thunder",3,"L03","预充线圈",0,0,0,0,CoreNavalExpansionMechanic.Precharge,"本回合下张主动雷霆攻击结算后额外 +2 连锁。",min:1,max:1,resource:"连锁");
            Add(c,"thunder",4,"L04","脉冲失准",1,0,0,0,CoreNavalExpansionMechanic.Pulse,"直到下个敌方回合结束，目标造成的伤害减少 4 点；连锁 2：改为减少 8 点。",enemy:true);
            Add(c,"thunder",5,"L05","接地回流",1,0,0,0,CoreNavalExpansionMechanic.Grounding,"抽 1 张；本回合下次主动雷霆牌直接扣除雷栅内敌舰船体时恢复 1 能量；同名副本每回合共用一次成功额度，衍生伤害不触发。",draw:1);
            Add(c,"thunder",6,"L06","雷暴收束",3,18,0,0,CoreNavalExpansionMechanic.ThunderCollapse,"选择含敌舰且剩余格全在 4 格内的一处己方雷栅，移除剩余格并消耗全部连锁 X；原覆盖格双方受 {damage}＋4X 直伤；结算后 +1 连锁。");
            Add(c,"blood",1,"B04","血誓锚阵",2,0,0,0,CoreNavalExpansionMechanic.BloodField,"铺设 2×2 血誓阵；己舰自己回合进入或回合开始位于阵内获 6 格挡，下张主动攻击首段 +4；每大回合一次。",hull:4,field:true);
            Add(c,"blood",2,"B05","止愈血契",0,0,0,0,CoreNavalExpansionMechanic.HealingBan,"目标实际船体恢复减半并向下取整，持续至目标下个回合结束；不叠加。",hull:3,once:true,enemy:true);
            Add(c,"blood",3,"B06","背水重炮",3,30,0,1,CoreNavalExpansionMechanic.DesperateCannon,"造成 {damage} 炮击伤害；支付前己方船体不高于最大船体 40% 时额外 +12。",cannon:true,hull:5,enemy:true);
            return c.AsReadOnly();
        }
        private static void Add(List<CoreBattleCard> cards,string school,int number,string key,string name,int energy,int damage,int block,int powder,
            CoreNavalExpansionMechanic mechanic,string text,bool cannon=false,int mana=0,int hull=0,int wood=0,int min=0,int max=0,string resource="",bool once=false,bool field=false,bool enemy=false,int draw=0,bool all=false)
        {
            string id="card_school_"+school+"_n"+number.ToString("00");
            specs.Add(id,new CoreNavalExpansionCardSpec { Key=key,SchoolId=school,Mechanic=mechanic,ManaCost=mana,HullCost=hull,WoodCost=wood,
                ResourceMinimum=min,ResourceMaximum=max,ResourceLabel=resource,OncePerTurn=once,IsField=field,NeedsEnemyTarget=enemy,ConsumesAllResource=all });
            if(field||enemy||mechanic==CoreNavalExpansionMechanic.HarborExplosion||mechanic==CoreNavalExpansionMechanic.GreatWave) text="4 格曼哈顿范围，每一格须合法；"+text;
            if(field) text+="持续至第 2 次后续己方回合开始，放置本身不触发。";
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
