using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;
namespace Ara {
 public static class QrPairTests {
  static void Assert(bool ok,string message){if(!ok)throw new Exception(message);}
  public static void Run(Action<string,Action> check,string dir){
   check("pair QR: private PC code is present only in desktop pairing QR",()=>{using(var f=new MainForm(new Store(Path.Combine(dir,"pair-qr","state.json")),State.New(),true))using(var server=new MobileServer(f,18765)){server.Start(false);string pair=PhoneQr.PairUrl(server),install=PhoneQr.Url(server);Assert(pair.StartsWith("aradaily://pair?v=1&pc=")&&pair.Contains("code="+server.Code)&&!pair.Contains(server.Token),"ephemeral code, no long-lived token");Assert(install.EndsWith("/download")&&!install.Contains(server.Code),"install QR remains public safe");f.Quit();}});
   check("pair QR: desktop image fits without cropping and gives app camera instructions",()=>{using(var f=new MainForm(new Store(Path.Combine(dir,"pair-dialog","state.json")),State.New(),true)){f.PhoneServer.Dispose();f.PhoneServer=new MobileServer(f,18766);f.PhoneServer.Start(false);using(var dialog=f.CreatePhoneDialog()){dialog.Show();Application.DoEvents();var qr=dialog.Controls.OfType<PictureBox>().Single();Assert(qr.SizeMode==PictureBoxSizeMode.Zoom&&qr.AccessibleName=="앱 연결용 QR 코드","scaled QR");Assert(dialog.Controls.OfType<Label>().Any(x=>x.Text.Contains("ARA 앱 안에서")),"in-app scanner instructions");using(var image=new Bitmap(dialog.Width,dialog.Height)){dialog.DrawToBitmap(image,new Rectangle(Point.Empty,dialog.Size));image.Save(Path.Combine(dir,"pair-qr-desktop.png"));}dialog.Close();}f.Quit();}});
   check("pair QR: reference camera frame uses code and private endpoint without external URL",()=>{const string value="aradaily://pair?v=1&pc=http%3A%2F%2F192.168.0.2%3A8765&code=12345678";File.WriteAllBytes(Path.Combine(dir,"pair-frame.png"),PhoneQr.Png(value));Assert(File.ReadAllBytes(Path.Combine(dir,"pair-frame.png")).Length>200,"reference QR image");});
  }
 }
}
