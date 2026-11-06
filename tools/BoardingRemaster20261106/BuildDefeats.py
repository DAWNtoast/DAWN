"""Pack painted full-party collapses to match the original production formation rectangle."""
import argparse
import json
import shutil
import sys
from pathlib import Path

import numpy as np
from PIL import Image,ImageDraw

sys.dont_write_bytecode = True
from BuildActorArt import ROOT, OUT, META, lines, trim, meta_file, digest, read

BASE=OUT/"defeats"
DEST=ROOT/"UnityClient/Assets/_Project/Resources/BoardingBattle/Defeats"
CELL=(384,256)


def bounds(im):
    return im.getchannel("A").point(lambda x:255 if x>=64 else 0).getbbox()


def build(record):
    key=record["key"]
    local=BASE/"sources"/(key+".png")
    local.parent.mkdir(exist_ok=True)
    candidate=Path(record["source"])
    if candidate.exists() and (not local.exists() or digest(local)!=digest(candidate)):
        shutil.copyfile(candidate,local)
    with Image.open(local) as source:
        paint=source.convert("RGBA")
    with Image.open(record["ref"]) as original:
        original=original.convert("RGBA").resize(CELL,Image.Resampling.LANCZOS)
    a=np.array(paint.getchannel("A"))
    if np.percentile(a,20)>10:
        raise ValueError(f"{key}: matte needs review, alpha P20={np.percentile(a,20):.1f}")
    ys=lines((a>90).sum(1),2)
    poses=[]
    for top,bottom in zip(ys,ys[1:]):
        xs=lines((a[top:bottom]>90).sum(0),2)
        poses.extend(trim(paint.crop((x,top,right,bottom))) for x,right in zip(xs,xs[1:]))
    bx,by,br,ground=bounds(original)
    scale=min((br-bx)/max(im.width for im in poses),(ground-by)*.98/poses[0].height,
              (ground-2)/max(im.height for im in poses))
    frames=[original]
    for source in poses:
        im=source.resize((max(1,round(source.width*scale)),max(1,round(source.height*scale))),Image.Resampling.LANCZOS)
        x,y,r,b=bounds(im)
        left=round((br+bx-x-r)/2)
        left=min(max(0,left),CELL[0]-im.width)
        frame=Image.new("RGBA",CELL)
        frame.alpha_composite(im,(left,ground-b))
        frames.append(frame)
    frames.append(frames[-1])
    strip=Image.new("RGBA",(6*CELL[0],CELL[1]))
    for i,frame in enumerate(frames):
        strip.alpha_composite(frame,(i*CELL[0],0))
    dest=DEST/(key+".png")
    strip.save(dest)
    template=META.read_text(encoding="utf-8")
    meta_file(dest,template)
    meta=Path(str(dest)+".meta")
    text=meta.read_text(encoding="utf-8").replace("Boarding actor; 6 columns x 5 actions; RGBA.","Boarding named formation defeat; 6 columns by 1 row; RGBA.")
    meta.write_text(text,encoding="utf-8")
    return dict(key=key,sourceSha256=digest(local),atlasSha256=digest(dest),footRow=ground-1,
                silhouetteHeights=[bounds(f)[3]-bounds(f)[1] for f in frames])


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument("--available",action="store_true")
    args=parser.parse_args()
    files=sorted((BASE/"generation").glob("*.json"))
    if not args.available and len(files)!=13:
        raise ValueError(f"Expected 13 named parties, found {len(files)}")
    DEST.mkdir(parents=True,exist_ok=True)
    meta_file(DEST,Path(str(DEST.parent/"Characters")+".meta").read_text(encoding="utf-8"))
    results=[]
    for file in files:
        record=read(file)
        revision=BASE/"revisions"/file.name
        if revision.exists():
            record=read(revision)
        try:
            results.append(build(record))
        except ValueError as error:
            if not args.available: raise
            print(error)
    for page in range(0,len(results),5):
        portion=results[page:page+5]
        sheet=Image.new("RGB",(1000,148*len(portion)),"#20313e")
        draw=ImageDraw.Draw(sheet)
        for i,result in enumerate(portion):
            with Image.open(DEST/(result["key"]+".png")) as atlas:
                for n in range(5):
                    tile=atlas.crop((n*384,0,(n+1)*384,256)).resize((192,128),Image.Resampling.LANCZOS)
                    sheet.paste(tile,(n*200,i*148),tile)
            draw.text((6,i*148+131),result["key"],fill="#ece3cf")
        sheet.save(BASE/f"formation-proof-{page//5+1}.jpg",quality=92)
    (BASE/"validation.json").write_text(json.dumps(results,indent=2),encoding="utf-8")
    print("Packed",len(results),"formations")


if __name__=="__main__":
    main()
