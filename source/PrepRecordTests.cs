using System;using System.IO;using System.Linq;using System.Windows.Forms;
namespace Ara {
 public static class PrepRecordTests {
  static void Assert(bool yes,string text){if(!yes)throw new Exception(text);}
  static PhoneDay Command(MainForm f,string op,int first,int last,string source,string category){return f.PhoneApply(new PhoneCommand{op=op,lane=1,first=first,last=last,sourceId=source,category=category,date=f.Day.Date,baseRevision=f.PhoneSnapshot(f.Day.Date).revision,requestId=Guid.NewGuid().ToString("N")});}
  public static void Run(Action<string,Action> check,string dir){
   foreach(string operation in new[]{"copy","category"}){string op=operation;check("prep record: early meal permits "+op+" without moving history or facts",()=>{var s=State.New();var d=s.GetDay(DateTime.Today);d.Wake=DateTime.Today.AddHours(8);GridEngine.Ensure(s,d);using(var f=new MainForm(new Store(Path.Combine(dir,"prep-"+op+".json")),s,true)){f.Show();Application.DoEvents();try{
    Command(f,"copy",51,53,"plan-b1",null);Command(f,"copy",63,66,"plan-b4",null);Command(f,"copy",69,71,"plan-b6",null);
    var before=f.PhoneSnapshot(f.Day.Date);string facts=JsonMap.Write(before.actual),plans=JsonMap.Write(before.planned);var after=Command(f,op,68,68,"plan-prep-b6","끼니 준비");
    var prep=after.actual.Single(x=>x.start==68);Assert(prep.category=="끼니 준비"&&string.IsNullOrEmpty(prep.mealId),"prep stays non-meal");Assert(after.actual.Count==before.actual.Count+1,"one new fact");Assert(JsonMap.Write(after.actual.Where(x=>x.id!=prep.id).ToList())==facts,"existing facts protected");Assert(JsonMap.Write(after.planned)==plans,"upper history and future unchanged");Assert(after.sleep==before.sleep,"bedtime fixed");
   }finally{f.Quit();}}});}
  }
 }
}
