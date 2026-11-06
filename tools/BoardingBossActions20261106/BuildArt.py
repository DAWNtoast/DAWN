"""Pack four painted performances per named boarding party, grounded in the original group image."""
import argparse
import hashlib
import json
import re
import shutil
import uuid
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "outputs/boarding-boss-actions-20261106"
RES = ROOT / "UnityClient/Assets/_Project/Resources"
DEST = RES / "BoardingBattle/BossActions"
TEMPLATE = RES / "BattleVfx/NavalV2/naval_fx_remaster_v2.png.meta"
CELL = (384,256)
ACTIONS = ("Attack","Hit","Cast","Guard")
IDENTITIES = ("enemy_ember_ark","enemy_ghost","enemy_imperial","enemy_iron_oath","enemy_last_horizon",
              "enemy_moon_weave","enemy_red_widow","enemy_route_gold","enemy_route_moon","enemy_route_reef",
              "enemy_thunder_crown","enemy_tide_requiem","enemy_treasure")


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def json_file(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def pixel_bounds(im):
    box = im.getchannel("A").point(lambda n: 255 if n>=64 else 0).getbbox()
    if not box:
        raise ValueError("Empty pose")
    return box


def feet_center(im):
    """Anchor the supporting feet rather than the tip of an extended weapon."""
    alpha=np.array(im.getchannel("A"))
    x,y,r,b=pixel_bounds(im)
    band=alpha[max(y,b-max(3,round((b-y)*.10))):b,x:r]>=96
    xs=np.where(band)[1]+x
    return (np.percentile(xs,8)+np.percentile(xs,92))/2 if len(xs) else (x+r)/2


def cuts(projection, count):
    boundaries=[0]
    cell=len(projection)/count
    for i in range(1,count):
        center=cell*i
        lo,hi=round(center-cell*.24),round(center+cell*.24)
        local=projection[lo:hi+1]
        # Prefer a wide clear gutter nearest the expected boundary.
        candidates=np.flatnonzero(local==local.min())+lo
        boundaries.append(int(candidates[np.argmin(abs(candidates-center))]))
    return boundaries+[len(projection)]


def poses_from(source, grid=(3,2)):
    with Image.open(source) as original:
        art=original.convert("RGBA")
    alpha=np.asarray(art.getchannel("A"))
    if float(np.percentile(alpha,15))>12:
        raise ValueError("Visible background outside the cutouts")
    mask=alpha>=90
    cols,rows=grid
    ys=cuts(mask.sum(1),rows)
    crops=[]
    metadata=[]
    for top,bottom in zip(ys,ys[1:]):
        xs=cuts(mask[top:bottom].sum(0),cols)
        for left,right in zip(xs,xs[1:]):
            cell=art.crop((left,top,right,bottom))
            x,y,r,b=pixel_bounds(cell)
            if r-x<cell.width*.36 or b-y<cell.height*.30:
                raise ValueError(f"Incomplete formation in cell {len(crops)+1}")
            crop=(max(0,x-2),max(0,y-2),min(cell.width,r+2),min(cell.height,b+2))
            crops.append(cell.crop(crop))
            metadata.append((left+crop[0],top+crop[1],left+crop[2],top+crop[3]))
    return crops,metadata


def normalize(crops,idle):
    ox,oy,orr,ground=pixel_bounds(idle)
    ref_center=feet_center(idle)
    # The end poses are close to ready; use the same uniform scale across the movement.
    # Keep a complete weapon and head even when an active cel uses more of the viewport.
    near_ready=np.median([pixel_bounds(c)[3]-pixel_bounds(c)[1] for c in (crops[0],crops[-1])])
    scale=min((ground-oy)/near_ready,(CELL[0]-3)/max(c.width for c in crops),
              (ground-1)/max(c.height for c in crops))
    aligned=[]
    for crop in crops:
        size=(max(1,round(crop.width*scale)),max(1,round(crop.height*scale)))
        im=crop.resize(size,Image.Resampling.LANCZOS)
        left=int(np.clip(round(ref_center-feet_center(im)),0,CELL[0]-size[0]))
        top=ground-pixel_bounds(im)[3]
        if top<0 or top+im.height>CELL[1]+1:
            raise ValueError("Grounding would clip pose")
        tile=Image.new("RGBA",CELL)
        tile.alpha_composite(im,(left,top))
        if abs(pixel_bounds(tile)[3]-ground)>1:
            raise ValueError("Ground line differs between cels")
        aligned.append(tile)
    return [idle,*aligned,idle],round(scale,5)


def meta(path,source):
    dest=Path(str(path)+".meta")
    guid=uuid.uuid5(uuid.NAMESPACE_URL,path.relative_to(ROOT).as_posix()).hex
    data=re.sub(r"(?m)^guid: .+$","guid: "+guid,source)
    if path.suffix==".png":
        data=re.sub(r"(?m)^  userData:.*$","  userData: Boarding boss; 8 columns x 4 actions (Attack Hit Cast Guard); RGBA.",data)
    if dest.exists():
        if ("guid: "+guid) not in dest.read_text(encoding="utf-8-sig"):
            raise ValueError("Unexpected existing metadata: "+str(dest))
    else:
        dest.write_text(data,encoding="utf-8")


def pack(identity,available):
    original=RES/"BoardingBattle/Characters"/("boarding_"+identity+"_v1.png")
    with Image.open(original) as im:
        idle=im.convert("RGBA").resize(CELL,Image.Resampling.LANCZOS)
    frames={}
    records={}
    for action in ACTIONS:
        key=identity+"_"+action
        path=OUT/"generation"/(key+".json")
        if not path.is_file():
            if available: continue
            raise ValueError("Missing generation: "+key)
        revision=OUT/"revisions"/(key+".json")
        description=json_file(revision if revision.exists() else path)
        target=OUT/"sources"/identity/(action+".png")
        target.parent.mkdir(parents=True,exist_ok=True)
        generated=Path(description["source"])
        if generated.is_file() and (not target.is_file() or sha(target)!=sha(generated)):
            shutil.copyfile(generated,target)
        try:
            crops,boxes=poses_from(target,description.get("grid",(3,2)))
            row,scale=normalize(crops,idle)
            frames[action]=row
            records[action]={"source":str(target.relative_to(OUT)),"sha256":sha(target),"crops":boxes,"uniformScale":scale}
        except ValueError as ex:
            if not available: raise ValueError(f"{key}: {ex}") from ex
            print("REVIEW",key,ex)
    if len(frames)!=len(ACTIONS):
        return None
    sheet=Image.new("RGBA",(8*CELL[0],4*CELL[1]))
    thumb=Image.new("RGB",(8*157,len(ACTIONS)*121),"#1d303e")
    draw=ImageDraw.Draw(thumb)
    for i,action in enumerate(ACTIONS):
        for j,frame in enumerate(frames[action]):
            sheet.alpha_composite(frame,(j*CELL[0],i*CELL[1]))
            small=frame.resize((153,102),Image.Resampling.LANCZOS)
            thumb.paste(small,(j*157,i*121),small)
        draw.text((5,i*121+104),action,fill="#efdab1")
    (OUT/"art-review").mkdir(exist_ok=True)
    thumb.save(OUT/"art-review"/(identity+".jpg"),quality=92)
    path=DEST/(identity+".png")
    sheet.save(path)
    meta(path,TEMPLATE.read_text(encoding="utf-8"))
    return {"identity":identity,"atlas":str(path.relative_to(ROOT)),"atlasSha256":sha(path),"bytes":path.stat().st_size,
            "idleSource":str(original.relative_to(ROOT)),"idleSourceSha256":sha(original),"actions":records}


def main():
    p=argparse.ArgumentParser()
    p.add_argument("--available",action="store_true")
    p.add_argument("--only",nargs="+",choices=IDENTITIES)
    args=p.parse_args()
    DEST.mkdir(parents=True,exist_ok=True)
    meta(DEST,Path(str(DEST.parent/"Characters")+".meta").read_text(encoding="utf-8"))
    result=[]
    for identity in args.only or IDENTITIES:
        info=pack(identity,args.available)
        if info: result.append(info)
    if result:
        target=OUT/"art-validation.json"
        previous={x["identity"]:x for x in json_file(target)} if target.exists() else {}
        previous.update({x["identity"]:x for x in result})
        target.write_text(json.dumps(list(previous.values()),indent=2),encoding="utf-8")
    print("Packed",len(result),"complete formations")


if __name__=="__main__":
    main()
