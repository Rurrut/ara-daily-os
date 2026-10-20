using System;
using System.Linq;
using System.IO;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Threading;
using System.Drawing;
using System.Windows.Forms;
using System.Security.Cryptography;
using System.Runtime.Serialization;
using System.Collections.Generic;

namespace Ara {
 [DataContract] public class PairingConfig {[DataMember] public string token;[DataMember] public bool enabled;}
 [DataContract] public class PairRequest {[DataMember] public string code;}
 [DataContract] public class PairReply {[DataMember] public string token,date;}
 [DataContract] public class ApiError {[DataMember] public string error;}
 public class HttpReply {public int status=200;public string type="application/json; charset=utf-8";public byte[] body;}
 public class MobileServer:IDisposable {
  MainForm owner;List<TcpListener> listeners=new List<TcpListener>();volatile bool stopped;string configPath,root;
  Dictionary<string,Tuple<DateTime,int>> failures=new Dictionary<string,Tuple<DateTime,int>>();
  SemaphoreSlim connections=new SemaphoreSlim(16);
  public string Token{get;private set;}public string Code{get;private set;}public string Address{get;private set;}public int Port{get;private set;}public bool Running{get{return listeners.Count>0;}}
  public bool WasEnabled{get;private set;}
  public static bool Private(IPAddress ip){var b=ip.GetAddressBytes();return b.Length==4&&(b[0]==127||b[0]==10||b[0]==192&&b[1]==168||b[0]==172&&b[1]>=16&&b[1]<=31);}
  public static string FindLanAddress(){
   foreach(var network in NetworkInterface.GetAllNetworkInterfaces().Where(x=>x.OperationalStatus==OperationalStatus.Up&&x.NetworkInterfaceType!=NetworkInterfaceType.Loopback)){
    var p=network.GetIPProperties();if(!p.GatewayAddresses.Any(x=>x.Address.AddressFamily==AddressFamily.InterNetwork&&x.Address.ToString()!="0.0.0.0"))continue;
    var ip=p.UnicastAddresses.Select(x=>x.Address).FirstOrDefault(x=>x.AddressFamily==AddressFamily.InterNetwork&&Private(x)&&x.GetAddressBytes()[0]!=127);if(ip!=null)return ip.ToString();
   }return null;
  }
  public MobileServer(MainForm form,int port=8765){owner=form;Port=port;root=Path.GetDirectoryName(form.Store.PathName);if(Path.GetFileName(root).Equals("data",StringComparison.OrdinalIgnoreCase))root=Path.GetDirectoryName(root);configPath=Path.Combine(Path.GetDirectoryName(form.Store.PathName),"mobile-pairing.json");
   PairingConfig config=null;try{if(File.Exists(configPath))config=PhoneCodec.Read<PairingConfig>(File.ReadAllBytes(configPath));}catch(Exception e){Diagnostics.Error("pairing-config",e);}if(config==null||config.token==null||config.token.Length!=64){byte[] bytes=new byte[32];using(var rng=RandomNumberGenerator.Create())rng.GetBytes(bytes);config=new PairingConfig{token=BitConverter.ToString(bytes).Replace("-","").ToLowerInvariant()};}
   Token=config.token;WasEnabled=config.enabled;byte[] random=new byte[4];using(var rng=RandomNumberGenerator.Create())rng.GetBytes(random);Code=(BitConverter.ToUInt32(random,0)%100000000).ToString("00000000");
  }
  public void Start(bool lan){if(Running)return;stopped=false;try{StartListener(IPAddress.Loopback);Address=FindLanAddress();if(lan&&Address!=null)StartListener(IPAddress.Parse(Address));Directory.CreateDirectory(Path.GetDirectoryName(configPath));File.WriteAllBytes(configPath,PhoneCodec.Json(new PairingConfig{token=Token,enabled=lan}));WasEnabled=lan;Diagnostics.Action("mobile-server-start","port="+Port+"; LAN="+(lan&&Address!=null));}catch{Dispose();throw;}}
  public void Disable(){Dispose();File.WriteAllBytes(configPath,PhoneCodec.Json(new PairingConfig{token=Token,enabled=false}));WasEnabled=false;Diagnostics.Action("mobile-server-disabled");}
  void StartListener(IPAddress ip){var listener=new TcpListener(ip,Port);listener.Start(16);listeners.Add(listener);var thread=new Thread(()=>Accept(listener)){IsBackground=true};thread.Start();}
  void Accept(TcpListener listener){while(!stopped){try{var client=listener.AcceptTcpClient();if(!connections.Wait(0)){client.Close();continue;}ThreadPool.QueueUserWorkItem(s=>{try{Serve(client);}finally{connections.Release();}});}catch(SocketException){if(!stopped)Diagnostics.Action("mobile-listener-interrupted");break;}catch(ObjectDisposedException){break;}}}
  static bool Equal(string a,string b){if(a==null||b==null)return false;using(var hash=SHA256.Create()){byte[] x=hash.ComputeHash(Encoding.UTF8.GetBytes(a)),y=hash.ComputeHash(Encoding.UTF8.GetBytes(b));int delta=0;for(int i=0;i<x.Length;i++)delta|=x[i]^y[i];return delta==0;}}
  bool AllowedOrigin(string origin,string host){return string.IsNullOrEmpty(origin)||origin=="http://"+host||origin=="https://ara.local";}
  public HttpReply Dispatch(string method,string path,Dictionary<string,string> headers,byte[] body,string clientIp){
   string host;headers.TryGetValue("host",out host);if(host!="127.0.0.1:"+Port&&host!=Address+":"+Port&&host!="localhost:"+Port)return Error(400,"연결 주소를 확인해 주세요.");
   string origin;headers.TryGetValue("origin",out origin);if(!AllowedOrigin(origin,host))return Error(403,"허용되지 않은 연결입니다.");
   if(method=="OPTIONS")return new HttpReply{status=204,body=new byte[0]};
   string route=path.Split('?')[0];if(method=="POST"&&route=="/api/pair"){
    lock(failures){Tuple<DateTime,int> f;if(failures.TryGetValue(clientIp,out f)&&DateTime.UtcNow-f.Item1<TimeSpan.FromMinutes(1)&&f.Item2>=5)return Error(429,"연결 코드 입력을 잠시 쉬고 다시 시도해 주세요.");}
    var request=PhoneCodec.Read<PairRequest>(body);if(!Equal(request.code,Code)){lock(failures){Tuple<DateTime,int> f;if(!failures.TryGetValue(clientIp,out f)||DateTime.UtcNow-f.Item1>=TimeSpan.FromMinutes(1))f=Tuple.Create(DateTime.UtcNow,0);failures[clientIp]=Tuple.Create(f.Item1,f.Item2+1);}Diagnostics.Action("mobile-pair-denied");return Error(401,"PC의 연결 코드를 확인해 주세요.");}
    Diagnostics.Action("mobile-paired");return Json(new PairReply{token=Token,date=OnUi(()=>owner.Day.Date)});
   }
   if(route.StartsWith("/api/")){
    string auth;headers.TryGetValue("authorization",out auth);if(!Equal(auth,"Bearer "+Token))return Error(401,"PC와 다시 연결해 주세요.");
    try{
     if(method=="GET"&&(route=="/api/update"||route=="/api/ui-bundle"||route=="/api/app-package")){string name=route=="/api/update"?"mobile-update.json":route=="/api/ui-bundle"?"mobile-update.zip":"ARA-Daily-OS.apk";string filePath=Path.Combine(root,name);if(!File.Exists(filePath))return Error(404,"업데이트 파일이 없습니다.");return new HttpReply{body=File.ReadAllBytes(filePath),type=route=="/api/update"?"application/json; charset=utf-8":"application/octet-stream"};}
     if(method=="POST"&&route=="/api/notion-hub"){return new HttpReply{body=Encoding.UTF8.GetBytes(JsonMap.Write(owner.Hub.Request(JsonMap.Read(Encoding.UTF8.GetString(body)))))};}
     if(method=="POST"&&route=="/api/notion"){var input=JsonMap.Codec().Deserialize<NotionInput>(Encoding.UTF8.GetString(body));var result=owner.NotionRequest(input);return new HttpReply{body=Encoding.UTF8.GetBytes(JsonMap.Write(result))};}
     if(method=="GET"&&route=="/api/replica"){string date=Uri.UnescapeDataString(path.Substring(path.IndexOf("date=",StringComparison.Ordinal)+5).Split('&')[0]);return Json(OnUi(()=>owner.ReplicaSnapshot(date)));}
     if(method=="POST"&&route=="/api/replica"){var input=PhoneCodec.Read<ReplicaInput>(body);return Json(OnUi(()=>owner.ReplicaApply(input)));}
     if(method=="GET"&&route=="/api/reminders")return Json(OnUi(()=>ScheduleNotifications.Feed(owner.Data,DateTime.Now)));
     if(method=="GET"&&route=="/api/day"){string key=Uri.UnescapeDataString(path.Substring(path.IndexOf("date=",StringComparison.Ordinal)+5).Split('&')[0]);return Json(OnUi(()=>owner.PhoneSnapshot(key)));}
     if(method=="POST"&&route=="/api/command"){var command=PhoneCodec.Read<PhoneCommand>(body);return Json(OnUi(()=>owner.PhoneApply(command)));}
     return Error(404,"찾을 수 없는 요청입니다.");
    }catch(SyncConflict e){return Error(409,e.Message);}catch(Exception e){Diagnostics.Error("mobile-request",e);return Error(422,e.Message);}
   }
   if(method!="GET")return Error(405,"허용되지 않은 요청입니다.");
   if(route=="/connect-qr.png"){string url=PhoneQr.Url(this);return url==null?Error(503,"PC의 공유기 연결을 확인해 주세요."):new HttpReply{body=PhoneQr.Png(url),type="image/png"};}
   string file=null,type=null;
   if(route=="/"||route=="/index.html"){file=Path.Combine(root,"mobile","index.html");type="text/html; charset=utf-8";}
   if(route=="/mobile.css"){file=Path.Combine(root,"mobile","mobile.css");type="text/css; charset=utf-8";}
   if(route=="/offline-core.js"){file=Path.Combine(root,"mobile","offline-core.js");type="text/javascript; charset=utf-8";}
   if(route=="/mobile.js"){file=Path.Combine(root,"mobile","mobile.js");type="text/javascript; charset=utf-8";}
   if(route=="/download"){file=Path.Combine(root,"mobile","download.html");type="text/html; charset=utf-8";}
   if(route=="/ARA-Daily-OS.apk"){file=Path.Combine(root,"ARA-Daily-OS.apk");type="application/vnd.android.package-archive";}
   if(file==null||!File.Exists(file))return Error(404,"파일을 찾지 못했습니다.");return new HttpReply{body=File.ReadAllBytes(file),type=type};
  }
  T OnUi<T>(Func<T> action){if(owner.InvokeRequired){Exception error=null;T result=default(T);owner.Invoke(new Action(()=>{try{result=action();}catch(Exception e){error=e;}}));if(error!=null)throw error;return result;}return action();}
  static HttpReply Json<T>(T result){return new HttpReply{body=PhoneCodec.Json(result)};}
  static HttpReply Error(int status,string message){return new HttpReply{status=status,body=PhoneCodec.Json(new ApiError{error=message})};}
  void Serve(TcpClient client){using(client){try{client.ReceiveTimeout=5000;client.SendTimeout=5000;var remote=((IPEndPoint)client.Client.RemoteEndPoint).Address;if(!Private(remote))return;var stream=client.GetStream();var raw=new MemoryStream();int state=0;
    while(state<4){int b=stream.ReadByte();if(b<0)return;raw.WriteByte((byte)b);if(raw.Length>16384)return;state=(state==0&&b==13||state==2&&b==13)?state+1:(state==1&&b==10||state==3&&b==10)?state+1:0;}
    string[] lines=Encoding.ASCII.GetString(raw.ToArray()).Split(new[]{"\r\n"},StringSplitOptions.None);var first=lines[0].Split(' ');if(first.Length!=3)return;
    var headers=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);foreach(string line in lines.Skip(1)){int colon=line.IndexOf(':');if(colon>0)headers[line.Substring(0,colon).Trim()]=line.Substring(colon+1).Trim();}
    int length=0;string value;if(headers.TryGetValue("content-length",out value)&&(!int.TryParse(value,out length)||length<0||length>4*1024*1024))return;if(headers.ContainsKey("transfer-encoding"))return;
    byte[] body=new byte[length];int read=0;while(read<length){int n=stream.Read(body,read,length-read);if(n==0)return;read+=n;}
    HttpReply response;try{response=Dispatch(first[0],first[1],headers,body,remote.ToString());}catch(Exception e){Diagnostics.Error("mobile-http",e);response=Error(400,"요청 내용을 확인해 주세요.");}
    string origin;headers.TryGetValue("origin",out origin);string host;headers.TryGetValue("host",out host);string cors=!string.IsNullOrEmpty(origin)&&AllowedOrigin(origin,host)?"Access-Control-Allow-Origin: "+origin+"\r\nVary: Origin\r\n":"";
    string head="HTTP/1.1 "+response.status+" Response\r\nContent-Type: "+response.type+"\r\nContent-Length: "+response.body.Length+"\r\nConnection: close\r\nCache-Control: no-store\r\nX-Content-Type-Options: nosniff\r\n"+cors+"Access-Control-Allow-Methods: GET, POST, OPTIONS\r\nAccess-Control-Allow-Headers: Authorization, Content-Type\r\n\r\n";byte[] bytes=Encoding.ASCII.GetBytes(head);stream.Write(bytes,0,bytes.Length);stream.Write(response.body,0,response.body.Length);
   }catch(IOException){}catch(Exception e){if(!stopped)Diagnostics.Error("mobile-connection",e);}}}
  public void Dispose(){stopped=true;foreach(var listener in listeners)listener.Stop();listeners.Clear();}
 }
 public partial class MainForm {
  public MobileServer PhoneServer;
  public void StartPhoneServer(bool lan){if(PhoneServer==null)PhoneServer=new MobileServer(this);PhoneServer.Start(lan);}
  public void PhoneConnection(){try{StartPhoneServer(true);using(var dialog=CreatePhoneDialog())dialog.ShowDialog(this);}catch(Exception e){Diagnostics.Error("mobile-start",e);MessageBox.Show(this,e.Message,"휴대폰 연결");}}
 }
}
