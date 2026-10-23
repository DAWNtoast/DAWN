using System.Text.RegularExpressions;

namespace ShatteredSail.Core.Gameplay
{
    internal static partial class CoreBattleCardChineseText
    {
        private static string RewriteNaval(string text)
        {
            // Put the acting side and the time before the condition they qualify.
            text = text.Replace("目标出牌前已燃烧时", "出牌前目标已燃烧时，")
                .Replace("目标施放时在雷栅上，再造成", "施放时若目标位于雷栅上，再造成")
                .Replace("出牌前敌方燃烧：", "出牌前敌方已燃烧时，")
                .Replace("出牌前敌方燃烧时增伤", "若出牌前敌方已燃烧，本次伤害提高")
                .Replace("敌方燃烧时强化格挡", "若敌方已燃烧，本次获得的格挡提高")
                .Replace("敌方燃烧时强化恢复", "若敌方已燃烧，本次恢复量提高")
                .Replace("已有连锁强化伤害", "本次伤害随已有连锁层数提高")
                .Replace("己舰自己回合进入或回合开始位于阵内", "己舰在己方回合开始时位于阵内，或在己方回合中进入阵内时，")
                .Replace("敌舰域内结束回合时", "敌舰在禁域内结束回合时")
                .Replace("本次咒印每层伤害提高", "本次咒印结算时，每层伤害提高")
                .Replace("下次咒印结算每层伤害提高", "下次结算咒印伤害时，每层伤害提高")
                .Replace("下张主动火焰攻击实际扣船体后追加", "下次主动火焰攻击实际造成船体伤害后，追加")
                .Replace("，随后消耗浸油", "。触发后移除浸油")
                .Replace("需要已有咒印", "要求目标已有咒印")
                .Replace("额外段不吃炮击或预充加成", "这段额外伤害不受炮击或预充加成影响")
                .Replace("源格和落点均须在", "起点和终点都须在")
                .Replace("保留到期时间", "到期时间不变")
                .Replace("沿所选远离己舰方向推动目标", "沿选定的方向将目标推离己舰")
                .Replace("实际走出后消耗", "目标实际主动移动后移除此效果")
                .Replace("受阻仍减速", "推动受阻时仍施加减速")
                .Replace("同名副本每回合共用一次成功额度", "同名牌每回合合计只能成功触发一次")
                .Replace("直接扣除雷栅内敌舰船体时", "对雷栅内的敌舰造成实际船体伤害时，")
                .Replace("双方先受", "区域内的双方船只先受到")
                .Replace("原覆盖格双方受", "原覆盖区域内的双方船只受到")
                .Replace("选择含敌舰且剩余格全在", "选择一处覆盖敌舰、且剩余格子都在")
                .Replace("格内的一处己方雷栅，移除剩余格", "格内的己方雷栅，移除其剩余格子")
                .Replace("双方首次进入受", "双方船只首次进入时受到")
                .Replace("铺设横或竖", "沿横向或纵向铺设")
                .Replace("双方在水流中结束位移后沿所选方向推动", "双方船只在水流中结束移动后，会沿选定方向被推动")
                .Replace("每船每大回合一次", "每艘船每大回合触发一次")
                .Replace("目标实际船体恢复减半并向下取整", "目标实际恢复的船体值减半（向下取整）")
                .Replace("目标下次新增格挡", "目标下次获得的格挡")
                .Replace("目标下次新增装填", "目标下次增加的装填")
                .Replace("目标下次完整主动攻击伤害", "目标下次主动攻击的总伤害")
                .Replace("同名副本每大回合共享", "同名牌每大回合共用")
                .Replace("次免疫额度", "次免疫机会")
                .Replace("本回合下张主动雷霆攻击结算后", "本回合内，下一张主动雷霆攻击结算后，")
                .Replace("至下个己方回合开始可拒绝一次强制位移", "且可在下个己方回合开始前拒绝一次强制位移")
                .Replace("预备：海战首次点燃后", "预备：海战中首次点燃敌方后，");

            text = Regex.Replace(text, @"每层(已有|出牌前)(连锁|咒印)追加\s*(\d+)\s*点伤害",
                "$1的$2每有一层，本次伤害提高 $3 点");
            text = Regex.Replace(text, @"按消耗前每层咒印获得\s*(\d+)\s*点额外格挡",
                "消耗前的咒印每有一层，额外获得 $1 点格挡");
            text = text.Replace("按消耗前咒印，每层获得", "按消耗前的咒印层数，每层获得")
                .Replace("按当前咒印层数每层获得", "当前咒印每有一层，获得");
            text = Regex.Replace(text, @"按出牌前潮势的\s*(\d+)%\s*追加伤害",
                "本次伤害提高出牌前潮势的 $1%");
            text = Regex.Replace(text, @"按出牌前潮势的\s*(\d+)%\s*折入伤害",
                "本次伤害已计入出牌前潮势的 $1%");
            text = Regex.Replace(text, @"(^|[；。])伤害提高\s*(\d+)\s*点", "$1本次攻击的伤害提高 $2 点");
            text = Regex.Replace(text, @"敌方生命不高于\s*(\d+)%：", "敌方生命不高于 $1% 时，");
            text = Regex.Replace(text, @"最大船体\s*(\d+)%", "最大船体的 $1%");
            text = Regex.Replace(text, @"双方每船每格每大回合一次\s*(\d+)\s*点场地伤害",
                "双方船只每大回合在每个火场格受到一次 $1 点场地伤害");
            text = Regex.Replace(text, @"使下一次敌回合末咒印结算每层额外扣除\s*(\d+)\s*点生命",
                "下次敌方回合结束结算咒印时，每层额外扣除 $1 点生命");
            text = Regex.Replace(text, @"敌回合末每层扣除\s*(\d+)\s*点生命",
                "敌方回合结束时，每层咒印扣除 $1 点生命");
            text = Regex.Replace(text, @"每层在回合末造成\s*(\d+)\s*点伤害", "回合结束时，每层造成 $1 点伤害");
            text = Regex.Replace(text, @"(下张主动秘术攻击首段)最多\s*(\d+)\s*点穿盾",
                "$1最多有 $2 点伤害穿透格挡");
            text = Regex.Replace(text, @"其中最多\s*(\d+)\s*点穿透格挡", "其中最多 $1 点伤害穿透格挡");
            text = Regex.Replace(text, @"敌舰在禁域内结束回合时，本次咒印结算时，每层伤害提高\s*(\d+)\s*点，追加最多\s*(\d+)",
                "敌舰在禁域内结束回合时，咒印每层伤害提高 $1 点，本次额外伤害最多 $2 点");
            text = Regex.Replace(text, @"直到下个己方回合开始免疫接下来\s*(\d+)\s*次火场伤害",
                "下个己方回合开始前，可免疫接下来 $1 次火场伤害");
            text = text.Replace("与折射穿盾取较高值", "与折射阵的穿盾效果相比，只取较高值");

            // Keep prerequisite ranges before the formula they feed, without repeating icon costs.
            text = Regex.Replace(text, @"(造成[^。；]+。)选择\s*(\d+～\d+)\s*(点潮势|层咒印)。",
                "选择 $2 $3，$1");
            text = Regex.Replace(text, @"每点所选潮势移除目标\s*(\d+)\s*点格挡。选择\s*(\d+～\d+)\s*点潮势。",
                "选择 $2 点潮势，每点移除目标 $1 点格挡。");
            text = Regex.Replace(text, @"(^|[；。])(\d+)\s*格内；选择\s*(\d+～\d+)\s*(点潮势|层咒印)，造成",
                "$1选择 $3 $4，对 $2 格内的敌舰造成");
            text = Regex.Replace(text, @"(^|[；。])(\d+)\s*格内；选择\s*(\d+～\d+)\s*点潮势，每点移除目标",
                "$1选择 $3 点潮势，每点移除 $2 格内目标的");
            text = Regex.Replace(text,
                @"^(\d+)\s*格内；选择波向和与其垂直的\s*(\d+×\d+)\s*区域；([^。]+)。选择\s*(\d+～\d+)\s*点潮势。",
                "选择 $4 点潮势，并在 $1 格内选定波向和与其垂直的 $2 区域；$3。");
            // Scope a range to its action, instead of leaving it as a sentence fragment.
            text = Regex.Replace(text, @"(^|[；。])(\d+)\s*格内炮击；造成", "$1炮击 $2 格内的敌舰，造成");
            text = Regex.Replace(text, @"(^|[；。])(\d+)\s*米内近距破坏；造成", "$1对 $2 米内的敌舰发动近距破坏，造成");
            text = Regex.Replace(text, @"(^|[；。])(\d+)\s*米内法术；造成", "$1对 $2 米内的敌舰施法，造成");
            text = Regex.Replace(text, @"(^|[；。])(\d+)\s*(格|米)内[：；](先)?造成", "$1对 $2 $3内的敌舰$4造成");
            text = Regex.Replace(text, @"(^|[；。])(\d+)\s*格内；铺设", "$1在 $2 格内铺设");
            text = Regex.Replace(text, @"(^|[；。])(\d+)\s*格内；沿横向或纵向铺设", "$1在 $2 格内沿横向或纵向铺设");
            text = Regex.Replace(text, @"(^|[；。])(\d+)\s*格内；选择", "$1在 $2 格内选择");
            text = Regex.Replace(text, @"(^|[；。])(\d+)\s*格内；施加浸油至下个己方回合结束",
                "$1对 $2 格内的敌舰施加浸油，持续至下个己方回合结束");
            text = Regex.Replace(text, @"(^|[；。])(\d+)\s*格内；目标", "$1使 $2 格内目标");
            text = Regex.Replace(text, @"(^|[；。])(\d+)\s*格内；沿选定的方向将目标推离己舰",
                "$1沿选定的方向，将 $2 格内的目标推离己舰");

            text = text.Replace("激活本场海战专属规则：", "本场海战中，");
            text = Regex.Replace(text, @"每回合首张其他(\S+?)牌成功后", "每回合首次成功打出其他$1牌后");
            text = text.Replace("每回合首张其他雷霆攻击成功后", "每回合首次成功使用其他雷霆攻击后")
                .Replace("每回合首张其他血契牌免除生命费用", "每回合打出的第一张其他血契牌免除生命费用");

            // Legacy templates use the first number as the unified stack count.
            text = text.Replace("命中灼烧", "命中后灼烧")
                .Replace("命中：未燃烧则灼烧", "命中后，若目标未燃烧，则灼烧")
                .Replace("实际造成直伤后", "实际造成直接伤害后")
                .Replace("未燃烧则灼烧", "若目标未燃烧，则灼烧");
            text = Regex.Replace(text, @"已燃烧则延烧\s*(\d+)\s*回合", "若目标已燃烧，则延长 $1 回合燃烧时间");
            text = Regex.Replace(text, @"灼烧\s*(\d+)×(\d+)\s*回合",
                "施加 $1 层燃烧");
            text = Regex.Replace(text, @"点燃敌舰，造成\s*(\d+)×(\d+)\s*回合燃烧",
                "对敌舰施加 $1 层燃烧");
            text = Regex.Replace(text, @"和\s*(\d+)×(\d+)\s*回合燃烧",
                "，并施加 $1 层燃烧");
            text = Regex.Replace(text, @"延长敌方燃烧\s*(\d+)\s*回合", "使敌方的燃烧持续时间延长 $1 回合");
            text = Regex.Replace(text, @"接下来\s*(\d+)\s*回合，敌方自然燃烧结算时",
                "接下来的 $1 个回合中，敌方燃烧伤害自然结算时，");
            text = text.Replace("对燃烧敌舰", "对燃烧中的敌舰");
            text = Regex.Replace(text, @"获得\s*(\d+)\s*发(附火|烈焰强化)，上限\s*(\d+)",
                "获得 $1 发$2，最多储备 $3 发");

            text = Regex.Replace(text, @"修复本次有效直伤的\s*(\d+)%\s*，上限\s*(\d+)\s*点船体",
                "按本次有效直接伤害的 $1% 修复船体，最多修复 $2 点船体");
            text = Regex.Replace(text, @"恢复本次有效直伤的\s*(\d+)%\s*，上限\s*(\d+)",
                "按本次有效直接伤害的 $1% 恢复生命，最多恢复 $2 点");
            text = Regex.Replace(text, @"恢复已失生命的\s*(\d+)%\s*，上限\s*(\d+)",
                "恢复已损失生命值的 $1%，最多恢复 $2 点生命值");
            text = Regex.Replace(text, @"恢复\s*(\d+)\s*点生命（按已失生命的\s*(\d+)%\s*，上限\s*(\d+)）",
                "恢复 $1 点生命值（恢复量为已损失生命值的 $2%，上限为 $3 点）");
            return Regex.Replace(text, @"，\s*，", "，");
        }
    }
}
