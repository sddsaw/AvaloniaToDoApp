# Web 离线前端组件库 (Vendor Libraries)

本目录存放看板页面用到的前端基础库独立离线文件，供本地开发与引用备份：

- `three.min.js`: Three.js 核心 3D 渲染引擎 (r128)
- `OrbitControls.js`: Three.js 3D 轨道控制器与手势交互插件
- `tailwindcss.js`: Tailwind CSS 运行时 JIT 样式编译器

> **说明**：为了确保在 Avalonia WebView 控件中通过 `HtmlContent` 字符串加载时不依赖文件系统相对路径和 CORS 跨域策略，`chip_dashboard.html` 中已完整内置内联了以上脚本，在任何无公网内网环境下均可 100% 独立离线渲染运行。
