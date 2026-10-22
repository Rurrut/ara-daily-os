using System;using System.Collections.Generic;
namespace Ara {
 public static class Adaptive {
  public static void Upgrade(State s){SharedEngine.Run("upgradeAdaptive",s);}
  public static CellBlock ActualWeight(Day d){return ActualActivity(d,"웨이트");}
  public static CellBlock ActualActivity(Day d,string category){var r=SharedEngine.DayCall("activity",d,JsonMap.Obj("category",category));return r["block"]==null?null:SharedEngine.Read<CellBlock>(r["block"]);}
  public static DateTime Target(State s,Day d){return d.Wake.Date.AddMinutes(Convert.ToDouble(SharedEngine.Value(SharedEngine.Run("target",s,d,null,false))));}
  public static DateTime? MealEnd(Day d,GridPlan p,string id){var r=SharedEngine.Run("mealEnd",new State{Days=new List<Day>{d}},d,JsonMap.Obj("plan",SharedEngine.Json(p),"meal",id),false);var value=SharedEngine.Value(r);return value==null?(DateTime?)null:d.Wake.Date.AddMinutes(Convert.ToDouble(value));}
  public static DateTime? FourthMealEnd(Day d,GridPlan p){return MealEnd(d,p,"b15");}
  public static DateTime EstimatedSleep(State s,Day d,GridPlan p){return Target(s,d);}
  public static DateTime RequiredFinish(State s,Day d,GridPlan p){return d.Wake.Date.AddMinutes(Convert.ToDouble(SharedEngine.Value(SharedEngine.Run("requiredFinish",s,d,JsonMap.Obj("plan",SharedEngine.Json(p)),false))));}
  public static void DescribeManual(State s,Day d,GridPlan p){SharedEngine.CopyFields(SharedEngine.Read<GridPlan>(SharedEngine.Run("manual",s,d,JsonMap.Obj("plan",SharedEngine.Json(p)))["plan"]),p);}
  static void FrontWaitBuffer(State s,Day d,GridPlan p,DateTime cutoff,string phase){SharedEngine.CopyFields(SharedEngine.Read<GridPlan>(SharedEngine.Run("frontBuffer",s,d,JsonMap.Obj("plan",SharedEngine.Json(p),"cutoff",SharedEngine.Json(cutoff),"phase",phase))["plan"]),p);}
  public static void Rebalance(State s,Day d,GridPlan p){SharedEngine.CopyFields(SharedEngine.Read<GridPlan>(SharedEngine.Run("rebalance",s,d,JsonMap.Obj("plan",SharedEngine.Json(p)))["plan"]),p);}
 }
}
