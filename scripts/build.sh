#!/usr/bin/env bash
set -e

# ==============================================================================
# AvaloniaTodoApp 跨平台构建与发布脚本 (类似前端项目的 npm run build)
# 用法:
#   ./scripts/build.sh                # 默认构建当前操作系统对应的独立安装包
#   ./scripts/build.sh win-x64        # 构建 Windows x64 独立版本
#   ./scripts/build.sh osx-arm64      # 构建 macOS Apple Silicon (M系列芯片) 独立版本
#   ./scripts/build.sh osx-x64        # 构建 macOS Intel 独立版本
#   ./scripts/build.sh linux-x64      # 构建 Linux x64 独立版本
#   ./scripts/build.sh all            # 一键构建所有支持平台的发布包
# ==============================================================================

PROJECT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CSPROJ_PATH="$PROJECT_DIR/AvaloniaTodoApp.csproj"
OUTPUT_ROOT="$PROJECT_DIR/dist"

# 支持的平台列表
TARGET_RID="${1:-auto}"

# 颜色输出
GREEN='\033[0;32m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m'

echo -e "${BLUE}======================================================${NC}"
echo -e "${BLUE}>>> 开始 Avalonia 桌面应用自动化构建流水线 <<<${NC}"
echo -e "${BLUE}======================================================${NC}"

# 检测操作系统默认 RID
detect_rid() {
    local os=$(uname -s)
    local arch=$(uname -m)
    if [ "$os" = "Darwin" ]; then
        if [ "$arch" = "arm64" ]; then
            echo "osx-arm64"
        else
            echo "osx-x64"
        fi
    elif [ "$os" = "Linux" ]; then
        echo "linux-x64"
    else
        echo "win-x64"
    fi
}

publish_target() {
    local rid=$1
    local out_dir="$OUTPUT_ROOT/$rid"
    local zip_file="$OUTPUT_ROOT/AvaloniaTodoApp-$rid.zip"

    local temp_bin="$OUTPUT_ROOT/${rid}-bin"
    rm -rf "$out_dir" "$temp_bin"
    mkdir -p "$out_dir"

    # 执行核心编译发布
    dotnet publish "$CSPROJ_PATH" \
        -c Release \
        -r "$rid" \
        --self-contained true \
        -p:PublishSingleFile=true \
        -p:IncludeNativeLibrariesForSelfExtract=true \
        -o "$temp_bin" \
        --nologo

    if [[ "$rid" == osx-* ]]; then
        echo -e "${BLUE}>>> 封装标准 macOS 应用程序包 (.app Bundle)...${NC}"
        local app_dir="$out_dir/AvaloniaTodoApp.app"
        mkdir -p "$app_dir/Contents/MacOS"
        mkdir -p "$app_dir/Contents/Resources"

        # 移动可执行文件与运行时依赖
        cp -R "$temp_bin/"* "$app_dir/Contents/MacOS/"
        
        # 写入应用清单与专属图标
        cp "$PROJECT_DIR/Assets/Info.plist" "$app_dir/Contents/Info.plist"
        cp "$PROJECT_DIR/Assets/app-icon.icns" "$app_dir/Contents/Resources/app-icon.icns"
        chmod +x "$app_dir/Contents/MacOS/AvaloniaTodoApp"
        
        rm -rf "$temp_bin"
        echo -e "${GREEN}>>> [2/3] macOS App 封装完成: $app_dir${NC}"
    else
        mv "$temp_bin/"* "$out_dir/"
        rm -rf "$temp_bin"
        echo -e "${GREEN}>>> [2/3] 编译发布成功: $out_dir${NC}"
    fi

    # 打包压缩为 zip 包便于分发 (类似前端打 dist.zip)
    echo -e "${YELLOW}>>> [3/3] 正在压缩产物为归档包: $zip_file ...${NC}"
    (
        cd "$out_dir"
        zip -r -q "$zip_file" ./*
    )
    echo -e "${GREEN}✓ 归档完成: $zip_file (${rid})${NC}"
}

mkdir -p "$OUTPUT_ROOT"

if [ "$TARGET_RID" = "all" ]; then
    echo -e "${YELLOW}模式: 多平台全量构建 (Windows, macOS, Linux)${NC}"
    publish_target "win-x64"
    publish_target "osx-arm64"
    publish_target "osx-x64"
    publish_target "linux-x64"
elif [ "$TARGET_RID" = "auto" ]; then
    DETECTED_RID=$(detect_rid)
    echo -e "${YELLOW}模式: 自动检测本机平台 -> $DETECTED_RID${NC}"
    publish_target "$DETECTED_RID"
else
    publish_target "$TARGET_RID"
fi

echo -e "\n${GREEN}======================================================${NC}"
echo -e "${GREEN}>>> 所有构建任务全部完成！产物已输出至: $OUTPUT_ROOT <<<${NC}"
echo -e "${GREEN}======================================================${NC}"
ls -lh "$OUTPUT_ROOT"/*.zip 2>/dev/null || true
