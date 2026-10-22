param([string]$OutputPath = (Join-Path $PSScriptRoot 'ARA Daily OS.exe'),[string]$NodePath = '')
$ErrorActionPreference = 'Stop'
$taskCompiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $taskCompiler)) { $taskCompiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (-not $NodePath) { $taskNodeCommand = Get-Command node -ErrorAction SilentlyContinue; if ($taskNodeCommand) { $NodePath = $taskNodeCommand.Source } }
if (-not $NodePath -or -not (Test-Path -LiteralPath $NodePath)) { throw 'Node.js 경로를 -NodePath로 지정해 주세요. 배포 ZIP에는 runtime/node.exe를 함께 포함합니다.' }
$taskOutputDir = Split-Path -Parent ([IO.Path]::GetFullPath($OutputPath))
New-Item -ItemType Directory -Path $taskOutputDir -Force | Out-Null
$taskRuntimeDir = Join-Path $taskOutputDir 'runtime'
New-Item -ItemType Directory -Path $taskRuntimeDir -Force | Out-Null
Copy-Item -LiteralPath $NodePath -Destination (Join-Path $taskRuntimeDir 'node.exe') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'engine/NODE-LICENSE.txt') -Destination $taskRuntimeDir -Force
$taskCore = Join-Path $PSScriptRoot 'mobile/offline-core.js'
$taskRuntimeHash = Join-Path $taskRuntimeDir 'node.sha256'
[IO.File]::WriteAllText($taskRuntimeHash,(Get-FileHash -LiteralPath (Join-Path $taskRuntimeDir 'node.exe') -Algorithm SHA256).Hash.ToLowerInvariant(),[Text.Encoding]::ASCII)
$taskHost = Join-Path $PSScriptRoot 'engine/host.cjs'
$taskFixtures = Join-Path $PSScriptRoot 'engine/fixtures.json'
$taskSources = Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'source') -Filter '*.cs' | ForEach-Object FullName
$taskQrLibrary = Join-Path $PSScriptRoot 'QRCoder.dll'
& $taskCompiler /nologo /target:winexe /platform:anycpu /optimize+ '/reference:System.dll,System.Core.dll,System.Drawing.dll,System.Windows.Forms.dll,System.Runtime.Serialization.dll,System.Web.Extensions.dll,System.Security.dll' "/reference:$taskQrLibrary" "/out:$OutputPath" "/resource:$taskCore,Aiary.Engine.js" "/resource:$taskHost,Aiary.EngineHost.cjs" "/resource:$taskFixtures,Aiary.EngineFixtures.json" "/resource:$taskRuntimeHash,Aiary.RuntimeHash.txt" $taskSources
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
if ((Split-Path -Parent ([IO.Path]::GetFullPath($OutputPath))) -ne $PSScriptRoot) { Copy-Item -LiteralPath $taskQrLibrary -Destination (Join-Path (Split-Path -Parent ([IO.Path]::GetFullPath($OutputPath))) 'QRCoder.dll') -Force }
if ((Split-Path -Parent ([IO.Path]::GetFullPath($OutputPath))) -ne $PSScriptRoot) { Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Windows 알림.ps1') -Destination (Join-Path (Split-Path -Parent ([IO.Path]::GetFullPath($OutputPath))) 'Windows 알림.ps1') -Force }
Write-Output 'Built ARA Daily OS.exe'
