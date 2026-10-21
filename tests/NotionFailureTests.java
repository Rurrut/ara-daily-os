package ara.daily;
import org.json.*;import java.net.*;import java.io.*;import java.lang.reflect.*;import java.util.*;import sun.misc.Unsafe;
public class NotionFailureTests {
 static int passed;static int failureCode=400;static String failureBody="";static int initializations,calls,rejections;static boolean alwaysReject;static int refreshes,tokenCode;static String tokenError="invalid_grant",persisted;
 static void ok(boolean b,String message){if(!b)throw new AssertionError(message);passed++;}
 static void set(Object o,String key,Object value)throws Exception{Field f=o.getClass().getDeclaredField(key);f.setAccessible(true);f.set(o,value);}
 static final class Connection extends HttpURLConnection{
  ByteArrayOutputStream sent=new ByteArrayOutputStream();int code;String response;
  Connection(URL u){super(u);}public void connect(){}public void disconnect(){}public boolean usingProxy(){return false;}
  public OutputStream getOutputStream(){return sent;}
  void prepare()throws IOException{if(response!=null)return;try{if(url.getPath().equals("/token")){refreshes++;code=tokenCode==0?200:tokenCode;response=code==200?new JSONObject().put("access_token","NEW_FAKE_ACCESS_TOKEN").put("refresh_token","NEW_FAKE_REFRESH_TOKEN").put("expires_in",3600).toString():new JSONObject().put("error",tokenError).put("error_description","FAKE_PRIVATE_DESCRIPTION").toString();return;}JSONObject q=new JSONObject(sent.toString("UTF-8"));String method=q.getString("method");if(method.equals("initialize")){initializations++;code=200;response=new JSONObject().put("id",q.get("id")).put("result",new JSONObject()).toString();}else if(method.equals("notifications/initialized")){code=202;response="";}else{calls++;if(rejections++==0||alwaysReject){code=failureCode;response=failureBody;}else{code=200;response=new JSONObject().put("id",q.get("id")).put("result",new JSONObject().put("ok",true)).toString();}}}catch(Exception e){throw new IOException(e);}}
  public int getResponseCode()throws IOException{prepare();return code;}
  public InputStream getInputStream()throws IOException{prepare();return new ByteArrayInputStream(response.getBytes("UTF-8"));}
  public InputStream getErrorStream(){try{prepare();return new ByteArrayInputStream(response.getBytes("UTF-8"));}catch(Exception e){return null;}}
  public String getHeaderField(String name){return name.equalsIgnoreCase("Mcp-Session-Id")?"fresh-session":null;}
 }
 static Object hub()throws Exception{Field f=Unsafe.class.getDeclaredField("theUnsafe");f.setAccessible(true);NotionHub h=(NotionHub)((Unsafe)f.get(null)).allocateInstance(NotionHub.class);set(h,"vault",new JSONObject().put("access_token","FAKE_PRIVATE_ACCESS").put("expires_at",System.currentTimeMillis()+3600000));set(h,"session","old-session");set(h,"initialized",true);set(h,"message","");set(h,"vaultWriter",(NotionHub.VaultWriter)value->persisted=value);return h;}
 static JSONObject vault(Object h)throws Exception{Field f=NotionHub.class.getDeclaredField("vault");f.setAccessible(true);return(JSONObject)f.get(h);}
 static JSONObject call(Object h)throws Exception{Method m=NotionHub.class.getDeclaredMethod("rpc",String.class,JSONObject.class);m.setAccessible(true);try{return(JSONObject)m.invoke(h,"tools/list",new JSONObject());}catch(InvocationTargetException e){throw(Exception)e.getCause();}}
 public static void main(String[]args)throws Exception{
  NotionFailure e=NotionFailure.from("/token",400,"{\"error\":\"invalid_grant\",\"error_description\":\"FAKE_PRIVATE_REFRESH\"}");ok(e.reconnect,"invalid grant requires login");ok(e.getMessage().contains("invalid_grant")&&!e.getMessage().contains("FAKE_PRIVATE"),"safe error, no raw response");
  ok(!NotionFailure.from("/mcp",400,"{\"error\":{\"message\":\"bad schema\"}}").sessionExpired,"schema failure is not a session error");
  ok(NotionFailure.from("/mcp",400,"{\"error\":{\"message\":\"Invalid session ID\"}}").sessionExpired,"explicit session failure");
  ok(NotionFailure.from("/mcp",404,"").sessionExpired,"404 requires new MCP session");
  for(int code:new int[]{400,401,403,429,500})ok(!NotionFailure.from("/mcp",code,"PRIVATE").getMessage().contains("PRIVATE"),"error response redacted "+code);
  URL.setURLStreamHandlerFactory(protocol->protocol.equals("https")?new URLStreamHandler(){protected URLConnection openConnection(URL u){return new Connection(u);}protected URLConnection openConnection(URL u,java.net.Proxy p){return new Connection(u);}}:null);
  for(int code:new int[]{400,404}){failureCode=code;failureBody="{\"error\":{\"message\":\"Session not found\"}}";initializations=calls=rejections=0;alwaysReject=false;ok(call(hub()).getBoolean("ok"),"expired session request recovered "+code);ok(initializations==1&&calls==2,"one new session and one retry "+code);}
  failureCode=400;failureBody="{\"error\":{\"message\":\"invalid input schema\"}}";initializations=calls=rejections=0;try{call(hub());throw new AssertionError();}catch(NotionFailure expected){ok(initializations==0&&calls==1,"ordinary 400 never replays request");}
  failureCode=404;initializations=calls=rejections=0;alwaysReject=true;try{call(hub());throw new AssertionError();}catch(NotionFailure expected){ok(initializations==1&&calls==2,"retry bounded to one");}

  alwaysReject=false;rejections=1;tokenCode=400;tokenError="invalid_grant";refreshes=0;
  NotionHub blocked=(NotionHub)hub();JSONObject initial=vault(blocked);initial.put("expires_at",0).put("refresh_token","FAKE_REFRESH_TOKEN").put("client_id","FAKE_CLIENT").put("target",new JSONObject().put("id","saved-db")).put("destination",new JSONObject().put("id","saved-page"));
  try{call(blocked);throw new AssertionError();}catch(NotionFailure expected){ok(expected.status==400&&expected.reconnect,"rejected refresh classified");}
  ok(refreshes==1&&blocked.reconnectRequired(),"one rejected refresh trips reconnect gate");
  JSONObject state=blocked.request(null,new JSONObject().put("op","status"));
  ok(!state.getBoolean("connected")&&state.getBoolean("reconnectRequired"),"UI does not report connected after terminal rejection");
  ok(state.getString("authError").equals("invalid_grant")&&state.getLong("authFailureAt")>0,"controlled diagnostic and failure time stored");
  ok(!persisted.contains("FAKE_REFRESH")&&!persisted.contains("FAKE_PRIVATE_ACCESS"),"rejected credentials removed from saved vault");
  ok(state.getJSONObject("target").getString("id").equals("saved-db")&&state.getJSONObject("destination").getString("id").equals("saved-page"),"saved destination retained");
  for(int i=0;i<3;i++)try{call(blocked);throw new AssertionError();}catch(IOException expected){}
  ok(refreshes==1,"repeated background requests never reuse invalid refresh");
  try{blocked.request(null,new JSONObject().put("op","auto-sync"));throw new AssertionError();}catch(IOException expected){ok(refreshes==1,"manual sync is also paused until reconnect");}
  NotionHub restarted=(NotionHub)hub();set(restarted,"vault",new JSONObject(persisted));try{call(restarted);throw new AssertionError();}catch(IOException expected){ok(refreshes==1&&restarted.reconnectRequired(),"gate and message survive restart");}
  NotionHub legacy=(NotionHub)hub();vault(legacy).put("reconnectRequired",true);try{call(legacy);throw new AssertionError();}catch(IOException expected){ok(refreshes==1&&!legacy.request(null,new JSONObject().put("op","status")).getBoolean("connected"),"old reconnect flag is respected after upgrade");}
  tokenCode=0;JSONObject renewed=new JSONObject().put("access_token","RECONNECTED_FAKE_ACCESS").put("refresh_token","RECONNECTED_FAKE_REFRESH").put("expires_in",3600);Method store=NotionHub.class.getDeclaredMethod("tokens",JSONObject.class);store.setAccessible(true);store.invoke(restarted,renewed);ok(!restarted.reconnectRequired()&&!vault(restarted).has("authError"),"successful reconnect clears terminal state");ok(vault(restarted).getJSONObject("target").getString("id").equals("saved-db"),"reconnect keeps saved target");
  NotionHub expired=(NotionHub)hub();vault(expired).put("expires_at",0).put("refresh_token","OLD_FAKE_REFRESH").put("client_id","FAKE_CLIENT");refreshes=0;initializations=calls=0;rejections=1;ok(call(expired).getBoolean("ok"),"ordinary expiry automatically refreshes and resumes");ok(refreshes==1&&vault(expired).getString("refresh_token").equals("NEW_FAKE_REFRESH_TOKEN")&&persisted.contains("NEW_FAKE_REFRESH_TOKEN"),"rotated refresh persisted before next request");
  tokenCode=503;tokenError="temporarily_unavailable";NotionHub transientHub=(NotionHub)hub();vault(transientHub).put("expires_at",0).put("refresh_token","KEEP_FAKE_REFRESH").put("client_id","FAKE_CLIENT");try{call(transientHub);throw new AssertionError();}catch(NotionFailure expected){ok(!transientHub.reconnectRequired()&&vault(transientHub).has("refresh_token"),"server errors do not discard credentials or require login");}
  tokenCode=400;tokenError="invalid_client";NotionHub client=(NotionHub)hub();vault(client).put("expires_at",0).put("refresh_token","FAKE_REFRESH").put("client_id","FAKE_CLIENT");try{call(client);throw new AssertionError();}catch(NotionFailure expected){ok(client.reconnectRequired()&&vault(client).getString("authError").equals("invalid_client"),"invalid client is terminal and distinguished");}
  ok(!state.toString().contains("FAKE_PRIVATE")&&!state.toString().contains("access_token")&&!state.toString().contains("refresh_token"),"status diagnostics expose no credentials or raw response");
  System.out.println("RESULT "+passed+"/"+passed+" passed: classification, credential redaction, real native HTTP/session recovery, no generic-400 replay.");
 }
}
