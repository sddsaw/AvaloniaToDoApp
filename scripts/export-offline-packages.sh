#!/usr/bin/env bash
set -e

# ==============================================================================
# AvaloniaTodoApp 离线依赖包导出工具
# 用途: 在有公网连接的机器上运行，将本项目所需的所有 NuGet 依赖包提取到本地 local-packages 目录，
#       以便直接拷贝到纯内网/离线开发环境使用。
# 用法:
#   ./scripts/export-offline-packages.sh
# ==============================================================================

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
OUTPUT_DIR="$PROJECT_DIR/local-packages"

GREEN='\033[0;32m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
NC='\033[0m'

echo -e "${BLUE}======================================================${NC}"
echo -e "${BLUE}>>> 开始导出 AvaloniaTodoApp 离线 NuGet 依赖包 <<<${NC}"
echo -e "${BLUE}======================================================${NC}"

mkdir -p "$OUTPUT_DIR"

# 1. 获取全局 NuGet 缓存目录
NUGET_CACHE=$(dotnet nuget locals global-packages --list | awk '{print $2}')
if [ -z "$NUGET_CACHE" ] || [ ! -d "$NUGET_CACHE" ]; then
    NUGET_CACHE="$HOME/.nuget/packages"
fi
echo -e "${YELLOW}>> 全局 NuGet 缓存目录: $NUGET_CACHE${NC}"

# 2. 预还原跨平台运行时依赖 (如 Windows x64，便于内网构建独立包)
echo -e "${YELLOW}>> 正在解析并还原多平台运行时依赖...${NC}"
dotnet restore "$PROJECT_DIR/AvaloniaTodoApp.csproj" -r win-x64 --verbosity quiet 2>/dev/null || true
dotnet restore "$PROJECT_DIR/AvaloniaTodoApp.sln" --verbosity quiet 2>/dev/null || true

# 3. 提取所有引用的 .nupkg
echo -e "${YELLOW}>> 正在收集所有依赖包到 $OUTPUT_DIR ...${NC}"

python3 - <<EOF
import json, os, shutil

project_dir = "$PROJECT_DIR"
output_dir = "$OUTPUT_DIR"
nuget_cache = "$NUGET_CACHE"

asset_files = [
    os.path.join(project_dir, "obj", "project.assets.json"),
    os.path.join(project_dir, "tests", "AvaloniaTodoApp.Tests", "obj", "project.assets.json")
]

packages_to_copy = set()

for af in asset_files:
    if os.path.exists(af):
        with open(af, "r", encoding="utf-8") as f:
            data = json.load(f)
            libs = data.get("libraries", {})
            for k, v in libs.items():
                if v.get("type") == "package":
                    path = v.get("path")
                    if path:
                        packages_to_copy.add(path)

count = 0
for p in packages_to_copy:
    full_pkg_dir = os.path.join(nuget_cache, p)
    if os.path.isdir(full_pkg_dir):
        for fname in os.listdir(full_pkg_dir):
            if fname.endswith(".nupkg"):
                src = os.path.join(full_pkg_dir, fname)
                dst = os.path.join(output_dir, fname)
                if not os.path.exists(dst):
                    shutil.copy2(src, dst)
                count += 1

# 补充运行时包
for root, dirs, files in os.walk(nuget_cache):
    for f in files:
        if f.endswith('.nupkg') and any(k in f.lower() for k in ['microsoft.netcore.app.runtime', 'microsoft.aspnetcore.app.runtime', 'microsoft.windowsdesktop.app.runtime']):
            src = os.path.join(root, f)
            dst = os.path.join(output_dir, f)
            if not os.path.exists(dst):
                shutil.copy2(src, dst)
                count += 1

print(f"共收集并就绪 {len(os.listdir(output_dir))} 个 .nupkg 离线依赖包。")
EOF

# 4. 确保 nuget.config 配置正确
NUGET_CONFIG="$PROJECT_DIR/nuget.config"
if [ ! -f "$NUGET_CONFIG" ]; then
    cat << 'CONFIG_EOF' > "$NUGET_CONFIG"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <!-- 清空默认在线源，确保内网环境下 100% 仅使用本地离线包源 -->
    <clear />
    <add key="local-packages" value="./local-packages" />
  </packageSources>
</configuration>
CONFIG_EOF
fi

TOTAL_SIZE=$(du -sh "$OUTPUT_DIR" | cut -f1)
echo -e "\n${GREEN}======================================================${NC}"
echo -e "${GREEN}✓ 离线依赖包导出完成！${NC}"
echo -e "${GREEN}  - 离线包目录: local-packages (${TOTAL_SIZE})${NC}"
echo -e "${GREEN}  - 配置文件: nuget.config${NC}"
echo -e "${GREEN}======================================================${NC}"
echo -e "${BLUE}内网使用说明:${NC}"
echo -e "1. 将本项目整个目录（含 local-packages 文件夹和 nuget.config）拷贝至内网机；"
echo -e "2. 在内网机执行编译: dotnet build 或发布脚本 ./scripts/build.sh ；"
echo -e "3. 编译过程中将 100% 优先从 local-packages 读取，完全不需要外网网络！"
