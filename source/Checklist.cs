using System;
using System.Linq;
using System.IO;
namespace Ara {
 public static class Checklist {
  public static string Category(State s,Todo t){return s.CategorySettings.Any(c=>c.name==t.Group&&!c.deleted)?t.Group:"작업";}
  public static void Edit(State s,string date,PhoneCommand c){
   var task=s.Tasks.Find(t=>t.Id==c.taskId);
   if(c.op=="task-done"){if(task==null)throw new InvalidDataException("할 일을 찾을 수 없습니다.");task.DoneDates.RemoveAll(d=>d==date);if(c.done)task.DoneDates.Add(date);return;}
   if(c.op=="task-delete"){if(task==null)throw new InvalidDataException("할 일을 찾을 수 없습니다.");s.Tasks.Remove(task);return;}
   string text=(c.name??"").Trim();if(text.Length==0||text.Length>250)throw new InvalidDataException("할 일을 250자 이내로 적어 주세요.");
   if(!s.CategorySettings.Any(x=>x.name==c.category&&!x.deleted&&!x.hidden))throw new InvalidDataException("카테고리를 선택해 주세요.");
   if(!string.IsNullOrEmpty(c.taskId)&&task==null)throw new InvalidDataException("할 일을 찾을 수 없습니다.");
   if(task==null){task=new Todo{Id=Guid.NewGuid().ToString("N")};s.Tasks.Add(task);}task.Text=text;task.Group=c.category;
  }
 }
}
