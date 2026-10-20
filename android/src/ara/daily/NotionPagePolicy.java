package ara.daily;
import org.json.*;
import java.io.IOException;
import java.util.UUID;
final class NotionPagePolicy {
 private NotionPagePolicy(){}
 static void validate(JSONObject args)throws Exception{
  JSONArray pages=args.optJSONArray("pages");if(pages==null||pages.length()!=1)throw new IOException("새 페이지는 한 번에 하나씩 만듭니다.");JSONObject page=pages.getJSONObject(0),props=page.optJSONObject("properties");
  if(page.length()!=1||props==null||props.length()!=1||!(props.opt("title") instanceof String))throw new IOException("새 페이지에는 제목만 지정할 수 있습니다.");String title=props.getString("title").trim();if(title.isEmpty()||title.length()>80)throw new IOException("페이지 이름을 1~80자로 입력해 주세요.");
  JSONObject parent=args.optJSONObject("parent");if(parent==null){if(!"draft".equals(args.optString("creation_mode")))throw new IOException("새 페이지의 개인 공간 위치를 확인해 주세요.");}else{if(parent.length()!=1||!parent.has("page_id")||args.has("creation_mode"))throw new IOException("일반 페이지 아래에만 만들 수 있습니다.");UUID.fromString(parent.getString("page_id"));}
  if(args.optBoolean("allow_async",false))throw new IOException("페이지 생성 결과를 바로 확인해야 합니다.");
 }
}
