"""Build a small side-by-side movie and a roster proof from final production atlas pixels."""
import json
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "outputs/boarding-remaster-20261106/actors"
ART = ROOT / "UnityClient/Assets/_Project/Resources/BoardingBattle/Actions"
FONT = "C:/Windows/Fonts/msyh.ttc"
TITLES = ["攻击", "施法", "受击", "格挡", "死亡"]


def frame(im, action, n, size):
    box = (n*256,action*256,(n+1)*256,(action+1)*256)
    return im.crop(box).resize((size,size),Image.Resampling.LANCZOS)


def main():
    sample = ["captain","crew_salt_crown_diver_isha","crew_white_shell_doctor_mirei","enemy_party_center"]
    titles = ["我方船长","伊沙","米蕾","敌方船长"]
    small = ImageFont.truetype(FONT,16)
    large = ImageFont.truetype(FONT,21)
    atlases = [Image.open(ART/(key+".png")).convert("RGBA") for key in sample]
    frames = []
    size = 152
    for n in range(6):
        stage=Image.new("RGB",(size*5,38+(size+22)*4),"#26323d")
        draw=ImageDraw.Draw(stage)
        for j,title in enumerate(TITLES):
            draw.text((j*size+size//2,18),title,font=large,fill="#f2ddb2",anchor="mm")
        for i,atlas in enumerate(atlases):
            top=38+i*(size+22)
            draw.rectangle((0,top,stage.width,top+size+20),fill=("#1c2b35" if i%2==0 else "#22333d"))
            for a in range(5):
                sprite=frame(atlas,a,n,size)
                stage.paste(sprite,(a*size,top),sprite)
            draw.text((10,top+size-1),titles[i],font=small,fill="#dfd0ac")
        frames.append(stage)
    frames[3].save(OUT/"双方动作对比.jpg",quality=91)
    palette = Image.new("RGB",(380, len(frames)*(frames[0].height//2)))
    for i,im in enumerate(frames):
        half=im.resize((380, im.height//2))
        palette.paste(half,(0,i*half.height))
    palette=palette.quantize(colors=256)
    pal=[im.quantize(palette=palette,dither=Image.Dither.NONE) for im in frames]
    target=OUT/"双方动作对比.gif"
    pal[0].save(target,save_all=True,append_images=pal[1:],duration=[220,165,140,150,170,500],loop=0,optimize=False)
    with Image.open(target) as final:
        assert final.n_frames==6
        for n in range(6):
            final.seek(n)
            final.load()
    roster=json.loads((OUT/"roster.json").read_text(encoding="utf-8"))
    names={r["key"]:r["name"]+" · "+r["title"] for r in roster}
    names.update(dict(captain="船长",enemy_party_left="敌方 · 剑士",enemy_party_center="敌方 · 船长",enemy_party_right="敌方 · 斧手"))
    for page,keys in enumerate([list(names)[i:i+5] for i in range(0,len(names),5)]):
        canvas=Image.new("RGB",(900, len(keys)*185),"#1d303c")
        draw=ImageDraw.Draw(canvas)
        for i,key in enumerate(keys):
            with Image.open(ART/(key+".png")) as im:
                for a in range(5):
                    sprite=frame(im,a,4 if a==4 else 3,160)
                    canvas.paste(sprite,(a*180,i*185),sprite)
            draw.text((10,i*185+159),names[key],font=small,fill="#f4ddb2")
        canvas.save(OUT/f"roster-proof-{page+1}.jpg",quality=91)


if __name__=="__main__":
    main()
