"""Verify the 17 captured states and assemble their post-removal screenshots."""
import html
import json
import re
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

sys.stdout.reconfigure(encoding="utf-8")
capture, output = map(Path, sys.argv[1:3])
targets = json.loads(Path(__file__).with_name("targets.json").read_text(encoding="utf-8-sig"))
report = json.loads((capture / "report.json").read_text(encoding="utf-8-sig"))
frames = {frame["file"]: frame for frame in report["frames"]}
allowed = {
    "Departure Dock Chart Records Heading": r"可用海图 \d+ 张",
    "Voyage Cargo Selection Hint": r"选择物品",
}
output.mkdir(parents=True, exist_ok=True)
font = ImageFont.truetype("C:/Windows/Fonts/msyh.ttc", 25)
cards, results, articles = [], [], []
for index, target in enumerate(targets, 1):
    frame = frames[target["file"]]
    if frame.get("error"):
        raise RuntimeError(frame["error"])
    remaining = []
    for label in frame["text"]:
        name = label["path"].split("/")[-1]
        if name not in target["labels"] or not label.get("meshAvailable"):
            continue
        value = re.sub(r"<[^>]+>", "", label["content"]).strip()
        if value and not re.fullmatch(allowed.get(name, r"(?!)"), value):
            raise AssertionError(f"Unexpected remaining caption: {name}: {value}")
        if value:
            remaining.append(value)
    source = capture / target["file"]
    image = Image.open(source).convert("RGB")
    filename = f"{index:02d}.png"
    image.save(output / filename)
    thumb = image.resize((900, 506), Image.Resampling.LANCZOS)
    card = Image.new("RGB", (940, 590), "#f5f1e7")
    ImageDraw.Draw(card).text((20, 15), f"{index:02d}  {target['title']}", font=font, fill="#292a2a")
    card.paste(thumb, (20, 65))
    cards.append(card)
    results.append({"id": index, "title": target["title"], "result": "PASS", "retained_data": remaining})
    articles.append(f"<article><h2>{index:02d} {html.escape(target['title'])}</h2>"
                    f"<a href='{filename}'><img loading='lazy' src='{filename}'></a></article>")
for start in range(0, len(cards), 6):
    subset = cards[start:start + 6]
    sheet = Image.new("RGB", (1920, 60 + ((len(subset) + 1) // 2) * 610), "#171f29")
    ImageDraw.Draw(sheet).text((30, 12), "说明小字删除后 · 界面复核", font=font, fill="white")
    for offset, card in enumerate(subset):
        sheet.paste(card, (10 + offset % 2 * 960, 60 + offset // 2 * 610))
    sheet.save(output / f"overview-{start // 6 + 1:02d}.jpg", quality=93)
(output / "verification.json").write_text(json.dumps(results, ensure_ascii=False, indent=2), encoding="utf-8")
(output / "index.html").write_text(
    "<!doctype html><html lang='zh-CN'><meta charset='utf-8'><title>说明小字删除后</title>"
    "<style>body{background:#171f29;color:white;font:18px/1.6 'Microsoft YaHei';margin:32px auto;max-width:1600px}"
    "img{width:100%}article{margin:40px 0}h2{font-size:24px}</style>"
    "<h1>说明小字删除后 · 17 组界面</h1>"
    "<p>当前代码在隔离 Unity 工程渲染。点击图片查看原始尺寸。保留必要的数据、标题与操作错误反馈。</p>"
    + "\n".join(articles) + "</html>", encoding="utf-8")
print(json.dumps({"verified": len(results), "result": "PASS", "report": str(output / "index.html")}, ensure_ascii=False))
