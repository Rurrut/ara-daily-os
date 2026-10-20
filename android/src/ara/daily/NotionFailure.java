package ara.daily;
import java.io.IOException;
import java.util.Locale;
import org.json.*;

/** Only controlled codes reach the UI; response bodies may contain credentials. */
final class NotionFailure extends IOException {
 final int status;final boolean reconnect,sessionExpired;
 private NotionFailure(int status,boolean reconnect,boolean sessionExpired,String message){super(message);this.status=status;this.reconnect=reconnect;this.sessionExpired=sessionExpired;}
 static NotionFailure from(String path,int code,String raw){
  String reason="",detail="";
  try{JSONObject o=new JSONObject(raw);Object e=o.opt("error");if(e instanceof JSONObject){reason=((JSONObject)e).optString("code");detail=((JSONObject)e).optString("message");}else reason=o.optString("error");detail+=" "+o.optString("error_description")+" "+o.optString("message");}catch(Exception ignored){}
  String lower=detail.toLowerCase(Locale.ROOT);boolean auth=path.equals("/token")&&(reason.equals("invalid_grant")||reason.equals("invalid_client"))||code==401;
  boolean expired=path.equals("/mcp")&&(code==404||code==400&&(lower.contains("invalid session")||lower.contains("session expired")||lower.contains("session not found")||lower.contains("session has expired")));
  String stage=path.equals("/token")?"인증 갱신/로그인":path.equals("/mcp")?"기록·페이지 요청":"연결 설정";
  String message=auth?"노션 인증이 만료되었거나 갱신이 거절됐습니다 ("+code+"). ‘노션 다시 연결’을 눌러 주세요. 기록과 저장 위치는 유지됩니다.":expired?"노션 연결 세션이 만료됐습니다 ("+code+"). 다시 시도해 주세요.":code==429?"노션 요청이 많습니다 (429). 잠시 후 자동 재시도합니다.":code==403?"노션 접근 권한이 없습니다 (403). 연결한 계정과 저장 페이지 권한을 확인해 주세요.":code>=500?"노션 서버 오류 ("+code+"). 기록을 보관하고 다시 시도합니다.":"노션이 "+stage+"을 거절했습니다 ("+code+"). 인터넷 끊김을 뜻하지 않습니다.";
  for(String safe:new String[]{"invalid_grant","invalid_client","invalid_request","invalid_scope","unsupported_grant_type"})if(reason.equals(safe))message+=" ["+safe+"]";
  return new NotionFailure(code,auth,expired,message);
 }
}
