using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Drawing;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.Collections.Generic;
namespace Ara {
 public partial class MainForm {
  public NotionService Notion; public NotionHub Hub;
  public object NotionRequest(NotionInput input){
   if(input==null)throw new InvalidDataException("요청을 확인해 주세요.");
   if(input.op=="status")return Notion.Status();
   if(input.op=="search")return JsonMap.Obj("results",Notion.Search((input.text??"").Substring(0,Math.Min(100,(input.text??"").Length))));
   if(input.op=="target"||input.op=="reference"){Notion.Destination(input.id,input.kind,input.op=="reference");return Notion.Status();}
   if(input.op=="read")return Notion.ReadReference(input.id,input.kind);
   if(input.op=="daily")return Notion.ReadDaily(PhoneCodec.Date(input.date).ToString("yyyy-MM-dd"));
   if(input.op=="preview"||input.op=="publish"||input.op=="draft"){
    State snapshot=null;Action capture=()=>{SaveNote();snapshot=Store.Clone(Data);};if(InvokeRequired)Invoke(capture);else capture();
    PhoneCodec.Date(input.date);if(input.op=="preview")return Notion.Preview(snapshot,input.date);if(input.op=="draft")return Notion.Draft(snapshot,input.date);return Notion.Publish(snapshot,input.date,input.text??"");
   }
   throw new InvalidDataException("지원하지 않는 노션 요청입니다.");
  }
  void NotionSettings(){SaveNote();using(var f=new NotionConnectWindow(this)){f.ShowDialog(this);}}
 }
 public class NotionWindow:Form {
  readonly MainForm owner;TextBox token,query,link,model,output,diary;ComboBox kind;ListBox results;Label status;FlowLayoutPanel actions;bool busy;
  string day;
  public NotionWindow(MainForm form){
   owner=form;day=form.Day.Date;Text="노션 · AI 하루 정리 / "+day+" 기상일";Size=new Size(820,800);MinimumSize=new Size(680,640);StartPosition=FormStartPosition.CenterParent;BackColor=Theme.Bg;
   var layout=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(14),ColumnCount=1,RowCount=10};foreach(int h in new[]{32,44,43,36,110,43,38,-1,115,48})layout.RowStyles.Add(new RowStyle(h<0?SizeType.Percent:SizeType.Absolute,h<0?100:h));Controls.Add(layout);
   status=Theme.Label("");layout.Controls.Add(status,0,0);
   var credentials=new FlowLayoutPanel{Dock=DockStyle.Fill};token=new TextBox{Width=380,UseSystemPasswordChar=true,AccessibleName="노션 연결 토큰 · PC에만 암호화 저장",Margin=new Padding(0,8,0,0)};credentials.Controls.Add(new Label{Text="노션 연결 토큰",AutoSize=true,Margin=new Padding(0,11,5,0)});token.Width=290;credentials.Controls.Add(token);Add(credentials,"연결 저장",async()=>{if(token.TextLength==0)throw new InvalidOperationException("노션 연결 정보를 PC 앱에 직접 입력해 주세요. 채팅에 보내지 마세요.");owner.Notion.Connect(token.Text);token.Clear();await Task.Run(()=>owner.Notion.Call("GET","users/me",null));UpdateStatus();});Add(credentials,"간편 노션 연결",()=>{using(var screen=new NotionConnectWindow(owner))screen.ShowDialog(this);return Task.FromResult(0);});Add(credentials,"연결 도움말",()=>{output.Text="Notion 개발자 설정에서 내부 연결을 만들고 읽기·삽입·수정 권한을 지정하세요. 사용할 페이지의 연결 메뉴에서 해당 연결에 접근을 허용하세요. 연결 토큰은 위 칸에 직접 입력합니다. 이 PC의 Windows 계정에 묶어 암호화하며 휴대폰으로 전송하지 않습니다.\r\nhttps://www.notion.so/profile/integrations\r\nAI는 PC의 Ollama(127.0.0.1:11434)를 사용합니다. 미설치 상태에서도 기록·일기 수동 저장은 됩니다.";return Task.FromResult(0);});layout.Controls.Add(credentials,0,1);
   var search=new FlowLayoutPanel{Dock=DockStyle.Fill};search.Controls.Add(new Label{Text="페이지 제목",AutoSize=true,Margin=new Padding(0,11,5,0)});query=new TextBox{Width=280,AccessibleName="노션 페이지 검색",Margin=new Padding(0,8,0,0)};search.Controls.Add(query);Add(search,"페이지 검색",async()=>{string queryText=query.Text;var found=await Task.Run(()=>owner.Notion.Search(queryText));results.Items.Clear();foreach(var target in found)results.Items.Add(new TargetItem(target));});layout.Controls.Add(search,0,2);
   layout.Controls.Add(Theme.Label("검색 결과 선택 또는 아래에 노션 링크 입력 · 저장 위치와 참고 위치는 별개입니다.",9),0,3);
   results=new ListBox{Dock=DockStyle.Fill};results.SelectedIndexChanged+=(s,e)=>{var item=results.SelectedItem as TargetItem;if(item!=null){link.Text=item.Target.id;kind.SelectedIndex=Array.IndexOf(new[]{"page","data_source","database"},item.Target.kind);}};layout.Controls.Add(results,0,4);
   var destination=new FlowLayoutPanel{Dock=DockStyle.Fill};link=new TextBox{Width=300,AccessibleName="노션 페이지 링크 또는 ID",Margin=new Padding(0,8,0,0)};kind=new ComboBox{Width=115,DropDownStyle=ComboBoxStyle.DropDownList,Margin=new Padding(0,8,0,0)};kind.Items.AddRange(new object[]{"페이지","데이터 원본","데이터베이스"});kind.SelectedIndex=0;destination.Controls.Add(link);destination.Controls.Add(kind);Add(destination,"저장 위치로",async()=>{string id=link.Text,k=new[]{"page","data_source","database"}[kind.SelectedIndex];await Task.Run(()=>owner.Notion.Destination(id,k,false));UpdateStatus();});Add(destination,"참고 위치로",async()=>{string id=link.Text,k=new[]{"page","data_source","database"}[kind.SelectedIndex];await Task.Run(()=>owner.Notion.Destination(id,k,true));UpdateStatus();});layout.Controls.Add(destination,0,5);
   var ai=new FlowLayoutPanel{Dock=DockStyle.Fill};ai.Controls.Add(new Label{Text="로컬 AI 모델",AutoSize=true,Margin=new Padding(0,9,0,0)});model=new TextBox{Width=180,Text=owner.Notion.Load().model,AccessibleName="Ollama 모델 이름",Margin=new Padding(0,6,0,0)};ai.Controls.Add(model);Add(ai,"모델 저장",()=>{owner.Notion.SetModel(model.Text);UpdateStatus();return Task.FromResult(0);});layout.Controls.Add(ai,0,6);
   output=new TextBox{Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Both,AccessibleName="기록과 노션 자료 미리보기"};layout.Controls.Add(output,0,7);diary=new TextBox{Dock=DockStyle.Fill,Multiline=true,ScrollBars=ScrollBars.Vertical,MaxLength=30000,AccessibleName="확인하고 수정할 일기 초안"};layout.Controls.Add(diary,0,8);
   actions=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoScroll=true,WrapContents=false};Add(actions,"기록 미리보기",()=>Run("preview"));Add(actions,"참고 자료 읽기",async()=>{var p=owner.Notion.Load();if(p.reference==null)throw new InvalidOperationException("참고 위치를 먼저 선택해 주세요.");var value=await Task.Run(()=>owner.Notion.ReadReference(p.reference.id,p.reference.kind));output.Text=JsonMap.Str(JsonMap.Map(value),"text");});Add(actions,"AI 초안",()=>Run("draft"));Add(actions,"노션에 저장",()=>Run("publish"));Add(actions,"저장한 일기 읽기",()=>Run("daily"));layout.Controls.Add(actions,0,9);UpdateStatus();FormClosing+=(s,e)=>{if(busy)e.Cancel=true;};
  }
  void Add(FlowLayoutPanel parent,string label,Func<Task> action){var button=Theme.Button(label,async(s,e)=>{if(busy)return;busy=true;buttonState(false);try{await action();}catch(Exception ex){output.Text=ex.Message;}finally{busy=false;buttonState(true);}});parent.Controls.Add(button);}
  void buttonState(bool enabled){foreach(var button in All(this).OfType<Button>())button.Enabled=enabled;}
  IEnumerable<Control> All(Control root){foreach(Control c in root.Controls){yield return c;foreach(var x in All(c))yield return x;}}
  void UpdateStatus(){var p=owner.Notion.Load();status.Text=(owner.Notion.Connected?"연결 정보 저장됨":"연결 필요")+" · 저장: "+(p.target==null?"미지정":p.target.title)+" · 참고: "+(p.reference==null?"미지정":p.reference.title);}
  async Task Run(string op){var request=new NotionInput{op=op,date=day,text=diary.Text};var result=JsonMap.Map(await Task.Run(()=>owner.NotionRequest(request)));if(op=="preview")output.Text=JsonMap.Str(result,"markdown");else if(op=="draft")diary.Text=JsonMap.Str(result,"draft");else if(op=="daily"){diary.Text=JsonMap.Str(result,"draft");output.Text=JsonMap.Str(JsonMap.Child(result,"page"),"text");}else output.Text="저장 완료\r\n"+JsonMap.Str(result,"url");}
  sealed class TargetItem {public NotionTarget Target;public TargetItem(NotionTarget t){Target=t;}public override string ToString(){return Target.title+" ["+Target.kind+"]";}}
 }
}
