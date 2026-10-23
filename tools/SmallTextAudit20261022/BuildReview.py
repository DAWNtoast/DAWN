"""Build a local, numbered screenshot review from Unity capture geometry."""
import argparse
import html
import json
import re
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

parser = argparse.ArgumentParser()
parser.add_argument("capture", type=Path)
parser.add_argument("targets", type=Path)
parser.add_argument("output", type=Path)
args = parser.parse_args()
args.output.mkdir(parents=True, exist_ok=True)
font_path = "C:/Windows/Fonts/msyh.ttc"
font = lambda size: ImageFont.truetype(font_path, size)
plain = lambda text: re.sub(r"<[^>]+>", "", text).replace("\r", "")
frames = []
for path in args.capture.rglob("report.json"):
    value = json.loads(path.read_text(encoding="utf-8-sig"))
    for frame in value.get("frames", [value]):
        frame["capture_directory"] = str(path.parent)
        frames.append(frame)
frames = {frame["file"]: frame for frame in frames if "file" in frame and "text" in frame}
targets = json.loads(args.targets.read_text(encoding="utf-8-sig"))
entries, cards = [], []
for index, target in enumerate(targets, 1):
    frame = frames[target["file"]]
    source = Path(frame["capture_directory"]) / target["file"]
    image = Image.open(source).convert("RGB")
    width, height = image.size
    labels = [label for label in frame["text"] if label.get("meshAvailable") and any(
        label["path"].endswith(suffix) for suffix in target["labels"])]
    if not labels:
        raise ValueError(f"No visible target text for {target['file']}: {target['labels']}")
    marked = image.copy()
    draw = ImageDraw.Draw(marked)
    rects = []
    for label in labels:
        rect = label["meshPixels"]
        x, y, w, h = rect["x"], rect["y"], rect["width"], rect["height"]
        box = (max(0, int(x)-9), max(0, int(height-y-h)-9), min(width, int(x+w)+9), min(height, int(height-y)+9))
        draw.rectangle(box, outline="#ff4c50", width=3)
        rects.append(box)
    stem = f"{index:02d}"
    marked.save(args.output / f"{stem}-marked.png")
    image.save(args.output / f"{stem}-original.png")
    excerpts = [plain(label["content"]) for label in labels]
    entries.append({"id": index, **target, "text": excerpts, "rects": rects,
                    "screenshot": f"{stem}-marked.png", "original": f"{stem}-original.png"})
    card = Image.new("RGB", (940, 760), "#f5f1e7")
    pen = ImageDraw.Draw(card)
    pen.text((22, 16), f"{index:02d}  {target['title']}", font=font(27), fill="#292a2a")
    thumb = marked.resize((900, round(height*900/width)), Image.Resampling.LANCZOS)
    card.paste(thumb, (20, 65))
    line_y = 65 + thumb.height + 14
    lines = []
    for text in excerpts:
        for segment in text.splitlines():
            while len(segment) > 38:
                lines.append(segment[:38]); segment = segment[38:]
            if segment:
                lines.append(segment)
    for line in lines[:5]:
        pen.text((24, line_y), line, font=font(22), fill="#4d463b")
        line_y += 29
    cards.append(card)

for start in range(0, len(cards), 6):
    subset = cards[start:start+6]
    sheet = Image.new("RGB", (1920, 60 + ((len(subset)+1)//2)*780), "#171f29")
    ImageDraw.Draw(sheet).text((30, 13), "游戏说明小字 · 截图清单", font=font(29), fill="white")
    for offset, card in enumerate(subset):
        sheet.paste(card, (10+(offset%2)*960, 60+(offset//2)*780))
    sheet.save(args.output / f"overview-{start//6+1:02d}.jpg", quality=94)

parts = ["<!doctype html><html lang='zh-CN'><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'>",
"<title>游戏说明小字截图清单</title><style>body{margin:0;background:#111a25;color:#e9edf1;font:17px/1.65 'Microsoft YaHei',sans-serif}main{max-width:1500px;margin:auto;padding:32px}h1{font-size:30px}a{color:#e7c17d}nav{display:grid;grid-template-columns:repeat(auto-fit,minmax(280px,1fr));gap:6px;padding-bottom:25px}article{background:#1d2938;padding:24px;margin:24px 0;border-radius:12px}img{width:100%;height:auto;border-radius:5px}.caption{color:#c1cedb}li{margin:8px 0}code{font-size:13px}</style><main>",
"<h1>游戏说明小字截图清单</h1><p class='caption'>截图来自当前代码的隔离 Unity 界面渲染。红框标记说明文字；每项提供未标记原图。教学、空列表和战斗弹窗等提示只在对应条件下出现。此次仅整理截图，未删除更多文字。</p><nav>"]
for entry in entries:
    parts.append(f"<a href='#item-{entry['id']}'>{entry['id']:02d} · {html.escape(entry['title'])}</a>")
parts.append("</nav>")
for entry in entries:
    parts.append(f"<article id='item-{entry['id']}'><h2>{entry['id']:02d} · {html.escape(entry['title'])}</h2>")
    if entry.get("note"):
        parts.append(f"<p class='caption'>{html.escape(entry['note'])}</p>")
    parts.append("<ul>"+"".join(f"<li>{html.escape(text).replace(chr(10), '<br>')}</li>" for text in entry["text"])+"</ul>")
    parts.append(f"<a href='{entry['original']}'><img loading='lazy' src='{entry['screenshot']}' alt='{html.escape(entry['title'])}'></a><p><a href='{entry['original']}'>打开未标记原图</a></p></article>")
parts.append("</main></html>")
(args.output / "index.html").write_text("\n".join(parts), encoding="utf-8")
(args.output / "entries.json").write_text(json.dumps(entries, ensure_ascii=False, indent=2), encoding="utf-8")
print(json.dumps({"entries": len(entries), "overview_pages": (len(cards)+5)//6, "report": str(args.output / "index.html")}, ensure_ascii=False))
