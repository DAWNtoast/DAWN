"""Independent, decimal-exact review of a campaign audit's cash and first kills."""
import argparse
import csv
import json
from decimal import Decimal
from pathlib import Path


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("run", type=Path)
    args = parser.parse_args()
    root = args.run.resolve()
    load = lambda p: json.loads(p.read_text(encoding="utf-8-sig"), parse_float=Decimal)
    campaign = load(root / "campaign.json")
    final = campaign["final"]
    initial = Decimal(str(final["initialCash"]))
    errors = []
    income = expense = loss = Decimal(0)
    with (root / "economy.csv").open(encoding="utf-8-sig", newline="") as file:
        rows = list(csv.DictReader(file))
    for row in rows:
        d = lambda key: Decimal(row.get(key) or "0")
        income += d("income")
        expense += d("expense")
        loss += d("failureLoss")
        expected = initial + income - expense - loss
        for key, value in (("totalCash", expected), ("totalIncome", income),
                           ("totalExpense", expense), ("totalLoss", loss)):
            if d(key) != value:
                errors.append(f"action {row['action']}: {key} {d(key)} != {value}")
        if d("netDelta") != d("income") - d("expense") - d("failureLoss"):
            errors.append(f"action {row['action']}: delta decomposition mismatch")
    for key, value in (("totalCash", initial + income - expense - loss),
                       ("totalIncome", income), ("totalExpense", expense), ("totalLoss", loss)):
        if Decimal(str(final[key])) != value:
            errors.append(f"final {key}: {final[key]} != {value}")
    battle_rows = list(csv.DictReader((root / "battles.csv").open(encoding="utf-8-sig", newline="")))
    snapshots = []
    for boss_id, kill in campaign["firstKills"].items():
        wins = [r for r in battle_rows if r["enemyId"] == boss_id and r["outcome"] == "win"]
        state = kill["firstKill"]
        snapshots.append(state)
        if not wins or int(wins[0]["trip"]) != state["trip"]:
            errors.append(f"{boss_id}: first-kill voyage does not match battle log")
        if state["ship"]["index"] not in state["ownedShips"]:
            errors.append(f"{boss_id}: unowned selected ship")
        for stage, deck in state["deck"].items():
            if len(deck) != 10:
                errors.append(f"{boss_id}: {stage} base deck has {len(deck)} cards")
    snapshots.append(final)
    for state in snapshots:
        if state["initialCash"] + state["totalIncome"] - state["totalExpense"] - state["totalLoss"] != state["totalCash"]:
            errors.append(f"snapshot action {state['action']}: cumulative cash mismatch")
        parts = state["accountGold"] + state["travelGold"] + state.get("pendingSettlementGold", state.get("settlementEscrow", 0))
        if parts != state["totalCash"]:
            errors.append(f"snapshot action {state['action']}: cash account components mismatch")
    result = {
        "status": "PASS" if not errors else "FAIL", "scope": "independent arithmetic and first-kill log cross-check; not a substitute for gameplay legality review",
        "run": str(root), "economyRows": len(rows), "battleRows": len(battle_rows),
        "firstKills": len(campaign["firstKills"]), "initialCash": str(initial),
        "totalIncome": str(income), "totalExpense": str(expense), "totalLoss": str(loss),
        "endingCash": str(initial + income - expense - loss), "errors": errors,
    }
    (root / "independent-ledger-review.json").write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps(result, ensure_ascii=False, indent=2))
    raise SystemExit(bool(errors))


if __name__ == "__main__":
    main()
