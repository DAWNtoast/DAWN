"""Finalize the independent policy review using retained Final07 evidence."""
import collections
import csv
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "outputs/battle-mode-difficulty-20261027"
RUN = OUT / "Final07"
SENS = OUT / "SensitivityStarter"


def read(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    data = read(OUT / "policy-data-review-Final07.json")
    assert data["result"] == "PASS_INITIAL_PAIRING_AND_HASHES" and not data["errors"]
    primary = read(RUN / "samples.json")
    sensitivity = read(SENS / "samples.json")
    assert len(primary) == 1120 and len(sensitivity) == 16
    verified = 0
    for filename, path_key in [("source-hashes.csv", "Snapshot"), ("input-hashes.csv", "Path")]:
        with (SENS / filename).open(encoding="utf-8-sig", newline="") as handle:
            for row in csv.DictReader(handle):
                assert sha(Path(row[path_key])) == row.get("SHA256", row.get("Hash")).lower()
                verified += 1
    comparisons = []
    for sample in sensitivity:
        baseline = next(r for r in primary if (r["cohort"], r["mode"], r["boss"], r["seed"]) ==
                        (sample["cohort"], "cannon", sample["boss"], sample["seed"]))
        assert sample["mode"] == "cannon-starter10"
        assert {k: v for k, v in baseline["initial"].items() if k not in ("naval", "mode", "upgrades")} == {
            k: v for k, v in sample["initial"].items() if k not in ("naval", "mode", "upgrades")}
        assert {k: v for k, v in baseline["initial"]["upgrades"].items() if v} == {
            k: v for k, v in sample["initial"]["upgrades"].items() if v}
        assert len(baseline["initial"]["naval"]) == 12 and len(sample["initial"]["naval"]) == 10
        comparisons.append({"boss": sample["boss"], "seed": sample["seed"],
                            "twelveCardOutcome": baseline["outcome"], "tenCardOutcome": sample["outcome"]})
    sensitivity_review = {"result": "PASS_SAME_INITIAL_CONDITIONS_EXCEPT_NAVAL_DECK", "samples": 16,
                          "verifiedInputFiles": verified, "comparisons": comparisons,
                          "samplesSHA256": sha(SENS / "samples.json")}
    (OUT / "policy-sensitivity-review.json").write_text(json.dumps(sensitivity_review, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    def evidence(needle, note):
        path = RUN / "Inputs/Program.cs"
        text = path.read_text(encoding="utf-8-sig")
        assert needle in text
        return {"source": path.as_posix(), "line": text[:text.index(needle)].count("\n") + 1, "note": note}
    checks = [
        {"id": "same-fixture-budget", "result": "PASS", "detail": "336 paired groups match initial ship, stats, fitted source cards, crew, level, HP, supplies and exact upgrade spend; legal decks are allowed to differ.", "evidence": [evidence("Need(s.SetBattleDeck", "Public stage/unlock/deck checks"), evidence("spent=Upgrade", "Actual equal-cost upgrades"), evidence("fixture cargo capacity", "Capacity assertion")]},
        {"id": "proper-cannon-policy", "result": "PASS_WITH_HEURISTIC_LIMITS", "detail": "Cannon uses a dedicated ranged deck, current damage quotes and independent preparation scoring; it does not inherit approach-card priority. Legal cards are considered individually before movement.", "evidence": [evidence("static int ChooseCannon", "Cannon chooser and legal AP fallback")]},
        {"id": "legal-movement", "result": "PASS_WITH_HEURISTIC_LIMITS", "detail": "Stay-put baseline; legal multi-step path committed once; up to six-step search; preparation uses planned endpoint, naval uses actual field preview. Heading and public ram risk reduce forward-lock mistakes.", "evidence": [evidence("var final=s.Battle.Stage==NavalBattleStage.Preparation?p:f.FinalPositionMeters", "Correct preparation endpoint"), evidence("static double HeadingScore", "Cannon heading safety"), evidence("static double RamRisk", "Visible threat and enemy bow geometry")]},
        {"id": "real-boarding-transition", "result": "PASS", "detail": "Actual CanBoard/TryBoard or legal arrival contact on the same battle; preceding hull/supply loss and production field/health transfer retained. No battle-time HP/resource/state grants found.", "evidence": [evidence("if(s.CanBoard(out _))", "Normal boarding preferred before ram"), evidence("s.TryNavalArrivalContact(true", "Arrival contact before ordinary heading confirmation"), evidence("static void Entry", "Entry HP and supplies captured")]},
        {"id": "finite-policy-reporting", "result": "PASS_WITH_DECLARED_LIMITS", "detail": "Dead, sunk, captured and forced boarding are distinct; forced boarding is not death. Turns are ended windows, finite heuristics are not optimal play, cross-tier pressure is separate."},
        {"id": "ten-versus-twelve", "result": "PASS_SENSITIVITY_CHECKED", "detail": "Sixteen independent same-seed starter cases only change the naval deck. The ten-card set produces 6/8 and 8/8 sunk versus the final twelve-card set 8/8 and 8/8; smaller deck is not automatically better."},
    ]
    resolved = [
        {"id": "preparation-preview", "status": "FIXED_IN_FINAL07", "detail": "Full06 mistakenly scored preparation paths from a field preview that returned the original position. Final07 uses the legal path endpoint; starter reef seeds 1/3/7 change from death to sunk with 4/15/11 hull."},
        {"id": "ap-four-forced-boarding", "status": "DIAGNOSED_AND_PREVENTED", "detail": "RamDiagnostic07 showed the old gunship moon boss seed6 had only an enemy-occupied legal first step; four AP did not provide immediate escape. Final07 preparation/heading changes prevent that earlier lock and finish sunk with 238 hull."},
        {"id": "over-capacity", "status": "FIXED", "detail": "Supplies scaled by cohort to 24/48/80 powder and balls plus 8/16/24 wood/medicine, with exact capacity checks."},
        {"id": "unspendable-budget", "status": "FIXED", "detail": "Development 5040/3600 claims were stopped by assertions; final mature budget is 2400, mid 720, starter 0, each actually paid."},
    ]
    limitations = [
        "Constructed test fixtures before battle, not proof of new-game acquisition or voyage cost. Private StartBattle and deterministic battle RNG reset are setup seams only.",
        "Neutral environmental component context and full initial hull only. Same battle seed is an environment control; different decks do not have identical draw sequences.",
        "One ordinary enemy per cohort and 13 bosses. Stage-appropriate comparisons and cross-tier pressure must remain separate.",
        "Bounded six-step greedy movement; field endpoint penalties approximate delayed hazards. Legal action execution is exact but policy quality is not proven optimal.",
        "Naval and boarding card heuristics differ in legitimate objectives and are not equal-strength optimal solvers; cannon has ordinary and tide variants, not an exhaustive school search.",
        "Turn counters count EndBattleTurn calls, not final partial action windows. TrainingBoardingCarryover is zero outside training and must not be interpreted as no loss transfer.",
        "Entry snapshot may include enemy first strike in the same transition action. Resource costs are actual card deltas at a fixed common replacement price, not a voyage profit ledger.",
        "No retreat-for-loss-minimization policy. Forced boarding means failure to achieve pure sinking at that point, not player death.",
    ]
    summary = {"result": "PASS_FOR_STATED_FINITE_POLICY_COMPARISON", "blockingFindings": [],
               "adoptedRun": "Final07", "sampleCount": 1120, "sensitivityRun": "SensitivityStarter", "sensitivitySamples": 16,
               "initialAndHashReview": "policy-data-review-Final07.json", "sensitivityReview": "policy-sensitivity-review.json",
               "checks": checks, "resolvedFindings": resolved, "limitations": limitations,
               "excludedDevelopmentRuns": ["Pilot01", "Pilot02", "Pilot03", "Mature04", "Extended05", "Full06"],
               "productionFilesChangedByReviewer": False, "unityOrCombatRunsStartedByReviewer": False,
               "reviewerCleanup": "Only Python read/validation and Markdown/JSON evidence; no compiled or Unity cache created.",
               "finalProgramSHA256": sha(RUN / "Inputs/Program.cs"), "finalSampleSHA256": sha(RUN / "samples.json")}
    (OUT / "policy-review.json").write_text(json.dumps(summary, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    text = f"""# 海战／接舷配对探针策略审查

**最终结论：Final07 可用于所述具体构筑和有限策略的配对比较，未发现尚未解决的阻断项。不能据此证明最优打法或全体玩家胜率。** 主矩阵采用 Final07 的 1120 场，另列 SensitivityStarter 的 16 场构筑敏感性检查；旧开发批次不进入最终结论。

## 独立核验

336 个配对组逐项比对通过：船、装配与来源牌、船员、等级、初始生命、物资和实际强化花费相同。模式间仅允许牌组与同预算强化分配不同；所有货舱容量合规。强化使用真实 TryUpgradeBattleCard，组牌使用 SetBattleDeck 校验；预算为初始 0、中期 720、成熟 2400。

156 个冻结生产文件、156 个当前生产对应文件、5 个工具与目录输入哈希全部吻合。详情见 [初态和哈希核验](D:/Game/outputs/battle-mode-difficulty-20261027/policy-data-review-Final07.json)。Final07 Program SHA256：`{summary['finalProgramSHA256']}`。

553 次转接舷保留了实际入场状态：己方 548 次等于生产继承公式，5 次因记录位于完整转换动作之后而更低；敌方 553 次均等于公式，没有高于公式的样本。未发现进入接舷后补满生命、补充物资或重设敌人。海战前置损耗与场地由同一真实会话保留。

## 策略合理性

- 炮战使用专用炮击／装填／防守／修理牌组与当前伤害报价；准备阶段单独评分。不会给赶路接舷机动固定优先级，逐张检查合法攻击，额外炮战构筑为潮系。
- 移动允许原地留守和安全 2–4 格；一次提交合法路径，使用海战场地预览的实际终点和伤害，准备阶段使用合法计划终点。停船朝向避免把下一次强制前进方向指向敌舰；公开冲撞意图进入风险评分。
- 接舷保留同一场海战过程，通过 TryBoard 或 TryNavalArrivalContact 进入；到达接触在普通停船确认前使用剩余 AP，并优先舷侧邻格和剩余接触预算。没有直接设置位置、阶段或接舷生命来完成登船。
- 出牌后按实际 cargo 减少记录耗材，记录结束回合时能量、AP、合法牌数和公开攻击意图。留有 AP 或可打但无收益的牌本身不等于浪费。

具体源码行证据见 [机器可读审查](D:/Game/outputs/battle-mode-difficulty-20261027/policy-review.json)。

## 已修正的偏差及结果影响

Full06 的准备路径评分误用了尚未启用的场地预览，导致准备阶段不移动。Final07 已改用准备路径终点，真实移动可见于 trace。轻帆船／裂桅 seed 1、3、7 从旧版死亡改为击沉，分别剩 4、15、11 船体；旧失败不能作为机制失衡反例。

旧重炮／月蚀 seed 6 的 4 AP 未动曾被列为风险。RamDiagnostic07 确认当时合法首步唯一是敌舰占格，已无即时避撞路线。修复准备移动和更早的停船朝向后，Final07 同例击沉，剩 238 船体。该旧 forced_boarding 结果也已排除。

初稿统一大补给超轻船货舱、成熟预算无法花满等问题均由断言拦截并修正。最终三阶段火药／炮弹为 24／48／80，木板／药品为 8／16／24。

## 12 张与 10 张构筑

12 张炮战和 10 张接舷均合法，张数不同本身不否定对照，但专精程度可能影响表现。独立补测预先固定 10 张炮战：齐射×2、链弹×2、回能×2、稳固×1、修理×1、装填×1、抽牌×1。16 例已逐项核对：除了海战牌组，初态、预算、准备组与种子完全相同，161 项冻结输入哈希通过。

10 张构筑对两个初阶首领为 6/8、8/8 击沉；修复后的 12 张基线均为 8/8。更薄牌组并未在这些样本中必然更强；这组补测单列，不混入主矩阵。见 [敏感性核验](D:/Game/outputs/battle-mode-difficulty-20261027/policy-sensitivity-review.json)。

## 使用边界

这是战斗前构造的固定校准初态，不是新档成长证明。中性环境加成、满初始生命、相同战斗种子都是受控条件；不同牌组并不拥有相同抽牌序列。适配敌人应与越级压力分列，普通敌人覆盖每档一个代表。

策略只做最多 6 步的有限路径搜索，场地后续风险使用启发式权重；它不穷尽全部流派和未来回合。当前未发现剩余明显漏动作足以阻断这些具体样本比较，但结论不得外推为最优玩家边界。

forced_boarding 不是死亡；中性接舷的意外击沉是战斗胜利但未达夺船目标。回合字段是已结束 EndTurn 次数，不含终结的未结束窗口。训练专用 carryover=0 不代表没有继承损伤；以真实入场生命与物资为准。单动作净失血可能被同动作治疗抵消，耗材报价仅是统一替换成本。本策略不以撤退止损为目标。

完整方法见 [paired-method.md](D:/Game/outputs/battle-mode-difficulty-20261027/paired-method.md)。本审查仅执行只读源码和数据核验，未启动 Unity 或战斗重跑、未修改生产文件，也未生成需要清理的编译或 Unity 缓存。
"""
    (OUT / "policy-review.md").write_text(text, encoding="utf-8")
    print(json.dumps({"result": summary["result"], "primarySamples": 1120, "sensitivitySamples": 16,
                      "sensitivityVerifiedFiles": verified, "blockingFindings": 0}, ensure_ascii=False))


if __name__ == "__main__":
    main()
