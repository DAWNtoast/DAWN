"""Independent read-only checks of frozen inputs and paired initial conditions."""
import collections
import csv
import hashlib
import json
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "outputs/battle-mode-difficulty-20261027"


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    tag = sys.argv[1] if len(sys.argv) > 1 else "Full06"
    run = OUT / tag
    errors = []
    verified = {"sourceSnapshot": 0, "currentProduction": 0, "toolAndData": 0}
    for filename, key, path_key in [("source-hashes.csv", "sourceSnapshot", "Snapshot"),
                                    ("input-hashes.csv", "toolAndData", "Path")]:
        with (run / filename).open(encoding="utf-8-sig", newline="") as handle:
            for row in csv.DictReader(handle):
                expected = row.get("SHA256", row.get("Hash")).lower()
                path = Path(row[path_key])
                if sha(path) != expected:
                    errors.append(f"Hash changed: {path}")
                verified[key] += 1
                if "Source" in row:
                    if sha(Path(row["Source"])) != expected:
                        errors.append(f"Current production differs: {row['Source']}")
                    verified["currentProduction"] += 1
    rows = json.loads((run / "samples.json").read_text(encoding="utf-8-sig"))
    grouped = collections.defaultdict(list)
    mode_counts = collections.Counter()
    outcome_counts = collections.Counter()
    fixtures = {}
    carry = {"observations": 0, "playerExactlyFormula": 0, "playerBelowFormula": 0,
             "enemyExactlyFormula": 0, "enemyBelowFormula": 0, "aboveFormula": []}
    intentional = {"mode", "preparation", "naval", "boarding", "upgrades"}
    for row in rows:
        init = row["initial"]
        key = (row["cohort"], row["boss"], row["seed"])
        grouped[key].append(row)
        mode_counts[(row["cohort"], row["mode"])] += 1
        outcome_counts[(row["cohort"], row["mode"], row["outcome"])] += 1
        fixtures[(row["cohort"], row["mode"])] = init
        if init["budget"] != init["spent"] or row["spent"] != row["budget"]:
            errors.append(f"Budget mismatch: {key}/{row['mode']}")
        if init["cargoVolume"] > init["capacity"] + .001:
            errors.append(f"Over-capacity fixture: {key}/{row['mode']}")
        if row["outcome"] == "action_watchdog":
            errors.append(f"Action watchdog: {key}/{row['mode']}")
        if row["boardingEntryNavalTurn"] >= 0:
            carry["observations"] += 1
            for side, hp_key, maximum, hull, start, max_hull in [
                ("player", "boardingEntryCrew", row["playerMaxCrew"], row["boardingEntryHull"], row["startPlayerHull"], row["maxHull"]),
                ("enemy", "boardingEntryEnemyCrew", row["enemyMaxCrew"], row["boardingEntryEnemyHull"], row["startEnemyHull"], row["enemyMaxHull"]),
            ]:
                loss = min(max_hull, max(0, start - max(0, hull)))
                den = 2 * max_hull
                expected = max(1, min(maximum, (maximum * (den - loss) + den - 1) // den))
                observed = row[hp_key]
                if observed == expected:
                    carry[side + "ExactlyFormula"] += 1
                elif observed < expected:
                    carry[side + "BelowFormula"] += 1
                else:
                    carry["aboveFormula"].append({"key": list(key), "mode": row["mode"], "side": side,
                                                 "expected": expected, "observed": observed})
    pair_count = 0
    for key, group in grouped.items():
        expected_modes = {"cannon", "boarding-neutral"} if key[0] == "starter" else {
            "cannon", "cannon-tide", "boarding-neutral", "boarding-curse"}
        actual = [r["mode"] for r in group]
        if set(actual) != expected_modes or len(actual) != len(expected_modes):
            errors.append(f"Incomplete/duplicate paired modes: {key}: {actual}")
        reference = {k: v for k, v in group[0]["initial"].items() if k not in intentional}
        for row in group[1:]:
            candidate = {k: v for k, v in row["initial"].items() if k not in intentional}
            if candidate != reference:
                errors.append(f"Initial conditions differ: {key}/{row['mode']}: " +
                              repr([k for k in reference if reference[k] != candidate.get(k)]))
            for field in ("initialHull", "maxHull", "playerMaxCrew", "enemyMaxHull", "enemyMaxCrew", "startPlayerHull", "startEnemyHull"):
                if row[field] != group[0][field]:
                    errors.append(f"Battle initial differs: {key}/{row['mode']}/{field}")
        pair_count += 1
    summary = {
        "result": "PASS_INITIAL_PAIRING_AND_HASHES" if not errors else "FAIL",
        "run": tag, "sampleCount": len(rows), "pairedGroups": pair_count,
        "verifiedFiles": verified, "errors": errors,
        "samplesSHA256": sha(run / "samples.json"),
        "sourceManifestSHA256": sha(run / "source-hashes.csv"),
        "inputManifestSHA256": sha(run / "input-hashes.csv"),
        "intentionalModeDifferences": sorted(intentional),
        "modeCounts": [{"cohort": k[0], "mode": k[1], "samples": v} for k, v in mode_counts.items()],
        "outcomes": [{"cohort": k[0], "mode": k[1], "outcome": k[2], "count": v} for k, v in outcome_counts.items()],
        "fixtures": list(fixtures.values()),
        "boardingEntryFormulaComparison": carry,
        "boardingFormulaCaveat": "Entry is sampled after the complete transition action. Enemy ram first strike or same-action post-entry effects can lower observed health below the carryover-only formula; above-formula observations require separate review.",
        "scope": "Checks recorded initial state equality, actual spend totals, carrying capacity and frozen-byte identity. It does not certify optimal tactics or inspect user saves.",
    }
    path = OUT / f"policy-data-review-{tag}.json"
    path.write_text(json.dumps(summary, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({k: summary[k] for k in ("result", "sampleCount", "pairedGroups", "verifiedFiles", "errors", "boardingEntryFormulaComparison")}, ensure_ascii=False))


if __name__ == "__main__":
    main()
