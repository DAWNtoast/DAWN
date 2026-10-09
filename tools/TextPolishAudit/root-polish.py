from pathlib import Path
import json

root = Path('D:/Game')
base = root / 'UnityClient/Assets/_Project/Presentation/Runtime'
changes = {
    'CoreGameplayVoyageCargo.cs': {
        '随船船舱': '船舱',
        '选择左侧物品\\n查看数量并选择舍弃': '选择物品',
        '点击物品后输入数量，再确认舍弃。舍弃的物品无法取回。': '',
        '编辑预览：可查看船舱与选择数量，不会舍弃物品或写入存档。': '编辑预览 · 舍弃与保存已禁用',
        '当前无法舍弃，请检查物品数量和航行状态。': '无法舍弃，请重新选择物品。',
        '，船舱数量已更新。': '。',
        '随船数量已变化，请重新选择物品并确认。': '数量已变化，请重新选择。',
        '选择全部数量': '全部数量',
    },
    'CoreGameplayPortWarehouseDialogs.cs': {
        '调整船舱库存': '存入 / 取出',
        '两处合计一次入账；珍稀物品也用于高阶捐献和海上事件，请酌情保留。': '珍稀物品可用于高阶捐献与海上事件。',
        '两处数量合并计价，确认后一次入账；出售不占采购额度。': '出售不占采购额度。',
        '库存、账户或本月价格已变化，请重新打开出售窗口。': '库存、金币或价格已变化，请重新选择。',
    },
    'CoreGameplayWarehouseView.cs': {
        '未获得 · 查看途径': '未获得',
        '的珍藏室尚无藏品\\n\\n各港分别收藏与陈列，其他港的藏品保留在原港。': '尚无藏品\\n\\n藏品存放在获得它的港口。',
        '点击藏品，查看获取途径。\\n\\n已获得的藏品保存在对应港口。': '选择藏品查看来源。',
        '每一件藏品，都记录一段航程。\\n\\n点击藏品，查看来历并选择展位。': '选择藏品查看来历。',
        '选择藏品查看获取条件；未获得的藏品也可在图鉴中查询。': '图鉴可查询未获得的藏品。',
        '贵重品永久保存在本港收藏室，不占随船货舱，也不会在返航损失中扣除。\\n\\n各港分别陈列；收回只改变摆放，不会消耗藏品。': '藏品分港保管，不占船舱，返航不损失。\\n\\n陈列或收回均不消耗藏品。',
    },
    'CoreGameplayTavernDossier.cs': {
        '← 返回候选船员': '← 返回酒馆',
        '出身专长 · ': '专长 · ',
        '设为该职业代表': '设为职业代表',
    },
    'CoreGameplayShipyardController.cs': {
        '　·　点击船身查看档案　　': '　·　',
        '当前装配值 · 部件加成显示在条形右侧': '当前装配 · 右侧为部件加成',
        '未改造船只基准值 · 购买后可独立更换配件': '原装性能 · 购入后可改装',
        '当前货物超过该船舱容，请先存入本港仓库或出售，再切换船只；未扣除金币。': '货物超过舱容，请先存仓或出售。金币未扣除。',
    },
    'CoreGameplayDemoController.cs': {
        ' · 核心玩法原型': '',
        '航海实习　|　按当前航务指引完成真实航程': '航海实习',
        '本次航行已结算　|　船长资金 ': '航程结算　|　',
        '断锚港航海学院 · 与正式游戏共用同一场景': '断锚港航海学院',
        '跳过教学，进入港口': '跳过教学',
        '每次行动后敌舰都会反击。先观察伤害，再决定进攻、防御或修理。': '每次行动后，敌舰都会反击。',
        '领取毕业奖励，进入正式港口': '领取奖励，进入港口',
        '去市场调整补给': '采购补给',
        '请先确认目标港口，再到港口市场按航程需要采购物资。': '按预计航程备足补给。',
        '出航前检查备战、炮击与接舷三套牌组；在船专业船员会自动追加专属牌。': '配置备战、炮击、接舷牌组。随船专业船员自动加入专属牌。',
        '点击海图任意位置设置自由航点。': '点击海图设定航点。',
    },
}
replacements=[]
for filename, pairs in changes.items():
    path=base/filename
    text=path.read_text(encoding='utf-8-sig')
    for old,new in pairs.items():
        if old not in text: raise RuntimeError(f'Missing text in {filename}: {old}')
        text=text.replace(old,new)
        replacements.append({'old':old.replace('\\n','\n'),'new':new.replace('\\n','\n')})
    path.write_text(text,encoding='utf-8')
(root/'Artifacts/TextPolish20261008/root-text-replacements.json').write_text(json.dumps(replacements,ensure_ascii=False,indent=2),encoding='utf-8')
print(f'Applied {len(replacements)} text replacements across {len(changes)} files.')
