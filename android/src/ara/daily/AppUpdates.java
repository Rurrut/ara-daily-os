package ara.daily;
import android.app.Activity;
import android.content.*;
import android.content.pm.*;
import android.net.Uri;
import android.os.Build;
import android.provider.Settings;
import org.json.*;
import java.io.*;
import java.net.*;
import java.nio.charset.StandardCharsets;
import java.security.*;
import java.security.spec.X509EncodedKeySpec;
import java.util.*;
import java.util.concurrent.*;
import java.util.concurrent.atomic.AtomicBoolean;
import java.util.zip.*;

public final class AppUpdates {
 public static final int UI_VERSION=2026102001, NATIVE_VERSION=21;
 private static final ExecutorService IO=Executors.newSingleThreadExecutor();
 private static final AtomicBoolean BUSY=new AtomicBoolean();
 private static volatile String server="",token="",error="";
 private static volatile JSONObject manifest;
 private static volatile long lastCheck=0;
 private static volatile int activeUi=UI_VERSION;
 private AppUpdates(){}
 private static File cache(Context c){return new File(c.getFilesDir(),"ui-update.zip");}
 private static File meta(Context c){return new File(c.getFilesDir(),"ui-update.json");}
 public static File apk(Context c){return new File(c.getCacheDir(),"ara-update.apk");}
 private static byte[] read(InputStream in,int limit)throws IOException{try(InputStream stream=in;ByteArrayOutputStream out=new ByteArrayOutputStream()){byte[] bytes=new byte[8192];int n;while((n=stream.read(bytes))!=-1){if(out.size()+n>limit)throw new IOException("업데이트 파일이 너무 큽니다.");out.write(bytes,0,n);}return out.toByteArray();}}
 private static byte[] fetch(String path,int limit)throws Exception{
  if(!MainActivity.local(server))throw new IOException("같은 Wi-Fi의 PC 연결이 필요합니다.");HttpURLConnection connection=(HttpURLConnection)new URL(server+path).openConnection(java.net.Proxy.NO_PROXY);try{connection.setConnectTimeout(4000);connection.setReadTimeout(20000);connection.setInstanceFollowRedirects(false);connection.setRequestProperty("Authorization","Bearer "+token);if(connection.getResponseCode()!=200)throw new IOException("PC 업데이트 응답을 확인해 주세요.");return read(connection.getInputStream(),limit);}finally{connection.disconnect();}
 }
 private static String sha(byte[] bytes)throws Exception{StringBuilder out=new StringBuilder();for(byte b:MessageDigest.getInstance("SHA-256").digest(bytes))out.append(String.format(java.util.Locale.ROOT,"%02x",b&255));return out.toString();}
 private static void signed(Context c,JSONObject m,byte[] bytes)throws Exception{
  byte[] key=read(c.getAssets().open("update-public.der"),8192);UpdatePolicy.verify(key,android.util.Base64.decode(m.getString("uiSignature"),android.util.Base64.DEFAULT),bytes,m.getString("uiSha256"),m.getInt("uiVersion"),m.getInt("minNative"),m.getInt("maxNative"),NATIVE_VERSION);
 }
 private static Map<String,String> unpack(byte[] bytes)throws Exception{return UpdatePolicy.unpack(bytes);}
 private static void atomic(File file,byte[] bytes)throws IOException{File tmp=new File(file.getPath()+".tmp");try(FileOutputStream out=new FileOutputStream(tmp)){out.write(bytes);out.getFD().sync();}if(!tmp.renameTo(file)){tmp.delete();throw new IOException("업데이트 저장에 실패했습니다.");}}
 private static int cachedVersion(Context c){try{if(!cache(c).exists()||!meta(c).exists())return UI_VERSION;JSONObject m=new JSONObject(new String(read(new FileInputStream(meta(c)),32000),StandardCharsets.UTF_8));byte[] bytes=read(new FileInputStream(cache(c)),3*1024*1024);signed(c,m,bytes);return Math.max(UI_VERSION,m.getInt("uiVersion"));}catch(Exception e){return UI_VERSION;}}
 public static String cachedHtml(Context c){activeUi=UI_VERSION;try{if(c.getSharedPreferences("ara.updates",0).getInt("failedUi",0)==cachedVersion(c))return null;if(cachedVersion(c)<=UI_VERSION)return null;JSONObject m=new JSONObject(new String(read(new FileInputStream(meta(c)),32000),StandardCharsets.UTF_8));byte[] bytes=read(new FileInputStream(cache(c)),3*1024*1024);signed(c,m,bytes);Map<String,String> files=unpack(bytes);activeUi=m.getInt("uiVersion");return files.get("index.html").replace("<link rel=\"stylesheet\" href=\"mobile.css\">","<style>"+files.get("mobile.css")+"</style>").replace("<script src=\"offline-core.js\"></script>","<script>"+files.get("offline-core.js")+"</script>").replace("<script src=\"mobile.js\"></script>","<script>"+files.get("mobile.js")+"</script>");}catch(Exception e){error="화면 업데이트를 열지 못해 기본 화면을 사용합니다.";return null;}}
 public static void failUi(Context c){c.getSharedPreferences("ara.updates",0).edit().putInt("failedUi",cachedVersion(c)).apply();error="새 화면을 시작하지 못해 기본 화면으로 복구했습니다.";}
 public static void configure(Context c,String pc,String bearer){server=pc;token=bearer;check(c,false);}
 public static void check(Context c,boolean force){if(!force&&System.currentTimeMillis()-lastCheck<60000||!BUSY.compareAndSet(false,true))return;lastCheck=System.currentTimeMillis();Context app=c.getApplicationContext();IO.execute(()->{try{if(token.isEmpty())throw new IOException("PC와 먼저 연결해 주세요.");JSONObject m=new JSONObject(new String(fetch("/api/update",32000),StandardCharsets.UTF_8));manifest=m;
   if(m.getInt("uiVersion")>cachedVersion(app)&&m.getInt("minNative")<=NATIVE_VERSION&&m.getInt("maxNative")>=NATIVE_VERSION){byte[] bytes=fetch("/api/ui-bundle",3*1024*1024);signed(app,m,bytes);unpack(bytes);atomic(cache(app),bytes);atomic(meta(app),m.toString().getBytes(StandardCharsets.UTF_8));}error="";
  }catch(Exception e){error=e.getMessage()==null?"업데이트 연결을 확인해 주세요.":e.getMessage();}finally{BUSY.set(false);}});}
 public static String status(Context c){try{JSONObject result=new JSONObject();result.put("installed",NATIVE_VERSION);result.put("installedName","2.7");result.put("builtinUi",UI_VERSION);result.put("cachedUi",cachedVersion(c));result.put("busy",BUSY.get());result.put("error",error);JSONObject m=manifest;result.put("available",m==null?NATIVE_VERSION:m.optInt("versionCode",NATIVE_VERSION));result.put("availableName",m==null?"2.7":m.optString("versionName","2.7"));result.put("uiReady",cachedVersion(c)>activeUi);return result.toString();}catch(Exception e){return "{}";}}
 public static void install(Activity activity){
  if(Build.VERSION.SDK_INT>=26&&!activity.getPackageManager().canRequestPackageInstalls()){activity.startActivity(new Intent(Settings.ACTION_MANAGE_UNKNOWN_APP_SOURCES,Uri.parse("package:"+activity.getPackageName())));return;}
  if(!BUSY.compareAndSet(false,true))return;
  IO.execute(()->{try{JSONObject m=manifest;if(m==null||m.getInt("versionCode")<=NATIVE_VERSION)throw new IOException("PC에 새 APK 버전이 없습니다.");byte[] bytes=fetch("/api/app-package",32*1024*1024);if(!sha(bytes).equals(m.getString("apkSha256")))throw new IOException("APK 파일이 손상됐습니다.");atomic(apk(activity),bytes);
   PackageManager pm=activity.getPackageManager();PackageInfo next=pm.getPackageArchiveInfo(apk(activity).getPath(),PackageManager.GET_SIGNATURES),current=pm.getPackageInfo(activity.getPackageName(),PackageManager.GET_SIGNATURES);
   if(next==null||!activity.getPackageName().equals(next.packageName)||next.versionCode<=current.versionCode||next.versionCode!=m.getInt("versionCode")||next.signatures==null||next.signatures.length!=1||current.signatures==null||current.signatures.length!=1||!Arrays.equals(next.signatures[0].toByteArray(),current.signatures[0].toByteArray())){apk(activity).delete();throw new IOException("현재 ARA 앱과 같은 서명의 새 APK가 아닙니다.");}
   activity.runOnUiThread(()->{try{Intent intent=new Intent(Intent.ACTION_VIEW);intent.setDataAndType(Uri.parse("content://ara.daily.updates/apk"),"application/vnd.android.package-archive");intent.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION);activity.startActivity(intent);}catch(Exception e){error="Android 설치 화면을 열지 못했습니다.";}});error="";
  }catch(Exception e){error=e.getMessage()==null?"APK 업데이트 실패":e.getMessage();}finally{BUSY.set(false);}});
 }
}
