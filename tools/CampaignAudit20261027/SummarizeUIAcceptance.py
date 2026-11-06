"""Summarize retained native Unity UI evidence; never touches user save data."""
import csv
import hashlib
import json
from pathlib import Path
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "Artifacts/CampaignAudit20261027"
RUN = OUT / "UIRun01"


def read(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def link(label, path):
    return f"[{label}](<{path.as_posix()}>)"


def main():
    snapshot = read(OUT / "Snapshot/snapshot.json")
    review = read(RUN / "input-normalization-review.json")
    cleanup = read(OUT / "cleanup.json")
    completion = read(RUN / "completion.json")
    records = read(RUN / "Actions/campaign-ui-states.json")
    states = {r["stage"]: r for r in records}
    initial = states["new_game_before_comic"]
    final = states["tutorial_completed"]
    continued = states["continued_saved_profile"]
    result = ET.parse(RUN / "results.xml").getroot().attrib
    assert result["result"] == "Passed" and result["passed"] == "1" and result["failed"] == "0"
    assert review["ProductionMainInputsUnchanged"]
    assert review["NormalizedTextExactlyEqual"] and review["NoSemanticSettingsChanges"]
    assert review["AllOtherProjectInputsByteIdentical"]
    assert initial["gold"] == "500" and final["gold"] == continued["gold"] == "560"
    assert initial["ordinaryCrew"] == 10 and initial["specialCrew"] == 0
    assert not initial["components"] and not initial["keepsakes"]
    assert not final["tutorialActive"]
    manifest = OUT / "Snapshot/source-manifest.csv"
    assert digest(manifest) == snapshot["SourceManifestSHA256"]
    backup = OUT / "QASaveAfterTutorial"
    with (backup / "sha256.csv").open(encoding="utf-8-sig", newline="") as handle:
        saved = list(csv.DictReader(handle))
    for entry in saved:
        assert digest(Path(entry["Path"])) == entry["Hash"].lower(), entry["Path"]
    assert cleanup["Result"] == "PASS"
    for removed in cleanup["Removed"]:
        assert not Path(removed["Path"]).exists(), removed["Path"]
    pngs = sorted((RUN / "Visuals").glob("*.png"))
    summary = {
        "result": "PASS_WITH_NEWLINE_NORMALIZATION",
        "method": "Native Unity PlayMode integration using production scenes, controllers, actual button callbacks and legal tutorial actions.",
        "scope": "New profile, opening comic, complete guided tutorial, port, earned component equip and treasure display, save to menu and Continue.",
        "limitations": [
            "Windows computer-use backend was unavailable; this is automated native PlayMode coverage, not manual mouse play.",
            "Full six-legendary campaign progression is separate Core automation and is not claimed as UI coverage here.",
            "Formal user save before/after hashes were not collected; no claim of formal-save byte identity is made.",
            "Screenshots and accelerated test duration do not measure human gameplay duration.",
        ],
        "tests": {"passed": 1, "failed": 0, "exitCode": completion["ExitCode"],
                  "testSeconds": float(result["duration"]), "wallSeconds": completion["Seconds"], "screenshots": len(pngs)},
        "inputReview": review,
        "sourceManifestSHA256": snapshot["SourceManifestSHA256"],
        "projectInputManifestSHA256": snapshot["ProjectInputManifestSHA256"],
        "saveIsolation": {
            "company": snapshot["Company"], "product": snapshot["Product"],
            "persistentDataPath": initial["persistentDataPath"],
            "editorRegistry": r"HKEY_CURRENT_USER\Software\Unity\UnityEditor\ShatteredSailCampaignAudit\Campaign20261027NewGame",
            "formalSaveHashesCollected": False,
            "basis": "Approved distinct company/product identity, runtime identity assertions, actual logged persistentDataPath, refusal to overwrite an existing QA profile, and QA-only backup paths. No formal save contents were read for this review.",
            "qaBackup": backup.as_posix(), "qaBackupVerifiedFiles": len(saved), "originalQASaveRetained": True,
        },
        "states": records,
        "tutorialOutcomeDifference": {
            "uiOutcome": "sunk", "uiBattleReward": 100, "uiFinalProfileGold": 560,
            "headlessOutcome": "captured", "headlessBattleReward": 200,
            "headlessComparableProfileGoldWithoutOptionalSupplyPurchase": 645,
            "retainedIncrement": 85,
            "explanation": "Capture pays an extra 100 gold; normal SafeReturn keeps 85%. The older headless 643.5525 additionally spent 1.4475 on optional water/food. Both outcomes follow production rules.",
            "uiLedger": "500 + 60 first-voyage return - 105 navigator contract + 105 second-voyage net reward = 560",
        },
        "observedReloadBehavior": "Hull 40/54 at tutorial end becomes 54/54 after production Save/Continue; test does not restore hull. Account remains 560, so same-session reload does not grant another login payment.",
        "cleanup": cleanup,
        "standalonePlayerBuilt": False,
        "evidence": {
            "results": (RUN / "results.xml").as_posix(),
            "originalGuardResult": (RUN / "completion.json").as_posix(),
            "review": (RUN / "input-normalization-review.json").as_posix(),
            "states": (RUN / "Actions/campaign-ui-states.json").as_posix(),
            "visuals": (RUN / "Visuals/index.html").as_posix(),
        },
    }
    json_path = OUT / "ui-acceptance-summary.json"
    json_path.write_text(json.dumps(summary, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    md = f"""# 新档与教程 UI 验收记录

本次原生 Unity PlayMode 集成验收 **1/1 通过**，退出码 0，保留 {len(pngs)} 张实际渲染截图。覆盖真实新游戏入口、开场漫画、完整教学、归港、首胜藏品陈列、赠送部件装备、保存返回菜单和继续游戏。Windows 计算机操作后端不可用，因此这里记录的是调用真实 Controller／按钮回调的自动验证；全 6 传奇的 Core 自动游玩属于另一组数据，不计为本次 UI 覆盖。

## 版本与输入核验

验证复用了隔离工程 `D:/Game/tmp/shipyard-detail-clean-20261011`，包含本轮 Boss 最终源码及当时当前仓库离线收益实现。完整生产输入清单共 5780 项，源清单 SHA256 为 `{snapshot['SourceManifestSHA256']}`；冻结代码在 {link('FrozenCode', OUT / 'Snapshot/FrozenCode')}。审计运行后 5780 项主工程输入哈希全部不变。

隔离工程共 5782 项输入（生产输入加审计测试及 meta）。原始运行器的严格字节守卫报错保留在 {link('completion.json', RUN / 'completion.json')}，未覆盖为通过：Unity 将 `ProjectSettings.asset` 的 710 处 CRLF 转为 LF。精确复核确认换行归一化后文本完全相同，无任何语义设置变化，其余 5781 项输入逐字节一致。验收结论为 `PASS_WITH_NEWLINE_NORMALIZATION`，证据及前后原文件见 {link('换行复核', RUN / 'input-normalization-review.json')} 和 {link('InputReview', RUN / 'InputReview')}。

## 新档和教程结束状态

| 项目 | 新游戏初始化 | 教程完成归港 |
|---|---:|---:|
| 港口／船 | 断锚港／sloop | 断锚港／sloop |
| 账户金币／随船金币 | 500／60 | 560／0 |
| 水／食物 | 120／120 | 99／101 |
| 火药／炮弹／木板／药品 | 20／16／8／2 | 20／16／8／2 |
| 船体 | 55/55 | 40/54 |
| 普通／特殊船员 | 10／0 | 9／1 |
| 藏宝图碎片 | 0 | 1 |
| 基础准备／海战／接舷牌数 | 11／12／12 | 11／12／12 |
| 卡牌强化 | 无 | 无 |

真实招募 `crew_broken_anchor_navigator_lorne`；准备和海战各增加其 1 张附加牌。真实获得并装备 `bow_sawtooth_drift`，获得 `first_victory_compass` 并陈列在第 1 格；始终只拥有 sloop。完整三阶段卡 ID、部件实例及四个时间点状态见 {link('状态记录', RUN / 'Actions/campaign-ui-states.json')}。教学结束仍在断锚港。

新档采用生产默认值，没有预置流派卡组、组件、藏品或额外金币。教学中的 20 发炮弹属于生产教学规则临时借用，结束恢复为自有 16 发。驱动复用现役教学测试的真实动作流程，并检查无效／过期回调不改变状态；没有直接设置完成标记或发放奖励。

UI 教学最后击沉敌舰，战斗奖励 100；独立自动游玩采用夺船，战斗奖励 200。额外 100 经正常安全返航的 85% 保留后形成 **85 金币**差异。因此同条件 UI 结业账户 560、夺船路线为 645；旧自动路线的 643.5525 还含一次可选补水粮开销 1.4475。UI 账目为 `500 + 首航回收60 - 航海士契约105 + 第二航净奖励105 = 560`，不是 UI 奖励桥接遗漏。结算截图见 {link('教学结算', RUN / 'Visuals/55_Guided_Normal_Chart_Settlement.png')}。

实际保存并继续游戏后账户仍为 560，未重复发放同进程登录收益；船体从 40/54 变为 54/54 是生产加载路径行为，审计测试没有修船。测试用时 {float(result['duration']):.3f} 秒、Unity 运行 {completion['Seconds']:.3f} 秒仅为自动验证耗时，不代表玩家游玩时长。

## 存档隔离与保留

隔离身份为 `ShatteredSailCampaignAudit / Campaign20261027NewGame`，运行前后由测试断言身份，首次进入时断言该身份没有已有档，拒绝覆盖先前 QA 档。实际记录的 LocalLow 路径为 `{initial['persistentDataPath']}`；Editor PlayerPrefs 位于 `HKCU/Software/Unity/UnityEditor/ShatteredSailCampaignAudit/Campaign20261027NewGame`。这与正式游戏的 `ShatteredSail / Shattered Sail` 身份分开。

**未采集正式用户存档的运行前后哈希，不能据此宣称正式存档逐字节未变。** 本次保护依据是独立身份和实际运行路径、测试入口的身份及空档断言、仅针对 QA 路径的保存与备份操作；本轮复核没有读取正式存档内容。

QA 原档保留于上述独立身份，另有 {link('QA 档案备份', backup)}，包含该 QA 身份的注册表导出与 LocalLow 文件；其哈希清单 {len(saved)} 项均已复核。截图索引：{link('全部 UI 截图', RUN / 'Visuals/index.html')}；归港截图：{link('完成教学后的港口', RUN / 'Visuals/91_Free_Port.png')}。

## 清理

已归档并移除隔离工程中的临时审计测试与 meta，释放 **{int(cleanup['FreedBytes']):,} 字节**；Temp 和 obj 均不存在。未创建独立 Player 包或另一份大型 Library，主工程 Library 未改动。保留既有预热 Library，因 `tools/ReleaseBuild/DesktopRelease.ps1`、`tools/ReleaseBuild/RunTests.ps1` 和 `tools/GameplayUiRelease20261026/Release.ps1` 仍依赖该共享缓存。源码冻结、原验证工程独有输入、审计工具、日志、截图和 QA 档案均保留。详见 {link('cleanup.json', OUT / 'cleanup.json')}。

机器可读汇总：{link('ui-acceptance-summary.json', json_path)}；测试原始结果：{link('results.xml', RUN / 'results.xml')}。
"""
    (OUT / "UI验收总结.md").write_text(md, encoding="utf-8")
    print(json.dumps({"Result": summary["result"], "Screenshots": len(pngs), "QABackupVerifiedFiles": len(saved), "FreedBytes": cleanup["FreedBytes"], "Markdown": str(OUT / "UI验收总结.md")}, ensure_ascii=False))


if __name__ == "__main__":
    main()
