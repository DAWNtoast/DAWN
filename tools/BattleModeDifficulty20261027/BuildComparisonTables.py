"""Build descriptive, stage-separated tables; no optimal-policy claim."""
import argparse
import csv
import json
import statistics
from collections import Counter, defaultdict
from pathlib import Path

STAGES = {"starter": "基础单桅", "patrol": "中期巡防", "gunship": "成熟重炮"}
MODES = {"cannon": "普通炮战", "cannon-tide": "潮系炮战", "boarding-neutral": "中性接舷", "boarding-curse": "缠咒接舷"}
FIRST = {"boss_route_shattered_sail_reef", "boss_route_shattered_sail_reef_tide"}
MIDDLE = {"boss_route_golden_tide_trade", "boss_route_golden_tide_trade_thunder"}


def appropriate(row):
    if row["cohort"] == "starter":
        return row["boss"] in FIRST | {"event_reef_coral_ambush"}
    if row["cohort"] == "patrol":
        return row["boss"] in MIDDLE | {"gold_customs"}
    return row["boss"] not in FIRST | MIDDLE


def aggregate(rows):
    counts = Counter(x["outcome"] for x in rows)
    return {
        "n": len(rows), "wins": sum(x["won"] for x in rows),
        "achieved": sum(x["achievedMode"] for x in rows),
        "outcomes": dict(counts),
        "minHull": statistics.mean(x["minimumHull"] / x["maxHull"] for x in rows),
        "minCrew": statistics.mean(x["minimumCrew"] / x["playerMaxCrew"] for x in rows),
        "turnEnds": statistics.mean(x["prepTurns"] + x["navalTurns"] + x["boardingTurns"] for x in rows),
        "replacement": statistics.mean(x["replacementCost"] for x in rows),
    }


def outcome_text(a):
    labels = {"sunk": "击沉", "captured": "夺船", "dead": "死亡", "forced_boarding": "转入接舷", "timeout": "超时"}
    return "、".join(str(n) + labels.get(k, k) for k, n in a["outcomes"].items())


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("run", type=Path)
    args = parser.parse_args()
    root = args.run.resolve()
    rows = json.loads((root / "samples.json").read_text(encoding="utf-8-sig"))
    relevant = [x for x in rows if appropriate(x)]
    groups, per_enemy = defaultdict(list), defaultdict(list)
    for row in relevant:
        groups[(row["cohort"], row["mode"])].append(row)
        per_enemy[(row["cohort"], row["boss"], row["name"])].append(row)
    summary = [{"cohort": c, "mode": m, **aggregate(v)} for (c, m), v in groups.items()]
    text = ["## 分阶段结果", "", f"主矩阵 {len(rows)} 场中，以下 {len(relevant)} 场用于适配阶段对照；其余 {len(rows)-len(relevant)} 场为跨阶段压力样本，未并入本表。", "",
            "|阶段|打法|场数|结果|平均最低船体|平均最低接舷生命|平均已结束回合|耗材补购参考金币|",
            "|---|---|---:|---|---:|---:|---:|---:|"]
    for s in summary:
        crew = "—" if s["mode"].startswith("cannon") else f'{s["minCrew"]:.1%}'
        text.append(f'|{STAGES[s["cohort"]]}|{MODES[s["mode"]]}|{s["n"]}|{outcome_text(s)}|{s["minHull"]:.1%}|{crew}|{s["turnEnds"]:.2f}|{s["replacement"]:.2f}|')
    text += ["", "船体与接舷生命分属两个系统，不能直接相加为综合难度分；接舷的船体列包含靠近过程。最低比例先按每场归一化，再求平均，死亡样本计 0。转入接舷不是死亡。回合列是实际 EndBattleTurn 次数，包含准备与前置海战，不包含取胜时尚未结束的行动窗口。耗材为同港同日补购报价，未扣奖励、未计金币败北损失，不代表实际净收益。", "", "## 逐敌结果", "", "每格依次为结果；平均最低船体 / 接舷生命。每打法每敌 8 个种子。", "", "|阶段|敌人|普通炮战|潮系炮战|中性接舷|缠咒接舷|", "|---|---|---|---|---|---|"]
    csv_rows = []
    for (cohort, boss, name), members in per_enemy.items():
        cells = []
        for mode in MODES:
            part = [x for x in members if x["mode"] == mode]
            if not part:
                cells.append("未解锁")
                continue
            a = aggregate(part)
            life = f'{a["minHull"]:.1%}' + (" / —" if mode.startswith("cannon") else f' / {a["minCrew"]:.1%}')
            cells.append(outcome_text(a) + "；" + life)
            csv_rows.append({"stage": STAGES[cohort], "enemyId": boss, "enemy": name, "mode": MODES[mode], **a, "outcomes": outcome_text(a)})
        text.append("|" + "|".join([STAGES[cohort], name] + cells) + "|")
    (root / "comparison-tables.md").write_text("\n".join(text) + "\n", encoding="utf-8")
    (root / "appropriate-summary.json").write_text(json.dumps(summary, ensure_ascii=False, indent=2), encoding="utf-8")
    with (root / "适配阶段逐敌对照.csv").open("w", encoding="utf-8-sig", newline="") as f:
        writer = csv.DictWriter(f, fieldnames=list(csv_rows[0]))
        writer.writeheader()
        writer.writerows(csv_rows)
    print(json.dumps({"run": root.name, "total": len(rows), "appropriate": len(relevant), "summary": summary}, ensure_ascii=False))


if __name__ == "__main__":
    main()
