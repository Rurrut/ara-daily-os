package ara.daily;
import org.json.*;
public final class NotionResponseTest {
 static int passed;static void check(String name,boolean ok){if(!ok)throw new AssertionError(name);passed++;System.out.println("PASS "+name);}
 static String request(int id){return "{\"jsonrpc\":\"2.0\",\"id\":"+id+",\"method\":\"tools/list\"}";}
 public static void main(String[] args)throws Exception {
  check("OAuth discovery without an RPC id is accepted",NotionResponse.parse("/.well-known/oauth-authorization-server",null,"{\"issuer\":\"https://mcp.notion.com\"}").has("issuer"));
  check("client registration response without an RPC id is accepted",NotionResponse.parse("/register","{\"client_name\":\"ARA\"}","{\"client_id\":\"test-client\"}").has("client_id"));
  check("authorization code exchange uses ordinary JSON",NotionResponse.parse("/token","grant_type=authorization_code","{\"access_token\":\"fake-token\"}").has("access_token"));
  check("refresh exchange also accepts ordinary JSON",NotionResponse.parse("/token","grant_type=refresh_token","{\"access_token\":\"fake-refreshed\",\"refresh_token\":\"fake-rotated\"}").has("refresh_token"));
  check("numbered MCP JSON response still matches its request",NotionResponse.parse("/mcp",request(12),"{\"id\":12,\"result\":{}}").has("result"));
  boolean rejected=false;try{NotionResponse.parse("/mcp",request(12),"{\"id\":13,\"result\":{}}");}catch(java.io.IOException e){rejected=true;}check("wrong MCP response remains rejected",rejected);
  rejected=false;try{NotionResponse.parse("/mcp",request(12),"{\"result\":{}}");}catch(java.io.IOException e){rejected=true;}check("missing id on a numbered MCP response remains rejected",rejected);
  check("MCP initialized notification accepts an empty acknowledgement",NotionResponse.parse("/mcp","{\"method\":\"notifications/initialized\"}","").length()==0);
  check("unnumbered notification acknowledgement is not compared with previous RPC id",NotionResponse.parse("/mcp","{\"method\":\"notifications/initialized\"}","{\"ok\":true}").getBoolean("ok"));
  check("SSE ignores notifications and selects its matching RPC response",NotionResponse.parse("/mcp",request(7),"data: {\"method\":\"notifications/message\"}\n\ndata: {\"id\":6,\"result\":{}}\n\ndata: {\"id\":7,\"result\":{}}\n").getInt("id")==7);
  check("numbered MCP errors are passed to the error handler",NotionResponse.parse("/mcp",request(2),"{\"id\":2,\"error\":{\"code\":-32601}}").has("error"));
  rejected=false;try{NotionResponse.parse("/mcp",request(12),"");}catch(java.io.IOException e){rejected=true;}check("empty numbered response is not mistaken for success",rejected);
  if(args.length>0){String live=new String(java.nio.file.Files.readAllBytes(java.nio.file.Paths.get(args[0])),java.nio.charset.StandardCharsets.UTF_8);check("live Notion discovery JSON is accepted by the actual Android response parser",NotionResponse.parse("/.well-known/oauth-authorization-server",null,live).getString("issuer").equals("https://mcp.notion.com"));}
  System.out.println("RESULT "+passed+"/"+passed+" passed");
 }
}
