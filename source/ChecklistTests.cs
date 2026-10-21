using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
namespace Ara {
 public static class ChecklistTests {
  public static void Run(Action<string,Action> check,string dir){
   Func<State> seed=()=>{var s=State.New();var d=s.GetDay(new DateTime(2026,10,22));GridEngine.Ensure(s,d);CategoryCatalog.Ensure(s);s.Tasks.Add(new Todo{Id="reading",Text="독서",Group="학습"});return s;};
   Action<State,int,int> record=(s,a,z)=>PhoneEditing.Apply(s,s.Days[0],new PhoneCommand{op="task-record",taskId="reading",lane=1,first=a,last=z});
   check("checklist: record links task and preserves scores when split",()=>{var s=seed();record(s,60,62);GridEngine.SetScore(s.Days[0].Records,s.Days[0].Wake.Date.AddHours(10).AddMinutes(10),1,5,true);Assert(s.Days[0].Records.Count==3&&s.Days[0].Records.All(b=>b.TaskId=="reading")&&s.Tasks[0].DoneDates.Count==0,"linked score fragments");});
   check("checklist: completion and undo completion preserve all actual facts",()=>{var s=seed();record(s,60,62);record(s,66,67);string facts=JsonMap.Write(s.Days[0].Records);Checklist.Edit(s,s.Days[0].Date,new PhoneCommand{op="task-done",taskId="reading",done=true});Assert(s.Tasks[0].DoneDates.Contains(s.Days[0].Date),"done");Checklist.Edit(s,s.Days[0].Date,new PhoneCommand{op="task-done",taskId="reading",done=false});Assert(JsonMap.Write(s.Days[0].Records)==facts&&s.Tasks[0].DoneDates.Count==0,"facts preserved");});
   check("checklist: adjacent different tasks do not merge",()=>{var s=seed();record(s,60,62);s.Tasks.Add(new Todo{Id="reading2",Text="독서",Group="학습"});PhoneEditing.Apply(s,s.Days[0],new PhoneCommand{op="task-record",taskId="reading2",lane=1,first=63,last=65});BlockTools.Merge(s.Days[0].Records);Assert(s.Days[0].Records.Count==2,"different links");});
   check("checklist: another record cannot be overwritten by a drop",()=>{var s=seed();record(s,60,62);s.Tasks.Add(new Todo{Id="other",Text="다른 일",Group="작업"});string facts=JsonMap.Write(s);bool failed=false;try{PhoneEditing.Apply(s,s.Days[0],new PhoneCommand{op="task-record",taskId="other",lane=1,first=61,last=63});}catch(InvalidDataException){failed=true;}Assert(failed&&JsonMap.Write(s)==facts,"rejected without mutation");});
   check("checklist: persistence and phone snapshots preserve task identity",()=>{var s=seed();record(s,60,62);var path=Path.Combine(dir,"checklist.json");new Store(path).Save(s);var loaded=new Store(path).Load();Assert(loaded.Days[0].Records[0].TaskId=="reading"&&PhoneCodec.Snapshot(loaded,loaded.Days[0].Date).actual[0].taskId=="reading","round trip");});
   check("checklist: deleting item preserves historical records",()=>{var s=seed();record(s,60,62);Checklist.Edit(s,s.Days[0].Date,new PhoneCommand{op="task-delete",taskId="reading"});Assert(s.Tasks.Count==0&&s.Days[0].Records[0].TaskId=="reading","retained history");});
   check("checklist: completion and PC score changes merge independently",()=>{var baseline=seed();record(baseline,60,62);var local=Store.Clone(baseline);var current=Store.Clone(baseline);Checklist.Edit(local,local.Days[0].Date,new PhoneCommand{op="task-done",taskId="reading",done=true});GridEngine.SetScore(current.Days[0].Records,current.Days[0].Wake.Date.AddHours(10),3,4);var merged=ReplicaSync.Merge(baseline,local,current,local.Days[0].Date);Assert(merged.Tasks[0].DoneDates.Contains(local.Days[0].Date)&&merged.Days[0].Records[0].TaskId=="reading"&&GridEngine.Total(merged.Days[0])==12,"both independent edits");});
  }
  static void Assert(bool condition,string message){if(!condition)throw new Exception(message);}
 }
}
