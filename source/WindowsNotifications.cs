using System;
using System.IO;
using System.Threading;
using System.Diagnostics;
using System.Runtime.Serialization;
using System.Runtime.InteropServices;
using System.Windows.Forms;
namespace Ara {
 [DataContract] public class ToastPayload {[DataMember] public string title,body,app,status;}
 [DataContract] public class ToastResult {[DataMember] public string state,setting,error,time;[DataMember] public int history;public bool Success{get{return state=="submitted";}}}
 public static class WindowsNotifications {
  public const string AppId="ARA.DailyOS";
  [DllImport("shell32.dll",CharSet=CharSet.Unicode)] static extern int SetCurrentProcessExplicitAppUserModelID(string id);
  public static void Identity(){try{SetCurrentProcessExplicitAppUserModelID(AppId);}catch{}}
  public static string StatusPath(string dataPath){return Path.Combine(Path.GetDirectoryName(dataPath),"notification-status.json");}
  public static ToastResult Last(string dataPath){try{return PhoneCodec.Read<ToastResult>(System.Text.Encoding.UTF8.GetBytes(File.ReadAllText(StatusPath(dataPath),System.Text.Encoding.UTF8)));}catch{return new ToastResult{state="not-tested",setting="아직 알림 전송 기록이 없습니다."};}}
  public static ToastResult Send(string title,string body,string dataPath){string file=null,resultFile=null;try{string folder=Path.Combine(Path.GetDirectoryName(dataPath),"notification-jobs");Directory.CreateDirectory(folder);file=Path.Combine(folder,Guid.NewGuid().ToString("N")+".json");resultFile=file+".result";File.WriteAllBytes(file,PhoneCodec.Json(new ToastPayload{title=title,body=body,app=Application.ExecutablePath,status=resultFile}));string script=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Windows 알림.ps1");if(!File.Exists(script))throw new FileNotFoundException("Windows 알림 파일을 찾지 못했습니다.");string shell=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"System32","WindowsPowerShell","v1.0","powershell.exe");using(var process=Process.Start(new ProcessStartInfo(shell,"-NoProfile -ExecutionPolicy Bypass -File \""+script+"\" -PayloadPath \""+file+"\""){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden})){if(!process.WaitForExit(20000)){try{process.Kill();}catch{}throw new TimeoutException("Windows 알림 전송을 확인하지 못했습니다.");}}var result=PhoneCodec.Read<ToastResult>(System.Text.Encoding.UTF8.GetBytes(File.ReadAllText(resultFile,System.Text.Encoding.UTF8)));File.WriteAllBytes(StatusPath(dataPath),PhoneCodec.Json(result));return result;}catch(Exception e){Diagnostics.Error("windows-notification",e);return new ToastResult{state="failed",error=e.Message};}finally{if(file!=null)try{File.Delete(file);}catch{}if(resultFile!=null)try{File.Delete(resultFile);}catch{}}}
  public static void Queue(string title,string body,string dataPath,Action<ToastResult> completed){ThreadPool.QueueUserWorkItem(_=>{var result=Send(title,body,dataPath);Diagnostics.Action("windows-notification",result.state+"; setting="+result.setting+"; history="+result.history);if(completed!=null)completed(result);});}
  public static void StatusDialog(IWin32Window owner,string dataPath){var last=Last(dataPath);string text="Windows 알림 상태: "+last.state+"\n"+(last.setting??"")+"\n"+(last.error??"")+"\n\n알림 테스트 후 Windows 알림센터(Win+N)도 확인해 주세요.\nWindows의 방해 금지 또는 ARA 알림 설정에 따라 배너가 숨겨질 수 있습니다.";using(var f=new Form{Text="ARA · Windows 알림 상태",Width=580,Height=300,StartPosition=FormStartPosition.CenterParent,BackColor=Theme.Bg}){var label=Theme.Label(text);label.Padding=new Padding(18);f.Controls.Add(label);var row=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=55};row.Controls.Add(Theme.Button("Windows 알림 설정",(s,e)=>Process.Start(new ProcessStartInfo("ms-settings:notifications"){UseShellExecute=true})));row.Controls.Add(Theme.Button("닫기",(s,e)=>f.Close()));f.Controls.Add(row);Theme.Apply(f);f.ShowDialog(owner);}}
 }
}
