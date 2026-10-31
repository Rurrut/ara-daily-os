package ara.daily;
import android.app.Activity;
import android.os.Bundle;
import android.os.Build;
import android.content.Intent;
import android.graphics.Color;
import android.graphics.Insets;
import android.net.Uri;
import android.view.View;
import android.view.WindowInsets;
import android.view.HapticFeedbackConstants;
import android.webkit.WebView;
import android.webkit.WebViewClient;
import android.webkit.WebResourceRequest;
import android.webkit.JavascriptInterface;
import org.json.JSONObject;
import java.io.*;
import java.net.*;
import java.nio.charset.StandardCharsets;
import java.util.concurrent.*;

public class MainActivity extends Activity {
 private WebView web; private NotionHub notionHub;
 private boolean uiHealthy=false,usingCached=false;
 private void showHtml(){try{AppUpdates.beginUiLoad();String html=AppUpdates.cachedHtml(this);usingCached=html!=null;uiHealthy=false;
  if(html==null)html=readAsset("index.html").replace("<link rel=\"stylesheet\" href=\"mobile.css\">","<style>"+readAsset("mobile.css")+"</style>").replace("<script src=\"offline-core.js\"></script>","<script>"+readAsset("offline-core.js")+"</script>").replace("<script src=\"mobile.js\"></script>","<script>"+readAsset("mobile.js")+"</script>");
  html=html.replace("<head>","<head><script>window.AiaryEdition="+JSONObject.quote(AppEdition.PREVIEW?"preview":"personal")+";window.addEventListener('error',function(e){try{if(window.AraNative)AraNative.reportUiError('JS: '+String(e.message||'실행 오류'))}catch(_){}});window.addEventListener('unhandledrejection',function(e){try{if(window.AraNative)AraNative.reportUiError('Promise: '+String(e.reason||'비동기 오류'))}catch(_){}});</script>");
  web.loadDataWithBaseURL("https://ara.local/",html,"text/html","UTF-8",null);
  if(usingCached)new android.os.Handler(android.os.Looper.getMainLooper()).postDelayed(()->{if(!uiHealthy&&web!=null&&!isFinishing()){AppUpdates.failUi(this);showHtml();}},8000);
 }catch(Exception e){web.loadData("<h2>Aiary 앱 파일을 읽지 못했습니다.</h2>","text/html","UTF-8");}}

 private String initial="",pendingQr=null;
 void reloadUpdatedUi(){showHtml();}
 private final ExecutorService io=Executors.newFixedThreadPool(2);
 static boolean local(String value){try{Uri u=Uri.parse(value);if(!"http".equals(u.getScheme())||u.getUserInfo()!=null)return false;String h=u.getHost();if(h==null||!h.matches("[0-9.]+"))return false;String[] parts=h.split("\\.");if(parts.length!=4)return false;int[] b=new int[4];for(int i=0;i<4;i++){b[i]=Integer.parseInt(parts[i]);if(b[i]<0||b[i]>255)return false;}return b[0]==10||b[0]==192&&b[1]==168||b[0]==172&&b[1]>=16&&b[1]<=31;}catch(Exception e){return false;}}
 private boolean initialDark(){return getPreferences(MODE_PRIVATE).getBoolean("dark",(getResources().getConfiguration().uiMode & android.content.res.Configuration.UI_MODE_NIGHT_MASK)==android.content.res.Configuration.UI_MODE_NIGHT_YES);}
 private void applyAppearance(boolean dark){int bg=dark?Color.rgb(16,24,21):Color.rgb(244,246,242);getWindow().setStatusBarColor(bg);getWindow().setNavigationBarColor(bg);getWindow().getDecorView().setBackgroundColor(bg);getWindow().getDecorView().setSystemUiVisibility(dark?0:View.SYSTEM_UI_FLAG_LIGHT_STATUS_BAR|View.SYSTEM_UI_FLAG_LIGHT_NAVIGATION_BAR);if(web!=null)web.setBackgroundColor(bg);}
 private String readAsset(String name)throws IOException{try(InputStream in=getAssets().open(name);ByteArrayOutputStream out=new ByteArrayOutputStream()){byte[] b=new byte[4096];int n;while((n=in.read(b))!=-1)out.write(b,0,n);return out.toString("UTF-8");}}
 private void intentAddress(Intent intent){Uri data=intent.getData();if(data!=null&&(AppEdition.PREVIEW?"aiarypreview":"aradaily").equals(data.getScheme())&&"connect".equals(data.getHost())){String pc=data.getQueryParameter("pc");if(pc!=null&&local(pc))initial=pc;}}
 @Override public void onCreate(Bundle bundle){super.onCreate(bundle);notionHub=NotionHub.get(this);intentAddress(getIntent());getWindow().setStatusBarColor(Color.rgb(244,246,242));getWindow().setNavigationBarColor(Color.rgb(248,250,246));getWindow().getDecorView().setSystemUiVisibility(View.SYSTEM_UI_FLAG_LIGHT_STATUS_BAR|View.SYSTEM_UI_FLAG_LIGHT_NAVIGATION_BAR);
  PhoneReminders.initialize(this);web=new WebView(this);applyAppearance(initialDark());web.getSettings().setJavaScriptEnabled(true);web.getSettings().setDomStorageEnabled(true);web.getSettings().setAllowFileAccess(false);web.getSettings().setAllowContentAccess(false);web.setOverScrollMode(View.OVER_SCROLL_NEVER);
  web.setOnApplyWindowInsetsListener((v,insets)->{if(Build.VERSION.SDK_INT>=30){Insets x=insets.getInsets(WindowInsets.Type.systemBars());v.setPadding(x.left,x.top,x.right,x.bottom);}else v.setPadding(insets.getSystemWindowInsetLeft(),insets.getSystemWindowInsetTop(),insets.getSystemWindowInsetRight(),insets.getSystemWindowInsetBottom());return insets;});
  web.setWebViewClient(new WebViewClient(){@Override public boolean shouldOverrideUrlLoading(WebView view,WebResourceRequest request){return !"ara.local".equals(request.getUrl().getHost());}});web.addJavascriptInterface(new Bridge(),"AraNative");setContentView(web);
  showHtml();
 }
 @Override protected void onNewIntent(Intent intent){super.onNewIntent(intent);setIntent(intent);intentAddress(intent);if(!initial.isEmpty())web.evaluateJavascript("window.AraMobile && window.AraMobile.openConnection("+JSONObject.quote(initial)+")",null);}
 @Override protected void onPause(){super.onPause();if(web!=null)web.onPause();}
 @Override protected void onResume(){super.onResume();PhoneReminders.initialize(this);PhoneReminders.refresh(this);if(web!=null){web.onResume();web.post(()->web.evaluateJavascript("window.AraMobile && window.AraMobile.resumeToday()",null));if(pendingQr!=null){String payload=pendingQr;pendingQr=null;web.evaluateJavascript("window.AraMobile && window.AraMobile.acceptQr("+payload+")",null);}web.evaluateJavascript("window.dispatchEvent(new Event('online'))",null);}}
 @Override public void onBackPressed(){web.evaluateJavascript("window.AraMobile ? window.AraMobile.back() : false",value->{if(!"true".equals(value))finish();});}
 @Override protected void onDestroy(){if(web!=null){web.removeJavascriptInterface("AraNative");web.destroy();}io.shutdownNow();super.onDestroy();}
 @Override public void onRequestPermissionsResult(int code,String[] permissions,int[] results){super.onRequestPermissionsResult(code,permissions,results);if(code==42){PhoneReminders.initialize(this);PhoneReminders.refresh(this);if(web!=null)web.evaluateJavascript("window.dispatchEvent(new Event('online'))",null);}}
 @Override protected void onActivityResult(int request,int result,Intent data){super.onActivityResult(request,result,data);if(request==71&&result==RESULT_OK&&data!=null){try{String pc=data.getStringExtra("pc"),code=data.getStringExtra("code");if(!local(pc)||code==null||!code.matches("[0-9]{8}"))return;JSONObject payload=new JSONObject();payload.put("pc",pc);payload.put("code",code);pendingQr=payload.toString();}catch(Exception ignored){}}}
 public class Bridge {
  @JavascriptInterface public void notionRequest(String id,String input){io.execute(()->{JSONObject reply=new JSONObject();try{if(input==null||input.length()>1000000)throw new IOException("요청이 너무 큽니다.");reply.put("status",200);reply.put("body",notionHub.request(MainActivity.this,new JSONObject(input)).toString());}catch(Exception e){try{reply.put("status",422);reply.put("body",new JSONObject().put("error",e.getMessage()==null?"노션 연결 실패":e.getMessage()).toString());}catch(Exception ignored){}}String json=reply.toString();runOnUiThread(()->{if(web!=null)web.evaluateJavascript("window.AraMobile.resolveNative("+JSONObject.quote(id)+","+json+")",null);});});}

  @JavascriptInterface public String readOfflineState(){try{android.util.AtomicFile file=new android.util.AtomicFile(new File(getFilesDir(),"offline-state.json"));try(InputStream in=file.openRead();ByteArrayOutputStream out=new ByteArrayOutputStream()){byte[] b=new byte[8192];int n;while((n=in.read(b))!=-1){if(out.size()+n>16*1024*1024)return "";out.write(b,0,n);}return out.toString("UTF-8");}}catch(Exception e){return "";}}
  @JavascriptInterface public boolean saveOfflineState(String text){if(text==null||text.length()>16*1024*1024)return false;android.util.AtomicFile file=new android.util.AtomicFile(new File(getFilesDir(),"offline-state.json"));FileOutputStream out=null;try{JSONObject obj=new JSONObject(text);if(obj.optInt("version")!=1||!obj.has("entries"))return false;out=file.startWrite();out.write(text.getBytes(StandardCharsets.UTF_8));file.finishWrite(out);return true;}catch(Exception e){if(out!=null)file.failWrite(out);return false;}}
  @JavascriptInterface public void setLocalReminders(String text){PhoneReminders.local(MainActivity.this,text);}
  @JavascriptInterface public void scanQr(){runOnUiThread(()->{if(!isFinishing())startActivityForResult(new Intent(MainActivity.this,QrScannerActivity.class),71);});}

  @JavascriptInterface public String updateStatus(){return AppUpdates.status(MainActivity.this);}
  @JavascriptInterface public void checkUpdates(){AppUpdates.check(MainActivity.this,true);}
  @JavascriptInterface public void retryUi(){AppUpdates.retryUi(MainActivity.this);}
  @JavascriptInterface public void reportUiError(String message){AppUpdates.reportUiError(MainActivity.this,message);}
  @JavascriptInterface public void installUpdate(){runOnUiThread(()->AppUpdates.install(MainActivity.this));}
  @JavascriptInterface public void reloadUi(){runOnUiThread(()->showHtml());}
  @JavascriptInterface public void uiBooted(){runOnUiThread(()->uiHealthy=true);}

  @JavascriptInterface public void configureNotifications(String server,String token){PhoneReminders.configure(MainActivity.this,server,token);AppUpdates.configure(MainActivity.this,server,token);}
  @JavascriptInterface public void refreshNotifications(){PhoneReminders.refresh(MainActivity.this);AppUpdates.check(MainActivity.this,false);}
  @JavascriptInterface public String queueNotionDiaries(String payload){return NotionDiarySync.enqueue(MainActivity.this,payload);}
  @JavascriptInterface public String notificationStatus(){return PhoneReminders.status(MainActivity.this);}
  @JavascriptInterface public void setNotifications(boolean value){PhoneReminders.setEnabled(MainActivity.this,value);}
  @JavascriptInterface public void requestNotificationPermission(){runOnUiThread(()->PhoneReminders.requestPermission(MainActivity.this));}
  @JavascriptInterface public void requestExactNotifications(){runOnUiThread(()->PhoneReminders.requestExact(MainActivity.this));}
  @JavascriptInterface public String notificationDiagnostics(){return PhoneReminders.diagnostics(MainActivity.this);}
  @JavascriptInterface public void openNotificationBatterySettings(){runOnUiThread(()->PhoneReminders.openBatterySettings(MainActivity.this));}
  @JavascriptInterface public void testScheduledNotification(){PhoneReminders.scheduledTest(MainActivity.this);}
  @JavascriptInterface public void testNotification(){PhoneReminders.test(MainActivity.this);}
  @JavascriptInterface public boolean initialDark(){return MainActivity.this.initialDark();}
  @JavascriptInterface public void setDark(final boolean dark){getPreferences(MODE_PRIVATE).edit().putBoolean("dark",dark).apply();runOnUiThread(()->applyAppearance(dark));}
  @JavascriptInterface public String initialServer(){return initial;}
  @JavascriptInterface public void haptic(){runOnUiThread(()->{if(web!=null)web.performHapticFeedback(HapticFeedbackConstants.CLOCK_TICK);});}
  @JavascriptInterface public void request(final String id,final String url,final String method,final String token,final String body){
   if(id==null||!id.matches("[A-Za-z0-9_-]{1,80}"))return;
   io.execute(()->{int status=0;String result;HttpURLConnection c=null;
    try{Uri u=Uri.parse(url);if(!local(url)||!u.getPath().startsWith("/api/")||!("GET".equals(method)||"POST".equals(method))||body.length()>4*1024*1024||!token.isEmpty()&&!token.matches("[a-f0-9]{64}"))throw new IOException("PC의 Wi-Fi 주소를 확인해 주세요.");
     c=(HttpURLConnection)new URL(url).openConnection(java.net.Proxy.NO_PROXY);c.setConnectTimeout(3500);c.setReadTimeout(u.getPath().equals("/api/notion")?130000:5500);c.setInstanceFollowRedirects(false);c.setRequestMethod(method);c.setRequestProperty("Accept","application/json");if(!token.isEmpty())c.setRequestProperty("Authorization","Bearer "+token);
     if("POST".equals(method)){c.setDoOutput(true);c.setRequestProperty("Content-Type","application/json; charset=utf-8");try(OutputStream out=c.getOutputStream()){out.write(body.getBytes(StandardCharsets.UTF_8));}}
     status=c.getResponseCode();InputStream input=status>=400?c.getErrorStream():c.getInputStream();try(InputStream in=input;ByteArrayOutputStream out=new ByteArrayOutputStream()){byte[] b=new byte[4096];int n,total=0;while(in!=null&&(n=in.read(b))!=-1){total+=n;if(total>1024*1024)throw new IOException("응답이 너무 큽니다.");out.write(b,0,n);}result=out.toString("UTF-8");}
    }catch(Exception e){status=0;result="{\"error\":\"PC 연결을 기다리고 있습니다. 같은 Wi-Fi와 PC 앱을 확인해 주세요.\"}";}finally{if(c!=null)c.disconnect();}
    final int code=status;final String text=result;runOnUiThread(()->{if(web==null||isFinishing())return;try{JSONObject response=new JSONObject();response.put("status",code);response.put("body",text);web.evaluateJavascript("window.AraMobile && window.AraMobile.resolveNative("+JSONObject.quote(id)+","+response.toString()+")",null);}catch(Exception ignored){}});
   });
  }
 }
}
