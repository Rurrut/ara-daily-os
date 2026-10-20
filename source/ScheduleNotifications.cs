using System;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.Serialization;
namespace Ara {
 [DataContract] public class ScheduledNotice {
  [DataMember] public string key,date,title,body;
  [DataMember] public long at,expires;
 }
 [DataContract] public class NoticeFeed {[DataMember] public List<ScheduledNotice> items=new List<ScheduledNotice>();[DataMember] public long generated;}
 public static class ScheduleNotifications {
  public static long Epoch(DateTime value){return (long)(value.ToUniversalTime()-new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc)).TotalMilliseconds;}
  static ScheduledNotice Notice(Day d,string key,string title,string body,DateTime at,DateTime expires){return new ScheduledNotice{date=d.Date,key=key,title=title,body=body,at=Epoch(at),expires=Epoch(expires<at.AddHours(1)?at.AddHours(1):expires)};}
  public static NoticeFeed Feed(State state,DateTime now){var result=new NoticeFeed{generated=Epoch(now)};var s=Store.Clone(state);foreach(var d in s.Days.Where(x=>x.Wake.Date>=now.Date.AddDays(-1)&&x.Wake.Date<=now.Date.AddDays(1))){var grid=GridEngine.Build(s,d);var plan=GridEngine.AsPlan(s,d,grid);
    foreach(var slot in plan.Slots.Where(x=>!x.Done&&x.Minutes>0&&(x.Block.Kind=="meal"||x.Block.Kind=="exercise"||x.Block.Kind=="work"||x.Block.Kind=="study"))){string title=slot.Block.Kind=="meal"?slot.Block.Name+" 준비":slot.Block.Name;result.items.Add(Notice(d,Reminders.Key(slot),title,slot.Start.ToString("HH:mm")+" 예정 · ARA Daily OS",slot.Start.AddMinutes(-s.ReminderMinutes),slot.Start.AddMinutes(10)));}
    DateTime sleep=Adaptive.EstimatedSleep(s,d,grid);result.items.Add(Notice(d,"lights|"+d.Date+"|"+sleep.ToString("o"),"소등 시간","수면 예상 "+sleep.ToString("HH:mm")+" · 취침 2시간 전",sleep.AddHours(-2),sleep.AddHours(-2).AddMinutes(15)));result.items.Add(Notice(d,"sleep|"+d.Date+"|"+sleep.ToString("o"),"수면 예상 시간",sleep.ToString("HH:mm")+" · 오늘 일정의 수면 예상",sleep,sleep.AddMinutes(15)));
   }long instant=Epoch(now),limit=Epoch(now.AddDays(2));result.items=result.items.Where(x=>x.expires>instant&&x.at<=limit).OrderBy(x=>x.at).ToList();return result;}
 }
}
