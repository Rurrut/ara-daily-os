using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
namespace Ara {
 public partial class MainForm {
  void SetupChecklistDrag(){
   Point down=Point.Empty;string dragged=null;int first=-1;CellSelection prior=null;
   tasks.MouseDown+=(s,e)=>{var h=tasks.HitTest(e.X,e.Y);dragged=e.Button==MouseButtons.Left&&h.RowIndex>=0&&h.ColumnIndex==1?(string)tasks.Rows[h.RowIndex].Tag:null;down=e.Location;};
   tasks.MouseMove+=(s,e)=>{if(dragged==null||e.Button!=MouseButtons.Left||Math.Abs(e.X-down.X)+Math.Abs(e.Y-down.Y)<8)return;string id=dragged;dragged=null;prior=Board.Selection;first=-1;tasks.DoDragDrop(new DataObject("ARA.Checklist",id),DragDropEffects.Copy);};
   Board.AllowDrop=true;Board.DragOver+=(s,e)=>{e.Effect=DragDropEffects.None;if(!e.Data.GetDataPresent("ARA.Checklist"))return;var point=Board.PointToClient(new Point(e.X,e.Y));if(point.Y< Timetable.Header+24)Board.AutoScrollPosition=new Point(0,Math.Max(0,-Board.AutoScrollPosition.Y-20));if(point.Y>Board.Height-24)Board.AutoScrollPosition=new Point(0,-Board.AutoScrollPosition.Y+20);var hit=Board.Hit(point,-1);if(hit==null||hit.Lane!=1)return;if(first<0)first=hit.First;Board.SetSelection(new CellSelection{Lane=1,First=Math.Min(first,hit.First),Last=Math.Max(first,hit.First)});e.Effect=DragDropEffects.Copy;};
   Board.DragDrop+=(s,e)=>{if(!e.Data.GetDataPresent("ARA.Checklist"))return;var hit=Board.Hit(Board.PointToClient(new Point(e.X,e.Y)),-1);if(hit==null||hit.Lane!=1){Board.SetSelection(prior);return;}if(first<0)Board.SetSelection(hit);RecordTask((string)e.Data.GetData("ARA.Checklist"),Board.Selection);first=-1;};
  }
  public void RecordTask(string id,CellSelection selection){if(selection==null||selection.Lane!=1)return;Change(()=>PhoneEditing.Apply(Data,Day,new PhoneCommand{op="task-record",taskId=id,lane=1,first=selection.First,last=selection.Last}));}
  void ChecklistMenu(ContextMenuStrip menu,CellSelection selected){
   if(selected.Lane!=1)return;var list=new ToolStripMenuItem("체크리스트");foreach(var task in Data.Tasks){string id=task.Id;list.DropDownItems.Add(task.Text,null,(s,e)=>RecordTask(id,selected));}if(list.DropDownItems.Count==0)list.DropDownItems.Add(new ToolStripMenuItem("오른쪽에서 할 일을 추가하세요"){Enabled=false});menu.Items.Add(list);
   var linked=Day.Records.Where(b=>GridEngine.Overlap(b,Board.TimeAt(selected.First),Board.TimeAt(selected.Last+1))).Select(b=>b.TaskId).Where(id=>!string.IsNullOrEmpty(id)).Distinct();
   foreach(var id in linked){var task=Data.Tasks.Find(t=>t.Id==id);if(task==null)continue;string taskId=id;var check=new CheckBox{Text="완료 · "+task.Text,Checked=task.DoneDates.Contains(Day.Date),AutoSize=true,Padding=new Padding(5)};check.CheckedChanged+=(s,e)=>Change(()=>Checklist.Edit(Data,Day.Date,new PhoneCommand{op="task-done",taskId=taskId,done=check.Checked}));menu.Items.Add(new ToolStripControlHost(check));}
  }
 }
}
