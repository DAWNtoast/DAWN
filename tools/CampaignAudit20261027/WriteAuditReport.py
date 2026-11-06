"""Render the final Chinese audit from immutable complete-run evidence."""
import collections
import csv
import json
from decimal import Decimal
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "outputs/campaign-audit-20261027"
TAGS = [("Campaign13", "路线 A：先打捞船，再巡防与重炮"),
        ("DirectPatrol14", "路线 B：直接攒巡防舰，再换重炮")]


def load(path):
    return json.loads(path.read_text(encoding="utf-8-sig"), parse_float=Decimal)


def readcsv(path):
    with path.open(encoding="utf-8-sig", newline="") as f:
        return list(csv.DictReader(f))


def n(value):
    return f"{Decimal(str(value)):,.2f}"


def link(label, path):
    return f"[{label}]({path.as_posix()})"


def decktext(cards):
    counts = collections.Counter((c["name"], c["upgrade"]) for c in cards)
    return "；".join(f"{name} +{rank} ×{count}" for (name, rank), count in counts.items()) or "无"


def main():
    econ = load(OUT / "economy-rules.json")
    crew = {c["id"]: c["name"] for c in econ["crew_catalog"]}
    runs = []
    for tag, title in TAGS:
        p = OUT / tag
        data = load(p / "campaign.json")
        assert data["completed"] and len(data["firstKills"]) == 13
        assert load(p / "independent-ledger-review.json")["status"] == "PASS"
        runs.append((tag, title, data, readcsv(p / "actions.csv"), readcsv(p / "battles.csv")))

    first_kills = []
    details = ["# 首次击败首领：船只、装备和卡组明细", "",
               "以下配置使用各场战斗**开战前**快照，收支与金币使用击杀奖励产生后的首杀快照。所有航次含两次教程。+N 为卡牌等级，×N 为副本数；附加卡由实际随船专家或已装备蓝色及以上组件提供。组件按当时实际装备列出，击杀掉落未提前计入。", ""]
    for tag, title, data, actions, battles in runs:
        parts = {r["id"]: r for r in econ["component_prices"]}
        details += [f"## {title}", ""]
        for kill in data["firstKills"].values():
            state, start = kill["firstKill"], kill["battleStart"]
            ship = start["ship"]
            equipped = [parts[c["ComponentId"]]["name"] + "（" + parts[c["ComponentId"]]["rarity"] + "）"
                        for c in start["components"] if c["ShipIndex"] == ship["index"]]
            stages = kill["stageTurns"]
            details += [f"### 第 {state['trip']} 航 · {kill['name']}", "",
                        f"累计获得 {n(state['totalIncome'])} 金币；累计消费 {n(state['totalExpense'])}；结算损耗 {n(state['totalLoss'])}；总现金 {n(state['totalCash'])}（账户 {n(state['accountGold'])}，随船 {n(state['travelGold'])}，待结算 {n(state.get('pendingSettlementGold', 0))}）。本战金币奖励 {n(kill['battleRewardGold'])}，仍受之后归港保护比例影响。", "",
                        f"船只：{ship['name']}；开战船体 {ship['hull']}/{ship['maxHull']}，护甲 {ship['armor']}，火力 {ship['firepower']}，航速 {ship['speed']}，货舱 {ship['capacity']}。海盗等级 {start['pirateLevel']}。", "",
                        "已装组件：" + "；".join(equipped) + "。", "",
                        f"船员：普通水手 {start['ordinaryCrew']}；" + "、".join(crew.get(i, i) for i in start['crew']) + "。", "",
                        "开战物资：" + "，".join(f"{label} {start['cargo'][key]}" for key, label in [("Water", "水"), ("Food", "食物"), ("Powder", "火药"), ("Cannonballs", "炮弹"), ("Wood", "木板"), ("Medicine", "药品")]) + "。", "",
                        f"已结束回合数：准备 {stages['Preparation']}、海战 {stages['Naval']}、接舷 {stages['Boarding']}；进入接舷前结束海战回合 {kill['boardingEntryAfterNavalTurns']}。这些计数不包括未结束即完成击杀的当前回合。", ""]
            for stage, label in [("Preparation", "准备"), ("Naval", "海战"), ("Boarding", "接舷")]:
                details += [f"- {label}基础牌组：{decktext(start['deck'][stage])}。",
                            f"- {label}附加牌：{decktext(start.get('bonusDeck', {}).get(stage, []))}。"]
            details += ["", f"击杀时已拥有藏品 {len(state['collection'])} 件；陈列记录 `{state['collectionDisplayed']}`。本路线不领取收藏收益，额外藏品也没有自动填满所有港口的陈列位。", ""]
            first_kills.append({"run": tag, "boss": kill["name"], "bossId": kill["id"], "voyages": state["trip"],
                               "voyagesAfterTutorial": state["trip"] - 2, "income": str(state["totalIncome"]),
                               "spending": str(state["totalExpense"]), "loss": str(state["totalLoss"]),
                               "cash": str(state["totalCash"]), "account": str(state["accountGold"]),
                               "aboard": str(state["travelGold"]), "battleReward": str(kill["battleRewardGold"]),
                               "ship": ship["name"], "pirateLevel": start["pirateLevel"],
                               "boardingStrategy": start.get("boardingStrategy", "neutral-sustain"),
                               "navalTurnsBeforeBoarding": kill["boardingEntryAfterNavalTurns"],
                               "components": "；".join(equipped),
                               "preparationDeck": decktext(start["deck"]["Preparation"]),
                               "navalDeck": decktext(start["deck"]["Naval"]),
                               "boardingDeck": decktext(start["deck"]["Boarding"])})
    (OUT / "首杀船只与卡组明细.md").write_text("\n".join(details), encoding="utf-8")
    with (OUT / "两条路线首杀节点.csv").open("w", encoding="utf-8-sig", newline="") as file:
        writer = csv.DictWriter(file, fieldnames=first_kills[0].keys())
        writer.writeheader()
        writer.writerows(first_kills)

    lines = ["# 当前版本成长与 Boss 数值审核", "", "审核日期：2026-10-27。正式完整记录：Campaign13 与 DirectPatrol14；两次都从全新角色开始，实际使用同一冻结版生产规则，不注入金币、船只、卡牌等级、抽牌结果或胜利。", "",
             "**结论：当前版本存在不依赖离线金币的完整通关路线；不能据此认定数值已经整体合理。更突出的风险是快速接舷绕过海战场地链、咒印阈值路线对高血量首领的影响，以及离线收益上限削弱收藏成长。中期的长间隔同时受转港、换船和战术选择影响，不能全部归因于缺金币。**", "",
             "## 完成范围与统计口径", "",
             "- Boss 调整后再次复核：1209/1209 回归测试通过。此前海战单场校准另有固定配置样本，不能当作完整成长通关。",
             "- 真实 Unity 界面验证：新游戏、漫画、完整教程、归港、陈列、装备、保存和继续游戏，1/1 通过，保留 24 张截图。",
             "- 长期成长由实际游戏规则的合法操作自动完成；桌面自动化入口不可用，**没有声称全程人工逐屏游玩**。两条完整路线总计 167 场战斗，覆盖每条路线全部 13 类首领。",
             "- 收入不含初始 560 金币；消费不含结算丢失。账户、随船现金和结算托管转移不重复记账。首杀时仍在海上的金币未必全额保留到港。",
             "- 所有航次包括 2 次教学；金额显示至小数点后两位，原始 CSV/JSON 保留完整精度。没有测量真人通关小时数，游戏内天数和脚本执行秒数都不能代替真人时长。", "",
             "## 两条完整路线", "", "| 路线 | 6 种传奇齐全 | 13 类首领齐全 | 全程战斗 / 败北 | 全程航行动作 | 全程最低账户余额 |", "|---|---:|---:|---:|---:|---:|"]
    for tag, title, data, actions, battles in runs:
        lines.append(f"| {title} | 第 {data['primaryCompletion']['trip']} 航 | 第 {data['final']['trip']} 航 | {len(battles)} / {sum(r['outcome']=='loss' for r in battles)} | {sum(r['actionType']=='Sail' for r in actions)} | {n(min(Decimal(r['accountGold']) for r in actions))} |")
    lines += ["", "地图、首领抽选和掉落使用正常随机源，两次没有固定同一组随机数。因此 23 航与 22 航是实际样本差异，不是严格的配对因果估计。直接购买巡防舰能赢中级首领，则是重炮舰并非硬性前置条件的直接证据。", "",
              "| 全部传奇首次击败时 | 累计获得 | 累计消费 | 结算及失败损耗 | 账户 | 随船 | 总现金 |", "|---|---:|---:|---:|---:|---:|---:|"]
    for tag, title, data, _, _ in runs:
        s = data["primaryCompletion"]
        lines.append(f"| {title} | {n(s['totalIncome'])} | {n(s['totalExpense'])} | {n(s['totalLoss'])} | {n(s['accountGold'])} | {n(s['travelGold'])} | {n(s['totalCash'])} |")
    lines += ["", "两条路线的离线补贴均为 **0**。上述总现金满足：初始 560 + 累计收入 − 累计消费 − 结算损耗。全首领完成并归港后，路线 A 账户 32,047.49，路线 B 账户 23,168.44。两个账本共 779 条现金变动记录已经独立逐笔对平。", "",
              "## 每类首领首次击败节点", "", "下表采用更短的路线 B，路线 A 的对应航次一并列出。每战的船员、组件、三阶段完整牌组及升级等级见配置附件；两条路线的完整金额都在 CSV。", "",
              "| 首领 | B 首杀航次 | A 首杀航次 | B 累计获得 | B 累计消费 | B 总现金 | B 船只 |", "|---|---:|---:|---:|---:|---:|---|"]
    a, b = runs[0][2], runs[1][2]
    for kill in b["firstKills"].values():
        s = kill["firstKill"]
        lines.append(f"| {kill['name']} | {s['trip']} | {a['firstKills'][kill['id']]['firstKill']['trip']} | {n(s['totalIncome'])} | {n(s['totalExpense'])} | {n(s['totalCash'])} | {s['ship']['name']} |")
    lines += ["", link("两条路线全部首杀配置", OUT / "首杀船只与卡组明细.md") + " · " + link("可筛选的首杀节点 CSV", OUT / "两条路线首杀节点.csv"), "",
              "## 成长节奏和瓶颈", "",
              "### 早期：现金确实紧，但本次没有被迫离线等待", "",
              "真实新档账户 500、随船 60；教学首领击沉路线毕业账户 560，合法夺船路线毕业 645。路线 B 在第 4/7 航打败两种初级首领，路线 A 为第 3/4 航；两条路线最低账户分别约 129 和 160 金。普通卡 +1/+2/+3 分别花 120/240/480，招募和多个技能同时升级会压缩早期余钱。该阶段优先购买主力卡有效等级，再按实际战术补给，比平均强化更关键。", "",
              "首件藏品必须在教程首领胜利且安全返港后获得并陈列。此前没有收藏收入；教学失败虽可进入正常市场恢复，却不直接补发免费金币或物资。因此“有收藏收入就绝不会卡死”并非对新档全阶段成立。两条成功样本也不能覆盖任意失误状态。", "",
              "### 中期：存在可感知的停顿，但不全是价格门槛", "",
              "路线 A 从第 4 航初级双首领齐全，到第 17 航才首次击败中级首领，中间有 13 航间隔。该策略先买 1300 金打捞船，又设定较高的转港储备；第 16 航购买巡防舰前已持有约 6410.21 金，显然不是还差 1800 金船价。", "",
              "路线 B 跳过打捞船，以约 2200 金转港条件先买巡防舰：第 12 航归港购船，第 13/15 航用同一艘巡防舰击败两种中级首领，第 18 航再买重炮舰。初级双首领到首个中级首领的间隔为 6 航。玩家若不清楚船坞所在地、哪些船足够挑战、怎样转港，可能把路线选择问题体验为长期刷钱；应优先改善进阶目标提示，而不是直接全面下调船价。", "",
              "### 后期：收益足够，战术与首领覆盖更突出", "",
              "路线 A 第 20–23 航完成六种传奇首杀，路线 B 为第 19–22 航。首杀全部传奇时，战斗收入占两条路线总收入约 90% 和 94%。路线 A 花费构成为购船 7900、强化 3480、专家 820、补给 1364.48；路线 B 为购船 6600、强化 3480、专家 810、补给 1108.55。没有购买组件，主要依靠合法战斗掉落改善装配。", "",
              "六种传奇齐全后，两条路线又分别花 8/6 航补齐所有普通航路首领，最后一个均为月织星槎号；这属于首领覆盖与挑战结果造成的尾段，不应算成击败传奇前的资金门槛。", "",
              "最终路线 A 第 19 航在终界观星号接舷战败北时，仍有账户 1509.07 金、药品 15、船体 238/252；路线 B 第 20 航同类败北时还有账户 6197.16 金、药品 21、船体 260/279。这两次都不是缺金币或无药导致，随后正常回港更换已解锁的生存与缠咒牌组继续通关。不能把特定策略的失败写成首领必定不可打，应把战术选择、装备掉落与抽牌波动纳入难度判断。", "",
              "## 战斗系统需要优先复核的地方", "",
              "1. **快速接舷绕过海战长链。** 12 场传奇首杀中，11 场在结束 0–1 个海战回合后接舷，另 1 场为 2 个海战回合。第 3/5/8/10/13 回合免疫、场地扩张和续期大多没机会发挥。这是正常规则允许的有效战术，也意味着提高海战船体和延长链条，无法直接控制这类路线的难度。建议先对比击沉与夺船的实际消耗、风险和奖励，再决定是否给首领增加清楚预告且可应对的防接舷行为。", "",
              "2. **缠咒的高血量旁路很强。** 当前阈值初始 50%，缠咒每次降低 10 个百分点，累计五次可降至 0%，即使没有咒印也立即消灭存活敌人；这符合现有卡牌文本和实现。路线使用两张缠咒加格挡、治疗和回能，通过合法过牌生存达成。首领血量上调对这条路线的收益有限。若设计目标是让高阶首领更耐打，需单独审核阈值规则，不能只堆血；本轮没有擅自改变该卡效果。", "",
              "3. **夺船奖励放大了接舷优势。** 战斗材料折现金币后按整百舍入，夺船再乘 2；两条完整路线均主要采用夺船。路线 A 六种传奇单战奖励在 2000–2600 金之间，而普通航路首领为 600 金。开始稳定击败传奇后，4800 金重炮舰和普通卡强化的金币压力迅速消退。是否合理取决于接舷是否承担相称风险，需要与同装备炮战路线做等条件比较。", "",
              "4. **金币不是无限后期成长。** 免费组入已解锁卡、普通卡等级上限、特殊卡依靠来源经验、当前船已装组件和稀有材料要求，都使金币不能直接解决所有战斗或投资问题。离线收入适合作为恢复资金的补充，不能代替构筑指引和战斗反馈。", "",
              "本次足以判断‘存在零离线通关路线’，不足以宣布所有流派和随机样本都平衡。之前的纯海战固定配置校准与本次完整接舷成长是两类证据，应分开使用。", "",
              "## 收藏收入能缓解多少等待", "",
              "每件已陈列藏品每现实小时 10 金；全港共享待领取上限 300。拥有但未陈列不计收入；游戏内航行小时不推进现实收益。在线也积累，但仅在新的应用进程首次进入存档时领取，同进程返回主菜单再继续不会重复领取。", "",
              "| 陈列件数 | 金币/现实小时 | 到 300 上限 | 从 0 等到 1800 巡防舰 | 从 0 等到 4800 重炮舰 |", "|---:|---:|---:|---:|---:|",
              "| 1 | 10 | 30 小时 | 180 小时 | 480 小时 / 20 天 |",
              "| 2 | 20 | 15 小时 | 90 小时 | 240 小时 / 10 天 |",
              "| 3 | 30 | 10 小时 | 60 小时 | 160 小时 / 6.67 天 |",
              "| 6 | 60 | 5 小时 | 30 小时 | 80 小时 / 3.33 天 |", "",
              "以上是及时领取、不浪费封顶时间的理论下限；1800 至少领取 6 次，4800 至少 16 次。6 件藏品若每天只登录一次，日收入仍被封顶为 300，买重炮舰需 16 天；1 件每日领取为 240，需 20 天。也就是说，日登玩家从 1 件升到 6 件，日收入只从 240 增到 300，而不是六倍。一次等满 80 小时再登录，仍然只有 300 金。", "",
              "若希望收藏明显缩短中后期等待，优先评估按目标登录间隔设置容量，例如让满陈列至少能存 12–24 小时收益，或增加游戏内主动领取入口；这些是待验证的设计方案，本次未调整参数。若坚持当前 300 上限，应明确展示全港共享上限和领取条件，避免玩家误以为多收藏可自动攒出一艘船。", "",
              "还存在条件性边界：本港当月补给额度耗尽、船上补给不足时，现实等待不会推进游戏月份，卖货也不返还已购额度。金币变多不等于水粮库存刷新。本次完整路线水粮没有耗尽，该边界不是对正常流程必然卡死的判断。", "",
              "## 建议处理顺序", "",
              "1. 先审快速接舷、夺船双倍收益与缠咒阈值对首领难度的共同影响；继续单独提高海战血量可能只拉大两种打法的体验差。",
              "2. 增加中期进阶提示：巡防舰所在地、1800 船价、可应对的中级首领，以及转港所需条件；保留自主换船与组牌选择。",
              "3. 明示收藏收入的现实时间、陈列要求、全港 300 上限与重启领取条件，并用每日登录节奏复核容量。",
              "4. 另做新手失误恢复测试：教程首胜前用尽金币、购买额度耗尽、连续战败等边界；决定是否需要一条有限且明确的恢复途径。",
              "5. 下一轮平衡验证采用多种构筑、多组随机样本和真人用时；尤其记录炮战与接舷首杀成本。当前结果可作为基线，不把单一路线当作平均玩家。", "",
              "## 交付与证据", "",
              "- " + link("每场首杀完整船只与卡组", OUT / "首杀船只与卡组明细.md"),
              "- " + link("两条路线首杀节点 CSV", OUT / "两条路线首杀节点.csv"),
              "- " + link("经济规则、价格与等待公式", OUT / "economy-rules.md"),
              "- " + link("审核方法及统计口径", OUT / "审核方法与口径.md"),
              "- " + link("最终适配器独立复核", OUT / "campaign-final-adapter-review.json"),
              "- " + link("真实界面验收及截图说明", ROOT / "Artifacts/CampaignAudit20261027/UI验收总结.md"),
              "- " + link("Boss 行为与单场数值校准", ROOT / "outputs/boss-behavior-20261027/Boss行为与数值调整.md"), ""]
    for tag, title, _, _, _ in runs:
        lines += [f"{title}：" + " · ".join(link(label, OUT / tag / file) for label, file in [("完整首杀 JSON", "campaign.json"), ("金币账本", "economy.csv"), ("航程", "voyages.csv"), ("战斗", "battles.csv"), ("完整操作", "actions.csv"), ("独立对账", "independent-ledger-review.json")]), ""]
    cleanup = []
    for p in OUT.glob("*/cleanup.json"):
        x = load(p)
        cleanup.append((p, int(x.get("ReleasedBytes", 0))))
    boss_cleanup = load(ROOT / "Artifacts/BossCalibration20261024/BossBehavior20261027-CampaignReview/cleanup.json")
    total = sum(v for _, v in cleanup) + int(boss_cleanup.get("FreedBytes", 0)) + 8722
    lines += [f"本次编译和临时测试缓存累计清理 {total:,} 字节（约 {total/1024/1024:.2f} MiB）；保留冻结源码、工具输入、结果、截图、QA 存档和必要失败证据。共享预热 Library 有现役工具依赖，按验收记录保留；没有清理主工程 UnityClient 的内容。", "",
              "本次审核未修改生产数值、卡牌定义或正式存档。UI 使用独立 company/product 与存档路径；未采集正式用户存档的前后哈希，因此不作逐字节一致性声明。首杀配置不是可直接导入游戏的全通关存档；实际可继续的 QA 存档保存于教程完成处。", "",
              "审计边界：自动流程没有完整复现所有绘图收藏领取和全港陈列操作，藏品计数是该流程已记录的结果，不能作为全收藏效率排名；主线不领取离线收入，故这不增加通关现金。终点可能尚有最后一战未领取的掉落，配置附件采用开战前实际装备，不将这些掉落提前用于战斗。早期 Policy 与 Campaign10/11、DirectPatrol12 是调试或修正前证据，最终首杀和金额以 Campaign13/DirectPatrol14 为准。"]
    (OUT / "全流程数值审核.md").write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(json.dumps({"report": str(OUT / "全流程数值审核.md"), "firstKillRows": len(first_kills), "configurations": len(first_kills), "cleanupBytes": total}, ensure_ascii=False))


if __name__ == "__main__":
    main()
