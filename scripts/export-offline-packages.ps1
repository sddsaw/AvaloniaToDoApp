# ==============================================================================
# AvaloniaTodoApp 离线依赖包导出工具 (Windows PowerShell 对应版)
# 用途: 在有公网连接的机器上运行，将本项目所需的所有 NuGet 依赖包提取到本地 local-packages 目录，
#       以便直接拷贝到纯内网/离线开发环境使用。
# 用法:
#   .\scripts\export-offline-packages.ps1
# ==============================================================================

$ErrorActionPreference = "Stop"
$ProjectDir = Split-Path -Parent $PSScriptRoot
$OutputDir = Join-Path $ProjectDir "local-packages"

Write-Host "======================================================" -ForegroundColor Cyan
Write-Host ">>> 开始导出 AvaloniaTodoApp 离线 NuGet 依赖包 (Windows) <<<" -ForegroundColor Cyan
Write-Host "======================================================" -ForegroundColor Cyan

if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
}

# 1. 获取全局 NuGet 缓存目录
$globalCacheOutput = (dotnet nuget locals global-packages --list | Out-String)
$nugetCache = ""
if ($globalCacheOutput -match 'global-packages:\s*(.+)') {
    $nugetCache = $matches[1].Trim()
}
if (-not (Test-Path $nugetCache)) {
    $nugetCache = Join-Path $env:USERPROFILE ".nuget\packages"
}
Write-Host ">> 全局 NuGet 缓存目录: $nugetCache" -ForegroundColor Yellow

# 2. 预还原运行时依赖
Write-Host ">> 正在解析并还原多平台运行时依赖..." -ForegroundColor Yellow
try {
    dotnet restore (Join-Path $ProjectDir "AvaloniaTodoApp.csproj") -r win-x64 --verbosity quiet 2>$null
    dotnet restore (Join-Path $ProjectDir "AvaloniaTodoApp.sln") --verbosity quiet 2>$null
} catch {}

# 3. 收集所有依赖包到 local-packages
Write-Host ">> 正在收集所有依赖包到 $OutputDir ..." -ForegroundColor Yellow

$assetFiles = @(
    Join-Path $ProjectDir "obj\project.assets.json",
    Join-Path $ProjectDir "tests\AvaloniaTodoApp.Tests\obj\project.assets.json"
)

$packagesToCopy = [System.Collections.Generic.HashSet[string]]::new()

foreach ($af in $assetFiles) {
    if (Test-Path $af) {
        $json = Get-Content $af -Raw | ConvertFrom-Json
        if ($json.libraries) {
            foreach ($prop in $json.libraries.PSObject.Properties) {
                if ($prop.Value.type -eq "package" -and $prop.Value.path) {
                    [void]$packagesToCopy.Add($prop.Value.path)
                }
            }
        }
    }
}

$copiedCount = 0
foreach ($pkgPath in $packagesToCopy) {
    $fullDir = Join-Path $nugetCache $pkgPath
    if (Test-Path $fullDir) {
        $nupkgs = Get-ChildItem -Path $fullDir -Filter "*.nupkg"
        foreach ($n in $nupkgs) {
            $dest = Join-Path $OutputDir $n.Name
            if (-not (Test-Path $dest)) {
                Copy-Item -Path $n.FullName -Destination $dest -Force
            }
            $copiedCount++
        }
    }
}

# 补充运行时包
Get-ChildItem -Path $nugetCache -Recurse -Filter "*.nupkg" -ErrorAction SilentlyContinue | Where-Object {
    $_.Name -match "Microsoft\.NETCore\.App\.Runtime" -or $_.Name -match "Microsoft\.AspNetCore\.App\.Runtime" -or $_.Name -match "Microsoft\.WindowsDesktop\.App\.Runtime"
} | ForEach-Object {
    $dest = Join-Path $OutputDir $_.Name
    if (-not (Test-Path $dest)) {
        Copy-Item -Path $_.FullName -Destination $dest -Force
        $copiedCount++
    }
}

# 4. 确保 nuget.config 存在
$nugetConfigFile = Join-Path $ProjectDir "nuget.config"
if (-not (Test-Path $nugetConfigFile)) {
    $configContent = @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <!-- 清空默认在线源，确保内网环境下 100% 仅使用本地离线包源 -->
    <clear />
    <add key="local-packages" value="./local-packages" />
  </packageSources>
</configuration>
"@
    Set-Content -Path $nugetConfigFile -Value $configContent -Encoding utf8
}

$allNupkgs = (Get-ChildItem -Path $OutputDir -Filter "*.nupkg").Count
Write-Host "`n======================================================" -ForegroundColor Green
Write-Host "✓ 离线依赖包导出完成！" -ForegroundColor Green
Write-Host "  - 离线包数量: $allNupkgs 个" -ForegroundColor Green
Write-Host "  - 离线包目录: local-packages" -ForegroundColor Green
Write-Host "  - 配置文件: nuget.config" -ForegroundColor Green
Write-Host "======================================================" -ForegroundColor Green
Write-Host "内网使用说明:" -ForegroundColor Cyan
Write-Host "1. 将本项目整个目录（含 local-packages 文件夹和 nuget.config）拷贝至内网机；"
Write-Host "2. 在内网机执行编译: dotnet build 或发布脚本 .\scripts\build.ps1 ；"
Write-Host "3. 编译过程中将 100% 优先从 local-packages 读取，完全不需要外网网络！"
