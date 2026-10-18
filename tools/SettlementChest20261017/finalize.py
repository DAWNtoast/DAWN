from pathlib import Path
import hashlib, json, re, xml.etree.ElementTree as ET
from PIL import Image

root = Path('D:/Game')
artifacts = root / 'Artifacts/SettlementChest20261017'
source = root / 'UnityClient/Assets/_Project'
validation = root / 'tmp/settlement-alignment-20261011/Assets/_Project'
report = {'scope': 'Production chest sequence, isolated Unity project; no source scenes created and retired ForMe excluded.',
          'tests': [], 'sourceVerification': [], 'finalCapture': {},
          'notes': ['EditMode final covers final source including rare rewards in overview.',
                    'PlayMode play02 covers sequence, input gates, paused time, tutorial restoration, keyboard focus and settlement transaction regressions; final changes afterward are visual fireworks/layout and rare manifest rendering.']}
for name in ('edit-final', 'play02'):
    run = ET.parse(artifacts / name / 'results.xml').getroot()
    assert run.attrib['failed'] == '0', name
    report['tests'].append({'batch': name, 'total': int(run.attrib['total']), 'passed': int(run.attrib['passed']),
                            'failed': int(run.attrib['failed']), 'result': run.attrib['result']})
for name in ('edit-final', 'play02', 'capture-final'):
    log = (artifacts / name / 'unity.log').read_text(encoding='utf-8-sig', errors='replace')
    failures = [line for line in log.splitlines() if re.search(r'error CS\d+|AssertionException|NullReferenceException|InvalidOperationException|ArgumentException|invalid GUID', line)]
    assert not failures, (name, failures)
files = ['Presentation/Runtime/SettlementChestRewardPlan.cs', 'Presentation/Runtime/SettlementChestSequenceController.cs',
         'Presentation/Runtime/SettlementFireworksGraphic.cs', 'Presentation/Runtime/CoreGameplaySettlementChest.cs',
         'Presentation/Runtime/CoreGameplaySettlementPresentation.cs', 'Presentation/Runtime/CoreGameplaySettlementStorage.cs',
         'Presentation/Runtime/CoreGameplayGuidedTutorialUI.cs']
for file in files:
    a, b = (source / file).read_bytes(), (validation / file).read_bytes()
    assert a == b, file
    report['sourceVerification'].append({'file': file, 'sha256': hashlib.sha256(a).hexdigest().upper(), 'matchesValidation': True})
capture = artifacts / 'capture-final/captures'
frames = json.loads((capture / 'report.json').read_text(encoding='utf-8-sig'))
assert len(frames['frames']) == 20
with Image.open(capture / 'SettlementChest_Jackpot.gif') as gif:
    milliseconds = 0
    for frame in range(gif.n_frames):
        gif.seek(frame)
        milliseconds += gif.info.get('duration', 0)
    report['finalCapture'] = {'keyframes': len(frames['frames']), 'sourceAnimationFrames': frames['animationFrames'],
        'gifFrames': gif.n_frames, 'gifSeconds': milliseconds / 1000, 'expectedSeconds': frames['animationDuration'],
        'gif': str(capture / 'SettlementChest_Jackpot.gif'), 'resolutions': ['1920x1080','1280x720','1280x1024'],
        'endsOnOverview': True, 'automaticOverviewEvidence': str(artifacts / 'play02/interactions/SettlementChest_AutoOverview.png')}
    assert abs(milliseconds / 1000 - frames['animationDuration']) < .1
report['passedTotal'] = sum(item['passed'] for item in report['tests'])
(artifacts / 'acceptance.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps({'passedTotal': report['passedTotal'], 'verifiedProductionFiles': len(files), 'keyframes': 20,
                  'gifSeconds': report['finalCapture']['gifSeconds'], 'acceptance': str(artifacts / 'acceptance.json')}, ensure_ascii=False))
