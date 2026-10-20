package ara.daily;
import java.util.*;
public class ReminderPolicyTest {
 static int count;static void check(boolean v,String name){if(!v)throw new AssertionError(name);System.out.println("PASS "+name);count++;}
 public static void main(String[] args){long now=1000000L;check(ReminderPolicy.deliver(now-1,now+100,now,false,true),"due notification");check(!ReminderPolicy.deliver(now+1,now+100,now,false,true),"future is scheduled only");check(!ReminderPolicy.deliver(now-100,now,now,false,true),"expired ignored");check(!ReminderPolicy.deliver(now-1,now+100,now,true,true),"delivered deduplicated");check(!ReminderPolicy.deliver(now-1,now+100,now,false,false),"permission required");check(!ReminderPolicy.current(now+172800001L,now+172800101L,now),"two day bound");check(!ReminderPolicy.current(0,now+1,now),"invalid payload rejected");check(ReminderPolicy.removed(new HashSet<>(Arrays.asList("old","same")),new HashSet<>(Arrays.asList("new","same"))).equals(new HashSet<>(Arrays.asList("old"))),"moved and removed reminders cancelled");System.out.println("RESULT "+count+"/"+count+" passed");}
}
