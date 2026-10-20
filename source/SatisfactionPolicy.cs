using System;using System.Linq;using System.Drawing;using System.Windows.Forms;
namespace Ara {
 public sealed class SatisfactionSummary {public decimal Average;public int Minutes;public DateTime Finish;}
 public static class SatisfactionPolicy {
  public static bool WakeConfirmed(Day d){return d.WakeConfirmed??d.Records.Count>0;}
  public static SatisfactionSummary Calculate(State s,Day d,DateTime sleep,DateTime now){
   now=new DateTime(now.Year,now.Month,now.Day,now.Hour,now.Minute,0);DateTime finish=now<sleep?now:sleep;if(finish<d.Wake)finish=d.Wake;
   decimal total=0;int minutes=0;for(DateTime at=d.Wake;at<finish;at=at.AddMinutes(10)){DateTime end=at.AddMinutes(10);if(end>finish)end=finish;int weight=(int)(end-at).TotalMinutes;var record=d.Records.FirstOrDefault(b=>b.Start<=at&&b.End>at);if(record==null&&s.ExcludeBlankSatisfaction)continue;total+=(record==null?s.BlankSatisfaction:record.Satisfaction)*weight;minutes+=weight;}
   return new SatisfactionSummary{Average=minutes==0?0:total/minutes,Minutes=minutes,Finish=finish};
  }
 }
 public partial class MainForm {
  void SatisfactionSettings(){using(var form=new Form{Text="만족도 집계 설정",Width=330,Height=220,StartPosition=FormStartPosition.CenterParent}){var panel=new FlowLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(15),FlowDirection=FlowDirection.TopDown};var exclude=new CheckBox{Text="빈칸은 평균에서 제외",Checked=Data.ExcludeBlankSatisfaction,AutoSize=true};var value=new NumericUpDown{Minimum=0,Maximum=5,Value=Data.BlankSatisfaction,Width=90,Enabled=!exclude.Checked};exclude.CheckedChanged+=(a,b)=>value.Enabled=!exclude.Checked;panel.Controls.Add(Theme.Label("기상~현재까지만 집계 · 수면 예상 이후는 제외",9));panel.Controls.Add(exclude);panel.Controls.Add(Theme.Label("미기록 빈칸의 기본 점수 (기록한 점수는 유지)",9));panel.Controls.Add(value);panel.Controls.Add(Theme.Button("저장",(a,b)=>{Change(()=>{Data.ExcludeBlankSatisfaction=exclude.Checked;Data.BlankSatisfactionValue=value.Value;});form.Close();},true));form.Controls.Add(panel);form.ShowDialog(this);}}
  void RefreshSatisfactionTotal(){if(scoreTotal==null||GridPlan==null)return;var stats=SatisfactionPolicy.Calculate(Data,Day,Adaptive.EstimatedSleep(Data,Day,GridPlan),DateTime.Now);scoreTotal.Text="집중 "+GridEngine.Total(Day).ToString("0.##")+"점 · "+(Day.Records.Sum(x=>x.Count)*10)+"분 기록\n만족도 "+(stats.Minutes==0?"—":stats.Average.ToString("0.00"))+" / 5 · 이행 "+(Adherence.Score(Day).HasValue?Adherence.Score(Day).Value.ToString("0.#"):"—");Board.BlankSatisfaction=Data.BlankSatisfaction;Board.IncludeBlankSatisfaction=!Data.ExcludeBlankSatisfaction;Board.SatisfactionFrom=Day.Wake;Board.SatisfactionTo=stats.Finish;}
 }
}
