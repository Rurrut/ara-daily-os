package ara.daily;
import java.util.*;
public final class ReminderPolicy {
 private ReminderPolicy(){}
 public static boolean current(long at,long expires,long now){return at>0&&expires>at&&expires>now&&at<=now+172800000L;}
 public static boolean deliver(long at,long expires,long now,boolean delivered,boolean permitted){return permitted&&!delivered&&current(at,expires,now)&&at<=now;}
 public static Set<String> removed(Set<String> previous,Set<String> next){Set<String> removed=new HashSet<>(previous);removed.removeAll(next);return removed;}
}
