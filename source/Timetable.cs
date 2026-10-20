using System;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Ara {
 public class CellSelection {
  public int Lane,First,Last;
  public int Count{get{return Last-First+1;}}
 }
 public class ScoreRequest:EventArgs {public decimal Score;}
 public class ResizeRequest:EventArgs {public int Lane,Count;public string Id;public DateTime Start;}
 public class MoveRequest:EventArgs {public int Lane;public string Id;public DateTime Start;}
 public class PaletteRequest:EventArgs {public CellSelection Selection;public CellBlock Item;}
 public class Timetable:Control {
  public const int Header=32,HourHeight=70,Gutter=100,Hours=48;
  public DateTime Origin{get;set;}
  public DateTime? EstimatedSleep{get;set;}
  public decimal BlankSatisfaction=2;public bool IncludeBlankSatisfaction=true;public DateTime SatisfactionFrom=DateTime.MinValue,SatisfactionTo=DateTime.MaxValue;
  DateTime currentTime=DateTime.Now;
  public int CurrentIndex{get{int index=(int)Math.Floor((currentTime-Origin).TotalMinutes/10);return index>=0&&index<Hours*6?index:-1;}}
  public void SetCurrentTime(DateTime time){int previous=CurrentIndex;currentTime=time;if(previous!=CurrentIndex)Invalidate();}
  public List<CellBlock> Planned=new List<CellBlock>(),Recorded=new List<CellBlock>();
  public CellSelection Selection;
  public bool ForceSelection;
  public Dictionary<int,decimal> AdherenceScores=new Dictionary<int,decimal>();
  public string DisplayMode="category";public List<CategoryDefinition> Catalog;
  public event EventHandler<ResizeRequest> ResizeRequested;
  string resizeEdge;
  public event EventHandler SelectionChanged;
  public event EventHandler<MoveRequest> MoveRequested;
  public event EventHandler<PaletteRequest> PaletteRequested;
  public event EventHandler EditRequested;
  public event EventHandler DeleteRequested;
  public event EventHandler<ScoreRequest> ScoreRequested;
  string scoreKeys="";DateTime scoreKeyTime=DateTime.MinValue;
  public void ResetScoreKeys(){scoreKeys="";scoreKeyTime=DateTime.MinValue;}
  int anchor,lane,sourceOffset,moveTarget;Point down;
  bool pressing,moving,moved,reclick,copying;CellBlock source;CellSelection priorSelection;
  VScrollBar vertical;
  int wheelDelta;
  public Point AutoScrollPosition {get{return new Point(0,vertical==null?0:-vertical.Value);}set{SetScroll(value.Y);}}
  public Size AutoScrollMinSize{get;set;}
  System.Windows.Forms.Timer scroll;
  Font hourFont=Theme.Font(9,true);
  public Timetable(){
   SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);
   DoubleBuffered=true;Dock=DockStyle.Fill;BackColor=Theme.Surface;AllowDrop=false;TabStop=true;
   AutoScrollMinSize=new Size(0,Header+Hours*HourHeight+10);Font=Theme.Font(8);AccessibleName="10분 계획 및 실제 타임테이블";
   vertical=new VScrollBar{TabStop=false};vertical.ValueChanged+=(s,e)=>Invalidate();Controls.Add(vertical);LayoutScroll();
   scroll=new System.Windows.Forms.Timer{Interval=70};scroll.Tick+=(s,e)=>AutoDragScroll();
  }
  void LayoutScroll(){if(vertical==null)return;int viewport=Math.Max(1,ClientSize.Height-Header);vertical.SetBounds(Math.Max(0,ClientSize.Width-SystemInformation.VerticalScrollBarWidth),Header,SystemInformation.VerticalScrollBarWidth,viewport);int old=vertical.Value;vertical.Minimum=0;vertical.Maximum=Hours*HourHeight+9;vertical.LargeChange=viewport;vertical.SmallChange=HourHeight/2;vertical.Value=Math.Min(old,Math.Max(0,vertical.Maximum-vertical.LargeChange+1));Invalidate();}
  void SetScroll(int value){if(vertical==null)return;int max=Math.Max(0,vertical.Maximum-vertical.LargeChange+1);vertical.Value=Math.Max(0,Math.Min(max,value));Invalidate();}
  protected override void OnResize(EventArgs e){base.OnResize(e);LayoutScroll();}
  protected override void OnMouseWheel(MouseEventArgs e){base.OnMouseWheel(e);wheelDelta+=e.Delta;int steps=wheelDelta/120;wheelDelta%=120;if(steps!=0)ScrollBy(-steps*(HourHeight/2)*Math.Max(1,SystemInformation.MouseWheelScrollLines));}
  public void SetData(DateTime origin,List<CellBlock> plan,List<CellBlock> records){Origin=origin.Date;Planned=plan;Recorded=records;Invalidate();}
  public DateTime TimeAt(int index){return Origin.AddMinutes(index*10);}
  public CellBlock At(int selectedLane,int index){DateTime time=TimeAt(index);return (selectedLane==0?Planned:Recorded).Find(x=>time>=x.Start&&time<x.End);}
  public int Index(DateTime time){return (int)((time-Origin).TotalMinutes/10);}
  int GridWidth{get{return Math.Max(6,ClientSize.Width-Gutter-18);}}
  public Rectangle CellRect(int selectedLane,int index){int hour=index/6,col=index%6;int x=Gutter+GridWidth*col/6,x2=Gutter+GridWidth*(col+1)/6;return new Rectangle(x,Header+hour*HourHeight+selectedLane*(HourHeight/2)+AutoScrollPosition.Y,x2-x,HourHeight/2);}
  public CellSelection Hit(Point p,int lockedLane){
   if(p.X<Gutter||p.X>=Gutter+GridWidth||p.Y<Header)return null;
   int y=p.Y-AutoScrollPosition.Y-Header;
   int hour=Math.Max(0,Math.Min(Hours-1,y/HourHeight));
   int cell=Math.Max(0,Math.Min(5,(p.X-Gutter)*6/GridWidth));
   int chosen=lockedLane>=0?lockedLane:(y%HourHeight>=HourHeight/2?1:0);
   return new CellSelection{Lane=chosen,First=hour*6+cell,Last=hour*6+cell};
  }
  public void SetSelection(CellSelection value){ResetScoreKeys();Selection=value;Invalidate();if(SelectionChanged!=null)SelectionChanged(this,EventArgs.Empty);}
  public void Jump(DateTime t){int hour=Math.Max(0,Math.Min(Hours-1,(int)(t-Origin).TotalHours));AutoScrollPosition=new Point(0,Math.Max(0,hour*HourHeight));Invalidate();}
  public void ScrollBy(int amount){int limit=Math.Max(0,AutoScrollMinSize.Height-ClientSize.Height);AutoScrollPosition=new Point(0,Math.Max(0,Math.Min(limit,-AutoScrollPosition.Y+amount)));Invalidate();}
  protected override void OnMouseDown(MouseEventArgs e){
   base.OnMouseDown(e);if(e.Button!=MouseButtons.Left)return;
   var hit=Hit(e.Location,-1);if(hit==null)return;Focus();Capture=true;pressing=true;moved=false;copying=false;lane=hit.Lane;anchor=hit.First;down=e.Location;source=At(lane,anchor);
   priorSelection=Selection;resizeEdge=null;if(source!=null&&source.Category!="기상"&&Selection!=null&&Selection.Lane==lane&&Selection.First==Index(source.Start)&&Selection.Last==Index(source.End)-1){var startRect=CellRect(lane,Selection.First);var endRect=CellRect(lane,Selection.Last);if(anchor==Selection.First&&e.X<startRect.Left+14)resizeEdge="start";else if(anchor==Selection.Last&&e.X>endRect.Right-14)resizeEdge="end";}
   reclick=Selection!=null&&Selection.Lane==lane&&anchor>=Selection.First&&anchor<=Selection.Last&&(ModifierKeys&Keys.Shift)==0;
   moving=source!=null&&!ForceSelection&&(ModifierKeys&Keys.Shift)==0;
   if(moving){sourceOffset=anchor-Index(source.Start);moveTarget=Index(source.Start);if(!reclick)SetSelection(new CellSelection{Lane=lane,First=Index(source.Start),Last=Index(source.End)-1});}
   else if(!reclick)SetSelection(hit);
   scroll.Start();
  }
  protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);UpdateDrag(e.Location);}
  void UpdateDrag(Point point){
   if(!pressing)return;if(Math.Abs(point.X-down.X)>4||Math.Abs(point.Y-down.Y)>4)moved=true;
   var pointClamped=new Point(Math.Max(Gutter,Math.Min(Gutter+GridWidth-1,point.X)),Math.Max(Header,Math.Min(ClientSize.Height-1,point.Y)));
   var raw=Hit(pointClamped,-1);var hit=Hit(pointClamped,lane);if(hit==null)return;
   if(hit.First!=anchor)moved=true;
   if(!moved)return;
   if(resizeEdge!=null){Selection=BlockTools.Clamp(lane==0?Planned:Recorded,source,lane,resizeEdge,hit.First,Origin);Invalidate();}
   else if(moving){
    copying=lane==0&&raw.Lane==1;
    moveTarget=Math.Max(0,Math.Min(Hours*6-source.Count,hit.First-sourceOffset));
    if(copying&&priorSelection!=null&&priorSelection.Lane==1&&raw.First>=priorSelection.First&&raw.First<=priorSelection.Last)Selection=priorSelection;
    else Selection=new CellSelection{Lane=copying?1:lane,First=moveTarget,Last=moveTarget+source.Count-1};Invalidate();
   }
   else SetSelection(new CellSelection{Lane=lane,First=Math.Min(anchor,hit.First),Last=Math.Max(anchor,hit.First)});
  }
  protected override void OnMouseUp(MouseEventArgs e){
   base.OnMouseUp(e);if(!pressing)return;scroll.Stop();pressing=false;Capture=false;
   Diagnostics.Action("pointer-release","lane="+lane+"; moved="+moved+"; copy="+copying+"; reclick="+reclick+"; range="+(Selection==null?"none":Selection.First+"-"+Selection.Last));
   if(resizeEdge!=null){if(moved&&ResizeRequested!=null)ResizeRequested(this,new ResizeRequest{Lane=lane,Id=source.Id,Start=TimeAt(Selection.First),Count=Selection.Count});resizeEdge=null;}
   else if(!moved&&reclick&&EditRequested!=null)EditRequested(this,EventArgs.Empty);
   else if(moving&&moved&&source!=null&&copying&&PaletteRequested!=null)PaletteRequested(this,new PaletteRequest{Selection=Selection,Item=source.Copy()});
   else if(moving&&moved&&source!=null&&moveTarget!=Index(source.Start)&&MoveRequested!=null)MoveRequested(this,new MoveRequest{Lane=lane,Id=source.Id,Start=TimeAt(moveTarget)});
   moving=false;source=null;if(SelectionChanged!=null)SelectionChanged(this,EventArgs.Empty);Invalidate();
  }
  protected override void OnMouseCaptureChanged(EventArgs e){base.OnMouseCaptureChanged(e);if(!Capture){scroll.Stop();pressing=false;}}
  protected override void OnKeyDown(KeyEventArgs e){
   base.OnKeyDown(e);if(Selection==null)return;
   if(e.KeyCode==Keys.Delete){Diagnostics.Action("delete-key");ResetScoreKeys();if(DeleteRequested!=null)DeleteRequested(this,EventArgs.Empty);e.Handled=true;e.SuppressKeyPress=true;return;}
   if(Selection.Lane==1&&!e.Control&&!e.Alt&&!e.Shift){
    int digit=e.KeyCode>=Keys.D0&&e.KeyCode<=Keys.D9?(int)e.KeyCode-(int)Keys.D0:e.KeyCode>=Keys.NumPad0&&e.KeyCode<=Keys.NumPad9?(int)e.KeyCode-(int)Keys.NumPad0:-1;
    if(digit>=0||e.KeyCode==Keys.Decimal||e.KeyCode==Keys.OemPeriod||e.KeyCode==Keys.Subtract||e.KeyCode==Keys.OemMinus||e.KeyCode==Keys.Back){
     if((DateTime.Now-scoreKeyTime).TotalMilliseconds>1200)scoreKeys="";scoreKeyTime=DateTime.Now;
     string next=scoreKeys;if(digit>=0)next+=digit.ToString();else if(e.KeyCode==Keys.Back)next=next.Length>0?next.Substring(0,next.Length-1):"";else if(e.KeyCode==Keys.Subtract||e.KeyCode==Keys.OemMinus)next=next.StartsWith("-")?next.Substring(1):"-"+next;else if(!next.Contains("."))next+=(next==""?"0.":".");
     decimal value;if(next==""||next=="-")value=0;else if(!decimal.TryParse(next,System.Globalization.NumberStyles.AllowLeadingSign|System.Globalization.NumberStyles.AllowDecimalPoint,System.Globalization.CultureInfo.InvariantCulture,out value)){e.SuppressKeyPress=true;return;}
     if(value>=-100000&&value<=100000){scoreKeys=next;if(ScoreRequested!=null)ScoreRequested(this,new ScoreRequest{Score=value});}e.Handled=true;e.SuppressKeyPress=true;return;
    }
   }
   if(e.KeyCode==Keys.Enter){ResetScoreKeys();e.SuppressKeyPress=true;return;}
   if(e.KeyCode==Keys.Escape){SetSelection(null);e.Handled=true;}
   else if(e.KeyCode==Keys.Left||e.KeyCode==Keys.Right||e.KeyCode==Keys.Up||e.KeyCode==Keys.Down){int delta=e.KeyCode==Keys.Left?-1:e.KeyCode==Keys.Right?1:e.KeyCode==Keys.Up?-6:6;int target=Math.Max(0,Math.Min(Hours*6-1,Selection.Last+delta));SetSelection(new CellSelection{Lane=Selection.Lane,First=e.Shift?Math.Min(Selection.First,target):target,Last=e.Shift?Math.Max(Selection.First,target):target});e.Handled=true;}
  }
  void AutoDragScroll(){if(!pressing)return;Point p=PointToClient(Cursor.Position);int change=p.Y>ClientSize.Height-25?35:p.Y<Header+25?-35:0;if(change!=0){ScrollBy(change);UpdateDrag(p);}}
  public bool IsLightsOut(int index){return EstimatedSleep.HasValue&&TimeAt(index)>=EstimatedSleep.Value.AddHours(-2)&&TimeAt(index)<EstimatedSleep.Value;}
  public static Color LightsColor(Color original){return Theme.Dark?Color.FromArgb((original.R*3+12)/4,(original.G*3+18)/4,(original.B*3+30)/4):Color.FromArgb((original.R*4+87)/5,(original.G*4+96)/5,(original.B*4+132)/5);}
  public static Color CategoryColor(string category){if(Theme.Dark)return category=="식사"?Color.FromArgb(67,51,30):category=="작업"?Color.FromArgb(29,58,42):category=="학습"?Color.FromArgb(34,48,72):category=="존2"||category=="웨이트"?Color.FromArgb(43,56,30):category=="휴식"?Color.FromArgb(58,41,58):Color.FromArgb(33,44,37);return category=="식사"?Color.FromArgb(241,222,187):category=="작업"?Color.FromArgb(211,231,220):category=="학습"?Color.FromArgb(219,227,242):category=="존2"||category=="웨이트"?Color.FromArgb(222,229,207):category=="휴식"?Color.FromArgb(232,224,233):Color.FromArgb(228,232,229);}
  protected override void OnPaint(PaintEventArgs e){
   base.OnPaint(e);var g=e.Graphics;
   int first=Math.Max(0,(-AutoScrollPosition.Y)/HourHeight-1),last=Math.Min(Hours-1,(-AutoScrollPosition.Y+Height)/HourHeight+1);
   using(var pen=new Pen(Theme.Line))using(var thin=new Pen(Theme.Line))using(var text=new SolidBrush(Theme.Ink))using(var muted=new SolidBrush(Theme.Muted)){
    thin.DashStyle=System.Drawing.Drawing2D.DashStyle.Dot;
    for(int h=first;h<=last;h++){
     int y=Header+h*HourHeight+AutoScrollPosition.Y;
     using(var gray=new SolidBrush(Theme.ActualSurface))g.FillRectangle(gray,Gutter,y+HourHeight/2,GridWidth,HourHeight/2);
     TextRenderer.DrawText(g,Origin.AddHours(h).ToString("HH:00")+(h>=24?" +1":""),hourFont,new Rectangle(0,y,57,HourHeight),Theme.Ink,TextFormatFlags.VerticalCenter|TextFormatFlags.HorizontalCenter);
     TextRenderer.DrawText(g,"계획",Font,new Rectangle(57,y,43,HourHeight/2),Theme.Muted,TextFormatFlags.VerticalCenter|TextFormatFlags.HorizontalCenter);
     TextRenderer.DrawText(g,"실제",Font,new Rectangle(57,y+HourHeight/2,43,HourHeight/2),Theme.Muted,TextFormatFlags.VerticalCenter|TextFormatFlags.HorizontalCenter);
     for(int l=0;l<2;l++)for(int col=0;col<6;col++){
      int idx=h*6+col;Rectangle r=CellRect(l,idx);var block=At(l,idx);bool lights=IsLightsOut(idx);if(lights){using(var night=new SolidBrush(LightsColor(l==0?Theme.Surface:Theme.ActualSurface)))g.FillRectangle(night,r.X+1,r.Y+1,r.Width-1,r.Height-1);}
      bool emptySatisfaction=block==null&&l==1&&DisplayMode=="satisfaction"&&IncludeBlankSatisfaction&&TimeAt(idx)>=SatisfactionFrom&&TimeAt(idx)<SatisfactionTo;if(emptySatisfaction)block=new CellBlock{Category="",Name="",Satisfaction=BlankSatisfaction};
      if(block!=null){var blockFill=CategoryCatalog.Fill(Catalog,block,DisplayMode,l==1);Color blockInk=blockFill.R*.2126+blockFill.G*.7152+blockFill.B*.0722<140?Color.White:Color.FromArgb(32,59,53);using(var fill=new SolidBrush(lights?LightsColor(blockFill):blockFill))g.FillRectangle(fill,r.X+1,r.Y+1,r.Width-1,r.Height-1);
       string caption=emptySatisfaction?BlankSatisfaction.ToString("0.#"):CategoryCatalog.Caption(Catalog,block);
       if(l==1&&!emptySatisfaction){string ratings=block.Score.ToString("0.#")+"·"+block.Satisfaction.ToString("0.#")+"·"+(AdherenceScores.ContainsKey(idx)?AdherenceScores[idx].ToString("0.#"):"—");TextRenderer.DrawText(g,caption,Font,new Rectangle(r.X+3,r.Y+1,r.Width-6,r.Height/2),blockInk,TextFormatFlags.VerticalCenter|TextFormatFlags.HorizontalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.SingleLine);TextRenderer.DrawText(g,ratings,Font,new Rectangle(r.X+3,r.Y+r.Height/2-1,r.Width-6,r.Height/2),blockInk,TextFormatFlags.VerticalCenter|TextFormatFlags.HorizontalCenter|TextFormatFlags.SingleLine);}else TextRenderer.DrawText(g,caption,Font,new Rectangle(r.X+5,r.Y+2,r.Width-9,r.Height-4),blockInk,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.SingleLine);
      }
      if(l==0&&EstimatedSleep.HasValue&&idx==Index(EstimatedSleep.Value)){
       using(var fill=new SolidBrush(Theme.SleepColor))g.FillRectangle(fill,r.X+1,r.Y+1,r.Width-1,r.Height-1);
       string caption=block==null?"수면 예상":block.Caption+" / 수면 예상";
       TextRenderer.DrawText(g,caption,Font,new Rectangle(r.X+5,r.Y+2,r.Width-9,r.Height-4),Theme.SleepInk,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.SingleLine);
      }
      if(idx==CurrentIndex){Color nowColor=Theme.Dark?Color.FromArgb(185,160,100):Color.FromArgb(162,131,60);using(var nowPen=new Pen(Color.FromArgb(150,nowColor),1)){nowPen.DashStyle=System.Drawing.Drawing2D.DashStyle.Dot;g.DrawRectangle(nowPen,r.X+4,r.Y+4,r.Width-8,r.Height-8);}using(var dot=new SolidBrush(nowColor))g.FillEllipse(dot,r.Right-9,r.Y+5,4,4);}
      if(Selection!=null&&Selection.Lane==l&&idx>=Selection.First&&idx<=Selection.Last){using(var fill=new SolidBrush(Color.FromArgb(65,38,108,81)))g.FillRectangle(fill,r);using(var selected=new Pen(Theme.Accent,2))g.DrawRectangle(selected,r.X+1,r.Y+1,r.Width-2,r.Height-2);}
      if(Selection!=null&&Selection.Lane==l&&!emptySatisfaction&&block!=null&&block.Category!="기상"&&Selection.First==Index(block.Start)&&Selection.Last==Index(block.End)-1&&(idx==Selection.First||idx==Selection.Last)){int x=idx==Selection.First?r.Left+3:r.Right-7;using(var handle=new SolidBrush(Theme.Accent))g.FillRectangle(handle,x,r.Top+10,4,r.Height-20);}
      g.DrawLine(thin,r.Right,r.Top,r.Right,r.Bottom);
     }
     g.DrawLine(pen,0,y,Gutter+GridWidth,y);g.DrawLine(pen,Gutter,y+HourHeight/2,Gutter+GridWidth,y+HourHeight/2);
     g.DrawLine(pen,0,y+HourHeight,Gutter+GridWidth,y+HourHeight);g.DrawLine(pen,Gutter,y,Gutter,y+HourHeight);
    }
    // Fixed minute header stays visible while hours scroll.
    using(var bg=new SolidBrush(Theme.Bg))g.FillRectangle(bg,0,0,ClientSize.Width,Header);
    TextRenderer.DrawText(g,"시간 / 줄",Font,new Rectangle(0,0,Gutter,Header),Theme.Muted,TextFormatFlags.VerticalCenter|TextFormatFlags.HorizontalCenter);
    for(int col=0;col<6;col++){int x=Gutter+GridWidth*col/6,x2=Gutter+GridWidth*(col+1)/6;TextRenderer.DrawText(g,(col*10).ToString("00")+"–"+(col*10+10).ToString("00")+"분",Font,new Rectangle(x,0,x2-x,Header),Theme.Ink,TextFormatFlags.VerticalCenter|TextFormatFlags.HorizontalCenter);}
    g.DrawLine(pen,0,Header-1,ClientSize.Width,Header-1);
   }
  }
  protected override void Dispose(bool disposing){if(disposing){scroll.Dispose();hourFont.Dispose();}base.Dispose(disposing);}
  public void SimulateDrag(int selectedLane,int start,int finish,bool force){
   ForceSelection=force;Jump(TimeAt(start));var a=CellRect(selectedLane,start);OnMouseDown(new MouseEventArgs(MouseButtons.Left,1,a.X+a.Width/2,a.Y+a.Height/2,0));
   Jump(TimeAt(finish));var b=CellRect(selectedLane,finish);OnMouseMove(new MouseEventArgs(MouseButtons.Left,0,b.X+b.Width/2,b.Y+b.Height/2,0));OnMouseUp(new MouseEventArgs(MouseButtons.Left,1,b.X+b.Width/2,b.Y+b.Height/2,0));ForceSelection=false;
  }
 }
}
