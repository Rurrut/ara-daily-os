param([Parameter(Mandatory=$true)][string]$PayloadPath)
$ErrorActionPreference='Stop'
$taskPayload=$null
try {
 $taskPayload=Get-Content -LiteralPath $PayloadPath -Raw -Encoding UTF8 | ConvertFrom-Json
 $taskAppId='ARA.DailyOS'
 $taskExecutable=(Resolve-Path -LiteralPath $taskPayload.app).Path
 $taskAppKey='HKCU:\Software\Classes\AppUserModelId\'+$taskAppId
 New-Item -Path $taskAppKey -Force | Out-Null
 New-ItemProperty -Path $taskAppKey -Name DisplayName -Value 'ARA Daily OS' -PropertyType String -Force | Out-Null
 New-ItemProperty -Path $taskAppKey -Name ShowInSettings -Value 1 -PropertyType DWord -Force | Out-Null
 $taskProtocol='HKCU:\Software\Classes\aradaily-pc'
 if (Test-Path $taskProtocol) { $taskExisting=(Get-ItemProperty -LiteralPath ($taskProtocol+'\shell\open\command') -ErrorAction SilentlyContinue).'(default)'; if ($taskExisting -and $taskExisting -ne ('"'+$taskExecutable+'" --show')) { throw '다른 ARA 프로토콜 등록을 확인해 주세요.' } }
 New-Item -Path ($taskProtocol+'\shell\open\command') -Force | Out-Null
 Set-Item -LiteralPath $taskProtocol -Value 'URL:ARA Daily OS'
 New-ItemProperty -Path $taskProtocol -Name 'URL Protocol' -Value '' -PropertyType String -Force | Out-Null
 Set-Item -LiteralPath ($taskProtocol+'\shell\open\command') -Value ('"'+$taskExecutable+'" --show')
 if (-not ('AraNotificationShortcut' -as [type])) {
 Add-Type -TypeDefinition @'
using System;
using System.Text;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
public static class AraNotificationShortcut {
 [ComImport,Guid("00021401-0000-0000-C000-000000000046")] class Link {}
 [ComImport,Guid("000214F9-0000-0000-C000-000000000046"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface ILink {
  void GetPath(StringBuilder p,int n,IntPtr f,uint flags); void GetIDList(out IntPtr p); void SetIDList(IntPtr p); void GetDescription(StringBuilder p,int n); void SetDescription([MarshalAs(UnmanagedType.LPWStr)]string s); void GetWorkingDirectory(StringBuilder p,int n); void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)]string s); void GetArguments(StringBuilder p,int n); void SetArguments([MarshalAs(UnmanagedType.LPWStr)]string s); void GetHotkey(out short p); void SetHotkey(short p); void GetShowCmd(out int p); void SetShowCmd(int p); void GetIconLocation(StringBuilder p,int n,out int i); void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)]string s,int i); void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)]string s,uint r); void Resolve(IntPtr w,uint f); void SetPath([MarshalAs(UnmanagedType.LPWStr)]string s);
 }
 [StructLayout(LayoutKind.Sequential)] struct Key {public Guid fmt; public uint pid;}
 [StructLayout(LayoutKind.Explicit,Size=24)] struct Value {[FieldOffset(0)]public ushort type;[FieldOffset(8)]public IntPtr text;}
 [ComImport,Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface Properties {void GetCount(out uint n);void GetAt(uint n,out Key k);void GetValue(ref Key k,out Value v);void SetValue(ref Key k,ref Value v);void Commit();}
 public static void Save(string path,string executable,string id){var link=(ILink)new Link();link.SetPath(executable);link.SetArguments("--show");link.SetWorkingDirectory(System.IO.Path.GetDirectoryName(executable));link.SetDescription("ARA Daily OS");link.SetIconLocation(executable,0);var key=new Key{fmt=new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"),pid=5};var value=new Value{type=31,text=Marshal.StringToCoTaskMemUni(id)};try{((Properties)link).SetValue(ref key,ref value);((Properties)link).Commit();((IPersistFile)link).Save(path,true);}finally{Marshal.FreeCoTaskMem(value.text);Marshal.FinalReleaseComObject(link);}}
}
'@
 }
 $taskShortcut=Join-Path ([Environment]::GetFolderPath('Programs')) 'ARA Daily OS.lnk'
 [AraNotificationShortcut]::Save($taskShortcut,$taskExecutable,$taskAppId)
 [Windows.UI.Notifications.ToastNotificationManager,Windows.UI.Notifications,ContentType=WindowsRuntime] | Out-Null
 [Windows.Data.Xml.Dom.XmlDocument,Windows.Data.Xml.Dom.XmlDocument,ContentType=WindowsRuntime] | Out-Null
 [Windows.UI.Notifications.ToastNotifier,Windows.UI.Notifications,ContentType=WindowsRuntime] | Out-Null
 [Windows.UI.Notifications.NotificationSetting,Windows.UI.Notifications,ContentType=WindowsRuntime] | Out-Null
 [Windows.UI.Notifications.ToastNotification,Windows.UI.Notifications,ContentType=WindowsRuntime] | Out-Null
 [Windows.UI.Notifications.ToastNotificationHistory,Windows.UI.Notifications,ContentType=WindowsRuntime] | Out-Null
 $taskNotifier=[Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier($taskAppId)
 $taskSetting=if ($null -ne $taskNotifier.Setting) { $taskNotifier.Setting.ToString() } else { 'Unknown' }
 if ($taskSetting -ne 'Enabled' -and $taskSetting -ne 'Unknown') { @{state='blocked';setting=$taskSetting;history=0;error='Windows의 ARA 알림 설정을 확인해 주세요.';time=[DateTimeOffset]::Now.ToString('o')} | ConvertTo-Json | Set-Content -LiteralPath $taskPayload.status -Encoding UTF8; exit 2 }
 $taskTitle=[Security.SecurityElement]::Escape([string]$taskPayload.title)
 $taskBody=[Security.SecurityElement]::Escape([string]$taskPayload.body)
 $taskXml=New-Object Windows.Data.Xml.Dom.XmlDocument
 $taskXml.LoadXml("<toast activationType='protocol' launch='aradaily-pc:open' duration='short'><visual><binding template='ToastGeneric'><text>$taskTitle</text><text>$taskBody</text></binding></visual></toast>")
 $taskToast=[Windows.UI.Notifications.ToastNotification]::new($taskXml)
 $taskToast.Tag='ara'+[Guid]::NewGuid().ToString('N').Substring(0,10)
 $taskToast.Group='schedule'
 $taskToast.ExpirationTime=[DateTimeOffset]::Now.AddMinutes(30)
 $taskNotifier.Show($taskToast)
 $taskEntries=@([Windows.UI.Notifications.ToastNotificationManager]::History.GetHistory($taskAppId))
 $taskHistory=@($taskEntries | Where-Object { $null -ne $_ }).Count
 if (-not ($taskEntries | Where-Object { $_.Tag -eq $taskToast.Tag })) { throw '알림 전송 후 알림센터 등록을 확인하지 못했습니다.' }
 @{state='submitted';setting=$taskSetting;history=$taskHistory;error='';time=[DateTimeOffset]::Now.ToString('o')} | ConvertTo-Json | Set-Content -LiteralPath $taskPayload.status -Encoding UTF8
} catch {
 if ($taskPayload -and $taskPayload.status) { @{state='failed';setting='';history=0;error=($_.Exception.Message+" | "+$_.ScriptStackTrace);time=[DateTimeOffset]::Now.ToString('o')} | ConvertTo-Json | Set-Content -LiteralPath $taskPayload.status -Encoding UTF8 }
 exit 1
}
