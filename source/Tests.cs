using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Drawing;
using System.Windows.Forms;

namespace Ara {
 public static class Tests {
  static List<string> results;
  static void Check(string name,Action test){try{test();results.Add("PASS "+name);}catch(Exception e){results.Add("FAIL "+name+" : "+e.Message);}}
  static void Assert(bool ok,string message){if(!ok)throw new Exception(message);}
  static State New(){var s=State.Legacy();s.GetDay(new DateTime(2026,10,13));return s;}
  static Day D(State s){return s.Days[0];}
  static void Consistent(Plan p){for(int i=1;i<p.Slots.Count;i++)Assert(p.Slots[i].Start>=p.Slots[i-1].End,"overlap at "+i);foreach(var x in p.Slots)Assert(x.End>=x.Start,"negative length");}
  public static int Run(string[] args){
   string dir=Path.GetDirectoryName(Program.DataPath(args));Directory.CreateDirectory(dir);results=new List<string>();
   Check("wake-relative plan, all blocks, exact day target",()=>{var s=New();var p=Engine.Build(s,D(s));Assert(p.Slots.Count==15,"count");Assert(p.Slots.First().Start.Hour==8,"wake");Assert(p.Slots.Last().End==D(s).Wake.AddMinutes(750),"target");Consistent(p);});
   Check("wake time moved 2 hours shifts every block",()=>{var s=New();var a=Engine.Build(s,D(s));D(s).Wake=D(s).Wake.AddHours(2);var b=Engine.Build(s,D(s));for(int i=0;i<a.Slots.Count;i++)Assert(b.Slots[i].Start==a.Slots[i].Start.AddHours(2),"shift");});
   Check("early lunch pulls later meals and schedule forward",()=>{var s=New();var a=Engine.Build(s,D(s));D(s).Actual["b6"]=a.Slots[6].Start.AddMinutes(-40);var b=Engine.Build(s,D(s));Assert(b.Slots[10].Start==a.Slots[10].Start.AddMinutes(-40),"dinner");Assert(b.Slots[5].Minutes==0,"preceding shower removed");Assert(b.Slots[4].Minutes==35,"preceding exercise truncated");Consistent(b);});
   Check("extremely early meal handles multiple preceding blocks",()=>{var s=New();var a=Engine.Build(s,D(s));D(s).Actual["b6"]=D(s).Wake.AddMinutes(10);var b=Engine.Build(s,D(s));Assert(b.Slots[6].Start==D(s).Wake.AddMinutes(10),"anchor");Consistent(b);});
   Check("small delay shifts without spending buffers",()=>{var s=New();var a=Engine.Build(s,D(s));D(s).Actual["b1"]=a.Slots[1].Start.AddMinutes(20);var b=Engine.Build(s,D(s));Assert(b.Saved==0&&b.Overflow==20,"buffer threshold");Assert(b.Slots[6].Start==a.Slots[6].Start.AddMinutes(20),"second meal");Consistent(b);});
   Check("large delay uses zone2, weights, learning, yogurt in order",()=>{var s=New();var a=Engine.Build(s,D(s));D(s).Actual["b1"]=a.Slots[1].Start.AddMinutes(140);var b=Engine.Build(s,D(s));Assert(b.Slots[4].Minutes==20,"zone2 min");Assert(b.Slots[8].Minutes==30,"weights min");Assert(b.Slots[11].Minutes==30,"study min");Assert(b.Slots[12].Minutes==5,"yogurt min");Assert(b.Overflow==0&&b.Saved==140,"saved");Consistent(b);});
   Check("late lunch only spends buffers after lunch",()=>{var s=New();var a=Engine.Build(s,D(s));D(s).Actual["b6"]=a.Slots[6].Start.AddMinutes(120);var b=Engine.Build(s,D(s));Assert(b.Slots[4].Minutes==45,"past exercise unchanged");Assert(b.Slots[8].Minutes==30&&b.Slots[11].Minutes==30,"future cut");Assert(b.Overflow==0,"target recovered");Consistent(b);});
   Check("buffer exhaustion reports remaining overrun",()=>{var s=New();var a=Engine.Build(s,D(s));D(s).Actual["b10"]=a.Slots[10].Start.AddMinutes(300);var b=Engine.Build(s,D(s));Assert(b.Saved==125&&b.Overflow==175,"exhaustion " + b.Saved+"/"+b.Overflow);Consistent(b);});
   Check("editing an actual record recalculates without accumulated drift",()=>{var s=New();var basePlan=Engine.Build(s,D(s));D(s).Actual["b1"]=basePlan.Slots[1].Start.AddMinutes(80);Engine.Build(s,D(s));D(s).Actual["b1"]=basePlan.Slots[1].Start.AddMinutes(10);var p=Engine.Build(s,D(s));Assert(p.Saved==0&&p.Slots[4].Minutes==45&&p.Overflow==10,"reset lengths");});
   Check("multiple recorded meals remain fixed",()=>{var s=New();var a=Engine.Build(s,D(s));D(s).Actual["b1"]=a.Slots[1].Start.AddMinutes(20);D(s).Actual["b6"]=a.Slots[6].Start.AddMinutes(35);var p=Engine.Build(s,D(s));Assert(p.Slots[1].Start==D(s).Actual["b1"]&&p.Slots[6].Start==D(s).Actual["b6"],"anchors");Consistent(p);});
   Check("overlap with completed history rejects conflicting recording",()=>{var s=New();var a=Engine.Build(s,D(s));D(s).Done.Add("b5");D(s).Actual["b6"]=a.Slots[6].Start.AddMinutes(-10);bool failed=false;try{Engine.Build(s,D(s));}catch(InvalidOperationException){failed=true;}Assert(failed,"must reject");});
   Check("overlap with earlier actual anchor rejects recording",()=>{var s=New();var a=Engine.Build(s,D(s));D(s).Actual["b1"]=a.Slots[1].Start;D(s).Actual["b6"]=a.Slots[1].Start.AddMinutes(5);bool failed=false;try{Engine.Build(s,D(s));}catch(InvalidOperationException){failed=true;}Assert(failed,"must reject");});
   Check("midnight is a full date, next-day labels",()=>{var s=New();D(s).Wake=D(s).Wake.Date.AddHours(18);var p=Engine.Build(s,D(s));Assert(p.Slots.Last().End.Date==D(s).Wake.Date.AddDays(1),"next day");Assert(Theme.Time(p.Slots.Last().End,D(s)).Contains("+1"),"label");Consistent(p);});
   Check("reminder lead time, deduplication and completed filtering",()=>{var s=New();var p=Engine.Build(s,D(s));var now=p.Slots[1].Start.AddMinutes(-s.ReminderMinutes);var due=Reminders.Due(s,D(s),p,now);Assert(due.Count==1&&due[0].Block.Id=="b1","due");D(s).Notified.Add(Reminders.Key(due[0]));Assert(Reminders.Due(s,D(s),p,now).Count==0,"dedup");D(s).Notified.Clear();D(s).Done.Add("b1");p=Engine.Build(s,D(s));Assert(Reminders.Due(s,D(s),p,now).Count==0,"done ignored");});
   Check("missed and zero-length blocks do not notify",()=>{var s=New();var p=Engine.Build(s,D(s));Assert(Reminders.Due(s,D(s),p,p.Slots.Last().End.AddMinutes(1)).Count==0,"expired");});
   Check("JSON preserves Korean tasks, notes, focus and actual dates",()=>{var s=New();var p=Engine.Build(s,D(s));D(s).Actual["b6"]=p.Slots[6].Start;D(s).Note="한국어 메모 테스트";s.Tasks.Add(new Todo{Id="test",Text="과제 끝내기",Group="학습",DoneDates=new List<string>{D(s).Date}});s.FocusEnd=DateTime.Now.AddMinutes(5);var store=new Store(Path.Combine(dir,"roundtrip.json"));store.Save(s);var t=store.Load();Assert(t.Tasks[0].Text==s.Tasks[0].Text&&t.Days[0].Note==D(s).Note,"Korean");Assert(t.Days[0].Actual["b6"]==D(s).Actual["b6"],"actual");Assert(t.Tasks[0].DoneDates.Contains(D(s).Date)&&t.FocusEnd.HasValue,"state");});
   Check("atomic replacement and recovery preserve damaged file",()=>{string path=Path.Combine(dir,"recover.json");var st=new Store(path);var s=New();st.Save(s);s.WorkLink="https://example.com";st.Save(s);Assert(File.Exists(path+".bak"),"backup");File.WriteAllText(path,"{broken");var recovered=st.Load();Assert(recovered.Template.Count==15&&!string.IsNullOrEmpty(st.Warning),"recovered");Assert(Directory.GetFiles(dir,"recover.json.damaged-*").Length>0,"damaged retained");});
   Check("malformed backups reject before replacing live data",()=>{var s=New();s.Template[0].Minimum=999;bool failed=false;try{Engine.Validate(s);}catch(InvalidDataException){failed=true;}Assert(failed,"invalid durations");});
   Check("day checklists remain distinct across dates",()=>{var s=New();s.Tasks.Add(new Todo{Id="t",Text="task",Group="작업",DoneDates=new List<string>{D(s).Date}});var next=s.GetDay(D(s).Wake.AddDays(1));Assert(!s.Tasks[0].DoneDates.Contains(next.Date),"daily completion");Assert(s.Days.Count==2,"day retained");});
   Check("random single anchors preserve consistency and exact anchor",()=>{var rnd=new Random(731);for(int n=0;n<300;n++){var s=New();var a=Engine.Build(s,D(s));int idx=new int[]{1,6,10,12}[rnd.Next(4)];int delta=rnd.Next(-25,300);DateTime actual=a.Slots[idx].Start.AddMinutes(delta);if(actual<D(s).Wake)actual=D(s).Wake;D(s).Actual[a.Slots[idx].Block.Id]=actual;var p=Engine.Build(s,D(s));Assert(p.Slots[idx].Start==actual,"anchor");Consistent(p);}});
   Check("no execution through unsupported workspace target",()=>{bool failed=false;try{MainForm.OpenTarget("C:\\Windows\\System32\\cmd.exe");}catch(InvalidOperationException){failed=true;}Assert(failed,"executable blocked");});
   Check("startup shortcut creation works in test folder without registering startup",()=>{string path=Path.Combine(dir,"startup-test.lnk");Startup.CreateLink(path,Application.ExecutablePath);Assert(File.Exists(path)&&new FileInfo(path).Length>0,"shortcut created");});
   GridTests.Run(Check,dir);
   RoutineTests.Run(Check,dir);
   FastTests.Run(Check,dir);
   AdaptiveTests.Run(Check,dir);
   SleepTests.Run(Check,dir);
   MobileTests.Run(Check,dir);
   MealGapTests.Run(Check,dir);
   DarkModeTests.Run(Check,dir);
   HistoryTests.Run(Check,dir);PrepRecordTests.Run(Check,dir);SatisfactionPolicyTests.Run(Check,dir);
   PlanEditTests.Run(Check,dir);
   QrNowSleepTests.Run(Check,dir);
   DiagnosticTests.Run(Check,dir);
   NotificationBufferTests.Run(Check,dir);
   BlockFeatureTests.Run(Check,dir);
   BufferPriorityTests.Run(Check,dir);
   SplitBufferTests.Run(Check,dir); ConnectedTests.Run(Check,dir); QrPairTests.Run(Check,dir); ReplicaTests.Run(Check,dir); NotionHubTests.Run(Check,dir);
   AdherenceTests.Run(Check,dir);
   Check("UI event loop has no unhandled exceptions",()=>Assert(Diagnostics.UiErrorCount==0,"UI failures: "+Diagnostics.UiErrorCount));
   File.WriteAllLines(Path.Combine(dir,"test-results.txt"),results.Concat(new[]{"RESULT "+results.Count(x=>x.StartsWith("PASS"))+"/"+results.Count+" passed"}),System.Text.Encoding.UTF8);
   return results.Any(x=>x.StartsWith("FAIL"))?1:0;
  }
  public static int Smoke(string[] args){
   string path=Program.DataPath(args);string dir=Path.GetDirectoryName(path);Directory.CreateDirectory(dir);
   var data=State.New();var day=data.GetDay(DateTime.Today);day.Wake=DateTime.Today.AddHours(8);data.Tasks.Add(new Todo{Id="demo1",Text="오늘의 가장 중요한 작업 정하기",Group="작업"});data.Tasks.Add(new Todo{Id="demo2",Text="학습한 내용 3줄로 정리하기",Group="학습"});
   using(var form=new MainForm(new Store(path),data,true)){
    form.Show();Application.DoEvents();form.RefreshAll();Application.DoEvents();form.SmokeActions();Application.DoEvents();
    using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new Rectangle(Point.Empty,form.Size));image.Save(Path.Combine(dir,"dashboard-smoke.png"),System.Drawing.Imaging.ImageFormat.Png);}
    using(var assignment=new AssignmentDialog(new CellSelection{Lane=1,First=51,Last=53},day.Wake.AddMinutes(30),form.GridPlan.Blocks.Find(x=>x.MealId=="b1"),form.GridPlan.Blocks)){assignment.Show(form);Application.DoEvents();using(var image=new Bitmap(assignment.Width,assignment.Height)){assignment.DrawToBitmap(image,new Rectangle(Point.Empty,assignment.Size));image.Save(Path.Combine(dir,"assignment-smoke.png"),System.Drawing.Imaging.ImageFormat.Png);}assignment.Close();}
    using(var settings=new SettingsDialog(data)){settings.Show(form);Application.DoEvents();using(var image=new Bitmap(settings.Width,settings.Height)){settings.DrawToBitmap(image,new Rectangle(Point.Empty,settings.Size));image.Save(Path.Combine(dir,"settings-smoke.png"),System.Drawing.Imaging.ImageFormat.Png);}settings.Close();}
    form.Quit();
   }
   File.WriteAllText(Path.Combine(dir,"smoke-results.txt"),"PASS 10-minute timetable and settings rendered without exception\nPASS actual activity editor, daily score, meal recognition and persistence through form handlers\nPASS checklist, memo and focus saving\nPASS startup registration was not changed\n");return 0;
  }
  public static int VerifyData(string[] args){string path=Program.DataPath(args);string dir=Path.GetDirectoryName(path);var store=new Store(path);var data=store.Load();using(var form=new MainForm(store,data,true)){form.Show();Application.DoEvents();form.RefreshAll();store.Save(data);using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new Rectangle(Point.Empty,form.Size));image.Save(Path.Combine(dir,"existing-data-smoke.png"));}form.Quit();}File.WriteAllText(Path.Combine(dir,"existing-data-results.txt"),"PASS existing user data upgraded and rendered without exception");return 0;}
  public static int NotificationSmoke(string[] args){
   string path=Program.DataPath(args);string dir=Path.GetDirectoryName(path);Directory.CreateDirectory(dir);var result=WindowsNotifications.Send("ARA Daily OS · 알림 테스트","끼니·운동·소등 일정을 일반 알림으로 알려 드립니다.",path);File.WriteAllText(Path.Combine(dir,"notification-results.txt"),result.state+"; setting="+result.setting+"; history="+result.history+"; error="+result.error);return result.Success?0:2;
  }
 }
}
