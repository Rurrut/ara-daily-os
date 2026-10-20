using System;
using System.Linq;
using System.IO;
using System.Text;
using System.Globalization;
using System.Security.Cryptography;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Collections.Generic;

namespace Ara {
 [DataContract] public class RemoteReceipt {[DataMember] public string id,hash;}
 [DataContract] public class PhoneCommand {
  [DataMember] public string op,date,baseRevision,requestId,category,name,mealId,sourceId,id,color;
  [DataMember] public bool hidden;
  [DataMember] public int position;
  [DataMember] public decimal satisfaction;
  [DataMember] public int lane,first,last;
  [DataMember] public decimal score;
 }
 [DataContract] public class PhoneBlock {
  [DataMember] public string id,name,category,mealId,templateId,prepFor;
  [DataMember] public int start,count;
  [DataMember] public decimal score,satisfaction;
 }
 [DataContract] public class PhoneDay {
  [DataMember] public string date,revision;
  [DataMember] public int wake,sleep,target,lightsStart,overflow;
  [DataMember] public decimal total,satisfactionAverage;
  [DataMember] public int satisfactionMinutes;
  [DataMember] public decimal blankSatisfaction;
  [DataMember] public bool excludeBlankSatisfaction,wakeConfirmed;
   [DataMember] public decimal? adherenceScore;
   [DataMember] public int adherenceCount;
   [DataMember] public string adherenceDetails;
  [DataMember] public List<CategoryDefinition> categorySettings;
  [DataMember] public string[] categories,adjustments;
  [DataMember] public List<PhoneBlock> planned,actual;
 }
 [DataContract] public class LegacyPhoneCommand {[DataMember] public string op,date,baseRevision,requestId,category,name,mealId,sourceId,id;[DataMember] public int lane,first,last;[DataMember] public decimal score;}
 public class SyncConflict:Exception {public SyncConflict():base("PC에서 기록이 바뀌었습니다. 최신 기록을 확인한 뒤 다시 적용해 주세요."){} public SyncConflict(string message):base(message){} }
 public static class PhoneCodec {
  public static byte[] Json<T>(T value){using(var m=new MemoryStream()){new DataContractJsonSerializer(typeof(T)).WriteObject(m,value);return m.ToArray();}}
  public static T Read<T>(byte[] body){using(var m=new MemoryStream(body))return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(m);}
  public static string Hash(byte[] body){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(body)).Replace("-","").ToLowerInvariant();}
  public static string CommandHash(PhoneCommand command){var copy=Read<PhoneCommand>(Json(command));copy.baseRevision="";if(new[]{"resize","satisfaction","category-save","satisfaction-settings"}.Contains(command.op))return Hash(Json(copy));return Hash(Json(Read<LegacyPhoneCommand>(Json(copy))));}
  public static DateTime Date(string key){DateTime date;if(!DateTime.TryParseExact(key,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out date))throw new InvalidDataException("날짜를 확인해 주세요.");return date;}
  public static PhoneDay Snapshot(State state,string key){
   var s=Store.Clone(state);var day=s.GetDay(Date(key));var plan=GridEngine.Build(s,day);DateTime origin=day.Wake.Date;Func<DateTime,int> index=t=>(int)((t-origin).TotalMinutes/10);
   Func<CellBlock,PhoneBlock> block=x=>new PhoneBlock{id=x.Id,name=x.Name,category=x.Category,mealId=x.MealId,templateId=x.TemplateId,prepFor=x.PrepFor,start=index(x.Start),count=x.Count,score=x.Score,satisfaction=x.Satisfaction};
   var ah=Adherence.Report(day);var view=new PhoneDay{adherenceScore=Adherence.Score(day),adherenceCount=Convert.ToInt32(ah["evaluated_actions"]),adherenceDetails=JsonMap.Write(ah["actions"]),date=key,wake=index(day.Wake),wakeConfirmed=SatisfactionPolicy.WakeConfirmed(day),blankSatisfaction=s.BlankSatisfaction,excludeBlankSatisfaction=s.ExcludeBlankSatisfaction,satisfactionMinutes=SatisfactionPolicy.Calculate(s,day,Adaptive.EstimatedSleep(s,day,plan),DateTime.Now).Minutes,sleep=index(Adaptive.EstimatedSleep(s,day,plan)),target=index(Adaptive.Target(s,day)),overflow=plan.Overflow,total=GridEngine.Total(day),categories=CategoryCatalog.Keys(s),categorySettings=s.CategorySettings,satisfactionAverage=GridEngine.SatisfactionAverage(s,day,plan),adjustments=plan.Changes.ToArray(),planned=plan.DisplayBlocks.Select(block).ToList(),actual=day.Records.OrderBy(x=>x.Start).Select(block).ToList()};view.lightsStart=view.sleep-12;decimal metric=view.satisfactionAverage;int elapsed=view.satisfactionMinutes;view.satisfactionAverage=0;view.satisfactionMinutes=0;view.revision=Hash(Json(view));view.satisfactionAverage=metric;view.satisfactionMinutes=elapsed;return view;
  }
 }
 public static class PhoneEditing {
  static void Defaults(CellBlock b){b.Priority=b.Category=="존2"?1:b.Category=="웨이트"?2:b.Category=="학습"?3:b.Category=="휴식"?5:0;b.Minimum=b.Category=="존2"?2:b.Category=="웨이트"||b.Category=="학습"?3:b.Category=="휴식"?1:b.Count;}
  static void Wake(State s,Day d,PhoneCommand c){PlanHistory.Reset(d);if(c.first>=144)throw new InvalidDataException("기상 날짜를 먼저 바꿔 주세요.");d.Wake=d.Wake.Date.AddMinutes(c.first*10);d.WakeConfirmed=true;d.GridBase=GridEngine.Generate(s,d);d.GridReady=true;d.StartAnchoredMeals=true;if(c.lane==1){var old=d.Records.Find(x=>GridEngine.Overlap(x,d.Wake,d.Wake.AddMinutes(10)));decimal score=old==null?0:old.Score;d.Records.RemoveAll(x=>x.Category=="기상");GridEngine.SetRange(d.Records,new CellBlock{Id=Guid.NewGuid().ToString("N"),Start=d.Wake,Count=1,Name="기상",Category="기상",Score=score,Satisfaction=old==null?2:old.Satisfaction});}}
  static void Put(Day d,GridPlan p,PhoneCommand c,CellBlock entry){
   if(c.lane==0){d.GridBase=p.DisplayBlocks.Select(x=>x.Copy()).ToList();entry.Score=0;GridEngine.SetRange(d.GridBase,entry);return;}
   var originals=d.Records.Select(x=>x.Copy()).ToList();DateTime cursor=entry.Start;bool first=true;
   while(cursor<entry.End){var old=originals.Find(x=>GridEngine.Overlap(x,cursor,cursor.AddMinutes(10)));decimal score=old==null?0:old.Score;decimal satisfaction=old==null?2:old.Satisfaction;DateTime begin=cursor;
    do{cursor=cursor.AddMinutes(10);var next=originals.Find(x=>GridEngine.Overlap(x,cursor,cursor.AddMinutes(10)));if(cursor>=entry.End||(next==null?0:next.Score)!=score||(next==null?2:next.Satisfaction)!=satisfaction)break;}while(true);
    var part=entry.Copy();part.Start=begin;part.Count=(int)((cursor-begin).TotalMinutes/10);part.Score=score;part.Satisfaction=satisfaction;if(!first){part.Id=Guid.NewGuid().ToString("N");part.MealId="";}GridEngine.SetRange(d.Records,part);first=false;
   }
  }
  public static void Apply(State s,Day d,PhoneCommand c){
   if(c.op=="satisfaction-settings"){if(c.satisfaction<0||c.satisfaction>5)throw new InvalidDataException("빈칸 만족도는 0~5점입니다.");s.BlankSatisfactionValue=c.satisfaction;s.ExcludeBlankSatisfaction=c.hidden;return;}
   if(c.lane<0||c.lane>1||c.first<0||c.last<c.first||c.last>=288||c.score < -100000||c.score>100000)throw new InvalidDataException("시간 칸 또는 점수를 확인해 주세요.");
   if(!string.IsNullOrEmpty(c.mealId)&&!new[]{"b1","b6","b10","b15","b16"}.Contains(c.mealId))throw new InvalidDataException("끼니 번호를 확인해 주세요.");
   var p=GridEngine.Build(s,d);DateTime start=d.Wake.Date.AddMinutes(c.first*10);int count=c.last-c.first+1;
   if(c.op=="wake"||c.op=="generate"||((c.op=="category"||c.op=="detail")&&c.category=="기상")){Wake(s,d,c);return;}
   if(c.op=="category-delete"){var category=s.CategorySettings.Find(x=>x.name==c.id);if(category==null)throw new InvalidDataException("카테고리가 없습니다.");category.deleted=true;category.hidden=true;return;}
   if(c.op=="category-save"){CategoryCatalog.Save(s,c.id,c.name,c.color,c.hidden,c.position);return;}
   if(c.op=="resize"){var list=c.lane==0?p.DisplayBlocks.Select(x=>x.Copy()).ToList():d.Records;BlockTools.Resize(list,c.id,start,count,d.Wake.Date,c.lane==0);if(c.lane==0)d.GridBase=list;return;}
   if(c.op=="satisfaction"){if(c.lane!=1||c.satisfaction<0||c.satisfaction>5)throw new InvalidDataException("만족도는 실제 칸에 0~5점으로 기록해 주세요.");GridEngine.SetScore(d.Records,start,count,c.satisfaction,true);return;}
   if(c.op=="score"){if(c.lane!=1)throw new InvalidDataException("점수는 실제 칸에 입력해 주세요.");GridEngine.SetScore(d.Records,start,count,c.score);return;}
   if(c.op=="delete"){
    var visible=c.lane==0?p.DisplayBlocks:d.Records;var ids=visible.Where(x=>GridEngine.Overlap(x,start,start.AddMinutes(count*10))).Select(x=>x.Id).ToList();var list=c.lane==0?p.DisplayBlocks.Select(x=>x.Copy()).ToList():d.Records;
    var meals=list.Where(x=>ids.Contains(x.Id)&&!string.IsNullOrEmpty(x.MealId)).Select(x=>x.MealId).ToList();list.RemoveAll(x=>ids.Contains(x.Id)||(c.lane==0&&meals.Contains(x.PrepFor)));if(c.lane==0)d.GridBase=list;return;
   }
   if(c.op=="move"){
    var visible=c.lane==0?p.DisplayBlocks:d.Records;var source=visible.Find(x=>x.Id==c.id);if(source==null)throw new InvalidDataException("이동할 블록이 없습니다.");
    if(source.Category=="기상"){Wake(s,d,c);return;}
    if(c.lane==0){d.GridBase=p.DisplayBlocks.Select(x=>x.Copy()).ToList();GridEngine.MovePlanned(d.GridBase,c.id,start);}else GridEngine.Move(d.Records,c.id,start);return;
   }
   CellBlock entry;
   if(c.op=="copy"){
    if(c.lane!=1)throw new InvalidDataException("계획은 실제 칸으로 가져와 주세요.");var source=p.DisplayBlocks.Find(x=>x.Id==c.sourceId);if(source==null)throw new InvalidDataException("계획이 바뀌었습니다. 다시 선택해 주세요.");if(source.Category=="기상"){Wake(s,d,c);return;}entry=source.Copy();entry.Id=Guid.NewGuid().ToString("N");entry.Start=start;entry.Count=count;
   }else if(c.op=="category"||c.op=="detail"){
    if(s.CategorySettings.Any(x=>x.name==c.category&&x.deleted)||!CategoryCatalog.Keys(s,true).Contains(c.category)||c.name!=null&&c.name.Length>250)throw new InvalidDataException("카테고리 또는 내용을 확인해 주세요.");
    var old=(c.lane==0?p.DisplayBlocks:d.Records).Find(x=>GridEngine.Overlap(x,start,start.AddMinutes(10)));
    entry=new CellBlock{Id=Guid.NewGuid().ToString("N"),Start=start,Count=count,Category=c.category,Name=c.op=="detail"?(c.name??""):old!=null&&old.Category==c.category?old.Name:""};
    if(old!=null&&old.Start==start&&old.Count==count)entry.Id=old.Id;
    if(c.op=="detail"&&GridEngine.MealName(entry.Name))entry.Category="식사";
    if(entry.Category=="식사")entry.MealId=!string.IsNullOrEmpty(c.mealId)?c.mealId:old!=null&&old.Category=="식사"&&!string.IsNullOrEmpty(old.MealId)?old.MealId:GridEngine.DetectMeal(d,p,entry);
    Defaults(entry);
   }else throw new InvalidDataException("지원하지 않는 조작입니다.");
   Put(d,p,c,entry);
  }
 }
 public partial class MainForm {
  Dictionary<string,Day> phoneUndo=new Dictionary<string,Day>();
  Dictionary<string,List<CategoryDefinition>> categoryUndo=new Dictionary<string,List<CategoryDefinition>>();
  public PhoneDay PhoneSnapshot(string key){return PhoneCodec.Snapshot(Data,key);}
  public PhoneDay PhoneApply(PhoneCommand command){
   SaveNote();if(string.IsNullOrEmpty(command.requestId)||command.requestId.Length>80)throw new InvalidDataException("요청 번호가 올바르지 않습니다.");string hash=PhoneCodec.CommandHash(command);
   if(Data.RemoteReceipts==null)Data.RemoteReceipts=new List<RemoteReceipt>();var receipt=Data.RemoteReceipts.Find(x=>x.id==command.requestId);
   if(receipt!=null){if(receipt.hash!=hash)throw new InvalidDataException("중복 요청의 내용이 다릅니다.");return PhoneSnapshot(command.date);}
   var before=PhoneSnapshot(command.date);if(before.revision!=command.baseRevision)throw new SyncConflict();
   var edited=Store.Clone(Data);var day=edited.GetDay(PhoneCodec.Date(command.date));string currentDate=Day.Date;
   try{if(command.op=="undo"){Day restored;if(!phoneUndo.TryGetValue(command.sourceId??"",out restored))throw new InvalidDataException("다시 실행한 뒤에는 이전 변경을 되돌릴 수 없습니다.");if(restored.Date!=command.date)throw new InvalidDataException("기록을 바꾼 날짜에서 되돌려 주세요.");var copy=Store.Clone(Data);copy.Days=new List<Day>{restored};restored=Store.Clone(copy).Days[0];restored.Note=day.Note;restored.Done=day.Done;restored.Notified=day.Notified;edited.Days.Remove(day);edited.Days.Add(restored);day=restored;List<CategoryDefinition> restoredCategories;if(categoryUndo.TryGetValue(command.sourceId??"",out restoredCategories))edited.CategorySettings=PhoneCodec.Read<List<CategoryDefinition>>(PhoneCodec.Json(restoredCategories));}else {PhoneEditing.Apply(edited,day,command);BlockTools.Merge(day.Records);if(command.lane==0&&command.op!="category-save")BlockTools.Merge(day.GridBase);var historicalBefore=Store.Clone(Data);PlanHistory.Advance(historicalBefore,historicalBefore.GetDay(PhoneCodec.Date(command.date)),edited,day);}GridEngine.Build(edited,day);Engine.Validate(edited);edited.RemoteReceipts.Add(new RemoteReceipt{id=command.requestId,hash=hash});if(edited.RemoteReceipts.Count>128)edited.RemoteReceipts.RemoveRange(0,edited.RemoteReceipts.Count-128);Store.Save(edited);if(command.op=="category-save")categoryUndo[command.requestId]=Store.Clone(Data).CategorySettings;if(categoryUndo.Count>16)categoryUndo.Remove(categoryUndo.Keys.First());phoneUndo[command.requestId]=Store.Clone(Data).GetDay(PhoneCodec.Date(command.date));if(phoneUndo.Count>16)phoneUndo.Remove(phoneUndo.Keys.First());undo=Data;Data=edited;Day=Data.GetDay(PhoneCodec.Date(currentDate));RefreshAll();Diagnostics.Action("mobile-"+command.op,command.lane+":"+command.first+"-"+command.last);return PhoneSnapshot(command.date);}catch(Exception e){Diagnostics.Error("mobile-change",e);throw;}
  }
 }
}

