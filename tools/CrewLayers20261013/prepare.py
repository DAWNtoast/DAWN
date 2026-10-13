from pathlib import Path
import hashlib
import json
from PIL import Image

ROOT = Path('D:/Game')
OUT = ROOT/'Artifacts/CrewLayers20261013'
PREVIOUS = ROOT/'Artifacts/BrokenAnchorCrewAtlas4K20261013'
OUT.mkdir(parents=True, exist_ok=True)
(OUT/'references').mkdir(exist_ok=True)
old = json.loads((PREVIOUS/'manifest.json').read_text(encoding='utf-8'))
rects = [(70,0,1070,1380),(0,0,1366,1560),(0,0,1365,1280),
         (180,0,1185,1380),(100,0,1160,1015),(40,0,1325,1560)]
refs=[]
crew=[]
for record, (x,y,w,h) in zip(old['crew'],rects):
    dest=OUT/'references'/(record['id']+'_identity.jpg')
    with Image.open(record['output']) as im:
        im.crop((x,y,x+w,y+h)).convert('RGB').save(dest,quality=95,subsampling=0)
    refs.append(dest)
    crew.append(dict(id=record['id'],name=record['name'],row=record['row'],column=record['column'],
                     reference=str(dest),source=record['output'],referenceCropXYWH=[x,y,w,h]))

portrait_prompt='''Use case: identity-preserve / production character sprites.
Create ONE square 4K atlas containing exactly SIX clean, isolated, independently reusable head-and-shoulders/upper-chest character portraits in a precise equal 3-column by 2-row grid. The entire non-character area is one uniform, perfectly flat chroma-blue color #0080FF. This blue is only a technical removal background, completely unlit: NO blue reflection, spill, rim-light, shadow, gradient, paper texture, scenery or physical interaction with it.

Input images 1-6 are IDENTITY REFERENCES ONLY, in reading order. Some references are intentionally cropped to exclude old page ornament. Preserve each person's face, apparent age, complexion, hairstyle, scars, expression, earrings and clothing identity, but RECONSTRUCT the complete shoulders and upper chest rather than reproducing the reference crop edges. This output must be six standalone people, with no old dossier fragments whatsoever.

Exact character assignments:
TOP LEFT, reference 1: Lorne. Old weathered dark-brown-skinned man, deeply wrinkled narrow face, hooked nose, cloudy pale eye, grey-white braided hair, short grey beard, battered dark brown leather maritime jacket and brass trinkets. No handheld measuring instrument.
TOP MIDDLE, reference 2: Kia. Strong young bronze-tanned woman, broad square jaw, very short black hair, nose and cheek scars, brass earring, muscular shoulders, dark brown brass-riveted work harness/apron. No tools.
TOP RIGHT, reference 3: Hector. Gaunt elderly pale man, bald crown, sparse white side hair, white beard, distinctive stitched forehead/cheek scars, worn off-white shirt and dark leather vest. No medicine box.
BOTTOM LEFT, reference 4: Mira. Broad-shouldered dark-skinned adult, the exact rugged face of reference 4, short silver curls, forehead/eyebrow scar, earrings, rusty red neck cloth, dark brown brass-fastened maritime coat/armor. A small portion of the shoulder-mounted cannon equipment may remain as wearable equipment within this cell; no loose held weapon. Preserve the reference's facial proportions and gender presentation.
BOTTOM MIDDLE, reference 5: Tam. Warm smiling heavyset middle-aged woman, broad dimpled brown face, chestnut curls, red-gold headscarf, large brass hoop earrings and layered necklaces, off-white blouse and worn brown apron. Complete BOTH earrings, shoulders and upper chest even though the identity reference is tightly cropped. No ladle, pot, food or handheld objects.
BOTTOM RIGHT, reference 6: Udo. Lean dark-skinned bald man, angular diamond face, copper lower-lip ring and earring, short stubble, chain necklace and weathered brass diving equipment at the shoulder. Keep wearable diving gear inside the cell, without connecting it to an edge. No external scenery or props.

Mandatory compositing geometry:
- Square canvas. Divide at exactly width/3, 2*width/3 and height/2. Equal independent cells. Do not draw grid lines, labels or panels.
- One large bust in each cell, identical general camera distance, all heads and hair complete. Head plus neck, full shoulder contour and upper chest. NO full-body characters.
- Leave at least 6 percent of cell width of empty BLUE on BOTH sides of the entire silhouette, including hair, shoulder and wearable gear. Leave 4-6 percent empty blue above the hair. Characters must never touch another cell or its borders. Show the complete shoulders, not amputated by a vertical rectangle.
- All portraits face generally toward the viewer, retaining the individual three-quarter pose and expression from the references. Their faces should be similarly large, occupying around the upper half of the usable cell height. Continue clothing below the collar through upper chest. End the torso with an ordinary photographic horizontal bust crop near the lower 90 percent of its own cell; leave a small blue gap below it. Do not invent a pedestal, statue base, rounded plaque, vignette, torn-paper edge or decorative cutout edge.
- These people are not resting on, standing against, holding, leaning over, emerging through, or framed by ANYTHING. No frame, border, parchment, rope, compass, wheel, seal, gold ornament, card, nameplate, picture mat, shadow on background, scenery, furniture, text, symbol, signature, watermark or checkerboard anywhere.

Style: match the approved realistic painted maritime fantasy character art and warm restrained earth colors. Preserve faces; improve fine readable eye, hair, scar, leather and metal detail without plastic skin or sharpening halos. Neutral studio-like key lighting confined to the people; the blue background stays perfectly flat and saturated. It is essential to omit ALL former common-page decoration. Output ONE final high-resolution atlas image.'''
(OUT/'portraits-prompt.txt').write_text(portrait_prompt,encoding='utf-8')

frame_prompt='''Use case: game UI art / independent reusable dossier frame.
Generate ONE new 4K ultrawide 21:9 ornamental parchment dossier FRAME asset for the existing maritime fantasy game, closely matching the supplied EMPTY parchment reference in materials, warm colors and decoration. It will be reused for every character. NO person, face, silhouette, body, weapon, tool, clothing, portrait, text, letters, numbers or labels anywhere.

This image is a separately composited foreground frame and blank writing paper. Use perfectly FLAT chroma blue #0080FF in two places only: (1) every pixel OUTSIDE the paper's outer irregular silhouette, and (2) the large EMPTY portrait APERTURE on the left, all the way to its inner ornamental edge. The blue represents holes that software removes. Do not paint shadows, gradients, texture, fake transparency squares or blue spill/reflections into either blue area. Preserve all actual paper/ornament color elsewhere. Do not place brown paper, painting or a photograph inside the blue left aperture.

Composition requirements, measured from top-left as percentages of the whole canvas:
- A single wide old parchment sheet fills almost the entire image, with complete uncut outer outline and 1.5-2.5 percent blue safety margin around it. Slight curled and worn edges, knotted rope and dark red wax seal toward the far top-left and far bottom-right, as in the reference. No extra loose objects outside it.
- Large upright empty portrait opening on the LEFT. Interior blue opening bounds approximately x=6.0% to 28.5% and y=14% to 86%. Keep its long side edges straight and vertical, small rounded/corner embellishments only. This opening will accept a head-and-shoulders portrait. Its entire interior MUST be blank #0080FF, with no person or background scenery. Surround this opening with one restrained aged brass/brown engraved nautical border attached to the parchment.
- The picture frame should be a clean reusable frame, without a compass or ship wheel intruding into the portrait opening. Keep all ornament OUTSIDE the blue aperture; no rope across its center, no swirls crossing into a future face, no large bottom-left wheel. The border may occlude a few pixels at the edge, but not occupy the aperture.
- The RIGHT 65 percent is uninterrupted EMPTY light warm parchment for runtime text and controls, beginning near x=33%. This region stays opaque paper, never blue. Preserve the original quiet map texture very subtly, with ample low-contrast clear space. A faint small compass rose near the far top-right and faint ship illustration near the far bottom-right are acceptable only if faint enough not to compete with UI text. No printed lines of text, empty outlined text boxes, form fields or painted buttons.
- Keep the header area inside the right paper blank, central reading area blank, and bottom right-writing-area footer blank. Maintain the existing reference's restrained worn-fantasy appearance, not a modern UI, shiny gilded palace frame, poster, collage or illustration scene.

The reference is STYLE and overall arrangement only. Required changes are the wider 21:9 production composition, a genuinely empty chroma-blue left aperture, chroma-blue exterior, removal of the old large wheel near the left portrait, and crisp 4K edge/engraving detail. Output a single complete full-resolution frame image. No finished portrait/mockup/composited character is allowed.'''
(OUT/'frame-prompt.txt').write_text(frame_prompt,encoding='utf-8')
api=ROOT/'tools/BackgroundClarity4K20261013/Invoke-GeminiEnhancement.ps1'
for name,prompt,reference_list,aspect in [('portraits','portraits-prompt.txt',refs,'1:1'),
    ('frame','frame-prompt.txt',[ROOT/'UnityClient/Assets/_Project/Resources/PortArt/UI/tavern_crew_dossier_parchment_v1.png'],'21:9')]:
    quoted=',\n    '.join("'"+str(p)+"'" for p in reference_list)
    script="$ErrorActionPreference = 'Stop'\n$references = @(\n    "+quoted+"\n)\n& '"+str(api)+"' -PromptFile '"+str(OUT/prompt)+"' -OutputPath '"+str(OUT/(name+'-4k-v1.png'))+"' -ReferenceImages $references -Resolution '4K' -AspectRatio '"+aspect+"' -TimeoutSeconds 300\n"
    (OUT/('run-'+name+'-once.ps1')).write_text(script,encoding='utf-8-sig')
manifest={'scope':'Broken Anchor only; one clean six-portrait atlas and one reusable frame; compose in game.',
          'generationCallBudget':2,'automaticRetries':False,'crew':crew,
          'identityReferenceBytes':sum(p.stat().st_size for p in refs),'chromaColor':'#0080FF',
          'prompts':[str(OUT/'portraits-prompt.txt'),str(OUT/'frame-prompt.txt')]}
(OUT/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'references':len(refs),'bytes':manifest['identityReferenceBytes'],'directory':str(OUT)},indent=2))
