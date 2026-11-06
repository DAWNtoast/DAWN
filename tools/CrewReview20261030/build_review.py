"""Build the stable, offline crew review catalogue from exported production data.

Only writes the review HTML/CSS/JS. User review JSON files and browser keys stay intact.
"""
from pathlib import Path
import html
import json
import shutil
import argparse

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'outputs' / 'crew-review-20261030'
HERE = Path(__file__).resolve().parent
LABELS = {'navigator':'航海士','carpenter':'船匠','doctor':'船医','gunner':'炮手','cook':'厨师','diver':'潜水员'}
PAGES = [('background','Background','人物背景'),('story','Story','航海异闻'),('skills','Skills','职业技能'),('specialCards','SpecialCards','专属卡牌')]

def e(value):
    return html.escape(str(value if value is not None else ''), quote=True)

def paragraphs(value):
    return '<p>' + e(value).replace('\n','<br>') + '</p>'

def render_value(value):
    if isinstance(value,dict):
        return '<dl class="detail-list">'+''.join('<div><dt>'+e(k)+'</dt><dd>'+render_value(v)+'</dd></div>' for k,v in value.items())+'</dl>'
    if isinstance(value,list):
        return '<ul>'+''.join('<li>'+render_value(v)+'</li>' for v in value)+'</ul>'
    return e(value).replace('\n','<br>')

def figure(src, title, note='', extra='', thumb=''):
    if not src:
        return ''
    return f'''<figure class="{extra}"><button class="image-open" type="button" data-src="{e(src)}" data-title="{e(title)}" aria-label="放大查看{e(title)}"><img src="{e(thumb or src)}" alt="{e(title)}" loading="lazy" decoding="async"><span class="expand-icon" aria-hidden="true">⤢</span></button><figcaption><strong>{e(title)}</strong>{f'<span>{e(note)}</span>' if note else ''}</figcaption></figure>'''

def art_path(value):
    return value.get('path','') if isinstance(value,dict) else (value or '')

def render_person(c, index, capture):
    cid=c['id']; name=c['displayName']; port=c['portName']
    role=c.get('professionName') or LABELS.get(c.get('professionId'), c.get('professionId',''))
    art=c.get('art',{})
    portrait=art_path(art.get('portrait'))
    tavern=art_path(art.get('tavern'))
    pages=capture.get('pages',[]) if capture else []
    shots=[]
    for key, suffix, label in PAGES:
        match=next((p for p in pages if p.get('key')==key),{})
        src=match.get('path',f'game-view/{cid}_{suffix}.png')
        note='初始滚动位置 · 完整文字见上方资料' if match.get('scrollable') else '当前游戏界面 · 点击放大'
        if (OUT/src).exists(): shots.append(figure(src,label,note,'game-shot',match.get('thumbPath','')))
    scene=capture.get('tavernScenePath','') if capture else ''
    story=c.get('story') or {}
    story_html='<h4>'+e(story.get('chapterLabel') or story.get('title','航海异闻'))+'</h4>'+paragraphs(story.get('body',''))
    skill=c.get('skills') or c.get('skillDetails')
    cards=c.get('specialCards') or []
    skills_html=''.join('<h4>'+e(s.get('label','职业技能'))+'</h4>'+paragraphs(s.get('text','')) for s in skill) if isinstance(skill,list) else paragraphs(c.get('skillDescription',''))
    extra_art=''
    for art_key, art_title in [('regionalPortrait','通用角色画像'),('legacyFullBody','旧版全身立绘（参考）')]:
        a=art.get(art_key)
        if a: extra_art+=figure(art_path(a),art_title,a.get('label','') if isinstance(a,dict) else '',thumb=a.get('thumbPath','') if isinstance(a,dict) else '')
    if extra_art: extra_art='<details class="text-details"><summary>更多角色素材 <span>通用画像 / 已有全身立绘</span></summary><div class="extra-art">'+extra_art+'</div></details>'
    cards_html=''
    for card in cards:
        if isinstance(card,dict):
            cards_html+='<div class="text-card"><h4>'+e(card.get('name') or card.get('displayName') or card.get('id','专属卡牌'))+'</h4>'
            cards_html+='<p class="card-lore">'+e(card.get('lore',''))+'</p>'
            cards_html+='<p class="muted">基础版本 +'+e(card.get('level',0))+' · 可用阶段：'+e(' / '.join(card.get('stages',[])))+'</p>'
            for v in card.get('variants',[]):
                cards_html+='<div class="card-variant"><strong>'+e(v.get('stageLabel',''))+'</strong><span class="muted"> · 能量 '+e(v.get('energy',''))+' · 其他费用 '+e(v.get('otherCost','—'))+'</span>'+paragraphs(v.get('gameEffect') or v.get('effect',''))+'</div>'
            if not card.get('variants'): cards_html+=paragraphs(card.get('previewEffect') or card.get('description',''))
            cards_html+='</div>'
        else: cards_html+='<div class="text-card">'+render_value(card)+'</div>'
    search=' '.join(str(c.get(k,'')) for k in ('id','displayName','portName','professionId','roleSummary','specialty','biography','appearanceTags'))+' '+role
    return f'''<article class="crew-block" id="{e(cid)}" data-crew-id="{e(cid)}" data-crew-number="{index:02d}" data-crew-name="{e(name)}" data-port="{e(port)}" data-profession="{e(c.get('professionId',''))}" data-search="{e(search)}">
      <header class="person-head"><div class="person-number">{index:02d}</div><div class="person-name"><div class="person-meta">{e(port)}<span> / </span>{e(role)}</div><h2>{e(name)}</h2><p>{e(c.get('roleSummary',''))}</p></div><span class="review-badge">待审核</span></header>
      <div class="person-body"><div class="facts"><div><span>契约费用</span><strong>{e(c.get('contractCost',''))}<small> 金币</small></strong></div><div><span>专长</span><strong>{e(c.get('specialty',''))}</strong></div><div><span>叠加规则</span><strong>{e(c.get('stats',''))}</strong></div><div><span>档案关系</span><strong>{e(c.get('loreRelationLabel',''))}</strong></div></div>
      <div class="bio"><div><h3>人物背景</h3>{paragraphs(c.get('biography',''))}</div><div><h3>外观设定</h3>{paragraphs(c.get('appearanceTags',''))}<h3>职业能力</h3>{paragraphs(c.get('skillDescription',''))}</div></div>
      <div class="section-line"><h3>形象对照</h3><span>点击图片放大 · 支持原始尺寸查看</span></div>
      <div class="art-grid">{figure(portrait,'人物立绘','游戏使用的立绘原素材','portrait',art.get('portrait',{}).get('thumbPath',''))}{figure(tavern,'酒馆内形象','从游戏当前酒馆素材按人物位置裁切','tavern-person',art.get('tavern',{}).get('thumbPath',''))}{figure(scene,port+' · 酒馆全景','当前游戏界面实际渲染','tavern-scene',capture.get('tavernThumbPath','')) if scene else ''}</div>
      {extra_art}<details class="text-details"><summary>查看完整文字资料 <span>航海异闻 / 职业技能 / 专属卡牌</span></summary><div class="detail-content"><section>{story_html}</section><section>{skills_html}</section><section><h4>专属卡牌</h4><div class="cards-text">{cards_html or '<p>见下方游戏专属卡牌页。</p>'}</div></section><p class="source-id">船员 ID：{e(cid)}</p></div></details>
      <div class="section-line"><h3>游戏内四页信息</h3><span>1920 × 1080 · 点击查看完整画面</span></div><div class="shots-grid">{''.join(shots) if shots else '<p class="capture-pending">游戏界面正在导出，完成后将在此显示四页截图。</p>'}</div>
      <section class="review-box" aria-label="{e(name)}审核"><div class="review-heading"><label class="check-label"><input type="checkbox" class="review-checkbox"><span>已审核这位船员</span></label><span class="note-hint">无需手动保存</span></div><label class="notes-label" for="notes-{e(cid)}">修改建议</label><textarea id="notes-{e(cid)}" class="review-notes" rows="3" placeholder="例如：人物背景、立绘细节、酒馆形象、技能描述或页面排版需要怎样调整……"></textarea><p class="review-crew-print"></p></section></div>
    </article>'''

def main():
    parser=argparse.ArgumentParser(); parser.add_argument('--allow-pending',action='store_true'); args=parser.parse_args()
    data=json.loads((OUT/'crew-data.json').read_text(encoding='utf-8-sig'))
    people=data['crew']
    assert len(people)==36 and len({c['id'] for c in people})==36, 'Expected 36 unique crew members'
    manifest=OUT/'capture-manifest.json'
    captures=json.loads(manifest.read_text(encoding='utf-8-sig')) if manifest.exists() else {}
    capture_map={c['id']:c for c in captures.get('crew',[])}
    if not args.allow_pending:
        for c in people:
            assert c['id'] in capture_map, 'Missing screenshot entry: '+c['id']
            assert len(capture_map[c['id']]['pages'])==4
            for p in capture_map[c['id']]['pages']: assert (OUT/p['path']).is_file(), p['path']
    ports=list(dict.fromkeys(c['portName'] for c in people))
    article='\n'.join(render_person(c,i+1,capture_map.get(c['id'],{})) for i,c in enumerate(people))
    nav=''.join(f'<button type="button" class="port-button" data-port="{e(p)}"><span>{e(p)}</span><small>0 / 6</small></button>' for p in ports)
    profession_options=''.join(f'<option value="{e(k)}">{e(v)}</option>' for k,v in LABELS.items())
    html_text=f'''<!doctype html><html lang="zh-CN"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><title>全部船员审核 · 36 位船员</title><meta name="description" content="全部船员资料、立绘、酒馆形象及游戏内四页截图的逐人审核册。"><link rel="stylesheet" href="review.css"></head><body data-catalog-date="2026-10-30">
    <header class="masthead"><a class="brand" href="#top" aria-label="回到顶部"><span class="brand-mark" aria-hidden="true">⚓</span><span>人物档案 <small>CREW REVIEW</small></span></a><div class="header-actions"><button type="button" id="import-reviews" class="quiet">导入记录</button><button type="button" id="export-reviews" class="primary">导出审核记录 <span aria-hidden="true">↗</span></button><input id="import-reviews-file" type="file" accept=".json,application/json" hidden></div></header>
    <div class="shell" id="top"><aside class="sidebar"><div class="sidebar-title">按港口浏览</div><button class="port-button active" data-port="" type="button"><span>全部船员</span><small>0 / 36</small></button>{nav}<div class="sidebar-footer"><strong>审核方式</strong><p>查看资料和图片，在每个人下方勾选并填写建议。</p><p>完成后导出 JSON 文件，即可交给我逐项处理。</p><span>资料版本 · 2026.10.30</span></div></aside>
    <main><section class="intro"><div class="eyebrow">六座港口 / 三十六位同行者</div><h1>船员审核册<span>36</span></h1><p>人物资料、立绘与酒馆形象，以及游戏中的四页档案，一起核对。</p><div class="progress-row"><strong id="review-progress">已审核 0 / 36 位</strong><span id="visible-count">显示 36 位</span></div><div class="progress-track"><div id="progress-bar"></div></div><p id="review-storage-status" role="status" aria-live="polite">审核记录会自动保存到当前浏览器。</p></section>
    <div class="filter-bar"><label class="search-box"><span aria-hidden="true">⌕</span><input type="search" id="search" placeholder="搜索姓名、职业、专长或背景" aria-label="搜索船员"></label><select id="profession-filter" aria-label="按职业筛选"><option value="">全部职业</option>{profession_options}</select><select id="review-filter" aria-label="按审核状态筛选"><option value="all">全部审核状态</option><option value="pending">待审核</option><option value="reviewed">已审核</option><option value="notes">有修改建议</option></select></div>
    <div id="crew-list">{article}</div><div id="empty-state" hidden>没有找到符合条件的船员。<button type="button" id="reset-filters">清除筛选</button></div><footer class="page-footer">36 位船员 · 6 座港口 · 6 类职业 · 144 张游戏档案截图<br>资料和图像来自当前项目。审核记录保存在当前浏览器，请导出备份。</footer></main></div>
    <dialog id="lightbox"><div class="lightbox-bar"><strong id="lightbox-title"></strong><div><button type="button" id="image-fit">适合窗口</button><button type="button" id="image-native">原始尺寸</button><a id="image-original" target="_blank" rel="noopener">打开原图</a><button type="button" id="image-close" aria-label="关闭图片">关闭 ✕</button></div></div><div class="lightbox-stage"><img id="lightbox-image" alt=""></div><div class="lightbox-foot"><button type="button" id="image-prev">← 上一张</button><span>方向键切换 · Esc 关闭</span><button type="button" id="image-next">下一张 →</button></div></dialog>
    <script src="crew-review.js"></script><script src="review-ui.js"></script></body></html>'''
    (OUT/'全部船员审核.html').write_text(html_text,encoding='utf-8')
    (OUT/'index.html').write_text('<!doctype html><meta charset="utf-8"><meta http-equiv="refresh" content="0;url=全部船员审核.html"><title>全部船员审核</title><a href="全部船员审核.html">打开全部船员审核</a>',encoding='utf-8')
    for name in ('review.css','review-ui.js'): shutil.copyfile(HERE/name,OUT/name)
    # Reuse the proven per-item persistence/merge/recovery flow under a separate namespace.
    script=(ROOT/'tools/CardListBySchool20261023/CardReview.js').read_text(encoding='utf-8-sig')
    for old,new in [('CardReview','CrewReview'),('card-review','crew-review'),('card-block','crew-block'),('cardId','crewId'),('cardNumber','crewNumber'),('cardName','crewName'),('data-card','data-crew'),('review-card-print','review-crew-print'),('card-review-change','crew-review-change'),('^card_','^crew_'),('data.cards','data.crew'),(', cards};',', crew:cards};'),('卡牌','船员'),('张','位'),('已检查','已审核')]: script=script.replace(old,new)
    (OUT/'crew-review.js').write_text(script,encoding='utf-8')
    print(json.dumps({'crew':len(people),'screenshots':sum(len(c.get('pages',[])) for c in capture_map.values()),'page':str(OUT/'全部船员审核.html')},ensure_ascii=False))

if __name__=='__main__': main()
