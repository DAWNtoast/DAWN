"""A compact comparison of four verified GIFs sampled from the Unity renderer."""
from bisect import bisect_right
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ROOT=Path(__file__).resolve().parents[2]
SRC=ROOT/"outputs/boarding-remaster-20261106/validation/named-enemies"
DEST=ROOT/"outputs/boarding-boss-actions-20261106"
CASES=(("enemy_red_widow","Attack","绯刃寡妇号 · 攻击"),
       ("enemy_thunder_crown","Cast","雷冠审判号 · 施法"),
       ("enemy_iron_oath","Hit","铁誓不沉号 · 受击"),
       ("enemy_treasure","Guard","沉月王庭守藏舰 · 格挡"))


def main():
    clips=[]
    stops={0}
    for identity,action,_ in CASES:
        frames=[]
        ends=[]
        with Image.open(SRC/identity/(action+".gif")) as src:
            for i in range(src.n_frames):
                src.seek(i)
                frames.append(src.convert("RGB").resize((360,245),Image.Resampling.LANCZOS))
                ends.append((ends[-1] if ends else 0)+src.info.get("duration",80))
        clips.append((frames,ends))
        stops.update(ends)
    times=sorted(stops)
    output=[]
    font=ImageFont.truetype("C:/Windows/Fonts/msyh.ttc",18)
    for t in times[:-1]:
        canvas=Image.new("RGB",(720,552),"#192933")
        draw=ImageDraw.Draw(canvas)
        for j,((frames,ends),(_,_,name)) in enumerate(zip(clips,CASES)):
            x,y=j%2*360,j//2*276
            canvas.paste(frames[min(len(frames)-1,bisect_right(ends,t))],(x,y+31))
            draw.text((x+13,y+2),name,font=font,fill="#e6dcc4")
        output.append(canvas)
    samples=Image.new("RGB",(180,138*len(output)))
    for i,frame in enumerate(output):
        samples.paste(frame.resize((180,138)),(0,138*i))
    palette=samples.quantize(colors=256)
    images=[frame.quantize(palette=palette,dither=Image.Dither.NONE) for frame in output]
    durations=[b-a for a,b in zip(times,times[1:])]
    durations[-1]+=300
    path=DEST/"接舷Boss四动作.gif"
    images[0].save(path,save_all=True,append_images=images[1:],duration=durations,loop=0,optimize=False)
    with Image.open(path) as check:
        assert check.n_frames>=4
        for i in range(check.n_frames):
            check.seek(i)
            check.load()
    print(path.name,path.stat().st_size)


if __name__=="__main__":
    main()
