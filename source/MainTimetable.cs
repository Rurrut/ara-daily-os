using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Ara {
 public partial class MainForm:Form {
  public State Data;public Day Day;public Plan Plan;public GridPlan GridPlan;public Store Store;
  public Timetable Board;
  DateTimePicker date,wake;Label headline,scoreTotal,selectionInfo,focus,footer,changes;
  TextBox activity,taskText,note;ComboBox category,taskGroup,meal;NumericUpDown points,satisfactionPoints;DataGridView tasks;
  CheckBox notify,selectionMode;NotifyIcon tray;System.Windows.Forms.Timer timer;
  bool closing,refreshing,smoke;string lastAdjustment="";State undo;DateTime noteDue=DateTime.MaxValue;
  public MainForm(Store store,State data,bool smokeMode){
   Store=store;Data=data;Notion=new NotionService(store.PathName);Hub=new NotionHub(store.PathName);Theme.Set(Data.DarkMode);Routine.Upgrade(Data,DateTime.Today);Adaptive.Upgrade(Data);if(Routine.ShortenYogurt(Data,DateTime.Now))Store.Save(Data);CategoryCatalog.Ensure(Data);smoke=smokeMode;Text="ARA Daily OS · 10분 타임테이블";Icon=SystemIcons.Application;Size=new Size(1390,950);MinimumSize=new Size(1120,800);StartPosition=FormStartPosition.CenterScreen;BackColor=Theme.Bg;Font=Theme.Font(9);AutoScaleMode=AutoScaleMode.Dpi;
   BuildUI();Diagnostics.Context=DiagnosticContext;Diagnostics.Recover=RecoverSavedState;
   tray=new NotifyIcon{Icon=SystemIcons.Application,Text="ARA Daily OS · 10분 타임테이블",Visible=!smoke};var menu=new ContextMenuStrip();menu.Items.Add("타임테이블 열기",null,(s,e)=>{Show();WindowState=FormWindowState.Normal;Activate();});menu.Items.Add("완전히 종료",null,(s,e)=>Quit());tray.ContextMenuStrip=menu;tray.DoubleClick+=(s,e)=>{Show();WindowState=FormWindowState.Normal;Activate();};
   PhoneServer=new MobileServer(this);FormClosed+=(s,e)=>PhoneServer.Dispose();
   Day=Data.GetDay(DefaultDate(Data,DateTime.Now));RefreshAll();Board.Jump(Day.Wake.AddHours(-1));
   if(!smoke)try{Hub.Automatic=new NotionDiaryAuto(Store.PathName,Hub);Store.Saved=state=>Hub.Automatic.Enqueue(Store.Clone(state));Hub.Automatic.Enqueue(Store.Clone(Data));Hub.Automatic.Poke();FormClosed+=(s,e)=>{Store.Saved=null;Hub.Automatic.Dispose();};}catch(Exception ex){Diagnostics.Error("diary-auto-start",ex);}
   timer=new System.Windows.Forms.Timer{Interval=1000};timer.Tick+=(s,e)=>Tick();if(!smoke)timer.Start();
   FormClosing+=(s,e)=>{SaveNote();if(!closing&&!smoke){e.Cancel=true;Hide();Notify("트레이에서 실행 중","시간표와 집중 알림이 계속 동작합니다.");}else{timer.Stop();timer.Dispose();tray.Visible=false;tray.Dispose();}};
   Shown+=(s,e)=>{if(!smoke&&PhoneServer.WasEnabled)try{PhoneServer.Start(true);}catch(Exception ex){Diagnostics.Error("mobile-auto-start",ex);}if(!string.IsNullOrEmpty(Store.Warning))MessageBox.Show(this,Store.Warning,"데이터 복구");};
  }
  FlowLayoutPanel Row(){return new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false,AutoScroll=false,Margin=Padding.Empty};}
  public static DateTime DefaultDate(State s,DateTime now){var active=s.Days.Where(x=>x.Wake<=now&&x.Wake.Date>=now.Date.AddDays(-1)&&Adaptive.Target(s,x)>=now).OrderByDescending(x=>x.Wake).FirstOrDefault();return active==null?now.Date:DateTime.Parse(active.Date).Date;}
  void BuildUI(){
   var root=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(18,10,18,10),ColumnCount=1,RowCount=6};
   foreach(int h in new[]{49,47,49,-1,53,26})root.RowStyles.Add(new RowStyle(h<0?SizeType.Percent:SizeType.Absolute,h<0?100:h));Controls.Add(root);
   var header=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1};header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,790));header.RowStyles.Add(new RowStyle(SizeType.Percent,100));header.Controls.Add(Theme.Label("ARA / 10-minute day",20,true),0,0);var tools=Row();tools.AutoScroll=true;tools.Controls.Add(Theme.Button("노션 · AI",(s,e)=>NotionSettings()));tools.Controls.Add(Theme.Button("설정 · 기본표",(s,e)=>Settings()));tools.Controls.Add(Theme.Button("백업",(s,e)=>Export()));tools.Controls.Add(Theme.Button("복원",(s,e)=>Import()));tools.Controls.Add(Theme.Button("로그 폴더",(s,e)=>OpenTarget(Diagnostics.DirectoryPath)));tools.Controls.Add(Theme.Button("휴대폰 연결",(s,e)=>PhoneConnection()));themeToggle=Theme.Button("다크 모드",(s,e)=>ToggleDarkMode());themeToggle.AccessibleName="테마 전환";tools.Controls.Add(themeToggle);header.Controls.Add(tools,1,0);root.Controls.Add(header,0,0);
   var bar=Row();bar.Controls.Add(new Label{Text="날짜",AutoSize=true,Padding=new Padding(0,11,0,0)});date=new DateTimePicker{Format=DateTimePickerFormat.Custom,CustomFormat="yyyy-MM-dd",Width=126,Margin=new Padding(5,8,8,0)};date.ValueChanged+=(s,e)=>{if(!refreshing){SaveNote();Day=Data.GetDay(date.Value.Date);undo=null;Board.Selection=null;RefreshAll();Save();Board.Jump(Day.Wake.AddHours(-1));}};bar.Controls.Add(date);
   bar.Controls.Add(new Label{Text="시작 / 기상",AutoSize=true,Padding=new Padding(0,11,0,0)});wake=new DateTimePicker{Format=DateTimePickerFormat.Custom,CustomFormat="HH:mm",ShowUpDown=true,Width=84,Margin=new Padding(5,8,5,0)};bar.Controls.Add(wake);
   bar.Controls.Add(Theme.Button("자동 계획 생성",(s,e)=>GenerateAt(date.Value.Date.AddHours(wake.Value.Hour).AddMinutes(wake.Value.Minute)),true));bar.Controls.Add(Theme.Button("오늘",(s,e)=>date.Value=DateTime.Today));bar.Controls.Add(Theme.Button("되돌리기",(s,e)=>Undo()));notify=new CheckBox{Text="Windows 알림",AutoSize=true,Margin=new Padding(12,11,2,0)};notify.CheckedChanged+=(s,e)=>{if(!refreshing)Change(()=>Data.Notifications=notify.Checked);};bar.Controls.Add(notify);bar.Controls.Add(Theme.Button("알림 테스트",(s,e)=>Notify("ARA Daily OS","Windows 일정 알림 테스트입니다.")));bar.Controls.Add(Theme.Button("알림 상태",(s,e)=>WindowsNotifications.StatusDialog(this,Store.PathName)));root.Controls.Add(bar,0,1);
   var summary=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,BackColor=Theme.Accent,Tag="theme-primary",Padding=new Padding(13,0,13,0)};summary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));summary.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,355));headline=Theme.Label("계획과 실제, 한눈에",11,true);headline.ForeColor=Color.White;scoreTotal=Theme.Label("오늘 0점",16,true);scoreTotal.ForeColor=Color.White;scoreTotal.TextAlign=ContentAlignment.MiddleRight;summary.Controls.Add(headline,0,0);scoreTotal.Cursor=Cursors.Hand;scoreTotal.Click+=(sender,args)=>ShowAdherence();summary.Controls.Add(scoreTotal,1,0);root.Controls.Add(summary,0,2);
   var columns=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2};columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));columns.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,345));root.Controls.Add(columns,0,3);
   var left=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=2,Padding=new Padding(0,5,12,0)};left.RowStyles.Add(new RowStyle(SizeType.Absolute,34));left.RowStyles.Add(new RowStyle(SizeType.Percent,100));columns.Controls.Add(left,0,0);
   var instructions=Row();selectionMode=new CheckBox{Text="항상 범위 선택",AutoSize=true,Margin=new Padding(0,6,10,0)};selectionMode.CheckedChanged+=(s,e)=>Board.ForceSelection=selectionMode.Checked;instructions.Controls.Add(selectionMode);instructions.Controls.Add(new Label{Text="",AutoSize=true,ForeColor=Theme.Muted,Margin=new Padding(0,6,0,0)});foreach(string mode in new[]{"category","focus","satisfaction"}){string value=mode;var colorTab=Theme.Button(mode=="category"?"카테고리색":mode=="focus"?"집중색":"만족도색",(s,e)=>{Board.DisplayMode=value;Board.Invalidate();foreach(Button tab in instructions.Controls.OfType<Button>()){bool active=object.ReferenceEquals(tab,s);tab.BackColor=active?Theme.Accent:Theme.Soft;tab.ForeColor=active?Color.White:Theme.Ink;}footer.Text="색상 보기 · "+(value=="category"?"카테고리":value=="focus"?"집중 0→5점 옅게→진하게":"만족도 0→5점 옅게→진하게");});colorTab.Margin=Padding.Empty;colorTab.MinimumSize=new Size(75,28);colorTab.Height=28;instructions.Controls.Add(colorTab);}left.Controls.Add(instructions,0,0);
   Board=new Timetable();Board.SelectionChanged+=(s,e)=>SelectCells();Board.MoveRequested+=(s,e)=>MoveCells(e);Board.ResizeRequested+=(s,e)=>ResizeCells(e);Board.PaletteRequested+=(s,e)=>CopyPlanned(e);Board.EditRequested+=(s,e)=>ShowCategories();Board.ScoreRequested+=(s,e)=>ApplyQuickScore(e.Score);Board.DeleteRequested+=(s,e)=>DeleteSelected();left.Controls.Add(Board,0,1);
   var right=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=14,Padding=new Padding(7,6,0,0)};
   foreach(int h in new[]{26,36,29,30,30,40,38,43,24,-1,38,20,40,18})right.RowStyles.Add(new RowStyle(h<0?SizeType.Percent:SizeType.Absolute,h<0?100:h));columns.Controls.Add(right,1,0);
   right.Controls.Add(Theme.Label("선택한 시간 기록",12,true),0,0);selectionInfo=Theme.Label("칸을 드래그하거나 블록을 클릭하세요.",9);right.Controls.Add(selectionInfo,0,1);
   activity=new TextBox{Dock=DockStyle.Fill,MaxLength=250};activity.AccessibleName="활동 내용 (선택)";right.Controls.Add(activity,0,2);
   var classify=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=4};classify.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,53));classify.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));classify.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,44));classify.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,91));classify.Controls.Add(Theme.Label("분류",9),0,0);category=new ThemeComboBox{Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList};category.Items.AddRange(CategoryCatalog.Keys(Data,true).Cast<object>().ToArray());category.FormattingEnabled=true;category.Format+=(s,e)=>e.Value=CategoryCatalog.Label(Data.CategorySettings,Convert.ToString(e.ListItem));category.SelectedItem="기타";classify.Controls.Add(category,1,0);classify.Controls.Add(Theme.Label("집중",9),2,0);points=new NumericUpDown{Minimum=-100000,Maximum=100000,DecimalPlaces=2,Dock=DockStyle.Fill};points.AccessibleName="집중 점수";classify.Controls.Add(points,3,0);right.Controls.Add(classify,0,3);
   var meals=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2};meals.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,53));meals.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));meals.Controls.Add(Theme.Label("끼니",9),0,0);meal=new ThemeComboBox{Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList};meal.Items.AddRange(AssignmentDialog.MealLabels);meal.SelectedIndex=0;meals.Controls.Add(meal,1,0);right.Controls.Add(meals,0,4);
   var edit=Row();edit.Controls.Add(Theme.Button("선택 칸 저장",(s,e)=>ApplyEditor(),true));edit.Controls.Add(Theme.Button("계획 가져오기",(s,e)=>CopyPlanFields()));edit.Controls.Add(Theme.Button("비우기",(s,e)=>ClearCells()));right.Controls.Add(edit,0,5);
   var satisfactionRow=Row();satisfactionRow.Controls.Add(Theme.Label("만족도",9));satisfactionPoints=new NumericUpDown{Minimum=0,Maximum=5,Value=2,Width=55};satisfactionPoints.AccessibleName="만족도 점수";satisfactionRow.Controls.Add(satisfactionPoints);satisfactionRow.Controls.Add(Theme.Button("만족도 저장",(s,e)=>ApplySatisfaction(satisfactionPoints.Value)));right.Controls.Add(satisfactionRow,0,6);
   var work=Row();work.Controls.Add(Theme.Button("작업 시작",(s,e)=>StartFocus("작업",true),true));work.Controls.Add(Theme.Button("학습 시작",(s,e)=>StartFocus("학습",true)));work.Controls.Add(Theme.Button("집중 종료",(s,e)=>Change(()=>Data.FocusEnd=null)));right.Controls.Add(work,0,7);focus=Theme.Label("집중 준비",9);right.Controls.Add(focus,0,8);
   tasks=Theme.Grid();tasks.Columns.Add(new DataGridViewCheckBoxColumn{Name="done",HeaderText="완료",FillWeight=22});tasks.Columns.Add("text","과제");tasks.Columns[1].ReadOnly=true;tasks.CurrentCellDirtyStateChanged+=(s,e)=>{if(tasks.IsCurrentCellDirty)tasks.CommitEdit(DataGridViewDataErrorContexts.Commit);};tasks.CellValueChanged+=(s,e)=>{if(!refreshing&&e.RowIndex>=0&&e.ColumnIndex==0){string id=(string)tasks.Rows[e.RowIndex].Tag;bool check=Convert.ToBoolean(tasks.Rows[e.RowIndex].Cells[0].Value);Change(()=>{var t=Data.Tasks.Find(x=>x.Id==id);if(check&&!t.DoneDates.Contains(Day.Date))t.DoneDates.Add(Day.Date);if(!check)t.DoneDates.Remove(Day.Date);});}};right.Controls.Add(tasks,0,9);
   var todo=Row();taskText=new TextBox{Width=110,Margin=new Padding(0,8,3,0)};taskGroup=new ThemeComboBox{Width=55,DropDownStyle=ComboBoxStyle.DropDownList,Margin=new Padding(0,8,0,0)};taskGroup.Items.AddRange(new object[]{"작업","학습","생활"});taskGroup.SelectedIndex=0;todo.Controls.Add(taskText);todo.Controls.Add(taskGroup);var add=Theme.Button("추가",(s,e)=>AddTask());add.AutoSize=false;add.MinimumSize=new Size(55,30);add.Size=new Size(58,30);todo.Controls.Add(add);var remove=Theme.Button("제거",(s,e)=>RemoveTask());remove.AutoSize=false;remove.MinimumSize=new Size(55,30);remove.Size=new Size(58,30);todo.Controls.Add(remove);taskText.KeyDown+=(s,e)=>{if(e.KeyCode==Keys.Enter){AddTask();e.SuppressKeyPress=true;}};right.Controls.Add(todo,0,10);
   right.Controls.Add(Theme.Label("오늘의 메모 · 자동 저장",8),0,11);note=new TextBox{Multiline=true,Dock=DockStyle.Fill,ScrollBars=ScrollBars.Vertical};note.TextChanged+=(s,e)=>{if(!refreshing)noteDue=DateTime.Now.AddMilliseconds(700);};note.Leave+=(s,e)=>SaveNote();right.Controls.Add(note,0,12);right.Controls.Add(Theme.Label("위: 계획  /  아래: 실제  /  한 칸 10분",8),0,13);
   changes=Theme.Label("",8);changes.ForeColor=Theme.Muted;root.Controls.Add(changes,0,4);footer=Theme.Label("",8);footer.ForeColor=Theme.Muted;root.Controls.Add(footer,0,5);
  }
  public void RefreshAll(){
   ApplyTheme();
   refreshing=true;
   try{
    GridPlan=GridEngine.Build(Data,Day);Plan=GridEngine.AsPlan(Data,Day,GridPlan);date.Value=DateTime.Parse(Day.Date);wake.Value=Day.Wake;notify.Checked=Data.Notifications;note.Text=Day.Note??"";
    string selectedCategory=Convert.ToString(category.SelectedItem);category.Items.Clear();category.Items.AddRange(CategoryCatalog.Keys(Data,true).Cast<object>().ToArray());category.SelectedItem=category.Items.Contains(selectedCategory)?selectedCategory:"기타";Board.Catalog=Data.CategorySettings;
    Board.AdherenceScores.Clear();foreach(var item in JsonMap.Items(Adherence.Report(Day),"actions")){var rating=JsonMap.Map(item);for(int minute=Convert.ToInt32(rating["actual_start"]);minute<Convert.ToInt32(rating["actual_end"]);minute+=10)Board.AdherenceScores[minute/10]=Convert.ToDecimal(rating["score"]);}
    Board.EstimatedSleep=Adaptive.EstimatedSleep(Data,Day,GridPlan);Board.SetData(Day.Wake.Date,GridPlan.DisplayBlocks,Day.Records);tasks.Rows.Clear();foreach(var t in Data.Tasks){int i=tasks.Rows.Add(t.DoneDates.Contains(Day.Date),t.Text);tasks.Rows[i].Tag=t.Id;}
    decimal total=GridEngine.Total(Day);int count=Day.Records.Sum(x=>x.Count);scoreTotal.Font=Theme.Font(11,true);scoreTotal.Text="집중 "+total.ToString("0.##")+"점 · "+(count*10)+"분 기록\n만족도 "+GridEngine.SatisfactionAverage(Data,Day,GridPlan).ToString("0.00")+" / 5 · 이행 "+(Adherence.Score(Day).HasValue?Adherence.Score(Day).Value.ToString("0.#"):"—");
    changes.Text="취침 목표 "+Theme.Time(Plan.Target,Day)+" · 수면 예상 "+Theme.Time(Board.EstimatedSleep.Value,Day)+" · "+(GridPlan.Changes.Count>0?string.Join(" · ",GridPlan.Changes):"앞 대기 → 뒤 대기 → 식사·샤워 → 앞 대기 재확인 → 3끼니 → 요거트 → 취침 여유 → 존2 → 웨이트");
    string adjustment=Plan.Target.ToString("o")+"; saved="+GridPlan.Saved+"; overflow="+GridPlan.Overflow+"; sleep="+Board.EstimatedSleep.Value.ToString("o")+"; "+string.Join(" | ",GridPlan.Changes);if(adjustment!=lastAdjustment){Diagnostics.Action("auto-adjust",adjustment);lastAdjustment=adjustment;}
    if(GridPlan.Overflow>0)changes.Text+="  /  목표보다 "+GridPlan.Overflow+"분 늦게 종료";
    UpdateClock();
   }finally{refreshing=false;}
   SelectCells();
  }
  void SelectCells(){
   var selected=Board.Selection;if(selected==null){selectionInfo.Text="칸을 드래그하거나 블록을 클릭하세요.";return;}
   selectionInfo.Text=(selected.Lane==0?"계획":"실제")+"  "+Board.TimeAt(selected.First).ToString("MM/dd HH:mm")+"–"+Board.TimeAt(selected.Last+1).ToString("MM/dd HH:mm")+"\n"+selected.Count+"칸 · "+(selected.Count*10)+"분";
   var b=Board.At(selected.Lane,selected.First);if(b!=null)LoadFields(b);else{activity.Text="";category.SelectedItem="기타";points.Value=0;satisfactionPoints.Value=2;meal.SelectedIndex=0;}
  }
  void LoadFields(CellBlock b){activity.Text=b.Name;category.SelectedItem=b.Category;points.Value=b.Score;satisfactionPoints.Value=b.Satisfaction;meal.SelectedIndex=AssignmentDialog.MealIndex(b.MealId);}
  void Defaults(CellBlock b){
   b.Priority=b.Category=="존2"?1:b.Category=="웨이트"?2:b.Category=="학습"?3:b.Category=="휴식"?5:0;
   b.Minimum=b.Category=="존2"?2:b.Category=="웨이트"||b.Category=="학습"?3:b.Category=="휴식"?1:b.Count;
   if(b.MealId=="b12"){b.Priority=4;b.Minimum=1;}
  }
  void ApplyEditor(){
   if(Board.Selection==null)return;var selected=Board.Selection;
   var b=new CellBlock{Id=Guid.NewGuid().ToString("N"),Start=Board.TimeAt(selected.First),Count=selected.Count,Name=activity.Text.Trim(),Category=Convert.ToString(category.SelectedItem),Score=selected.Lane==1?points.Value:0,Satisfaction=selected.Lane==1?satisfactionPoints.Value:2};
   if(meal.SelectedIndex!=7&&GridEngine.MealName(b.Name))b.Category="식사";
   string[] meals=AssignmentDialog.MealValues;
   var existing=Board.At(selected.Lane,selected.First);
   if(existing!=null&&existing.Start==b.Start&&existing.Count==b.Count){b.Id=existing.Id;b.TemplateId=existing.TemplateId;b.PrepFor=b.Category=="끼니 준비"?existing.PrepFor:"";}
   if(b.Category=="식사")b.MealId=meal.SelectedIndex>0?meals[meal.SelectedIndex]:GridEngine.DetectMeal(Day,GridPlan,b);
   Defaults(b);SaveEntry(selected,b);
  }
  void SaveEntry(CellSelection selected,CellBlock b){
   if(b.Category=="기상"){SetWakeCell(selected);return;}
   Change(()=>{
    if(selected.Lane==0){Day.GridBase=GridPlan.DisplayBlocks.Select(x=>x.Copy()).ToList();GridEngine.SetRange(Day.GridBase,b);}
    else{if(b.Category!="식사")b.MealId="";GridEngine.SetRange(Day.Records,b);}
   });
  }
  void ShowAssignment(CellSelection selected,CellBlock preset){
   if(selected==null)return;Diagnostics.Action("detail-open");
   var existing=Board.At(selected.Lane,selected.First);
   using(var dialog=new AssignmentDialog(selected,Board.TimeAt(selected.First),preset??existing,GridPlan.DisplayBlocks,Data)){
    if(dialog.ShowDialog(this)!=DialogResult.OK)return;var b=dialog.Result;
    if(existing!=null&&existing.Start==b.Start&&existing.Count==b.Count)b.Id=existing.Id;
    if(b.Category=="식사"&&dialog.AutoMeal)b.MealId=GridEngine.DetectMeal(Day,GridPlan,b);
    if(string.IsNullOrEmpty(b.TemplateId))Defaults(b);AssignPreservingScores(selected,b);
   }
  }
  void CopyPlanFields(){if(Board.Selection==null)return;var b=Board.At(0,Board.Selection.First);if(b!=null)LoadFields(b);}
  void ClearCells(){if(Board.Selection==null)return;var selected=Board.Selection;Change(()=>{if(selected.Lane==0){Day.GridBase=GridPlan.DisplayBlocks.Select(x=>x.Copy()).ToList();GridEngine.ClearRange(Day.GridBase,Board.TimeAt(selected.First),selected.Count);}else GridEngine.ClearRange(Day.Records,Board.TimeAt(selected.First),selected.Count);});}
  void MoveCells(MoveRequest e){
   var source=Board.At(e.Lane,Board.Index((e.Lane==0?GridPlan.DisplayBlocks:Day.Records).Find(x=>x.Id==e.Id).Start));if(source!=null&&source.Category=="기상"){SetWakeCell(new CellSelection{Lane=e.Lane,First=Board.Index(e.Start),Last=Board.Index(e.Start)});return;}
   Change(()=>{if(e.Lane==0){Day.GridBase=GridPlan.DisplayBlocks.Select(x=>x.Copy()).ToList();GridEngine.MovePlanned(Day.GridBase,e.Id,e.Start);}else GridEngine.Move(Day.Records,e.Id,e.Start);});
  }
  void Undo(){if(undo==null)return;string key=Day.Date;var current=Data;Data=undo;Data.DarkMode=current.DarkMode;Data.RemoteReceipts=current.RemoteReceipts==null?new List<RemoteReceipt>():current.RemoteReceipts.ToList();Day=Data.GetDay(DateTime.Parse(key));if(Save()){undo=current;RefreshAll();}else{Data=current;Day=Data.GetDay(DateTime.Parse(key));RefreshAll();}}
  bool Save(){try{Store.Save(Data);footer.Text="저장됨 · "+DateTime.Now.ToString("HH:mm:ss")+" · "+Store.PathName;return true;}catch(Exception e){Diagnostics.Error("operation",e);MessageBox.Show(this,e.Message,"저장 오류");return false;}}
  void Change(Action action,[CallerMemberName]string operation="change"){Diagnostics.Action(operation,"begin");var old=Store.Clone(Data);string key=Day.Date;try{action();if(PhoneCodec.Hash(PhoneCodec.Json(old.GetDay(DateTime.Parse(key)).Records))!=PhoneCodec.Hash(PhoneCodec.Json(Day.Records)))BlockTools.Merge(Day.Records);if(PhoneCodec.Hash(PhoneCodec.Json(old.GetDay(DateTime.Parse(key)).GridBase))!=PhoneCodec.Hash(PhoneCodec.Json(Day.GridBase)))BlockTools.Merge(Day.GridBase);PlanHistory.Advance(old,old.GetDay(DateTime.Parse(key)),Data,Day);GridEngine.Build(Data,Day);Engine.Validate(Data);if(!Save())throw new IOException("저장 실패");undo=old;RefreshAll();Diagnostics.Action(operation,"saved");}catch(Exception e){Diagnostics.Error(operation,e);Data=old;Day=Data.GetDay(DateTime.Parse(key));RefreshAll();if(smoke)throw;MessageBox.Show(this,e.Message,"변경 확인",MessageBoxButtons.OK,MessageBoxIcon.Warning);}}
  void AddTask(){string text=taskText.Text.Trim();if(text.Length==0)return;Change(()=>Data.Tasks.Add(new Todo{Id=Guid.NewGuid().ToString("N"),Text=text,Group=taskGroup.Text}));taskText.Clear();}
  void RemoveTask(){if(tasks.SelectedRows.Count==0)return;string id=(string)tasks.SelectedRows[0].Tag;Change(()=>Data.Tasks.RemoveAll(x=>x.Id==id));}
  void SaveNote(){noteDue=DateTime.MaxValue;if(Day==null||refreshing||Day.Note==note.Text)return;string old=Day.Note;Day.Note=note.Text;if(!Save())Day.Note=old;}
  void StartFocus(string name,bool open){Change(()=>{Data.FocusName=name;Data.FocusEnd=DateTime.Now.AddMinutes(Data.FocusMinutes);Data.FocusNotified=false;});if(open){string target=name=="학습"?Data.StudyLink:Data.WorkLink;if(!string.IsNullOrWhiteSpace(target))try{OpenTarget(target);}catch(Exception e){Diagnostics.Error("operation",e);MessageBox.Show(this,e.Message,"작업 공간 열기");}}}
  public static void OpenTarget(string target){Uri uri;if(Uri.TryCreate(target,UriKind.Absolute,out uri)&&(uri.Scheme=="https"||uri.Scheme=="http")){Process.Start(new ProcessStartInfo(target){UseShellExecute=true});return;}string path=Path.GetFullPath(target);if(Directory.Exists(path)||(File.Exists(path)&&new[]{".pdf",".txt",".md",".docx",".xlsx",".pptx",".csv"}.Contains(Path.GetExtension(path).ToLowerInvariant()))){Process.Start(new ProcessStartInfo(path){UseShellExecute=true});return;}throw new InvalidOperationException("웹 주소, 문서 또는 폴더를 연결해 주세요.");}
  bool noticeBusy; DateTime noticeRetry=DateTime.MinValue;
  void Notify(string title,string text){if(!smoke)WindowsNotifications.Queue(title,text,Store.PathName,result=>{});}
  void ShowAdherence(){var report=Adherence.Report(Day);var lines=new List<string>{"9점 만점 · 10분 오차마다 1점 차감 · 식사는 시작만 평가"};foreach(var row in JsonMap.Items(report,"actions")){var a=JsonMap.Map(row);lines.Add(JsonMap.Str(a,"name")+" ("+CategoryCatalog.Label(Data.CategorySettings,JsonMap.Str(a,"category"))+") · "+Convert.ToDecimal(a["score"]).ToString("0.#")+"/9 · 시작 오차 "+a["start_error_minutes"]+"분"+(a["end_score"]==null?"":" / 종료 오차 "+a["end_error_minutes"]+"분"));}if(lines.Count==1)lines.Add("비교 기준이 저장된 행동 기록이 아직 없습니다. 기존 기록은 미평가일 수 있습니다.");MessageBox.Show(this,string.Join("\n",lines),"이행도");}
  void UpdateClock(){
   DateTime now=DateTime.Now;Board.SetCurrentTime(now);var current=Plan.Slots.Find(x=>x.Start<=now&&now<x.End);var next=Plan.Slots.Find(x=>x.Start>now);
   headline.Text=current!=null?"계획 · "+current.Block.Name+"  "+current.Start.ToString("HH:mm")+"–"+current.End.ToString("HH:mm"):next!=null?"다음 계획 · "+next.Block.Name+"  "+next.Start.ToString("MM/dd HH:mm"):"계획과 실제 · 10분씩 기록하는 하루";
   if(Data.FocusEnd.HasValue){var span=Data.FocusEnd.Value-now;focus.Text=span.TotalSeconds>0?Data.FocusName+" 집중 · "+Math.Floor(span.TotalMinutes).ToString("00")+":"+Math.Max(0,span.Seconds).ToString("00"):"집중 완료";}else focus.Text="집중 "+Data.FocusMinutes+"분 · 과제 "+Data.Tasks.Count(x=>x.DoneDates.Contains(Day.Date))+"/"+Data.Tasks.Count;
  }
  void Tick(){
   if(DateTime.Now>=noteDue)SaveNote();UpdateClock();if(!Data.Notifications)return;
   if(noticeBusy||DateTime.Now<noticeRetry)return;
   bool focusDue=Data.FocusEnd.HasValue&&DateTime.Now>=Data.FocusEnd.Value&&!Data.FocusNotified;
   DateTime? focusEnd=Data.FocusEnd;
   long now=ScheduleNotifications.Epoch(DateTime.Now);var pending=ScheduleNotifications.Feed(Data,DateTime.Now).items.Where(x=>x.at<=now&&!Data.GetDay(DateTime.Parse(x.date)).Notified.Contains(x.key)).ToList();
   if(!focusDue&&pending.Count==0)return;noticeBusy=true;noticeRetry=DateTime.Now.AddSeconds(60);
   string title=focusDue?"집중 완료":pending[0].title;string body=focusDue?Data.FocusName+" 집중 시간이 끝났습니다.":string.Join("\n",pending.Take(4).Select(x=>x.title+" · "+x.body));
   WindowsNotifications.Queue(title,body,Store.PathName,result=>{if(IsDisposed||!IsHandleCreated)return;try{BeginInvoke(new Action(()=>{noticeBusy=false;if(!result.Success)return;if(focusDue){if(Data.FocusEnd==focusEnd)Data.FocusNotified=true;}else foreach(var item in pending){var d=Data.GetDay(DateTime.Parse(item.date));if(!d.Notified.Contains(item.key))d.Notified.Add(item.key);}Save();}));}catch(ObjectDisposedException){}catch(InvalidOperationException){}});

  }
  void Settings(){using(var dialog=new SettingsDialog(Data)){if(dialog.ShowDialog(this)!=DialogResult.OK)return;Change(()=>{var edited=dialog.Result;edited.Days=Data.Days;edited.Tasks=Data.Tasks;edited.FocusEnd=Data.FocusEnd;edited.FocusName=Data.FocusName;edited.FocusNotified=Data.FocusNotified;Data=edited;Day=Data.GetDay(DateTime.Parse(Day.Date));});}}
  void Export(){using(var d=new SaveFileDialog{Filter="ARA 백업 (*.json)|*.json",FileName="ARA-backup-"+DateTime.Now.ToString("yyyyMMdd-HHmm")+".json"}){if(d.ShowDialog(this)!=DialogResult.OK)return;try{new Store(d.FileName).Save(Data);footer.Text="백업: "+d.FileName;}catch(Exception e){Diagnostics.Error("operation",e);MessageBox.Show(this,e.Message,"백업 오류");}}}
  void Import(){using(var d=new OpenFileDialog{Filter="ARA 백업 (*.json)|*.json"}){if(d.ShowDialog(this)!=DialogResult.OK)return;try{var restored=Store.ReadFile(d.FileName);if(MessageBox.Show(this,"현재 데이터를 백업하고 이 파일로 복원할까요?","복원",MessageBoxButtons.OKCancel)!=DialogResult.OK)return;new Store(Path.Combine(Path.GetDirectoryName(Store.PathName),"before-import-"+DateTime.Now.ToString("yyyyMMddHHmmss")+".json")).Save(Data);Change(()=>{Data=restored;Day=Data.GetDay(date.Value.Date);});}catch(Exception e){Diagnostics.Error("operation",e);MessageBox.Show(this,e.Message,"복원 오류");}}}
  public void Quit(){closing=true;Close();}
  public void SmokeActions(){
   taskText.Text="타임테이블 테스트 과제";AddTask();tasks.Rows[tasks.Rows.Count-1].Cells[0].Value=true;note.Text="10분 기록 메모";SaveNote();StartFocus("검증",false);
   Board.SetSelection(new CellSelection{Lane=1,First=60,Last=71});activity.Text="RAG 공부";category.SelectedItem="학습";points.Value=3;ApplyEditor();
   if(GridEngine.Total(Day)!=36)throw new Exception("화면 점수 합산 실패");
   Board.SetSelection(new CellSelection{Lane=1,First=51,Last=53});activity.Text="첫 식사";category.SelectedItem="식사";points.Value=1;meal.SelectedIndex=1;ApplyEditor();
   if(Day.Records.Find(x=>x.MealId=="b1")==null||GridPlan.DisplayBlocks.Find(x=>x.MealId=="b1").Start!=Day.Wake.Date.AddHours(8).AddMinutes(30))throw new Exception("식사 연결 실패");
   var loaded=Store.Load();if(GridEngine.Total(loaded.GetDay(DateTime.Today))!=39||!loaded.FocusEnd.HasValue)throw new Exception("재시작 데이터 저장 실패");
   Board.Jump(Day.Wake.AddHours(-1));Board.SetSelection(null);
  }
 }
}
