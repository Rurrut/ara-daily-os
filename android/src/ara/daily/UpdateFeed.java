package ara.daily;
import org.json.*;
import java.net.*;
import java.io.*;
import java.nio.charset.StandardCharsets;
import java.util.*;

/** Public release transport. PC credentials are never part of this interface. */
final class UpdateFeed {
 static final String API="https://api.github.com/repos/Rurrut/ara-daily-os/releases?per_page=10";
 static final String DOWNLOAD="https://github.com/Rurrut/ara-daily-os/releases/download/";
 static final class Release {
  final Map<String,String> assets=new HashMap<>();
 }
 static final class Offer {
  final JSONObject manifest;final String apkUrl,uiUrl;
  Offer(JSONObject m,String apk,String ui){manifest=m;apkUrl=apk;uiUrl=ui;}
 }
 static boolean allowed(String value,boolean initial){try{URL u=new URL(value);if(!u.getProtocol().equals("https")||u.getUserInfo()!=null||u.getRef()!=null||u.getPort()!=-1&&u.getPort()!=443)return false;String host=u.getHost().toLowerCase(Locale.ROOT);if(host.equals("api.github.com"))return value.equals(API);if(host.equals("github.com"))return value.startsWith(DOWNLOAD)&&!u.getPath().contains("/../");return !initial&&(host.equals("release-assets.githubusercontent.com")||host.equals("objects.githubusercontent.com"));}catch(Exception e){return false;}}
 static byte[] fetch(String value,int limit)throws Exception {
  if(!allowed(value,true))throw new IOException("허용하지 않는 GitHub 업데이트 주소입니다.");
  String next=value;for(int hop=0;hop<6;hop++){
   if(!allowed(next,hop==0))throw new IOException("GitHub 다운로드 이동 주소를 확인하지 못했습니다.");
   HttpURLConnection c=(HttpURLConnection)new URL(next).openConnection(Proxy.NO_PROXY);
   try{c.setConnectTimeout(8000);c.setReadTimeout(25000);c.setInstanceFollowRedirects(false);c.setRequestProperty("User-Agent","ARA-Daily-OS/2.9.1");c.setRequestProperty("Accept",next.equals(API)?"application/vnd.github+json":"application/octet-stream");int status=c.getResponseCode();
    if(status==301||status==302||status==303||status==307||status==308){String location=c.getHeaderField("Location");if(location==null)throw new IOException("GitHub 다운로드 주소가 없습니다.");next=new URL(new URL(next),location).toExternalForm();continue;}
    if(status!=200)throw new IOException("GitHub 업데이트 응답 오류 (HTTP "+status+"). 잠시 후 다시 확인해 주세요.");
    try(InputStream in=c.getInputStream();ByteArrayOutputStream out=new ByteArrayOutputStream()){byte[] b=new byte[8192];int n;while((n=in.read(b))!=-1){if(out.size()+n>limit)throw new IOException("업데이트 파일이 너무 큽니다.");out.write(b,0,n);}return out.toByteArray();}
   }finally{c.disconnect();}
  }throw new IOException("GitHub 다운로드 이동 횟수를 초과했습니다.");
 }
 static Release locate(String text)throws Exception {
  JSONArray releases=new JSONArray(text);Release newest=null;String latest="";for(int i=0;i<releases.length();i++){JSONObject r=releases.getJSONObject(i);if(r.optBoolean("draft"))continue;Release release=new Release();JSONArray assets=r.optJSONArray("assets");if(assets==null)continue;for(int j=0;j<assets.length();j++){JSONObject a=assets.getJSONObject(j);String name=a.optString("name"),url=a.optString("browser_download_url");if(allowed(url,true)&&url.startsWith(DOWNLOAD))release.assets.put(name,url);}if(release.assets.containsKey(AppEdition.MANIFEST)&&release.assets.containsKey(AppEdition.BUNDLE)){String published=r.optString("published_at");if(newest==null||published.compareTo(latest)>0){newest=release;latest=published;}}}if(newest!=null)return newest;throw new IOException("GitHub에 준비된 앱 업데이트가 없습니다.");
 }
 static void validate(JSONObject m)throws Exception {if(m.getInt("versionCode")<1||m.getInt("versionCode")>100000||!m.getString("versionName").matches("[0-9]+(?:\\.[0-9]+){1,3}")||m.getInt("uiVersion")<1||m.getInt("minNative")<1||m.getInt("maxNative")<m.getInt("minNative")||!m.getString("apkSha256").matches("[0-9a-fA-F]{64}")||!m.getString("uiSha256").matches("[0-9a-fA-F]{64}")||m.getString("uiSignature").isEmpty())throw new IOException("업데이트 버전 정보를 확인하지 못했습니다.");}
 static Offer offer(Release r,JSONObject m)throws Exception {validate(m);String name=AppEdition.APK_PREFIX+m.getString("versionName")+".apk",apk=r.assets.get(name),ui=r.assets.get(AppEdition.BUNDLE);if(apk==null||ui==null)throw new IOException("GitHub 릴리스의 설치 파일 구성이 맞지 않습니다.");return new Offer(m,apk,ui);}
 static Offer discover()throws Exception {Release r=locate(new String(fetch(API,1000000),StandardCharsets.UTF_8));JSONObject m=new JSONObject(new String(fetch(r.assets.get(AppEdition.MANIFEST),32000),StandardCharsets.UTF_8));return offer(r,m);}
}
