"""Build a file:// review from delivered animations and the Unity recording manifests."""
import argparse
import json
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "outputs/boarding-remaster-20261106"
ACTIONS = ("Attack", "Cast", "Hit", "Guard", "Death")


def read(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def local_path(path):
    path = path.resolve()
    if not path.is_relative_to(OUT) or not path.is_file() or not path.stat().st_size:
        raise ValueError(f"Missing or invalid review media: {path}")
    return path.relative_to(OUT).as_posix()


def image_asset(path, is_gif=False):
    with Image.open(path) as image:
        if is_gif and (image.format != "GIF" or image.n_frames < 3):
            raise ValueError(f"Expected an animated character: {path}")
        image.seek(0)
        image.load()
    return local_path(path)


def character(key, name, group, detail, paths):
    return {"key":key, "name":name, "group":group, "detail":detail,
            "gifs":{action:image_asset(paths[action], True) for action in ACTIONS}}


def build(available_only):
    roster = read(OUT / "actors/roster.json")
    definitions = [("captain", "船长", "我方", "玩家角色")]
    for crew in roster:
        definitions.append((crew["key"], crew["name"], "特殊船员", "专属牌 · " + crew["title"]))
    definitions.extend([
        ("enemy_party_left", "敌方左翼", "普通敌阵", "普通敌阵 · 左翼"),
        ("enemy_party_center", "敌方队长", "普通敌阵", "普通敌阵 · 中位"),
        ("enemy_party_right", "敌方右翼", "普通敌阵", "普通敌阵 · 右翼"),
    ])
    actors = []
    for key, name, group, detail in definitions:
        directory = OUT / "actors/gifs" / key
        paths = {action:directory / (action + ".gif") for action in ACTIONS}
        if available_only and not all(path.is_file() for path in paths.values()):
            continue
        actor = character(key, name, group, detail, paths)
        crew = next((person for person in roster if person["key"] == key), None)
        if crew is not None:
            actor["special"] = {"title": crew["title"],
                                "action": "Attack" if key == "crew_salt_crown_diver_isha" else "Cast"}
        actors.append(actor)
    named_path = OUT / "validation/named-enemies/manifest.json"
    if named_path.is_file() or not available_only:
        by_key = {}
        for clip in read(named_path)["clips"]:
            actor = by_key.setdefault(clip["identity"], {"name":clip["displayName"], "paths":{}})
            action = clip["action"]
            if action in actor["paths"] or action not in ACTIONS:
                raise ValueError(f"Duplicate or invalid action: {clip}")
            actor["paths"][action] = named_path.parent / clip["gif"]
        for key, actor in by_key.items():
            if set(actor["paths"]) != set(ACTIONS):
                raise ValueError(f"Incomplete encounter: {key}")
            actors.append(character(key, actor["name"], "具名敌阵", "游戏敌阵 · Unity 动作录制", actor["paths"]))
    if not actors or actors[0]["key"] != "captain":
        raise ValueError("Review needs the production captain's five actions")
    if not available_only and (len(actors) != 32 or len(set(a["key"] for a in actors)) != 32):
        raise ValueError(f"Expected 19 painted actors and 13 named enemies, found {len(actors)}")
    scene_path = OUT / "validation/gameplay/manifest.json"
    scenes = []
    if scene_path.is_file() or not available_only:
        for clip in read(scene_path)["clips"]:
            scenes.append({"title":clip["title"], "video":local_path(scene_path.parent / clip["video"]),
                           "poster":image_asset(scene_path.parent / clip["poster"])})
        if not scenes:
            raise ValueError("No Unity gameplay recordings to review")
    fx = image_asset(OUT / "effects/previews/boarding-effects.gif", True)
    image_asset(OUT / "effects/previews/all-frames.jpg")
    deck = ROOT / "UnityClient/Assets/_Project/Resources/BoardingBattle/Backgrounds/boarding_joined_deck_v3.png"
    with Image.open(deck) as source:
        reduced = source.convert("RGBA")
        reduced.thumbnail((1280,720), Image.Resampling.LANCZOS)
        base = Image.new("RGBA", reduced.size, "#213236")
        base.alpha_composite(reduced)
        base.convert("RGB").save(OUT / "review-deck.jpg", quality=87)
    manifest = {"actors":actors, "scenes":scenes, "effects":fx}
    template = Path(__file__).with_name("ActionReview.template.html").read_text(encoding="utf-8")
    if template.count("__MANIFEST__") != 1:
        raise ValueError("Unexpected review template")
    embedded = json.dumps(manifest, ensure_ascii=False, separators=(",", ":")).replace("<", "\\u003c")
    page = template.replace("__MANIFEST__", embedded)
    (OUT / "index.html").write_text(page, encoding="utf-8")
    print(f"Verified {len(actors) * len(ACTIONS)} character GIFs, {len(scenes)} scene recordings, effects and deck artwork.")


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--available", action="store_true", help="Review complete actors while production rendering continues")
    build(parser.parse_args().available)
