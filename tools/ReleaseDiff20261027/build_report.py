"""Render the local release comparison from reviewed, frozen evidence."""
import html,json
from pathlib import Path

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'outputs/release-diff-20261027'
def load(n):return json.loads((OUT/n).read_text(encoding='utf-8-sig'))
def h(v):return html.escape(str(v),quote=True)
def uri(p):return Path(p).as_uri()
summary=load('comparison-summary.json');changes=load('file-diff.json')
core=load('core-content-review.json');ui=load('presentation-review.json')
stable=load('source-stability.json');assert stable['matchesSnapshot']
highlights=[
 ('首领战','首步铺场扩展到其他体系与宝藏首领；重排四步输出与格挡，只有回整步可改选合法冲撞；冲锋从第3、5、8、10…回合改为第4、8、12、16…回合。敌方格挡意图准确计入已有血池返盾。'),
 ('卡牌与战斗规则','13张现有牌受影响：8张部件仅统一免疫措辞，隔焰护舷/潮盾定锚改在下个己方回合开始到期，雾铁偿伤/活舟还生新增血池免疫，幽帆牵制退出咒术护盾分类。取消水手不足降低火力/装甲的处罚。'),
 ('战斗图标与提示','连锁、潮势移到持有者状态栏，咒印保留持有者状态并去除资源栏重复入口；资源栏由三行改两行。七项资源及抽弃牌堆取消悬停文字；删水手不足、咒术护盾图标，新增血池防护并简化指定提示。'),
 ('接舷表现','新增六系施法、命中表现与火焰/潮汐特效素材；同步血条、音效与命中时点。人物保持静态、败北淡出；按本次航行船员身份显示人物，并修正幽灵敌人立绘。'),
 ('港口与工坊','六港正式接入独立木纹顶栏，航行/遭遇/战斗使用出发港木纹。卡组工坊新增无体系及六流派筛选。'),
 ('物资与音乐','火药、炮弹、木板、药品统一使用透明物品本体图案；货物入口复用同一纹理与UV。主菜单与六港改用共同主题曲，切换时记住播放进度。'),
 ('教学与界面精简','教学新增点击海图事件锁定航向的指引；删减工坊、部件店、仓库、船舱、寻宝、返菜单等页的补充小字；藏品收益改为金币图标配数字。'),
]
boundaries=[
 '卡牌身份/可用阶段无增量，沿用上次包的324张、358阶段定义；没有新增或删除卡牌ID。13张中8张只是措辞统一，不能算8项新增免疫。',
 '80×80顶栏图标、功能页图标和导航、上一批51张卡图修复、滑块船舵、航行时间条及此前响应式布局已在r03，不属于本次新增。',
 '固定接舷队伍合照虽新增为素材，但当前生产代码没有加载调用；人物动作兼容组件也未启用。实际人物仍保持静态。',
 '血池付血返2倍格挡和首领四步循环原本已有；本次变的是防护、意图准确性和循环内安排。燃烧、咒印等简化提示没有同时改动其基础结算规则。',
 '14项旧状态曾被标记为无正常来源，这属于图标清单审查；不能写成本次批量删除了14种战斗机制。',
 '场景文件、Packages、ProjectSettings均无差异。Resources内的返回菜单确认框prefab有1处默认说明文字清空，不等于删除自动保存流程。',
 '比较范围为与发布流程相同的Assets、Packages、ProjectSettings，排除退役UIPreviewsForMe。文档、输出报告及工具脚本不计入168个打包输入差异。',
 '本次执行源码与资源差异审查，未运行新的Unity整包测试或出包；历史包的验收结果不作为当前全部改动的验收结果。',
]
records=[]
for f in core['facts']:records.append(dict(f,section='玩法和卡牌',boundary=f.get('limits','')))
for f in ui['groups']:records.append(dict(f,section='界面和表现'))
def evidence(r):
 result=[]
 for e in r.get('evidence',[]):
  if 'current' in e:
   for key,label in [('published','发布版'),('current','当前快照')]:
    v=e.get(key)
    if v:result.append((label+' '+e['file']+':'+str(v['line']),v['path']))
  else:
   p=OUT/'current-snapshot/UnityClient/Assets/_Project/Presentation/Runtime'/e['file']
   result.append((e['file']+':'+str(e.get('line','')),str(p)))
 return result
md=['# 当前工程与上次出包的差异','',f"对比基准：2026-10-26 r03，版本 **{summary['releaseVersion']}**。当前工程快照：**2026-10-27 01:49:25（北京时间）**；核对结束时5776个输入文件仍与该快照一致。",'',
 '内容哈希比较结果：**108个修改、60个新增、0个删除，共168个文件**。其中Core 19、Presentation 54、Resources 12、Editor 1、测试51、meta 31。','',
 '## 主要变化','']
for title,body in highlights:md+=['- **'+title+'：**'+body]
md+=['','## 变化边界','']+['- '+x for x in boundaries]
for section in ['玩法和卡牌','界面和表现']:
 md+=['','## '+section,'']
 for r in records:
  if r['section']!=section:continue
  md+=['### '+r['title'],'','上次包：'+r['before'],'','当前工程：'+r['after'],'']
  if r.get('boundary'):md+=[r['boundary'],'']
  md+=['证据：'+'；'.join('['+name+'](<'+path.replace('\\','/')+'>)' for name,path in evidence(r)),'']
md+=['## 首领四步基础攻击系数','', '以下数字是相对基础炮击值的百分比，按循环第1至第4步排列；实际伤害仍叠加资源、场地、装填等效果。','', '| 体系 | 上次包 | 当前工程 | 补充 |','|---|---|---|---|']
for r in core['bossBaseAttackCoefficientTable']:md+=['| '+ ' | '.join(str(r.get(k,'')).replace('|','\\|') for k in ['school','old','new','notes'])+' |']
md+=['','## 文件清单','', '| 类型 | 动作 | 路径 |','|---|---|---|']
for e in changes:md+=['| '+e['category']+' | '+e['change']+' | '+e['path']+' |']
md+=['','来源：发布封包清单、发布源码清单、当前输入哈希和本目录内冻结差异。未修改主工程，未产生Unity或编译缓存；差异快照与报告作为证据保留。','']
(OUT/'当前工程与上次出包差异.md').write_text('\n'.join(md),encoding='utf-8')

parts=['<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>当前工程与上次出包差异</title>',
 '''<style>body{margin:0;overflow-wrap:anywhere;background:#f3f6f7;color:#18313e;font:16px/1.75 system-ui,"Microsoft YaHei",sans-serif}main{max-width:1240px;margin:auto;padding:34px 25px 65px}h1{font-size:30px;margin:0 0 12px}h2{margin-top:34px}h3{font-size:20px;margin:0 0 12px}a{color:#08798d;overflow-wrap:anywhere}section,article,.panel{background:white;border:1px solid #d6e0e5;border-radius:10px;padding:22px;margin:16px 0}li{margin:8px 0}.meta{color:#53717d}.stats{display:flex;gap:12px;flex-wrap:wrap}.stat{background:#18313e;color:white;padding:10px 20px;border-radius:6px}.controls{position:sticky;top:0;background:#f3f6f7;z-index:2;display:flex;gap:12px;padding:12px 0}input,select{font:inherit;padding:9px;border:1px solid #a8bac4;border-radius:5px;min-width:0}input{flex:1}table{border-collapse:collapse;width:100%;table-layout:fixed}th,td{text-align:left;vertical-align:top;padding:12px;border:1px solid #d6e0e5;overflow-wrap:anywhere}th{background:#edf3f5}summary{cursor:pointer;color:#08798d}details{margin-top:15px}.limit{color:#536d78;font-size:14px}.files td{font-size:13px}.files th:first-child{width:100px}.files th:nth-child(2){width:70px}[hidden]{display:none!important}@media(max-width:700px){main{padding:20px 12px}section,article,.panel{padding:14px}.controls{flex-wrap:wrap}input{flex-basis:100%}h1{font-size:24px}th,td{padding:8px}.files th:first-child{width:65px}.files th:nth-child(2){width:50px}}</style><main>''',
 '<h1>当前工程与上次出包差异</h1>',
 f'<p>上次正式包：<b>2026-10-26 r03 · {h(summary["releaseVersion"])}</b><br>当前快照：2026-10-27 01:49:25（北京时间），核对结束时输入保持一致。</p>',
 '<div class="stats"><div class="stat">108 修改 · 60 新增 · 0 删除</div><div class="stat">13 张现有卡牌受影响</div><div class="stat">324 卡牌身份 · 无增量</div></div>',
 '<section><h2 style="margin-top:0">主要变化</h2><ul>'+''.join('<li><b>'+h(t)+'：</b>'+h(b)+'</li>' for t,b in highlights)+'</ul></section>',
 '<details class="panel"><summary>哪些内容不能算作本次新增</summary><ul>'+''.join('<li>'+h(x)+'</li>' for x in boundaries)+'</ul></details>',
 '<div class="controls"><input id="search" placeholder="搜索首领、卡牌、血池、音乐、文件名…"><select id="scope"><option value="">全部变化</option><option>玩法和卡牌</option><option>界面和表现</option><option>文件清单</option></select><span id="count"></span></div>']
for r in records:
 parts+=['<article class="change" data-section="'+h(r['section'])+'"><span class="meta">'+h(r['section'])+'</span><h3>'+h(r['title'])+'</h3><table><tr><th>上次包</th><th>当前工程</th></tr><tr><td>'+h(r['before'])+'</td><td>'+h(r['after'])+'</td></tr></table>']
 if r.get('boundary'):parts+=['<p class="limit">'+h(r['boundary'])+'</p>']
 parts+=['<details><summary>查看源码证据</summary><ul>'+''.join('<li><a href="'+h(uri(p))+'">'+h(n)+'</a></li>' for n,p in evidence(r))+'</ul></details></article>']
parts+=['<section><h2 style="margin-top:0">首领四步基础攻击系数</h2><p class="limit">相对基础炮击值的百分比；实际伤害另计资源、场地、装填等效果。</p><table><tr><th>体系</th><th>上次包</th><th>当前工程</th><th>补充</th></tr>']
for r in core['bossBaseAttackCoefficientTable']:parts+=['<tr>'+''.join('<td>'+h(r.get(k,''))+'</td>' for k in ['school','old','new','notes'])+'</tr>']
parts+=['</table></section><section><h2 style="margin-top:0">全部168项文件差异</h2><p class="limit">Core 19 · Presentation 54 · Resources 12（含1份prefab） · Editor 1 · 测试51 · meta 31。仅显示打包输入差异。</p><table class="files"><thead><tr><th>类型</th><th>动作</th><th>文件与证据</th></tr></thead><tbody>']
for e in changes:
 link='<a href="'+h(uri(e['diff']))+'">'+h(e['path'])+'</a>' if e.get('diff') else h(e['path'])
 parts+=['<tr class="change" data-section="文件清单"><td>'+h(e['category'])+'</td><td>'+h(e['change'])+'</td><td>'+link+'</td></tr>']
parts+=['</tbody></table></section><p class="meta">仅执行只读差异核对，未产生Unity或编译缓存。发布基准与当前快照的清单哈希、逐文件差异和分项审查证据保存在同一目录。</p>',
 '<p><a href="'+h(uri(OUT/'当前工程与上次出包差异.md'))+'">下载 Markdown 清单</a> · <a href="'+h(uri(OUT/'comparison-summary.json'))+'">哈希比较摘要</a></p>',
 '''<script>const items=[...document.querySelectorAll('.change')],q=document.querySelector('#search'),s=document.querySelector('#scope');function filter(){const term=q.value.trim().toLowerCase();let n=0;for(const x of items){x.hidden=!!((s.value&&x.dataset.section!==s.value)||(term&&!x.textContent.toLowerCase().includes(term)));if(!x.hidden)n++}document.querySelector('#count').textContent=n+' 条'}q.addEventListener('input',filter);s.addEventListener('change',filter);filter()</script></main></html>''']
(OUT/'当前工程与上次出包差异.html').write_text('\n'.join(parts),encoding='utf-8')
print(json.dumps({'reportRecords':len(records),'fileDifferences':len(changes),'html':'当前工程与上次出包差异.html','markdown':'当前工程与上次出包差异.md'},ensure_ascii=True))
