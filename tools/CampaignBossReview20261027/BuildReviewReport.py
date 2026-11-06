"""Build traceable campaign tables from recorded legal play, without simulation."""
import argparse
import collections
import csv
import html
import json
import re
from decimal import Decimal
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'outputs/campaign-boss-review-20261027'
TARGETS = {
    'boss_route_golden_tide_trade': 3,
    'boss_route_golden_tide_trade_thunder': 3,
    'boss_route_moon_tide_ruins': 5,
    'boss_route_moon_tide_ruins_arcane': 5,
}
LABELS = {'Original01': '保守换船路线', 'Direct01': '旧构筑失败样本',
          'Unlock01': '旧构筑早挑战失败样本', 'Direct02': '直接换船与调整构筑',
          'Unlock02': '解锁尽早挑战与调整构筑', 'Unlock03': '早挑战与补药恢复样本',
          'DirectMiniboss01': '小Boss版 · 固定直接换船策略',
          'UnlockMiniboss01': '小Boss版 · 固定早挑战策略',
          'LearningMiniboss01': '小Boss版 · 导航策略错误样本',
          'LearningMiniboss02': '小Boss版 · 修正导航后的成长路线'}

POLICY_NOTES = {
    'Direct01': '旧版纯咒印生存构筑未适应当前卡牌规则，保留为失败样本。',
    'Unlock01': '旧版纯咒印生存构筑过早启用，保留为失败样本。',
    'UnlockMiniboss01': '药品月度额度耗尽后策略立即返港，末尾 27 次为空往返；这些不代表玩家实际磨 Boss 时间。',
    'LearningMiniboss01': '前段首杀合法；后段工具错误地直接驶向目的港，未先击败航路首领，无法到港购船。该导航错误导致的长航程不能归因于游戏 Boss 难度。',
}

def load(p):
    return json.loads(p.read_text(encoding='utf-8-sig'), parse_float=Decimal)

def csvrows(p):
    with p.open(encoding='utf-8-sig', newline='') as f:
        return list(csv.DictReader(f))

def money(x):
    return f'{Decimal(str(x)):,.2f}'

def h(x):
    return html.escape(str(x), quote=True)

def decktext(cards):
    counts = collections.Counter((c['name'], c['upgrade']) for c in cards)
    return '；'.join(f'{name} +{rank} ×{count}' for (name, rank), count in counts.items()) or '无'

def writecsv(name, rows):
    if not rows:
        return
    with (OUT / name).open('w', encoding='utf-8-sig', newline='') as f:
        writer = csv.DictWriter(f, fieldnames=list(rows[0]))
        writer.writeheader()
        writer.writerows(rows)

def table(headers, rows):
    return '<div class="scroll"><table><thead><tr>' + ''.join(f'<th>{h(x)}</th>' for x in headers) + '</tr></thead><tbody>' + ''.join('<tr>' + ''.join(f'<td>{h(c)}</td>' for c in row) + '</tr>' for row in rows) + '</tbody></table></div>'

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('runs', nargs='+')
    args = parser.parse_args()
    crew_source = (OUT / 'FrozenSource/Content/Runtime/CrewNarrativeCatalog.cs').read_text(encoding='utf-8-sig')
    crew_names = dict(re.findall(r'Crew\("([^"]+)",\s*"[^"]+",\s*\d+,\s*"([^"]+)"', crew_source))
    kills, gates, summaries, sections = [], [], [], []
    for tag in args.runs:
        p = OUT / tag
        data = load(p / 'campaign.json')
        actions, battles = csvrows(p / 'actions.csv'), csvrows(p / 'battles.csv')
        voyages = csvrows(p / 'voyages.csv')
        zero_trips = [int(x['trip']) for x in voyages if Decimal(x['hours']) == 0 and Decimal(x['distanceNm']) == 0]
        version = '小Boss最终调整后' if 'Miniboss' in tag else '第二次下调前基线'
        parts = {x['id']: x for x in load(p / 'component-catalog.json')}
        final, primary = data['final'], data.get('primaryCompletion')
        review_path = OUT / 'EconomyReview' / (tag + '-ledger-review.json')
        ledger_review = load(review_path) if review_path.exists() else None
        summary = dict(run=tag, version=version, label=LABELS.get(tag, tag), completed=data['completed'],
                       finalVoyage=final['trip'], firstKills=len(data['firstKills']),
                       zeroDistanceTrips=zero_trips, actualSailingVoyages=len(voyages)-len(zero_trips),
                       legendaryCompletionVoyage=primary['trip'] if primary else None,
                       legendaryCompletion=primary, battleCount=len(battles),
                       losses=sum(x['outcome'] == 'loss' for x in battles),
                       sailActions=sum(x['actionType'] == 'Sail' for x in actions),
                       final=final, ledgerReview=ledger_review)
        summaries.append(summary)
        title = LABELS.get(tag, tag) + ' · ' + tag + '（' + version + '）'
        rows, detail = [], []
        for bid, kill in sorted(data['firstKills'].items(), key=lambda kv: kv[1]['firstKill']['action']):
            state, start = kill['firstKill'], kill['battleStart']
            assert Decimal(str(state['initialCash'])) + state['totalIncome'] - state['totalExpense'] - state['totalLoss'] == state['totalCash']
            equipped = [x for x in start['components'] if x['ShipIndex'] == start['ship']['index']]
            row = dict(run=tag, version=version, bossId=bid, bossName=kill['name'], firstKillVoyage=state['trip'],
                       nonTutorialVoyage=max(0, state['trip'] - 2), totalGoldEarned=state['totalIncome'],
                       totalGoldSpent=state['totalExpense'], failureLoss=state['failureLoss'],
                       settlementRetentionLoss=state['settlementRetentionLoss'], totalCash=state['totalCash'],
                       totalCashOut=state['totalExpense']+state['totalLoss'],
                       accountGold=state['accountGold'], travelGold=state['travelGold'],
                       pendingSettlementGold=state['pendingSettlementGold'], battleRewardGold=kill['battleRewardGold'],
                       level=state['pirateLevel'], xp=state['pirateXP'],
                       sailedHours=state['sailedHours'], sailedNm=state['sailedNm'],
                       ship=start['ship']['name'], ordinaryCrew=start['ordinaryCrew'],
                       preparationTurns=kill['stageTurns']['Preparation'], navalTurns=kill['stageTurns']['Naval'],
                       boardingTurns=kill['stageTurns']['Boarding'],
                       startingMedicine=kill['startingMedicine'], endingMedicine=kill['endingMedicine'],
                       preparationDeck=decktext(start['deck']['Preparation']), navalDeck=decktext(start['deck']['Naval']),
                       boardingDeck=decktext(start['deck']['Boarding']),
                       bonusDeckJson=json.dumps(start['bonusDeck'], ensure_ascii=False, default=str),
                       equippedComponentsJson=json.dumps(equipped, ensure_ascii=False, default=str),
                       crewJson=json.dumps(start['crew'], ensure_ascii=False),
                       battleStartJson=json.dumps(start, ensure_ascii=False, default=str))
            kills.append(row)
            rows.append([kill['name'], state['trip'], money(state['totalIncome']), money(state['totalExpense']),
                         money(state['totalLoss']), money(state['totalCash']), start['ship']['name'],
                         f"{row['preparationTurns']}/{row['navalTurns']}/{row['boardingTurns']}"])
            component_text = '；'.join(parts.get(x['ComponentId'], {}).get('name', x['ComponentId']) for x in equipped) or '无'
            detail.append('<details><summary>' + h(kill['name']) + ' · 第 ' + str(state['trip']) + ' 航首杀配置</summary>'
                          + '<p>' + h(f"{start['ship']['name']}，船体 {start['ship']['hull']}/{start['ship']['maxHull']}，火力 {start['ship']['firepower']}，装甲 {start['ship']['armor']}，普通船员 {start['ordinaryCrew']}，药品 {kill['startingMedicine']} → {kill['endingMedicine']}") + '</p>'
                          + '<p><b>已装组件：</b>' + h(component_text) + '</p><p><b>随船专家：</b>' + h('；'.join(crew_names.get(x, x) for x in start['crew']) or '无') + '</p>'
                          + ''.join('<p><b>' + name + '：</b>' + h(decktext(start['deck'][stage])) + '</p><p class="muted">附加卡：' + h(decktext(start['bonusDeck'][stage])) + '</p>' for stage, name in [('Preparation', '准备牌组'), ('Naval', '海战牌组'), ('Boarding', '接舷牌组')])
                          + '</details>')
        gate_rows = []
        for bid, level in TARGETS.items():
            unlock = data.get('levelUnlocks', {}).get(str(level))
            attempts = [x for x in data.get('bossAttempts', []) if x['id'] == bid]
            kill = data['firstKills'].get(bid)
            before_win = [x for x in attempts if not kill or x['battleStart']['action'] <= kill['battleStart']['action']]
            row = dict(run=tag, version=version, bossId=bid, level=level, unlockVoyage=unlock['trip'] if unlock else None,
                       firstAttemptVoyage=attempts[0]['trip'] if attempts else None,
                       firstKillVoyage=kill['firstKill']['trip'] if kill else None,
                       unlockToFirstWinVoyages=kill['firstKill']['trip'] - unlock['trip'] if kill and unlock else None,
                       failedAttemptsBeforeWin=sum(not x['won'] for x in before_win),
                       firstAttemptShip=attempts[0]['battleStart']['ship']['name'] if attempts else '',
                       firstWinShip=kill['battleStart']['ship']['name'] if kill else '')
            gates.append(row)
            name = kill['name'] if kill else attempts[0]['name'] if attempts else bid
            gate_rows.append([name, level, row['unlockVoyage'], row['firstAttemptVoyage'] or '未挑战',
                              row['firstKillVoyage'] or '未首胜', row['unlockToFirstWinVoyages'] if kill else '—',
                              row['failedAttemptsBeforeWin'], row['firstWinShip'] or row['firstAttemptShip']])
        intro = f"总计 {len(battles)} 战 / {summary['losses']} 败；截至第 {final['trip']} 航，{len(data['firstKills'])}/13 类首领。"
        if zero_trips:
            intro += f"其中 {len(zero_trips)} 次为零小时、零海里立即返港；实际航行 {summary['actualSailingVoyages']} 次，不能把这些空往返算作真实磨 Boss 时间。"
        if primary:
            intro += f"第 {primary['trip']} 航首次集齐六种传奇胜利；累计获得 {money(primary['totalIncome'])}、消费 {money(primary['totalExpense'])}、损耗 {money(primary['totalLoss'])}、总现金 {money(primary['totalCash'])}。"
        else:
            intro += '本样本未完成全部传奇，保留失败过程，不作为通关证明。'
        if tag in POLICY_NOTES:
            intro += POLICY_NOTES[tag]
        sections.append('<section class="run" data-run="' + h(tag) + '"><h2>' + h(title) + '</h2><p>' + h(intro) + '</p>'
                        + '<h3>三级、五级海图的解锁与首胜</h3>' + table(['Boss', '开放等级', '解锁航次', '首次尝试', '首胜航次', '解锁后间隔', '首胜前失败', '首胜船只'], gate_rows)
                        + '<h3>全部首杀经济节点</h3>' + table(['Boss', '航次', '累计获得', '累计消费', '累计损耗', '总现金', '开战船只', '准备/海战/接舷回合'], rows)
                        + '<h3>开战时的船只与完整牌组</h3>' + ''.join(detail)
                        + '<p class="muted">原始数据：' + h(tag) + '/campaign.json、actions.csv、economy.csv、battles.csv、voyages.csv、Checkpoints/。</p></section>')
    writecsv('全部路线首杀节点.csv', kills)
    writecsv('三级五级海图卡关节点.csv', gates)
    (OUT / 'review-metrics.json').write_text(json.dumps(dict(runs=summaries, gates=gates), ensure_ascii=False, indent=2, default=str), encoding='utf-8')
    findings = (OUT / '审核结论正文.html').read_text(encoding='utf-8') if (OUT / '审核结论正文.html').exists() else '<p>正在汇总独立经济复核及原生 Unity 界面验证。以下为已经记录的原始结果。</p>'
    default_run = 'LearningMiniboss02' if 'LearningMiniboss02' in args.runs else args.runs[0]
    options = '<option value="all">全部路线</option>' + ''.join('<option value="' + h(tag) + '"' + (' selected' if tag == default_run else '') + '>' + h(LABELS.get(tag, tag) + ' · ' + tag) + '</option>' for tag in args.runs)
    doc = '''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Boss 调整后的完整成长审核</title><style>
    :root{color-scheme:light;--ink:#17323a;--muted:#5e7075;--line:#dce5e6;--accent:#116d78}*{box-sizing:border-box}body{margin:0;background:#f1f5f4;color:var(--ink);font:15px/1.75 'Microsoft YaHei',sans-serif}main{max-width:1480px;margin:auto;padding:36px 30px 70px}header,section{background:white;border:1px solid var(--line);border-radius:12px;padding:26px;margin:0 0 22px}h1{font-size:30px;margin:0 0 8px}h2{font-size:23px}h3{margin-top:30px;font-size:17px}p{max-width:1150px}.muted{color:var(--muted);font-size:13px}.badge{display:inline-block;background:#deefeb;color:#165d52;border-radius:5px;padding:3px 10px;font-size:12px}.scroll{overflow:auto}table{border-collapse:collapse;min-width:920px;width:100%;font-size:13px}th,td{text-align:left;border-bottom:1px solid var(--line);padding:10px;white-space:nowrap}th{background:#eaf2f1}tr:nth-child(even){background:#f7faf9}details{border:1px solid var(--line);border-radius:7px;margin:10px 0;padding:12px 15px}summary{cursor:pointer;font-weight:bold}details p{font-size:13px;margin:8px 0}select,input{font:inherit;padding:8px 12px;border:1px solid #adc3c6;border-radius:6px;background:white}.filters{position:sticky;top:0;z-index:2;padding:14px;background:#f1f5f4eF;display:flex;gap:12px;flex-wrap:wrap}a{color:var(--accent)}.finding{border-left:4px solid var(--accent);padding-left:18px;margin:22px 0}.risk{border-color:#b46a32}li{margin:8px 0}@media print{body{background:white}main{padding:0}.filters{display:none}header,section{border:0;padding:8px;break-inside:auto}details{break-inside:avoid}table{font-size:10px;min-width:0}td,th{white-space:normal;padding:5px}}</style><main>
    <header><span class="badge">2026-10-27 · 当前源码 · 连续新档记录</span><h1>Boss 调整后的完整成长审核</h1><p>从新建角色、教学、早期养成到六种传奇舰队全部首次击败，逐笔记录金币与配置；重点核对三级、五级海图开放后的实际挑战间隔。</p><p class="muted">长期流程使用生产规则自动执行合法玩家操作；原生 Unity 界面另行验证。没有人工逐屏全程用时数据。地图、首领抽选与掉落保留正常随机，不把非配对路线差异直接当作因果效果。</p></header>
    <section id="findings"><h2>审核结论与证据边界</h2>''' + findings + '''<p class="muted">统计口径：累计收入不含初始 560 金；消费与失败/归港折损分列。总现金＝账户＋随船＋待确认结算托管，内部转移不重复记收入。航次包含 2 次教学；首杀现金在奖励产生后记录，配置取开战前。离线收入为 0。游戏内天数、自动执行秒数均不等同玩家在线小时数。</p></section>
    <div class="filters"><label>路线 <select id="runFilter">''' + options + '''</select></label><label>查找首杀配置 <input id="search" placeholder="输入 Boss、船只或卡牌名称"></label></div>''' + ''.join(sections) + '''<section><h2>可复核数据</h2><p><a href="全部路线首杀节点.csv">全部首杀节点 CSV</a> · <a href="三级五级海图卡关节点.csv">三级/五级节点 CSV</a> · <a href="review-metrics.json">结构化汇总</a> · <a href="EconomyReview/">独立账本复核</a> · <a href="UIValidation/">Unity 界面验收</a></p><p class="muted">按用户追加要求再次降低四位区域 Boss；其余九位 Boss（含六传奇）经运行时逐项比较完全不变。玩家卡牌及用户存档未改。失败路线完整保留，不隐藏或改写失败样本。</p></section></main><script>
    const filter=document.querySelector('#runFilter'),search=document.querySelector('#search');function apply(){document.querySelectorAll('.run').forEach(s=>{s.hidden=filter.value!=='all'&&s.dataset.run!==filter.value;s.querySelectorAll('details').forEach(d=>{d.hidden=search.value&&!d.textContent.toLowerCase().includes(search.value.toLowerCase())})})}filter.addEventListener('change',apply);search.addEventListener('input',apply);apply();</script></html>'''
    (OUT / 'Boss完整成长审核.html').write_text(doc, encoding='utf-8')
    print(json.dumps(dict(runs=args.runs, firstKills=len(kills), gateRows=len(gates), report=str(OUT / 'Boss完整成长审核.html')), ensure_ascii=False))

if __name__ == '__main__':
    main()
