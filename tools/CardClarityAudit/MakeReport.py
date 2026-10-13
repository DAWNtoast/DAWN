"""Compose unscaled, original-pixel comparisons from three real Unity captures.

Run only after all capture report.json files and PNGs are complete:
  python MakeReport.py --root D:/Game/Artifacts/CardClarity20261013
Output defaults to ROOT/Comparison. No source images are modified or resampled.
"""
from __future__ import annotations

import argparse
import html
import json
import math
import os
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont


VARIANTS = (
    ("Baseline512Retry", "旧卡名 · 512 卡图"),
    ("FixedTitle512", "修正卡名 · 512 卡图"),
    ("FixedTitle1024", "修正卡名 · 1024 卡图"),
)
BG = (21, 25, 34)
FG = (243, 236, 219)
MUTED = (189, 196, 205)


def key(row):
    return (row["id"], int(row["rank"]), int(row["width"]), round(float(row["cardScale"]), 2))


def font(size, bold=False):
    for candidate in (
        Path("C:/Windows/Fonts/msyhbd.ttc" if bold else "C:/Windows/Fonts/msyh.ttc"),
        Path("C:/Windows/Fonts/simhei.ttf"),
    ):
        if candidate.is_file():
            return ImageFont.truetype(str(candidate), size)
    raise RuntimeError("A Chinese font is required for readable comparison labels.")


def source_path(base, relative):
    result = (base / relative).resolve()
    if not result.is_relative_to(base.resolve()) or not result.is_file():
        raise RuntimeError(f"Missing or out-of-scope capture: {result}")
    return result


def read_png(base, row, part):
    with Image.open(source_path(base, row[f"{part}File"])) as image:
        return image.convert("RGB")


def display_scale(value):
    return "检视 1.0" if math.isclose(value, 1.0) else "工坊 0.76"


def texture_stats(row):
    texture = row.get("artTexture") or {}
    return {name: texture.get(name) for name in ("width", "height", "format", "mipmapCount", "runtimeBytes")}


def pixel_difference(before, after):
    if before.size != after.size:
        return {"comparable": False, "reason": "Pixel dimensions differ; images were not resized.",
                "beforeSize": list(before.size), "afterSize": list(after.size)}
    old, new = np.asarray(before, dtype=np.int16), np.asarray(after, dtype=np.int16)
    difference = np.abs(new - old)
    return {"comparable": True, "cropWidth": before.width, "cropHeight": before.height,
            "rgbMeanAbsoluteDifference0to255": round(float(difference.mean()), 5),
            "rgbRootMeanSquareDifference0to255": round(float(np.sqrt(np.square(difference.astype(np.float64)).mean())), 5),
            "changedPixelPercent": round(float(np.any(difference > 0, axis=2).mean() * 100), 4),
            "maxChannelDifference0to255": int(difference.max()),
            "interpretation": "Difference magnitude only; not a sharpness or quality score."}


def compose(path, heading, subtitle, headers, rows):
    """Each row is (label, list[PIL.Image]); every source pixel is pasted 1:1."""
    pad, gap, top, label_height = 28, 30, 140, 42
    column_widths = [max([image.width for _, images in rows for j, image in enumerate(images) if j == i] + [280])
                     for i in range(len(headers))]
    heights = [label_height + max(image.height for image in images) + 30 for _, images in rows]
    width = 2 * pad + sum(column_widths) + gap * (len(headers) - 1)
    height = top + sum(heights) + pad
    canvas = Image.new("RGB", (width, height), BG)
    draw = ImageDraw.Draw(canvas)
    draw.text((pad, 18), heading, font=font(28, True), fill=FG)
    draw.text((pad, 62), subtitle, font=font(17), fill=MUTED)
    x = pad
    for i, header in enumerate(headers):
        draw.text((x, 104), header, font=font(19, True), fill=FG)
        x += column_widths[i] + gap
    y = top
    for (label, images), row_height in zip(rows, heights):
        draw.text((pad, y + 3), label, font=font(18), fill=MUTED)
        x = pad
        for index, image in enumerate(images):
            canvas.paste(image, (x, y + label_height))
            x += column_widths[index] + gap
        y += row_height
    canvas.save(path)
    return {"file": path.name, "width": width, "height": height,
            "sourcePixelScale": 1, "sourceResampling": "none"}


def pick_representatives(rows):
    candidates = {row["id"]: row for row in rows if row["width"] == 3840 and row["cardScale"] == 1.0}
    ordered = sorted(candidates.values(), key=lambda row: (len(row["title"]), row["id"]))
    if not ordered:
        raise RuntimeError("No native 4K inspect representatives found.")
    # Actual available title lengths, never invented or modified text.
    selected = []
    for row in (ordered[0], ordered[len(ordered) // 2], ordered[-1]):
        if row["id"] not in selected:
            selected.append(row["id"])
    for row in ordered:
        if len(selected) >= min(3, len(ordered)):
            break
        if row["id"] not in selected:
            selected.append(row["id"])
    art_ids = [row["id"] for row in ordered if row["school"] in ("fire", "arcane", "tide")]
    for row in ordered:
        if len(art_ids) >= min(3, len(ordered)):
            break
        if row["id"] not in art_ids:
            art_ids.append(row["id"])
    return selected, art_ids[:3], ordered[-1]["id"]


def checks(report):
    return {"status": report.get("status"), "activeCardCount": report.get("activeCardCount"),
            "catalogChecks": report.get("catalogChecks"), "representativeChecks": len(report.get("representatives", [])),
            "failedChecks": report.get("failedChecks"), "errorCount": len(report.get("errors", [])),
            "completedUtc": report.get("completedUtc"), "unityVersion": report.get("unityVersion")}


def density_summary(rows):
    grouped = {}
    for row in rows:
        group = f"{row['width']}x{row['height']}/{float(row['cardScale']):.2g}"
        grouped.setdefault(group, set()).add(round(float(row.get("renderedTitlePixelsPerUnit", 1)), 5))
    return {group: sorted(values) for group, values in grouped.items()}


def write_html(output, root, reports, indexes, summary):
    entries = []
    for capture_key in sorted(set.intersection(*(set(index) for index in indexes.values()))):
        first = indexes[VARIANTS[0][0]][capture_key]
        entry = {"id": first["id"], "rank": first["rank"], "title": first["title"], "width": first["width"],
                 "height": first["height"], "scale": first["cardScale"], "variants": []}
        for variant, label in VARIANTS:
            row = indexes[variant][capture_key]
            files = {part: Path(os.path.relpath(source_path(root / variant, row[f"{part}File"]), output)).as_posix()
                     for part in ("card", "title", "art")}
            entry["variants"].append({"label": label, "files": files, "density": row.get("renderedTitlePixelsPerUnit", 1),
                                      "fontSize": row["titleFontSize"], "glyphs": row["renderedTitleGlyphs"],
                                      "expectedGlyphs": row["expectedTitleGlyphs"], "texture": texture_stats(row),
                                      "passed": row["passed"]})
        entries.append(entry)
    payload = json.dumps(entries, ensure_ascii=False).replace("</", "<\\/")
    title_rows = "".join(f"<tr><th>{html.escape(label)}</th><td>{reports[variant]['catalogChecks']}</td>"
                         f"<td>{reports[variant]['failedChecks']}</td><td>{len(reports[variant]['representatives'])}</td></tr>"
                         for variant, label in VARIANTS)
    content = r'''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>卡牌清晰度：原像素对比</title><style>
body{margin:0;background:#151922;color:#f3ecdb;font:16px/1.65 system-ui,"Microsoft YaHei",sans-serif}
main{padding:24px 30px}h1{font-size:28px;margin:0 0 12px}h2{font-size:21px;margin-top:28px}
p{max-width:1100px}a{color:#a9d3ff}label{display:inline-flex;gap:8px;align-items:center;margin:6px 20px 6px 0}
select{font:inherit;background:#27313f;color:#fff;padding:5px 8px;border:1px solid #6b7889;border-radius:4px}
.controls{position:sticky;top:0;padding:12px 0;background:#151922;z-index:2;border-bottom:1px solid #526071}
.scroller{max-width:100%;overflow:auto;border:1px solid #526071;margin-top:18px;background:#0e131b}
.comparison{display:flex;width:max-content;align-items:flex-start;gap:24px;padding:20px}.panel{flex:none;min-width:280px}
.panel h3{font-size:18px;margin:0 0 8px}.panel p{font-size:13px;color:#bec8d5;max-width:520px;margin:0 0 12px}
.panel img{display:block;width:auto;height:auto;max-width:none;image-rendering:auto}
table{border-collapse:collapse}td,th{border:1px solid #526071;padding:7px 14px;text-align:left}.note{color:#bec8d5}
.tag{display:inline-block;padding:2px 9px;background:#27313f;border-radius:4px;margin-right:8px}
</style><main><h1>卡牌清晰度：原始像素并排对比</h1>
<p>三组都来自真实 Unity 卡面工厂与画布：1920×1080 和 3840×2160 渲染，卡牌实际缩放为 1.0 或工坊 0.76。
以下图片保持原像素尺寸，可横向滚动。浏览器建议设为 100% 缩放；系统和浏览器仍可能影响屏幕显示，下载源 PNG 可独立检查。</p>
<p><strong>两项变化分开评估：</strong>先比较第 1、2 列的卡名，查看字形密度修正；再比较第 2、3 列的插画，查看 512→1024 的收益。
卡图分辨率增加并不等于在当前卡牌显示尺寸下必然显著变清晰。像素差异只表示图像改变，不是画质分数。</p>
<div class="controls"><label>卡牌<select id="card"></select></label><label>渲染目标<select id="resolution"><option value="3840">3840×2160</option><option value="1920">1920×1080</option></select></label>
<label>显示尺寸<select id="scale"><option value="0.76">真实工坊 0.76</option><option value="1">检视 1.0</option></select></label>
<label>区域<select id="part"><option value="title">卡名</option><option value="art">插画</option><option value="card">完整卡面</option></select></label></div>
<p id="selection"></p><div class="scroller"><div class="comparison" id="comparison"></div></div>
<h2>固定原像素对照图</h2><p><a href="titles-4k.png">4K 卡名：旧 / 新</a> · <a href="art-4k.png">4K 插画：512 / 1024</a> ·
<a href="full-card-4k.png">4K 工坊完整卡面：三个版本</a></p>
<h2>布局检查</h2><table><thead><tr><th>版本</th><th>目录检查数</th><th>失败数（含代表）</th><th>代表检查数</th></tr></thead><tbody>__CHECKS__</tbody></table>
<p class="note">目录检查：全部有效卡牌 × rank 0/3 × 1080p/4K × 1.0/0.76。检查覆盖字形数量与带弧线的标题边界、描述完整性和卡框边界；
通过布局检查不等于主观清晰度已通过。</p>
<h2>内存口径</h2><p>报告里的 runtimeBytes 来自编辑器中的 Profiler.GetRuntimeMemorySizeLong，是 Unity 纹理资源内存测量，不等于纯显存，也不是游戏总显存。
不带 mipmap 的 BC7 纹理数据：512×512 约 256 KiB，1024×1024 约 1 MiB；每张增加约 768 KiB。
若 216 张不同纹理全部驻留，理论纹理数据增加约 162 MiB。此为假设估算，未计其他资源、驱动分配或缓存。</p>
<p><a href="summary.json">完整对比数据 JSON</a> · <a href="检查与对比.txt">中文检查说明</a></p></main>
<script>const entries=__DATA__;const el=id=>document.getElementById(id);const unique=[...new Map(entries.map(x=>[x.id,x])).values()];
for(const row of unique){const o=document.createElement('option');o.value=row.id;o.textContent=row.title+' · '+row.id;el('card').append(o)}
function draw(){const row=entries.find(x=>x.id===el('card').value&&x.width===+el('resolution').value&&Math.abs(x.scale-(+el('scale').value))<.001);el('comparison').replaceChildren();if(!row){el('selection').textContent='没有该组合的捕获数据。';return;}
el('selection').textContent=row.title+'，rank '+row.rank+'；真实 Unity 渲染 '+row.width+'×'+row.height+'；卡牌缩放 '+row.scale+'。';
for(const version of row.variants){const box=document.createElement('section');box.className='panel';const head=document.createElement('h3');head.textContent=version.label;box.append(head);const description=document.createElement('p');
description.textContent='卡名字号 '+version.fontSize+'；字形密度 '+version.density+'×；字形 '+version.glyphs+'/'+version.expectedGlyphs+'；卡图 '+version.texture.width+'×'+version.texture.height+' '+version.texture.format+'；编辑器纹理资源 '+(version.texture.runtimeBytes/1048576).toFixed(3)+' MiB';box.append(description);
const link=document.createElement('a');link.href=version.files[el('part').value];link.target='_blank';const image=document.createElement('img');image.src=link.href;image.alt=version.label+' '+row.title;link.append(image);box.append(link);el('comparison').append(box)}}
for(const id of ['card','resolution','scale','part'])el(id).addEventListener('change',draw);draw();</script></html>'''
    content = content.replace("__CHECKS__", title_rows).replace("__DATA__", payload)
    (output / "index.html").write_text(content, encoding="utf-8")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path("D:/Game/Artifacts/CardClarity20261013"))
    parser.add_argument("--out", type=Path)
    args = parser.parse_args()
    root = args.root.resolve()
    output = (args.out or root / "Comparison").resolve()
    if output == root or output in [(root / name).resolve() for name, _ in VARIANTS]:
        raise RuntimeError("Output must be a separate comparison directory.")
    reports, indexes = {}, {}
    for variant, _ in VARIANTS:
        file = root / variant / "report.json"
        if not file.is_file():
            raise RuntimeError(f"Capture is not complete: missing {file}")
        report = json.loads(file.read_text(encoding="utf-8-sig"))
        if report.get("status") not in ("passed", "completed-with-layout-failures") or not report.get("completedUtc"):
            raise RuntimeError(f"Capture is not complete or failed: {file}")
        reports[variant] = report
        indexes[variant] = {key(row): row for row in report["representatives"]}
    if not all(set(index) == set(indexes[VARIANTS[0][0]]) for index in indexes.values()):
        raise RuntimeError("The representative card/rank/resolution/scale sets differ; refusing mismatched comparisons.")
    output.mkdir(parents=True, exist_ok=True)
    title_ids, art_ids, full_id = pick_representatives(reports[VARIANTS[1][0]]["representatives"])
    summary = {"scope": "Original Unity capture pixels, no image resizing, sharpening, AI generation or source-image edits.",
               "qualityCaveat": "Pixel-difference magnitudes do not establish subjective clarity improvements.",
               "variants": {}, "composites": [], "pixelDifferences": [], "layoutRegressions": []}
    for variant, label in VARIANTS:
        report = reports[variant]
        summary["variants"][variant] = {"label": label, "checks": checks(report),
                                        "titleGlyphDensity": density_summary(report["representatives"]),
                                        "representativeTextureDimensions": sorted({f"{r['artTexture']['width']}x{r['artTexture']['height']}" for r in report["representatives"]}),
                                        "editorArtTextureRuntimeBytes": sorted({r["artTexture"]["runtimeBytes"] for r in report["representatives"]})}
    titles = []
    for scale in (1.0, .76):
        for card_id in title_ids:
            capture_key = (card_id, 3, 3840, scale)
            rows = [indexes[name][capture_key] for name, _ in VARIANTS[:2]]
            pictures = [read_png(root / name, row, "title") for (name, _), row in zip(VARIANTS[:2], rows)]
            titles.append((f"{rows[0]['title']}  |  {display_scale(scale)}  |  字形密度 {rows[0].get('renderedTitlePixelsPerUnit', 1):g}× → {rows[1].get('renderedTitlePixelsPerUnit', 1):g}×", pictures))
    summary["composites"].append(compose(output / "titles-4k.png", "4K 卡名：只比较字形密度修正",
        "原始像素 1:1；两列均使用 512 卡图；未放大、缩小或锐化。", [label for _, label in VARIANTS[:2]], titles))
    arts = []
    for scale in (1.0, .76):
        for card_id in art_ids:
            capture_key = (card_id, 3, 3840, scale)
            rows = [indexes[name][capture_key] for name, _ in VARIANTS[1:]]
            pictures = [read_png(root / name, row, "art") for (name, _), row in zip(VARIANTS[1:], rows)]
            arts.append((f"{rows[0]['title']}  |  {display_scale(scale)}  |  显示裁图 {pictures[0].width}×{pictures[0].height} 像素", pictures))
    summary["composites"].append(compose(output / "art-4k.png", "4K 插画：只比较 512 与 1024 卡图",
        "原始像素 1:1；两列均已修正卡名；是否值得取决于可见细节和内存成本。", [label for _, label in VARIANTS[1:]], arts))
    full_rows = [indexes[name][(full_id, 3, 3840, .76)] for name, _ in VARIANTS]
    full_images = [read_png(root / name, row, "card") for (name, _), row in zip(VARIANTS, full_rows)]
    summary["composites"].append(compose(output / "full-card-4k.png", "4K 工坊完整卡面：两项改变分开看",
        "卡牌实际缩放 0.76；卡面裁图按原始像素粘贴，三列没有重采样。", [label for _, label in VARIANTS], [(full_rows[0]["title"], full_images)]))
    for before_name, after_name, part, change in (
        (VARIANTS[0][0], VARIANTS[1][0], "title", "title-density"),
        (VARIANTS[1][0], VARIANTS[2][0], "art", "art-resolution"),
    ):
        for capture_key in sorted(indexes[before_name]):
            before, after = indexes[before_name][capture_key], indexes[after_name][capture_key]
            stat = pixel_difference(read_png(root / before_name, before, part), read_png(root / after_name, after, part))
            summary["pixelDifferences"].append({"change": change, "id": before["id"], "title": before["title"],
                "rank": before["rank"], "width": before["width"], "cardScale": before["cardScale"], **stat})
    baseline_catalog = {key(row): row for row in reports[VARIANTS[0][0]].get("catalog", [])}
    for variant, _ in VARIANTS[1:]:
        for row in reports[variant].get("catalog", []):
            old = baseline_catalog.get(key(row))
            if old is not None and old.get("passed") and not row.get("passed"):
                summary["layoutRegressions"].append({"variant": variant, "id": row["id"], "rank": row["rank"],
                    "width": row["width"], "cardScale": row["cardScale"]})
    # BC7 is 16 bytes per 4x4 block, no mipmaps. Separate theory from editor Profiler data.
    summary["memory"] = {"editorMeasurementMeaning": "Profiler.GetRuntimeMemorySizeLong(Texture): Unity resource memory measured in Editor, not pure VRAM or total game memory.",
        "bc7NoMip512Bytes": 512 * 512, "bc7NoMip1024Bytes": 1024 * 1024,
        "incrementPerUniqueTextureBytes": 1024 * 1024 - 512 * 512,
        "hypotheticalAllResidentDistinctTextureCount": 216,
        "hypotheticalIncrementMiB": 216 * (1024 * 1024 - 512 * 512) / 1048576,
        "estimateCaveat": "Assumes 216 distinct BC7 textures all resident with no mipmaps; not measured game VRAM, excludes other resources and driver/cache overhead."}
    (output / "summary.json").write_text(json.dumps(summary, ensure_ascii=False, indent=2), encoding="utf-8")
    write_html(output, root, reports, indexes, summary)
    lines = ["卡牌清晰度检查与原像素对比", "", "两项收益分开看：",
        "1. 旧512 与 修正卡名512：只比较卡名字形密度。4K卡名应按实际画布和卡牌缩放申请字形，几何布局与内容保持对应。",
        "2. 修正卡名512 与 修正卡名1024：只比较插画纹理。当前卡牌显示像素宽度小于512，1024不等于必然出现明显肉眼收益。", "",
        "真实Unity渲染检查："]
    for variant, label in VARIANTS:
        report = reports[variant]
        lines.append(f"{label}：有效卡{report['activeCardCount']}，目录检查{report['catalogChecks']}，代表检查{len(report['representatives'])}，失败{report['failedChecks']}；状态{report['status']}。")
        lines.append("  字形密度：" + json.dumps(density_summary(report["representatives"]), ensure_ascii=False))
    lines += [f"相对旧版本新增布局失败：{len(summary['layoutRegressions'])}。", "",
        "PNG并排图全部直接粘贴源裁图，保留原像素，没有缩放、锐化或AI处理。HTML默认原始尺寸，可横向滚动。",
        "summary.json 的像素差异是RGB绝对差和变化像素比例，只反映发生了变化，不是清晰度评分。", "",
        "内存说明：Profiler.runtimeBytes为编辑器中Unity纹理资源内存，不等于纯显存或游戏总显存。",
        "无mipmap的BC7理论数据：512×512=256KiB；1024×1024=1MiB；每张增加768KiB。",
        "假设216张不同纹理全驻留，理论数据增加162MiB。此为估算，未实测游戏总显存，未计其他资源、驱动分配或缓存。", "",
        "查看：index.html；titles-4k.png；art-4k.png；full-card-4k.png；summary.json。"]
    (output / "检查与对比.txt").write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(json.dumps({"output": str(output), "composites": summary["composites"],
                      "layoutRegressions": len(summary["layoutRegressions"])}, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
