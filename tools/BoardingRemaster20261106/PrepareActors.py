"""Crop production crew silhouettes for identity references and list legal boarding cards."""
import json
import re
from pathlib import Path

from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
DEST = ROOT / "outputs/boarding-remaster-20261106/actors"
RES = ROOT / "UnityClient/Assets/_Project/Resources/BoardingBattle/Characters"
ROLES = ("navigator", "carpenter", "doctor", "gunner", "cook", "diver")


def main():
    refs = DEST / "references"
    refs.mkdir(parents=True, exist_ok=True)
    data = json.loads((ROOT / "outputs/card-list-by-school-20261023/card-list-data.json").read_text(encoding="utf-8-sig"))
    regions = (ROOT / "UnityClient/Assets/_Project/Presentation/Runtime/BoardingCrewAtlasLayout.cs").read_text(encoding="utf-8-sig")
    roster = []
    for card in data["Cards"]:
        if not card["Id"].startswith("card_special_crew_"):
            continue
        variant = next((v for v in card["StageVariants"] if "接舷" in v["Stages"]), None)
        if not variant:
            continue
        key = card["Id"].removeprefix("card_special_")
        port, role, _ = key.removeprefix("crew_").rsplit("_", 2)
        atlas = "boarding_crew_" + port + "_v2"
        line = next(line for line in regions.splitlines() if ('"' + atlas + '"') in line)
        boxes = re.findall(r"Region\((\d+), (\d+), (\d+), (\d+)\)", line)
        with Image.open(RES / (atlas + ".png")) as src:
            box = tuple(round(int(v) * src.width / 1254) for v in boxes[ROLES.index(role)])
            crop = src.crop(box)
            crop.save(refs / (key + ".png"))
        roster.append(dict(key=key, name=card["Source"].split("：")[-1], card=card["Id"], title=variant["Name"],
                           effect=variant["Effect"], school=card["School"], reference=atlas, box=box))
    (DEST / "roster.json").write_text(json.dumps(roster, ensure_ascii=False, indent=2), encoding="utf-8")
    board = Image.new("RGB", (750, 3 * 228), "#26303a")
    draw = ImageDraw.Draw(board)
    for i, item in enumerate(roster):
        with Image.open(refs / (item["key"] + ".png")) as src:
            thumb = src.convert("RGBA")
        thumb.thumbnail((145, 197))
        x, y = i % 5 * 150, i // 5 * 228
        board.paste(thumb, (x+(150-thumb.width)//2,y+4), thumb)
        draw.text((x+6, y+202), item["key"].split("_")[-1], fill="white")
    board.save(refs / "crew-contact.jpg", quality=92)
    print(len(roster))


if __name__ == "__main__":
    main()
