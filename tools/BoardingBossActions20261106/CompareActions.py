"""Compare the archived and current production GIFs using relative links that work offline."""
import argparse
import hashlib
import json
import os
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/"outputs/boarding-boss-actions-20261106"
REVIEW=OUT/"review"
LIVE=ROOT/"outputs/boarding-remaster-20261106/validation/named-enemies"
BEFORE=OUT/"source-before"/LIVE.relative_to(ROOT)
ACTIONS=("Attack","Hit","Cast","Guard")
ZH={"Attack":"攻击","Hit":"受击","Cast":"施法","Guard":"格挡"}


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def manifest():
    info=json.loads((LIVE/"manifest.json").read_text(encoding="utf-8-sig"))
    return {r["identity"]:r["displayName"] for r in info["clips"] if r["action"]=="Attack"}


def old_proof(names):
    metrics=[]
    ids=list(names)
    for start in range(0,len(ids),4):
        batch=ids[start:start+4]
        board=Image.new("RGB",(920,151*len(batch)),"#0d2029")
        draw=ImageDraw.Draw(board)
        for i,key in enumerate(batch):
            for j,action in enumerate(ACTIONS):
                file=BEFORE/key/(action+".gif")
                with Image.open(file) as im:
                    frames=[]
                    for n in range(im.n_frames):
                        im.seek(n)
                        frames.append(im.convert("RGB"))
                base=np.asarray(frames[0]).astype("int16")
                bg=np.median(base[:8,:12].reshape(-1,3),axis=0)
                masks=[np.max(np.abs(np.asarray(f).astype("int16")-bg),2)>30 for f in frames]
                diffs=[int((masks[0]^m).sum()) for m in masks]
                peak=int(np.argmax(diffs))
                basepixels=max(1,masks[0].sum())
                metrics.append(dict(identity=key,action=action,sha256=digest(file),decodedFrames=len(frames),
                                    silhouetteChange=round(diffs[peak]/basepixels,4),sampledPeakFrame=peak))
                if j==0:
                    board.paste(frames[0].resize((184,125),Image.Resampling.LANCZOS),(0,i*151))
                board.paste(frames[peak].resize((184,125),Image.Resampling.LANCZOS),((j+1)*184,i*151))
                draw.text(((j+1)*184+5,i*151+126),action+" "+str(peak+1),fill="#eed8aa")
            draw.text((5,i*151+126),key,fill="white")
        board.save(REVIEW/f"previous-{start//4+1}.jpg",quality=92)
    (REVIEW/"previous-movement.json").write_text(json.dumps(metrics,indent=2),encoding="utf-8")
    print("Archived poses inspected:",len(metrics))


def build_page(names):
    data={}
    checks=[]
    for key,name in names.items():
        clips={}
        for action in ACTIONS:
            old,new=BEFORE/key/(action+".gif"),LIVE/key/(action+".gif")
            before_hash, after_hash = digest(old), digest(new)
            if before_hash == after_hash:
                raise ValueError(f"Expected a new capture: {key} {action}")
            frames = []
            for path in [old,new]:
                with Image.open(path) as im:
                    if im.format != "GIF" or im.n_frames < 2:
                        raise ValueError(f"Expected an animated GIF at {path}")
                    frames.append(im.n_frames)
                    for i in range(im.n_frames):
                        im.seek(i)
                        im.load()
            checks.append(dict(identity=key, action=action, beforeSha256=before_hash, afterSha256=after_hash,
                               beforeFrames=frames[0], afterFrames=frames[1]))
            clips[action]=[os.path.relpath(p,REVIEW).replace(os.sep,"/") for p in (new,old)]
        data[key]={"name":name,"motions":clips}
    template=(Path(__file__).parent/"compare-template.html").read_text(encoding="utf-8")
    (REVIEW/"index.html").write_text(template.replace("/* DATA */",json.dumps(data,ensure_ascii=False).replace("</","<\\/")),
                                    encoding="utf-8")
    (REVIEW/"comparison-checks.json").write_text(json.dumps(checks,indent=2),encoding="utf-8")
    print("Comparison available for",len(data)*len(ACTIONS),"pairs")


if __name__=="__main__":
    parser=argparse.ArgumentParser()
    parser.add_argument("--before",action="store_true")
    args=parser.parse_args()
    REVIEW.mkdir(exist_ok=True)
    if args.before:old_proof(manifest())
    else:build_page(manifest())
