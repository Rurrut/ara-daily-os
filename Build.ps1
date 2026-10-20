param([string]$OutputPath = (Join-Path $PSScriptRoot 'ARA Daily OS.exe'))
$ErrorActionPreference = 'Stop'
$taskCompiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $taskCompiler)) { $taskCompiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
$taskSources = Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'source') -Filter '*.cs' | ForEach-Object FullName
$taskQrLibrary = Join-Path $PSScriptRoot 'QRCoder.dll'
& $taskCompiler /nologo /target:winexe /platform:anycpu /optimize+ '/reference:System.dll,System.Core.dll,System.Drawing.dll,System.Windows.Forms.dll,System.Runtime.Serialization.dll,System.Web.Extensions.dll,System.Security.dll' "/reference:$taskQrLibrary" "/out:$OutputPath" $taskSources
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
if ((Split-Path -Parent ([IO.Path]::GetFullPath($OutputPath))) -ne $PSScriptRoot) { Copy-Item -LiteralPath $taskQrLibrary -Destination (Join-Path (Split-Path -Parent ([IO.Path]::GetFullPath($OutputPath))) 'QRCoder.dll') -Force }
if ((Split-Path -Parent ([IO.Path]::GetFullPath($OutputPath))) -ne $PSScriptRoot) { Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Windows 알림.ps1') -Destination (Join-Path (Split-Path -Parent ([IO.Path]::GetFullPath($OutputPath))) 'Windows 알림.ps1') -Force }
Write-Output 'Built ARA Daily OS.exe'
