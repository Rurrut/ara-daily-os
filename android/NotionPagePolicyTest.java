package ara.daily;
import org.json.*;
public final class NotionPagePolicyTest {
 static int count;static JSONObject basic()throws Exception{return new JSONObject().put("creation_mode","draft").put("allow_async",false).put("pages",new JSONArray().put(new JSONObject().put("properties",new JSONObject().put("title","새 보관함"))));}
 static void pass(String name)throws Exception{count++;System.out.println("PASS "+name);}
 static void rejected(JSONObject input,String name)throws Exception{boolean failed=false;try{NotionPagePolicy.validate(input);}catch(Exception error){failed=true;}if(!failed)throw new AssertionError(name);pass(name);}
 public static void main(String[] args)throws Exception{
  NotionPagePolicy.validate(basic());pass("one private empty titled page is accepted");
  JSONObject nested=basic();nested.remove("creation_mode");nested.put("parent",new JSONObject().put("page_id","11111111-1111-4111-8111-111111111111"));NotionPagePolicy.validate(nested);pass("one empty page under a selected ordinary page is accepted");
  JSONObject bulk=basic();bulk.getJSONArray("pages").put(new JSONObject().put("properties",new JSONObject().put("title","extra")));rejected(bulk,"bulk creation is blocked");
  JSONObject body=basic();body.getJSONArray("pages").getJSONObject(0).put("content","unexpected records");rejected(body,"record bodies cannot be uploaded by setup");
  JSONObject row=basic();row.remove("creation_mode");row.put("parent",new JSONObject().put("data_source_id","11111111-1111-4111-8111-111111111111"));rejected(row,"database rows cannot be created by the page setup button");
  JSONObject empty=basic();empty.getJSONArray("pages").getJSONObject(0).getJSONObject("properties").put("title"," ");rejected(empty,"empty title is rejected");
  JSONObject async=basic();async.put("allow_async",true);rejected(async,"asynchronous creation cannot bypass immediate page selection");
  System.out.println("RESULT "+count+"/"+count+" passed");
 }
}
