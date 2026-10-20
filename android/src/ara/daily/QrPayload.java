package ara.daily;
import java.net.URI;
import java.net.URLDecoder;
import java.util.*;
public final class QrPayload {
 public final String pc,code;
 private QrPayload(String pc,String code){this.pc=pc;this.code=code;}
 public static QrPayload parse(String text)throws Exception{
  if(text==null||text.length()>2048)throw new IllegalArgumentException("PC 앱의 연결용 QR을 스캔해 주세요.");
  URI uri=new URI(text);if(!"aradaily".equals(uri.getScheme())||!"pair".equals(uri.getHost())||uri.getRawUserInfo()!=null||uri.getPort()!=-1||uri.getFragment()!=null||uri.getPath()!=null&&!uri.getPath().isEmpty()||uri.getRawQuery()==null)throw new IllegalArgumentException("설치용 QR 대신 PC 앱의 ‘앱 연결용 QR’을 스캔해 주세요.");
  Map<String,String> values=new HashMap<>();for(String pair:uri.getRawQuery().split("&")){String[] bits=pair.split("=",2);if(bits.length!=2)throw new IllegalArgumentException("연결 QR 형식 오류");String key=URLDecoder.decode(bits[0],"UTF-8"),value=URLDecoder.decode(bits[1],"UTF-8");if(!Arrays.asList("v","pc","code").contains(key)||values.put(key,value)!=null)throw new IllegalArgumentException("연결 QR 항목 오류");}
  if(!"1".equals(values.get("v"))||values.get("code")==null||!values.get("code").matches("[0-9]{8}"))throw new IllegalArgumentException("연결 코드 형식 오류");
  URI pc=new URI(values.get("pc"));if(!"http".equals(pc.getScheme())||pc.getHost()==null||pc.getRawUserInfo()!=null||pc.getRawQuery()!=null||pc.getRawFragment()!=null||pc.getPath()!=null&&!pc.getPath().isEmpty()&&!pc.getPath().equals("/"))throw new IllegalArgumentException("같은 Wi-Fi의 PC 주소가 필요합니다.");
  String[] octets=pc.getHost().split("\\.");if(octets.length!=4)throw new IllegalArgumentException("PC의 Wi-Fi 주소가 필요합니다.");int[] b=new int[4];for(int i=0;i<4;i++){if(!octets[i].matches("[0-9]{1,3}"))throw new IllegalArgumentException("PC 주소 오류");b[i]=Integer.parseInt(octets[i]);if(b[i]>255)throw new IllegalArgumentException("PC 주소 오류");}
  if(!(b[0]==10||b[0]==192&&b[1]==168||b[0]==172&&b[1]>=16&&b[1]<=31))throw new IllegalArgumentException("같은 Wi-Fi의 PC 주소만 연결할 수 있습니다.");int port=pc.getPort()==-1?8765:pc.getPort();if(port<1||port>65535)throw new IllegalArgumentException("PC 포트 오류");
  return new QrPayload("http://"+pc.getHost()+":"+port,values.get("code"));
 }
}
