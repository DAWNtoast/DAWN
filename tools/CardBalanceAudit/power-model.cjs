'use strict';

/**
 * Transparent, deterministic EXPECTED EFFECT BUDGET, not a win-rate model.
 *
 * Input is the Unity export's flat TSV schema (SchoolEffect.Power etc.). No
 * school multiplier is applied. Target ratios occur only in audit(), never in
 * effect valuation. Reference rows fix role, original energy and peer budgets;
 * adding a new effect to a candidate cannot change its comparison group.
 *
 * Time window: three enemy actions, discount 0.8 per later action. All schools
 * use the same state distributions. Low/high are sensitivity cases, not fitted
 * confidence bounds. Reference crew is 28, no medicine, equipment firepower,
 * critical hits or external deck buffs. Those shared bonuses are excluded.
 *
 * Resource accounting: generated mana is credited at a frozen ordinary-card
 * shadow price; spenders subtract that SAME price at the consumer's rank.
 * Curse generation includes incremental weakness and future option value;
 * detonation/drain subtract the weakness/options they destroy. Thunder chains
 * arise naturally from EVERY card. Only extra Prime acceleration is credited,
 * while Finish relinquishes subsequent chain-conditioned options; natural
 * chains are never charged as though a Prime card paid for all of them.
 *
 * Sources: CoreSchoolBattleExecution.cs, CoreNavalBattleCombat.cs,
 * CoreSchoolCardMechanics.cs, CoreBattleCardUpgrades.cs, CoreNavalBattle.cs.
 */
const fs = require('node:fs');
const NUMERIC = ['LibraryIndex', 'Energy', 'Damage', 'Block', 'Repair', 'Draw',
  'PowderCost', 'CannonballCost', 'MovementMeters', 'StrategyAmount', 'StrategyLimit',
  'BurnDamage', 'BurnDuration', 'UpgradeLevel', ...['Power', 'Secondary', 'Threshold',
    'Duration', 'Limit', 'Movement'].map(x => 'SchoolEffect.' + x)];
const PARAMS = Object.freeze({
  version: 'expected-effect-budget-v1.1', damageValue: 1, blockUtilization: 0.78,
  healingUtilization: 0.8, drawValue: 2.5, futureDiscount: 0.8, horizon: 3,
  movementPerMeter: 0.015, movementCap: 180, movementAlignment: 1.6,
  ramOption: 2.0, boardingOption: 2.0, powderValue: 0.25, cannonballValue: 0.25,
  lifeCost: 1, manaUnitsPerOrdinaryCard: 3, curseOptionBudgetShare: 0.10,
  thunderOptionBudgetShare: 0.16, preparationChainThreshold: 2,
  preparationChainGuardShare: 0.5, referenceHealth: {Preparation: 100, Naval: 100, Boarding: 80},
  // Action-point opportunity curve is used ONLY when a candidate changes cost,
  // or an empty ordinary peer group needs explicit cross-cost extrapolation.
  handSlotCost: 0.75,
});
const SCENARIOS = Object.freeze({
  low: {burn: 0.3, hit: 0.78, continuation: 0.6, resourceUse: 0.6, guard: 0.4,
    missingFraction: 0.2, enemyAttack: 10, followupAttacks: 1, followupGuards: 1,
    futureCannon: 0.55, boarding: 0.2, statusReplace: 0.65, reserveRoom: 0.65,
    mana: [[0,.4],[2,.3],[4,.2],[6,.1]], marks: [[0,.4],[1,.3],[2,.2],[4,.1]],
    chain: [[0,.35],[1,.35],[2,.2],[3,.1]], preparationChain: [[0,.8],[1,.2]], guardAbsorbed: [[0,.6],[4,.25],[8,.1],[16,.05]],
    enemyLowHealth: 0.2, loadedEnemy: 0.15},
  typical: {burn: 0.6, hit: 0.9, continuation: 0.8, resourceUse: 0.8, guard: 0.65,
    missingFraction: 0.4, enemyAttack: 16, followupAttacks: 2, followupGuards: 2,
    futureCannon: 0.8, boarding: 0.4, statusReplace: 0.8, reserveRoom: 0.8,
    mana: [[0,.2],[2,.25],[4,.35],[6,.2]], marks: [[0,.2],[1,.2],[2,.3],[4,.3]],
    chain: [[0,.15],[1,.25],[2,.3],[3,.2],[4,.1]], preparationChain: [[0,.6],[1,.3],[2,.1]], guardAbsorbed: [[0,.35],[4,.3],[8,.2],[16,.15]],
    enemyLowHealth: 0.35, loadedEnemy: 0.3},
  high: {burn: 0.85, hit: 0.98, continuation: 0.95, resourceUse: 0.95, guard: 0.85,
    missingFraction: 0.6, enemyAttack: 24, followupAttacks: 3, followupGuards: 3,
    futureCannon: 0.95, boarding: 0.65, statusReplace: 0.95, reserveRoom: 0.95,
    mana: [[0,.05],[2,.15],[4,.35],[6,.3],[10,.15]], marks: [[0,.05],[1,.1],[2,.25],[4,.4],[6,.2]],
    chain: [[0,.05],[1,.1],[2,.2],[3,.3],[4,.25],[5,.1]], preparationChain: [[0,.4],[1,.35],[2,.25]], guardAbsorbed: [[0,.15],[4,.2],[8,.3],[16,.35]],
    enemyLowHealth: 0.5, loadedEnemy: 0.5},
});
const mean = xs => xs.length ? xs.reduce((a,b) => a+b, 0)/xs.length : 0;
const median = xs => { const a=xs.slice().sort((x,y)=>x-y); return a.length ? (a[(a.length-1)>>1]+a[a.length>>1])/2 : 0; };
const clamp = (v,a,b) => Math.max(a,Math.min(b,v));
const num = (r,k) => Number(r[k]) || 0;
const eff = (r,k) => num(r,'SchoolEffect.'+k);
const mechanic = r => r['SchoolEffect.Mechanic'] || '';
const expected = (dist, fn) => dist.reduce((sum,[value,p])=>sum+p*fn(value),0);
const probability = (dist, min) => expected(dist,x=>x>=min?1:0);
const ticks = (duration, start=0) => Array.from({length:clamp(Math.floor(duration),0,PARAMS.horizon)},(_,i)=>Math.pow(PARAMS.futureDiscount,i+start)).reduce((a,b)=>a+b,0);

function parseTsv(text) {
  const lines = text.replace(/^\uFEFF/,'').trimEnd().split(/\r?\n/);
  const heads = lines.shift().split('\t');
  return lines.filter(Boolean).map(line=>{
    const values=line.split('\t'), row=Object.fromEntries(heads.map((key,i)=>[key,values[i]??'']));
    for(const key of NUMERIC) row[key]=num(row,key);
    return row;
  });
}
const readTsv = path => parseTsv(fs.readFileSync(path,'utf8'));
function stagesFor(row) {
  const stage=row.AllowedStages;
  if(stage==='All') return ['Preparation','Naval','Boarding'];
  if(stage==='Ship') return ['Preparation','Naval'];
  return ['Preparation','Naval','Boarding'].filter(s=>(stage||'').split(/,\s*/).includes(s));
}
function roleFor(row) {
  const m=mechanic(row);
  if(num(row,'Damage')>0) return 'attack';
  if(num(row,'Repair')>0 || ['FireHeal','ArcaneMend','CurseDrain','TideRenew','BloodMend'].includes(m)) return 'recovery';
  if(num(row,'MovementMeters')>0 || eff(row,'Movement')!==0) return 'maneuver';
  if(['Polish','Load','PrecisionLoad','FireRounds'].includes(row.Action) ||
    ['FireLoad','FireExtend','FireOverflow','ArcaneCharge','CurseMark','ThunderPrime'].includes(m)) return 'setup';
  if(num(row,'Block')>0 || ['FirePrepareGuard','FireNaturalGuard','FireGuard','ArcaneWard','CurseWard',
    'TideWard','TideShift','TideCleanse','ThunderWard','BloodWard'].includes(m)) return 'guard';
  return 'draw';
}
function upgradeRow(base, level) {
  if(num(base,'UpgradeLevel')!==0) throw new Error('upgradeRow requires an authored level-0 row');
  level=clamp(Math.floor(level),0,3);
  const row={...base,UpgradeLevel:level};
  if(!level) return row;
  for(const key of ['Damage','Block','Repair','StrategyAmount','StrategyLimit','BurnDamage']) row[key]=num(base,key)*2;
  row.Energy=Math.max(0,num(base,'Energy')-(level>=2?1:0));
  row.Draw=num(base,'Draw')+(level>=3?1:0);
  const flags=(base['SchoolEffect.Upgrades']||'None').split(/,\s*/);
  for(const key of ['Power','Secondary','Threshold','Duration','Limit','Movement'])
    row['SchoolEffect.'+key]=eff(base,key)*(flags.includes(key)?2:1);
  // Movement property uses the effect override if nonzero, else the ordinary
  // action's rank-scaled movement. This distinction is identical to Unity.
  row.MovementMeters=eff(base,'Movement')!==0 ? Math.abs(eff(row,'Movement')) : num(base,'MovementMeters')*2;
  row['SchoolEffect.Upgrades']='None';
  return row;
}
const expandUpgrades = bases => bases.flatMap(base=>[0,1,2,3].map(level=>upgradeRow(base,level)));

function stageValue(row, stage, model, scenario='typical') {
  const s=typeof scenario==='string'?SCENARIOS[scenario]:scenario;
  if(!s) throw new Error('Unknown scenario '+scenario);
  const p=PARAMS, level=num(row,'UpgradeLevel'), m=mechanic(row), terms={};
  const add=(key,value)=>{if(value) terms[key]=(terms[key]||0)+value;};
  const shadow=model?.shadowByLevel?.[level] || 8*(level>0?2:1);
  const manaPrice=shadow/p.manaUnitsPerOrdinaryCard;
  const markPrice=shadow*p.curseOptionBudgetShare;
  const chainPrice=shadow*p.thunderOptionBudgetShare;
  const chainDistribution=stage==='Preparation'?s.preparationChain:s.chain;
  const power=eff(row,'Power'), secondary=eff(row,'Secondary'), threshold=eff(row,'Threshold'),
    duration=eff(row,'Duration')||2, limit=eff(row,'Limit'), effectiveThreshold=threshold||1;
  const block=x=>Math.max(0,x)*p.blockUtilization;
  const missing=p.referenceHealth[stage]*s.missingFraction;
  const heal=x=>Math.min(Math.max(0,x),missing)*p.healingUtilization;
  const future=ticks(duration)*s.continuation;
  const burnPower=5*(level>0?2:1); // Frozen representative EXISTING burn, not a new implicit multiplier.
  const damage=num(row,'Damage');
  add('directDamage',damage*p.damageValue);
  add('effectiveBlock',block(num(row,'Block')));
  add('effectiveHealing',heal(num(row,'Repair')));
  add('draw',num(row,'Draw')*p.drawValue);
  add('ammunitionCost',-num(row,'PowderCost')*p.powderValue-num(row,'CannonballCost')*p.cannonballValue);

  if(stage!=='Boarding') {
    const meters=Math.abs(eff(row,'Movement')) || (!m ? num(row,'MovementMeters') : 0);
    if(meters) {
      add('movementDistance',Math.min(p.movementCap,meters)*p.movementPerMeter);
      const action=row.Action;
      if(eff(row,'Movement')>0 || ['Navigator','Parallel'].includes(action))
        add('boardingAlignment',p.boardingOption*s.boarding+p.movementAlignment*.5);
      if(!m&&['Approach','Turn'].includes(action)) add('ramOption',p.ramOption*s.continuation);
      if(!m&&['Circle','Turn'].includes(action)) add('broadsideAlignment',p.movementAlignment*s.hit);
      if((eff(row,'Movement')<0 || action==='Retreat')) add('distanceControl',p.movementAlignment*.5);
    }
    if(!m) {
      const amount=num(row,'StrategyAmount');
      if(row.Action==='Polish') add('persistentDeckGuard',block(Math.min(amount,num(row,'StrategyLimit')))*s.followupGuards*s.continuation);
      if(['Load','PrecisionLoad'].includes(row.Action)) add('loadedShot',Math.min(amount,num(row,'StrategyLimit'))*s.futureCannon*s.reserveRoom);
      if(row.Action==='FireRounds') add('storedBurn',num(row,'BurnDamage')*ticks(num(row,'BurnDuration'))*
        Math.min(amount,1+s.futureCannon*.5)*s.futureCannon*s.reserveRoom*s.statusReplace);
    }
  }
  const conditional=(name,value,prob)=>add(name,value*clamp(prob,0,1));
  const manaAvailable=probability(s.mana,threshold);
  const markAvailable=probability(s.marks,threshold);
  switch(m) {
    case '': case 'None': break;
    case 'FireLoad':
      add('storedBurn',power*ticks(duration)*Math.min(secondary,limit||4,1+s.futureCannon*.5)*s.futureCannon*s.reserveRoom*s.statusReplace); break;
    case 'FireIgnite': add('newBurn',power*future*s.hit*s.statusReplace); break;
    case 'FireIgniteOrExtend':
      add('newBurn',(1-s.burn)*power*future*s.hit);
      add('extendedBurn',s.burn*burnPower*ticks(Math.min(secondary||1,2),1)*s.hit*s.continuation); break;
    case 'FireExtend': add('extendedBurn',s.burn*burnPower*ticks(Math.min(power,2),1)*s.continuation*(damage>0?s.hit:1)); break;
    case 'FireExploit': conditional('burnConditionalDamage',power,s.burn); break;
    case 'FireGuard': conditional('burnConditionalBlock',block(power),s.burn); break;
    case 'FireHeal': conditional('burnConditionalHeal',heal(num(row,'Repair')+power)-heal(num(row,'Repair')),s.burn); break;
    case 'FirePrepareGuard': conditional('preparedFireGuard',block(power)*s.continuation,s.burn); break;
    case 'FireNaturalGuard': conditional('naturalBurnGuard',block(power)*future,s.burn); break;
    case 'FireBoardingSeed':
      add('newBurn',power*future*s.hit*s.statusReplace);
      if(stage!=='Boarding') add('boardingBurn',secondary*future*s.boarding*p.futureDiscount*s.hit); break;
    case 'FireBoardingCover': if(stage!=='Boarding') add('boardingCover',block(power)*s.burn*s.boarding*p.futureDiscount); break;
    case 'FireExtraTick':
      add('newBurn',power*future*s.hit*s.statusReplace); add('extraBurnTick',power*s.burn*s.hit); break;
    case 'FireScorch': add('futureScorch',power*Math.min(secondary||2,s.followupAttacks)*s.burn*s.hit*s.continuation); break;
    case 'FireOverflow': add('overflowLoad',Math.min(power,limit||power,threshold||power)*s.burn*s.burn*s.futureCannon*s.reserveRoom); break;
    case 'ArcaneCharge':
      add('manaCreated',expected(s.mana,x=>Math.min(power,Math.max(0,(limit||12)-x)))*manaPrice*s.resourceUse*p.futureDiscount); break;
    case 'ArcaneRelease': case 'ArcaneWard': case 'ArcaneMend': case 'ArcaneEcho': {
      const existing=Object.values(terms).reduce((a,b)=>a+b,0);
      add('manaAvailability',(manaAvailable-1)*existing);
      const extra=m==='ArcaneRelease'?power:m==='ArcaneWard'?block(power):m==='ArcaneMend'?
        heal(num(row,'Repair')+power)-heal(num(row,'Repair')):power*p.drawValue;
      add('manaConditionalBenefit',extra*manaAvailable);
      add('manaConsumed',-threshold*manaPrice*manaAvailable); break;
    }
    case 'CurseMark': {
      const gain=expected(s.marks,x=>Math.min(power,Math.max(0,(limit||6)-x)));
      const weakness=expected(s.marks,x=>(Math.min(50,(x+Math.min(power,Math.max(0,(limit||6)-x)))*secondary)-Math.min(50,x*secondary))/100);
      add('curseWeakness',weakness*s.enemyAttack*future);
      add('curseOptionsCreated',gain*markPrice*s.resourceUse*p.futureDiscount); break;
    }
    case 'CurseExploit': conditional('curseConditionalDamage',power,probability(s.marks,effectiveThreshold)); break;
    case 'CurseWard': conditional('curseConditionalBlock',block(power),probability(s.marks,effectiveThreshold)); break;
    case 'CurseDetonate': {
      const marks=expected(s.marks,x=>x);
      add('curseDetonation',power*marks);
      add('curseOptionsConsumed',-marks*markPrice);
      add('curseWeaknessLost',-Math.min(.5,marks*.04)*s.enemyAttack*s.continuation); break;
    }
    case 'CurseDrain': {
      const existing=Object.values(terms).reduce((a,b)=>a+b,0);
      add('curseAvailability',(markAvailable-1)*existing);
      add('curseConditionalHeal',(heal(num(row,'Repair')+power)-heal(num(row,'Repair')))*markAvailable);
      add('curseOptionsConsumed',-threshold*markPrice*markAvailable);
      add('curseWeaknessLost',-threshold*.04*s.enemyAttack*s.continuation*markAvailable); break;
    }
    case 'TideWard': conditional('guardCounter',power*future,probability(s.guardAbsorbed,effectiveThreshold)); break;
    case 'TideRiposte': conditional('guardConditionalDamage',power,probability(s.guardAbsorbed,effectiveThreshold)); break;
    case 'TideRenew': conditional('guardConditionalHeal',heal(num(row,'Repair')+power)-heal(num(row,'Repair')),probability(s.guardAbsorbed,effectiveThreshold)); break;
    case 'TideShift':
      if(threshold===1) add('attackStance',power*s.followupAttacks*future);
      else if(threshold===2) add('defenseStance',Math.min(power,s.enemyAttack)*future*s.guard);
      else add('counterStance',power*future*s.guard); break;
    case 'TideCleanse': add('cleanseGuard',block(power)); add('enemyLoadRemoved',4*s.loadedEnemy); break;
    case 'ThunderPrime': {
      if(stage==='Preparation') {
        // Preparation is one window, and entering Naval CLEARS the chain.
        // Its only consumers are Ward/Echo: price a 50/50 ordinary guard/draw
        // opportunity, never a later cannon or finisher. Compare against the
        // free +1 that playing any ordinary card would already have produced.
        const gate=p.preparationChainThreshold;
        const extraReady=expected(chainDistribution,x=>
          (Math.min(limit||6,x+1+power)>=gate?1:0)-(Math.min(limit||6,x+1)>=gate?1:0));
        const rankScale=shadow/(model?.shadowByLevel?.[0]||8);
        const reward=block(shadow)*p.preparationChainGuardShare+
          p.drawValue*rankScale*(1-p.preparationChainGuardShare);
        add('preparationWardEchoAcceleration',extraReady*reward*s.resourceUse);
      } else {
        const gained=expected(chainDistribution,x=>Math.min(power,Math.max(0,(limit||6)-x-1)));
        add('chainAcceleration',gained*chainPrice*s.resourceUse);
      }
      break;
    }
    case 'ThunderChain': conditional('chainConditionalDamage',power,probability(chainDistribution,effectiveThreshold)); break;
    case 'ThunderWard': conditional('chainConditionalBlock',block(power),probability(chainDistribution,effectiveThreshold)); break;
    case 'ThunderEcho': conditional('chainConditionalDraw',power*p.drawValue,probability(chainDistribution,effectiveThreshold)*s.reserveRoom); break;
    case 'ThunderFinish': {
      const chain=expected(s.chain,x=>x);
      add('chainFinisher',power*chain);
      add('chainOptionsLost',-chain*chainPrice*.35*s.resourceUse); break;
    }
    case 'BloodRisk': add('lifePaidDamage',power); add('lifeCost',-threshold*p.lifeCost); break;
    case 'BloodWard': add('lifePaidBlock',block(power)); add('lifeCost',-threshold*p.lifeCost); break;
    case 'BloodDrain': add('damageDrain',heal(Math.min(limit||Number.MAX_SAFE_INTEGER,damage*s.hit*Math.min(100,power)/100))); break;
    case 'BloodMend': add('missingHealthHeal',heal(Math.min(limit||Number.MAX_SAFE_INTEGER,missing*Math.min(100,power)/100))); break;
    case 'BloodExecute': conditional('lowHealthDamage',power,s.enemyLowHealth*clamp(threshold/35,0,100/35)); break;
    default: throw new Error('Unmodelled mechanic: '+m+' on '+row.Id);
  }
  let score=Object.values(terms).reduce((a,b)=>a+b,0);
  const meta=model?.meta?.[row.Id];
  if(meta) {
    const expectedCost=Math.max(0,meta.originalEnergy-(level>=2?1:0));
    const adjustment=(expectedCost+p.handSlotCost)/(num(row,'Energy')+p.handSlotCost);
    if(adjustment!==1) {add('changedEnergyOpportunity',score*(adjustment-1));score*=adjustment;}
  }
  return {stage,score,terms,shadow:{mana:manaPrice,curseMark:markPrice,extraChain:chainPrice}};
}

function buildModel(referenceRows) {
  const bases=referenceRows.filter(row=>num(row,'UpgradeLevel')===0);
  if(!bases.length) throw new Error('Reference needs level-0 rows');
  const rows=referenceRows.length===bases.length?expandUpgrades(bases):referenceRows;
  const meta=Object.fromEntries(bases.map(row=>[row.Id,{role:roleFor(row),originalEnergy:num(row,'Energy'),stages:stagesFor(row)}]));
  const model={version:PARAMS.version,params:PARAMS,scenarios:SCENARIOS,meta,shadowByLevel:{},peers:{},fallbacks:[],referenceCount:bases.length};
  const ordinary=rows.filter(row=>!row.SchoolId);
  // Derive ordinary effect prices before evaluating any special mechanic.
  for(const level of [0,1,2,3]) {
    const values=ordinary.filter(row=>num(row,'UpgradeLevel')===level&&meta[row.Id].originalEnergy===1)
      .map(row=>mean(stagesFor(row).map(stage=>stageValue(row,stage,null).score)));
    model.shadowByLevel[level]=median(values);
  }
  for(const scenario of Object.keys(SCENARIOS)) {
    const samples=[];
    for(const row of ordinary) for(const stage of stagesFor(row)) {
      const {role,originalEnergy}=meta[row.Id];
      samples.push({role,originalEnergy,stage,level:num(row,'UpgradeLevel'),score:stageValue(row,stage,model,scenario).score,id:row.Id});
    }
    for(const row of rows) for(const stage of stagesFor(row)) {
      const {role,originalEnergy}=meta[row.Id],level=num(row,'UpgradeLevel');
      const key=[scenario,role,stage,originalEnergy,level].join('|');
      if(model.peers[key]) continue;
      let match=samples.filter(x=>x.role===role&&x.stage===stage&&x.level===level&&x.originalEnergy===originalEnergy);
      let method='same role / stage / original energy / upgrade',factor=1;
      if(!match.length) {
        match=samples.filter(x=>x.role===role&&x.stage===stage&&x.level===level&&x.originalEnergy===1);
        method='same role / stage / upgrade; energy extrapolated from 1';
        factor=(originalEnergy+PARAMS.handSlotCost)/(1+PARAMS.handSlotCost);
      }
      if(!match.length) {
        match=samples.filter(x=>x.role===role&&x.level===level&&x.originalEnergy===1);
        method='same role / upgrade; other stage + energy extrapolation';
      }
      if(!match.length) {
        match=samples.filter(x=>x.stage===stage&&x.level===level&&x.originalEnergy===1);
        method='same stage / upgrade; role unavailable + energy extrapolation';
      }
      if(!match.length) throw new Error('No ordinary baseline for '+key);
      model.peers[key]={baseline:median(match.map(x=>x.score))*factor,method,ids:[...new Set(match.map(x=>x.id))]};
      if(method!=='same role / stage / original energy / upgrade') model.fallbacks.push({key,...model.peers[key]});
    }
  }
  return model;
}
function evaluateRow(row, model, scenario='typical') {
  const meta=model.meta[row.Id];
  if(!meta) throw new Error('Candidate not present in frozen reference: '+row.Id);
  const level=num(row,'UpgradeLevel');
  const stageRows=meta.stages.map(stage=>{
    const value=stageValue(row,stage,model,scenario),peer=model.peers[[scenario,meta.role,stage,meta.originalEnergy,level].join('|')];
    return {...value,baseline:peer.baseline,ratio:value.score/peer.baseline,baselineMethod:peer.method,baselineIds:peer.ids};
  });
  return {id:row.Id,name:row.Name,school:row.SchoolId||'ordinary',role:meta.role,level,
    originalEnergy:meta.originalEnergy,energy:num(row,'Energy'),scenario,
    score:mean(stageRows.map(x=>x.score)),baseline:mean(stageRows.map(x=>x.baseline)),
    ratio:mean(stageRows.map(x=>x.ratio)),stageRows};
}
function audit(rows, model, options={}) {
  const targets={ordinary:1,special:1.3,...options.targets};
  const cardTolerance=options.cardTolerance??0.10, schoolTolerance=options.schoolTolerance??0.03;
  const scenarios=options.scenarios||['typical','low','high'];
  const cards=rows.flatMap(row=>scenarios.map(scenario=>evaluateRow(row,model,scenario)));
  const groups=new Map();
  for(const card of cards) {
    const key=[card.school,card.scenario,card.level].join('|');
    if(!groups.has(key)) groups.set(key,[]);groups.get(key).push(card);
  }
  const schools=[...groups.values()].map(group=>{
    const first=group[0],target=first.school==='ordinary'?targets.ordinary:targets.special;
    const ratio=mean(group.map(x=>x.ratio));
    return {school:first.school,scenario:first.scenario,level:first.level,count:group.length,ratio,
      minRatio:Math.min(...group.map(x=>x.ratio)),maxRatio:Math.max(...group.map(x=>x.ratio)),
      target,withinSchoolTolerance:Math.abs(ratio-target)<=schoolTolerance};
  });
  const worst=cards.filter(x=>x.scenario==='typical').map(card=>({...card,
    target:card.school==='ordinary'?targets.ordinary:targets.special,
    deviation:Math.abs(card.ratio-(card.school==='ordinary'?targets.ordinary:targets.special))}))
    .sort((a,b)=>b.deviation-a.deviation);
  return {version:model.version,definitionCount:new Set(rows.map(r=>r.Id)).size,rowCount:rows.length,
    targets,cardTolerance,schoolTolerance,cards,schools,worst,
    outsideCardTolerance:worst.filter(x=>x.deviation>cardTolerance),baselineFallbacks:model.fallbacks,
    limitations:['Expected-effect units, not win rate or proof of PvP equivalence.',
      'Fixed scenario distributions; actual deck order, enemy AI, repeated-play energy and encounter duration require gameplay validation.',
      'Ordinary baseline is frozen current median, with explicitly disclosed empty-group fallback.',
      'Low/high sensitivity uses the same frozen ordinary reference evaluated in that scenario.']};
}

module.exports={PARAMS,SCENARIOS,NUMERIC,parseTsv,readTsv,stagesFor,roleFor,upgradeRow,expandUpgrades,stageValue,buildModel,evaluateRow,audit};
if(require.main===module) {
  const path=process.argv[2];
  if(!path) {console.error('Usage: node power-model.cjs cards.tsv [candidate.tsv]');process.exitCode=2;}
  else {
    const reference=readTsv(path),model=buildModel(reference),candidate=process.argv[3]?readTsv(process.argv[3]):reference;
    const result=audit(candidate,model);
    console.log(JSON.stringify({version:result.version,definitionCount:result.definitionCount,rowCount:result.rowCount,
      shadows:model.shadowByLevel,schools:result.schools.filter(x=>x.scenario==='typical'),
      worst:result.worst.slice(0,20).map(({stageRows,...x})=>x),fallbackCount:model.fallbacks.length},null,2));
  }
}
