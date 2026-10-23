from pathlib import Path
import json
root=Path(r'D:/Game/outputs/imagegen/crew-tavern-redraw-20261019')
source=json.loads((root/'manifest.json').read_text(encoding='utf-8-sig'))
prepared={}
for name in ['broken-gray.json','white-bronze.json','salt-twilight.json','broken-gray-story-preparation.json']:
    p=root/'manifests'/name
    if p.exists():
        for item in json.loads(p.read_text(encoding='utf-8-sig')):
            prepared[item['id']]=item
queue=[]
for item in source:
    details=prepared.get(item['id'],{})
    final=root/'final'/(item['id']+'.png')
    prompt=root/'prompts'/(item['id']+'.txt')
    queue.append({
        'id':item['id'],'name':item['name'],'port':item['port'],
        'status':'existing_completed' if final.exists() else 'pending_generation',
        'reference':item['reference'],'target':item['target'],
        'final':str(final) if final.exists() else None,
        'prompt':str(prompt),'promptReady':prompt.exists(),
        'scene':details.get('scene',''),'story_basis':details.get('story_basis',''),
        'tool':'builtin image_gen only',
        'newImageProducedThisTurn':False
    })
missing=[q['id'] for q in queue if not q['promptReady']]
assert not missing, f'Missing prompts: {missing}'
assert len(queue)==36 and len({q['id'] for q in queue})==36
assert all(q['scene'] for q in queue), 'Missing story scene'
(root/'generation-queue.json').write_text(json.dumps(queue,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'total':len(queue),'promptsReady':len(queue),'existingCompleted':sum(q['status']=='existing_completed' for q in queue),'pending':sum(q['status']=='pending_generation' for q in queue),'newImagesThisTurn':0},ensure_ascii=False))
