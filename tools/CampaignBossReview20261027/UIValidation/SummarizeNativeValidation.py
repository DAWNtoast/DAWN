"""Seal native UI evidence without claiming a continuous UI campaign."""
import hashlib, json
from pathlib import Path

ROOT=Path(__file__).resolve().parents[3]
BASE=ROOT/'outputs/campaign-boss-review-20261027/UIValidation'
def read(path): return json.loads(path.read_text(encoding='utf-8-sig'))
def digest(path): return hashlib.sha256(path.read_bytes()).hexdigest()
new=BASE/'NewProfile02'
final=BASE/'CheckpointFinal02'
snapshot=BASE/'SnapshotCheckpointFinal02'
for run in (new,final):
    assert read(run/'summary.json')['Result']=='Passed'
    assert read(run/'completion.json')['InputValidationResult']=='PASS'
lines=(final/'Actions/checkpoint-ui-results.txt').read_text(encoding='utf-8-sig').splitlines()
assert len(lines)==13 and all(x.startswith('PASS ') for x in lines)
outcomes=[]
for boss in ('boss_route_golden_tide_trade','boss_route_moon_tide_ruins'):
    path=final/'Actions'/f'{boss}-complete-outcome.txt'
    action_path=final/'Actions'/f'{boss}-complete-actions.txt'
    result=path.read_text(encoding='utf-8-sig').splitlines()[0]
    assert result in ('victory','defeat')
    outcomes.append({'boss':boss,'outcome':result,'completeDriverActionsAfterTwoTurnProbe':len(action_path.read_text(encoding='utf-8-sig').splitlines()),'actionLog':str(action_path),'finalState':str(path)})
new_screens=ROOT/'Artifacts/CampaignBossReview20261027/NewProfile02/Visuals'
final_screens=ROOT/'Artifacts/CampaignBossReview20261027/CheckpointFinal02/Visuals'
checkpoints=read(snapshot/'earned-checkpoint-manifest.json')
assert len(checkpoints)==13
frozen=ROOT/'outputs/campaign-boss-review-20261027/FrozenSourceMiniboss'
native=ROOT/'tmp/shipyard-detail-clean-20261011/Assets/_Project'
frozen_files=[p for p in frozen.rglob('*') if p.is_file()]
assert len(frozen_files)==161
assert all(p.read_bytes()==(native/p.relative_to(frozen)).read_bytes() for p in frozen_files)
for entry in checkpoints:
    assert digest(Path(entry['Source']))==entry['SHA256']
    assert digest(snapshot/'EarnedCheckpoints'/(entry['Boss']+'.json'))==entry['SHA256']
report={
 'Result':'PASS',
 'Method':'Native Unity PlayMode test automation invokes live button callbacks and waits for production animation/comic transitions. It is not manual pointer gameplay.',
 'NewProfile':{'summary':read(new/'summary.json'),'completion':read(new/'completion.json'),'checkpoints':read(new/'Actions/campaign-ui-states.json'),'screenshots':len(list(new_screens.glob('*.png'))),'evidence':str(new),'visuals':str(new_screens),'coverage':'Real New Game -> opening comic -> complete existing guided tutorial -> earned item/collection UI -> save to main menu -> real Continue. New independent QA identity; no existing save accepted.'},
 'FinalBossUI':{'summary':read(final/'summary.json'),'completion':read(final/'completion.json'),'snapshot':read(snapshot/'snapshot.json'),'results':lines,'completeBattles':outcomes,'screenshots':len(list(final_screens.glob('*.png'))),'visuals':str(final_screens),'coverage':'All 13 naturally earned first-kill encounter checkpoints from LearningMiniboss02 restored into real CoreGameplayDemo scene, with earned component inventory. Every encounter choice, one legal drawn simple card when available, and two EndTurn callbacks checked against the identically restored Core. Tax queen and curse moon continue to a natural result with actual card/ram/boarding/end-turn buttons and per-action state equality.'},
 'Limitations':['Boss checkpoint replay begins by importing earned session and component state into the controller; it does not establish continuous play through the UI or saved-game import through Continue at these advanced states.','The complete fresh-save campaign and economy evidence are the separate LearningMiniboss02 continuous production-Core run.','Callback invocation verifies native rendering/logic/animation, not pointer hit testing, human strategy discovery, subjective play feel or human wall-clock completion time.','All 13 boss entry probes complete two turns; only tax queen and curse moon continue to a complete natural battle in this UI fixture.'],
 'HarnessCorrections':['NewProfile01 screenshot destination was outside the existing capture-helper allowed Artifacts tree; failed before new-game interaction.','Checkpoint preflight found an invalid generated meta GUID, corrected in the isolated fixture metadata before running Unity.','CheckpointBefore01 used a Port-only CanSelectRoute assertion on an Encounter snapshot; replaced by route unlock-level assertion.','CheckpointFinal01 completed a natural tax queen victory, then capture helper looked for its default Battle Card UI after combat had ended; result capture now targets Navigation Chart or Settlement Card. These were fixture corrections, not production changes.'],
 'Safety':'Formal storage untouched; fresh QA company/product namespaces; immutable raw checkpoints and production hashes; all 161 continuous-campaign frozen inputs match native Unity validation inputs byte-for-byte; no forced victory, resources, hull, draw order or enemy mutations; no retired ForMe outputs.',
 'Cleanup':read(BASE/'cleanup.json') if (BASE/'cleanup.json').exists() else {'Result':'PENDING'}
}
(BASE/'native-ui-validation-summary.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
text=['原生 Unity UI 验证：PASS','',
 f"新档/教程/保存/继续：1/1 通过，{report['NewProfile']['screenshots']} 张截图。",
 f"最终小 Boss 数值：13 个合法首杀前检查点全部通过入口/实际出牌/两回合回调复核，{report['FinalBossUI']['screenshots']} 张截图。"]
for item in outcomes:text.append(f"完整 UI 战斗：{item['boss']} -> {item['outcome']}，两回合探针之后 {item['completeDriverActionsAfterTwoTurnProbe']} 个自然 UI 动作。")
text+=['','范围：这是原生 PlayMode 自动 UI 回调诊断；13 个战前状态来自 LearningMiniboss02 的合法连续新档记录，按原始资源/卡组恢复，不代表全程逐屏实玩。连续全传奇通关与经济统计请引用 LearningMiniboss02。',
 '没有人工金币/血量/抽牌顺序/胜利注入，没有触碰正式玩家存档。原生自动化不等于鼠标命中测试或玩家主观体验。',
 '详细输入哈希、逐动作日志、结果 XML、截图、测试工具修正和缓存清理记录见 native-ui-validation-summary.json。']
(BASE/'native-ui-validation-summary.txt').write_text('\n'.join(text)+'\n',encoding='utf-8')
print(json.dumps({'Result':report['Result'],'NewProfileScreenshots':report['NewProfile']['screenshots'],'FinalBossScreenshots':report['FinalBossUI']['screenshots'],'CompleteBattles':outcomes,'Cleanup':report['Cleanup']['Result']},ensure_ascii=False))
