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
  public static NoticeFeed Feed(State state,DateTime now){return SharedEngine.Read<NoticeFeed>(SharedEngine.Run("reminders",state,null,JsonMap.Obj("now",Epoch(now)),false)["feed"]);}
 }
}
