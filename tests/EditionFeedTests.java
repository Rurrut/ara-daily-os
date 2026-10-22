package ara.daily;
import org.json.*;
public class EditionFeedTests {
 static JSONObject asset(String name)throws Exception{return new JSONObject().put("name",name).put("browser_download_url",UpdateFeed.DOWNLOAD+"test/"+name);}
 static JSONObject release(boolean preview)throws Exception{return new JSONObject().put("published_at",preview?"2026-10-23T00:00:00Z":"2026-10-22T00:00:00Z").put("assets",new JSONArray().put(asset(preview?"aiary-preview-update.json":"ara-update.json")).put(asset(preview?"mobile-preview-update.zip":"mobile-update.zip")).put(asset((preview?"Aiary-Preview-Android-":"ARA-Daily-OS-Android-")+"1.0.apk")));}
 public static void main(String[]args)throws Exception{
  UpdateFeed.Release release=UpdateFeed.locate(new JSONArray().put(release(false)).put(release(true)).toString());
  if(!release.assets.containsKey(AppEdition.MANIFEST)||release.assets.containsKey(AppEdition.PREVIEW?"ara-update.json":"aiary-preview-update.json"))throw new AssertionError("Wrong edition selected");
  JSONObject m=new JSONObject().put("versionCode",30).put("versionName","1.0").put("uiVersion",2026102202).put("minNative",1).put("maxNative",30).put("uiSignature","test").put("apkSha256",new String(new char[64]).replace('\0','a')).put("uiSha256",new String(new char[64]).replace('\0','b'));
  UpdateFeed.Offer offer=UpdateFeed.offer(release,m);
  if(!offer.apkUrl.endsWith(AppEdition.APK_PREFIX+"1.0.apk")||!offer.uiUrl.endsWith(AppEdition.BUNDLE))throw new AssertionError("Wrong edition assets");
  try{UpdateFeed.locate(new JSONArray().put(release(!AppEdition.PREVIEW)).toString());throw new AssertionError("Other edition accepted");}catch(java.io.IOException expected){}
  System.out.println("PASS "+(AppEdition.PREVIEW?"Preview":"personal")+" release selection, APK and UI bundle isolated");
 }
}
