using System;
using System.Linq;
using System.Collections.Generic;

namespace Ara {
 public static class Adaptive {
  public static void Upgrade(State s){
   if(s.AdaptiveRevision>=1)return;s.AdaptivePriority=true;s.AdaptiveRevision=1;
   foreach(var b in s.Template.Where(x=>x.Id=="b16"&&x.Name=="5끼니"))b.Name="5끼니 · 요거트";
   foreach(var day in s.Days)foreach(var b in day.GridBase.Where(x=>x.MealId=="b16"&&x.Name=="5끼니"))b.Name="5끼니 · 요거트";
  }
  public static CellBlock ActualWeight(Day d){return ActualActivity(d,"웨이트");}
  public static CellBlock ActualActivity(Day d,string category){
   var records=d.Records.Where(x=>x.Start>=d.Wake&&(x.Category==category||x.TemplateId==(category=="웨이트"?"b8":"b4"))).OrderBy(x=>x.Start).ToList();if(records.Count==0)return null;
   DateTime end=records[0].End;foreach(var r in records.Skip(1)){if(r.Start!=end)break;end=r.End;}
   var result=records[0].Copy();result.Count=(int)((end-result.Start).TotalMinutes/10);return result;
  }
  public static DateTime Target(State s,Day d){var yogurt=s.Template.Find(x=>x.Id=="b16");int offset=s.AdaptivePriority?60-(s.ScheduleRevision>=3?30:yogurt==null?30:yogurt.Minutes):0;return GridEngine.Snap(d.Wake.AddMinutes(s.DayMinutes+offset));}
  public static DateTime? MealEnd(Day d,GridPlan p,string mealId){var actual=d.Records.Where(x=>x.MealId==mealId&&x.Start>=d.Wake).OrderBy(x=>x.Start).FirstOrDefault();if(actual==null){var planned=p.Blocks.Find(x=>x.MealId==mealId);return planned==null?(DateTime?)null:planned.End;}DateTime end=actual.End;foreach(var next in d.Records.Where(x=>x.Start>=actual.End).OrderBy(x=>x.Start)){if(next.Start!=end||next.Category!="식사"||!string.IsNullOrEmpty(next.MealId)&&next.MealId!=mealId)break;end=next.End;}return end;}
  public static DateTime? FourthMealEnd(Day d,GridPlan p){return MealEnd(d,p,"b15");}
  // The bedtime marker is a deadline, independent of meal delays.
  public static DateTime EstimatedSleep(State s,Day d,GridPlan p){return Target(s,d);}
  public static DateTime RequiredFinish(State s,Day d,GridPlan p){var actual=d.Records.Where(x=>x.MealId=="b16"&&x.Start>=d.Wake).OrderBy(x=>x.Start).FirstOrDefault();var yogurt=actual??p.Blocks.Find(x=>x.MealId=="b16");var fourthEnd=FourthMealEnd(d,p);DateTime sleep=yogurt==null?Target(s,d):actual!=null||p.YogurtRestRelaxed?new[]{Target(s,d),yogurt.End}.Max():yogurt.End.AddMinutes(p.YogurtRestMinutes??Math.Max(0,60-yogurt.Count*10));if(fourthEnd.HasValue&&p.PostFourthRestMinutes.HasValue){var held=fourthEnd.Value.AddMinutes(p.PostFourthRestMinutes.Value);if(held>sleep)sleep=held;}return fourthEnd.HasValue&&sleep<fourthEnd.Value.AddHours(2)?fourthEnd.Value.AddHours(2):sleep;}

  static DateTime End(Day d,GridPlan p){return p.Blocks.Select(x=>x.End).Concat(d.Records.Where(x=>x.Start>=d.Wake).Select(x=>x.End)).DefaultIfEmpty(d.Wake).Max();}
  static int Debt(State s,Day d,GridPlan p){return Math.Max(0,(int)Math.Ceiling((new[]{End(d,p),RequiredFinish(s,d,p)}.Max()-Target(s,d)).TotalMinutes));}
  public static void DescribeManual(State s,Day d,GridPlan p){p.Overflow=Debt(s,d,p);p.Changes.Add("직접 수정한 계획 유지 · 다음 식사·운동 기록에서 이후 일정 자동 조정");if(p.Overflow>0)p.Changes.Add("직접 수정한 계획 · 고정 취침까지 맞추지 못한 시간 "+p.Overflow+"분");}
  static bool Recorded(Day d,CellBlock block){return d.Records.Any(r=>r.Start>=d.Wake&&((!string.IsNullOrEmpty(block.MealId)&&r.MealId==block.MealId)||(block.Category=="웨이트"&&(r.Category=="웨이트"||r.TemplateId=="b8"))||(block.Category=="존2"&&(r.Category=="존2"||r.TemplateId=="b4"))||GridEngine.Overlap(r,block.Start,block.End)));}
  static void MinimumMealGaps(Day d,GridPlan p,bool report=false){
   var meals=p.Blocks.Where(x=>!string.IsNullOrEmpty(x.MealId)).OrderBy(x=>x.Start).ToList();
   DateTime cutoff=d.Records.Where(x=>x.Start>=d.Wake).Select(x=>x.End).DefaultIfEmpty(d.Wake).Max();
   for(int i=1;i<meals.Count;i++){
    var previous=meals[i-1];var next=meals[i];DateTime minimum=previous.Start.AddHours(3);if(next.Start>=minimum)continue;
    var prep=p.Blocks.Find(x=>x.PrepFor==next.MealId);
    if(next.Start<cutoff||d.Records.Any(x=>x.Start>=d.Wake&&x.MealId==next.MealId)){if(report){string warning=previous.Caption+"→"+next.Caption+" 시작 간격 "+(int)(next.Start-previous.Start).TotalMinutes+"분 · 최소 180분 미달 (실제 기록 보호)";if(!p.Changes.Contains(warning))p.Changes.Add(warning);}continue;}
    int delay=(int)(minimum-next.Start).TotalMinutes;DateTime edge=next.Start;
    foreach(var later in p.Blocks.Where(x=>x.Start>=edge||x==prep))later.Start=later.Start.AddMinutes(delay);
    p.Changes.Add(previous.Caption+"→"+next.Caption+" 최소 3시간 확보 · 이후 +"+delay+"분");
   }
  }
  static int MealAdvanceAllowance(GridPlan p,DateTime boundary){
   var before=p.Blocks.Where(x=>!string.IsNullOrEmpty(x.MealId)&&x.Start<boundary).OrderByDescending(x=>x.Start).FirstOrDefault();
   var after=p.Blocks.Where(x=>!string.IsNullOrEmpty(x.MealId)&&x.Start>=boundary).OrderBy(x=>x.Start).FirstOrDefault();
   return before==null||after==null?int.MaxValue:Math.Max(0,(int)(after.Start-before.Start).TotalMinutes-180);
  }
  static GridPlan Trial(GridPlan p){return new GridPlan{Blocks=p.Blocks.Select(x=>x.Copy()).ToList(),Changes=p.Changes.ToList(),FixedIds=new HashSet<string>(p.FixedIds),FutureFloor=p.FutureFloor,Saved=p.Saved,Overflow=p.Overflow,FrontWaitSpent=p.FrontWaitSpent,YogurtRestRelaxed=p.YogurtRestRelaxed,YogurtRestMinutes=p.YogurtRestMinutes,PostFourthRestMinutes=p.PostFourthRestMinutes};}
  static int CommitBuffer(State s,Day d,GridPlan p,GridPlan trial,string reason){int before=Debt(s,d,p);MinimumMealGaps(d,trial);MinimumWeightGap(d,trial);PlanHistory.KeepFutureClear(trial);trial.Blocks=trial.Blocks.OrderBy(x=>x.Start).ToList();for(int i=1;i<trial.Blocks.Count;i++)if(trial.Blocks[i].Start<trial.Blocks[i-1].End)return 0;
   foreach(var old in p.Blocks){var updated=trial.Blocks.Find(x=>x.Id==old.Id);bool changed=updated==null||updated.Start!=old.Start||updated.Count!=old.Count;if(changed&&(p.FixedIds.Contains(old.Id)||Recorded(d,old)))return 0;}
   int recovered=Math.Max(0,before-Debt(s,d,trial));if(recovered==0)return 0;p.Blocks=trial.Blocks;p.Changes=trial.Changes;p.FrontWaitSpent=trial.FrontWaitSpent;p.PostFourthRestMinutes=trial.PostFourthRestMinutes;p.YogurtRestRelaxed=trial.YogurtRestRelaxed;p.YogurtRestMinutes=trial.YogurtRestMinutes;p.Saved+=recovered;p.Changes.Add(reason+" · "+recovered+"분 회복");return recovered;
  }
  static int Remove(State s,Day d,GridPlan p,CellBlock block,DateTime cutoff){
   if(block==null||p.FixedIds.Contains(block.Id)||block.Start<cutoff||Recorded(d,block))return 0;var group=p.Blocks.Where(x=>x.Id==block.Id||(!string.IsNullOrEmpty(block.MealId)&&x.PrepFor==block.MealId)).ToList();if(group.Any(x=>x.Start<cutoff||Recorded(d,x)))return 0;var trial=Trial(p);var ids=group.Select(x=>x.Id).ToList();trial.Blocks.RemoveAll(x=>ids.Contains(x.Id));DateTime boundary=group.Max(x=>x.End);
   if(block.MealId=="b10"){var second=p.Blocks.Find(x=>x.MealId=="b6");var weights=p.Blocks.Find(x=>x.Category=="웨이트");if(second!=null&&weights!=null&&!Recorded(d,weights)){DateTime desired=second.Start.AddMinutes(150);if(desired<cutoff)desired=cutoff;int shift=Math.Max(0,(int)((weights.Start-desired).TotalMinutes/10)*10);foreach(var later in trial.Blocks.Where(x=>x.Start>=boundary))later.Start=later.Start.AddMinutes(-shift);return CommitBuffer(s,d,p,trial,"3끼니 제외 → 웨이트 2끼니 시작 +2시간 30분"+(desired>second.Start.AddMinutes(150)?" (실제 기록 이후로 제한)":""));}}
   if(block.MealId=="b16"){var fourth=FourthMealEnd(d,p);if(fourth.HasValue){int credit=Math.Min(40,group.Sum(x=>x.Count*10));int gap=Math.Max(120,(int)((RequiredFinish(s,d,p)-fourth.Value).TotalMinutes));trial.PostFourthRestMinutes=Math.Max(120,gap-credit);foreach(var later in trial.Blocks.Where(x=>x.Start>=boundary))later.Start=later.Start.AddMinutes(-credit);return CommitBuffer(s,d,p,trial,"요거트·준비 제외 (식사·준비 최대 "+credit+"분, 취침 전 여유는 다음 단계)");}}
   int minutes=group.Sum(x=>x.Count*10);foreach(var later in trial.Blocks.Where(x=>x.Start>=boundary))later.Start=later.Start.AddMinutes(-minutes);return CommitBuffer(s,d,p,trial,block.Caption+" 제외 · 취침 목표 유지");
  }
  static void MinimumWeightGap(Day d,GridPlan p){
   var meal2=p.Blocks.Find(x=>x.MealId=="b6");var upcomingWeights=p.Blocks.Find(x=>x.Category=="웨이트");
   if(meal2!=null&&upcomingWeights!=null&&ActualWeight(d)==null&&!d.Records.Any(x=>x.Start>=d.Wake&&(x.MealId=="b15"||x.MealId=="b16"))&&upcomingWeights.Start<meal2.Start.AddMinutes(150)){
    int delay=(int)((meal2.Start.AddMinutes(150)-upcomingWeights.Start).TotalMinutes);DateTime edge=upcomingWeights.Start;foreach(var later in p.Blocks.Where(x=>x.Start>=edge))later.Start=later.Start.AddMinutes(delay);p.Changes.Add("2끼니→웨이트 최소 2시간 30분 확보");
   }
  }
  static void ZoneFloor(Day d,GridPlan p){
   var first=p.Blocks.Find(x=>x.MealId=="b1");var zone=p.Blocks.Find(x=>x.Category=="존2");if(first==null||zone==null||p.FixedIds.Contains(zone.Id)||ActualActivity(d,"존2")!=null)return;
   DateTime minimum=first.Start.AddMinutes(90);var end=MealEnd(d,p,"b1");if(end.HasValue&&end.Value.AddHours(1)>minimum)minimum=end.Value.AddHours(1);
   if(zone.Start>=minimum)return;int delay=(int)(minimum-zone.Start).TotalMinutes;DateTime edge=zone.Start;foreach(var later in p.Blocks.Where(x=>x.Start>=edge))later.Start=later.Start.AddMinutes(delay);p.Changes.Add("1끼니 종료 후 존2까지 최소 1시간 확보 · 이후 +"+delay+"분");
  }
  static void PostShowerBuffer(State s,Day d,GridPlan p,DateTime cutoff){
   var first=p.Blocks.Find(x=>x.MealId=="b1");var second=p.Blocks.Find(x=>x.MealId=="b6");if(first==null||second==null||p.FixedIds.Contains(second.Id)||second.Start<cutoff||Recorded(d,second))return;
   var prep=p.Blocks.Find(x=>x.PrepFor=="b6");var shower=p.Blocks.Where(x=>x.TemplateId=="b5"||x.Name=="샤워").OrderByDescending(x=>x.End).FirstOrDefault();var zone=p.Blocks.Find(x=>x.Category=="존2");DateTime? completed=shower==null?(zone==null?(DateTime?)null:zone.End):shower.End;if(!completed.HasValue)return;
   DateTime earliest=first.Start.AddHours(3);DateTime after=completed.Value.AddMinutes(prep==null?0:prep.Count*10);if(after>earliest)earliest=after;if(cutoff.AddMinutes(prep==null?0:prep.Count*10)>earliest)earliest=cutoff.AddMinutes(prep==null?0:prep.Count*10);
   DateTime edge=prep==null?second.Start:prep.Start;var affected=p.Blocks.Where(x=>x.Start>=second.Start||x==prep).ToList();if(affected.Any(x=>p.FixedIds.Contains(x.Id)||Recorded(d,x)))return;
   int available=Math.Max(0,(int)((second.Start-earliest).TotalMinutes/10)*10);int cut=Math.Min((int)Math.Ceiling(Debt(s,d,p)/10.0)*10,Math.Min(available,MealAdvanceAllowance(p,edge)));if(cut<=0)return;
   var trial=Trial(p);var ids=affected.Select(x=>x.Id).ToList();foreach(var later in trial.Blocks.Where(x=>ids.Contains(x.Id)))later.Start=later.Start.AddMinutes(-cut);CommitBuffer(s,d,p,trial,"존2·샤워→2끼니 대기 −"+cut+"분 (끼니 시작 간격 최소 3시간)");
  }
  static void MealBeforeShower(State s,Day d,GridPlan p,DateTime cutoff){var first=p.Blocks.Find(x=>x.MealId=="b1");var second=p.Blocks.Find(x=>x.MealId=="b6");var zone=p.Blocks.Find(x=>x.Category=="존2");var shower=p.Blocks.Find(x=>x.TemplateId=="b5"||x.Name=="샤워");if(first==null||second==null||zone==null||shower==null||shower.Start>=second.Start||shower.Start<cutoff||second.Start<cutoff||Recorded(d,shower)||Recorded(d,second))return;var prep=p.Blocks.Find(x=>x.PrepFor=="b6");DateTime earliest=new[]{first.Start.AddHours(3),zone.End.AddMinutes(prep==null?0:prep.Count*10),cutoff.AddMinutes(prep==null?0:prep.Count*10)}.Max();int available=Math.Max(0,(int)((second.Start-earliest).TotalMinutes/10)*10);int cut=Math.Min((int)Math.Ceiling(Debt(s,d,p)/10.0)*10,Math.Min(available,MealAdvanceAllowance(p,prep==null?second.Start:prep.Start)));if(cut<=0)return;var trial=Trial(p);foreach(var later in trial.Blocks.Where(x=>x.Id!=shower.Id&&(x.Start>=second.Start||prep!=null&&x.Id==prep.Id)))later.Start=later.Start.AddMinutes(-cut);var movedSecond=trial.Blocks.Find(x=>x.Id==second.Id);trial.Blocks.Find(x=>x.Id==shower.Id).Start=movedSecond.End;if(CommitBuffer(s,d,p,trial,"존2 → 2끼니 → 샤워 순서로 변경 (준비 10분·끼니 시작 최소 3시간 유지)")>0)d.ShowerAfterSecond=true;}
  static void FrontWaitBuffer(State s,Day d,GridPlan p,DateTime cutoff,string phase){
    var zone=p.Blocks.Find(x=>x.Category=="존2");var first=p.Blocks.Find(x=>x.MealId=="b1");
    if(zone!=null&&first!=null&&!p.FixedIds.Contains(zone.Id)&&zone.Start>=cutoff&&!Recorded(d,zone)){
     DateTime earliest=first.Start.AddMinutes(90);var firstEnd=MealEnd(d,p,"b1");if(firstEnd.HasValue&&firstEnd.Value.AddHours(1)>earliest)earliest=firstEnd.Value.AddHours(1);
     int available=Math.Max(0,Math.Min(Math.Max(0,30-p.FrontWaitSpent),(int)((zone.Start-earliest).TotalMinutes/10)*10));int cut=Math.Min((int)Math.Ceiling(Debt(s,d,p)/10.0)*10,available);
     cut=Math.Min(cut,MealAdvanceAllowance(p,zone.Start));
     if(cut>0){DateTime boundary=zone.Start;var trial=Trial(p);foreach(var later in trial.Blocks.Where(x=>x.Start>=boundary))later.Start=later.Start.AddMinutes(-cut);trial.FrontWaitSpent=p.FrontWaitSpent+cut;CommitBuffer(s,d,p,trial,"1끼니→존2 대기 −"+cut+"분 (최소 시작 간격 1시간 30분·식사 종료 후 1시간) · "+phase);}
    }
  }
  static void BedtimeRestBuffer(State s,Day d,GridPlan p){if(!p.PostFourthRestMinutes.HasValue||p.Blocks.Any(x=>x.MealId=="b16")||d.Records.Any(x=>x.MealId=="b16"&&x.Start>=d.Wake))return;int available=Math.Max(0,p.PostFourthRestMinutes.Value-120);int cut=Math.Min(available,(int)Math.Ceiling(Debt(s,d,p)/10.0)*10);if(cut<=0)return;var trial=Trial(p);trial.PostFourthRestMinutes-=cut;CommitBuffer(s,d,p,trial,"4끼니→취침 여유 −"+cut+"분 (식사 종료 후 최소 2시간)");}
  static void EarlyYogurtGap(State s,Day d,GridPlan p,DateTime cutoff){var y=p.Blocks.Find(x=>x.MealId=="b16");if(y==null||p.YogurtRestRelaxed||p.FixedIds.Contains(y.Id)||y.Start<cutoff||Recorded(d,y))return;int rest=p.YogurtRestMinutes??Math.Max(0,60-y.Count*10);int cut=Math.Min(Math.Max(0,rest-30),(int)Math.Ceiling(Debt(s,d,p)/10.0)*10);if(cut<=0)return;var trial=Trial(p);trial.YogurtRestMinutes=rest-cut;CommitBuffer(s,d,p,trial,"요거트 종료→취침 여유 −"+cut+"분 (최소 30분 유지 · 우선 버퍼)");}
  static void YogurtBuffer(State s,Day d,GridPlan p,DateTime cutoff){var yogurt=p.Blocks.Find(x=>x.MealId=="b16");if(yogurt==null)return;int before=Debt(s,d,p);p.YogurtRestRelaxed=true;int recovered=Math.Max(0,before-Debt(s,d,p));if(recovered>0){p.Saved+=recovered;p.Changes.Add("요거트→수면 간격 생략 · 요거트 유지 · "+recovered+"분 회복");}if(Debt(s,d,p)>0)Remove(s,d,p,yogurt,cutoff);}
  static void RestoreOptionalYogurt(State s,Day d,GridPlan p,CellBlock yogurt,CellBlock prep,DateTime? fourthStart,DateTime cutoff){if(yogurt==null||p.Blocks.Any(x=>x.MealId=="b16")||d.Records.Any(x=>x.Start>=d.Wake&&x.MealId=="b16"))return;var fourth=p.Blocks.Find(x=>x.MealId=="b15");double shift=fourth!=null&&fourthStart.HasValue?(fourth.Start-fourthStart.Value).TotalMinutes:0;var candidate=yogurt.Copy();candidate.Start=candidate.Start.AddMinutes(shift);var group=new List<CellBlock>{candidate};if(prep!=null){var preparation=prep.Copy();preparation.Start=candidate.Start.AddMinutes(-preparation.Count*10);group.Add(preparation);}if(candidate.End>EstimatedSleep(s,d,p)||group.Any(x=>x.Start<cutoff||p.Blocks.Any(b=>GridEngine.Overlap(b,x.Start,x.End)))||p.Blocks.Any(x=>!string.IsNullOrEmpty(x.MealId)&&x.Start<candidate.Start&&candidate.Start<x.Start.AddHours(3)))return;p.Blocks.AddRange(group);p.Changes.Add("요거트 10분 유지 · 수면 예상 전 선택 가능");}
  public static void Rebalance(State s,Day d,GridPlan p){
   ZoneFloor(d,p);
   MinimumMealGaps(d,p);
   MinimumWeightGap(d,p);
   PlanHistory.KeepFutureClear(p);
   var optionalYogurt=p.Blocks.Find(x=>x.MealId=="b16");optionalYogurt=optionalYogurt==null?null:optionalYogurt.Copy();var optionalPrep=p.Blocks.Find(x=>x.PrepFor=="b16");optionalPrep=optionalPrep==null?null:optionalPrep.Copy();var originalFourth=p.Blocks.Find(x=>x.MealId=="b15");DateTime? fourthStart=originalFourth==null?(DateTime?)null:originalFourth.Start;
   int initial=Debt(s,d,p);if(initial<=0){p.Overflow=0;MinimumMealGaps(d,p,true);return;}
   DateTime cutoff=d.Records.Where(x=>x.Start>=d.Wake).Select(x=>x.End).DefaultIfEmpty(d.Wake).Max();
   var weight=p.Blocks.Find(x=>x.Category=="웨이트");bool afterWeight=ActualWeight(d)!=null||d.Records.Any(x=>x.Start>=d.Wake&&(x.MealId=="b15"||x.MealId=="b16"));
   if(!afterWeight){
    if(Debt(s,d,p)>0)FrontWaitBuffer(s,d,p,cutoff,"우선 사용");
    if(Debt(s,d,p)>0)PostShowerBuffer(s,d,p,cutoff);
    if(Debt(s,d,p)>0)MealBeforeShower(s,d,p,cutoff);
    if(Debt(s,d,p)>0)FrontWaitBuffer(s,d,p,cutoff,"재확인");
    if(Debt(s,d,p)>0)EarlyYogurtGap(s,d,p,cutoff);
    if(Debt(s,d,p)>0)Remove(s,d,p,p.Blocks.Find(x=>x.MealId=="b10"),cutoff);
    if(Debt(s,d,p)>0)YogurtBuffer(s,d,p,cutoff);
    if(Debt(s,d,p)>0)BedtimeRestBuffer(s,d,p);
    if(Debt(s,d,p)>0)Remove(s,d,p,p.Blocks.Find(x=>x.Category=="존2"),cutoff);
    weight=p.Blocks.Find(x=>x.Category=="웨이트");
    if(Debt(s,d,p)>0&&weight!=null&&!p.FixedIds.Contains(weight.Id)&&p.Blocks.Contains(weight)&&weight.Start>=cutoff&&!Recorded(d,weight)){
     int minimum=Math.Max(1,weight.Minimum>0?weight.Minimum:3);int cut=Math.Min((int)Math.Ceiling(Debt(s,d,p)/10.0),Math.Max(0,weight.Count-minimum));
     cut=Math.Min(cut,MealAdvanceAllowance(p,weight.End)/10);
     if(cut>0){DateTime boundary=weight.End;weight.Count-=cut;foreach(var later in p.Blocks.Where(x=>x.Start>=boundary))later.Start=later.Start.AddMinutes(-cut*10);p.Saved+=cut*10;p.Changes.Add("웨이트 −"+(cut*10)+"분 (최소 "+(minimum*10)+"분)");}
    }
   }else{
    YogurtBuffer(s,d,p,cutoff);
    if(Debt(s,d,p)>0)BedtimeRestBuffer(s,d,p);
   }
   RestoreOptionalYogurt(s,d,p,optionalYogurt,optionalPrep,fourthStart,cutoff);
   MinimumMealGaps(d,p,true);MinimumWeightGap(d,p);PlanHistory.KeepFutureClear(p);p.Blocks=p.Blocks.OrderBy(x=>x.Start).ToList();p.Overflow=Debt(s,d,p);p.Saved=Math.Max(0,initial-p.Overflow);
   var fourthEnd=FourthMealEnd(d,p);if(fourthEnd.HasValue&&fourthEnd.Value.AddHours(2)>Target(s,d))p.Changes.Add("4끼니 종료 후 2시간 휴식과 고정 취침 시각이 충돌 · 실제 기록과 취침 시각 유지");
   if(p.Overflow>0)p.Changes.Add("고정 취침까지 맞추지 못한 시간 "+p.Overflow+"분");
  }
 }
}
