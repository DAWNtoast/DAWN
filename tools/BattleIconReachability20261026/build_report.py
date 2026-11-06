"""Build the current-reachability report and refresh the user's fixed links."""
import base64
import collections
import html
import json
import shutil
import sys
from pathlib import Path

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'outputs/battle-icon-reachability-20261026'
FIXED=ROOT/'outputs/battle-icon-tooltips-20261026'
LABELS={'reachable':'当前会出现','legacy_unreachable':'无正常触发来源','training_only':'仅训练/测试入口','uncertain':'待确认'}


def load(p): return json.loads(p.read_text(encoding='utf-8-sig'))
def save(p,v): p.write_text(json.dumps(v,ensure_ascii=False,indent=2),encoding='utf-8')
def string(v): return '、'.join(map(str,v)) if isinstance(v,list) else str(v or '')
def location(v):
    stages={'Preparation':'准备','Naval':'海战','Boarding':'接舷'}
    sides={'player':'己方','enemy':'敌方','both':'双方'}
    return string([stages.get(x,x) for x in v.get('stages',[])])+' / '+sides.get(v.get('side'),string(v.get('side')))
def unique(values): return list(dict.fromkeys(x for x in values if x))
def esc(v): return html.escape(string(v),quote=True)
def cell(v): return string(v).replace('|','\\|').replace('\n','<br>')


def main():
    # Do not overwrite the user-approved 2026-10-27 revision with this old audit.
    if (FIXED/'status-revision.json').is_file():
        import subprocess
        result=subprocess.run([sys.executable,str(ROOT/'tools/BattleStatusRevision20261027/build_report.py'),'--publish'])
        raise SystemExit(result.returncode)
    if '--refresh-current-atlas' in sys.argv:
        shutil.copyfile(ROOT/'UnityClient/Assets/_Project/Resources/BattleIcons/battle_icons_v1.png',OUT/'battle_icons_v1.png')
    previous=load(OUT/'previous-inventory/all-icons.json')
    old_status=[e for e in previous if e['origin']=='statuses.json']
    variants=load(OUT/'naval-status-audit.json')['entries']+load(OUT/'boarding-status-audit.json')['entries']
    by_id=collections.defaultdict(list)
    for v in variants: by_id[v['id']].append(v)
    missing=[e['id'] for e in old_status if e['id'] not in by_id]
    if missing: raise RuntimeError('Unreviewed status IDs: '+','.join(missing))
    rows=load(OUT/'non-status-audit.json')['entries']
    rank={'reachable':3,'training_only':2,'uncertain':1,'legacy_unreachable':0}
    for old in old_status:
        e=dict(old); vv=[v for v in by_id[e['id']] if v['name']==e['name']]
        if not vv: raise RuntimeError('Unreviewed status use: '+e['id']+' / '+e['name'])
        for v in vv:
            if v['availability'] not in rank: raise RuntimeError('Invalid availability '+str(v))
        e['availability']=max((v['availability'] for v in vv),key=rank.get)
        e['variants']=vv
        relevant=[v for v in vv if v['availability']==e['availability']]
        e['reason']='；'.join(unique(v.get('reason','') for v in relevant))
        conditions=[]
        for v in relevant:
            prefix=location(v)
            conditions.append((prefix+'：' if prefix else '')+string(v.get('condition',e['condition'])))
        e['condition']='\n'.join(unique(conditions))
        e['evidence']=[x for v in vv for x in v.get('evidence',[])]
        e['sources']=[x for v in relevant for x in v.get('sources',[])]
        rows.append(e)
    new_ids=set(by_id)-{e['id'] for e in old_status}
    if new_ids:
        raise RuntimeError('New status IDs need explicit review before adding: '+','.join(new_ids))
    identity=lambda e:(e['origin'],e['id'],e['name'])
    ordering={identity(e):i for i,e in enumerate(previous)}
    rows.sort(key=lambda e:ordering.get(identity(e),99999))
    assert len({identity(e) for e in rows})==len(rows)
    assert len(rows)==len(previous)
    cat=load(OUT/'card-catalog.json')
    count=lambda pred:sum(pred(e) for e in rows)
    counts={'totalRecords':len(rows),'normalIcons':count(lambda e:e['origin']!='keywords.json' and e['availability']=='reachable'),
            'normalStatuses':count(lambda e:e['origin']=='statuses.json' and e['availability']=='reachable'),
            'inactiveStatuses':count(lambda e:e['origin']=='statuses.json' and e['availability']=='legacy_unreachable'),
            'normalKeywords':count(lambda e:e['origin']=='keywords.json' and e['availability']=='reachable'),
            'inactiveKeywords':count(lambda e:e['origin']=='keywords.json' and e['availability']=='legacy_unreachable'),
            'inactiveIcons':count(lambda e:e['origin']!='keywords.json' and e['availability']=='legacy_unreachable'),
            'trainingIcons':count(lambda e:e['origin']!='keywords.json' and e['availability']=='training_only'),
            'uncertain':count(lambda e:e['availability']=='uncertain')}
    summary={'counts':counts,'catalogue':cat['Counts'],'inactiveStatuses':[e['name'] for e in rows if e['origin']=='statuses.json' and e['availability']=='legacy_unreachable'],
             'basis':'当前冻结生产源码、重新编译后的最终已发布卡库、全部可用升级与独立敌方牌组；排除仅测试强制造态的路径。',
             'limitation':'静态触发路径核对与运行时卡库枚举；未在Unity中逐条触发全部图标。','runtimeCodeModified':False}
    save(OUT/'audit-entries.json',rows); save(OUT/'audit-summary.json',summary)
    assets=['energy','action_point','mana','tide','thunder_chain','curse_mark','blood','powder','cannonball','planks','medicine','gold','food','draw_pile','discard_pile','supplies','cannon','melee','ram','guard','repair','empower','curse','move','wait','fire','load','morale','armor','attack','healing','weakness','arcane','thunder','anchor','slow','immunity','seal','draw','settings','deck']
    atlas=base64.b64encode((OUT/'battle_icons_v1.png').read_bytes()).decode('ascii')
    categories=unique(e['category'] for e in rows)
    def tooltip(e):
        t=e['tooltip']
        return t if not e['hasTooltip'] or e['origin'] in ['other.json','keywords.json'] else e['name']+'\n'+t
    def source_text(e):
        result=[]
        for s in e.get('sources',[]):
            if isinstance(s,str): result.append(s)
            elif isinstance(s,dict):
                name=s.get('name') or s.get('Name') or s.get('id') or s.get('Id')
                if name: result.append(str(name))
        return '、'.join(unique(result))
    def evidence_text(e):
        return '\n'.join(unique(string(x.get('source'))+(':'+str(x['line']) if x.get('line') else '')+' '+string(x.get('note')) for x in e.get('evidence',[]) if isinstance(x,dict)))
    body=[]
    for category in categories:
        trs=[]
        for e in [r for r in rows if r['category']==category]:
            n=assets.index(e['assetId']) if e.get('assetId') in assets else -1
            icon=f'<span class="sprite" style="background-position:{-(n%8)*64}px {-(n//8)*64}px"></span>' if n>=0 else '<span class="texticon">'+('整卡词条' if e['origin']=='keywords.json' else '见说明')+'</span>'
            scope=[]
            for v in e.get('variants',[]):
                scope.append(LABELS[v['availability']]+' · '+location(v)+'：'+string(v.get('reason')))
            details='\n'.join(unique(scope))
            sources=source_text(e)
            trs.append(f'<tr data-category="{esc(category)}" data-availability="{e["availability"]}" data-kind="{"keyword" if e["origin"]=="keywords.json" else "icon"}" data-tooltip="{"yes" if e["hasTooltip"] else "no"}"><td><div class="identity">{icon}<div><b>{esc(e["name"])}</b><span class="badge {e["availability"]}">{LABELS[e["availability"]]}</span><small>{esc(e["id"])}</small></div></div></td><td><p>{esc(e["condition"])}</p><p>{esc(e["reason"])}</p>'+ (f'<p class="muted">有效来源：{esc(sources)}</p>' if sources else '')+f'<details><summary>分阶段与源码证据</summary><pre>{esc(details)}\n{esc(evidence_text(e))}</pre></details></td><td><pre>{esc(tooltip(e))}</pre></td></tr>')
        body.append(f'<section class="group"><h2>{esc(category)}</h2><div class="tablewrap"><table><thead><tr><th>图标用途</th><th>当前条件与结论</th><th>现有悬停文字</th></tr></thead><tbody>'+''.join(trs)+'</tbody></table></div></section>')
    intro=f'当前正常流程可出现 {counts["normalIcons"]} 条独立图标用途，其中状态 {counts["normalStatuses"]} 条；另有 {counts["normalKeywords"]} 条整卡提示关键词。旧清单保留在审计备份，原链接已刷新。'
    notice=f'已移出常规清单：{counts["inactiveIcons"]} 条无正常触发来源的图标用途、{counts["trainingIcons"]} 条训练/测试专用意图、{counts["inactiveKeywords"]} 条未触发词条。统计按用途而非不同PNG数量；同一基础图形仍可能被其他有效用途使用。'
    document='''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>当前战斗图标可达性核对</title><style>
*{box-sizing:border-box}body{margin:0;background:#111b24;color:#e9efed;font:15px/1.6 "Microsoft YaHei",sans-serif}main{max-width:1540px;margin:auto;padding:30px}h1,h2{color:#edc886}h1{font-size:30px;margin:0 0 12px}h2{font-size:22px;margin:25px 0 10px}.notice{border-left:3px solid #d7b477;background:#20313d;padding:14px;margin:18px 0}.muted,small{color:#a8bac4}small{display:block;font:11px/1.6 Consolas,monospace;margin-top:5px}.toolbar{display:flex;gap:10px;flex-wrap:wrap;position:sticky;top:0;padding:14px 0;background:#111b24;z-index:2}input,select{background:#233440;border:1px solid #566773;color:#eee;padding:9px;border-radius:5px;font:inherit}input{flex:1;min-width:200px}.tablewrap{overflow:auto}table{border-collapse:collapse;width:100%;table-layout:fixed;background:#1b2a35}th,td{text-align:left;padding:13px;border:1px solid #3a4c58;vertical-align:top}th:nth-child(1){width:24%}th:nth-child(2){width:46%}th:nth-child(3){width:30%}p{margin:0 0 9px}.identity{display:flex;gap:9px;align-items:center}.sprite{width:64px;height:64px;flex:0 0 64px;background-size:512px 384px;background-repeat:no-repeat;background-image:url(data:image/png;base64,ATLAS)}.texticon{font-size:11px;color:#a8bac4}.badge{display:block;font-size:11px;color:#8de4c2}.legacy_unreachable{color:#ffbd9e}.training_only{color:#e5cb89}pre{font:13px/1.7 "Microsoft YaHei",sans-serif;white-space:pre-wrap;overflow-wrap:anywhere;margin:0}details{font-size:12px;color:#a8bac4}summary{cursor:pointer}tr[hidden],section[hidden]{display:none}@media(max-width:900px){main{padding:18px}table{min-width:900px}.toolbar{position:static}}
</style><main><h1>当前战斗图标与悬停提示</h1><p>INTRO</p><div class="notice">NOTICE</div><p class="muted">依据本次冻结的生产源码，以及重新编译导出的 324 张已发布卡牌、1062 个阶段×升级变体、19 套独立敌方接舷牌组。未在 Unity 中逐条触发全部图标。显示条件以正常流程为准；仅测试直接赋值不计为有效来源。</p><div class="toolbar"><input id="search" placeholder="搜索图标、效果或来源…"><select id="availability"><option value="reachable">当前正常会出现</option><option value="">全部审计记录</option><option value="legacy_unreachable">无正常触发来源</option><option value="training_only">仅训练/测试入口</option><option value="uncertain">待确认</option></select><select id="category"><option value="">全部类别</option>CATEGORIES</select><select id="kind"><option value="icon">独立图标用途</option><option value="">含整卡词条</option><option value="keyword">整卡词条</option></select><select id="tooltip"><option value="">全部提示状态</option><option value="yes">有悬停提示</option><option value="no">无独立悬停提示</option></select><span id="count"></span></div>BODY<p class="muted">本次只更新审计与清单，未修改游戏代码、卡牌效果或删除图标素材。旧分支移入筛选项，便于后续决定是否清理。</p></main><script>
const rows=[...document.querySelectorAll('tbody tr')],groups=[...document.querySelectorAll('section.group')];const ids=['search','availability','category','kind','tooltip'];const inputs=Object.fromEntries(ids.map(id=>[id,document.getElementById(id)]));for(const r of rows)r.searchText=r.textContent.toLowerCase();function filter(){let n=0;for(const r of rows){r.hidden=ids.some(id=>{const v=inputs[id].value;return v&&(id==='search'?!r.searchText.includes(v.trim().toLowerCase()):r.dataset[id]!==v)});if(!r.hidden)n++;}for(const g of groups)g.hidden=![...g.querySelectorAll('tbody tr')].some(r=>!r.hidden);document.getElementById('count').textContent=n+' / '+rows.length+' 条';}for(const id of ids)inputs[id].addEventListener(id==='search'?'input':'change',filter);filter();
</script></html>'''
    document=document.replace('ATLAS',atlas).replace('INTRO',esc(intro)).replace('NOTICE',esc(notice)).replace('CATEGORIES',''.join(f'<option>{esc(c)}</option>' for c in categories)).replace('BODY',''.join(body))
    md=['# 当前战斗图标与悬停提示','',intro,'',notice,'',f'核对基础：{cat["Counts"]["PublishedIdentities"]} 张最终已发布卡牌、{cat["Counts"]["StageLevelVariants"]} 个阶段×升级变体、{cat["Counts"]["EnemyBoardingDecks"]} 套独立敌方接舷牌组。','',summary['limitation'],'','本次仅更新清单与审计记录，未修改游戏逻辑、卡牌效果或删除素材。','',f'## 不再正常出现的 {counts["inactiveStatuses"]} 项旧状态','']
    for e in rows:
        if e['origin']=='statuses.json' and e['availability']=='legacy_unreachable': md.append('- **'+e['name']+'** (`'+e['id']+'`)：'+e['reason'])
    for avail in ['reachable','training_only','legacy_unreachable','uncertain']:
        chosen=[e for e in rows if e['availability']==avail]
        if not chosen: continue
        md+=['','## '+LABELS[avail],'']
        for category in unique(e['category'] for e in chosen):
            md+=['### '+category,'']
            for e in [x for x in chosen if x['category']==category]:
                md+=['#### '+e['name']+' (`'+e['id']+'`)','','- 条件：'+e['condition'].replace('\n','；'),'- 结论：'+e['reason']]
                sources=source_text(e)
                if sources: md+=['- 有效来源：'+sources]
                if e.get('variants'):
                    for v in e['variants']:
                        md+=['- '+LABELS[v['availability']]+' · '+location(v)+'：'+string(v.get('reason'))]
                md+=['','**悬停文字**（'+('整卡词条，非独立图标' if e['origin']=='keywords.json' else '有提示' if e['hasTooltip'] else '无独立提示')+'）：','']
                md += ['> '+line for line in tooltip(e).split('\n')]
                md+=['','**证据：**']
                for x in e.get('evidence',[]):
                    path=string(x.get('source')).replace('\\','/')
                    if path: md+=['- ['+Path(path).name+(':'+str(x['line']) if x.get('line') else '')+'](</'+path+(':'+str(x['line']) if x.get('line') else '')+'>) '+string(x.get('note'))]
                md+=['']
    for folder in [OUT,FIXED]:
        (folder/'战斗图标与悬停提示.html').write_text(document,encoding='utf-8')
        (folder/'战斗图标与悬停提示.md').write_text('\n'.join(md),encoding='utf-8')
    save(FIXED/'all-icons.json',rows)
    save(FIXED/'reachability-summary.json',summary)
    print(json.dumps(summary,ensure_ascii=False,indent=2))


if __name__=='__main__': main()
