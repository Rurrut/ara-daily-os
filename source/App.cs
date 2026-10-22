using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Drawing;
using System.Windows.Forms;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace Ara {
 public static partial class Theme {
  public static Color Bg=Color.FromArgb(245,246,240), Ink=Color.FromArgb(30,47,42), Muted=Color.FromArgb(101,113,105), Accent=Color.FromArgb(38,108,81), Soft=Color.FromArgb(225,237,225);
  public static Font Font(float size, bool bold=false){return new Font("맑은 고딕",size,bold?FontStyle.Bold:FontStyle.Regular);}
  public static Label Label(string text,float size=10,bool bold=false){return new Label{Text=text,Font=Font(size,bold),ForeColor=Ink,AutoSize=false,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft};}
  public static Button Button(string text,EventHandler action,bool primary=false) {
   var b=new Button{Text=text,Tag=primary?"theme-primary":"theme-button",AutoSize=true,Height=36,MinimumSize=new Size(92,36),FlatStyle=FlatStyle.Flat,Font=Font(9),BackColor=primary?Accent:Surface,ForeColor=primary?Color.White:Ink,Cursor=Cursors.Hand,Margin=new Padding(4)};
   b.FlatAppearance.BorderColor=Line;b.Click+=action;return b;
  }
  public static DataGridView Grid(){
   var g=new DataGridView{Dock=DockStyle.Fill,BackgroundColor=Color.White,BorderStyle=BorderStyle.None,AllowUserToAddRows=false,AllowUserToDeleteRows=false,AllowUserToResizeRows=false,RowHeadersVisible=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,SelectionMode=DataGridViewSelectionMode.FullRowSelect,MultiSelect=false,Font=Font(9),EnableHeadersVisualStyles=false,CellBorderStyle=DataGridViewCellBorderStyle.SingleHorizontal,GridColor=Color.FromArgb(237,241,233)};
   g.ColumnHeadersDefaultCellStyle=new DataGridViewCellStyle{BackColor=Bg,ForeColor=Muted,Font=Font(9,true),Padding=new Padding(5)};
   g.DefaultCellStyle=new DataGridViewCellStyle{BackColor=Color.White,ForeColor=Ink,SelectionBackColor=Soft,SelectionForeColor=Ink,Padding=new Padding(5)};
   g.ColumnHeadersHeight=34;g.RowTemplate.Height=32;return g;
  }
  public static string Time(DateTime t,Day d){return t.ToString("HH:mm")+(t.Date>d.Wake.Date?" +"+(t.Date-d.Wake.Date).Days:"");}
 }
 public class Timeline : Control {
  public Plan Plan; public Day Day;
  public Timeline(){DoubleBuffered=true;Dock=DockStyle.Fill;Height=76;Font=Theme.Font(8);}
  protected override void OnPaint(PaintEventArgs e){
   base.OnPaint(e);if(Plan==null||Day==null)return;
   var g=e.Graphics;g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
   float width=Width-20; double total=Math.Max(1,(Plan.Slots.Last().End-Day.Wake).TotalMinutes);
   foreach(var s in Plan.Slots){
    float x=10+(float)((s.Start-Day.Wake).TotalMinutes/total*width),w=(float)(s.Minutes/total*width);
    Color c=s.Block.Kind=="meal"?Color.FromArgb(197,149,69):s.Block.Kind=="work"||s.Block.Kind=="study"?Theme.Accent:s.Block.Kind=="exercise"?Color.FromArgb(108,139,117):Color.FromArgb(194,204,189);
    using(var b=new SolidBrush(c))g.FillRectangle(b,x,29,Math.Max(0,w-2),19);
   }
   for(int i=0;i<=4;i++){
    var t=Day.Wake.AddMinutes(total*i/4);float x=10+width*i/4;
    using(var b=new SolidBrush(Theme.Muted))g.DrawString(Theme.Time(t,Day),Font,b,Math.Min(x,Width-72),5);
   }
   var now=DateTime.Now;
   if(now>=Day.Wake&&now<=Plan.Slots.Last().End){float x=10+(float)((now-Day.Wake).TotalMinutes/total*width);using(var p=new Pen(Theme.Ink,2))g.DrawLine(p,x,23,x,54);}
   using(var b=new SolidBrush(Theme.Muted))g.DrawString("식사     /     작업·학습     /     운동     /     생활·휴식",Font,b,10,56);
  }
 }
 public class ActualDialog:Form {
  DateTimePicker input; public DateTime Value{get{return input.Value;}}
  public ActualDialog(Slot s,Day day){Shown+=(a,b)=>Theme.Apply(this);
   Text=s.Block.Name+" · 실제 시작";Size=new Size(465,205);StartPosition=FormStartPosition.CenterParent;FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;BackColor=Theme.Bg;Font=Theme.Font(10);
   var layout=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(18),RowCount=3};layout.RowStyles.Add(new RowStyle(SizeType.Absolute,40));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,38));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));Controls.Add(layout);
   layout.Controls.Add(Theme.Label("예정 "+s.Start.ToString("MM/dd HH:mm")+"  ·  날짜도 함께 확인해 주세요.",10),0,0);
   input=new DateTimePicker{Dock=DockStyle.Fill,Format=DateTimePickerFormat.Custom,CustomFormat="yyyy-MM-dd HH:mm",ShowUpDown=true,Value=s.Start};layout.Controls.Add(input,0,1);
   var row=new FlowLayoutPanel{Dock=DockStyle.Fill};row.Controls.Add(Theme.Button("기록하고 재배치",(a,b)=>{DialogResult=DialogResult.OK;Close();},true));row.Controls.Add(Theme.Button("취소",(a,b)=>Close()));layout.Controls.Add(row,0,2);
  }
 }
 public class SettingsDialog:Form {
  State copy;DataGridView grid;NumericUpDown day,threshold,reminder,focus;TextBox work,study; public State Result;
  public SettingsDialog(State state){Shown+=(a,b)=>Theme.Apply(this);
   copy=Store.Clone(state);Text="Aiary 설정";Size=new Size(880,720);MinimumSize=new Size(800,650);StartPosition=FormStartPosition.CenterParent;BackColor=Theme.Bg;Font=Theme.Font(9);
   var root=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(20),RowCount=4};root.RowStyles.Add(new RowStyle(SizeType.Absolute,205));root.RowStyles.Add(new RowStyle(SizeType.Absolute,35));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,62));Controls.Add(root);
   var options=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=4,RowCount=5};for(int i=0;i<4;i++)options.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,25));
   day=Number(state.DayMinutes,60,1440);threshold=Number(state.ShrinkThreshold,1,240);reminder=Number(state.ReminderMinutes,0,120);focus=Number(state.FocusMinutes,1,240);
   options.Controls.Add(Theme.Label("기상~취침 목표 (분)"),0,0);options.Controls.Add(day,1,0);if(state.AdaptivePriority){threshold.Value=50;threshold.Enabled=false;}options.Controls.Add(Theme.Label(state.AdaptivePriority?"대기 버퍼 최대 (분)":"버퍼 사용 지연 (분)"),2,0);options.Controls.Add(threshold,3,0);
   options.Controls.Add(Theme.Label("사전 알림 (분)"),0,1);options.Controls.Add(reminder,1,1);options.Controls.Add(Theme.Label("집중 세션 (분)"),2,1);options.Controls.Add(focus,3,1);
   options.Controls.Add(Theme.Label("작업 URL / 문서 / 폴더"),0,2);work=new TextBox{Text=state.WorkLink??"",Dock=DockStyle.Fill};options.Controls.Add(work,1,2);options.SetColumnSpan(work,3);
   options.Controls.Add(Theme.Label("학습 URL / 문서 / 폴더"),0,3);study=new TextBox{Text=state.StudyLink??"",Dock=DockStyle.Fill};options.Controls.Add(study,1,3);options.SetColumnSpan(study,3);
   var auto=Theme.Button(Startup.Exists()?"자동 시작 끄기":"자동 시작 켜기",(s,e)=>{
    try{if(Startup.Exists()){if(MessageBox.Show(this,"이 앱의 로그인 자동 시작 등록을 해제할까요?","자동 시작",MessageBoxButtons.YesNo)==DialogResult.Yes)Startup.Disable();}
    else{if(MessageBox.Show(this,"Windows 로그인 시 Aiary를 트레이에서 실행하도록 현재 사용자 시작프로그램 폴더에 바로가기를 만들까요?","자동 시작 등록 승인",MessageBoxButtons.YesNo,MessageBoxIcon.Question)==DialogResult.Yes)Startup.Enable();}
    ((Button)s).Text=Startup.Exists()?"자동 시작 끄기":"자동 시작 켜기";}catch(Exception ex){MessageBox.Show(this,ex.Message,"자동 시작 오류");}
   });options.Controls.Add(auto,0,4);options.SetColumnSpan(auto,2);
   var portable=Theme.Label("자동 시작은 승인 후 등록 · 관리자 권한 불필요",8);options.Controls.Add(portable,2,4);options.SetColumnSpan(portable,2);root.Controls.Add(options,0,0);
   root.Controls.Add(Theme.Label(state.AdaptivePriority?"자동 조정: 대기 50분 → 3끼니 → 존2 → 5끼니 → 웨이트 · 2끼니와 최소 2시간 30분 · 끼니 최소 3시간":"기본 시간표 · 최소 길이와 버퍼 우선순위 (0=고정, 1부터 먼저 단축)",10,true),0,1);
   grid=Theme.Grid();grid.ReadOnly=false;grid.Columns.Add("name","일정");grid.Columns.Add("minutes","기본 분");grid.Columns.Add("min","최소 분");grid.Columns.Add("priority",state.AdaptivePriority?"기존 순서":"버퍼 순서");grid.Columns[3].ReadOnly=state.AdaptivePriority;grid.Columns[0].FillWeight=60;foreach(var b in state.Template){int i=grid.Rows.Add(b.Name,b.Minutes,b.Minimum,b.Priority);grid.Rows[i].Tag=b.Id;}root.Controls.Add(grid,0,2);
   var buttons=new FlowLayoutPanel{Dock=DockStyle.Fill};buttons.Controls.Add(Theme.Button("저장",(s,e)=>Apply(),true));buttons.Controls.Add(Theme.Button("취소",(s,e)=>Close()));buttons.Controls.Add(new Label{Text="기본표 변경은 새 날짜와 자동 계획 생성에 적용됩니다.\n위쪽 계획은 칸에서 직접 수정할 수 있습니다.",AutoSize=true,Margin=new Padding(12,7,0,0),ForeColor=Theme.Muted});root.Controls.Add(buttons,0,3);
  }
  NumericUpDown Number(int value,int min,int max){return new NumericUpDown{Minimum=min,Maximum=max,Value=value,Dock=DockStyle.Fill};}
  void Apply(){
   try {
    grid.EndEdit();for(int i=0;i<grid.Rows.Count;i++){var row=grid.Rows[i];var b=copy.Template.Find(x=>x.Id==(string)row.Tag);b.Name=Convert.ToString(row.Cells[0].Value).Trim();b.Minutes=Convert.ToInt32(row.Cells[1].Value);b.Minimum=Convert.ToInt32(row.Cells[2].Value);b.Priority=Convert.ToInt32(row.Cells[3].Value);}
    copy.DayMinutes=(int)day.Value;if(!copy.AdaptivePriority)copy.ShrinkThreshold=(int)threshold.Value;copy.ReminderMinutes=(int)reminder.Value;copy.FocusMinutes=(int)focus.Value;copy.WorkLink=work.Text.Trim();copy.StudyLink=study.Text.Trim();Engine.Validate(copy);foreach(var d in copy.Days)Engine.Build(copy,d);Result=copy;DialogResult=DialogResult.OK;Close();
   }catch(Exception e){Diagnostics.Error("settings",e);MessageBox.Show(this,e.Message,"설정 확인");}
  }
 }
 public static class Startup {
  public static string Link{get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup),"ARA Daily OS.lnk");}}
  public static bool Exists(){return File.Exists(Link);}
  public static void Enable(){
   CreateLink(Link,Application.ExecutablePath);
  }
  public static void CreateLink(string path,string executable){
   Type type=Type.GetTypeFromProgID("WScript.Shell");object shell=Activator.CreateInstance(type);object shortcut=null;
   try{shortcut=type.InvokeMember("CreateShortcut",System.Reflection.BindingFlags.InvokeMethod,null,shell,new object[]{path});var t=shortcut.GetType();t.InvokeMember("TargetPath",System.Reflection.BindingFlags.SetProperty,null,shortcut,new object[]{executable});t.InvokeMember("Arguments",System.Reflection.BindingFlags.SetProperty,null,shortcut,new object[]{"--tray"});t.InvokeMember("WorkingDirectory",System.Reflection.BindingFlags.SetProperty,null,shortcut,new object[]{Path.GetDirectoryName(executable)});t.InvokeMember("Save",System.Reflection.BindingFlags.InvokeMethod,null,shortcut,null);}finally{if(shortcut!=null)Marshal.FinalReleaseComObject(shortcut);Marshal.FinalReleaseComObject(shell);}
  }
  public static void Disable(){if(Exists())File.Delete(Link);}
 }
 public static class Program {
  [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
  [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr FindWindow(string cls,string title);
  [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr window,int command);
  [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr window);
  public static string DataPath(string[] args){
   int idx=Array.IndexOf(args,"--data");if(idx>=0&&idx+1<args.Length)return Path.GetFullPath(args[idx+1]);
   return Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"data","state.json");
  }
  [STAThread] public static int Main(string[] args){
   try{
    Diagnostics.Initialize(DataPath(args));Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);Application.ThreadException+=(s,e)=>{Diagnostics.UiErrorCount++;Diagnostics.ReportUiError(e.Exception,!(args.Contains("--self-test")||args.Contains("--smoke")||args.Contains("--verify-data")));};AppDomain.CurrentDomain.UnhandledException+=(s,e)=>Diagnostics.Error("unhandled-process",e.ExceptionObject as Exception);Application.ApplicationExit+=(s,e)=>Diagnostics.Action("shutdown");
    WindowsNotifications.Identity();SetProcessDPIAware();Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
    if(args.Contains("--self-test"))return Tests.Run(args);
    if(args.Contains("--smoke"))return Tests.Smoke(args);
    if(args.Contains("--verify-data"))return Tests.VerifyData(args);
    if(args.Contains("--notification-smoke"))return Tests.NotificationSmoke(args);
    if(args.Contains("--mobile-preview")){var sampleStore=new Store(DataPath(args));var sample=File.Exists(sampleStore.PathName)?sampleStore.Load():State.New();if(!File.Exists(sampleStore.PathName)){var sampleDay=sample.GetDay(DateTime.Today);sampleDay.Wake=DateTime.Today.AddHours(15);}var preview=new MainForm(sampleStore,sample,true);preview.PhoneServer.Dispose();preview.PhoneServer=new MobileServer(preview,19765);preview.Shown+=(s,e)=>{preview.StartPhoneServer(false);File.WriteAllText(Path.Combine(Path.GetDirectoryName(DataPath(args)),"test-pair-code.txt"),preview.PhoneServer.Code);preview.Hide();};Application.Run(preview);return 0;}
    string path=DataPath(args);bool created;using(var mutex=new Mutex(true,"Local\\ARA-Daily-OS-"+StableHash(path),out created)){
     if(!created&&args.Contains("--show")){var window=FindWindow(null,"Aiary · 아이어리");if(window!=IntPtr.Zero){ShowWindow(window,9);SetForegroundWindow(window);}return 0;}if(!created){MessageBox.Show("Aiary가 이미 실행 중입니다. 트레이 아이콘을 더블클릭해 주세요.");return 0;}
     var store=new Store(path);var data=store.Load();if(args.Contains("--dark"))data.DarkMode=true;if(data.ScheduleRevision<2&&File.Exists(path))File.Copy(path,Path.Combine(Path.GetDirectoryName(path),"before-routine-update-"+DateTime.Now.ToString("yyyyMMddHHmmssfff")+".json"),false);var form=new MainForm(store,data,false);store.Save(data);
     if(args.Contains("--mobile"))form.Shown+=(s,e)=>{try{form.StartPhoneServer(true);}catch(Exception ex){Diagnostics.Error("mobile-start",ex);}};
     if(args.Contains("--connect"))form.Shown+=(s,e)=>form.BeginInvoke(new Action(()=>form.PhoneConnection()));
     if(args.Contains("--tray"))form.Shown+=(s,e)=>form.Hide();Application.Run(form);mutex.ReleaseMutex();
    }
    return 0;
   }catch(Exception e){
    Diagnostics.Error("startup-or-test",e);
    if(args.Contains("--self-test")||args.Contains("--smoke")||args.Contains("--verify-data")){try{File.WriteAllText(Path.Combine(Path.GetDirectoryName(DataPath(args)),"fatal-error.txt"),e.ToString());}catch{}return 1;}
    MessageBox.Show("Aiary 실행 오류\n"+e.Message,"Aiary",MessageBoxButtons.OK,MessageBoxIcon.Error);return 1;
   }
  }
  static uint StableHash(string value){uint hash=2166136261;foreach(char c in value.ToLowerInvariant()){hash^=c;hash*=16777619;}return hash;}
 }
}

