package ara.daily;
import org.json.*;
import java.net.*;
import java.io.*;
import java.nio.charset.StandardCharsets;
import java.util.*;

public class UpdateFeedTests {
 static int passed,requests;static boolean leaked;static String mode="ok";
 interface Run {void run()throws Exception;}
 static void ok(boolean value,String name){if(!value)throw new AssertionError(name);passed++;System.out.println("PASS "+name);}
 static void rejects(String name,Run run)throws Exception{try{run.run();throw new AssertionError(name);}catch(IOException|JSONException expected){passed++;System.out.println("PASS "+name);}}
 static JSONObject manifest()throws Exception{return new JSONObject().put("versionCode",24).put("versionName","2.9").put("uiVersion",2026102106).put("minNative",20).put("maxNative",24).put("apkSha256",String.join("",Collections.nCopies(64,"a"))).put("uiSha256",String.join("",Collections.nCopies(64,"b"))).put("uiSignature","public-signature");}
 static JSONObject release(boolean draft)throws Exception{JSONArray assets=new JSONArray();for(String name:new String[]{"ara-update.json","mobile-update.zip","ARA-Daily-OS-Android-2.9.apk"})assets.put(new JSONObject().put("name",name).put("browser_download_url",UpdateFeed.DOWNLOAD+"v2.9-test/"+name));return new JSONObject().put("draft",draft).put("prerelease",true).put("assets",assets);}
 static final class Mock extends HttpURLConnection {
  Mock(URL url){super(url);requests++;}public void connect(){}public void disconnect(){}public boolean usingProxy(){return false;}
  public int getResponseCode(){leaked|=getRequestProperty("Authorization")!=null;if(mode.equals("error"))return 503;if(mode.equals("loop")||url.getHost().equals("github.com")&&mode.startsWith("redirect"))return 302;return 200;}
  public String getHeaderField(String key){if(!key.equalsIgnoreCase("Location"))return null;if(mode.equals("redirect-http"))return "http://release-assets.githubusercontent.com/file";if(mode.equals("redirect-evil"))return "https://example.invalid/private";if(mode.equals("loop"))return UpdateFeed.DOWNLOAD+"v2.9-test/ara-update.json";return "https://release-assets.githubusercontent.com/file?sig=FAKE_PUBLIC_SIGNATURE";}
  public InputStream getInputStream(){return new ByteArrayInputStream((mode.equals("oversize")?"TOO-LONG":"download").getBytes(StandardCharsets.UTF_8));}
 }
 public static void main(String[] args)throws Exception {
  ok(UpdateFeed.allowed(UpdateFeed.API,true),"fixed public releases API allowed");
  ok(UpdateFeed.allowed(UpdateFeed.DOWNLOAD+"tag/ara-update.json",true),"own release asset allowed");
  for(String url:new String[]{"http://github.com/Rurrut/ara-daily-os/releases/download/a/b","https://github.com/other/repo/releases/download/a/b","https://github.com.evil.invalid/Rurrut/ara-daily-os/releases/download/a/b","https://user:pass@github.com/Rurrut/ara-daily-os/releases/download/a/b","https://192.168.0.2/file","https://api.github.com/repos/other/repo/releases?per_page=10"})ok(!UpdateFeed.allowed(url,true),"reject foreign, HTTP, credential or LAN URL");
  ok(!UpdateFeed.allowed("https://release-assets.githubusercontent.com/file",true)&&UpdateFeed.allowed("https://release-assets.githubusercontent.com/file",false),"CDN allowed only after own GitHub asset redirect");
  UpdateFeed.Release r=UpdateFeed.locate(new JSONArray().put(release(true)).put(new JSONObject().put("draft",false)).put(release(false)).toString());UpdateFeed.Offer offer=UpdateFeed.offer(r,manifest());ok(offer.apkUrl.endsWith("Android-2.9.apk"),"prerelease catalog selects exact APK without PC");
  rejects("missing manifest is not an update",()->UpdateFeed.locate("[]"));
  rejects("different version filename cannot be installed",()->UpdateFeed.offer(r,manifest().put("versionName","2.10")));
  rejects("malformed hash rejected",()->UpdateFeed.validate(manifest().put("apkSha256","x")));
  rejects("invalid native bounds rejected",()->UpdateFeed.validate(manifest().put("minNative",25)));
  rejects("unsafe version label rejected",()->UpdateFeed.validate(manifest().put("versionName","../../payload")));
  JSONObject older=release(false).put("published_at","2026-10-20T00:00:00Z"),newer=release(false).put("published_at","2026-10-21T00:00:00Z");newer.getJSONArray("assets").getJSONObject(0).put("browser_download_url",UpdateFeed.DOWNLOAD+"newest/ara-update.json");ok(UpdateFeed.locate(new JSONArray().put(older).put(newer).toString()).assets.get("ara-update.json").contains("/newest/"),"latest published release selected independently of API ordering");
  URL.setURLStreamHandlerFactory(protocol->protocol.equals("https")?new URLStreamHandler(){protected URLConnection openConnection(URL u){return new Mock(u);}protected URLConnection openConnection(URL u,Proxy p){return new Mock(u);}}:null);
  requests=0;leaked=false;mode="redirect-ok";ok(new String(UpdateFeed.fetch(offer.apkUrl,100),StandardCharsets.UTF_8).equals("download")&&requests==2,"APK download follows permitted CDN redirect");ok(!leaked,"public requests never send a PC bearer credential");
  mode="redirect-http";requests=0;rejects("HTTPS downgrade rejected",()->UpdateFeed.fetch(offer.apkUrl,100));ok(requests==1,"HTTP redirect blocked before network request");
  mode="redirect-evil";rejects("foreign redirect rejected",()->UpdateFeed.fetch(offer.apkUrl,100));
  mode="loop";requests=0;rejects("redirect loop bounded",()->UpdateFeed.fetch(offer.apkUrl,100));ok(requests==6,"redirect limit enforced");
  mode="oversize";rejects("download size limit enforced",()->UpdateFeed.fetch(offer.apkUrl,2));
  mode="error";rejects("HTTP failure is not reported as no new version",()->UpdateFeed.fetch(offer.apkUrl,100));
  System.out.println("RESULT "+passed+"/"+passed+" passed: public catalog, exact assets, isolated transport, redirects, bounds.");
 }
}
