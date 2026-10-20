using System;
using System.Linq;
using System.Collections.Generic;

namespace Ara {
 public static class Routine {
  public static readonly string[] MealIds={"b1","b6","b10","b15","b16"};
  public static void ApplyDefaults(State s){
   s.Template=new List<Block>();
   Add(s,"gap0","빈 시간","gap",20,20,0);Prep(s,"b1",1);Add(s,"b1","1끼니","meal",30,30,0);
   Add(s,"gap1","빈 시간","gap",90,90,0);Add(s,"b4","존2","exercise",40,20,1);Add(s,"b5","샤워","life",20,20,0);
   Add(s,"gap2","빈 시간","gap",50,50,0);Prep(s,"b6",2);Add(s,"b6","2끼니","meal",30,30,0);
   Add(s,"gap3","빈 시간","gap",140,140,0);Prep(s,"b10",3);Add(s,"b10","3끼니","meal",30,30,0);
   Add(s,"gap4","빈 시간","gap",90,90,0);Add(s,"b8","웨이트","exercise",120,30,2);
   Add(s,"gap5","빈 시간","gap",50,50,0);Prep(s,"b15",4);Add(s,"b15","4끼니","meal",30,30,0);
   Add(s,"gap6","빈 시간","gap",140,140,0);Prep(s,"b16",5);Add(s,"b16","5끼니 · 요거트","meal",10,10,0);
   s.DayMinutes=960;s.ScheduleRevision=3;
  }
  static void Add(State s,string id,string name,string kind,int minutes,int min,int priority){s.Template.Add(new Block{Id=id,Name=name,Kind=kind,Minutes=minutes,Minimum=min,Priority=priority});}
  static void Prep(State s,string id,int number){Add(s,"prep-"+id,number+"끼니 준비","prep",10,10,0);}
  public static void Upgrade(State s,DateTime date){
   if(s.ScheduleRevision>=2)return;
   foreach(var oldDay in s.Days.Where(x=>DateTime.Parse(x.Date).Date>=date.Date&&!x.GridReady))GridEngine.Ensure(s,oldDay);
   ApplyDefaults(s);
   foreach(var d in s.Days.Where(x=>DateTime.Parse(x.Date).Date>=date.Date)){
    d.GridBase=GridEngine.Generate(s,d);d.GridReady=true;d.StartAnchoredMeals=true;
   }
  }
  public static bool ShortenYogurt(State s,DateTime now){
   if(s.ScheduleRevision<2||s.ScheduleRevision>=3)return false;var yogurt=s.Template.Find(x=>x.Id=="b16");if(yogurt==null)return false;
   // Hold the existing bedtime budget; eating occupies one slot, leaving 20 more minutes empty.
   s.DayMinutes+=30-yogurt.Minutes;yogurt.Minutes=10;yogurt.Minimum=10;
   foreach(var d in s.Days)foreach(var b in d.GridBase.Where(x=>x.MealId=="b16"&&x.Count==3&&x.Start>now&&!d.PlanHistoryIds.Contains(x.Id)&&!d.Records.Any(a=>a.MealId=="b16"&&a.Start>=d.Wake))){b.Count=1;b.Minimum=1;}
   s.ScheduleRevision=3;return true;
  }
  public static GridPlan Build(State s,Day d){
   var originals=d.GridBase.Where(x=>!d.PlanHistoryIds.Contains(x.Id)||d.PlanHistory.Any(h=>h.Id==x.Id)||d.Records.Any(r=>r.Start>=d.Wake&&(!string.IsNullOrEmpty(x.MealId)&&r.MealId==x.MealId||!string.IsNullOrEmpty(x.PrepFor)&&r.MealId==x.PrepFor))).Select(x=>x.Copy()).ToList();
   var hidden=new List<string>();
   if(s.AdaptivePriority)foreach(var r in d.Records.Where(x=>x.Start>=d.Wake&&!string.IsNullOrEmpty(x.MealId)&&!originals.Any(b=>b.MealId==x.MealId))){var seed=GridEngine.Generate(s,d).Find(x=>x.MealId==r.MealId);if(seed==null)continue;DateTime reference;if(d.AnchorReferences.TryGetValue(r.MealId,out reference))seed.Start=reference;seed.Id="virtual-"+r.MealId;originals.Add(seed);hidden.Add(seed.Id);var prep=new CellBlock{Id="virtual-prep-"+r.MealId,Start=seed.Start.AddMinutes(-10),Count=1,Category="끼니 준비",PrepFor=r.MealId,Name="준비"};originals.Add(prep);hidden.Add(prep.Id);}
   originals=originals.OrderBy(x=>x.Start).ToList();var p=new GridPlan{Blocks=originals.Select(x=>x.Copy()).ToList()};PlanHistory.Configure(d,p);foreach(string id in hidden)p.FixedIds.Add(id);
   var meals=originals.Where(x=>!string.IsNullOrEmpty(x.MealId)).OrderBy(x=>x.Start).ToList();
   if(d.Records.Any(x=>!string.IsNullOrEmpty(x.MealId)&&x.Start<d.Wake))p.Changes.Add("기상 전 식사 기록은 보존 · 새 계획에는 연결하지 않음");
   var anchors=s.AdaptivePriority?originals.Where(x=>!string.IsNullOrEmpty(x.MealId)||x.Category=="웨이트"||x.Category=="존2").OrderBy(x=>x.Start).ToList():meals;
   foreach(var original in anchors){
    bool activityAnchor=original.Category=="웨이트"||original.Category=="존2";var recorded=activityAnchor?Adaptive.ActualActivity(d,original.Category):d.Records.Find(x=>x.MealId==original.MealId&&x.Start>=d.Wake);if(recorded==null)continue;
    var planned=p.Blocks.Find(x=>x.Id==original.Id);if(planned==null)continue;
    string anchorKey=activityAnchor?original.Category:original.MealId;DateTime reference;
    DateTime expected=d.AnchorReferences.TryGetValue(anchorKey,out reference)?reference.AddMinutes((planned.Start-original.Start).TotalMinutes):planned.Start;
    int delta=(int)((recorded.Start-expected).TotalMinutes);
    // Start-to-start intervals stay fixed. Actual eating duration is kept only in the actual lane.
    int oldCount;DateTime previousStart=planned.Start;int durationDelta=activityAnchor?(recorded.Count-(d.AnchorCounts.TryGetValue(anchorKey,out oldCount)?oldCount:planned.Count))*10:0;
    if(original.Category=="웨이트"&&delta<0){var third=p.Blocks.Find(x=>x.MealId=="b10");if(third!=null&&third.End>recorded.Start&&!d.Records.Any(x=>x.MealId=="b10"&&x.Start>=d.Wake)){p.Blocks.RemoveAll(x=>x.MealId=="b10"||x.PrepFor=="b10");p.Changes.Add("3끼니 제외 · 실제 웨이트 시각 반영");}}
    if(recorded.Start<previousStart){
     var prep=string.IsNullOrEmpty(original.MealId)?null:p.Blocks.Find(x=>x.PrepFor==original.MealId);DateTime boundary=recorded.Start.AddMinutes(prep==null?0:-prep.Count*10);
     foreach(var preceding in p.Blocks.Where(x=>x.Start<previousStart&&x.End>boundary&&(string.IsNullOrEmpty(original.MealId)||x.PrepFor!=original.MealId)).ToList()){
      if(!string.IsNullOrEmpty(preceding.MealId)||!string.IsNullOrEmpty(preceding.PrepFor))throw new InvalidOperationException("이전 끼니·준비와 실제 식사 시각이 겹칩니다.");
      if(d.Records.Any(r=>string.IsNullOrEmpty(r.MealId)&&r.Category!="끼니 준비"&&string.IsNullOrEmpty(r.PrepFor)&&GridEngine.Overlap(r,preceding.Start,preceding.End)))throw new InvalidOperationException("이른 끼니가 이미 기록한 활동과 겹칩니다. 실제 기록을 확인해 주세요.");
      int count=Math.Max(0,(int)((boundary-preceding.Start).TotalMinutes/10));p.Changes.Add(preceding.Caption+" −"+((preceding.Count-count)*10)+"분 (이른 끼니)");preceding.Count=count;
     }
    }
    foreach(var block in p.Blocks)if(block.Start>=previousStart||(!string.IsNullOrEmpty(original.MealId)&&block.PrepFor==original.MealId)){int durationShift=block.Start>previousStart?durationDelta:0;block.Start=block.Start.AddMinutes(delta+durationShift);}
    planned.Start=recorded.Start;
    if(activityAnchor){planned.Count=recorded.Count;if(durationDelta!=0)p.Changes.Add(planned.Caption+" 실제 길이 "+(recorded.Count*10)+"분 반영");}
    if(delta!=0)p.Changes.Add(planned.Caption+" "+(delta>0?"+":"")+delta+"분 · 이후 시작 간격 유지");
   }
   // Prep follows its own meal even after independent anchors were recorded.
   foreach(var prep in p.Blocks.Where(x=>!string.IsNullOrEmpty(x.PrepFor))){var meal=p.Blocks.Find(x=>x.MealId==prep.PrepFor);if(meal!=null)prep.Start=meal.Start.AddMinutes(-prep.Count*10);}
   // Once the shower was moved behind meal two, actual eating end remains its lower bound.
   if(d.ShowerAfterSecond){var second=p.Blocks.Find(x=>x.MealId=="b6");var shower=p.Blocks.Find(x=>x.TemplateId=="b5"||x.Name=="샤워");var actual=d.Records.Find(x=>x.MealId=="b6"&&x.Start>=d.Wake);if(second!=null&&shower!=null&&actual!=null&&!p.FixedIds.Contains(shower.Id)&&!d.Records.Any(x=>x.TemplateId=="b5"||x.Name=="샤워")){DateTime end=Adaptive.MealEnd(d,p,"b6").Value;shower.Start=end;foreach(var next in p.Blocks.Where(x=>x!=shower&&x.Start>=second.End&&x.Start<shower.End).OrderBy(x=>x.Start).ToList()){if(p.FixedIds.Contains(next.Id)||d.Records.Any(x=>GridEngine.Overlap(x,next.Start,next.End)))throw new InvalidOperationException("식사 뒤 샤워가 기록한 활동과 겹칩니다. 실제 기록을 확인해 주세요.");int delay=(int)(shower.End-next.Start).TotalMinutes;DateTime boundary=next.Start;foreach(var later in p.Blocks.Where(x=>x!=shower&&x.Start>=boundary))later.Start=later.Start.AddMinutes(delay);}p.Changes.Add("2끼니 실제 종료 후 샤워 20분 유지");}}
   var orderedMeals=meals.Select(x=>p.Blocks.Find(b=>b.Id==x.Id)).Where(x=>x!=null).ToList();
   for(int i=1;i<orderedMeals.Count;i++)if(orderedMeals[i].Start<orderedMeals[i-1].End)throw new InvalidOperationException("끼니 순서 또는 시간이 겹칩니다. 실제 끼니 시각을 확인해 주세요.");
   // Prep is a separate actual fact, not evidence that the preceding activity was performed.
   // Resolve an early meal by trimming only unrecorded overlapping preceding plans, never meal anchors.
   p.Blocks=p.Blocks.Where(x=>x.Count>0).OrderBy(x=>x.Start).ToList();
   for(int i=0;i<p.Blocks.Count-1;i++){
    var a=p.Blocks[i];var b=p.Blocks[i+1];if(a.End<=b.Start)continue;
    if(!string.IsNullOrEmpty(a.MealId)||!string.IsNullOrEmpty(a.PrepFor))throw new InvalidOperationException("끼니·준비 계획이 겹칩니다: "+a.Caption+" "+a.Start.ToString("HH:mm")+"–"+a.End.ToString("HH:mm")+" / "+b.Caption+" "+b.Start.ToString("HH:mm")+"–"+b.End.ToString("HH:mm"));
    int newCount=Math.Max(0,(int)((b.Start-a.Start).TotalMinutes/10));
    if(d.Records.Any(r=>string.IsNullOrEmpty(r.MealId)&&r.Category!="끼니 준비"&&string.IsNullOrEmpty(r.PrepFor)&&GridEngine.Overlap(r,a.Start,a.End)))throw new InvalidOperationException("이른 끼니가 이미 기록한 활동과 겹칩니다. 실제 기록을 확인해 주세요.");
    p.Changes.Add(a.Caption+" −"+((a.Count-newCount)*10)+"분");a.Count=newCount;
   }
   p.Blocks=p.Blocks.Where(x=>x.Count>0).ToList();
   if(s.AdaptivePriority){Adaptive.Rebalance(s,d,p);if(p.Blocks.Any(x=>x.Start<d.Wake.Date||x.End>d.Wake.Date.AddDays(2)))throw new InvalidOperationException("계획이 당일·다음날 표시 범위를 벗어납니다.");PlanHistory.Project(d,p);return p;}
   // A large delay spends future buffers and advances later unrecorded plans.
   DateTime target=GridEngine.Snap(d.Wake.AddMinutes(s.DayMinutes));
   int overflow=p.Blocks.Count==0?0:Math.Max(0,(int)((p.Blocks.Max(x=>x.End)-target).TotalMinutes));
   DateTime lastAnchor=d.Records.Where(r=>!string.IsNullOrEmpty(r.MealId)&&r.Start>=d.Wake&&meals.Any(m=>m.MealId==r.MealId)).Select(r=>r.Start).DefaultIfEmpty(DateTime.MaxValue).Max();
   if(overflow>=s.ShrinkThreshold){
    foreach(var b in p.Blocks.Where(x=>x.Start>lastAnchor&&x.Priority>0).OrderBy(x=>x.Priority).ToList()){
     if(d.Records.Any(r=>GridEngine.Overlap(r,b.Start,b.End)))continue;
     int cut=Math.Min((int)Math.Ceiling(overflow/10.0),Math.Max(0,b.Count-b.Minimum));
     if(cut>0){b.Count-=cut;p.Saved+=cut*10;overflow-=cut*10;foreach(var later in p.Blocks.Where(x=>x.Start>b.Start))later.Start=later.Start.AddMinutes(-cut*10);p.Changes.Add(b.Caption+" −"+(cut*10)+"분 (지연 버퍼)");}
     if(overflow<=0)break;
    }
   }
   p.Overflow=p.Blocks.Count==0?0:Math.Max(0,(int)((p.Blocks.Max(x=>x.End)-target).TotalMinutes));
   if(p.Blocks.Any(x=>x.Start<d.Wake.Date||x.End>d.Wake.Date.AddDays(2)))throw new InvalidOperationException("계획이 당일·다음날 표시 범위를 벗어납니다.");
   return p;
  }
 }
}
