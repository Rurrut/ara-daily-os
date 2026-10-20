using System;using System.Linq;
namespace Ara {public static class YogurtGapTests {
 static void Yes(bool value,string why){if(!value)throw new Exception(why);}
 static State First(int delay){var s=State.New();var d=s.GetDay(new DateTime(2026,10,21));d.Wake=d.Wake.Date.AddHours(8);GridEngine.Ensure(s,d);var first=d.GridBase.Single(x=>x.MealId=="b1").Copy();first.Id="actual-first";first.Start=first.Start.AddMinutes(delay);first.Score=3;first.Satisfaction=4;d.Records.Add(first);return s;}
 public static void Run(Action<string,Action> check,string dir){
 check("yogurt gap: small residual preserves third meal and thirty-minute minimum",()=>{foreach(int delay in new[]{70,80}){var s=First(delay);var d=s.Days[0];var p=GridEngine.Build(s,d);var y=p.Blocks.Single(x=>x.MealId=="b16");Yes(p.Blocks.Any(x=>x.MealId=="b10")&&p.Overflow==0,"third retained");Yes((Adaptive.Target(s,d)-y.End).TotalMinutes==(delay==70?40:30),"only necessary gap used");Yes(p.YogurtRestMinutes==(delay==70?40:30),"bounded rest credit");Yes(d.Records.Single().Score==3&&d.Records.Single().Satisfaction==4,"facts retained");}});
 check("yogurt gap: exhausted gap precedes third omission and does not duplicate credit",()=>{var s=First(90);var d=s.Days[0];var p=GridEngine.Build(s,d);int gap=p.Changes.FindIndex(x=>x.Contains("요거트 종료→취침")),third=p.Changes.FindIndex(x=>x.Contains("3끼니 제외"));Yes(gap>=0&&third>gap&&p.YogurtRestMinutes==30,"phase order and limit");Yes(p.Saved==90&&p.Overflow==0,"total actual recovery");Yes(Adaptive.EstimatedSleep(s,d,p)==Adaptive.Target(s,d),"fixed sleep");});
 check("yogurt gap: earlier buffers solve delay without spending yogurt gap",()=>{var s=First(60);var p=GridEngine.Build(s,s.Days[0]);Yes(!p.YogurtRestMinutes.HasValue&&p.Blocks.Any(x=>x.MealId=="b10"),"unneeded gap untouched");});
 }
}}
