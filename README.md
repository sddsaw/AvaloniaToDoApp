# Avalonia TodoApp (跨平台桌面待办清单)

基于 **.NET 8** 与 **Avalonia 11** 构建的现代化跨平台桌面待办事项应用，采用标准 MVVM 架构，集成系统原生托盘、本地数据安全原子持久化、全局异常崩溃兜底与工业级滚动日志。

---

## 🏗 技术栈架构

- **UI 渲染引擎**: [Avalonia UI 11.1.0](https://avaloniaui.net/)（跨平台 XAML、Fluent 主题、编译期绑定）
- **MVVM 框架**: [CommunityToolkit.Mvvm 8.2.2](https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/)（源生成器、响应式属性与 RelayCommand）
- **依赖注入 (IoC/DI)**: `Microsoft.Extensions.DependencyInjection 8.0.0`
- **工业级日志**: [Serilog 4.0.0](https://serilog.net/)（控制台实时输出 + 本地文件按天滚动 + 异常降级防御）
- **矢量图标系统**: 原生 `StreamGeometry` + `<PathIcon>`（源自 Lucide Icons，无损缩放、暗黑主题自动适配、零第三方体积负担）
- **数据持久化**: 线程安全原子替换写入（`todos.json.tmp` -> `todos.json`）与损坏数据隔离保护
- **单元测试**: `xUnit 2.6.2` + `Microsoft.NET.Test.Sdk`

---

## 📁 解决方案目录结构

```text
AvaloniaTodoApp/
├── AvaloniaTodoApp.sln              # 解决方案总控文件
├── Directory.Build.props            # 全局构建控制与统一版本号
├── .editorconfig                    # 代码风格与静态代码规范
├── .gitignore                       # Git 忽略规则
├── AvaloniaTodoApp.csproj           # 桌面客户端主工程
├── App.axaml / App.axaml.cs         # 应用程序生命周期、DI 注入与系统托盘
├── Program.cs                       # 启动入口与全生命周期崩溃防御
├── Models/                          # 数据模型 (TodoItem, DateTimeOffset)
├── Services/                        # 核心服务 (ITodoStorageService, IUpdateService, LogHelper)
├── Resources/                       # 全局矢量图标字典 (Icons.axaml，高性能 StreamGeometry 硬件直绘)
├── ViewModels/                      # 视图模型 (MainWindowViewModel, AboutUpdateViewModel)
├── Views/                           # 视图层 (MainWindow, AboutUpdateWindow)
├── tests/
│   └── AvaloniaTodoApp.Tests/       # 自动化测试工程 (覆盖存储并发、SemVer、ViewModel)
└── scripts/
    ├── build.sh                     # macOS / Linux / Jenkins 原生打包脚本
    └── build.ps1                    # Windows PowerShell 原生打包脚本
```

---

## 🚀 快速上手

### 1. 环境准备
- 安装 [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)（`dotnet --version` 输出 `8.0.xxx`）

### 2. 本地开发与热重载调试
```bash
# 启动热重载开发调试模式
dotnet watch
```

### 3. 运行自动化测试
```bash
# 运行全部 19 项单元测试用例
dotnet test --nologo
```

---

## 📦 跨平台打包与发布

项目自带无需安装任何额外依赖的跨平台打包脚本：

### macOS / Linux 平台
```bash
# 1. 自动根据当前本机系统打包
./scripts/build.sh

# 2. 交叉编译打包 Windows 64位独立单文件版 (.exe)
./scripts/build.sh win-x64

# 3. 打包 macOS Apple Silicon (M系列芯片) 版
./scripts/build.sh osx-arm64

# 4. 一键构建 Windows + macOS + Linux 全部平台安装包
./scripts/build.sh all
```

### Windows 平台 (PowerShell)
```powershell
.\scripts\build.ps1 -Target win-x64
.\scripts\build.ps1 -Target all
```

> **构建输出**：产物将打包输出在 `./dist/` 目录，生成的 `.zip` 自带 .NET 运行时（`--self-contained`），目标机器**不需要安装任何 .NET 环境**即可双击直接运行。

---

## 🔄 CI/CD (Jenkins) 集成

在 Jenkins 任务的【构建步骤 (Execute shell)】中填入：

```bash
# 还原依赖
dotnet restore AvaloniaTodoApp.sln

# 质量门禁：运行全部自动化单元测试
dotnet test AvaloniaTodoApp.sln -c Release --no-restore

# 打包发布全平台安装包
chmod +x ./scripts/build.sh
./scripts/build.sh all
```

在【构建后操作】添加 **Archive the artifacts**，填入 `dist/*.zip` 即可自动收集安装包。

---

## 🛡 数据安全性设计亮点

1. **原子替换写盘**：先写入 `.tmp` 临时文件，完成后执行原子重命名，防止断电导致 JSON 文件损坏截断。
2. **合并式异步调度 (Coalescing Save)**：高频修改时单写入循环有序落盘，最后一次状态保证落盘，退出前调用 `FlushAsync()` 确保安全写毕。
3. **坏数据自动隔离**：遇到损坏数据文件时自动备份为 `.corrupted.{timestamp}`，绝不使用空列表覆写抹除原文件。
