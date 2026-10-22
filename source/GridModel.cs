using System;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.IO;

namespace Ara {
 [Serializable,DataContract] public class CellBlock {
  [DataMember] public string Id;
  [DataMember] public DateTime Start;
  [DataMember] public int Count;
  [DataMember] public string Name="";
  [DataMember] public string Category="생활";
  [DataMember] public decimal Score;
  [DataMember] public decimal? SatisfactionValue;
  public decimal Satisfaction{get{return SatisfactionValue??2;}set{SatisfactionValue=value;}}
  [DataMember] public string TaskId="";
  [DataMember] public string TemplateId="";
  [DataMember] public string MealId="";
  [DataMember] public string PrepFor="";
  [DataMember] public int Priority;
  [DataMember] public int Minimum;
  [DataMember(EmitDefaultValue=false)] public bool Fixed;
  public DateTime End { get { return Start.AddMinutes(Count*10); } }
  public CellBlock Copy(){return (CellBlock)MemberwiseClone();}
  public string Caption{get{return string.IsNullOrWhiteSpace(Name)?Category:Name;}}
 }
 public class GridPlan {
  public bool YogurtRestRelaxed;
  public int? YogurtRestMinutes;
  public List<CellBlock> Blocks;
  public List<string> Changes=new List<string>();
  public int Saved,Overflow,FrontWaitSpent;
  public int? PostFourthRestMinutes;
  public List<CellBlock> VisualBlocks;
  public List<CellBlock> DisplayBlocks{get{return VisualBlocks??Blocks;}}
  public HashSet<string> FixedIds=new HashSet<string>();
  public DateTime? FutureFloor;
 }
 public static class GridEngine {
  public static readonly string[] Categories={"기상","작업","학습","식사","끼니 준비","존2","웨이트","휴식","생활","기타"};
  public static DateTime Snap(DateTime time){return new DateTime(((time.Ticks+TimeSpan.TicksPerMinute*5)/(TimeSpan.TicksPerMinute*10))*(TimeSpan.TicksPerMinute*10),time.Kind);}
  public static string Category(Block b){return b.Id=="b4"?"존2":b.Id=="b8"?"웨이트":b.Kind=="prep"?"끼니 준비":b.Kind=="work"?"작업":b.Kind=="study"?"학습":b.Kind=="meal"?"식사":b.Kind=="rest"?"휴식":"생활";}
  public static string Kind(string c){return c=="작업"?"work":c=="학습"?"study":c=="식사"?"meal":c=="존2"||c=="웨이트"?"exercise":c=="휴식"?"rest":"life";}
  public static bool MealName(string name){string n=(name??"").Replace(" ","");return !n.Contains("준비")&&new[]{"1끼니","2끼니","3끼니","4끼니","5끼니","아침","점심","저녁","첫식사","두번째식사","세번째식사","요거트","요구르트","식사","밥"}.Any(x=>n.StartsWith(x));}
  public static List<CellBlock> Generate(State s,Day d){return SharedEngine.Read<List<CellBlock>>(SharedEngine.Run("generate",s,d,null,false)["blocks"]);}
  public static void Ensure(State s,Day d){SharedEngine.Run("ensure",s,d);}
  public static bool Overlap(CellBlock b,DateTime start,DateTime end){return b.Start<end&&b.End>start;}
  public static void Validate(Day d){try{SharedEngine.DayCall("validateDay",d);}catch(InvalidOperationException error){throw new InvalidDataException(error.Message,error);}}
  public static string DetectMeal(Day d,GridPlan p,CellBlock record){return Convert.ToString(SharedEngine.Value(SharedEngine.Run("detectMeal",new State{Days=new List<Day>{d}},d,JsonMap.Obj("plan",SharedEngine.Json(p),"block",SharedEngine.Json(record)),false)));}
  public static GridPlan Build(State s,Day d){return SharedEngine.Build(s,d);}
  public static decimal SatisfactionAverage(State s,Day d,GridPlan p){return SatisfactionPolicy.Calculate(s,d,Adaptive.EstimatedSleep(s,d,p),DateTime.Now).Average;}
  public static decimal SatisfactionAverage(Day d){return Convert.ToDecimal(JsonMap.Child(SharedEngine.DayCall("stats",d),"value")["satisfaction"]);}
  public static decimal Total(Day d){return Convert.ToDecimal(JsonMap.Child(SharedEngine.DayCall("stats",d),"value")["total"]);}
  public static void SetScore(List<CellBlock> list,DateTime start,int count,decimal score,bool satisfaction=false){SharedEngine.EditList(list,start.Date,"score",JsonMap.Obj("start",SharedEngine.Json(start),"count",count,"value",score,"satisfaction",satisfaction));}
  public static void SetRange(List<CellBlock> list,CellBlock entry){SharedEngine.EditList(list,entry.Start.Date,"range",JsonMap.Obj("block",SharedEngine.Json(entry),"start",SharedEngine.Json(entry.Start)));}
  public static void ClearRange(List<CellBlock> list,DateTime start,int count){SharedEngine.EditList(list,start.Date,"clear",JsonMap.Obj("start",SharedEngine.Json(start),"count",count));}
  public static void Move(List<CellBlock> list,string id,DateTime target){
   var b=list.Find(x=>x.Id==id);if(b==null)throw new InvalidOperationException("이동할 블록이 없습니다.");
   DateTime end=target.AddMinutes(b.Count*10);
   if(list.Any(x=>x.Id!=id&&Overlap(x,target,end)))throw new InvalidOperationException("이동할 위치에 다른 활동이 있습니다. 빈 칸으로 옮기거나 먼저 범위를 수정해 주세요.");
   b.Start=target;list.Sort((a,c)=>a.Start.CompareTo(c.Start));
  }
  public static void MovePlanned(List<CellBlock> list,string id,DateTime target){
   var source=list.Find(x=>x.Id==id);if(source==null)throw new InvalidOperationException("이동할 블록이 없습니다.");
   string meal=!string.IsNullOrEmpty(source.MealId)?source.MealId:source.PrepFor;
   var group=list.Where(x=>x.Id==id||!string.IsNullOrEmpty(meal)&&(x.MealId==meal||x.PrepFor==meal)).ToList();
   TimeSpan shift=target-source.Start;
   if(group.Any(x=>list.Any(other=>!group.Contains(other)&&Overlap(other,x.Start+shift,x.End+shift))))throw new InvalidOperationException("이동할 위치에 다른 계획이 있습니다. 끼니와 준비가 함께 들어갈 빈 칸으로 옮겨 주세요.");
   foreach(var block in group)block.Start+=shift;list.Sort((a,b)=>a.Start.CompareTo(b.Start));
  }
  public static Plan AsPlan(State s,Day d,GridPlan p){
   var slots=new List<Slot>();
   foreach(var b in p.DisplayBlocks){
    var block=new Block{Id=b.Id,Name=b.Caption,Kind=Kind(b.Category),Minutes=b.Count*10,Minimum=b.Minimum*10,Priority=b.Priority};
    bool recordedMeal=d.Records.Any(x=>!string.IsNullOrEmpty(x.MealId)&&x.MealId==b.MealId&&x.Start>=d.Wake);
    bool recordedActivity=(b.Category=="웨이트"||b.Category=="존2")&&Adaptive.ActualActivity(d,b.Category)!=null||b.Category=="기상"&&d.Records.Any(x=>x.Category=="기상"&&x.Start==d.Wake);
    slots.Add(new Slot{Block=block,Start=b.Start,End=b.End,Minutes=b.Count*10,Actual=recordedMeal||recordedActivity,Done=d.Done.Contains(b.Id)||recordedMeal||recordedActivity});
   }
   return new Plan{Slots=slots,Changes=p.Changes,Overflow=p.Overflow,Saved=p.Saved,Target=Adaptive.Target(s,d)};
  }
 }
}
