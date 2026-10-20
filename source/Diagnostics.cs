using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Windows.Forms;
using System.Collections.Generic;

namespace Ara {
 [DataContract] public class DiagnosticEntry {
  [DataMember] public string time,session,level,action,details,context,exception;
 }
 public static class Diagnostics {
  static readonly object gate=new object();
  static readonly Queue<string> recent=new Queue<string>();
  static string session=Guid.NewGuid().ToString("N");
  static bool reporting;
  public static int UiErrorCount;
  public static string DirectoryPath{get;private set;}
  public static string CurrentFile{get;private set;}
  public static Func<string> Context;
  public static Func<bool> Recover;
  public static void Initialize(string dataPath){
   string folder=Path.GetDirectoryName(Path.GetFullPath(dataPath));if(Path.GetFileName(folder).Equals("data",StringComparison.OrdinalIgnoreCase))folder=Path.GetDirectoryName(folder);
   DirectoryPath=Path.Combine(folder,"logs");
   try{Directory.CreateDirectory(DirectoryPath);}catch{DirectoryPath=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ARA Daily OS","logs");try{Directory.CreateDirectory(DirectoryPath);}catch{}}
   Action("startup","build=2026-10-16-notion-pages-2.1; OS="+Environment.OSVersion+"; CLR="+Environment.Version);
  }
  public static void Action(string name,string details=""){
   lock(gate){recent.Enqueue(DateTime.Now.ToString("HH:mm:ss.fff")+" "+name+" "+details);while(recent.Count>30)recent.Dequeue();Write("INFO",name,details,null);}
  }
  public static void Error(string name,Exception error){lock(gate){Write("ERROR",name,string.Join("\n",recent.ToArray()),error);}}
  static void Write(string level,string action,string details,Exception error){
   if(string.IsNullOrEmpty(DirectoryPath))return;
   try{
    string context="";try{if(Context!=null)context=Context();}catch(Exception e){context="Context unavailable: "+e.GetType().Name;}
    var entry=new DiagnosticEntry{time=DateTimeOffset.Now.ToString("o"),session=session,level=level,action=action,details=details,context=context,exception=error==null?null:error.ToString()};
    using(var memory=new MemoryStream()){new DataContractJsonSerializer(typeof(DiagnosticEntry)).WriteObject(memory,entry);
     string prefix="ara-"+DateTime.Now.ToString("yyyy-MM-dd");int index=0;string path;
     do{path=Path.Combine(DirectoryPath,prefix+(index==0?"":"-"+index.ToString("000"))+".jsonl");index++;}while(File.Exists(path)&&new FileInfo(path).Length>=2*1024*1024);
     File.AppendAllText(path,Encoding.UTF8.GetString(memory.ToArray())+Environment.NewLine,new UTF8Encoding(false));CurrentFile=path;
    }
    foreach(var old in new DirectoryInfo(DirectoryPath).GetFiles("ara-*.jsonl").OrderByDescending(x=>x.LastWriteTimeUtc).ThenByDescending(x=>x.Name).Skip(14))if(!old.FullName.Equals(CurrentFile,StringComparison.OrdinalIgnoreCase))old.Delete();
   }catch{ /* Logging must never raise a second exception during error handling. */ }
  }
  public static void ReportUiError(Exception error,bool showDialog){
   Error("unhandled-ui",error);if(reporting)return;reporting=true;
   try{bool recovered=false;try{if(Recover!=null)recovered=Recover();}catch(Exception recoveryError){Error("ui-recovery",recoveryError);}
    if(showDialog)MessageBox.Show("오류를 로그에 기록했습니다.\n"+(recovered?"마지막으로 저장한 상태로 화면을 복구했습니다.":"앱을 다시 실행해 주세요. 저장한 데이터는 유지됩니다.")+"\n\n로그 폴더: "+DirectoryPath,"ARA Daily OS · 오류 기록",MessageBoxButtons.OK,MessageBoxIcon.Warning);
   }finally{reporting=false;}
  }
 }
 public partial class MainForm {
  string DiagnosticContext(){
   if(Day==null)return "day=uninitialized";var selected=Board==null?null:Board.Selection;
   return "date="+Day.Date+"; wake="+Day.Wake.ToString("o")+"; selection="+(selected==null?"none":selected.Lane+":"+selected.First+"-"+selected.Last)+"; plan="+(GridPlan==null?0:GridPlan.Blocks.Count)+"; fixedPlan="+Day.PlanHistory.Count+"; records="+Day.Records.Count+"; total="+GridEngine.Total(Day)+"; meals="+string.Join(",",Day.Records.Where(x=>!string.IsNullOrEmpty(x.MealId)).Take(20).Select(x=>x.MealId+"@"+x.Start.ToString("o")+"/"+x.Count));
  }
  bool RecoverSavedState(){if(IsDisposed||Disposing)return false;string dateKey=Day==null?DateTime.Today.ToString("yyyy-MM-dd"):Day.Date;Data=Store.Load();Day=Data.GetDay(DateTime.Parse(dateKey));RefreshAll();Diagnostics.Action("ui-recovered");return true;}
 }
}

