using System;
using System.Linq;
using ShatteredSail.Core.Gameplay;
public static partial class BattleModes {
 // Explicit stage-only counterfactual. Never used by full-route acceptance samples.
 static void ApplyDiagnosticPressure(CoreBattleState b) {
  if(diagnosticAttackScale==1)return;
  var deck=b.BoardingEnemy.Deck;
  int scale(int v)=>CoreBattleBalance.ScaleBattleValue(v,(decimal)diagnosticAttackScale);
  b.BoardingEnemy.Deck=new CoreBoardingEnemyDeck(deck.SchoolId,deck.Turns.Select(t=>new CoreBoardingEnemyTurn(t.Name,t.Cards.Select(c=>new CoreBoardingEnemyCard(c.Id,c.Name,scale(c.AttackPercent),c.GuardPercent,c.BurnPercent,c.HealPercent,c.DrainPercent,c.SelfDamagePercent,c.PiercePercent,c.Mechanic,c.ResourceAmount,scale(c.BonusPercent),c.Energy)).ToArray())).ToArray());
 }
}
