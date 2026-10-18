using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ShatteredSail.Core.Gameplay;

internal static partial class SimulateNaval
{
 static double Score(CoreVoyageSession s,int index)
 {
  var b=s.Battle;var c=s.GetBattleCard(index);var fx=s.GetBattleCardEffects(c,b.Stage);bool prep=b.Stage==NavalBattleStage.Preparation;
  int cost=s.GetBattleCardEnergyCost(index),damage=s.GetBattleCardDamage(c,b.Stage),range=CoreVoyageSession.NavalGridDistance(b.PlayerPositionMeters,b.EnemyPositionMeters);
  int incoming=Incoming(s),need=Math.Max(0,(prep?14:incoming)-b.PlayerBlock);double heal=s.Hull<s.MaxHull*.55?2.5:.8;
  double score=Math.Min(s.MaxHull-s.Hull,fx.Repair)*heal+Math.Min(need,fx.Block+(c.Block>0?b.PlayerDeckBuff:0))*(s.Hull<=incoming*2?2:1.1);
  if(!prep){if(damage>=b.EnemyHull+b.EnemyBlock)return 10000/(cost+.65);score+=damage*1.7;}
  if(c.Draw>0&&b.Hand.Count<7)score+=c.Draw*(b.Hand.Count<4?6:2);
  int recovery=s.GetBattleCardEnergyRecovery(index);if(recovery>0&&b.Hand.Any(i=>i!=index&&s.GetBattleCardEnergyCost(i)>0))score+=recovery*10;
  if(c.Action==NavalBattleCardAction.Polish&&b.PlayerDeckBuff<c.StrategyLimit)score+=prep?30:8;
  if((c.Action==NavalBattleCardAction.Load||c.Action==NavalBattleCardAction.PrecisionLoad)&&b.PlayerLoadedShot<c.StrategyLimit)score+=prep?22:8;
  if(c.NavalActionPointGain>0&&b.NavalActionPoints<5&&(range>4||range<2||s.EnemyWillRam))score+=25*c.NavalActionPointGain;
  var effect=c.SchoolEffect;
  if(effect!=null)
  {
   int absorbed=Math.Min(need,fx.Block+(c.Block>0?b.PlayerDeckBuff:0));
   int effectiveDamage=Math.Min(b.EnemyHull,Math.Max(0,damage-b.EnemyBlock));
   switch(effect.Mechanic)
   {
    case CoreSchoolMechanic.TideWard:
     if(s.GetBattleCardWoodCost(index)>0)score+=absorbed*.55;
     break;
    case CoreSchoolMechanic.TideRiposte:
     // The quote already includes every consumed tide point. Shield break has
     // some value, but does not justify spending the entire stored resource.
     score-=Math.Min(damage,b.EnemyBlock)*1.2;
     break;
    case CoreSchoolMechanic.ThunderReady:
     score+=Math.Max(0,effect.Power-b.Schools.ThunderReadyDamage)*(prep?1:1.4);
     break;
    case CoreSchoolMechanic.ThunderFinish:
     if(b.Schools.ThunderChain<2&&b.Hand.Any(i=>i!=index&&s.GetBattleCard(i).SchoolId=="thunder"&&s.GetBattleCard(i).Damage>0&&s.GetBattleCardEnergyCost(i)+cost<=b.Energy))score-=7;
     break;
    case CoreSchoolMechanic.BloodRisk:
    case CoreSchoolMechanic.BloodWard:
     score-=Math.Min(effect.Threshold,Math.Max(0,s.Hull-1))*heal;
     break;
    case CoreSchoolMechanic.BloodDrain:
     score+=Math.Min(s.MaxHull-s.Hull,Math.Min(effect.Limit>0?effect.Limit:int.MaxValue,(int)((long)effectiveDamage*Math.Min(100,effect.Power)/100)))*heal;
     break;
   }
  }
  return score/(cost+.65);
 }
 // Ramming bypasses guard; its public warning affects movement, never shield value.
 static int Incoming(CoreVoyageSession s){var m=Regex.Match(s.GetEnemyIntent()??"","炮击：伤害 (\\d+)");return m.Success?int.Parse(m.Groups[1].Value):0;}
}
