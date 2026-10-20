package ara.daily;
import org.json.*;import java.net.*;import java.io.*;import java.lang.reflect.*;import java.util.*;import sun.misc.Unsafe;
public class NotionFailureTests {
 static int passed;static int failureCode=400;static String failureBody="";static int initializations,calls,rejections;static boolean alwaysReject;
 static void ok(boolean b,String message){if(!b)throw new AssertionError(message);passed++;}
 static void set(Object o,String key,Object value)throws Exception{Field f=o.getClass().getDeclaredField(key);f.setAccessible(true);f.set(o,value);}
 static final class Connection extends HttpURLConnection{
  ByteArrayOutputStream sent=new ByteArrayOutputStream();int code;String response;
  Connection(URL u){super(u);}public void connect(){}public void disconnect(){}public boolean usingProxy(){return false;}
  public OutputStream getOutputStream(){return sent;}
  void prepare()throws IOException{if(response!=null)return;try{JSONObject q=new JSONObject(sent.toString("UTF-8"));String method=q.getString("method");if(method.equals("initialize")){initializations++;code=200;response=new JSONObject().put("id",q.get("id")).put("result",new JSONObject()).toString();}else if(method.equals("notifications/initialized")){code=202;response="";}else{calls++;if(rejections++==0||alwaysReject){code=failureCode;response=failureBody;}else{code=200;response=new JSONObject().put("id",q.get("id")).put("result",new JSONObject().put("ok",true)).toString();}}}catch(Exception e){throw new IOException(e);}}
  public int getResponseCode()throws IOException{prepare();return code;}
  public InputStream getInputStream()throws IOException{prepare();return new ByteArrayInputStream(response.getBytes("UTF-8"));}
  public InputStream getErrorStream(){try{prepare();return new ByteArrayInputStream(response.getBytes("UTF-8"));}catch(Exception e){return null;}}
  public String getHeaderField(String name){return name.equalsIgnoreCase("Mcp-Session-Id")?"fresh-session":null;}
 }
 static Object hub()throws Exception{Field f=Unsafe.class.getDeclaredField("theUnsafe");f.setAccessible(true);NotionHub h=(NotionHub)((Unsafe)f.get(null)).allocateInstance(NotionHub.class);set(h,"vault",new JSONObject().put("access_token","FAKE_PRIVATE_ACCESS").put("expires_at",System.currentTimeMillis()+3600000));set(h,"session","old-session");set(h,"initialized",true);set(h,"message","");return h;}
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
  System.out.println("RESULT "+passed+"/"+passed+" passed: classification, credential redaction, real native HTTP/session recovery, no generic-400 replay.");
 }
}
