"""Write final release notes only after all sealed evidence passes validation.

Run after Seal and Cleanup. This script never opens or writes UnityClient,
launches Unity, changes gameplay inputs, or modifies archived evidence.
"""
from __future__ import annotations

import argparse
import csv
import hashlib
import html
import json
import os
import re
import sys
import xml.etree.ElementTree as ET
import zipfile
from collections import Counter
from datetime import datetime, timedelta, timezone
from pathlib import Path
from urllib.parse import quote


ROOT = Path(__file__).resolve().parents[2]
RUN_ROOT = ROOT / "Artifacts/GameplayAuditRelease20261028"
OUTPUT = ROOT / "outputs/release-audit-20261028"
COMPARISON = ROOT / "outputs/release-diff-20261028"
SMOKE_SOURCE = "Assets/_Project/Presentation/Runtime/ReleasePlayerSmokeCheck.cs"


def require(condition: bool, message: str) -> None:
    if not condition:
        raise ValueError(message)


def load_json(path: Path):
    with path.open(encoding="utf-8-sig") as stream:
        return json.load(stream)


def load_csv(path: Path) -> list[dict[str, str]]:
    with path.open(encoding="utf-8-sig", newline="") as stream:
        return list(csv.DictReader(stream))


def digest(path: Path) -> str:
    value = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(8 * 1024 * 1024), b""):
            value.update(block)
    return value.hexdigest().upper()


def same_hash(actual: str, expected: str, label: str) -> None:
    require(bool(re.fullmatch(r"[0-9a-fA-F]{64}", str(expected))), f"Invalid SHA256: {label}")
    require(actual.upper() == expected.upper(), f"SHA256 mismatch: {label}")


def integer(value, label: str) -> int:
    require(not isinstance(value, bool) and isinstance(value, (int, float)) and
            value >= 0 and int(value) == value, f"Invalid nonnegative integer: {label}")
    return int(value)


def listed(value) -> list:
    return [] if value is None else value if isinstance(value, list) else [value]


def text_fields(text: str) -> dict[str, str]:
    return dict(line.split(": ", 1) for line in text.splitlines() if ": " in line)


def check_recorded_path(value: str, expected: Path, label: str) -> None:
    require(Path(value).resolve() == expected.resolve(), f"Unexpected {label}: {value}")


def human_size(value: int) -> str:
    unit = "GiB" if value >= 1024 ** 3 else "MiB"
    divisor = 1024 ** (3 if unit == "GiB" else 2)
    return f"{value:,} 字节（{value / divisor:.2f} {unit}）"


def escaped(value) -> str:
    return html.escape(str(value), quote=True)


def link(path: Path, label: str, document: Path) -> str:
    relative = os.path.relpath(path, document.parent).replace("\\", "/")
    return f'<a href="{quote(relative, safe="/.-_")}">{escaped(label)}</a>'


def read_play02_history() -> dict:
    directory = RUN_ROOT / "Play02"
    results = directory / "results.xml"
    completion = load_json(directory / "completion.json")
    same_hash(digest(results), completion["ResultsSHA256"], "historical Play02 result XML")
    run = ET.parse(results).getroot()
    cases = list(run.iter("test-case"))
    counts = Counter(case.get("result") for case in cases)
    require(run.tag == "test-run" and set(counts) <= {"Passed", "Failed", "Skipped"},
            "Historical Play02 contains an unknown test state")
    for field in ("Passed", "Failed", "Skipped"):
        require(int(run.get(field.lower(), "-1")) == counts[field], "Historical Play02 count mismatch: " + field)
    require(int(run.get("total", "-1")) == len(cases) and counts["Failed"] > 0,
            "Historical Play02 failure evidence is missing")
    failures = listed(load_json(directory / "failures.json"))
    require(Counter(row["Name"] for row in failures) ==
            Counter(case.get("fullname") for case in cases if case.get("result") == "Failed"),
            "Historical Play02 failure names do not match the original result XML")
    explanations = {
        "CornerPiles_ShowLiveCountsAndOpenTheRequestedReadOnlyGroup":
            "旧测试把Button正常处理pointer-enter误判为存在悬停提示。修正为确认无BattleStatusHover、悬停后提示仍隐藏，并以真实pointerClick验证抽弃牌堆可点击及指定分组；保留滚动、只读和实时计数检查。",
        "BattleResponsive_SameStageEnemyTurnRetainsSceneAndRefreshesAllLiveHudValues":
            "旧测试仍要求燃烧长说明，未同步用户指定的简版。现严格核对2层时“燃烧\\n回合结束时，受到2点伤害。”；保留提示显示/隐藏、先扣格挡、层数3→2及完整HUD刷新检查。",
        "WorkshopPreview_SeparatesPaidBaseCardsFromVoyageSpecialCollection":
            "旧fixture最多航行30海里，按每40海里1经验只能积累不足1点。改为60海里预算并选择实际40≤距离<80的航向，验证真实距离与精确1经验；保留未结算回滚、结算持久化及重复提交幂等检查。",
        "DeckBuilderHeader_AllFourButtonsStayOnPaperAndFilterReadOnlySpecialCardsByStage":
            "新增流派筛选后，旧测试仍把跨流派特殊卡当作同一集合分页，默认无流派分类不足第二页。修正为逐阶段切换无流派及六流派筛选，核对集合、排序、空分类及翻页边界；保留顶栏几何、只读和存档检查。",
    }
    for row in failures:
        short_name = row["Name"].rsplit(".", 1)[-1]
        require(short_name in explanations, "Unreviewed historical Play02 failure: " + row["Name"])
        row["Explanation"] = explanations[short_name]
    skipped = []
    for case in cases:
        if case.get("result") != "Skipped":
            continue
        message = case.find("reason/message")
        reason = "" if message is None else "".join(message.itertext())
        require(bool(reason.strip()), "Historical Play02 skip reason is missing")
        skipped.append({"Name": case.get("fullname"), "Reason": reason})
    return {"Directory": directory, "Results": results, "Counts": counts, "Failures": failures, "Skipped": skipped}


def validate(revision: str) -> dict:
    snapshot_dir = RUN_ROOT / ("Snapshot" + revision)
    verification_dir = RUN_ROOT / ("Verification" + revision)
    release_dir = ROOT / ("Builds/第七港口-玩法审核20261028-r" + revision)
    manifest_path = release_dir / "release-manifest.json"
    snapshot = load_json(snapshot_dir / "snapshot.json")
    verification = load_json(verification_dir / "verification-summary.json")
    seal = load_json(manifest_path)
    cleanup = load_json(snapshot_dir / "cleanup.json")
    package = load_json(snapshot_dir / "package.json")
    images = load_json(snapshot_dir / "Windows/packaged-images.json")
    fixes = load_json(OUTPUT / "fixes.json")
    identity = load_json(OUTPUT / "final-main-source-identity.json")
    reviewed = load_json(COMPARISON / "reviewed-changes.json")
    comparison = load_json(COMPARISON / "comparison-summary.json")
    comparison_report = COMPARISON / "当前工程与上次出包差异.html"
    require(comparison_report.is_file() and reviewed.get("baseline") == comparison.get("releaseVersion") == "0.3.20261026.1",
            "The reviewed r03 comparison is missing or identifies another baseline")
    for label, record in (("snapshot", snapshot), ("verification", verification),
                          ("release seal", seal), ("cleanup", cleanup),
                          ("package", package), ("packaged images", images)):
        require(record.get("Result") == "PASS", f"{label} has not passed")
    require(cleanup.get("MainProjectUnchanged") is True, "Cleanup did not confirm the main project was unchanged")
    require(seal.get("SmokePassed") is True, "Sealed player smoke has not passed")
    require(fixes.get("gameplayBalanceChangedByThisAudit") is False,
            "The audit's no-gameplay-balance-change claim is not confirmed")
    version = seal["Version"]
    require(snapshot["Version"] == version, "Snapshot and release versions differ")
    check_recorded_path(seal["Evidence"], snapshot_dir, "release evidence path")
    check_recorded_path(seal["Validation"], verification_dir / "verification-summary.json", "validation path")

    source_hash = digest(snapshot_dir / "source-manifest.csv")
    project_hash = digest(snapshot_dir / "project-input-manifest.csv")
    for label, record in (("snapshot", snapshot), ("verification", verification), ("release seal", seal)):
        same_hash(source_hash, record["SourceManifestSHA256"], label + " source manifest")
        same_hash(project_hash, record["ProjectInputManifestSHA256"], label + " project manifest")
    source_rows = load_csv(snapshot_dir / "source-manifest.csv")
    source = {row["Path"].replace("\\", "/"): row["SHA256"] for row in source_rows}
    require(len(source) == len(source_rows) == integer(snapshot["Files"], "snapshot files"),
            "Source manifest has duplicate or inconsistent paths")
    require(identity.get("result") == "PASS" and identity.get("changedPaths") == [] and
            integer(identity["expectedFiles"], "identity expected files") == len(source) and
            integer(identity["currentFiles"], "identity current files") == len(source),
            "Final main-source identity was not confirmed")
    check_recorded_path(identity["sourceManifest"], snapshot_dir / "source-manifest.csv", "main identity source manifest")
    same_hash(source_hash, identity["sourceManifestSha256"], "final main-source identity")

    cases = load_csv(verification_dir / "verification-cases.csv")
    skipped = listed(load_json(verification_dir / "skipped-tests.json"))
    suites = listed(verification["Suites"])
    require(len(suites) == 2 and {item["Suite"] for item in suites} == {"EditMode", "PlayMode"},
            "Both complete test suites are required")
    require(all(row.get("Result") in ("Passed", "Skipped") for row in cases), "Unresolved test case in verification CSV")
    require(all(row.get("Suite") in ("EditMode", "PlayMode") for row in cases), "Unexpected test suite")
    require(all(row.get("Result") == "Skipped" and row.get("Reason", "").strip() for row in skipped),
            "Every skipped test requires its recorded reason")
    require(Counter((row["Suite"], row["Name"], row.get("Reason", "")) for row in skipped) ==
            Counter((row["Suite"], row["Name"], row.get("Reason", "")) for row in cases if row["Result"] == "Skipped"),
            "Skipped test evidence does not match verification cases")
    for suite in suites:
        name = suite["Suite"]
        counts = Counter(row["Result"] for row in cases if row["Suite"] == name)
        require(integer(suite["Passed"], name + " passed") > 0 and integer(suite["Failed"], name + " failed") == 0,
                name + " did not pass")
        require(counts["Passed"] == suite["Passed"] and counts["Skipped"] == suite["Skipped"] and
                sum(counts.values()) == suite["Total"], name + " counts do not match case evidence")
        same_hash(source_hash, suite["SourceManifestSHA256"], name + " source manifest")
        same_hash(project_hash, suite["ProjectInputManifestSHA256"], name + " project manifest")
        results_path = RUN_ROOT / (("Edit" if name == "EditMode" else "Play") + revision) / "results.xml"
        check_recorded_path(suite["Report"], results_path, name + " results")
        same_hash(digest(results_path), suite["ResultsSHA256"], name + " result XML")
        launch = load_json(results_path.parent / "launch.json")
        completion = load_json(results_path.parent / "completion.json")
        arguments = launch["Arguments"]
        lower_arguments = [str(value).lower() for value in arguments]
        require("-runtests" in lower_arguments and not
                {"-testfilter", "-testcategory", "-testnames", "-assemblynames"}.intersection(lower_arguments),
                name + " must be a complete suite; targeted diagnostics cannot qualify a release")
        for flag, expected in (("-projectpath", snapshot["Project"]), ("-testplatform", name)):
            require(flag in lower_arguments and lower_arguments.index(flag) + 1 < len(arguments),
                    name + " launch is missing " + flag)
            value = arguments[lower_arguments.index(flag) + 1]
            if flag == "-projectpath":
                check_recorded_path(value, Path(expected), name + " project")
            else:
                require(value == expected, name + " launch platform differs")
        require(completion.get("ExitCode") == 0, name + " test process did not succeed")
        for label, record in (("launch", launch), ("completion", completion)):
            require(record.get("InputValidationResult") == "PASS", name + " inputs were not checked at " + label)
            same_hash(source_hash, record["SourceManifestSHA256"], name + " " + label + " source manifest")
            same_hash(project_hash, record["ProjectInputManifestSHA256"], name + " " + label + " project manifest")
        same_hash(suite["ResultsSHA256"], completion["ResultsSHA256"], name + " completed results")
        run = ET.parse(results_path).getroot()
        require(run.tag == "test-run" and run.get("result") in ("Passed", "Skipped:Ignored") and
                int(run.get("failed", "-1")) == 0 and int(run.get("inconclusive", "-1")) == 0,
                name + " result XML contains an unresolved result")
        for field in ("Total", "Passed", "Failed", "Skipped"):
            require(int(run.get(field.lower(), "-1")) == suite[field], name + " result XML count mismatch: " + field)
        xml_cases = []
        for case in run.iter("test-case"):
            reason = case.find("reason/message")
            xml_cases.append((case.get("fullname"), case.get("result"), "" if reason is None else "".join(reason.itertext())))
        require(Counter(xml_cases) == Counter((row["Name"], row["Result"], row.get("Reason", ""))
                                             for row in cases if row["Suite"] == name),
                name + " verification cases differ from the sealed result XML")
    for field in ("Passed", "Failed", "Skipped"):
        require(integer(verification[field], field) == sum(integer(row[field], field) for row in suites),
                "Aggregate test count mismatch: " + field)

    smoke_dir = snapshot_dir / "Smoke"
    smoke_result = load_json(smoke_dir / "result.json")
    windows_result = load_json(snapshot_dir / "Windows/result.json")
    require(smoke_result.get("ExitCode") == 0 and windows_result.get("ExitCode") == 0,
            "Windows build or actual player smoke process failed")
    smoke = (smoke_dir / "Report/player-smoke-report.txt").read_text(encoding="utf-8-sig")
    smoke_fields = text_fields(smoke)
    require(smoke.splitlines()[0] == "PASS", "Player smoke report did not pass")
    require(smoke_fields.get("Version") == version and smoke_fields.get("Company") == "ShatteredSail" and
            smoke_fields.get("Product") == "Shattered Sail", "Player identity does not match the release")
    require(smoke_fields.get("Save changes", "").startswith("none."), "Smoke did not confirm read-only save handling")
    audio = load_csv(smoke_dir / "Report/packaged-audio.csv")
    music = [row["name"] for row in audio if row["group"] == "Music"]
    require(len(music) == 22 and len(set(music)) == 22 and music.count("the_captains_vow") == 1,
            "The corrected 22-track music smoke evidence is missing")
    require(all(row["frequency"] == "48000" and int(row["samples"]) > 0 for row in audio),
            "Invalid packaged audio metadata")
    require(all(row["channels"] == "2" for row in audio if row["group"] in ("Music", "Ambience")),
            "Packaged music or ambience is not stereo")

    exe = release_dir / "Windows/第七港口.exe"
    archive = release_dir / "第七港口.zip"
    check_recorded_path(seal["Executable"], exe, "executable")
    check_recorded_path(package["Zip"], archive, "package")
    require(seal.get("Package") == package, "Sealed package evidence differs from package.json")
    require(archive.stat().st_size == integer(package["ZipBytes"], "ZIP size"), "ZIP size changed")
    same_hash(digest(archive), package["ZipSHA256"], "ZIP")
    same_hash(digest(exe), package["ExeSHA256"], "executable")
    with zipfile.ZipFile(archive) as zipped:
        entries = [entry for entry in zipped.infolist() if not entry.is_dir()]
        require(len(entries) == integer(package["FilesVerified"], "ZIP files"), "ZIP entry count changed")
        require(sum(entry.file_size for entry in entries) == integer(package["UncompressedBytes"], "ZIP uncompressed bytes"),
                "ZIP uncompressed size changed")
    build = text_fields((release_dir / "Windows/build-report.txt").read_text(encoding="utf-8-sig"))
    require(build.get("Result") == "Succeeded" and build.get("Errors") == "0" and build.get("Version") == version,
            "Build report failed or identifies another version")
    require(not listed(images.get("Missing")) and integer(images["Images"], "resource images") > 0 and
            images["Images"] == seal["ImagesReferencedInBuildLog"], "Packaged resource image evidence is incomplete")
    image_import = (snapshot_dir / "image-import-report.txt").read_text(encoding="utf-8-sig")
    require(image_import.splitlines()[0] == "PASS" and text_fields(image_import).get("Failed") == "0",
            "Resource image import validation did not pass")

    changed = listed(fixes["changedFiles"])
    test_changes = [row for row in changed if row["path"].startswith("Assets/_Project/Tests/")]
    production = [row for row in changed if row not in test_changes]
    require(len(production) == 1 and production[0]["path"] == SMOKE_SOURCE,
            "Unexpected production changes: review the no-gameplay-change report wording")
    require(test_changes, "Test maintenance evidence is missing")
    for row in changed:
        path = row["path"].replace("\\", "/")
        require(path in source, "Fix is absent from the sealed snapshot: " + path)
        same_hash(source[path], row["afterSha256"], "final fix " + path)
        backup = OUTPUT / "before/UnityClient" / path
        patch = OUTPUT / "fix-diffs" / (path + ".diff")
        check_recorded_path(row["backup"], backup, "fix backup")
        check_recorded_path(row["diff"], patch, "fix diff")
        same_hash(digest(backup), row["beforeSha256"], "original fix " + path)
        require(patch.is_file(), "Fix diff evidence is missing: " + path)
    freed = integer(cleanup["FreedBytes"], "cleanup bytes")
    removed = listed(cleanup.get("RemovedDetails"))
    require(sum(integer(row["Bytes"], "removed bytes") for row in removed) == freed, "Cleanup byte total mismatch")
    retained = listed(cleanup.get("Retained"))
    require(retained and all(row.get("Path") and row.get("Reason") for row in retained),
            "Retained cache reasons are missing")
    reviewed_records = {row["title"]: row for row in reviewed["records"]}
    update_groups = (
        ("首领战斗", ("首领行为、铺场和选路", "首领冲锋与场地免疫", "首领数值重新校准")),
        ("咒印与防护", ("咒印与缠咒", "防护卡与水手不足")),
        ("成长与奖励", ("经验与流派开放", "夺船奖励")),
        ("藏品携带", ("藏品跨港携带",)),
        ("界面与物资", ("战斗资源栏与状态提示", "港口顶栏、工坊与物资图案")),
        ("接舷表现", ("接舷视觉与音效同步",)),
        ("音乐与教学", ("音乐、教学和界面精简",)),
    )
    major_updates = []
    for label, titles in update_groups:
        require(all(title in reviewed_records and reviewed_records[title].get("after") for title in titles),
                "A reviewed update summary is missing: " + label)
        summary = " ".join(reviewed_records[title]["after"] for title in titles)
        summary = summary.replace("完整13名首领数值见下表。", "完整13名首领数值见相对r03的差异报告。")
        major_updates.append(label + "：" + summary)
    play02_history = read_play02_history()
    return locals()


def render(data: dict, revision: str) -> tuple[str, str]:
    report = OUTPUT / "发布验收报告.html"
    version, suites, verification = data["version"], data["suites"], data["verification"]
    package, cleanup, fixes = data["package"], data["cleanup"], data["fixes"]
    snapshot_dir, verification_dir, release_dir = data["snapshot_dir"], data["verification_dir"], data["release_dir"]
    now = datetime.now(timezone(timedelta(hours=8))).strftime("%Y-%m-%d %H:%M:%S UTC+08:00")
    counts = f'通过 {int(verification["Passed"]):,}，失败 {int(verification["Failed"])}, 跳过 {int(verification["Skipped"])}'
    reasons = Counter((row["Suite"], row["Reason"]) for row in data["skipped"])
    initial = fixes.get("initialEditMode", {})
    initial_text = (f'首次 EditMode：通过 {integer(initial["passed"], "initial passed"):,}、'
                    f'失败 {integer(initial["failed"], "initial failed")}、'
                    f'跳过 {integer(initial["skippedRetired"], "initial skipped")}。') if initial else ""
    history = data["play02_history"]
    history_counts = history["Counts"]
    play02_text = (f'历史完整 Play02：通过 {history_counts["Passed"]:,}、失败 {history_counts["Failed"]}、'
                   f'跳过 {history_counts["Skipped"]}。跳过均为需显式启用的截图、性能或深度诊断项目，未计为通过。')
    maintenance = [
        "音频验收：原21首音乐的数量断言更新为包含共享主题曲的22首准确名单，同时验证数量与名称集合；保留48kHz、样本有效性和立体声检查。",
        f'测试维护共 {len(data["test_changes"])} 个文件：同步已确认的防护文案与时限、Boss最终数值、两行3+4资源栏；到港用例实际航行54海里并明确获得1经验，保留结算幂等检查。',
        "补充实际音频资源验收，以及缺主题曲、曲名错误、同数量重复替换、多曲的反例；没有通过放宽数量检查掩盖缺失资源。",
        "PlayMode测试维护同步用户燃烧简版提示、牌堆悬停与真实点击、40海里特殊卡经验规则以及工坊流派分页；保留实际效果、存档与只读边界检查。",
        "本轮审核未调整玩法平衡、卡牌能力、成长倍率或奖励倍率；生产改动仅为发布验收器修复。",
    ]
    suite_rows = "".join(f'<tr><td>{escaped(row["Suite"])}</td><td>{int(row["Passed"]):,}</td><td>{int(row["Failed"])}</td>'
                         f'<td>{int(row["Skipped"])}</td><td>{link(Path(row["Report"]), "完整结果 XML", report)}</td></tr>' for row in suites)
    reason_rows = "".join(f'<tr><td>{escaped(suite)}</td><td>{count}</td><td>{escaped(reason)}</td></tr>'
                          for (suite, reason), count in sorted(reasons.items())) or '<tr><td colspan="3">没有跳过项目。</td></tr>'
    skipped_rows = "".join(f'<tr><td>{escaped(row["Suite"])}</td><td>{escaped(row["Name"])}</td><td>{escaped(row["Reason"])}</td></tr>'
                           for row in data["skipped"])
    fix_rows = "".join(f'<tr><td>{escaped(row["path"])}</td><td>{link(Path(row["diff"]), "差异", report)} · '
                       f'{link(Path(row["backup"]), "修改前备份", report)}</td></tr>' for row in data["changed"])
    removed_rows = "".join(f'<tr><td>{escaped(row["Path"])}</td><td>{human_size(integer(row["Bytes"], "removed bytes"))}</td></tr>'
                           for row in data["removed"]) or '<tr><td colspan="2">本次没有需要删除的缓存。</td></tr>'
    retained_rows = "".join(f'<tr><td>{escaped(row["Path"])}</td><td>{escaped(row["Reason"])}</td></tr>' for row in data["retained"])
    history_rows = "".join(f'<tr><td>{escaped(row["Name"])}</td><td>{escaped(row["Explanation"])}</td>'
                           f'<td><pre>{escaped(row["Message"])}</pre></td></tr>' for row in history["Failures"])
    history_skip_rows = "".join(f'<tr><td>{escaped(row["Name"])}</td><td>{escaped(row["Reason"])}</td></tr>'
                                for row in history["Skipped"])
    evidence = [
        (data["manifest_path"], "发布封存清单"), (snapshot_dir / "snapshot.json", "输入快照"),
        (snapshot_dir / "source-manifest.csv", "正式源码 SHA256 清单"),
        (verification_dir / "verification-summary.json", "完整测试汇总"),
        (verification_dir / "skipped-tests.json", "跳过项目与理由"),
        (snapshot_dir / "Smoke/Report/player-smoke-report.txt", "实际成品 smoke"),
        (snapshot_dir / "Smoke/Report/packaged-audio.csv", "成品音频清单"),
        (snapshot_dir / "Windows/packaged-images.json", "资源图片入包检查"),
        (snapshot_dir / "package.json", "ZIP逐文件核验"), (snapshot_dir / "cleanup.json", "缓存清理记录"),
        (OUTPUT / "fixes.json", "修复清单"), (RUN_ROOT / "Edit01/failures.json", "首次失败明细（保留）"),
        (history["Directory"] / "failures.json", "Play02失败明细（保留）"),
        (history["Results"], "Play02完整原始结果（含失败与跳过）"),
        (OUTPUT / "final-main-source-identity.json", "主工程与最终封存输入一致性"),
        (data["comparison_report"], "相比上次20261026-r03的完整差异报告"),
        (COMPARISON / "reviewed-changes.json", "已复核的更新说明与源码定位"),
    ]
    document = f'''<!doctype html><html lang="zh-CN"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>第七港口 {escaped(version)} · 发布验收报告</title><style>
:root{{color-scheme:light}}*{{box-sizing:border-box}}body{{margin:0;background:#eef2f3;color:#21313d;font:16px/1.75 "Microsoft YaHei",sans-serif}}main{{max-width:1180px;margin:32px auto;padding:0 24px 48px}}header,section{{background:white;border:1px solid #dbe3e7;border-radius:12px;padding:24px 28px;margin:18px 0}}h1{{font-size:30px;line-height:1.4;margin:8px 0}}h2{{font-size:23px;margin:0 0 16px}}h3{{font-size:18px}}.pass{{color:#16734b;font-weight:700}}.muted{{color:#60737f}}.metrics{{display:flex;gap:16px;flex-wrap:wrap}}.metric{{padding:12px 18px;background:#edf6f1;border-radius:8px;min-width:170px}}.metric b{{display:block;font-size:25px}}a{{color:#126296;text-decoration:none}}a:hover{{text-decoration:underline}}table{{border-collapse:collapse;width:100%;font-size:14px}}th,td{{padding:10px 12px;border-bottom:1px solid #dde5e9;text-align:left;vertical-align:top;overflow-wrap:anywhere}}th{{background:#f1f5f7}}.scroll{{overflow-x:auto}}.hash{{font-family:Consolas,monospace;overflow-wrap:anywhere;font-size:13px}}pre{{white-space:pre-wrap;overflow-wrap:anywhere;font-size:13px;background:#f2f6f8;padding:16px;border-radius:6px}}li{{margin:8px 0}}summary{{cursor:pointer;font-weight:700}}@media print{{body{{background:white}}main{{margin:0;max-width:none}}header,section{{break-inside:avoid;border:0;padding:12px 0}}a{{color:inherit}}}}@media(max-width:600px){{main{{padding:0 12px}}header,section{{padding:18px 14px}}h1{{font-size:24px}}}}
</style></head><body><main>
<header><div class="pass">PASS · 已封存且已完成清理验收</div><h1>第七港口 {escaped(version)}</h1><p>玩法审核发布 · r{escaped(revision)} · {escaped(data["seal"]["Platform"])}</p><p class="muted">报告生成：{escaped(now)}。当前输入的完整测试、实际成品 smoke、ZIP及清理证据均已核对；生成报告时重新验证ZIP与EXE哈希。</p>
<div class="metrics"><div class="metric"><b>{int(verification["Passed"]):,}</b>测试通过</div><div class="metric"><b>{int(verification["Failed"])}</b>测试失败</div><div class="metric"><b>{int(verification["Skipped"])}</b>逐项记录的跳过</div><div class="metric"><b>{int(package["FilesVerified"])}</b>ZIP文件已核验</div></div>
<p>{link(data["exe"], "启动游戏 EXE", report)} · {link(data["archive"], "完整 Windows ZIP", report)} · {link(release_dir / "发布说明.txt", "发布说明", report)}</p></header>
<section><h2>相比上次 20261026-r03 的主要更新</h2><p>比较基准：{escaped(data["reviewed"]["baseline"])}。以下摘自已逐项核对源码的更新记录；{link(data["comparison_report"], "完整差异、首领数值及源码证据", report)} 保留了本轮审核前的比较快照。</p><ul>{''.join('<li>' + escaped(item) + '</li>' for item in data["major_updates"])}</ul><p>这一节说明新包相对上次发布的功能变化。下节的 {len(data["changed"])} 个文件是随后本轮审核修复的范围，不能用来代表整个新包的改动量；旧差异报告中的“未运行测试或出包”仅描述当时的比较阶段，最终验收以本报告为准。</p></section>
<section><h2>审核修复范围</h2><ul>{''.join('<li>' + escaped(item) + '</li>' for item in maintenance)}</ul><p>{escaped(initial_text)}历史失败日志与输入快照保留；最终结果来自当前封存输入的重新完整运行。</p>
<details><summary>逐文件修复、差异与修改前备份</summary><div class="scroll"><table><tr><th>文件</th><th>证据</th></tr>{fix_rows}</table></div></details></section>
<section><h2>历史失败与修复依据</h2><p>{escaped(play02_text)} {link(history["Directory"] / "failures.json", "失败明细", report)} · {link(history["Results"], "当次完整结果 XML", report)}</p><table><tr><th>失败用例</th><th>核实原因与修复</th><th>原始失败消息</th></tr>{history_rows}</table><details><summary>当次跳过项目与实际理由</summary><table><tr><th>用例</th><th>理由</th></tr>{history_skip_rows}</table></details><p>上述失败属于过时测试预期或不足量的测试输入。定向用例运行用于诊断；最终发布仍要求同一封存输入重新完成全部EditMode与PlayMode，定向结果不替代完整套件。</p></section>
<section><h2>完整回归验收</h2><p>{escaped(counts)}。跳过项目未计为通过；退役预览未恢复。已核对完整套件启动参数，不接受带筛选条件的定向诊断作为发布验收。</p><table><tr><th>套件</th><th>通过</th><th>失败</th><th>跳过</th><th>证据</th></tr>{suite_rows}</table><h3>跳过原因汇总</h3><table><tr><th>套件</th><th>数量</th><th>实际记录的理由</th></tr>{reason_rows}</table>
<details><summary>展开全部跳过项目</summary><table><tr><th>套件</th><th>用例</th><th>理由</th></tr>{skipped_rows}</table></details></section>
<section><h2>实际成品、资源与存档保护</h2><p>正式 Windows player smoke 退出码为0；产品与公司身份沿用 Shattered Sail / ShatteredSail。测试工程使用独立 Validation 身份，正式成品 smoke 使用显式只读入口，不执行新游戏、继续、设置保存或玩家档案恢复。</p><p>资源图片入包检查：<b>{int(data["images"]["Images"]):,}</b> 张；音频验收：<b>{len(data["audio"])}</b> 条，其中音乐 <b>{len(data["music"])}</b> 首。下方为真实成品报告，界面 smoke 不代表人工全流程游玩。</p><pre>{escaped(data["smoke"])}</pre></section>
<section><h2>包完整性</h2><p>主工程与最终封存输入一致性记录：{int(data["identity"]["currentFiles"]):,} 个文件完全一致；检查时间 {escaped(data["identity"]["checkedUtc"])}。该记录对应本次正式输入清单。</p><table><tr><th>项目</th><th>核验结果</th></tr><tr><td>ZIP文件数</td><td>{int(package["FilesVerified"])}，发布脚本已逐条回读并对比磁盘SHA256</td></tr><tr><td>压缩大小</td><td>{human_size(int(package["ZipBytes"]))}</td></tr><tr><td>解压文件总大小</td><td>{human_size(int(package["UncompressedBytes"]))}</td></tr><tr><td>ZIP SHA256</td><td class="hash">{escaped(package["ZipSHA256"])}</td></tr><tr><td>EXE SHA256</td><td class="hash">{escaped(package["ExeSHA256"])}</td></tr><tr><td>正式输入清单 SHA256</td><td class="hash">{data["source_hash"]}</td></tr><tr><td>验证工程输入清单 SHA256</td><td class="hash">{data["project_hash"]}</td></tr></table></section>
<section><h2>缓存清理与保留</h2><p>本次安全回收 <b>{human_size(data["freed"])}</b>。清理记录确认主工程未受改动，源码、输入、失败诊断、最终日志、EXE与ZIP保留。</p><table><tr><th>已删除的明确缓存路径</th><th>释放空间</th></tr>{removed_rows}</table><h3>仍保留的共用缓存</h3><p>共用预热工程仍供现役发布与测试脚本复用，保留编辑器导入及编译依赖，避免破坏后续验证；未整体清空 Library 或 Bee。</p><table><tr><th>路径</th><th>清理记录中的保留理由</th></tr>{retained_rows}</table></section>
<section><h2>验收证据</h2><ul>{''.join('<li>' + link(path, label, report) + '</li>' for path, label in evidence)}</ul></section>
</main></body></html>'''
    lines = [f"第七港口 {version} — 玩法审核发布", "", f"发布状态：PASS；修订 r{revision}",
             f"平台：{data['seal']['Platform']}", f"报告生成：{now}", "", f"相比上次 20261026-r03（{data['reviewed']['baseline']}）的主要更新："]
    lines.extend("- " + item for item in data["major_updates"])
    lines += ["完整差异报告：" + str(data["comparison_report"]),
              f"上述为版本功能变化；下列 {len(data['changed'])} 个文件仅为随后本轮审核修复，并非整个新包改动量。旧差异报告描述审核前快照，最终测试与出包结果以本说明为准。", "", "本轮审核修复："]
    lines.extend("- " + item for item in maintenance)
    lines += ["", "历史失败与修复依据：", play02_text]
    lines.extend("- " + row["Name"] + "：" + row["Explanation"] for row in history["Failures"])
    lines += ["Play02失败明细：" + str(history["Directory"] / "failures.json"),
              "Play02完整结果：" + str(history["Results"]), "Play02跳过理由："]
    lines.extend("- " + row["Name"] + "：" + row["Reason"] for row in history["Skipped"])
    lines.append("定向用例仅用于诊断，不能替代同一封存输入的完整EditMode与PlayMode验收。")
    lines += ["", "完整回归：" + counts]
    lines.extend(f"{row['Suite']}：通过 {int(row['Passed'])}，失败 {int(row['Failed'])}，跳过 {int(row['Skipped'])}。" for row in suites)
    lines += ["", "跳过原因（未计为通过）："]
    lines.extend(f"- {suite} / {count} 项：{reason}" for (suite, reason), count in sorted(reasons.items()))
    if not reasons:
        lines.append("无。")
    lines += ["", initial_text + "历史失败明细及输入保留，不以最终成功覆盖旧记录。", "", "实际成品 smoke：", data["smoke"].strip(), "",
              f"资源图片入包：{int(data['images']['Images'])} 张；成品音频：{len(data['audio'])} 条（音乐 {len(data['music'])} 首）。",
              f"ZIP逐文件验证：{int(package['FilesVerified'])} 项。", f"ZIP：{data['archive']}",
              f"ZIP大小：{human_size(int(package['ZipBytes']))}", f"解压大小：{human_size(int(package['UncompressedBytes']))}",
              f"ZIP SHA256：{package['ZipSHA256']}", f"EXE：{data['exe']}", f"EXE SHA256：{package['ExeSHA256']}",
              f"正式输入清单 SHA256：{data['source_hash']}", f"验证工程输入清单 SHA256：{data['project_hash']}", "",
              f"主工程与最终封存输入：{int(data['identity']['currentFiles'])} 个文件完全一致；检查时间 {data['identity']['checkedUtc']}。",
              f"缓存清理：释放 {human_size(data['freed'])}；主工程未受清理改动。"]
    lines.extend(f"- 已删除：{row['Path']} / {human_size(int(row['Bytes']))}" for row in data["removed"])
    lines.append("共用预热缓存及其他保留项：")
    lines.extend(f"- {row['Path']}：{row['Reason']}" for row in data["retained"])
    lines += ["", "完整HTML报告：" + str(report), "验收证据："]
    lines.extend(f"- {label}：{path}" for path, label in evidence)
    return document, "\n".join(lines) + "\n"


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--revision", default="03", help="Two-digit sealed release revision (default: 03)")
    args = parser.parse_args()
    require(bool(re.fullmatch(r"[0-9]{2}", args.revision)), "Revision must contain exactly two digits")
    data = validate(args.revision)
    document, notes = render(data, args.revision)
    report = OUTPUT / "发布验收报告.html"
    notes_path = data["release_dir"] / "发布说明.txt"
    # Validation and rendering finish before the first output write. No input,
    # archive or Unity project is touched by report generation.
    report.write_text(document, encoding="utf-8")
    notes_path.write_text(notes, encoding="utf-8-sig")
    print(json.dumps({"Result": "PASS", "Version": data["version"], "Revision": args.revision,
                      "Report": str(report), "ReleaseNotes": str(notes_path)}, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (ValueError, KeyError, TypeError, OSError, json.JSONDecodeError, zipfile.BadZipFile, ET.ParseError) as error:
        print("Release report not written: " + str(error), file=sys.stderr)
        sys.exit(1)
