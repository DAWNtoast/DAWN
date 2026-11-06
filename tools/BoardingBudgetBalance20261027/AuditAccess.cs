// Test tool only: reads current visible geometry/equipment/fields; no RNG or mutation.
namespace ShatteredSail.Core.Gameplay {
 public sealed partial class CoreBattleState {
  internal CoreBattleState AuditPositionQuoteCopy(VoyagePoint position){var copy=(CoreBattleState)MemberwiseClone();copy.NavalFields=NavalFields.Copy();copy.PlayerPositionMeters=position;return copy;}
 }
 public sealed partial class CoreVoyageSession {
  // Narrow guard only: a currently out-of-range cannon policy must not enter an advertised
  // attack when it has no payable damaging card at that destination. No tactical risk reranking.
  public bool AuditCannonEntryHasThreatButNoAttack(VoyagePoint position) {
   if(!NavalFieldsActive)return false;var original=Battle;var quote=original.AuditPositionQuoteCopy(position);
   quote.DistanceMeters=NavalPointDistance(position,quote.EnemyPositionMeters);
   try{Battle=quote;bool threat=false;foreach(var intent in BuildBattleIntentPresentation())if(intent.Kind==CoreBattleIntentKind.Cannon&&(intent.Amount??0)>0)threat=true;
    if(!threat)return false;
    for(int i=0;i<quote.Hand.Count;i++)if(CanPlayCard(i)&&GetBattleCardDamage(GetBattleCard(quote.Hand[i]),NavalBattleStage.Naval)>0)return false;
    return true;
   }finally{Battle=original;}
  }
  // Evaluate the same public intent at a proposed legal destination on an isolated state shell.
  // BuildBattleIntentPresentation and its field contexts are pure; the original battle is restored in finally.
  public int AuditEnemyNavalDamageAt(VoyagePoint position,int finalBlock,out bool ram) {
   ram=false;if(!NavalFieldsActive)return 0;var original=Battle;var quote=original.AuditPositionQuoteCopy(position);
   quote.DistanceMeters=NavalPointDistance(position,quote.EnemyPositionMeters);quote.PlayerBlock=finalBlock;
   try{Battle=quote;int cannon=0,ramDamage=0;foreach(var intent in BuildBattleIntentPresentation()){
    if(intent.Kind==CoreBattleIntentKind.Cannon)cannon+=intent.Amount??0;
    if(intent.Kind==CoreBattleIntentKind.Ram){ram=true;ramDamage+=intent.Amount??0;}}
    return System.Math.Max(0,cannon-finalBlock)+ramDamage;
   } finally {Battle=original;}
  }
  public bool AuditTideBlocksStep(VoyagePoint from,VoyagePoint to)=>NavalFieldsActive&&NavalTideBlocksMovement(Battle.NavalFields,true,from,to);
  // Same deterministic, public boss-intent field preview used by the UI. All changes stay in a copied context.
  public bool AuditNextTurnTideBlocksHeading(float heading) {
   if(!NavalFieldsActive)return false;
   var context=CreateNavalFieldPreviewContext(false);var plan=BuildEnemyPlan();
   var path=BuildNavalFieldEnemyPath(plan.Move,context);var preview=PreviewNavalFields(path,false,context);
   if(preview.Sinks||EnemyPreviewEntersBoarding(preview,context))return false;
   PreviewNavalBossPlacedField(context.State,preview.FinalPositionMeters,context.PlayerPosition);
   context.State.PlayerTurn++;
   var from=context.PlayerPosition;var to=new VoyagePoint(from.X+(float)System.Math.Round(System.Math.Cos(heading*System.Math.PI/180))*100,from.Y+(float)System.Math.Round(System.Math.Sin(heading*System.Math.PI/180))*100);
   return NavalTideBlocksMovement(context.State,true,from,to);
  }
  public bool AuditPlannedArrivalContact(bool boarding,out int enemyDamage,out int selfDamage) {
   enemyDamage=selfDamage=0;if(!NavalFieldsActive||navalMovementPath.Count<2)return false;
   var preview=NavalMovementFieldPreview;int steps=preview.ActiveSteps;if(preview.Sinks||steps<1||Battle.NavalActionPoints-steps<1)return false;
   var pos=preview.FinalPositionMeters;float dx=System.Math.Abs(pos.X-Battle.EnemyPositionMeters.X),dy=System.Math.Abs(pos.Y-Battle.EnemyPositionMeters.Y);
   if(!((dx<.01f&&System.Math.Abs(dy-100)<.01f)||(dy<.01f&&System.Math.Abs(dx-100)<.01f)))return false;
   float incoming=navalMovementPath[System.Math.Min(steps,navalMovementPath.Count-1)].HeadingDegrees;
   bool allowed(float h){float turn=System.Math.Abs(NavalAngleDelta(incoming,h));return turn<NavalContactTolerance||System.Math.Abs(turn-90)<NavalContactTolerance;}
   if(boarding){float h=Battle.EnemyHeadingDegrees;if(!allowed(h)&&!allowed((h+180)%360))return false;bool horizontal=System.Math.Abs(System.Math.Sin(h*System.Math.PI/180))<.5;return horizontal?dy>50:dx>50;}
   if(!allowed(NavalBearing(pos,Battle.EnemyPositionMeters)))return false;
   float angle=System.Math.Abs(NavalAngleDelta(Battle.EnemyHeadingDegrees,NavalBearing(Battle.EnemyPositionMeters,pos)));float target=angle<NavalContactTolerance?Battle.EnemyBowQuality:Battle.EnemyHullQuality;
   RamDamage(Battle.PlayerBowQuality,target,out enemyDamage,out selfDamage);
   enemyDamage=NavalFieldAttackDamage(CoreBattleBalance.ScaleBattleValue(enemyDamage,CoreBattleBalance.PlayerRamDamageMultiplier),true,false,Battle.NavalFields,pos,Battle.EnemyPositionMeters);selfDamage=ApplyCurseBattleDamage(selfDamage);
   return Hull-preview.HullDamage>selfDamage;
  }
  public int AuditPlayerRamSelfDamageQuote(){RamDamage(Battle.PlayerBowQuality,NavalRamTargetQuality(true),out int damage,out int selfDamage);return ApplyCurseBattleDamage(selfDamage);}
  public int AuditPlayerRamDamageQuote() {
   RamDamage(Battle.PlayerBowQuality,NavalRamTargetQuality(true),out int damage,out int selfDamage);
   return NavalFieldAttackDamage(CoreBattleBalance.ScaleBattleValue(damage,CoreBattleBalance.PlayerRamDamageMultiplier),true,false);
  }
 }
}
