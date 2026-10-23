import json, re, sys
from pathlib import Path
sys.stdout.reconfigure(encoding='utf-8')
report = json.loads(Path(sys.argv[1]).read_text(encoding='utf-8-sig'))
for frame in report['frames']:
    if frame.get('error'):
        print('FAILED', frame['file'], frame['error'].splitlines()[0])
        continue
    found = []
    for label in frame['text']:
        if not label.get('meshAvailable'):
            continue
        if re.search(r'Hint|Note$|Instruction|instruction|Footer$|Status$|Records (Heading|Empty)|Preview Empty|Chart Seed Validation|Treasure (Empty|Detail|Consumption|Status)|Empty Description|Equipment Help|Installed Heading|Battle Deck Summary|Upgrade Limit', label['path']):
            found.append((label['path'].split('/')[-1], label['fontSize'], label['content']))
    if found:
        print(frame['file'])
        for value in found:
            print(' ', json.dumps(value, ensure_ascii=False))
