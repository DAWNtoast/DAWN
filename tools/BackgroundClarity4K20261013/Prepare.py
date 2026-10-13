import hashlib,json,shutil
from pathlib import Path
ROOT=Path('D:/Game')
OUT=ROOT/'Artifacts/BackgroundClarity4K20261013'
approved=['A01','A02','A03','A06']
audit=json.loads((ROOT/'Artifacts/ResolutionUpgradeAudit20261013/approval-list.json').read_text(encoding='utf-8'))
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
for name in ['before','prompts','api-originals','final-4k','comparisons','verification']:(OUT/name).mkdir(parents=True,exist_ok=True)
base='''Use case: identity-preserve / exact artwork super-resolution restoration.
Input image 1 is the EDIT TARGET, an already approved finished game background. It is NOT inspiration for a new composition.
Primary request: Produce a faithful 4K high-resolution restoration of THIS EXACT IMAGE. Improve fine spatial detail and clarity only. This is a production texture replacement whose interactive hotspots and foreground UI must still align.
Preserve all normalized image coordinates and the entire existing frame: exact camera, perspective, horizon, framing, object and character count, silhouettes, positions, scale, poses, faces, clothing, architecture, symbols, furniture, lighting direction, shadows, palette and atmosphere. Preserve the original hand-painted fantasy game illustration style.
Resolve existing blurred small details into clean restrained detail: fine wood grain, stone edges, rope fibres, cloth weave, metal engraving and existing brushwork where applicable. Keep the original identities and design of all subjects. Do not invent new architecture, props, people, marks, lettering or decorations. Do not beautify or redesign anything. Do not change global contrast, exposure, saturation or color grading.
Avoid halos, oversharpening, waxy smoothing, new noise, painterly simplification and added depth-of-field blur. Keep any deliberate atmospheric softness appropriate to the original scene.
No crop, no zoom, no reframing, no extra border, no new labels, no watermark, no UI overlay. Existing blank areas must stay blank. Return one complete 16:9 high-resolution restored image. The only intended change is sharper, richer resolution of the same original artwork.
'''
notes={
'A01':'Critical invariants: all port buildings and their recognisable emblems are navigation hotspots. Preserve every roofline, pier, stair, ship, mast, pavilion and foreground market at its exact original relative location. Preserve ALL four image edges.',
'A02':'Critical invariants: the large display cabinet and right-hand ledger panel are UI alignment targets. Preserve the exact six empty display compartments, shelf boundaries, dividers, rectangular writing panel, ornaments and all empty interiors. Do not fill any compartment or add writing. Keep straight frame edges at identical relative coordinates.',
'A03':'Critical invariants: preserve the enormous open sky area, exact cloud patterns, horizon, sun, lighthouse, distant island silhouettes and small sailing ship positions. The empty sky is a real UI text area: add no text and no objects.',
'A06':'Critical invariants: all existing characters are approved named crew. Preserve each face identity, expression, age, skin tone, hair, pose, hand placement, costume, held tools and exact position. Preserve all furniture, windows and existing nautical emblems. Do not replace characters, add extras or change their relationships.'}
records=[]
for g in audit['groups']:
 if g['id'] not in approved:continue
 for f in g['files']:
  src=Path(f['file']); stem=src.stem; backup=OUT/'before'/src.name; meta=Path(str(src)+'.meta')
  if backup.exists():assert sha(backup)==sha(src),'Source changed after backup'
  else:shutil.copy2(src,backup);shutil.copy2(meta,Path(str(backup)+'.meta'))
  prompt=OUT/'prompts'/(stem+'.txt')
  prompt.write_text(base+'\n'+notes[g['id']]+'\n',encoding='utf-8')
  records.append({'group':g['id'],'name':stem,'source':str(src),'backup':str(backup),'beforeSha256':sha(src),'beforeMetaSha256':sha(meta),'prompt':str(prompt),'requestedOutput':str(OUT/'api-originals'/(stem+'.png')),'finalOutput':str(OUT/'final-4k'/src.name),'targetSize':[3840,2160],'status':'prepared'})
assert len(records)==19
manifest={'approvedGroups':approved,'approvedCount':19,'provider':'Google Gemini / Nano Banana Pro','model':'gemini-3-pro-image','method':'reference-guided fidelity-preserving 4K enhancement','status':'prepared','items':records,'unchangedRequirements':['composition','locations and interaction targets','character identities','palette and lighting','live UI text']}
(OUT/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
print('Prepared 19 backed-up inputs and exact-preservation prompts. No API request or product replacement performed.')
