# TideServer 一键门禁（2026-09-27，网络协议.md §14.2）
# 用法：
#   TideServer/verify.ps1              快档：三工程编译 + loadcheck + verify --quick（约 1-2 分钟）
#   TideServer/verify.ps1 -Full        全量：verify 不带 --quick（含 V7 对拍/V8 并发断线，约 2-3 分钟）
#   TideServer/verify.ps1 -WithUnity   附加慢档：串行 Unity 批处理跑三验证器（约十几分钟）
# 任何一步失败 → 非零退出。改共享源（Assets 内）建议 -WithUnity；只改 TideServer 专用代码用快档即可。

param(
    [switch]$Quick,
    [switch]$Full,
    [switch]$WithUnity
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $PSScriptRoot "bin/Debug/net10.0/TideServer.exe"
$unity = "C:/Program Files/Unity/Hub/Editor/6000.5.10f1/Editor/Unity.exe"
$failed = $false

function Step([string]$name, [scriptblock]$body) {
    Write-Host "`n════ $name ════" -ForegroundColor Cyan
    & $body
    if ($LASTEXITCODE -ne 0) {
        Write-Host "✗ $name 失败（exit=$LASTEXITCODE）" -ForegroundColor Red
        $script:failed = $true
    } else {
        Write-Host "✓ $name" -ForegroundColor Green
    }
}

# ---- ① 三工程编译 ----
Step "编译 Assembly-CSharp" { dotnet build (Join-Path $root "Assembly-CSharp.csproj") --nologo -v q }
Step "编译 Assembly-CSharp-Editor" { dotnet build (Join-Path $root "Assembly-CSharp-Editor.csproj") --nologo -v q }
Step "编译 TideServer" { dotnet build (Join-Path $PSScriptRoot "TideServer.csproj") --nologo -v q }

if (-not (Test-Path $exe)) {
    Write-Host "✗ 未找到 $exe（编译失败？）" -ForegroundColor Red
    exit 1
}

# ---- ② 数据装载自检 ----
Step "loadcheck" { & $exe loadcheck }

# ---- ③ verify（快档/全量） ----
if ($Full) {
    Step "verify（全量档）" { & $exe verify }
} else {
    Step "verify（quick 档）" { & $exe verify --quick }
}

# ---- ④ Unity 慢档（可选）：三验证器 ----
if ($WithUnity) {
    $verifiers = @(
        @{ Name = "端到端验证";      Method = "CardCore.Editor.CardPipelineVerifier.RunVerification";  Log = "verify_pipeline.log" },
        @{ Name = "战场专项验证";    Method = "CardCore.Editor.BattlefieldVerifier.Run";                Log = "verify_battlefield.log" },
        @{ Name = "网络协议回环验证"; Method = "CardCore.Editor.NetProtocolLoopbackVerifier.Run";        Log = "verify_loopback.log" }
    )
    foreach ($v in $verifiers) {
        $log = Join-Path $root "Logs" $v.Log
        Step ("Unity·" + $v.Name) {
            & $unity -batchmode -nographics -quit -projectPath $root -executeMethod $v.Method -logFile $log
            # Unity 自身退出码不可靠——从日志解析 PASS/FAIL 汇总行（控制台桥域重载后失效的既定教训）
            $summary = Select-String -Path $log -Pattern "PASS[=:\s]+(\d+)[^\d]+FAIL[=:\s/]+(\d+)" |
                Select-Object -Last 1
            if (-not $summary) { Write-Host "日志缺 PASS/FAIL 汇总行"; cmd /c "exit 1"; return }
            $pass = [int]$summary.Matches[0].Groups[1].Value
            $fail = [int]$summary.Matches[0].Groups[2].Value
            Write-Host "$($v.Name)：PASS=$pass FAIL=$fail"
            if ($fail -ne 0) { cmd /c "exit 1" }
        }
    }
}

Write-Host ""
if ($failed) { Write-Host "❌ 门禁未通过" -ForegroundColor Red; exit 1 }
Write-Host "✅ 门禁通过" -ForegroundColor Green; exit 0
