"""Original-pixel before/after report for TextClarityCapture, no image resampling.
Usage: python MakeReport.py --root D:/Game/Artifacts/TextClarity20261013
Requires completed Baseline/report.json and Fixed/report.json; writes Comparison/.
"""
from __future__ import annotations
import argparse
import hashlib
import html
import json
import math
import os
import re
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


def load(path):
    report = json.loads(path.read_text(encoding="utf-8-sig"))
    if report.get("status") != "passed" or not report.get("completedUtc"):
        raise RuntimeError(f"Capture is incomplete or failed: {path}")
    return report


def font(size, bold=False):
    return ImageFont.truetype("C:/Windows/Fonts/msyhbd.ttc" if bold else "C:/Windows/Fonts/msyh.ttc", size)


def canonical(path):
    return re.sub(r"\[\d+\]", "", path)


def crop_box(before, after, width, height):
    a, b = before["meshPixels"], after["meshPixels"]
    left = max(0, math.floor(min(a["x"], b["x"])) - 8)
    right = min(width, math.ceil(max(a["x"] + a["width"], b["x"] + b["width"])) + 8)
    bottom = max(0, math.floor(min(a["y"], b["y"])) - 8)
    top = min(height, math.ceil(max(a["y"] + a["height"], b["y"] + b["height"])) + 8)
    # Both captures use exactly the same integer pixel rectangle, so subpixel
    # translations remain visible and are never recentered or interpolated away.
    return left, height - top, right, height - bottom


def compose(path, title, rows, output):
    images = [(row, Image.open(output / row["beforeFile"]).convert("RGB"),
               Image.open(output / row["afterFile"]).convert("RGB")) for row in rows]
    if not images:
        return None
    column = max(360, max(max(a.width, b.width) for _, a, b in images))
    width, height = 56 + 2 * column + 28, 132 + sum(62 + max(a.height, b.height) for _, a, b in images) + 24
    image = Image.new("RGB", (width, height), (21, 25, 34)); draw = ImageDraw.Draw(image)
    draw.text((28, 18), title, font=font(27, True), fill=(244, 237, 220))
    draw.text((28, 60), "同一屏幕像素区域 · 原像素粘贴 · 不缩放、不锐化、不重新居中", font=font(17), fill=(186, 197, 210))
    draw.text((28, 98), "修复前", font=font(20, True), fill=(244, 237, 220))
    draw.text((56 + column, 98), "修复后", font=font(20, True), fill=(244, 237, 220))
    y = 132
    for row, before, after in images:
        label = row["content"].replace("\n", " / ")
        if len(label) > 55:
            label = label[:52] + "…"
        draw.text((28, y + 6), label, font=font(18), fill=(197, 206, 216))
        image.paste(before, (28, y + 46)); image.paste(after, (56 + column, y + 46))
        y += 62 + max(before.height, after.height)
    image.save(path)
    return {"file": path.name, "width": width, "height": height, "sourcePixelScale": 1}


def stat(row):
    fields = ("fontName", "fontStyle", "fontSize", "bestFitSize", "textPixelsPerUnit", "renderedPixelsPerUnit", "rootCanvasScale", "localScale", "lossyScale",
              "projectedPixelsPerLocalUnit", "scaleRelativeToRootCanvas", "maximumMeshVertexYResidual",
              "meanMeshVertexYResidual", "meshBoundsMinPixelResidual", "meshBoundsMaxPixelResidual", "meshPixels")
    return {field: row.get(field) for field in fields}


def write_html(output, pairs, summary):
    data = json.dumps(pairs, ensure_ascii=False).replace("</", "<\\/")
    check_rows = "".join(f"<tr><td>{html.escape(row['state'])}</td><td>{row['width']}×{row['height']}</td><td>{'相同' if row['pngSha256Equal'] else '不同'}</td><td>{'相同' if row['pixelSha256Equal'] else '不同'}</td></tr>" for row in summary["otherScreens"])
    fixed_links = " · ".join(f"<a href='{row['file']}'>{html.escape(row['file'])}</a>" for row in summary["composites"])
    body = r'''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>其他界面文字清晰度对比</title><style>body{margin:24px;background:#151922;color:#eee;font:16px/1.65 system-ui,"Microsoft YaHei",sans-serif}
h1{font-size:27px}p{max-width:1150px}a{color:#acd}label{display:inline-flex;align-items:center;gap:8px;margin:8px 16px 8px 0}
select{font:inherit;max-width:650px;background:#263141;color:#fff;padding:5px}.scroll{overflow:auto;border:1px solid #526071}.panels{display:flex;width:max-content;gap:28px;padding:20px}.panel{flex:none;min-width:320px}.panel img{display:block;max-width:none;width:auto;height:auto}.meta{font-size:13px;max-width:650px;color:#bec8d5}table{border-collapse:collapse}td,th{border:1px solid #526071;padding:6px 14px;text-align:left}code{color:#c9ddf4}</style>
<h1>其他界面文字清晰度：原像素前后对比</h1>
<p><strong>本报告的修复对照：</strong>导航页标题、船舱和设置按钮取消合成粗体；地图标题与补给读数修正静态父级放大导致的字形采样密度不足；
结算文字在最终字形网格阶段校正屏幕 Y 相位，单次校正不超过 0.5 个物理像素。其他普通 Text 和界面另作验证。</p>
<p>两列取自同一个屏幕整数像素区域，按原像素显示，可横向滚动。没有放大、缩小、锐化，也没有单独居中抵消文字位移。
建议浏览器 100% 缩放；字体曲线/旋转等可合理包含小数像素，残差不是通用画质得分。</p>
<label>界面<select id="state"></select></label><label>目标<select id="width"><option value="3840">3840×2160</option><option value="1920">1920×1080</option></select></label>
<label>文字<select id="label"></select></label><p id="description"></p><div class="scroll"><div class="panels" id="panels"></div></div>
<h2>重点对照图</h2><p>__LINKS__</p><h2>其他界面验证</h2>
<p>以下比较 Home、Settings、PortHub、PortSettings 的完整 PNG 文件 SHA-256 与解码 RGB 像素 SHA-256。相同表示本次确定性预览捕获未观察到画面变化，不等于所有可能游戏状态均已验证。</p>
<table><thead><tr><th>界面</th><th>分辨率</th><th>PNG SHA-256</th><th>像素 SHA-256</th></tr></thead><tbody>__CHECKS__</tbody></table>
<p><a href="summary.json">字体样式与结算 Y 相位详细数据</a> · <a href="检查与对比.txt">检查说明</a></p>
<script>const entries=__DATA__;const el=x=>document.getElementById(x);for(const state of [...new Set(entries.map(r=>r.state))]){const o=document.createElement('option');o.value=state;o.textContent=state;el('state').append(o)}el('state').value='Navigation';
function fill(){el('label').replaceChildren();for(const r of entries.filter(r=>r.state===el('state').value&&r.width===+el('width').value)){const o=document.createElement('option');o.value=r.id;o.textContent=r.content.replaceAll('\n',' / ').slice(0,50)+' · '+r.path.split('/').at(-1);el('label').append(o)}draw()}
function draw(){const r=entries.find(x=>x.id===el('label').value);el('panels').replaceChildren();if(!r)return;el('description').textContent=r.state+' / '+r.path+'；内容：'+r.content;
for(const [name,snapshot,file]of[['修复前',r.before,r.beforeFile],['修复后',r.after,r.afterFile]]){const box=document.createElement('section');box.className='panel';const title=document.createElement('h3');title.textContent=name;box.append(title);const meta=document.createElement('p');meta.className='meta';meta.textContent=snapshot.fontName+' / '+snapshot.fontStyle+' / '+snapshot.fontSize+'px；PPU '+snapshot.textPixelsPerUnit+'；画布 '+snapshot.rootCanvasScale+'×；实际投影 '+Number(snapshot.projectedPixelsPerLocalUnit?.y||0).toFixed(4)+' px/单位；localScale '+Number(snapshot.localScale?.y||0).toFixed(4)+'；最终网格 Y 最大残差 '+Number(snapshot.maximumMeshVertexYResidual||0).toFixed(5)+' px';box.append(meta);const link=document.createElement('a');link.href=file;link.target='_blank';const image=document.createElement('img');image.src=file;image.alt=name+' '+r.content;link.append(image);box.append(link);el('panels').append(box)}}
el('state').onchange=fill;el('width').onchange=fill;el('label').onchange=draw;fill();</script></html>'''
    (output / "index.html").write_text(body.replace("__LINKS__", fixed_links).replace("__CHECKS__", check_rows).replace("__DATA__", data), encoding="utf-8")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path("D:/Game/Artifacts/TextClarity20261013"))
    parser.add_argument("--out", type=Path)
    args = parser.parse_args(); root = args.root.resolve(); output = (args.out or root / "Comparison").resolve()
    if output == root or output in ((root / "Baseline").resolve(), (root / "Fixed").resolve()):
        raise RuntimeError("Use a separate comparison output directory.")
    before_report, after_report = load(root / "Baseline/report.json"), load(root / "Fixed/report.json")
    before_frames = {(r["state"], r["width"]): r for r in before_report["frames"]}
    after_frames = {(r["state"], r["width"]): r for r in after_report["frames"]}
    if before_frames.keys() != after_frames.keys():
        raise RuntimeError("Baseline and Fixed frame sets differ.")
    output.mkdir(parents=True, exist_ok=True); (output / "crops").mkdir(exist_ok=True)
    summary = {"scope": "Production Unity snapshots; original pixels; no resampling or sharpening.",
        "fixedScope": "NavigationHeaderSkin title and Cargo/Settings button synthetic bold; Navigation Chart/Map Title and Water Food Endurance static-transform sampling density; final SettlementTextCentering screen Y phase.",
        "beforeCounts": {k: before_report[k] for k in ("frameCount", "textCount", "sampledTextCount", "status")},
        "afterCounts": {k: after_report[k] for k in ("frameCount", "textCount", "sampledTextCount", "status")},
        "fontStyleChanges": [], "navigationDensity": [], "settlementY": [], "otherScreens": [], "frameComparisons": [], "unmatchedText": [], "composites": []}
    pairs = []
    for frame_key in before_frames:
        bf, af = before_frames[frame_key], after_frames[frame_key]
        before_path, after_path = root / "Baseline" / bf["file"], root / "Fixed" / af["file"]
        with Image.open(before_path) as source: before_image = source.convert("RGB")
        with Image.open(after_path) as source: after_image = source.convert("RGB")
        if before_image.size != after_image.size or before_image.size != (bf["width"], bf["height"]):
            raise RuntimeError(f"Capture dimensions differ: {frame_key}")
        sha = {"state": bf["state"], "width": bf["width"], "height": bf["height"],
            "beforePngSha256": hashlib.sha256(before_path.read_bytes()).hexdigest(),
            "afterPngSha256": hashlib.sha256(after_path.read_bytes()).hexdigest(),
            "beforePixelSha256": hashlib.sha256(before_image.tobytes()).hexdigest(),
            "afterPixelSha256": hashlib.sha256(after_image.tobytes()).hexdigest()}
        sha["pngSha256Equal"] = sha["beforePngSha256"] == sha["afterPngSha256"]
        sha["pixelSha256Equal"] = sha["beforePixelSha256"] == sha["afterPixelSha256"]
        summary["frameComparisons"].append(sha)
        if bf["state"] in ("Home", "Settings", "PortHub", "PortSettings"): summary["otherScreens"].append(sha)
        after_texts = {canonical(row["path"]): row for row in af["texts"]}
        for old in bf["texts"]:
            path = canonical(old["path"]); new = after_texts.get(path)
            if new is None or new["content"] != old["content"]:
                summary["unmatchedText"].append({"state": bf["state"], "width": bf["width"], "path": path, "reason": "missing or changed content"}); continue
            identity = {"state": bf["state"], "width": bf["width"], "path": path, "content": old["content"]}
            if old["fontStyle"] != new["fontStyle"]:
                summary["fontStyleChanges"].append({**identity, "before": old["fontStyle"], "after": new["fontStyle"]})
            if bf["state"] == "Navigation" and (path.endswith("/Map Title") or path.endswith("/Water Food Endurance") or path.endswith("/MapCourseHint")):
                summary["navigationDensity"].append({**identity, "before": stat(old), "after": stat(new),
                    "beforeProjectedYToRasterDensity": old["projectedPixelsPerLocalUnit"]["y"] / max(.0001, old["renderedPixelsPerUnit"]),
                    "afterProjectedYToRasterDensity": new["projectedPixelsPerLocalUnit"]["y"] / max(.0001, new["renderedPixelsPerUnit"])})
            if bf["state"] == "Settlement" and "SettlementTextCentering" in old.get("meshEffects", []):
                summary["settlementY"].append({**identity, "before": stat(old), "after": stat(new),
                    "meshBottomShiftPixels": new["meshPixels"]["y"] - old["meshPixels"]["y"]})
            if not old["meshAvailable"] or not new["meshAvailable"] or old["rendererCulled"] or new["rendererCulled"]: continue
            box = crop_box(old, new, bf["width"], bf["height"])
            if box[2] <= box[0] or box[3] <= box[1]: continue
            row_id = f"{bf['state']}-{bf['width']}-{len(pairs):03d}"
            pair = {"id": row_id, **identity, "before": stat(old), "after": stat(new), "samePixelCropBox": list(box),
                    "beforeFile": f"crops/{row_id}-before.png", "afterFile": f"crops/{row_id}-after.png"}
            before_image.crop(box).save(output / pair["beforeFile"]); after_image.crop(box).save(output / pair["afterFile"])
            pairs.append(pair)
    for width, label in ((1920, "1080p"), (3840, "4K")):
        navigation = [r for r in pairs if r["state"] == "Navigation" and r["width"] == width and
                      (r["path"].endswith("/Voyage Header Title") or r["path"].endswith("/Voyage Cargo/Label") or r["path"].endswith("/Voyage Settings/Label")
                       or r["path"].endswith("/Map Title") or r["path"].endswith("/Water Food Endurance"))]
        settlement = []
        for token in ("/Settlement Reward Gold", "/Settlement Title", "/Settlement Featured Name", "/Settlement Resource Wood", "/Settlement Badge"):
            candidates = [r for r in pairs if r["state"] == "Settlement" and r["width"] == width and r["path"].endswith(token)]
            if candidates: settlement.append(candidates[0])
        for name, title, rows in ((f"navigation-{width}.png", f"{label} 导航标题、按钮与地图读数", navigation),
                                  (f"settlement-{width}.png", f"{label} 结算文字最终像素相位", settlement)):
            composed = compose(output / name, title, rows, output)
            if composed: summary["composites"].append(composed)
    summary["comparedTextCount"] = len(pairs)
    summary["notes"] = ["All comparison crops use the union of before/after final CanvasRenderer mesh bounds with equal screen-coordinate crop boxes.",
        "Ordinary Text fractional vertex values can be legitimate. Y residuals here diagnose the targeted SettlementTextCentering correction only.",
        "Matching SHA-256 proves these deterministic snapshots are unchanged, not every gameplay state."]
    (output / "summary.json").write_text(json.dumps(summary, ensure_ascii=False, indent=2), encoding="utf-8")
    write_html(output, pairs, summary)
    lines = ["其他界面文字清晰度检查与对比", "", "修复对照：导航标题/船舱/设置按钮取消合成粗体；地图标题与补给读数修正父级放大导致的采样密度不足；结算最终网格Y相位校正。",
        "记录实际字号、PPU、屏幕投影密度、画布比例与localScale；普通Text和其他界面另作验证。", "",
        f"基线：{before_report['frameCount']}帧，{before_report['textCount']}项文字，状态{before_report['status']}。",
        f"修复：{after_report['frameCount']}帧，{after_report['textCount']}项文字，状态{after_report['status']}。",
        f"有效原像素文字对照：{len(pairs)}；样式改变：{len(summary['fontStyleChanges'])}；未匹配项：{len(summary['unmatchedText'])}。", ""]
    for row in summary["fontStyleChanges"]: lines.append(f"{row['state']} {row['width']} {row['content']}: {row['before']} → {row['after']}。")
    for row in summary["navigationDensity"]:
        old, new = row["before"], row["after"]
        lines.append(f"导航 {row['width']} {row['content']}: 字号{old['fontSize']}→{new['fontSize']}；PPU {old['renderedPixelsPerUnit']:.4f}→{new['renderedPixelsPerUnit']:.4f}；实际投影Y {old['projectedPixelsPerLocalUnit']['y']:.4f}→{new['projectedPixelsPerLocalUnit']['y']:.4f}px/单位；localScaleY {old['localScale']['y']:.6f}→{new['localScale']['y']:.6f}。")
    lines += ["", "其他界面完整PNG与解码像素SHA-256："]
    for row in summary["otherScreens"]: lines.append(f"{row['state']} {row['width']}×{row['height']}: PNG={'相同' if row['pngSha256Equal'] else '不同'}，像素={'相同' if row['pixelSha256Equal'] else '不同'}。")
    for width in (1920, 3840):
        rows = [r for r in summary["settlementY"] if r["width"] == width]
        if rows:
            before = max(r["before"].get("maximumMeshVertexYResidual", 0) or 0 for r in rows)
            after = max(r["after"].get("maximumMeshVertexYResidual", 0) or 0 for r in rows)
            shift = max(abs(r["meshBottomShiftPixels"]) for r in rows)
            lines.append(f"结算 {width}: {len(rows)}项最终网格；Y最大残差 {before:.6f}→{after:.6f}px；本次最大底边位移 {shift:.6f}px。")
    lines += ["", "裁图直接来自实际Unity全帧；前后采用同一个屏幕整数像素框，没有缩放、锐化或重新居中。",
        "小数像素本身不等于错误，曲线/旋转字形可合法包含小数。SHA一致仅证明本次预览捕获一致。",
        "查看 index.html、navigation-1920.png、navigation-3840.png、settlement-1920.png、settlement-3840.png、summary.json。"]
    (output / "检查与对比.txt").write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(json.dumps({"output": str(output), "comparedTextCount": len(pairs), "fontStyleChanges": len(summary["fontStyleChanges"]),
        "otherFramesPixelIdentical": sum(r["pixelSha256Equal"] for r in summary["otherScreens"]), "composites": summary["composites"]}, ensure_ascii=False, indent=2))


if __name__ == "__main__": main()
