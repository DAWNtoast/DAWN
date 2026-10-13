import hashlib
import json
from pathlib import Path

from PIL import Image

ROOT = Path('D:/Game')
OUT = ROOT / 'Artifacts/BrokenAnchorCrewAtlas4K20261013'
SRC = ROOT / 'UnityClient/Assets/_Project/Resources/PortArt/CrewDossiers'
CREW = [
    ('navigator_lorne', '测潮师 洛恩'),
    ('carpenter_kia', '铆骨匠 琪娅'),
    ('doctor_hector', '瘢痕医师 赫克托'),
    ('gunner_mira', '沉钟炮师 米拉'),
    ('cook_tam', '烟熏厨娘 塔姆'),
    ('diver_udo', '锚墓潜手 乌铎'),
]
CROP = (90, 105, 580, 690)


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    (OUT / 'references').mkdir(parents=True, exist_ok=True)
    records = []
    refs = []
    for i, (suffix, name) in enumerate(CREW):
        crew_id = 'crew_broken_anchor_' + suffix
        source = SRC / (crew_id + '_dossier_v1.png')
        reference = OUT / 'references' / (f'{i+1:02d}_' + crew_id + '.png')
        with Image.open(source) as im:
            source_size = im.size
            im.crop(CROP).convert('RGB').save(reference, optimize=True)
        refs.append(reference)
        records.append(dict(id=crew_id, name=name, index=i, row=i//3, column=i%3,
                            source=str(source), sourceSize=source_size,
                            sourceSha256=digest(source), reference=str(reference),
                            referenceCropXYXY=CROP, referenceSha256=digest(reference)))
    manifest = dict(purpose='One user-approved six-portrait atlas trial; no game integration.',
                    gridColumns=3, gridRows=2, generationCallBudget=1,
                    requestedResolution='4K', requestedAspectRatio='1:1',
                    crew=records)
    (OUT/'manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding='utf-8')
    prompt = '''Create ONE square 4K game portrait atlas containing exactly SIX separate character portraits, arranged in a precise 3-column by 2-row equal grid. This is an economical production sprite sheet, not a group scene, poster, card layout, or collage of full dossier pages.

The six supplied reference images are ordered by the six grid cells, reading left-to-right across the top row, then left-to-right across the bottom row. Reproduce each approved character faithfully, with higher-resolution painted detail. Identity preservation is more important than embellishment. Keep the original apparent age, facial structure, skin tone, hair, scars, expression, clothing, metals, and visible personal equipment. Do not redesign or beautify the faces.

EXACT CELL ASSIGNMENTS:
Top left (reference 1): Lorne, the tide navigator. Weathered older dark-brown-skinned man, long narrow hooked-nose face, gray-white rope braids, cloudy left eye, lean shoulders, battered dark leather maritime jacket with brass fasteners. Preserve the tall rust-red wooden tide-measuring instrument held at his side as pictured.
Top middle (reference 2): Kia, the rivet carpenter. Young strong stocky bronze-tanned woman, square jaw, very short black cropped hair, scars across the bridge of her nose, muscular bare upper arms, dark brown leather work apron/harness with brass rivets. Match the reference's stern face. Her hands are not holding a tool; do not invent a hammer or axe.
Top right (reference 3): Hector, the scar physician. Gaunt elderly pale man, bald crown, white side hair and white beard, deep facial creases and stitched scars across forehead/cheek, worn off-white shirt and dark leather vest. Preserve the dark medical case with bone-colored handle at the lower side as in the reference.
Bottom left (reference 4): Mira, the bell gunner. Broad-shouldered dark-skinned adult with the exact rugged face in the reference, short silver curls, split eyebrow scar, brass earrings, rusty-red neck cloth, heavy dark brown brass-fastened coat/harness. Preserve the short heavy cannon apparatus behind the shoulder shown in the reference. Do not change the person's facial proportions or gender presentation.
Bottom middle (reference 5): Tam, the smokehouse cook. Heavyset warm smiling middle-aged woman, warm brown skin, broad dimpled face, chestnut curls under a red-gold wrapped headscarf, brass hoop earrings and layered necklaces, off-white shirt, dark leather apron. Preserve her ladle and copper cooking pot as shown.
Bottom right (reference 6): Udo, the anchor-grave diver. Lean dark-skinned adult man, shaved bald head, angular diamond-shaped face, copper lower-lip ring and earring, chain necklace, worn brass diving apparatus with a circular porthole beside his shoulder. Preserve his assured expression and asymmetric diving equipment.

LAYOUT AND CROP REQUIREMENTS:
- Exactly six portraits, one in each equal rectangular cell. Canvas 1:1 square. Vertical divisions at exactly one-third and two-thirds of canvas width; horizontal division at exactly half of canvas height. No drawn grid lines.
- Each portrait is a large head-and-upper-torso to upper-waist composition, comparable to its reference, with all hair and important visible equipment inside its own cell. No full-body figures. Keep faces large and similarly scaled, with modest clear headroom. Center each character independently in its own cell. No character, hair, tool, shoulder or shadow may cross into another cell. Leave a small safe margin at each cell edge for clean rectangular cutting. All six cells must be individually usable as portraits.
- Background is the same quiet plain warm tan in every cell, minimally textured, with gentle local shadow if needed. No locations or scenery. No shared cast shadow linking neighboring portraits. No group interaction.
- The source crops contain remnants of a common dossier frame, parchment page, rope, wax seal and lower-left compass decoration. REMOVE all of those shared UI decorations. The repeated compass belongs to the old frame, not to any character: do not reproduce it. No decorative borders, frames, paper edges, ropes, seals, scrolls, banners, panels or vignettes cutting into heads.
- No words, letters, Chinese text, names, numbers, labels, logos, signatures or watermarks anywhere.

STYLE AND FINISH:
Match the supplied game's realistic hand-painted maritime fantasy portrait style, warm restrained earthy palette, weathered skin and leather, aged brass, controlled directional light. Maintain the existing painterly character and approved designs while making eyes, facial features, hair, scars, fabric seams and metal edges crisp and readable at native output resolution. Avoid blur, hazy bloom, plastic skin, excessive sharpening halos, photographic style changes, cartoons, modern items, new costumes, extra people, duplicate faces or mixed identities. Deliver one final full-resolution square atlas image only.'''
    (OUT/'prompt.txt').write_text(prompt, encoding='utf-8')
    quoted = ',\n    '.join("'" + str(p).replace("'", "''") + "'" for p in refs)
    runner = """$ErrorActionPreference = 'Stop'
$references = @(
    %s
)
& 'D:\\Game\\tools\\BackgroundClarity4K20261013\\Invoke-GeminiEnhancement.ps1' -PromptFile 'D:\\Game\\Artifacts\\BrokenAnchorCrewAtlas4K20261013\\prompt.txt' -OutputPath 'D:\\Game\\Artifacts\\BrokenAnchorCrewAtlas4K20261013\\broken-anchor-six-crew-4k-v1.png' -ReferenceImages $references -Resolution '4K' -AspectRatio '1:1' -TimeoutSeconds 300
""" % quoted
    (OUT/'run-once.ps1').write_text(runner, encoding='utf-8-sig')
    print(json.dumps(dict(output=str(OUT), referenceCount=len(refs),
                          totalReferenceBytes=sum(p.stat().st_size for p in refs)), indent=2))


if __name__ == '__main__':
    main()
