using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;
namespace Ara {
 public static class DarkModeTests {
  static void Assert(bool yes,string message){if(!yes)throw new Exception(message);}
  static IEnumerable<Control> Children(Control c){foreach(Control child in c.Controls){yield return child;foreach(var sub in Children(child))yield return sub;}}
  static double L(Color c){Func<byte,double> f=x=>x/255.0<=.04045?x/255.0/12.92:Math.Pow((x/255.0+.055)/1.055,2.4);return .2126*f(c.R)+.7152*f(c.G)+.0722*f(c.B);}
  static double Contrast(Color a,Color b){double x=L(a),y=L(b);return (Math.Max(x,y)+.05)/(Math.Min(x,y)+.05);}
  public static void Run(Action<string,Action> check,string dir){
   check("dark mode: actual theme button saves appearance without changing timetable or selection",()=>{var s=State.New();var d=s.GetDay(DateTime.Today);d.Wake=DateTime.Today.AddHours(15);GridEngine.Ensure(s,d);d.Records.Add(new CellBlock{Id="work",Start=d.Wake.AddHours(1),Count=3,Category="작업",Name="작업",Score=4});using(var f=new MainForm(new Store(Path.Combine(dir,"dark-state.json")),s,true)){f.Show();Application.DoEvents();f.Board.SetSelection(new CellSelection{Lane=1,First=96,Last=98});var before=f.PhoneSnapshot(d.Date);Children(f).OfType<Button>().Single(x=>x.AccessibleName=="테마 전환").PerformClick();Assert(f.Data.DarkMode&&Theme.Dark&&f.BackColor==Theme.Bg&&f.Store.Load().DarkMode,"dark mode saved");Assert(f.PhoneSnapshot(d.Date).revision==before.revision&&GridEngine.Total(f.Day)==12&&f.Board.Selection.First==96&&f.Board.Selection.Last==98,"no record conflict or lost selection");Assert(Children(f).OfType<TextBox>().All(x=>x.BackColor==Theme.Surface&&x.ForeColor==Theme.Ink),"input colors");var menu=f.CreateCategoryMenu(f.Board.Selection);Assert(menu.BackColor==Theme.Surface&&menu.ForeColor==Theme.Ink,"category menu dark");menu.Dispose();using(var bitmap=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(bitmap,new Rectangle(Point.Empty,f.Size));bitmap.Save(Path.Combine(dir,"dark-pc.png"));}Children(f).OfType<Button>().Single(x=>x.AccessibleName=="테마 전환").PerformClick();Assert(!f.Data.DarkMode&&!Theme.Dark&&f.PhoneSnapshot(d.Date).revision==before.revision,"light mode restored");f.Quit();}});
   check("dark mode: restarted app and detail dialog retain dark surfaces",()=>{var s=State.New();s.DarkMode=true;var d=s.GetDay(DateTime.Today);GridEngine.Ensure(s,d);var store=new Store(Path.Combine(dir,"dark-restart.json"));store.Save(s);using(var f=new MainForm(store,store.Load(),true)){f.Show();Application.DoEvents();Assert(f.Data.DarkMode&&f.BackColor==Theme.Bg&&f.Board.BackColor==Theme.Surface,"restart preference");using(var dialog=new AssignmentDialog(new CellSelection{Lane=1,First=51,Last=53},d.Wake.AddMinutes(30),null,f.GridPlan.Blocks)){dialog.Show(f);Application.DoEvents();Assert(Children(dialog).OfType<TextBox>().All(x=>x.BackColor==Theme.Surface&&x.ForeColor==Theme.Ink),"details dark");dialog.Close();}f.Quit();}Theme.Set(false);});
   check("dark mode: category labels and sleep marker have readable contrast and dim lighting",()=>{Theme.Set(true);try{foreach(string c in GridEngine.Categories)Assert(Contrast(Theme.Ink,Timetable.CategoryColor(c))>=4.5,"category "+c);Assert(Contrast(Theme.SleepInk,Theme.SleepColor)>=4.5&&Contrast(Theme.Muted,Theme.Bg)>=4.5,"sleep and secondary text contrast");Assert(L(Timetable.LightsColor(Theme.Surface))<L(Theme.Surface),"lights-out dims dark surface");}finally{Theme.Set(false);}});
  }
 }
}
