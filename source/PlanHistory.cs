using System;
namespace Ara {
 public static class PlanHistory {
  public static void Reset(Day d){SharedEngine.DayCall("reset",d,null,true);}
  public static void Ensure(State s,Day d){SharedEngine.Run("historyEnsure",s,d);}
  public static void Advance(State before,Day old,State after,Day d){SharedEngine.Run("advance",after,d,JsonMap.Obj("before",SharedEngine.DayState(before,old)));}
 }
}
