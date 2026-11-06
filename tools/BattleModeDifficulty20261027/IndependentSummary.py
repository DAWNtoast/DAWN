"""Independent grouping and paired-initial-state review of battle-mode samples."""
import argparse
import csv
import json
import statistics
from collections import Counter, defaultdict
from pathlib import Path


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("run", type=Path)
    args = parser.parse_args()
    root = args.run.resolve()
    rows = json.loads((root / "samples.json").read_text(encoding="utf-8-sig"))
    groups, pairs = defaultdict(list), defaultdict(list)
    for r in rows:
        groups[(r["cohort"], r["boss"], r["mode"])].append(r)
        pairs[(r["cohort"], r["boss"], r["seed"])].append(r)
    errors = []
    excluded = {"mode", "preparation", "naval", "boarding", "upgrades"}
    for key, members in pairs.items():
        signatures = []
        for m in members:
            initial = {k: v for k, v in m["initial"].items() if k not in excluded}
            signatures.append(json.dumps(initial, sort_keys=True, ensure_ascii=False))
        if len(set(signatures)) != 1:
            errors.append({"pair": key, "problem": "physical initial state or budget differs across modes"})
    summaries = []
    for (cohort, boss, mode), members in groups.items():
        mean = lambda key: round(statistics.mean(r[key] for r in members), 4)
        summaries.append({
            "cohort": cohort, "boss": boss, "name": members[0]["name"], "mode": mode,
            "samples": len(members), "wins": sum(r["won"] for r in members),
            "achievedMode": sum(r["achievedMode"] for r in members),
            "outcomes": dict(Counter(r["outcome"] for r in members)),
            "meanPrepTurns": mean("prepTurns"), "meanNavalTurns": mean("navalTurns"),
            "meanBoardingTurns": mean("boardingTurns"),
            "meanMinHullFraction": round(statistics.mean(r["minimumHull"] / r["maxHull"] for r in members), 4),
            "meanMinCrewFraction": round(statistics.mean(r["minimumCrew"] / r["playerMaxCrew"] for r in members), 4),
            "meanReplacementCost": mean("replacementCost"),
            "meanPowder": mean("powder"), "meanBalls": mean("balls"), "meanWood": mean("wood"), "meanMedicine": mean("medicine"),
        })
    comparisons = []
    for (cohort, boss, seed), members in pairs.items():
        modes = {r["mode"]: r for r in members}
        for naval in ("cannon", "cannon-tide"):
            if naval not in modes:
                continue
            for board in ("boarding-neutral", "boarding-curse"):
                if board not in modes:
                    continue
                n, b = modes[naval], modes[board]
                comparisons.append({"cohort": cohort, "boss": boss, "seed": seed, "naval": naval, "boarding": board,
                                    "navalAchieved": n["achievedMode"], "boardingAchieved": b["achievedMode"],
                                    "navalOutcome": n["outcome"], "boardingOutcome": b["outcome"]})
    result = {"status": "PASS" if not errors else "FAIL", "samples": len(rows), "pairedGroups": len(pairs),
              "errors": errors, "groups": summaries, "pairs": comparisons,
              "limits": "Arithmetic and initial-state review; does not prove either policy is optimal or eliminate differences in card effects."}
    (root / "independent-summary.json").write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
    fields = [k for k in summaries[0] if k != "outcomes"] if summaries else []
    with (root / "independent-summary.csv").open("w", encoding="utf-8-sig", newline="") as f:
        writer = csv.DictWriter(f, fieldnames=fields, extrasaction="ignore")
        writer.writeheader()
        writer.writerows(summaries)
    print(json.dumps({"status": result["status"], "samples": len(rows), "pairedGroups": len(pairs), "errors": errors[:8]}, ensure_ascii=False))
    raise SystemExit(bool(errors))


if __name__ == "__main__":
    main()
