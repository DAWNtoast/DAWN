"""Keep the existing report URL and label its older recorded results explicitly."""
import hashlib
import json
import shutil
from pathlib import Path

root = Path(__file__).resolve().parents[2]
report = root / 'outputs/campaign-boss-review-20261027/Boss完整成长审核.html'
out = root / 'outputs/campaign-progression-retune-20261027'
backup_dir = out / 'Before/PreviousReport'
backup_dir.mkdir(parents=True, exist_ok=True)
backup = backup_dir / report.name
if not backup.exists():
    shutil.copy2(report, backup)
    (backup_dir / 'manifest.json').write_text(json.dumps({
        'source': str(report), 'backup': str(backup),
        'sha256': hashlib.sha256(backup.read_bytes()).hexdigest(),
    }, ensure_ascii=False, indent=2), encoding='utf-8')
text = report.read_text(encoding='utf-8-sig')
banner = '''<section id="latest-progression-report" style="border:2px solid #116d78;background:#eaf7f3">
<h2>已有更新：第一章降难与最终成长校准</h2>
<p>本页以下内容保留上一轮历史结果。当前版本已调整第一章 Boss、经验获取、2～7 级卡牌解锁及接舷 1.25 倍收益；最新新档第 31 航击败全部传奇。</p>
<p><a href="../campaign-progression-retune-20261027/成长节奏与Boss校准报告.html"><b>打开最新数值、首杀金币与完整构筑报告 →</b></a></p></section>'''
if 'id="latest-progression-report"' not in text:
    assert '<main>' in text
    report.write_text(text.replace('<main>', '<main>' + banner, 1), encoding='utf-8')
print(json.dumps({'originalUrlPreserved': report.exists(), 'backup': str(backup), 'latestLinkAdded': 'id="latest-progression-report"' in report.read_text(encoding='utf-8')}, ensure_ascii=False))
