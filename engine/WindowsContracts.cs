using System;
using System.IO;
using Ara;

// Runs the Windows transport/storage contracts without an interactive desktop.
public static class WindowsContracts {
 public static int Main() {
  int passed=0,failed=0;
  string dir=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-output","engine");
  Directory.CreateDirectory(dir);
  try {
   SharedEngineTests.Run((name,test)=>{try{test();passed++;Console.WriteLine("PASS "+name);}catch(Exception error){failed++;Console.WriteLine("FAIL "+name+": "+error.Message);}},dir);
   Console.WriteLine("RESULT "+passed+"/"+(passed+failed)+" Windows contracts passed");
   return failed==0?0:1;
  } finally { SharedEngine.Stop(); }
 }
}
