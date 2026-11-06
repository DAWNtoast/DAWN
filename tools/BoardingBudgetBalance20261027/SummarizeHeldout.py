"""Read completed, frozen holdout runs. Never selects a policy or edits inputs."""
import argparse
import collections
import csv
import hashlib
import json
import math
import statistics
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument("runs", nargs="+")
parser.add_argument("--preflight", default="heldout-preflight-freeze.json")
parser.add_argument("--output", default="heldout-outcomes.json")
args = parser.parse_args()
root = Path(__file__).resolve().parents[2]
out = root / "outputs/boarding-budget-balance-20261027"
preflight = json.loads((out / args.preflight).read_text(encoding="utf-8"))
seed_start = preflight["seedStart"]
summary, checks = [], []


def wilson(k, n):
    if not n:
        return None
    z = 1.96
    denominator = 1 + z * z / n
    center = (k / n + z * z / (2 * n)) / denominator
    margin = z * math.sqrt(k / n * (1 - k / n) / n + z * z / (4 * n * n)) / denominator
    return [max(0, center - margin), min(1, center + margin)]


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


for tag in args.runs:
    run = out / tag
    parameters = json.loads((run / "run-parameters.json").read_text(encoding="utf-8-sig"))
    cleanup = json.loads((run / "cleanup.json").read_text(encoding="utf-8-sig"))
    assert parameters["SeedStart"] == seed_start and parameters["Partition"] == "held-out-validation"
    assert cleanup["ExitCode"] == 0 and not cleanup["BuildRemaining"]
    manifest = {
        Path(row["Snapshot"]).relative_to(run / "SourceSnapshot").as_posix(): row["SHA256"].lower()
        for row in csv.DictReader((run / "source-hashes.csv").open(encoding="utf-8-sig"))
    }
    assert manifest == preflight["source"], tag + " source differs"
    assert {name: sha(run / "Inputs" / name) for name in preflight["inputs"]} == preflight["inputs"]
    assert {name: sha(run / "Resources/Data" / name) for name in preflight["data"]} == preflight["data"]
    rows = json.loads((run / "samples.json").read_text(encoding="utf-8-sig"))
    groups = collections.defaultdict(list)
    for row in rows:
        assert seed_start <= row["seed"] < seed_start + parameters["Seeds"]
        groups[row["cohort"], row["boss"], row["mode"]].append(row)
    for (cohort, boss, mode), samples in groups.items():
        n = len(samples)
        assert n == parameters["Seeds"] and len({r["seed"] for r in samples}) == n
        entries = [r for r in samples if r["boardingEntryNavalTurn"] >= 0]
        captures = sum(r["outcome"] == "captured" for r in entries)
        in_band = [r for r in entries if r["boardingEntryInTargetBand"]]
        in_band_captures = sum(r["outcome"] == "captured" for r in in_band)
        achieved = sum(r["achievedMode"] for r in samples)
        summary.append({
            "run": tag, "boardingDeck": parameters["BoardingDeck"], "cohort": cohort,
            "boss": boss, "name": samples[0]["name"], "mode": mode, "n": n,
            "achieved": achieved, "fullRouteQualifiedRate": achieved / n,
            "fullRouteWilson95": wilson(achieved, n),
            "outcomes": dict(collections.Counter(r["outcome"] for r in samples)),
            "entries": len(entries), "entryCaptures": captures,
            "boardingContinuationEvaluated": not mode.startswith("cannon"),
            "afterEntryWinRate": captures / len(entries) if entries and not mode.startswith("cannon") else None,
            "afterEntryWilson95": wilson(captures, len(entries)) if not mode.startswith("cannon") else None,
            "inBandEntries": len(in_band), "inBandCaptures": in_band_captures,
            "outOfBandEntries": len(entries) - len(in_band),
            "outOfBandCaptures": captures - in_band_captures,
            "meanEntryCrewRatio": statistics.mean(r["boardingEntryPlayerCrewRatio"] for r in entries) if entries else None,
            "meanEntryEnemyHullRatio": statistics.mean(r["boardingEntryEnemyRatio"] for r in entries) if entries else None,
            "failureStages": dict(collections.Counter(r["failureStage"] for r in samples if not r["achievedMode"])),
            "failedSeeds": [r["seed"] for r in samples if not r["achievedMode"]],
            "entryFields": dict(collections.Counter(r["entryFieldKinds"] for r in entries)),
            "meanReplacementCost": statistics.mean(r["replacementCost"] for r in samples),
            "source": str(run / "samples.json"),
        })
    checks.append({"run": tag, "n": len(rows), "sourceFiles": len(manifest),
                   "sourceAndInputsMatchPreflight": True, "cleanup": cleanup})

result = {
    "status": "completed independent held-out evaluation; no retuning against these seeds",
    "finitePolicyCaveat": "These are constructed equal-incremental-budget fixtures, not campaign acquisition or an optimal-play guarantee.",
    "entryRateCaveat": "After-entry success excludes failure before entry. Full-route qualified rate retains every sample and counts softening only when actual entry enemy hull is 50–60%.",
    "runs": checks, "groups": summary,
}
(out / args.output).write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
print("Verified samples", sum(c["n"] for c in checks))
for r in summary:
    print(r["boardingDeck"], r["name"], r["mode"], f'{r["achieved"]}/{r["n"]}',
          "entry", f'{r["entryCaptures"]}/{r["entries"]}', r["outcomes"])
