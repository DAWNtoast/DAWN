"""Publish recorded progression, first-kill economy and loadouts as local HTML."""
import argparse
import collections
import csv
import html
import json
import re
from decimal import Decimal
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'outputs/campaign-progression-retune-20261027'
TARGETS = [('boss_route_golden_tide_trade', '税金女王号', 3),
           ('boss_route_golden_tide_trade_thunder', '极光敕令号', 3),
           ('boss_route_moon_tide_ruins', '月蚀圣骸', 5),
           ('boss_route_moon_tide_ruins_arcane', '月织星槎号', 5)]

def load(p):
    return json.loads(p.read_text(encoding='utf-8-sig'), parse_float=Decimal)

def esc(x):
    return html.escape(str(x), quote=True)

def num(x):
    return f'{Decimal(str(x)):,.2f}'

def deck(cards):
    return '；'.join(f'{n} +{u} ×{q}' for (n, u), q in collections.Counter((x['name'], x['upgrade']) for x in cards).items()) or '无'

def table(head, rows):
    return '<div class="scroll"><table><thead><tr>' + ''.join(f'<th>{esc(x)}</th>' for x in head) + '</tr></thead><tbody>' + ''.join('<tr>' + ''.join(f'<td>{esc(x)}</td>' for x in r) + '</tr>' for r in rows) + '</tbody></table></div>'

def csvout(name, rows):
    if not rows:
        return
    with (OUT / name).open('w', encoding='utf-8-sig', newline='') as f:
        writer = csv.DictWriter(f, list(rows[0]))
        writer.writeheader()
        writer.writerows(rows)

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('runs', nargs='+')
    args = parser.parse_args()
    source = (OUT / 'FrozenSource/Content/Runtime/CrewNarrativeCatalog.cs').read_text(encoding='utf-8-sig')
    crew = dict(re.findall(r'Crew\("([^"]+)",\s*"[^"]+",\s*\d+,\s*"([^"]+)"', source))
    cards = {x['Id']: x['Name'] for x in load(ROOT / 'outputs/card-list-by-school-20261023/card-list-data.json')['Cards']}
    all_kills, all_pacing, summaries, sections = [], [], [], []
    for tag in args.runs:
        path = OUT / tag
        data = load(path / 'campaign.json')
        with (path / 'voyages.csv').open(encoding='utf-8-sig') as f:
            voyages = list(csv.DictReader(f))
        with (path / 'battles.csv').open(encoding='utf-8-sig') as f:
            battles = list(csv.DictReader(f))
        components = {x['id']: x['name'] for x in load(path / 'component-catalog.json')}
        audit_path = OUT / 'EconomyReview' / (tag + '-ledger-review.json')
        audit = load(audit_path) if audit_path.exists() else None
        primary = data.get('primaryCompletion')
        version = '最终版：经验0.25 / 0.025，流派2至7级，初图下调' if tag == 'Retune04' else '中间版：经验0.5 / 0.05，旧首图与旧流派门槛'
        summary = dict(run=tag, version=version, completed=data['completed'], final=data['final'], legendaryCompletion=primary,
                       firstKills=len(data['firstKills']), battles=len(battles), losses=sum(x['outcome']=='loss' for x in battles),
                       actualSailingVoyages=sum(Decimal(x['distanceNm'])>0 for x in voyages), ledgerAudit=audit)
        summaries.append(summary)
        pacing_rows = []
        for bid, name, level in TARGETS:
            unlock = data['levelUnlocks'].get(str(level))
            attempts = [x for x in data['bossAttempts'] if x['id'] == bid]
            kill = data['firstKills'].get(bid)
            start = attempts[0]['trip'] if attempts else None
            win = kill['firstKill']['trip'] if kill else None
            losses_before = sum(not x['won'] for x in attempts if not kill or x['battleStart']['action'] < kill['battleStart']['action'])
            row = dict(run=tag, bossId=bid, bossName=name, unlockLevel=level,
                       unlockVoyage=unlock['trip'] if unlock else None, firstAttemptVoyage=start, firstWinVoyage=win,
                       unlockToWinVoyages=win-unlock['trip'] if win and unlock else None,
                       firstAttemptToWinVoyages=win-start if win and start else None, failuresBeforeFirstWin=losses_before,
                       firstWinShip=kill['battleStart']['ship']['name'] if kill else None)
            all_pacing.append(row)
            pacing_rows.append([name, level, row['unlockVoyage'], start or '未尝试', win or '未胜',
                                row['unlockToWinVoyages'] if win else '—', row['firstAttemptToWinVoyages'] if win else '—', losses_before, row['firstWinShip'] or '—'])
        kill_rows, details = [], []
        for bid, kill in sorted(data['firstKills'].items(), key=lambda p: p[1]['firstKill']['action']):
            state, start = kill['firstKill'], kill['battleStart']
            assert state['initialCash'] + state['totalIncome'] - state['totalExpense'] - state['totalLoss'] == state['totalCash']
            equipped = [x for x in start['components'] if x['ShipIndex'] == start['ship']['index']]
            row = dict(run=tag, bossId=bid, bossName=kill['name'], voyage=state['trip'], level=state['pirateLevel'],
                       earned=state['totalIncome'], spent=state['totalExpense'], losses=state['totalLoss'],
                       totalCashOut=state['totalExpense']+state['totalLoss'], cash=state['totalCash'], ship=start['ship']['name'],
                       ordinaryCrew=start['ordinaryCrew'], medicineStart=kill['startingMedicine'], medicineEnd=kill['endingMedicine'],
                       preparationDeck=deck(start['deck']['Preparation']), navalDeck=deck(start['deck']['Naval']), boardingDeck=deck(start['deck']['Boarding']),
                       completeBattleStartJson=json.dumps(start, ensure_ascii=False, default=str))
            all_kills.append(row)
            kill_rows.append([kill['name'], state['trip'], state['pirateLevel'], num(state['totalIncome']), num(state['totalExpense']), num(state['totalLoss']), num(state['totalCash']), start['ship']['name']])
            details.append('<details><summary>' + esc(kill['name']) + ' · 第 ' + str(state['trip']) + ' 航</summary>'
                           + '<p>' + esc(f"{start['ship']['name']}；船体 {start['ship']['hull']}/{start['ship']['maxHull']}；火力 {start['ship']['firepower']}；装甲 {start['ship']['armor']}；普通船员 {start['ordinaryCrew']}；药品 {kill['startingMedicine']} → {kill['endingMedicine']}") + '</p>'
                           + '<p><b>组件：</b>' + esc('；'.join(components.get(x['ComponentId'], x['ComponentId']) for x in equipped) or '无') + '</p>'
                           + '<p><b>专家：</b>' + esc('；'.join(crew.get(x, x) for x in start['crew']) or '无') + '</p>'
                           + ''.join('<p><b>' + label + '：</b>' + esc(deck(start['deck'][stage])) + '</p><p class="muted">附加牌：' + esc(deck(start['bonusDeck'][stage])) + '</p>' for stage,label in [('Preparation','准备牌组'),('Naval','海战牌组'),('Boarding','接舷牌组')])
                           + '<p class="muted">来源专属卡经验：' + esc('；'.join(f'{cards.get(card_id, card_id)}：{xp}' for card_id, xp in start['specialCardXP'].items())) + '</p></details>')
        intro = f"第 {data['final']['trip']} 航结束，{len(data['firstKills'])}/13 种非教学 Boss；{len(battles)} 战、{summary['losses']} 败，实际航行 {summary['actualSailingVoyages']} 次。"
        if primary:
            intro += f" 第 {primary['trip']} 航首次集齐六传奇：累计收入 {num(primary['totalIncome'])}、消费 {num(primary['totalExpense'])}、损耗 {num(primary['totalLoss'])}、现金 {num(primary['totalCash'])}。"
        else:
            intro += ' 本样本未完成全部传奇，不作为完整通关证明。'
        tutorial = data.get('tutorialFirstKill')
        tutorial_text = ''
        if tutorial:
            node = tutorial['firstKill']
            tutorial_text = '<p class="muted">教学残帆掠夺者另列：第 ' + str(node['trip']) + ' 航，累计收入 ' + num(node['totalIncome']) + '、消费 ' + num(node['totalExpense']) + '、损耗 ' + num(node['totalLoss']) + '、现金 ' + num(node['totalCash']) + '。教学会替换为固定教学牌堆，记录中的初始保存牌组不等于阶段中临时牌堆；详情在 campaign.json 的 tutorialFirstKill。</p>'
        sections.append('<section class="run" data-run="' + esc(tag) + '"><h2>' + esc(tag) + ' · ' + esc(version) + '</h2><p>' + esc(intro) + '</p>' + tutorial_text + '<h3>三级、五级首领进度</h3>'
                        + table(['Boss','开放等级','开放航次','首试航次','首胜航次','解锁至首胜','首试至首胜','首胜前败北','首胜船只'],pacing_rows)
                        + '<h3>全部非教学首杀经济节点</h3>' + table(['Boss','航次','海盗等级','累计收入','累计消费','累计损耗','现金','船只'],kill_rows)
                        + '<h3>首杀时的完整构筑</h3>' + ''.join(details) + '<p><a href="'+esc(tag)+'/campaign.json">完整记录</a> · <a href="'+esc(tag)+'/actions.csv">逐步操作</a> · <a href="'+esc(tag)+'/economy.csv">逐笔账本</a> · <a href="'+esc(tag)+'/voyages.csv">航行记录</a></p></section>')
    csvout('首杀金币航次与配置.csv', all_kills)
    csvout('三级五级挑战间隔.csv', all_pacing)
    (OUT/'review-metrics.json').write_text(json.dumps(dict(runs=summaries, pacing=all_pacing), ensure_ascii=False, indent=2, default=str), encoding='utf-8')
    findings = (OUT/'校准结论-最终.html').read_text(encoding='utf-8')
    old = (ROOT/'outputs/campaign-boss-review-20261027/Boss完整成长审核.html').read_text(encoding='utf-8')
    css = re.search(r'<style>(.*?)</style>', old, re.S).group(1)
    css += 'details p{overflow-wrap:anywhere}.filters label{min-width:0}input,select{max-width:100%}@media(max-width:600px){main{padding:18px 12px 40px}header,section{padding:18px}h1{font-size:25px}.filters{padding:10px}.filters label{display:flex;flex-direction:column;gap:4px;max-width:100%}}'
    doc = '<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>成长节奏与 Boss 校准报告</title><style>'+css+'</style><main><header><span class="badge">2026-10-27 · 新版成长与收益</span><h1>成长节奏与 Boss 校准报告</h1><p>放缓海盗、船员与部件成长；区域 Boss 检验解锁时的构筑和数次发育；接舷金币与声望按海战报价的 1.25 倍结算。</p><p class="muted">采用生产规则自动连续新档、独立账本审计和 Unity 验证。地图与掉落保留正常随机；没有人工全程在线时长数据，也不把单条策略样本当作新手平均通关率。</p></header><section>'+findings+'</section>'+''.join(sections)+'<section><h2>统计口径与证据</h2><p>累计收入不含初始 560 金；消费与损耗分列，总现金包含账户、随船与结算托管。首杀余额取胜利奖励产生后、返港折损前；配置取开战时。航次含 2 次教学，游戏小时不是人类在线时长。不同版本的地图和掉落未配对，构筑与恢复策略也可能不同，不能把航次变化全部视为单项改动的因果效果。</p><p><a href="首杀金币航次与配置.csv">首杀明细 CSV</a> · <a href="三级五级挑战间隔.csv">挑战间隔 CSV</a> · <a href="EconomyReview/">独立经济复核</a> · <a href="BossNumericAudit/">Boss 运行时差异</a> · <a href="UIValidation/">Unity 验证</a> · <a href="../campaign-boss-review-20261027/Boss完整成长审核.html">上一轮历史报告</a></p></section></main></html>'
    options = ''.join('<option value="' + esc(tag) + '">' + esc(tag) + (' · 最终版本' if tag == 'Retune04' else ' · 中间版本') + '</option>' for tag in args.runs)
    filters = '<div class="filters" id="first-kills"><label>记录 <select id="runFilter">' + options + '<option value="all">全部版本</option></select></label><label>查找首杀配置 <input id="search" placeholder="Boss、船只或卡牌名称"></label></div>'
    doc = doc.replace('<section class="run"', filters + '<section class="run"', 1)
    doc = doc.replace('</header>', '<p><a href="#first-kills">跳到首杀金币、航次与完整配置</a></p></header>', 1)
    doc = doc.replace('href="BossNumericAudit/"', 'href="BossNumericAuditExperience2Verified/"')
    doc = doc.replace('</html>', '''<script>
    const filter=document.querySelector('#runFilter'),search=document.querySelector('#search');
    function apply(){document.querySelectorAll('.run').forEach(s=>{s.hidden=filter.value!=='all'&&s.dataset.run!==filter.value;s.querySelectorAll('details').forEach(d=>{d.hidden=!!search.value&&!d.textContent.toLowerCase().includes(search.value.toLowerCase())})})}
    filter.addEventListener('change',apply);search.addEventListener('input',apply);apply();
    </script></html>''')
    (OUT/'成长节奏与Boss校准报告.html').write_text(doc, encoding='utf-8')
    print(json.dumps(dict(runs=args.runs, firstKills=len(all_kills), report=str(OUT/'成长节奏与Boss校准报告.html')), ensure_ascii=False))

if __name__ == '__main__':
    main()
