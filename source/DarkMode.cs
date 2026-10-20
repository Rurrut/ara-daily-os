using System;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;
namespace Ara {
 public class ThemeComboBox:ComboBox {
  public ThemeComboBox(){DrawMode=DrawMode.OwnerDrawFixed;}
  protected override void OnDrawItem(DrawItemEventArgs e){if(e.Bounds.Width<=0||e.Bounds.Height<=0)return;Color back=(e.State&DrawItemState.Selected)!=0?Theme.Soft:Theme.Surface;using(var brush=new SolidBrush(back))e.Graphics.FillRectangle(brush,e.Bounds);string text=e.Index>=0&&e.Index<Items.Count?GetItemText(Items[e.Index]):Text;TextRenderer.DrawText(e.Graphics,text,e.Font,new Rectangle(e.Bounds.X+3,e.Bounds.Y,e.Bounds.Width-4,e.Bounds.Height),Theme.Ink,TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis);e.DrawFocusRectangle();}
 }
 public static partial class Theme {
  public static bool Dark;
  public static Color Surface=Color.White,ActualSurface=Color.FromArgb(242,244,240),Line=Color.FromArgb(211,218,212),SleepColor=Color.FromArgb(214,209,240),SleepInk=Color.FromArgb(65,47,117);
  public static void Set(bool dark){Dark=dark;Bg=dark?Color.FromArgb(16,24,21):Color.FromArgb(245,246,240);Ink=dark?Color.FromArgb(222,234,224):Color.FromArgb(30,47,42);Muted=dark?Color.FromArgb(147,165,153):Color.FromArgb(101,113,105);Accent=dark?Color.FromArgb(45,112,85):Color.FromArgb(38,108,81);Soft=dark?Color.FromArgb(36,69,52):Color.FromArgb(225,237,225);Surface=dark?Color.FromArgb(25,36,30):Color.White;ActualSurface=dark?Color.FromArgb(21,31,26):Color.FromArgb(242,244,240);Line=dark?Color.FromArgb(48,64,53):Color.FromArgb(211,218,212);SleepColor=dark?Color.FromArgb(62,47,85):Color.FromArgb(214,209,240);SleepInk=dark?Color.FromArgb(231,220,255):Color.FromArgb(65,47,117);}
  [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr window,int attribute,ref int value,int size);
  public static void TitleBar(Form form){if(!form.IsHandleCreated)return;try{int dark=Dark?1:0;DwmSetWindowAttribute(form.Handle,20,ref dark,4);}catch{}}
  public static void Apply(Control control){
   string role=control.Tag as string;if(role=="pastel")return;
   if(control is Label&&(role==null||role.StartsWith("theme-"))){if(role==null&&(control.ForeColor==Color.FromArgb(101,113,105)||control.ForeColor==Color.FromArgb(147,165,153))){control.Tag="theme-muted";role="theme-muted";}control.ForeColor=control.Parent!=null&&Convert.ToString(control.Parent.Tag)=="theme-primary"?Color.White:role=="theme-muted"?Muted:Ink;}
   else control.ForeColor=Ink;
   control.BackColor=control is Label?Color.Transparent:role=="theme-primary"?Accent:control is Timetable||control is TextBoxBase||control is ComboBox||control is UpDownBase||control is Button?Surface:Bg;
   var button=control as Button;if(button!=null){button.ForeColor=role=="theme-primary"?Color.White:Ink;button.FlatAppearance.BorderColor=Line;button.FlatAppearance.MouseOverBackColor=role=="theme-primary"?Accent:Soft;}
   var picker=control as DateTimePicker;if(picker!=null){picker.CalendarMonthBackground=Surface;picker.CalendarForeColor=Ink;picker.CalendarTitleBackColor=Bg;picker.CalendarTitleForeColor=Ink;}
   var grid=control as DataGridView;if(grid!=null){grid.BackgroundColor=Surface;grid.GridColor=Line;grid.DefaultCellStyle.BackColor=Surface;grid.DefaultCellStyle.ForeColor=Ink;grid.DefaultCellStyle.SelectionBackColor=Soft;grid.DefaultCellStyle.SelectionForeColor=Ink;grid.ColumnHeadersDefaultCellStyle.BackColor=Bg;grid.ColumnHeadersDefaultCellStyle.ForeColor=Muted;}
   foreach(Control child in control.Controls)Apply(child);
   var form=control as Form;if(form!=null)TitleBar(form);control.Invalidate();
  }
  class MenuColors:ProfessionalColorTable {public override Color ToolStripDropDownBackground{get{return Surface;}}public override Color ImageMarginGradientBegin{get{return Surface;}}public override Color ImageMarginGradientMiddle{get{return Surface;}}public override Color ImageMarginGradientEnd{get{return Surface;}}public override Color MenuItemSelected{get{return Soft;}}public override Color MenuItemBorder{get{return Line;}}public override Color MenuBorder{get{return Line;}}}
  public static void ApplyMenu(ContextMenuStrip menu){menu.BackColor=Surface;menu.ForeColor=Ink;menu.Renderer=new ToolStripProfessionalRenderer(new MenuColors());foreach(ToolStripItem item in menu.Items)item.ForeColor=Ink;}
 }
 public partial class MainForm {
  Button themeToggle;
  public void ApplyTheme(){Theme.Set(Data.DarkMode);Theme.Apply(this);if(tray!=null&&tray.ContextMenuStrip!=null)Theme.ApplyMenu(tray.ContextMenuStrip);if(themeToggle!=null)themeToggle.Text=Data.DarkMode?"라이트 모드":"다크 모드";}
  public void ToggleDarkMode(){SaveNote();bool before=Data.DarkMode;Data.DarkMode=!before;if(!Save())Data.DarkMode=before;ApplyTheme();Diagnostics.Action("theme",Data.DarkMode?"dark":"light");}
 }
}
