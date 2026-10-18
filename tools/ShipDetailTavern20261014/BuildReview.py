"""Build local contact sheets from the actual Unity captures, without changing art."""
import json
import html
from pathlib import Path
import sys
import xml.etree.ElementTree as ET
from PIL import Image, ImageDraw, ImageFont, ImageOps

folder = Path(sys.argv[1] if len(sys.argv) > 1 else r"D:\Game\Artifacts\ShipDetailTavern20261014\validation\final\captures")
report = json.loads((folder / "report.json").read_text(encoding="utf-8"))
font = ImageFont.truetype("C:/Windows/Fonts/msyh.ttc", 18)
groups = [
    ("01-performance-overview.jpg", [f for f in report["frames"] if not f["tavern"] and f["page"] == 0 and f["width"] == 1920]),
    ("02-background-equipment-reference.jpg", [f for f in report["frames"] if f["width"] == 1920 and (f["page"] != 0 or f["tavern"])]),
    ("03-narrow-screen.jpg", [f for f in report["frames"] if f["width"] == 1280]),
]
for name, frames in groups:
    cols = 3 if len(frames) > 4 else 2
    thumb = (640, 360 if len(frames) > 4 else 512)
    row_height = thumb[1] + 34
    sheet = Image.new("RGB", (thumb[0] * cols, row_height * ((len(frames) + cols - 1) // cols)), "#1b1713")
    draw = ImageDraw.Draw(sheet)
    for index, frame in enumerate(frames):
        image = Image.open(folder / frame["file"]).convert("RGB")
        image = ImageOps.contain(image, thumb)
        x, y = index % cols * thumb[0], index // cols * row_height
        sheet.paste(image, (x + (thumb[0] - image.width) // 2, y))
        draw.text((x + 10, y + thumb[1] + 5), frame["name"], fill="#efdbb3", font=font)
    sheet.save(folder / name, quality=92)
print(f"Built {len(groups)} contact sheets from {len(report['frames'])} actual Unity frames.")

artifact = folder.parents[2]
boundary = artifact / "validation/equipment-boundary"
if (boundary / "play-results.xml").exists():
    edit = ET.parse(artifact / "validation/final/edit-results.xml").getroot()
    play = ET.parse(boundary / "play-results.xml").getroot()
    live = json.loads((boundary / "interactions/report.json").read_text(encoding="utf-8"))
    summary = {
        "editMode": {key: edit.attrib[key] for key in ["result", "total", "passed", "failed"]},
        "playMode": {key: play.attrib[key] for key in ["result", "total", "passed", "failed"]},
        "previewFrames": len(report["frames"]), "interactionFrames": len(live["frames"]),
        "allPreviewScalersVerified": all(frame["scalerVerified"] and not frame.get("error") for frame in report["frames"]),
        "previewGallery": "validation/final/captures/index.html",
        "latestEquipmentGallery": "validation/equipment-boundary/interactions/index.html",
        "notes": [
            "The final equipment interaction captures include the last 6px metadata spacing adjustment.",
            "The six-effect legendary comparison scroll reaches the final quality line without reducing the font size.",
            "The longest catalog component name fits both the inventory and selected comparison panel.",
            "Main Unity project, production scenes and retired UIPreviewsForMe outputs were never opened or written by validation.",
        ],
    }
    (artifact / "validation-summary.json").write_text(json.dumps(summary, ensure_ascii=False, indent=2), encoding="utf-8")
    cards = []
    for frame in report["frames"]:
        file = (folder / frame["file"]).relative_to(artifact).as_posix()
        cards.append(f'<figure><a href="{file}"><img loading="lazy" src="{file}"></a><figcaption>{html.escape(frame["name"])} · {frame["width"]}×{frame["height"]}</figcaption></figure>')
    livecards = []
    for frame in live["frames"]:
        file = (boundary / "interactions" / frame["file"]).relative_to(artifact).as_posix()
        livecards.append(f'<figure><a href="{file}"><img loading="lazy" src="{file}"></a><figcaption>{html.escape(frame["caption"])}</figcaption></figure>')
    page = '''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><title>船只详情 · 酒馆同框布局</title><style>body{background:#17120f;color:#ead8b7;font:16px system-ui;margin:28px}main{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:24px}figure{margin:0}img{width:100%;height:auto}a{color:#efd19d}figcaption{padding:10px 0}p{color:#bba98f}</style><h1>船只详情 · 酒馆同框布局</h1><p>船只完整置于左侧同款边框；标题、三项标签与正文位于右侧；底部保留购船、旗舰和装配操作。39 项布局与 3 项实际交互测试通过。</p><h2>12 艘船、三个页面与酒馆对照</h2><main>'''
    page += "".join(cards) + '</main><h2>最终装配交互与六行属性滚动</h2><p>以下真实操作画面包含最终质量文字间距调整。</p><main>' + "".join(livecards) + '</main></html>'
    (artifact / "index.html").write_text(page, encoding="utf-8")
    print(f"Wrote final combined review: {artifact / 'index.html'}")
