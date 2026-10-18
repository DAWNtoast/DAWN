using System;
using System.Collections.Generic;
using System.Linq;
using ShatteredSail.Core.Gameplay;
internal static partial class SimulateNaval
{
 static bool Move(CoreVoyageSession s)
 {
  var b=s.Battle;if(!s.NavalActionMovementActive||b.NavalActionPoints<=0)return false;
  if(b.NavalAwaitingHeadingSelection)throw new InvalidOperationException("Unresolved heading");
  int range=CoreVoyageSession.NavalGridDistance(b.PlayerPositionMeters,b.EnemyPositionMeters);
  if(range>=3&&range<=4&&!s.EnemyWillRam)return false;
  s.ClearNavalMovementPath();var best=new List<VoyagePoint>();double bestScore=PositionScore(s,b.PlayerPositionMeters,0);int limit=Math.Min(7,b.NavalActionPoints);
  Search(s,limit,ref bestScore,ref best);
  s.ClearNavalMovementPath();if(best.Count==0)return false;
  foreach(var point in best)if(!s.TryAppendNavalMovementStep(point,out var reason))throw new InvalidOperationException(reason);
  if(!s.CommitNavalMovement(out var why))throw new InvalidOperationException(why);
  if(s.Phase==CoreDemoPhase.Battle&&b.Stage!=NavalBattleStage.Boarding&&b.NavalAwaitingHeadingSelection)
  {float heading=s.GetNavalArrivalHeadings().OrderBy(h=>HeadingScore(b,h)).First();if(!s.ConfirmNavalArrivalHeading(heading,out why))throw new InvalidOperationException(why);}
  return true;
 }
 static void Search(CoreVoyageSession s,int remaining,ref double bestScore,ref List<VoyagePoint> best)
 {
  if(remaining==0)return;
  foreach(var point in s.GetNavalLegalNextSteps().ToArray())
  {if(!s.TryAppendNavalMovementStep(point,out _))continue;double v=PositionScore(s,point,s.NavalMovementPlannedSteps);if(v>bestScore+.001){bestScore=v;best=s.NavalMovementPath.Skip(1).Select(n=>n.PositionMeters).ToList();}Search(s,remaining-1,ref bestScore,ref best);if(!s.UndoNavalMovementStep())throw new InvalidOperationException("Undo rejected");}
 }
 static double PositionScore(CoreVoyageSession s,VoyagePoint p,int steps)
 {int d=CoreVoyageSession.NavalGridDistance(p,s.Battle.EnemyPositionMeters);double v=d>4?-15*(d-4):d<3?-25*(3-d):8-Math.Abs(d-3)*.1;if(s.EnemyWillRam&&d<3)v-=60;return v-steps*.15;}
 static double HeadingScore(CoreBattleState b,float heading)
 {double rad=heading*Math.PI/180;var p=new VoyagePoint(b.PlayerPositionMeters.X+(float)Math.Round(Math.Cos(rad))*100,b.PlayerPositionMeters.Y+(float)Math.Round(Math.Sin(rad))*100);int d=CoreVoyageSession.NavalGridDistance(p,b.EnemyPositionMeters);return CoreVoyageSession.NavalGridDistance(b.PlayerPositionMeters,b.EnemyPositionMeters)>4?d:-d;}
}
