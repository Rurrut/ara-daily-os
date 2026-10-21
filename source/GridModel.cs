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
  public static List<CellBlock> Generate(State s,Day d){
   var plain=new Day{Date=d.Date,Wake=Snap(d.Wake)};
   var plan=Engine.Build(s,plain);
   var list=new List<CellBlock>();
   foreach(var x in plan.Slots){
    if(x.Block.Kind=="gap")continue;
    DateTime start=Snap(x.Start),end=Snap(x.End);
    if(end<=start)continue;
    list.Add(new CellBlock{Id="plan-"+x.Block.Id,Start=start,Count=(int)((end-start).TotalMinutes/10),Name=x.Block.Name,Category=Category(x.Block),TemplateId=x.Block.Id,MealId=x.Block.Kind=="meal"?x.Block.Id:"",PrepFor=x.Block.Kind=="prep"?x.Block.Id.Substring(5):"",Priority=x.Block.Priority,Minimum=(int)Math.Ceiling(x.Block.Minimum/10.0)});
   }
   if(s.ScheduleRevision>=2)list.Insert(0,new CellBlock{Id="plan-wake",Start=Snap(d.Wake),Count=1,Name="기상",Category="기상",TemplateId="wake",Minimum=1});
   return list;
  }
  public static void Ensure(State s,Day d){
   if(d.GridReady)return;
   d.GridBase=Generate(s,d);d.Records=new List<CellBlock>();
   // Previous-version actual starts are retained and converted to editable 10-minute records.
   foreach(var pair in d.Actual.OrderBy(x=>x.Value)){
    var b=s.Template.Find(x=>x.Id==pair.Key);if(b==null)continue;
    var item=new CellBlock{Id=Guid.NewGuid().ToString("N"),Start=Snap(pair.Value),Count=Math.Max(1,(int)Math.Round(b.Minutes/10.0)),Name=b.Name,Category=Category(b),TemplateId=b.Id,MealId=b.Kind=="meal"?b.Id:""};
    if(!d.Records.Any(x=>Overlap(x,item.Start,item.End)))d.Records.Add(item);
   }
   d.GridReady=true;
   d.StartAnchoredMeals=s.ScheduleRevision>=2;
  }
  public static bool Overlap(CellBlock b,DateTime start,DateTime end){return b.Start<end&&b.End>start;}
  public static void Validate(Day d){
   if(d.GridBase==null||d.Records==null)throw new InvalidDataException("칸 기록이 올바르지 않습니다.");
   if(d.PlanHistory==null||d.PlanHistoryIds==null||d.AnchorReferences==null||d.AnchorCounts==null||d.PlanHistoryIds.Count>288||d.PlanHistoryIds.Any(string.IsNullOrWhiteSpace)||d.PlanHistoryIds.Distinct().Count()!=d.PlanHistoryIds.Count||d.PlanHistory.Any(x=>x==null||!d.PlanHistoryIds.Contains(x.Id))||d.PlanHistory.Select(x=>x.Id).Distinct().Count()!=d.PlanHistory.Count)throw new InvalidDataException("고정된 계획 기록이 올바르지 않습니다.");
   if(d.AnchorReferences.Values.Any(x=>x<d.Wake.Date||x>=d.Wake.Date.AddDays(2))||d.AnchorCounts.Values.Any(x=>x<1||x>288))throw new InvalidDataException("계획 기준 시각이 올바르지 않습니다.");
   foreach(var list in new[]{d.GridBase,d.Records,d.PlanHistory}){
    var ids=new HashSet<string>();
    foreach(var b in list){
     if(b==null||string.IsNullOrWhiteSpace(b.Id)||!ids.Add(b.Id)||b.Count<1||b.Count>288||b.Start.Second!=0||b.Start.Minute%10!=0||b.Start.Millisecond!=0||b.Start<d.Wake.Date||b.End>d.Wake.Date.AddDays(2)||b.Score < -100000||b.Score>100000||!CategoryCatalog.ValidKey(b.Category)||b.Satisfaction<0||b.Satisfaction>5||b.Name==null||b.Name.Length>250||b.Minimum<0||b.Priority<0)throw new InvalidDataException("시간 칸, 카테고리 또는 점수 범위를 확인해 주세요.");
    }
    var order=list.OrderBy(x=>x.Start).ToList();
    for(int i=1;i<order.Count;i++)if(order[i].Start<order[i-1].End)throw new InvalidDataException("같은 줄의 시간 블록이 겹칩니다.");
   }
   var plannedMeals=d.GridBase.Where(x=>!string.IsNullOrEmpty(x.MealId)).Select(x=>x.MealId).ToList();
   if(plannedMeals.Count!=plannedMeals.Distinct().Count())throw new InvalidDataException("위 계획에 같은 끼니가 중복되었습니다. 기존 식사 계획 블록을 수정하거나 이동해 주세요.");
   var meals=d.Records.Where(x=>!string.IsNullOrEmpty(x.MealId)&&x.Start>=d.Wake).Select(x=>x.MealId).ToList();
   if(meals.Count!=meals.Distinct().Count())throw new InvalidDataException("같은 끼니를 여러 블록으로 중복 기록할 수 없습니다. 기존 식사 블록을 수정해 주세요.");
  }
  public static string DetectMeal(Day d,GridPlan p,CellBlock record){
   if(record.Category!="식사")return "";
   var meals=d.GridBase.Where(x=>x.Category=="식사"&&!string.IsNullOrEmpty(x.MealId)).ToList();
   if(!string.IsNullOrEmpty(record.TemplateId)){
    var exact=meals.Find(x=>x.TemplateId==record.TemplateId);if(exact!=null)return exact.MealId;
   }
   string name=(record.Name??"").Replace(" ","");
   string expected=name.Contains("5끼니")?"b16":name.Contains("4끼니")?"b15":name.Contains("요거트")||name.Contains("요구르트")?"b12":name.Contains("1끼니")||name.Contains("첫")||name.Contains("아침")?"b1":name.Contains("2끼니")||name.Contains("두번째")||name.Contains("점심")?"b6":name.Contains("3끼니")||name.Contains("세번째")||name.Contains("저녁")?"b10":null;
   if(expected!=null&&meals.Any(x=>x.MealId==expected))return expected;
   var used=new HashSet<string>(d.Records.Where(x=>x.Id!=record.Id&&x.Start>=d.Wake).Select(x=>x.MealId));
   var available=p.Blocks.Where(x=>x.Category=="식사"&&!string.IsNullOrEmpty(x.MealId)&&!used.Contains(x.MealId)).OrderBy(x=>Math.Abs((x.Start-record.Start).TotalMinutes)).FirstOrDefault();
   return available==null?"":available.MealId;
  }
  public static GridPlan Build(State s,Day d){
   CategoryCatalog.Ensure(s);Ensure(s,d);if(d.Records.Count==0)d.ShowerAfterSecond=false;PlanHistory.Ensure(s,d);Validate(d);
   if(d.ManualPlan){var manual=new GridPlan{Blocks=d.GridBase.Select(x=>x.Copy()).OrderBy(x=>x.Start).ToList()};Adaptive.DescribeManual(s,d,manual);return manual;}
   if(d.StartAnchoredMeals)return Routine.Build(s,d);
   var result=new GridPlan{Blocks=d.GridBase.OrderBy(x=>x.Start).Select(x=>x.Copy()).ToList()};
   var original=d.GridBase.OrderBy(x=>x.Start).ToList();
   int lastAnchor=-1;
   foreach(var baseMeal in original.Where(x=>!string.IsNullOrEmpty(x.MealId))){
    var record=d.Records.Find(x=>x.MealId==baseMeal.MealId);if(record==null)continue;
    int index=result.Blocks.FindIndex(x=>x.Id==baseMeal.Id);if(index<0)continue;
    var meal=result.Blocks[index];
    if(record.Start<d.Wake)throw new InvalidOperationException("식사 기록은 기상 시각 이후에 입력해 주세요.");
    int delta=(int)((record.Start-meal.Start).TotalMinutes);
    DateTime boundary=record.Start;
    if(delta<0){
     for(int j=index-1;j>=0;j--){
      var prev=result.Blocks[j];if(prev.End<=boundary)break;
      bool anchored=d.Records.Any(x=>!string.IsNullOrEmpty(x.MealId)&&x.MealId==prev.MealId);
      if(anchored)throw new InvalidOperationException("이전 끼니의 실제 시간과 겹칩니다. 식사 기록의 순서를 확인해 주세요.");
      int cut=(int)((prev.End-boundary).TotalMinutes/10);cut=Math.Min(cut,prev.Count);prev.Count-=cut;
      if(prev.Start>boundary)prev.Start=boundary;
      boundary=prev.Start;
      if(cut>0)result.Changes.Add(prev.Caption+" −"+(cut*10)+"분");
     }
    }
    meal.Start=record.Start;int lengthChange=(record.Count-meal.Count)*10;meal.Count=record.Count;
    for(int j=index+1;j<result.Blocks.Count;j++)result.Blocks[j].Start=result.Blocks[j].Start.AddMinutes(delta+lengthChange);
    if(delta!=0)result.Changes.Add(meal.Caption+" "+(delta>0?"+":"")+delta+"분 이동");
    lastAnchor=index;
   }
   if(result.Blocks.Count==0)return result;
   DateTime target=Snap(d.Wake.AddMinutes(s.DayMinutes));
   int debt=Math.Max(0,(int)((result.Blocks.Last().End-target).TotalMinutes));
   if(debt>=s.ShrinkThreshold&&lastAnchor>=0){
    var candidates=result.Blocks.Select((b,i)=>new{b,i}).Where(x=>x.i>lastAnchor&&x.b.Priority>0).OrderBy(x=>x.b.Priority).ThenBy(x=>x.i).ToList();
    foreach(var x in candidates){
     // Actually recorded time remains a fact; only unrecorded future plan buffers are spent.
     if(d.Records.Any(r=>Overlap(r,x.b.Start,x.b.End)))continue;
     int cut=Math.Min((int)Math.Ceiling(debt/10.0),Math.Max(0,x.b.Count-x.b.Minimum));
     if(cut==0)continue;x.b.Count-=cut;debt-=cut*10;result.Saved+=cut*10;
     for(int j=x.i+1;j<result.Blocks.Count;j++)result.Blocks[j].Start=result.Blocks[j].Start.AddMinutes(-cut*10);
     result.Changes.Add(x.b.Caption+" −"+(cut*10)+"분 (버퍼)");if(debt<=0)break;
    }
   }
   result.Blocks=result.Blocks.Where(x=>x.Count>0).ToList();
   result.Overflow=result.Blocks.Count==0?0:Math.Max(0,(int)((result.Blocks.Last().End-target).TotalMinutes));
   for(int i=1;i<result.Blocks.Count;i++)if(result.Blocks[i].Start<result.Blocks[i-1].End)throw new InvalidOperationException("식사 기록 때문에 계획이 겹칩니다. 기록을 확인해 주세요.");
   if(result.Blocks.Any(x=>x.Start<d.Wake.Date||x.End>d.Wake.Date.AddDays(2)))throw new InvalidOperationException("계획이 표시 범위(당일과 다음날)를 벗어납니다.");
   return result;
  }
  public static decimal SatisfactionAverage(State s,Day d,GridPlan p){return SatisfactionPolicy.Calculate(s,d,Adaptive.EstimatedSleep(s,d,p),DateTime.Now).Average;}
  public static decimal SatisfactionAverage(Day d){int count=d.Records.Sum(x=>x.Count);return count==0?0:d.Records.Sum(x=>x.Satisfaction*x.Count)/count;}
  public static decimal Total(Day d){return d.Records.Sum(x=>x.Score*x.Count);}
  public static void SetScore(List<CellBlock> list,DateTime start,int count,decimal score,bool satisfaction=false){
   DateTime end=start.AddMinutes(count*10);var changed=new List<CellBlock>();
   foreach(var original in list){
    if(!Overlap(original,start,end)){changed.Add(original.Copy());continue;}
    var boundaries=new List<DateTime>{original.Start};if(start>original.Start&&start<original.End)boundaries.Add(start);if(end>original.Start&&end<original.End)boundaries.Add(end);boundaries.Add(original.End);
    for(int i=0;i<boundaries.Count-1;i++){var part=original.Copy();part.Start=boundaries[i];part.Count=(int)((boundaries[i+1]-boundaries[i]).TotalMinutes/10);if(i>0){part.Id=Guid.NewGuid().ToString("N");part.MealId="";}if(part.Start>=start&&part.Start<end){if(satisfaction)part.Satisfaction=score;else part.Score=score;}changed.Add(part);}
   }
   DateTime cursor=start;
   while(cursor<end){if(changed.Any(x=>Overlap(x,cursor,cursor.AddMinutes(10)))){cursor=cursor.AddMinutes(10);continue;}DateTime blankStart=cursor;do{cursor=cursor.AddMinutes(10);}while(cursor<end&&!changed.Any(x=>Overlap(x,cursor,cursor.AddMinutes(10))));changed.Add(new CellBlock{Id=Guid.NewGuid().ToString("N"),Start=blankStart,Count=(int)((cursor-blankStart).TotalMinutes/10),Name="",Category="기타",Score=satisfaction?0:score,Satisfaction=satisfaction?score:2});}
   list.Clear();list.AddRange(changed.OrderBy(x=>x.Start));
  }
  public static void SetRange(List<CellBlock> list,CellBlock entry){
   var updated=new List<CellBlock>();
   foreach(var b in list){
    if(!Overlap(b,entry.Start,entry.End)){updated.Add(b);continue;}
    // An edit cuts only the selected cells. Both remaining fragments retain their own scores.
    if(b.Start<entry.Start){var left=b.Copy();left.Count=(int)((entry.Start-b.Start).TotalMinutes/10);if(!string.IsNullOrEmpty(entry.MealId)&&entry.MealId==left.MealId)left.MealId="";updated.Add(left);}
    if(b.End>entry.End){var right=b.Copy();right.Id=Guid.NewGuid().ToString("N");right.Start=entry.End;right.Count=(int)((b.End-entry.End).TotalMinutes/10);if(b.Start<entry.Start||(!string.IsNullOrEmpty(entry.MealId)&&entry.MealId==b.MealId)){right.MealId="";right.TemplateId="";}updated.Add(right);}
   }
   updated.Add(entry);list.Clear();list.AddRange(updated.OrderBy(x=>x.Start));
  }
  public static void ClearRange(List<CellBlock> list,DateTime start,int count){
   var blank=new CellBlock{Id="__clear__",Start=start,Count=count};SetRange(list,blank);list.RemoveAll(x=>x.Id=="__clear__");
  }
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
