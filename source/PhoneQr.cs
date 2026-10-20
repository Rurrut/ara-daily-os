using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using QRCoder;
namespace Ara {
 public static class PhoneQr {
  public static string Url(MobileServer server){return server.Address==null?null:"http://"+server.Address+":"+server.Port+"/download";}
  public static string PairUrl(MobileServer server){return server.Address==null?null:"aradaily://pair?v=1&pc="+Uri.EscapeDataString("http://"+server.Address+":"+server.Port)+"&code="+Uri.EscapeDataString(server.Code);}
  public static Bitmap Image(string url){using(var generator=new QRCodeGenerator())using(var data=generator.CreateQrCode(url,QRCodeGenerator.ECCLevel.M))using(var code=new QRCode(data))return code.GetGraphic(6,Color.Black,Color.White,true);}
  public static byte[] Png(string url){using(var bitmap=Image(url))using(var stream=new MemoryStream()){bitmap.Save(stream,ImageFormat.Png);return stream.ToArray();}}
 }
 public partial class MainForm {
  public Form CreatePhoneDialog(){
   string url=PhoneQr.Url(PhoneServer),pairUrl=PhoneQr.PairUrl(PhoneServer);
   var dialog=new Form{Text="Android와 QR 연결",ClientSize=new Size(650,450),StartPosition=FormStartPosition.CenterParent,BackColor=Theme.Bg,Font=Theme.Font(10),FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false};
   var qr=new PictureBox{Location=new Point(20,24),Size=new Size(256,256),SizeMode=PictureBoxSizeMode.Zoom,BackColor=Color.White,AccessibleName="앱 연결용 QR 코드"};
   if(pairUrl!=null){qr.Image=PhoneQr.Image(pairUrl);qr.Disposed+=(s,e)=>{if(qr.Image!=null)qr.Image.Dispose();};}dialog.Controls.Add(qr);
   var title=Theme.Label("ARA 앱 안에서 QR로 연결하세요.",13,true);title.Dock=DockStyle.None;title.SetBounds(294,25,335,55);dialog.Controls.Add(title);
   var hint=Theme.Label("같은 Wi-Fi → 앱의 연결 탭 →\n‘QR 코드로 연결하기’를 누르세요.\n코드를 직접 입력하지 않고 연결합니다.",10);hint.Dock=DockStyle.None;hint.SetBounds(294,88,335,90);dialog.Controls.Add(hint);
   var code=Theme.Label("연결 코드  "+PhoneServer.Code,18,true);code.Dock=DockStyle.None;code.SetBounds(294,190,335,55);dialog.Controls.Add(code);
   var address=new TextBox{Text=url??"공유기 주소를 찾지 못했습니다.",ReadOnly=true,Location=new Point(20,300),Size=new Size(610,28),AccessibleName="휴대폰 접속 주소"};dialog.Controls.Add(address);
   var note=Theme.Label("이 QR은 PC 주소와 현재 연결 코드를 포함합니다. 화면을 다른 사람에게 공유하지 마세요.\n연결 후에는 PC 앱이 켜져 있으면 다시 QR을 스캔하지 않아도 됩니다.",9);note.Dock=DockStyle.None;note.SetBounds(20,341,610,50);dialog.Controls.Add(note);
   var row=new FlowLayoutPanel{Location=new Point(20,395),Size=new Size(610,45)};
   row.Controls.Add(Theme.Button("설치용 QR",(s,e)=>{if(url==null)return;using(var f=new Form{Text="앱 설치용 QR · 카메라 앱으로 스캔",ClientSize=new Size(340,370),StartPosition=FormStartPosition.CenterParent})using(var image=PhoneQr.Image(url)){var box=new PictureBox{Dock=DockStyle.Fill,Image=image,SizeMode=PictureBoxSizeMode.Zoom,BackColor=Color.White};f.Controls.Add(box);f.Controls.Add(new Label{Dock=DockStyle.Bottom,Height=55,Text="Android 2.0 설치가 필요할 때만 사용합니다.\n설치 후에는 앱 안에서 연결용 QR을 읽으세요.",TextAlign=ContentAlignment.MiddleCenter});f.ShowDialog(dialog);}}));
   row.Controls.Add(Theme.Button("주소 복사",(s,e)=>{if(url!=null)Clipboard.SetText(url);}));
   row.Controls.Add(Theme.Button("새 연결 코드",(s,e)=>{PhoneServer.Dispose();PhoneServer=new MobileServer(this);PhoneServer.Start(true);dialog.Close();PhoneConnection();}));
   row.Controls.Add(Theme.Button("연결 끄기",(s,e)=>{PhoneServer.Disable();dialog.Close();}));row.Controls.Add(Theme.Button("닫기",(s,e)=>dialog.Close()));dialog.Controls.Add(row);
   dialog.Shown+=(s,e)=>{Theme.Apply(dialog);qr.BackColor=Color.White;};return dialog;
  }
 }
}
