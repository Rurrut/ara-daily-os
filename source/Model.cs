using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace Ara {
 [DataContract] public class Block {
  [DataMember] public string Id;
  [DataMember] public string Name;
  [DataMember] public string Kind;
  [DataMember] public int Minutes;
  [DataMember] public int Minimum;
  [DataMember] public int Priority;
  public Block Copy() { return (Block)MemberwiseClone(); }
 }
 [DataContract] public class Day {
  [DataMember] public string Date;
  [DataMember] public DateTime Wake;
  [DataMember] public bool? WakeConfirmed;
  [DataMember] public Dictionary<string,DateTime> Actual = new Dictionary<string,DateTime>();
  [DataMember] public List<string> Done = new List<string>();
  [DataMember] public List<string> Notified = new List<string>();
  [DataMember] public string Note = "";
  [DataMember] public string AdherenceState = "";
  [DataMember] public List<CellBlock> GridBase = new List<CellBlock>();
  [DataMember] public List<CellBlock> Records = new List<CellBlock>();
  [DataMember] public List<CellBlock> PlanHistory = new List<CellBlock>();
  [DataMember] public List<string> PlanHistoryIds = new List<string>();
  [DataMember] public Dictionary<string,DateTime> AnchorReferences = new Dictionary<string,DateTime>();
  [DataMember] public Dictionary<string,int> AnchorCounts = new Dictionary<string,int>();
  [DataMember] public bool GridReady;
  [DataMember] public bool StartAnchoredMeals;
  [DataMember] public bool ManualPlan;
  [DataMember] public bool ShowerAfterSecond;
  [OnDeserialized] void Upgrade(StreamingContext context) {
   if(GridBase==null)GridBase=new List<CellBlock>();
   if(Records==null)Records=new List<CellBlock>();
   if(PlanHistory==null)PlanHistory=new List<CellBlock>();
   if(PlanHistoryIds==null)PlanHistoryIds=new List<string>();
   if(AnchorReferences==null)AnchorReferences=new Dictionary<string,DateTime>();
   if(AnchorCounts==null)AnchorCounts=new Dictionary<string,int>();
  }
 }
 [DataContract] public class Todo {
  [DataMember] public string Id;
  [DataMember] public string Text;
  [DataMember] public string Group;
  [DataMember] public List<string> DoneDates = new List<string>();
 }
 [DataContract] public class State {
  [DataMember] public int Version = 1;
  [DataMember] public int ScheduleRevision;
  [DataMember] public bool AdaptivePriority;
  [DataMember] public int AdaptiveRevision;
  [DataMember] public List<RemoteReceipt> RemoteReceipts = new List<RemoteReceipt>();
  [DataMember] public List<Block> Template;
  [DataMember] public List<Day> Days = new List<Day>();
  [DataMember] public List<Todo> Tasks = new List<Todo>();
  [DataMember] public int DayMinutes = 750;
  [DataMember] public int ShrinkThreshold = 30;
  [DataMember] public int ReminderMinutes = 5;
  [DataMember] public bool Notifications = true;
  [DataMember] public bool DarkMode;
  [DataMember] public decimal? BlankSatisfactionValue;
  [DataMember] public bool ExcludeBlankSatisfaction;
  public decimal BlankSatisfaction{get{return BlankSatisfactionValue??2;}}
  [DataMember] public List<CategoryDefinition> CategorySettings;
  [DataMember] public string WorkLink = "";
  [DataMember] public string StudyLink = "";
  [DataMember] public int FocusMinutes = 25;
  [DataMember] public DateTime? FocusEnd;
  [DataMember] public string FocusName = "";
  [DataMember] public bool FocusNotified;
  public static State New() {var s=Legacy();Routine.ApplyDefaults(s);Adaptive.Upgrade(s);return s;}
  public static State Legacy() {
   var s = new State();
   string[] names={"기상 · 준비","첫 식사","집중 작업 1","짧은 휴식","존2","샤워 · 정리","두 번째 식사","집중 작업 2","웨이트","회복 · 휴식","세 번째 식사","학습","요거트","개인 시간","취침 준비"};
   string[] kinds={"life","meal","work","rest","exercise","life","meal","work","exercise","rest","meal","study","meal","rest","life"};
   int[] mins={30,30,120,15,45,30,30,120,60,30,30,90,20,70,30};
   int[] minimum={30,30,120,5,20,30,30,120,30,10,30,30,5,20,30};
   int[] priority={0,0,0,5,1,0,0,0,2,5,0,3,4,6,0};
   s.Template = new List<Block>();
   for(int i=0;i<names.Length;i++)s.Template.Add(new Block{Id="b"+i,Name=names[i],Kind=kinds[i],Minutes=mins[i],Minimum=minimum[i],Priority=priority[i]});
   return s;
  }
  public Day GetDay(DateTime date) {
   string key=date.ToString("yyyy-MM-dd");
   var d=Days.Find(x=>x.Date==key);
   if(d==null) { d=new Day{Date=key,Wake=date.Date.AddHours(8),WakeConfirmed=false}; Days.Add(d); }
   return d;
  }
 }
 public class Slot {
  public Block Block;
  public DateTime Start,End;
  public int Minutes;
  public bool Actual,Done;
  public int Cut;
 }
 public class Plan {
  public List<Slot> Slots;
  public List<string> Changes = new List<string>();
  public int Overflow;
  public int Saved;
  public DateTime Target;
 }
 public static class Engine {
  static List<Slot> Layout(State s, Day d, Dictionary<string,int> durations, List<string> changes) {
   var result=new List<Slot>();
   DateTime cursor=d.Wake;
   foreach(var b in s.Template) {
    int duration=durations[b.Id];
    DateTime start=cursor;
    bool actual=d.Actual.ContainsKey(b.Id);
    if(actual) {
     start=d.Actual[b.Id];
     if(start<d.Wake)throw new InvalidOperationException("실제 시작 시각은 기상 시각보다 빠를 수 없습니다.");
     if(start<cursor) {
      DateTime boundary=start;
      for(int j=result.Count-1;j>=0;j--) {
       var prev=result[j];
       if(prev.End<=boundary)break;
       if(prev.Actual || prev.Done)throw new InvalidOperationException("기록되거나 완료한 일정과 겹칩니다. 앞 일정의 실제 시각이나 완료 표시를 먼저 확인해 주세요.");
       int trim=(int)Math.Ceiling((prev.End-boundary).TotalMinutes);
       int remove=Math.Min(trim,prev.Minutes);
       prev.Minutes-=remove; prev.Cut+=remove;
       prev.End=boundary;
       if(prev.Start>boundary)prev.Start=boundary;
       if(remove>0 && changes!=null)changes.Add(prev.Block.Name+" " +remove+"분 단축 (이른 실제 시작)");
       boundary=prev.Start;
      }
     }
    }
    var slot=new Slot{Block=b,Start=start,End=start.AddMinutes(duration),Minutes=duration,Actual=actual,Done=d.Done.Contains(b.Id),Cut=b.Minutes-duration};
    result.Add(slot); cursor=slot.End;
   }
   return result;
  }
  public static Plan Build(State s, Day d) {
   Validate(s);
   var durations=s.Template.ToDictionary(b=>b.Id,b=>b.Minutes);
   var initial=Layout(s,d,durations,null);
   var p=new Plan{Target=d.Wake.AddMinutes(s.DayMinutes)};
   int debt=Math.Max(0,(int)Math.Ceiling((initial.Last().End-p.Target).TotalMinutes));
   int lastAnchor=initial.FindLastIndex(x=>x.Actual || x.Done);
   // Small delays shift the day; only a large overrun spends remaining buffers.
   if(debt>=s.ShrinkThreshold && lastAnchor>=0) {
    var eligible=initial.Select((slot,i)=>new {slot,i}).Where(x=>x.i>lastAnchor && x.slot.Block.Priority>0).OrderBy(x=>x.slot.Block.Priority).ThenBy(x=>x.i);
    foreach(var entry in eligible) {
     var b=entry.slot.Block;
     int cut=Math.Min(debt,Math.Max(0,entry.slot.Minutes-b.Minimum));
     durations[b.Id]-=cut; debt-=cut; p.Saved+=cut;
     if(cut>0)p.Changes.Add(b.Name+" "+cut+"분 단축 (유동 버퍼)");
     if(debt==0)break;
    }
   }
   p.Slots=Layout(s,d,durations,p.Changes);
   p.Overflow=Math.Max(0,(int)Math.Ceiling((p.Slots.Last().End-p.Target).TotalMinutes));
   return p;
  }
  public static void Validate(State s) {
   if(s!=null&&(s.BlankSatisfaction<0||s.BlankSatisfaction>5))throw new InvalidDataException("빈칸 만족도는 0~5점입니다.");
   if(s==null || s.Version!=1 || s.Template==null || s.Template.Count==0 || s.Template.Count>100 || s.Days==null || s.Tasks==null)throw new InvalidDataException("지원하지 않거나 잘못된 데이터입니다.");
   if(s.DayMinutes<60 || s.DayMinutes>1440 || s.ShrinkThreshold<1 || s.ShrinkThreshold>240 || s.ReminderMinutes<0 || s.ReminderMinutes>120 || s.FocusMinutes<1 || s.FocusMinutes>240)throw new InvalidDataException("설정 범위가 올바르지 않습니다.");
   CategoryCatalog.Validate(s);
   if(s.RemoteReceipts==null)s.RemoteReceipts=new List<RemoteReceipt>();
   if(s.RemoteReceipts.Count>128||s.RemoteReceipts.Any(x=>x==null||string.IsNullOrEmpty(x.id)||string.IsNullOrEmpty(x.hash)))throw new InvalidDataException("휴대폰 동기화 기록이 올바르지 않습니다.");
   var ids=new HashSet<string>();
   foreach(var b in s.Template)if(string.IsNullOrWhiteSpace(b.Id)||!ids.Add(b.Id)||string.IsNullOrWhiteSpace(b.Name)||b.Minutes<1||b.Minutes>480||b.Minimum<0||b.Minimum>b.Minutes||b.Priority<0||b.Priority>20)throw new InvalidDataException("일정의 이름, 길이, 최소 길이 또는 우선순위를 확인해 주세요.");
   foreach(var d in s.Days) {
    DateTime parsed;
    if(d==null || !DateTime.TryParseExact(d.Date,"yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.None,out parsed) || d.Wake.Date!=parsed.Date || d.Actual==null || d.Done==null || d.Notified==null)throw new InvalidDataException("날짜 기록이 올바르지 않습니다.");
    GridEngine.Validate(d);
   }
   if(s.Days.Select(d=>d.Date).Distinct().Count()!=s.Days.Count)throw new InvalidDataException("날짜 기록이 중복되었습니다.");
   foreach(var t in s.Tasks)if(t==null || string.IsNullOrWhiteSpace(t.Id) || string.IsNullOrWhiteSpace(t.Text) || t.DoneDates==null)throw new InvalidDataException("과제 데이터가 올바르지 않습니다.");
  }
 }
 public class Store {
  public string PathName;
  public string Warning="";
  public Store(string path){PathName=path;}
  public State ReadFile(string path) {
   using(var f=File.OpenRead(path)) {
    if(f.Length>20*1024*1024)throw new InvalidDataException("백업 파일이 너무 큽니다.");
    var s=(State)new DataContractJsonSerializer(typeof(State)).ReadObject(f);
    Engine.Validate(s);
    foreach(var d in s.Days){if(d.GridReady)GridEngine.Build(s,d);else Engine.Build(s,d);}
    return s;
   }
  }
  public State Load() {
   if(!File.Exists(PathName))return State.New();
   try{return ReadFile(PathName);}catch(Exception e){
    Diagnostics.Error("data-load",e);
    if(File.Exists(PathName+".bak")) {
     try {
      var s=ReadFile(PathName+".bak");
      File.Copy(PathName,PathName+".damaged-"+DateTime.Now.ToString("yyyyMMddHHmmss"),false);
      Warning="저장 파일 손상이 발견되어 직전 백업을 복구했습니다. 손상 파일은 별도로 보존했습니다.";
      return s;
     } catch(Exception backupError){Diagnostics.Error("data-recovery",backupError); }
    }
    throw new InvalidDataException("저장 파일을 읽지 못했습니다. 원본은 보존됩니다. "+e.Message);
   }
  }
  public Action<State> Saved;
  public void Save(State s) {
   Engine.Validate(s);
   Directory.CreateDirectory(System.IO.Path.GetDirectoryName(PathName));
   string tmp=PathName+".tmp";
   using(var f=new FileStream(tmp,FileMode.Create,FileAccess.Write,FileShare.None)){
    new DataContractJsonSerializer(typeof(State)).WriteObject(f,s); f.Flush(true);
   }
   if(File.Exists(PathName))File.Replace(tmp,PathName,PathName+".bak");else File.Move(tmp,PathName);
   if(Saved!=null)try{Saved(s);}catch(Exception e){Diagnostics.Error("diary-queue",e);}
  }
  public static State Clone(State s) {
   using(var m=new MemoryStream()) {
    var serializer=new DataContractJsonSerializer(typeof(State));serializer.WriteObject(m,s);m.Position=0;return (State)serializer.ReadObject(m);
   }
  }
 }
 public static class Reminders {
  public static List<Slot> Due(State s, Day d, Plan p, DateTime now) {
   return p.Slots.Where(x=>!x.Done && x.Minutes>0 && (x.Block.Kind=="meal"||x.Block.Kind=="work"||x.Block.Kind=="study"||x.Block.Kind=="exercise") && now>=x.Start.AddMinutes(-s.ReminderMinutes) && now<x.End && !d.Notified.Contains(Key(x))).ToList();
  }
  public static string Key(Slot s){return s.Block.Id+"|"+s.Start.ToString("o");}
 }
}
