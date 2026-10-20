package ara.daily;
import java.io.IOException;
import org.json.*;

/** OAuth returns ordinary JSON; only numbered MCP requests require a matching RPC id. */
final class NotionResponse {
 private NotionResponse(){}
 static JSONObject parse(String path,String requestBody,String raw)throws Exception {
  JSONObject request="/mcp".equals(path)&&requestBody!=null?new JSONObject(requestBody):null;
  Object expected=request==null?null:request.opt("id");
  boolean numbered=expected!=null&&expected!=JSONObject.NULL;
  String text=raw.trim();
  if(!numbered)return text.isEmpty()?new JSONObject():new JSONObject(text);
  if(text.startsWith("{")){
   JSONObject result=new JSONObject(text);
   if(!matches(expected,result))throw new IOException("노션 응답 번호가 일치하지 않습니다. 다시 시도해 주세요.");
   return result;
  }
  for(String line:text.split("\n"))if(line.startsWith("data:")){
   try{JSONObject result=new JSONObject(line.substring(5).trim());if(matches(expected,result))return result;}catch(JSONException ignored){}
  }
  throw new IOException("노션 응답을 읽지 못했습니다. 다시 시도해 주세요.");
 }
 private static boolean matches(Object expected,JSONObject value){Object actual=value.opt("id");return actual!=null&&actual!=JSONObject.NULL&&String.valueOf(expected).equals(String.valueOf(actual));}
}
