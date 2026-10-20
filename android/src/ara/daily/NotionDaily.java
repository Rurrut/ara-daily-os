package ara.daily;
import org.json.*;
import java.io.IOException;
import java.time.LocalDate;
import java.util.*;
import java.util.regex.*;

/** Only summary properties of one wake-date row in the configured destination are written. */
final class NotionDaily {
 interface Call {JSONObject run(String suffix,JSONObject args)throws Exception;}
 interface Mark {void run(boolean pending)throws Exception;}
 private static IOException fail(String s){return new IOException(s);}
 static JSONObject decode(JSONObject result)throws Exception{
  if(result.optBoolean("isError"))throw fail("노션 작업 실패: "+result.optJSONArray("content"));
  if(result.optJSONObject("structuredContent")!=null)return result.getJSONObject("structuredContent");
  JSONArray content=result.optJSONArray("content");if(content==null)return result;
  StringBuilder text=new StringBuilder();for(int i=0;i<content.length();i++){JSONObject c=content.optJSONObject(i);if(c!=null&&"text".equals(c.optString("type")))text.append(c.optString("text"));}
  String raw=text.toString();if(raw.trim().startsWith("{"))return new JSONObject(raw);if(raw.trim().startsWith("["))return new JSONObject().put("results",new JSONArray(raw));return new JSONObject().put("text",raw);
 }
 static String date(JSONObject input)throws Exception{String d=input.optString("date");try{if(!d.matches("\\d{4}-\\d{2}-\\d{2}")||!LocalDate.parse(d).toString().equals(d))throw new Exception();}catch(Exception e){throw fail("기상한 날짜를 확인해 주세요.");}return d;}
 static String id(String raw)throws Exception{Matcher m=Pattern.compile("(?i)([a-f0-9]{8}-?[a-f0-9]{4}-?[a-f0-9]{4}-?[a-f0-9]{4}-?[a-f0-9]{12})(?:[/?].*)?$").matcher(raw);if(!m.find())throw fail("노션 ID를 확인해 주세요.");String s=m.group(1).replace("-","");return s.substring(0,8)+"-"+s.substring(8,12)+"-"+s.substring(12,16)+"-"+s.substring(16,20)+"-"+s.substring(20);}
 static String source(JSONObject target,Call call)throws Exception{if(target==null)throw fail("저장할 하루 기록 데이터베이스를 먼저 지정해 주세요.");String targetId=id(target.getString("id"));JSONObject fetched=decode(call.run("fetch",new JSONObject().put("id","data_source".equals(target.optString("kind"))?"collection://"+targetId:targetId)));String text=fetched.optString("text",fetched.toString());Set<String> sources=new LinkedHashSet<>();Matcher m=Pattern.compile("collection://([a-fA-F0-9-]{32,36})").matcher(text);while(m.find())sources.add(id(m.group(1)));String src="data_source".equals(target.optString("kind"))?targetId:sources.size()==1?sources.iterator().next():"";if(src.isEmpty()||!sources.contains(src))throw fail("데이터 원본을 하나로 지정해 주세요.");
  for(String key:new String[]{"제목","date:날짜:start","집중도","만족도","집중 총점","기록 시간(분)"})if(!text.contains("\""+key+"\""))throw fail("하루 기록 DB의 필수 속성이 없습니다: "+key);return src;}
 static JSONObject properties(JSONObject input)throws Exception{String d=date(input);double minutes=num(input,"minutes",0,2880),total=num(input,"focusTotal",0,1440),focus=num(input,"focusAverage",0,5),sat=num(input,"satisfactionAverage",0,5);if(minutes%10!=0||total%1!=0||total>minutes/10*5||Math.abs(focus-(minutes==0?0:total/(minutes/10)))>.011)throw fail("하루 점수와 기록 시간을 확인해 주세요.");return new JSONObject().put("date:날짜:start",d).put("date:날짜:is_datetime",0).put("집중도",focus).put("만족도",sat).put("집중 총점",total).put("기록 시간(분)",minutes);}
 private static double num(JSONObject input,String key,double min,double max)throws Exception{Object value=input.opt(key);if(!(value instanceof Number))throw fail("기록 요약 숫자를 확인해 주세요.");double n=((Number)value).doubleValue();if(!Double.isFinite(n)||n<min||n>max)throw fail("기록 요약 범위를 확인해 주세요.");return n;}
 static JSONArray rows(JSONObject result)throws Exception{for(String key:new String[]{"results","rows","data"}){JSONArray a=result.optJSONArray(key);if(a!=null)return a;JSONObject o=result.optJSONObject(key);if(o!=null)for(String nested:new String[]{"results","rows"})if(o.optJSONArray(nested)!=null)return o.getJSONArray(nested);}throw fail("기존 날짜 기록 조회 결과를 확인하지 못했습니다. 새 행을 만들지 않았습니다.");}
 static JSONObject save(JSONObject input,JSONObject target,boolean pending,Call call,Mark mark)throws Exception{JSONObject props=properties(input);String d=date(input),src=source(target,call),uri="collection://"+src;JSONObject data=new JSONObject().put("mode","sql").put("data_source_urls",new JSONArray().put(uri)).put("query","SELECT * FROM \""+uri+"\" WHERE \"date:날짜:start\" = ? LIMIT 2").put("params",new JSONArray().put(d));JSONArray rows=rows(decode(call.run("query-data-sources",new JSONObject().put("data",data))));if(rows.length()>1)throw fail("같은 기상일의 행이 여러 개 있습니다. 노션에서 중복을 정리한 뒤 다시 저장해 주세요.");String pageId;boolean created=rows.length()==0;
  if(!created){JSONObject row=rows.getJSONObject(0);pageId=id(row.optString("url",row.optString("id")));decode(call.run("update-page",new JSONObject().put("page_id",pageId).put("command","update_properties").put("allow_async",false).put("properties",props)));mark.run(false);NotionDiary.update(pageId,input,call);}
  else{if(pending)throw fail("이 날짜의 생성 결과를 확인 중입니다. 노션에서 행 유무를 확인해 주세요. 중복 생성을 막았습니다.");mark.run(true);props.put("제목",d+" 하루 기록");JSONObject result=decode(call.run("create-pages",new JSONObject().put("parent",new JSONObject().put("data_source_id",src)).put("allow_async",false).put("pages",new JSONArray().put(NotionDiary.newPage(props,input)))));JSONArray pages=result.optJSONArray("pages");if(pages==null)pages=result.optJSONArray("results");JSONObject row=pages!=null&&pages.length()>0?pages.getJSONObject(0):result;try{pageId=id(row.optString("url",row.optString("id")));}catch(Exception e){throw fail("행은 생성됐을 수 있습니다. 다시 저장하면 기존 날짜를 먼저 조회합니다.");}mark.run(false);NotionDiary.update(pageId,input,call);}
  return new JSONObject().put("saved",true).put("created",created).put("date",d).put("url","https://www.notion.so/"+pageId.replace("-",""));
 }
 static boolean hasInline(String body,String src){java.util.regex.Matcher m=java.util.regex.Pattern.compile("<database\\b[^>]*>").matcher(body);while(m.find()){String tag=m.group();if(tag.contains("inline=\"true\"")&&(tag.contains(src)||tag.contains(src.replace("-",""))))return true;}return false;}
 static JSONObject inline(JSONObject target,Call call,Mark mark)throws Exception{String parent=id(target.optString("parentId")),src=source(target,call);JSONObject page=decode(call.run("fetch",new JSONObject().put("id",parent)));String text=page.optString("text"),body=text.replaceAll("(?s)^.*?<content>","").replaceAll("(?s)</content>.*$","");if(hasInline(body,src))return new JSONObject().put("saved",true).put("existing",true);if(target.optBoolean("inlinePending"))throw fail("페이지 표 생성 결과가 불확실합니다. 노션 페이지를 먼저 확인해 주세요.");mark.run(true);decode(call.run("create-view",new JSONObject().put("parent_page_id",parent).put("data_source_id",src).put("name","날짜별 기록").put("type","table").put("configure","SHOW \"날짜\", \"제목\", \"집중도\", \"만족도\", \"집중 총점\", \"기록 시간(분)\"; SORT BY \"날짜\" DESC")));mark.run(false);return new JSONObject().put("saved",true);
 }
}
