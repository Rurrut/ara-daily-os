package ara.daily;
import java.net.*;
import java.io.*;
import java.security.cert.CertificateException;
import javax.net.ssl.SSLException;
import org.json.JSONException;

/** Fixed diagnostics only: never expose exception messages, tokens or response bodies. */
final class NotionLocalFailure {
 static String code(Throwable error){
  for(int i=0;error!=null&&i<12;i++,error=error.getCause()){
   if(error instanceof CertificateException)return "tls_certificate";
   if(error instanceof SSLException)return "tls_connection";
   if(error instanceof UnknownHostException)return "dns_lookup";
   if(error instanceof SocketTimeoutException)return "network_timeout";
   if(error instanceof ConnectException)return "connection_refused";
   if(error instanceof SocketException)return "connection_interrupted";
   if(error instanceof JSONException)return "invalid_json_response";
   if(error instanceof EOFException)return "incomplete_response";
  }
  return "local_unclassified";
 }
 static String hint(String code){
  switch(code){
   case "tls_certificate":case "tls_connection":return "휴대폰의 자동 날짜·시간과 VPN·보안 앱의 HTTPS 검사를 확인해 주세요.";
   case "dns_lookup":return "노션 인증 서버 주소를 찾지 못했습니다. VPN·비공개 DNS 설정을 확인해 주세요.";
   case "network_timeout":return "노션 인증 서버 응답을 기다리다 시간이 초과됐습니다.";
   case "connection_refused":case "connection_interrupted":case "incomplete_response":return "인증 서버와의 연결이 중단됐습니다.";
   case "invalid_json_response":return "인증 서버 응답을 해석하지 못했습니다.";
   default:return "앱 내부 오류를 분류하지 못했습니다. 진단 코드를 알려 주세요.";
  }
 }
}
