import csv
import hashlib
import json
from decimal import Decimal
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'outputs/campaign-audit-20261027'
RUNS = ('Campaign13', 'DirectPatrol14')

def read_json(path):
    return json.loads(path.read_text(encoding='utf-8-sig'), parse_float=Decimal)

def write_json(path, value):
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2, default=str) + '\n', encoding='utf-8')

def rows(path):
    return list(csv.DictReader(path.open(encoding='utf-8-sig', newline='')))

fields = ['run', 'bossId', 'bossName', 'firstKillVoyage', 'battleRewardGold', 'totalIncome', 'totalExpense',
          'failureLoss', 'settlementRetentionLoss', 'totalCash', 'accountGold', 'travelGold', 'pendingSettlementGold',
          'sailedNm', 'sailedHours', 'gameElapsedHours', 'automationElapsedSeconds', 'shipId', 'shipName',
          'ordinaryCrew', 'pirateLevel', 'pirateXP', 'strategy', 'preparationTurns', 'navalTurns',
          'boardingTurns', 'boardingEntryAfterNavalTurns', 'startingMedicine', 'endingMedicine',
          'componentsJson', 'crewJson', 'baseDeckJson', 'bonusDeckJson', 'collectionsJson']
all_rows, comparisons = [], []
for name in RUNS:
    path = OUT / name
    data = read_json(path / 'campaign.json')
    result = []
    for boss_id, item in data['firstKills'].items():
        state = item['firstKill']
        row = dict(run=name, bossId=boss_id, bossName=item['name'], firstKillVoyage=state['trip'],
                   battleRewardGold=item['battleRewardGold'], automationElapsedSeconds=state['realElapsedSeconds'],
                   shipId=state['ship']['id'], shipName=state['ship']['name'], strategy=state['boardingStrategy'],
                   preparationTurns=item['stageTurns']['Preparation'], navalTurns=item['stageTurns']['Naval'],
                   boardingTurns=item['stageTurns']['Boarding'], boardingEntryAfterNavalTurns=item['boardingEntryAfterNavalTurns'],
                   startingMedicine=item['startingMedicine'], endingMedicine=item['endingMedicine'])
        for key in ['totalIncome', 'totalExpense', 'failureLoss', 'settlementRetentionLoss', 'totalCash', 'accountGold',
                    'travelGold', 'pendingSettlementGold', 'sailedNm', 'sailedHours', 'gameElapsedHours', 'ordinaryCrew', 'pirateLevel', 'pirateXP']:
            row[key] = state[key]
        for output_key, source_key in [('componentsJson', 'components'), ('crewJson', 'crew'), ('baseDeckJson', 'deck'),
                                       ('bonusDeckJson', 'bonusDeck'), ('collectionsJson', 'collection')]:
            row[output_key] = json.dumps(state[source_key], ensure_ascii=False, separators=(',', ':'), default=str)
        result.append(row)
    with (path / 'first-kills.csv').open('w', encoding='utf-8-sig', newline='') as stream:
        writer = csv.DictWriter(stream, fieldnames=fields)
        writer.writeheader(); writer.writerows(result)
    all_rows.extend(result)
    actions = rows(path / 'actions.csv')
    fights = rows(path / 'battles.csv')
    comparisons.append(dict(run=name, completed=data['completed'], totalVoyages=data['final']['trip'],
        sixLegendaryCompletion=data['primaryCompletion'], final=data['final'],
        shipPurchases=[{k: row[k] for k in ['trip', 'ship', 'port', 'accountGold', 'detail']} for row in actions if row['actionType']=='ShipPurchase'],
        battleCount=len(fights), wins=sum(row['outcome']=='win' for row in fights), losses=sum(row['outcome']=='loss' for row in fights),
        runMethod='Continuous new profile, zero offline income, legal actions and current source. Earlier policy failures were not restored into this run.'))
with (OUT / 'first-kills.csv').open('w', encoding='utf-8-sig', newline='') as stream:
    writer = csv.DictWriter(stream, fieldnames=fields)
    writer.writeheader(); writer.writerows(all_rows)
write_json(OUT / 'strategy-comparison.json', dict(runs=comparisons,
    interpretation='The direct patrol policy reaches the intermediate bosses earlier in this observed run. These are not paired random seeds: production chart/event/route-boss/loot GUID randomness was not overridden, so the full voyage difference cannot be attributed to the purchase policy alone.',
    timeMeaning='realElapsedSeconds is automated console wall-clock time, not a measured human playing time. Game hours and nautical miles come from legal sailing and events.',
    navalLimitation='These first victories enter boarding after 0–2 naval enemy turns. They demonstrate a campaign path, not balanced difficulty for all builds or a full test of the expanded naval boss cycles.'))

ui = read_json(ROOT / 'Artifacts/CampaignAudit20261027/UIRun01/Actions/campaign-ui-states.json')
ui_initial, ui_end = ui[0], next(x for x in ui if x['stage']=='tutorial_completed')
auto_actions = rows(OUT / RUNS[0] / 'actions.csv')
initial = auto_actions[0]
after = next(x for x in auto_actions if x['actionType']=='TutorialReward')
pairs = {'accountGold':'gold', 'travelGold':'travelGold', 'water':'water', 'food':'food', 'powder':'powder',
         'cannonballs':'cannonballs', 'wood':'wood', 'medicine':'medicine', 'hull':'hull', 'maxHull':'maxHull',
         'ordinaryCrew':'ordinaryCrew', 'specialCrew':'specialCrew'}
checks = {key: dict(automated=initial[key], actualUi=ui_initial[ui_key], equal=Decimal(initial[key])==Decimal(str(ui_initial[ui_key]))) for key,ui_key in pairs.items()}
write_json(OUT / 'tutorial-equivalence.json', dict(
    initialStateChecks=checks, allInitialChecksPass=all(x['equal'] for x in checks.values()),
    sharedInitialFacts='Sloop only; no fittings, specialists, collections or upgrades. The explicit empty special-card configuration removes standalone-Core profession defaults.',
    uiEvidence='Artifacts/CampaignAudit20261027/UIRun01/Actions/campaign-ui-states.json',
    automatedEvidence='outputs/campaign-audit-20261027/Campaign13/actions.csv + battles.csv + economy.csv',
    uiCompletedGold=ui_end['gold'], automatedCompletedGold=after['accountGold'],
    automaticTutorialBattle=next(x for x in rows(OUT / RUNS[0] / 'battles.csv') if x['enemyId']=='tutorial_boss_event'),
    legalDifference=dict(uiFinalBattle='Cannon-oriented free-play policy; inferred 100-gold sinking reward, consistent with final balance and actual UI battle driver.', automatedFinalBattle='Legal ram/boarding capture, 200-gold reward; no extra resource grants.',
        rewardDifference='200 - 100 = 100', safeReturnRetainedFraction='0.85', expectedBalanceDifference='85', observedBalanceDifference=str(Decimal(after['accountGold'])-Decimal(ui_end['gold'])),
        exactEquality=Decimal(after['accountGold'])==Decimal(ui_end['gold'])+Decimal(85)),
    sourceEvidence=['CoreBattleRewardBalance.GoldReward: captured reward is doubled after rounding',
                    'CoreVoyageSession.FinishVoyage: SafeReturn protects 85% of newly earned gold',
                    'CoreGuidedBossBattle.StartGuidedBossLesson / RestoreGuidedBossSupplies: exercise supplies are temporarily borrowed and restored',
                    'CoreGameplayGuidedTutorialController.ConfirmVoyageSettlement: confirmed victory grants bow_sawtooth_drift and first_victory_compass'],
    originalTrialDifference='Policy06–08 also bought 12 water + 11 food for 1.4475 before lesson two. This optional expense was removed in both completed runs: 560 + 85 - 1.4475 = 643.5525 in those trials.',
    finalHullDifference='The free-play tactics differ, so remaining hull and sailing provisions need not be identical. No final UI state was injected.'))

cleanup=[]
for path in sorted(OUT.iterdir()):
    if not path.is_dir() or not path.name.startswith(('Policy','Campaign','DirectPatrol')) or not (path/'cleanup.json').exists(): continue
    fact=read_json(path/'cleanup.json')
    binaries=[p.relative_to(ROOT).as_posix() for p in path.rglob('*') if p.is_file() and p.suffix.lower() in ('.exe','.dll','.pdb','.mdb')]
    cleanup.append(dict(run=path.name, releasedBytes=fact['ReleasedBytes'], buildRemaining=(path/'Build').exists(), remainingBinaries=binaries))
write_json(OUT/'console-cleanup-summary.json', dict(runs=cleanup, releasedBytes=sum(x['releasedBytes'] for x in cleanup),
    allBuildsRemoved=all(not x['buildRemaining'] for x in cleanup), allBinariesRemoved=all(not x['remainingBinaries'] for x in cleanup),
    retained='Source snapshots, input code/data, manifests, console logs, failed policy evidence, completed campaigns and audit results.',
    scope='Only this console runner Build directories. Shared Unity UI import cache and the main project were not cleaned by this agent.'))
write_json(OUT/'source-legality-review.json',dict(
    completedRuns=list(RUNS), result='PASS with stated scope limitations',
    compiledSource='Artifacts/CampaignAudit20261027/Snapshot/FrozenCode/Assets/_Project; each run retains exact compiled files and source-hashes.csv',
    componentData='Copied from the audited isolated checkout only after its SHA256 matched Snapshot/source-manifest.csv; no live main-project data fallback.',
    allowedAdapters=[
        'Initial new-profile date only uses RestoreGameClock; no profile, cargo, XP, enemy, win or deck-order restoration.',
        'Pure Presentation market/monthly quota, component catalog and inventory source are compiled alongside current Core and Content. UnityDataAdapter only reads the frozen component CSV and supplies unused art types; PlayerPrefs and save import are unavailable.',
        'Every ship purchase checks current port stock, real price, affordability and capacity, then registers ownership/location and applies the actual purchased-ship baseline fittings. Starter ship begins bare.',
        'Every specialist is hired in its current port at the exact catalog contract cost and selected to accompany the ship; ordinary crew selection is free in the actual UI.',
        'Equipment is either the confirmed tutorial reward or an actual queued boarding drop. Bonuses derive only from owned/equipped entries; only Rare-or-better fitted parts supply special cards.',
        'Navigation decisions refresh fitting context at the current sailing location. Encounter choice resolution refreshes it again at the arrived encounter position/time, matching the actual UI encounter dashboard before battle creation.',
        'All standard upgrades use TryUpgradeBattleCard and real payment. Decks use public SetBattleDeck unlock/size/copy validation; bonus XP comes from actual sailing.',
        'All encounters are physically reached with SetCourseTo/Sail; target selection filters IsChartVisible before reading encounter identity. No private StartBattle, teleport or victory injection.',
        'Battle policy reads the visible hand, public legal actions and intent. It never reads draw-pile order; movement is planned/committed through public APIs.',
        'Treasures are earned through actual tutorial and collection predicates. Offline income is exactly zero. Production tutorial temporary supplies and new-voyage full repair are rules behavior, not audit grants.',
        'Cash includes account, travel and settlement escrow. Internal transfers are excluded; settlement retention losses and failure losses are distinct from purchases.'],
    limitations=[
        'Console campaign mirrors the relevant front-end transactions; the actual Unity new-profile/tutorial path is separately evidenced and compared.',
        'The policy favors boarding and later switches after a real arcane loss to legal repeated curse-threshold reduction plus defense. It does not establish balanced full naval combat.',
        'Only owned source cards, random drops and public choices were used; no component-shopping or port-investment strategy was optimized.',
        'Random GUID-based charts/events/rosters/loot remain production behavior; all generated chart seeds and observed actions are logged, but this is not deterministic bit-for-bit replay.',
        'Automated wall time is not human active play time, and no game hours or console runtime were converted to offline income.',
        'Policy01–08 and Campaign09–10 are development/failed-policy evidence. Campaign11 and DirectPatrol12 are completed exploratory runs superseded by Campaign13 and DirectPatrol14 after adding the actual encounter-screen fitting-context refresh. They are preserved for methodological traceability, not final acceptance.'],
    productionFilesChanged=False,userSavesAccessed=False))
print('Wrote first-kill CSVs, comparison, tutorial equivalence, legality review and console cleanup summary.')
