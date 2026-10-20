using System;
using System.Linq;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;

namespace Ara {
 public partial class MainForm {
  ContextMenuStrip categoryMenu;
  public ContextMenuStrip CurrentCategoryMenu{get{return categoryMenu;}}
  protected override void Dispose(bool disposing){if(disposing&&categoryMenu!=null){categoryMenu.Dispose();categoryMenu=null;}base.Dispose(disposing);}
  void RegenerateCore(DateTime time){PlanHistory.Reset(Day);Day.Wake=GridEngine.Snap(time);Day.GridBase=GridEngine.Generate(Data,Day);Day.GridReady=true;Day.StartAnchoredMeals=Data.ScheduleRevision>=2;}
  public void GenerateAt(DateTime time){Diagnostics.Action("generate",time.ToString("o"));Change(()=>RegenerateCore(time));Board.SetSelection(null);Board.Jump(Day.Wake.AddHours(-1));}
  public void SetWakeCell(CellSelection selected){
   if(selected==null)return;DateTime start=Board.TimeAt(selected.First);Diagnostics.Action("wake-cell",start.ToString("o"));
   if(start.Date!=DateTime.Parse(Day.Date).Date){if(smoke)throw new InvalidOperationException("기상 날짜를 먼저 선택해 주세요.");MessageBox.Show(this,"기상 날짜를 먼저 위 날짜 선택에서 바꿔 주세요.","기상 설정");return;}
   Change(()=>{RegenerateCore(start);if(selected.Lane==1){var previous=Day.Records.Find(x=>GridEngine.Overlap(x,start,start.AddMinutes(10)));decimal score=previous==null?0:previous.Score;Day.Records.RemoveAll(x=>x.Category=="기상");GridEngine.SetRange(Day.Records,new CellBlock{Id=Guid.NewGuid().ToString("N"),Start=start,Count=1,Name="기상",Category="기상",Score=score,Satisfaction=previous==null?2:previous.Satisfaction});}});
   Board.SetSelection(new CellSelection{Lane=selected.Lane,First=selected.First,Last=selected.First});Board.Jump(start.AddHours(-1));Board.Focus();
  }
  public ContextMenuStrip CreateCategoryMenu(CellSelection selected){
   var menu=new ContextMenuStrip{Font=Theme.Font(10),ShowImageMargin=false};menu.PreviewKeyDown+=(s,e)=>{if(e.KeyCode==Keys.Delete)e.IsInputKey=true;};menu.KeyDown+=(s,e)=>{if(e.KeyCode==Keys.Delete){menu.Close();DeleteSelected();e.Handled=true;e.SuppressKeyPress=true;}};
   foreach(string label in CategoryCatalog.Keys(Data)){string categoryName=label;var item=new ToolStripMenuItem(CategoryCatalog.Label(Data.CategorySettings,label));item.Click+=(s,e)=>ApplyCategory(selected,categoryName);menu.Items.Add(item);}
   menu.Items.Add(new ToolStripSeparator());menu.Items.Add("블록 길이 조절",null,(s,e)=>ShowLengthMenu(selected));menu.Items.Add("카테고리 관리",null,(s,e)=>ManageCategories());menu.Items.Add("상세입력",null,(s,e)=>ShowAssignment(selected,null));Theme.ApplyMenu(menu);return menu;
  }
  public void ShowCategories(){
   var selected=Board.Selection;if(selected==null)return;Board.ResetScoreKeys();if(categoryMenu!=null){categoryMenu.Close();categoryMenu.Dispose();}var menu=CreateCategoryMenu(selected);categoryMenu=menu;menu.Closed+=(s,e)=>Diagnostics.Action("menu-close",e.CloseReason.ToString());Diagnostics.Action("menu-open");
   Rectangle cell=Board.CellRect(selected.Lane,selected.Last);Point where=new Point(Math.Max(Timetable.Gutter,Math.Min(Board.Width-120,cell.Left)),Math.Max(Timetable.Header,Math.Min(Board.Height-15,cell.Bottom)));
   menu.Show(Board,where);
  }
  public void ApplyQuickScore(decimal value){
   var selected=Board.Selection;if(selected==null||selected.Lane!=1)return;
   Diagnostics.Action("score",value.ToString(System.Globalization.CultureInfo.InvariantCulture));Change(()=>GridEngine.SetScore(Day.Records,Board.TimeAt(selected.First),selected.Count,value));
   footer.Text="칸당 "+value.ToString("0.##")+"점 · "+selected.Count+"칸 자동 저장 · 숫자를 이어 누르면 여러 자리 입력";
  }
  public void ApplyCategory(CellSelection selected,string label){
   if(selected==null)return;Diagnostics.Action("category",label);if(label=="기상"){SetWakeCell(selected);return;}
   var previous=Board.At(selected.Lane,selected.First);
   var entry=new CellBlock{Id=Guid.NewGuid().ToString("N"),Start=Board.TimeAt(selected.First),Count=selected.Count,Name=previous!=null&&previous.Category==label?previous.Name:"",Category=label};
   if(previous!=null&&previous.Start==entry.Start&&previous.Count==entry.Count)entry.Id=previous.Id;
   if(label=="식사")entry.MealId=previous!=null&&previous.Category=="식사"&&!string.IsNullOrEmpty(previous.MealId)?previous.MealId:GridEngine.DetectMeal(Day,GridPlan,entry);
   Defaults(entry);AssignPreservingScores(selected,entry);Board.ResetScoreKeys();Board.Focus();
  }
  public void CopyPlanned(PaletteRequest request){
   Diagnostics.Action("plan-copy",request.Item.Category+" "+request.Item.MealId+" source="+request.Item.Start.ToString("o")+" target="+request.Selection.First+"-"+request.Selection.Last);
   if(request.Item.Category=="기상"){SetWakeCell(request.Selection);return;}
   var entry=request.Item.Copy();entry.Id=Guid.NewGuid().ToString("N");entry.Start=Board.TimeAt(request.Selection.First);entry.Count=request.Selection.Count;AssignPreservingScores(request.Selection,entry);Board.ResetScoreKeys();Board.Focus();
  }
  public void DeleteSelected(){
   var selected=Board.Selection;if(selected==null)return;
   Diagnostics.Action("delete-selection",selected.Lane+":"+selected.First+"-"+selected.Last);
   Change(()=>{var visible=selected.Lane==0?GridPlan.DisplayBlocks:Day.Records;var list=selected.Lane==0?GridPlan.DisplayBlocks.Select(x=>x.Copy()).ToList():Day.Records;
    DateTime start=Board.TimeAt(selected.First),end=Board.TimeAt(selected.Last+1);
    var removed=visible.Where(x=>GridEngine.Overlap(x,start,end)).Select(x=>x.Id).ToList();
    var meals=list.Where(x=>removed.Contains(x.Id)&&!string.IsNullOrEmpty(x.MealId)).Select(x=>x.MealId).ToList();
    list.RemoveAll(x=>removed.Contains(x.Id)||(selected.Lane==0&&meals.Contains(x.PrepFor)));
    if(selected.Lane==0)Day.GridBase=list;
   });Board.SetSelection(null);Board.Focus();
  }
  void AssignPreservingScores(CellSelection selected,CellBlock entry){
   if(entry.Category=="기상"){SetWakeCell(selected);return;}
   if(selected.Lane==0){SaveEntry(selected,entry);return;}
   Change(()=>{
    var originals=Day.Records.Select(x=>x.Copy()).ToList();DateTime cursor=entry.Start;bool first=true;
    while(cursor<entry.End){var old=originals.Find(x=>GridEngine.Overlap(x,cursor,cursor.AddMinutes(10)));decimal score=old==null?0:old.Score;decimal satisfaction=old==null?2:old.Satisfaction;DateTime begin=cursor;
     do{cursor=cursor.AddMinutes(10);var next=originals.Find(x=>GridEngine.Overlap(x,cursor,cursor.AddMinutes(10)));if(cursor>=entry.End||(next==null?0:next.Score)!=score||(next==null?2:next.Satisfaction)!=satisfaction)break;}while(true);
     var piece=entry.Copy();piece.Start=begin;piece.Count=(int)((cursor-begin).TotalMinutes/10);piece.Score=score;piece.Satisfaction=satisfaction;if(!first){piece.Id=Guid.NewGuid().ToString("N");piece.MealId="";}GridEngine.SetRange(Day.Records,piece);first=false;
    }
   });
  }
 }
}
