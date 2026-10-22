using System;
namespace Ara {
 public static class Routine {
  public static readonly string[] MealIds={"b1","b6","b10","b15","b16"};
  public static void ApplyDefaults(State s){SharedEngine.Run("defaults",s);}
  public static void Upgrade(State s,DateTime date){SharedEngine.Run("upgradeRoutine",s,null,JsonMap.Obj("date",date.ToString("yyyy-MM-dd")));}
  public static bool ShortenYogurt(State s,DateTime now){return Convert.ToBoolean(SharedEngine.Value(SharedEngine.Run("shortenYogurt",s,null,JsonMap.Obj("now",ScheduleNotifications.Epoch(now)))));}
  public static GridPlan Build(State s,Day d){return SharedEngine.Build(s,d);}
 }
}
