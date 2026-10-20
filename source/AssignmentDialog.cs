using System;
using System.Linq;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;

namespace Ara {
 public class AssignmentDialog:Form {
  CellBlock draft;TextBox name;ComboBox category,meal;
  public CellBlock Result;
  public bool AutoMeal{get{return meal.SelectedIndex==0;}}
  public static readonly string[] MealValues={"","b1","b6","b10","b15","b16","b12",""};
  public static readonly object[] MealLabels={"자동 인식","1끼니","2끼니","3끼니","4끼니","5끼니","요거트","일정 반영 안 함"};
  public static int MealIndex(string id){int i=Array.IndexOf(MealValues,id??"");return Math.Max(0,i);}
  public AssignmentDialog(CellSelection selected,DateTime start,CellBlock preset,List<CellBlock> plans,State state=null){
   if(state==null)state=State.New();CategoryCatalog.Ensure(state);
   Shown+=(s,e)=>Theme.Apply(this);draft=preset==null?new CellBlock{Category="기타",Name=""}:preset.Copy();draft.Id=Guid.NewGuid().ToString("N");draft.Start=start;draft.Count=selected.Count;
   Text="상세입력";Size=new Size(500,275);StartPosition=FormStartPosition.CenterParent;FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;BackColor=Theme.Bg;Font=Theme.Font(10);
   var layout=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(16),ColumnCount=2,RowCount=5};layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,90));layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));foreach(int h in new[]{36,42,38,38,52})layout.RowStyles.Add(new RowStyle(SizeType.Absolute,h));Controls.Add(layout);
   var range=Theme.Label(start.ToString("MM/dd HH:mm")+"–"+draft.End.ToString("MM/dd HH:mm")+" · "+selected.Count+"칸",10,true);layout.Controls.Add(range,0,0);layout.SetColumnSpan(range,2);
   layout.Controls.Add(Theme.Label("내용 (선택)"),0,1);name=new TextBox{Dock=DockStyle.Fill,MaxLength=250,Text=draft.Name};layout.Controls.Add(name,1,1);
   layout.Controls.Add(Theme.Label("카테고리"),0,2);category=new ThemeComboBox{Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList};category.Items.AddRange(CategoryCatalog.Keys(state,true).Cast<object>().ToArray());category.FormattingEnabled=true;category.Format+=(s,e)=>e.Value=CategoryCatalog.Label(state.CategorySettings,Convert.ToString(e.ListItem));category.SelectedItem=draft.Category;layout.Controls.Add(category,1,2);
   layout.Controls.Add(Theme.Label("끼니"),0,3);meal=new ThemeComboBox{Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList};meal.Items.AddRange(MealLabels);meal.SelectedIndex=MealIndex(draft.MealId);layout.Controls.Add(meal,1,3);
   var buttons=new FlowLayoutPanel{Dock=DockStyle.Fill};var save=Theme.Button("저장",(s,e)=>{draft.Name=name.Text.Trim();draft.Category=Convert.ToString(category.SelectedItem);if(meal.SelectedIndex!=7&&GridEngine.MealName(draft.Name))draft.Category="식사";draft.MealId=draft.Category=="식사"?MealValues[meal.SelectedIndex]:"";draft.PrepFor=draft.Category=="끼니 준비"?draft.PrepFor:"";Result=draft;DialogResult=DialogResult.OK;Close();},true);buttons.Controls.Add(save);var cancel=Theme.Button("취소",(s,e)=>{DialogResult=DialogResult.Cancel;Close();});buttons.Controls.Add(cancel);layout.Controls.Add(buttons,0,4);layout.SetColumnSpan(buttons,2);AcceptButton=save;CancelButton=cancel;
   meal.SelectedIndexChanged+=(s,e)=>{if(meal.SelectedIndex>0&&meal.SelectedIndex<7)category.SelectedItem="식사";};
  }
 }
}
