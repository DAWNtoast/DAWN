"""Compare generated alpha at practical GIF transparency thresholds on a dark backdrop."""
from pathlib import Path
import json
import numpy as np
from PIL import Image, ImageDraw

DEST = Path(__file__).resolve().parents[2] / "outputs/boarding-remaster-20261106/actors"


def main():
    files = sorted((DEST / "generation").glob("*.json"))
    board = Image.new("RGB", (720, len(files) * 178), "#233643")
    draw = ImageDraw.Draw(board)
    for i, data in enumerate(files):
        source = Path(json.loads(data.read_text(encoding="utf-8"))["source"])
        with Image.open(source) as im:
            image = im.convert("RGBA")
        a = np.array(image.getchannel("A"))
        print(data.stem, image.size, np.percentile(a, (0, 10, 50, 85, 100)))
        for j, threshold in enumerate((0, 128, 212, 232)):
            view = image.copy()
            if threshold:
                view.putalpha(Image.fromarray((a >= threshold).astype("uint8") * 255))
            view.thumbnail((175, 152))
            board.paste(view, (j*180, i*178), view)
            draw.text((j*180, i*178+153), data.stem.split("_")[-1]+" "+str(threshold),fill="white")
    board.save(DEST / "references/source-alpha-review.jpg", quality=90)


if __name__ == "__main__":
    main()
