using System;
using A = ShatteredSail.Core.Gameplay.NavalBattleCardAction;
using M = ShatteredSail.Core.Gameplay.CoreSchoolMechanic;
using U = ShatteredSail.Core.Gameplay.CoreSchoolUpgradeFields;

namespace ShatteredSail.Core.Gameplay
{
    /// <summary>Epic and legendary component cards keep their source identity and work with their school's naval field.</summary>
    public static class CoreComponentFieldCards
    {
        public static bool IsFieldCard(CoreBattleCard card)
        {
            switch (card?.SchoolEffect?.Mechanic)
            {
                case M.ComponentFieldExpand: case M.ComponentFieldExtend: case M.ComponentFieldStrike:
                case M.ComponentFieldShelter: case M.ComponentFieldPulse: case M.ComponentFieldMastery:
                    return true;
                default: return false;
            }
        }

        public static CoreNavalFieldKind Kind(CoreBattleCard card)
        {
            switch (card?.SchoolId)
            {
                case "fire": return CoreNavalFieldKind.Fire;
                case "arcane": return CoreNavalFieldKind.Arcane;
                case "curse": return CoreNavalFieldKind.Curse;
                case "tide": return CoreNavalFieldKind.Tide;
                case "thunder": return CoreNavalFieldKind.Thunder;
                case "blood": return CoreNavalFieldKind.Blood;
                default: throw new ArgumentException("A component field card requires a school.", nameof(card));
            }
        }

        public static string FieldName(CoreNavalFieldKind kind)
        {
            switch (kind)
            {
                case CoreNavalFieldKind.Fire: return "火场";
                case CoreNavalFieldKind.Arcane: return "折射阵";
                case CoreNavalFieldKind.Curse: return "禁域";
                case CoreNavalFieldKind.Tide: return "定向水流";
                case CoreNavalFieldKind.Thunder: return "雷栅";
                case CoreNavalFieldKind.Blood: return "血誓阵";
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        public static CoreBattleCard Apply(CoreBattleCard card, string key)
        {
            if (card == null || card.RequiredComponent.Length == 0 || key == null || key.Length != 3 ||
                (key[0] != 'e' && key[0] != 'l') || key[1] != '0' || key[2] < '1' || key[2] > '5') return card;
            bool legendary = key[0] == 'l';
            CoreNavalFieldKind kind = Kind(card);
            string field = FieldName(kind);
            string expand = "将 4 格内的己方" + field + "向无场地且无船只的相邻格扩张，最多新增 {schoolPower} 格";
            string extend = "使至少有一格位于 4 格内的己方" + field + "整处延长 {schoolPower} 回合";
            switch (key[2])
            {
                case '1':
                    return Create(card, M.ComponentFieldExpand, legendary ? 2 : 1,
                        "范围 1-2 格：造成 {damage} 点船体伤害；获得 {block} 点格挡；" + expand + "。",
                        damage: legendary ? 13 : 7, block: legendary ? 4 : 2,
                        power: legendary ? 4 : 2, action: A.Sabotage);
                case '2':
                    return Create(card, M.ComponentFieldExtend, 1,
                        "获得 {block} 点格挡；抽 {draw} 张牌；获得 {navalActionPoints} 点行动点；" + extend + "。",
                        block: legendary ? 7 : 5, draw: 1, power: legendary ? 2 : 1, movement: 20);
                case '3':
                    return Create(card, M.ComponentFieldStrike, legendary ? 3 : 2,
                        "范围 1-4 格：造成 {damage} 点炮击伤害；敌舰位于己方" + field + "时，伤害提高 {schoolPower} 点。",
                        damage: legendary ? 26 : 18, power: legendary ? 18 : 10, powder: 1, action: A.Cannon);
                case '4':
                    string shelter = "获得 {block} 点格挡；位于己方" + field + "时，额外获得 {schoolPower} 点格挡";
                    int secondary = 0;
                    if (kind == CoreNavalFieldKind.Arcane)
                    {
                        secondary = legendary ? 2 : 1;
                        shelter += "并获得 {schoolSecondary} 点法力";
                    }
                    else if (kind == CoreNavalFieldKind.Blood)
                    {
                        secondary = legendary ? 8 : 4;
                        shelter += "并修复 {schoolSecondary} 点船体";
                    }
                    else shelter = "获得 {block} 点格挡；直到下个己方回合开始，免疫" + field +
                        "影响；位于己方" + field + "时，额外获得 {schoolPower} 点格挡";
                    return Create(card, M.ComponentFieldShelter, legendary ? 2 : 1, shelter + "。",
                        block: legendary ? 10 : 6, power: legendary ? 10 : 6, secondary: secondary,
                        upgrades: U.Power | U.Secondary);
                default:
                    if (legendary)
                        return Create(card, M.ComponentFieldMastery, 2,
                            "抽 {draw} 张牌；" + expand + "；" + extend.Replace("{schoolPower}", "{schoolSecondary}") + "。",
                            draw: 1, power: 4, secondary: 2);
                    string pulse; int power;
                    switch (kind)
                    {
                        case CoreNavalFieldKind.Fire: power = 3; pulse = "敌舰位于己方火场时，施加 {schoolPower} 层燃烧"; break;
                        case CoreNavalFieldKind.Arcane: power = 2; pulse = "位于己方折射阵时，获得 {schoolPower} 点法力"; break;
                        case CoreNavalFieldKind.Curse: power = 3; pulse = "敌舰位于己方禁域时，施加 {schoolPower} 层咒印"; break;
                        case CoreNavalFieldKind.Tide: power = 6; pulse = "位于己方定向水流时，获得 {schoolPower} 点潮势"; break;
                        case CoreNavalFieldKind.Thunder: power = 2; pulse = "敌舰位于己方雷栅时，获得 {schoolPower} 层连锁"; break;
                        default: power = 6; pulse = "位于己方血誓阵时，修复 {schoolPower} 点船体"; break;
                    }
                    return Create(card, M.ComponentFieldPulse, 1, "抽 {draw} 张牌；" + pulse + "。", draw: 1, power: power);
            }
        }

        private static CoreBattleCard Create(CoreBattleCard card, M mechanic, int energy, string text,
            int damage = 0, int block = 0, int draw = 0, int power = 0, int secondary = 0,
            int movement = 0, int powder = 0, A action = A.Standard, U upgrades = U.Power) =>
            new CoreBattleCard(card.Id, card.Name, action == A.Cannon ? "炮击" : action == A.Sabotage ? "突袭" :
                movement > 0 ? "机动" : block > 0 ? "防御" : "策略", energy, damage, block, 0, draw, powder,
                text, NavalBattleStages.Naval, action, card.RequiredCrew, card.ArtId, action == A.Cannon ? 1 : 0,
                card.RequiredComponent, schoolId: card.SchoolId,
                schoolEffect: new CoreSchoolCardEffect(mechanic, power: power, secondary: secondary,
                    movement: movement, upgrades: upgrades));
    }
}
