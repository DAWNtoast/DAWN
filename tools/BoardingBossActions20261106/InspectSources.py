"""Create a dark-deck review of generated cels; inspect the existing alpha without changing art."""
import json
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "outputs/boarding-boss-actions-20261106"
REVIEW = OUT / "review"
ACTIONS = ("Attack", "Hit", "Cast", "Guard")

def split(projection, count):
    splits = [0]
    width = len(projection) / count
    for i in range(1, count):
        center = width * i
        lo, hi = round(center - width*.24), round(center + width*.24)
        zone = projection[lo:hi+1]
        indices = np.where(zone == zone.min())[0] + lo
        splits.append(int(indices[np.argmin(abs(indices-center))]))
    return splits + [len(projection)]


def source_review(records):
    info = []
    thumbs = {}
    for record in records:
        image_path = Path(record["source"])
        if not image_path.is_file():
            image_path = OUT / "sources" / record["id"] / (record["action"] + ".png")
        with Image.open(image_path) as src:
            im = src.convert("RGBA")
        alpha = np.asarray(im.getchannel("A"))
        mask = alpha >= 100
        cols, nr = record.get("grid", [3,2])
        ys = split(mask.sum(1), nr)
        row = Image.new("RGB",(6*194,155), "#203442")
        draw = ImageDraw.Draw(row)
        stats = []
        for r in range(nr):
            top,bot = ys[r:r+2]
            xs = split(mask[top:bot].sum(0),cols)
            for c in range(cols):
                x, right = xs[c:c+2]
                cell = im.crop((x,top,right,bot))
                shape = mask[top:bot,x:right]
                box = cell.getchannel("A").point(lambda v:255 if v>=80 else 0).getbbox()
                if box is None:
                    stats.append({"empty":True})
                    continue
                l,t,rx,b=box
                stats.append(dict(width=rx-l,height=b-t,edgePixels=int(shape[0].sum()+shape[-1].sum()+shape[:,0].sum()+shape[:,-1].sum()),
                                  centerAlpha=int(np.median(alpha[top:bot,x:right])),fill=round(float(shape.mean()),3)))
                if np.percentile(alpha,15)<15:
                    cell = cell.crop((max(0,l-2),max(0,t-2),min(cell.width,rx+2),min(cell.height,b+2)))
                cell.thumbnail((190,124),Image.Resampling.LANCZOS)
                i = r*cols+c
                row.paste(cell,(i*194+(194-cell.width)//2,128-cell.height),cell)
                draw.text((i*194+5,132),record["action"]+"  "+str(i+1),fill="#e9dcba")
        id = record["id"]
        thumbs[(id,record["action"])]=row
        info.append(dict(id=id,action=record["action"],size=im.size,alphaP15=round(float(np.percentile(alpha,15)),2),
                         alphaP30=round(float(np.percentile(alpha,30)),2),stats=stats))
    for key in {i for i,a in thumbs}:
        keys=[(key,a) for a in ACTIONS if (key,a) in thumbs]
        board=Image.new("RGB",(1164,155*len(keys)),"#203442")
        for i,k in enumerate(keys):
            board.paste(thumbs[k],(0,i*155))
        board.save(REVIEW/(key+"-sources.jpg"),quality=91)
    (REVIEW/"source-measurements.json").write_text(json.dumps(info,indent=2),encoding="utf-8")
    print("Reviewed", len(info), "sheets across",len({v["id"] for v in info}),"formations")
    for v in info:
        if v["alphaP15"]>12 or v["alphaP30"]>30:
            print("CHECK MATTE",v["id"],v["action"],v["alphaP15"],v["alphaP30"])


def main():
    REVIEW.mkdir(parents=True,exist_ok=True)
    rec=[]
    for p in sorted((OUT/"generation").glob("*.json")):
        revision=OUT/"revisions"/p.name
        rec.append(json.loads((revision if revision.exists() else p).read_text(encoding="utf-8-sig")))
    source_review(rec)


if __name__=="__main__":
    main()
