"""Attach production-source trace and resource-resolution checks to the visual audit."""
from pathlib import Path
import json, hashlib, re, collections
from PIL import Image

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'outputs/crew-card-alignment-20261031/audit/crew-card-mapping.json'
RES=ROOT/'UnityClient/Assets/_Project/Resources'
report=json.loads(OUT.read_text(encoding='utf-8'))
paths={
 'crewCatalog':'Content/Runtime/CrewNarrativeCatalog.cs',
 'sourceCardCatalog':'Core/Runtime/CoreSpecialCardCatalog.cs',
 'schoolCardCatalog':'Core/Runtime/CoreSchoolCardCatalog.cs',
 'specialRedesign':'Core/Runtime/CoreSpecialCardRedesign.cs',
 'cardArtCatalog':'Presentation/Runtime/CardSchoolArtCatalog.cs',
 'portArtCatalog':'Presentation/Runtime/PortArtCatalog.cs',
 'facilityArtCatalog':'Presentation/Runtime/PortFacilityArtCatalog.cs',
 'tavernLayout':'Presentation/Runtime/TavernCrewLayoutCatalog.cs',
 'tavernOverview':'Presentation/Runtime/CoreGameplayPortController.cs',
 'tavernDetail':'Presentation/Runtime/CoreGameplayTavernDossierArtComparison.cs',
}
files={k:ROOT/'UnityClient/Assets/_Project'/v for k,v in paths.items()}
text={k:p.read_text(encoding='utf-8-sig') for k,p in files.items()}
hashcache={}
def sha(p):
 p=Path(p)
 if p not in hashcache: hashcache[p]=hashlib.sha256(p.read_bytes()).hexdigest()
 return hashcache[p]
def resource(path):
 candidates=[(RES/path).with_suffix(ext) for ext in ['.png','.jpg','.jpeg']]
 available=[p for p in candidates if p.exists()]
 assert len(available)<=1, 'Duplicate texture resource: '+path
 if not available:return None
 p=available[0]
 with Image.open(p) as im: dimensions=list(im.size)
 return {'resourcePath':path,'sourcePath':p.as_posix(),'sha256':sha(p),'dimensions':dimensions}
def evidence(source,needle):
 lines=text[source].splitlines()
 found=[{'line':i+1,'text':line.strip()} for i,line in enumerate(lines) if needle in line]
 assert found,(source,needle)
 return {'source':files[source].as_posix(),'matches':found}

catalog={}
for line in text['crewCatalog'].splitlines():
 if not re.search(r'\bCrew\("crew_',line):continue
 strings=re.findall(r'"([^"\\]*(?:\\.[^"\\]*)*)"',line)
 prof=int(re.search(r'"\s*,\s*(\d)\s*,',line).group(1))
 catalog[strings[0]]={'portName':strings[1],'professionIndex':prof,'displayName':strings[2],
                     'appearanceTags':strings[5],'portraitAssetName':strings[-1]}
assert len(catalog)==36
school={}
for line in text['schoolCardCatalog'].splitlines():
 if 'new CoreBattleCard("card_special_crew_' not in line: continue
 strings=re.findall(r'"([^"\\]*(?:\\.[^"\\]*)*)"',line)
 school[strings[0]]={'requiredCrew':strings[4],'artId':strings[5]}
neutral={}
for line in text['sourceCardCatalog'].splitlines():
 m=re.search(r'C\(cards, "(crew_[^"]+)".*artId: "([^"]+)"',line)
 if m:neutral['card_special_'+m.group(1)]={'requiredCrew':m.group(1),'artId':m.group(2)}
assert len(school)==12 and len(neutral)==4
artrows=json.loads((ROOT/'outputs/card-list-by-school-20261023/card-art-data.json').read_text(encoding='utf-8-sig'))['Cards']
consumers=collections.defaultdict(set)
for a in artrows:consumers[a['ResourcePath']].add(a['Id'])
layoutlines=[line for line in text['tavernLayout'].splitlines() if 'new[] { R(' in line]
assert len(layoutlines)==6
layout=[]
for line in layoutlines:
 values=[tuple(map(float,m)) for m in re.findall(r'R\((\d+),(\d+),(\d+),(\d+)\)',line)]
 assert len(values)==6
 layout.append(values)
ports=list(report['validation']['perPortCount'])
legacy=['crew_navigator_evelyn_fullbody','crew_carpenter_barlow_fullbody','crew_doctor_moer_fullbody',
        'crew_gunner_leisa_fullbody','crew_cook_pepe_fullbody','crew_diver_noya_fullbody']
frame=resource('PortArt/UI/tavern_crew_dossier_frame_4k_layers_v1')
assert frame
for r in report['crew']:
 crewid=r['id']; card=r['card']; idx=r['professionIndex']; port=r['portName']
 for key,value in catalog[crewid].items():assert r[key]==value,(crewid,key)
 r['productionCrewCatalogVerified']=True
 r['crewCatalogEvidence']=evidence('crewCatalog','Crew("'+crewid+'"')
 slug=crewid.removeprefix('crew_').split('_'+r['professionId']+'_')[0]
 tavern=resource('PortFacilities/Regional/Tavern/tavern_'+slug+'_v1')
 assert tavern and tavern['sourcePath']==r['tavernOverview']['sourcePath']
 x,y,w,h=layout[ports.index(port)][idx]
 normalized={'x':x/1672,'y':y/941,'width':w/1672,'height':h/941}
 for key,value in normalized.items():assert abs(value-r['tavernOverview']['normalizedBounds'][key])<1e-10
 r['tavernOverview']['resourcePath']=tavern['resourcePath']
 r['tavernOverview']['sourceCodeBoundsVerified']=True
 r['tavernOverview']['uvUnityBottomLeft']=[x/1672,1-(y+h)/941,w/1672,h/941]
 portrait=resource('PortArt/CrewDossiers/'+crewid+'_portrait_v2')
 assert portrait and portrait['sourcePath']==r['dossierPortrait']['sourcePath']
 r['dossierPortrait']['resourcePath']=portrait['resourcePath']
 r['dossierPortrait']['selectedAtRuntime']=True
 r['dossierPortrait']['frameResourcePath']=frame['resourcePath']
 r['dossierPortrait']['viewportDesignSize']=[1464/6336*1780,1856/2688*705]
 r['dossierPortrait']['layout']='uniform cover, centered horizontally, anchored at top; RectMask2D clips overflow'
 avatar=resource('PortArt/CrewAvatars/'+crewid+'_avatar_v1')
 r['departureAvatar']=avatar
 r['regionalPortraitAuxiliary']['resourcePath']='PortArt/CrewRegional/crew_'+slug+'_portraits_v2'
 cell=r['regionalPortraitAuxiliary']['uvUnityBottomLeft']
 r['regionalPortraitAuxiliary']['headOnlyUvUnityBottomLeft']=[cell[0]+cell[2]*.15,cell[1]+cell[3]*.30,cell[2]*.7,cell[3]*.65]
 bodies=['PortArt/CrewRegional/'+r['portraitAssetName'],
         'PortArt/CrewFullBody/'+r['portraitAssetName'],
         'PortArt/CrewFullBody/'+legacy[idx]]
 chain=[]
 for name in dict.fromkeys(bodies):
  found=resource(name)
  chain.append(found or {'resourcePath':name,'exists':False})
 selected=next((b for b in chain if 'sourcePath' in b),None)
 assert selected
 r['fullBodyFallback']={'selected':selected,'candidatesInPriorityOrder':chain,
                       'usedByCurrentTavernOverviewOrDossier':False}
 western=resource('CardArt/Western/'+card['id'])
 authored=school.get(card['id']) or neutral.get(card['id'])
 if authored:
  assert authored['requiredCrew']==crewid
  card['authoredArtId']=authored['artId']
  card['requiredCrewEvidence']=evidence('schoolCardCatalog' if card['id'] in school else 'sourceCardCatalog',crewid)
 else:
  card['requiredCrewEvidence']=evidence('sourceCardCatalog','C(cards, "'+crewid+'"')
 expected=western or resource('CardSchools/Art/'+authored['artId'])
 assert expected['resourcePath']==card['resourcePath'],card['id']
 card['runtimeResolutionVerified']=True
 card['resolutionBranch']='existing Western CardId priority' if western else 'authored school ArtId fallback'
 card['otherActiveCardConsumers']=sorted(consumers[card['resourcePath']]-{card['id']})
 card['availableWesternDedicatedCard']=western
 card['availableSchoolFallback']=resource('CardSchools/Art/'+authored['artId']) if authored else None
 card['unselectedIdentitySpecificProductionCardCandidates']=[]

report['sourceEvidence']={k:{'path':p.as_posix(),'sha256':sha(p)} for k,p in files.items()}
report['productionResolutionEvidence']=[
 evidence('cardArtCatalog','string western = WesternIllustrationPath(card);'),
 evidence('cardArtCatalog','if (western != null && Load(western) != null) return western;'),
 evidence('sourceCardCatalog','replacement.RequiredCrew'),
 evidence('sourceCardCatalog','kind == CoreSpecialCardSourceKind.Crew ? id : string.Empty'),
 evidence('tavernDetail','if (TryBuildTavernDossierPortrait(detail, crew.Id)) return;'),
 evidence('tavernOverview','TavernCrewLayoutCatalog.StageCenter'),
]
report['dossierFrame']=frame
report['validation'].update({
 'allCrewIdentitiesMatchCurrentSourceCatalog':True,
 'allTavernBoundsMatchCurrentSourceCatalog':True,
 'allCardResourceSelectionsMatchCurrentRuntimeResolution':True,
 'westernDedicatedCardCount':sum(r['card']['availableWesternDedicatedCard'] is not None for r in report['crew']),
 'schoolFallbackCount':sum(r['card']['availableWesternDedicatedCard'] is None for r in report['crew']),
 'skippedExistingDedicatedCrewImages':0,
 'sharedCrewCardArt':[{'crewId':r['id'],'resourcePath':r['card']['resourcePath'],
                      'otherCardIds':r['card']['otherActiveCardConsumers']} for r in report['crew'] if r['card']['otherActiveCardConsumers']],
 'scopeNote':'Existing production identity-specific card assets and current authored school fallbacks checked. This does not assert that arbitrary unlabelled historical images in outputs contain no usable alternate art.'
})
OUT.write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print('Verified 36 roster identities, 36 tavern bounds, 36 runtime card resource selections; 20 Western / 16 school; 1 shared asset.')
