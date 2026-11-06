"""Inspect original or revised defeat sheets beside each formation's game reference."""
import json
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
BASE = ROOT / "outputs/boarding-remaster-20261106/actors/defeats"
DEST = BASE / "source-review"


def gutter(occupancy):
    middle = len(occupancy) // 2
    distance = round(len(occupancy) * .115)
    search = occupancy[middle-distance:middle+distance+1]
    options = np.flatnonzero(search == np.min(search)) + middle-distance
    return int(options[np.argmin(np.abs(options-middle))])


def main():
    DEST.mkdir(parents=True, exist_ok=True)
    report = {}
    files = sorted((BASE / "generation").glob("*.json"))
    for first in range(0, len(files), 4):
        batch = files[first:first + 4]
        sheet = Image.new("RGB", (1100, 182 * len(batch)), "#1f303a")
        draw = ImageDraw.Draw(sheet)
        for row, file in enumerate(batch):
            revision = BASE / "revisions" / file.name
            record = json.loads((revision if revision.is_file() else file).read_text(encoding="utf-8-sig"))
            with Image.open(record["source"]) as image:
                source = image.convert("RGBA")
            with Image.open(record["ref"]) as image:
                original = image.convert("RGBA")
            alpha = np.array(source.getchannel("A"))
            frames, heights = [], []
            solid = alpha > 90
            split = gutter(solid.sum(1))
            for top, bottom in ((0,split),(split,source.height)):
                cross = gutter(solid[top:bottom].sum(0))
                for left, right in ((0,cross),(cross,source.width)):
                    tile = source.crop((left,top,right,bottom))
                    bounds = tile.getchannel("A").point(lambda v:255 if v >= 120 else 0).getbbox()
                    heights.append(0 if bounds is None else bounds[3]-bounds[1])
                    frames.append(tile)
            for col, frame in enumerate([original,*frames]):
                frame.thumbnail((214,151),Image.Resampling.LANCZOS)
                x, y = col*220+(220-frame.width)//2, row*182+151-frame.height
                sheet.paste(frame,(x,y),frame)
            values = list(map(float,np.percentile(alpha,(1,10,20,50))))
            name = record["key"]
            report[name] = {"alphaP01P10P20P50":values, "alphaGT230":round(float((alpha>230).mean()),3),
                            "coverageOver10":round(float((alpha>10).mean()),3),"cellHeightsAt120":heights,
                            "source":record["source"],"revised":revision.is_file()}
            draw.text((6,row*182+154),f"{name}  p20={values[2]:.1f}  heights={heights}",fill="#eee5d4")
        sheet.save(DEST / f"poses-{first//4+1}.jpg",quality=94)
    (DEST / "alpha.json").write_text(json.dumps(report,indent=2),encoding="utf-8")
    for name, check in report.items():
        print(name, "alphaP20", check["alphaP01P10P20P50"][2], "heights", check["cellHeightsAt120"])

if __name__ == "__main__":
    main()
