from pathlib import Path
import json, shutil, hashlib

root = Path(__file__).resolve().parent
base = json.loads((root/'manifest.json').read_text('utf-8-sig'))
records = {r['id']: dict(r) for r in base}
sources = {}
for p in sorted((root/'manifests').glob('*.json'), key=lambda p: p.stat().st_mtime):
    data = json.loads(p.read_text('utf-8-sig'))
    if isinstance(data, dict): data = [data]
    for rec in data:
        if rec['id'] not in records: raise ValueError('Unexpected crew ID')
        records[rec['id']].update({k:v for k,v in rec.items() if v is not None and v != ''})
        src = rec.get('source', '')
        if src and '/.codex/generated_images/' in src.replace('\\','/'):
            sources[rec['id']] = src

copied = []
for crew_id, src in sources.items():
    source = Path(src)
    if not source.is_file(): raise FileNotFoundError(source)
    target = root/'final'/(crew_id+'.png')
    if not target.exists() or source.read_bytes() != target.read_bytes():
        shutil.copyfile(source, target)
        copied.append(crew_id)
    rec = records[crew_id]
    rec['source'], rec['final'] = str(source), str(target)
    prompt = root/'prompts'/(crew_id+'.txt')
    if rec.get('prompt_text'): prompt.write_text(rec['prompt_text'], encoding='utf-8')
    rec['prompt'] = str(prompt)
    rec['sha256'] = hashlib.sha256(target.read_bytes()).hexdigest()
    rec['status'] = 'generated_visual_qa_pass'

(root/'delivery-manifest.json').write_text(json.dumps(list(records.values()),ensure_ascii=False,indent=2), encoding='utf-8')
print(json.dumps({'sources':len(sources),'finals':len(list((root/'final').glob('*.png'))),'copied':copied},ensure_ascii=False))
