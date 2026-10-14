# Web 离线前端组件库 (Vendor Libraries)

本目录存放看板页面用到的前端基础库独立离线文件，供本地开发与引用备份：

- `three.min.js`: Three.js 核心 3D 渲染引擎 (r128)
- `OrbitControls.js`: Three.js 3D 轨道控制器与手势交互插件
- `tailwindcss.js`: Tailwind CSS 样式生成库 (备份)

> **说明**：为了确保在 Avalonia WebView 控件中通过 `HtmlContent` 字符串加载时不依赖文件系统相对路径和网络连接，`chip_dashboard.html` 中已完整内置了 Three.js 核心库、OrbitControls 插件，并将所有 Tailwind CSS 样式预编译为纯静态原生 CSS 内联到 `<style>` 中，杜绝了动态 JIT 编译与 MutationObserver 在内网或沙箱环境中的阻塞风险，并具备 WebGL 双模兼容与自愈机制，在任何隔离内网环境下均可 100% 秒开渲染运行。
