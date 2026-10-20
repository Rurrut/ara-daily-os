using System;
using System.Linq;
using System.Collections.Generic;
namespace Ara {
 public static class PlanHistory {
  static List<string> Desired(State s,Day d){
   if(!s.AdaptivePriority||!d.StartAnchoredMeals)return new List<string>();
   var generated=GridEngine.Generate(s,d);DateTime semantic=DateTime.MinValue,visible=DateTime.MinValue;
   foreach(var record in d.Records.Where(x=>x.Start>=d.Wake)){
    var reference=generated.Find(x=>!string.IsNullOrEmpty(record.MealId)&&x.MealId==record.MealId||record.Category=="존2"&&x.Category=="존2"||record.Category=="웨이트"&&x.Category=="웨이트"||record.TemplateId=="b4"&&x.TemplateId=="b4"||record.TemplateId=="b8"&&x.TemplateId=="b8");
    if(reference==null)continue;semantic=reference.Start>semantic?reference.Start:semantic;
    var planned=d.GridBase.Find(x=>!string.IsNullOrEmpty(record.MealId)&&x.MealId==record.MealId||reference.Category=="존2"&&x.Category=="존2"||reference.Category=="웨이트"&&x.Category=="웨이트");
    DateTime marker=planned==null?reference.Start:planned.Start;if(marker>visible)visible=marker;
   }
   // Dragging a completed routine block changes its location, not which earlier actions are frozen.
   return d.GridBase.Where(x=>{if(d.ShowerAfterSecond&&(x.TemplateId=="b5"||x.Name=="샤워")){var secondReference=generated.Find(g=>g.MealId=="b6");return secondReference!=null&&semantic>secondReference.Start;}var reference=generated.Find(g=>g.Id==x.Id||!string.IsNullOrEmpty(x.TemplateId)&&g.TemplateId==x.TemplateId||!string.IsNullOrEmpty(x.MealId)&&g.MealId==x.MealId||!string.IsNullOrEmpty(x.PrepFor)&&g.PrepFor==x.PrepFor);return reference==null?x.Start<=visible:reference.Start<=semantic;}).Select(x=>x.Id).ToList();
  }
  public static void Reset(Day d){d.ShowerAfterSecond=false;d.ManualPlan=false;d.PlanHistory=new List<CellBlock>();d.PlanHistoryIds=new List<string>();d.AnchorReferences=new Dictionary<string,DateTime>();d.AnchorCounts=new Dictionary<string,int>();}
  static string Anchors(Day d){var entries=new List<string>();foreach(var r in d.Records.Where(x=>x.Start>=d.Wake&&!string.IsNullOrEmpty(x.MealId)).OrderBy(x=>x.MealId)){var end=Adaptive.MealEnd(d,new GridPlan{Blocks=new List<CellBlock>()},r.MealId);entries.Add(r.MealId+"|"+r.Start.Ticks+"|"+(end.HasValue?end.Value.Ticks:r.End.Ticks));}foreach(string category in new[]{"존2","웨이트"}){var r=Adaptive.ActualActivity(d,category);if(r!=null)entries.Add(category+"|"+r.Start.Ticks+"|"+r.End.Ticks);}return string.Join(";",entries);}

  static void Capture(Day d,List<string> desired,List<CellBlock> source,bool includeNewBaseline){
   if(d.PlanHistory==null)d.PlanHistory=new List<CellBlock>();if(d.PlanHistoryIds==null)d.PlanHistoryIds=new List<string>();
   d.PlanHistoryIds.RemoveAll(x=>!desired.Contains(x));d.PlanHistory.RemoveAll(x=>!d.PlanHistoryIds.Contains(x.Id));
   foreach(string id in desired.Where(x=>!d.PlanHistoryIds.Contains(x))){var block=source.Find(x=>x.Id==id);if(block==null&&includeNewBaseline)block=d.GridBase.Find(x=>x.Id==id);d.PlanHistoryIds.Add(id);if(block!=null)d.PlanHistory.Add(block.Copy());}
  }
  public static void Ensure(State s,Day d){Capture(d,Desired(s,d),d.GridBase,true);}
  public static void Advance(State before,Day oldDay,State after,Day day){
   bool reset=day.Wake!=oldDay.Wake||(oldDay.PlanHistoryIds.Count>0&&day.PlanHistoryIds.Count==0);
   var adherencePlan=reset?GridEngine.Generate(before,oldDay):GridEngine.Build(before,oldDay).DisplayBlocks;
   bool editedBase=PhoneCodec.Hash(PhoneCodec.Json(oldDay.GridBase))!=PhoneCodec.Hash(PhoneCodec.Json(day.GridBase));
   if(Anchors(day)!=Anchors(oldDay))day.ManualPlan=false;else if(editedBase)day.ManualPlan=true;
   if(reset){Reset(day);Capture(day,Desired(after,day),day.GridBase,true);Adherence.Update(before,oldDay,after,day,GridEngine.Generate(after,day));return;}
   var previous=adherencePlan;
   if(editedBase){foreach(var saved in day.PlanHistory.ToList()){var old=oldDay.GridBase.Find(x=>x.Id==saved.Id);var edited=day.GridBase.Find(x=>x.Id==saved.Id);if(edited!=null&&(old==null||PhoneCodec.Hash(PhoneCodec.Json(old))!=PhoneCodec.Hash(PhoneCodec.Json(edited)))){day.PlanHistory.Remove(saved);day.PlanHistory.Add(edited.Copy());}}day.AnchorReferences.Clear();day.AnchorCounts.Clear();foreach(var r in oldDay.Records.Where(x=>x.Start>=oldDay.Wake&&!string.IsNullOrEmpty(x.MealId))){day.AnchorReferences[r.MealId]=r.Start;day.AnchorCounts[r.MealId]=r.Count;}foreach(string category in new[]{"존2","웨이트"}){var r=Adaptive.ActualActivity(oldDay,category);if(r!=null){day.AnchorReferences[category]=r.Start;day.AnchorCounts[category]=r.Count;}}}
   Capture(day,Desired(after,day),previous,editedBase);
   Adherence.Update(before,oldDay,after,day,previous);
  }
  public static void Configure(Day d,GridPlan p){p.FixedIds=new HashSet<string>(d.PlanHistoryIds);if(d.PlanHistory.Count>0)p.FutureFloor=d.PlanHistory.Max(x=>x.End);}
  public static void KeepFutureClear(GridPlan p){
   if(!p.FutureFloor.HasValue)return;var future=p.Blocks.Where(x=>!p.FixedIds.Contains(x.Id)).ToList();if(future.Count==0)return;
   DateTime begin=future.Min(x=>x.Start);if(begin>=p.FutureFloor.Value)return;int delay=(int)(p.FutureFloor.Value-begin).TotalMinutes;
   foreach(var b in future)b.Start=b.Start.AddMinutes(delay);p.Changes.Add("지난·현재 계획 고정 · 이후 계획 +"+delay+"분");
  }
  public static void Project(Day d,GridPlan p){
   p.VisualBlocks=p.Blocks.Where(x=>!p.FixedIds.Contains(x.Id)).Concat(d.PlanHistory).Select(x=>x.Copy()).OrderBy(x=>x.Start).ToList();
   for(int i=1;i<p.VisualBlocks.Count;i++)if(p.VisualBlocks[i].Start<p.VisualBlocks[i-1].End)throw new InvalidOperationException("고정된 계획과 이후 계획이 겹칩니다. 위 계획을 확인해 주세요.");
  }
 }
}
