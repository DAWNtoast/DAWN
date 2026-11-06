"""Collect successful Unity captures in audit order; originals stay untouched."""
import html
import json
from pathlib import Path

root = Path(r'D:\Game\outputs\transparent-backing-screenshots-20261104')
frames = {}
for folder in sorted(root.glob('run*')):
    report_file = folder / 'report.json'
    if not report_file.is_file():
        continue
    report = json.loads(report_file.read_text(encoding='utf-8-sig'))
    for frame in report['frames']:
        if frame.get('error') or not (folder / frame['file']).is_file():
            continue
        if frame['variant'] == 'ComponentResale':
            frame['regions'] = [region for region in frame['regions'] if region['name'].startswith('Component Resale Row transparent-capture-spare-')]
        frame['file'] = f"{folder.name}/{frame['file']}"
        frames[frame['index']] = frame
if set(frames) != set(range(1, 14)):
    raise RuntimeError(f'Missing captures: {set(range(1, 14)) - set(frames)}')
ordered = [frames[index] for index in sorted(frames)]
(root / 'report.json').write_text(json.dumps({'scope': 'Current production UI rendered by Unity, in-memory preview samples; no player data changes.', 'frames': ordered}, ensure_ascii=False, indent=2), encoding='utf-8')
parts = ['''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>半透明底框 · 逐项截图</title>
<style>*{box-sizing:border-box}html{scroll-behavior:smooth}body{margin:0;background:#121719;color:#eee;font:16px/1.6 system-ui,"Microsoft YaHei",sans-serif}header,main{max-width:1680px;margin:auto;padding:24px}header p{color:#b9c3c7}nav{display:grid;grid-template-columns:repeat(auto-fit,minmax(260px,1fr));gap:6px 20px}a{color:#e6c58b}section{margin:36px 0 64px;scroll-margin-top:18px}h2{font-size:22px;font-weight:600;margin:0 0 12px}.shot{position:relative;background:black}.shot img{width:100%;display:block}.box{position:absolute;border:3px solid #ff4b45;pointer-events:none}.hide .box{display:none}.toolbar{position:fixed;right:18px;bottom:18px;z-index:5;display:flex;gap:8px}.toolbar button,.toolbar a{background:#313b40;border:1px solid #819095;color:#fff;padding:9px 13px;border-radius:6px;cursor:pointer;text-decoration:none}small{color:#aab6bb}.links{display:flex;justify-content:space-between;padding-top:8px}</style>
<header id="top"><h1>半透明底框 · 逐项截图</h1><p>按清单顺序，共13张。当前源码的 Unity 实际界面渲染，1920×1080；使用独立内存样本，未改动玩家进度。红框只标位置，原图保持不变。仓库拆成总览与详情两张。</p><nav>''']
for frame in ordered:
    parts.append(f'<a href="#f{frame["index"]}">{frame["index"]:02d} · {html.escape(frame["title"])}</a>')
parts.append('</nav></header><main>')
for frame in ordered:
    index, title, file = frame['index'], html.escape(frame['title']), html.escape(frame['file'])
    parts.append(f'<section id="f{index}"><h2>{index:02d} · {title}</h2><div class="shot"><img loading="lazy" src="{file}" alt="{title}">')
    for region in frame['regions']:
        bounds = region['bounds']
        x, y, w, h = (bounds[key] for key in ('x', 'y', 'width', 'height'))
        parts.append(f'<i class="box" style="left:{x/frame["width"]*100:.3f}%;top:{y/frame["height"]*100:.3f}%;width:{w/frame["width"]*100:.3f}%;height:{h/frame["height"]*100:.3f}%"></i>')
    next_link = f'<a href="#f{index+1}">下一张 →</a>' if index < 13 else '<a href="#top">返回目录 ↑</a>'
    parts.append(f'</div><div class="links"><a href="{file}" target="_blank">查看1920×1080原图</a>{next_link}</div></section>')
parts.append('</main><div class="toolbar"><button onclick="document.body.classList.toggle(\'hide\')">显示／隐藏红框</button><a href="#top">目录 ↑</a></div></html>')
(root / 'index.html').write_text('\n'.join(parts), encoding='utf-8')
print(f'Gallery ready: {len(ordered)} captures; originals untouched; {root / "index.html"}')
