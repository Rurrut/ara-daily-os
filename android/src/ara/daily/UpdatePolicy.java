package ara.daily;
import java.io.*;
import java.security.*;
import java.security.spec.X509EncodedKeySpec;
import java.nio.charset.StandardCharsets;
import java.util.*;
import java.util.zip.*;
public final class UpdatePolicy {
 private UpdatePolicy(){}
 public static String hash(byte[] bytes)throws Exception{StringBuilder out=new StringBuilder();for(byte b:MessageDigest.getInstance("SHA-256").digest(bytes))out.append(String.format(Locale.ROOT,"%02x",b&255));return out.toString();}
 public static void verify(byte[] key,byte[] signature,byte[] bytes,String hash,int version,int min,int max,int nativeVersion)throws Exception{
  if(!hash(bytes).equals(hash))throw new IOException("화면 업데이트 파일이 손상됐습니다.");
  Signature verifier=Signature.getInstance("SHA256withRSA");verifier.initVerify(KeyFactory.getInstance("RSA").generatePublic(new X509EncodedKeySpec(key)));verifier.update(("ARA-UI\n"+version+"\n"+min+"\n"+max+"\n"+hash).getBytes(StandardCharsets.UTF_8));if(!verifier.verify(signature))throw new IOException("ARA가 서명한 화면 업데이트가 아닙니다.");if(min>nativeVersion||max<nativeVersion)throw new IOException("이 화면 버전은 APK 업데이트가 필요합니다.");
 }
 public static Map<String,String> unpack(byte[] bytes)throws Exception{
  Map<String,String> files=new HashMap<>();Set<String> allowed=new HashSet<>(Arrays.asList("index.html","mobile.css","mobile.js","offline-core.js"));int total=0;
  try(ZipInputStream zip=new ZipInputStream(new ByteArrayInputStream(bytes))){ZipEntry entry;while((entry=zip.getNextEntry())!=null){String name=entry.getName();if(!allowed.contains(name)||files.containsKey(name)||entry.isDirectory())throw new IOException("화면 업데이트 구성 오류");ByteArrayOutputStream out=new ByteArrayOutputStream();byte[] buffer=new byte[8192];int n;while((n=zip.read(buffer))!=-1){total+=n;if(total>3*1024*1024)throw new IOException("화면 업데이트가 너무 큽니다.");out.write(buffer,0,n);}files.put(name,out.toString("UTF-8"));}}
  if(files.size()!=4)throw new IOException("화면 업데이트 파일이 빠졌습니다.");return files;
 }
}
