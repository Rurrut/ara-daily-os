using System;using System.IO;using System.Linq;using System.Text;using System.Diagnostics;using System.Collections.Generic;using System.Reflection;
namespace Ara {
 // Synchronous local adapter for WinForms. There is deliberately no C# calculation fallback.
 public static class SharedEngine {
  static readonly object Gate=new object();static Process child;static long sequence;static string identity;static string lastError="";
  static SharedEngine(){AppDomain.CurrentDomain.ProcessExit+=(s,e)=>Stop();}
  public static string Json<T>(T value){return Encoding.UTF8.GetString(PhoneCodec.Json(value));}
  static System.Web.Script.Serialization.JavaScriptSerializer Wire(){return new System.Web.Script.Serialization.JavaScriptSerializer{MaxJsonLength=64*1024*1024,RecursionLimit=128};}
  public static string DayState(State state,Day day){var slice=new State();CopyFields(state,slice);slice.Days=new List<Day>{day};return Json(slice);}
  static string Resource(string name){using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream(name)){if(stream==null)throw new IOException("공통 엔진 리소스가 없습니다: "+name);using(var reader=new StreamReader(stream,Encoding.UTF8))return reader.ReadToEnd();}}
  static void Start(){
   if(child!=null&&!child.HasExited)return;
   string runtime=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"runtime"),node=Path.Combine(runtime,"node.exe");
   if(!File.Exists(node))throw new IOException("공통 계산 실행 파일이 없습니다. Aiary ZIP 전체를 압축 해제해 runtime 폴더를 함께 두세요.");
   if(PhoneCodec.Hash(File.ReadAllBytes(node))!=Resource("Aiary.RuntimeHash.txt").Trim())throw new IOException("공통 실행 파일 구성이 다릅니다. 같은 배포 ZIP의 runtime 폴더를 함께 사용해 주세요.");
   string core=Resource("Aiary.Engine.js"),host=Resource("Aiary.EngineHost.cjs"),hash=PhoneCodec.Hash(Encoding.UTF8.GetBytes(core));
   string folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Aiary","engine",hash+"-"+PhoneCodec.Hash(Encoding.UTF8.GetBytes(host)).Substring(0,12));
   Directory.CreateDirectory(folder);string engine=Path.Combine(folder,"schedule.cjs"),entry=Path.Combine(folder,"host.cjs");
   WriteCache(engine,core);WriteCache(entry,host);
   var info=new ProcessStartInfo(node,"--no-warnings \""+entry+"\" \""+engine+"\""){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8,WorkingDirectory=runtime};
   info.EnvironmentVariables.Remove("NODE_OPTIONS");info.EnvironmentVariables.Remove("NODE_PATH");
   lastError="";child=new Process{StartInfo=info};child.ErrorDataReceived+=(s,e)=>{if(e.Data!=null)lastError=(lastError+e.Data+"\n").Substring(Math.Max(0,lastError.Length+e.Data.Length+1-4000));};if(!child.Start())throw new IOException("공통 계산 엔진을 시작하지 못했습니다.");child.BeginErrorReadLine();child.StandardInput.AutoFlush=true;identity=hash;
  }
  static void WriteCache(string path,string text){if(File.Exists(path)&&File.ReadAllText(path,Encoding.UTF8)==text)return;string temporary=path+"."+Process.GetCurrentProcess().Id+".tmp";File.WriteAllText(temporary,text,new UTF8Encoding(false));if(File.Exists(path))File.Replace(temporary,path,null);else try{File.Move(temporary,path);}catch(IOException){if(File.Exists(path))File.Replace(temporary,path,null);else throw;}}
  public static void Stop(){lock(Gate){if(child==null)return;try{child.StandardInput.Close();if(!child.WaitForExit(1500))child.Kill();}catch{}finally{child.Dispose();child=null;}}}
  public static Dictionary<string,object> Call(string op,Dictionary<string,object> args=null){lock(Gate){Start();var q=args??new Dictionary<string,object>();q["op"]=op;q["id"]=++sequence;try{
   var ascii=new StringBuilder();foreach(char c in Wire().Serialize(q)){if(c>127)ascii.Append("\\u"+((int)c).ToString("x4"));else ascii.Append(c);}child.StandardInput.WriteLine(ascii.ToString());var pending=child.StandardOutput.ReadLineAsync();if(!pending.Wait(15000))throw new IOException("공통 엔진 응답 시간이 초과됐습니다.");string text=pending.Result;if(text==null||text.Length>64*1024*1024)throw new IOException("공통 엔진 응답을 읽지 못했습니다.");var response=Wire().Deserialize<Dictionary<string,object>>(text);if(!response.ContainsKey("id")||Convert.ToInt64(response["id"])!=sequence)throw new IOException("공통 엔진 응답 번호가 다릅니다.");if(!Convert.ToBoolean(response["ok"]))throw new InvalidOperationException(Convert.ToString(response["error"]));return JsonMap.Child(response,"result");
  }catch(InvalidOperationException){throw;}catch(Exception error){string detail=lastError;Stop();throw new IOException("공통 JS 엔진 통신 실패: "+error.Message+(detail.Length>0?" · "+detail:""),error);}}}
  public static string Identity{get{Call("identity");return identity;}}
  public static void CopyFields<T>(T source,T destination){foreach(var field in typeof(T).GetFields(BindingFlags.Public|BindingFlags.Instance))field.SetValue(destination,field.GetValue(source));}
  public static T Read<T>(object json){return PhoneCodec.Read<T>(Encoding.UTF8.GetBytes(Convert.ToString(json)));}
  public static Dictionary<string,object> Run(string op,State state,Day day=null,Dictionary<string,object> args=null,bool update=true){var q=args??new Dictionary<string,object>();bool sliced=day!=null;q["state"]=sliced?DayState(state,day):Json(state);if(day!=null)q["date"]=day.Date;var r=Call(op,q);if(update&&r.ContainsKey("state")){var next=Read<State>(r["state"]);for(int i=0;i<next.Days.Count;i++){var converted=next.Days[i];var keep=day!=null&&day.Date==converted.Date?day:state.Days.Find(old=>old.Date==converted.Date);if(keep!=null){CopyFields(converted,keep);next.Days[i]=keep;}}if(sliced){var combined=state.Days.ToList();foreach(var changed in next.Days){int index=combined.FindIndex(old=>old.Date==changed.Date);if(index<0)combined.Add(changed);else combined[index]=changed;}next.Days=combined;}CopyFields(next,state);}return r;}
  public static object Value(Dictionary<string,object> r){return r.ContainsKey("value")?r["value"]:null;}
  public static Dictionary<string,object> DayCall(string op,Day day,Dictionary<string,object> args=null,bool update=false){var q=args??new Dictionary<string,object>();q["day"]=Json(day);var r=Call(op,q);if(update&&r.ContainsKey("day"))CopyFields(Read<Day>(r["day"]),day);return r;}
  public static GridPlan Build(State s,Day d){return Read<GridPlan>(Run("build",s,d)["plan"]);}
  public static void EditList(List<CellBlock> list,DateTime date,string action,Dictionary<string,object> extra){extra["date"]=date.Date.ToString("yyyy-MM-dd");extra["list"]=Json(list);extra["action"]=action;var r=Call("list",extra);list.Clear();list.AddRange(Read<List<CellBlock>>(r["list"]));}
 }
}
