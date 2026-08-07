# OrionDesk 项目概述

## 项目定位

轻量级 Windows 桌面小组件工具，让用户在桌面上放置实用组件，不遮挡应用程序窗口。

## 核心特性

- 组件固定在**桌面图标层**（与桌面图标同层，在应用程序下面）
- 毛玻璃背景、圆角边框、现代 UI 风格
- 自由拖拽定位和调整大小
- 支持锁定功能（锁定后无法移动）
- 配置持久化（同步写入 + 原子替换 + .gold 黄金备份）

## 技术栈

| 技术 | 用途 |
|------|------|
| .NET 10 | 运行时框架 |
| WPF | UI 框架 |
| MVVM | 架构模式 |
| PerformanceCounter | CPU/内存监控 |
| Win32 API | 桌面层级控制 |
| JSON | 配置存储（原子写入 + 备份） |
| QWeather API | 天气数据（和风天气） |
| ip-api.com | IP 定位（免费 HTTP） |

## 目录结构

```
D:\Projects\OrionDesk\
├── src/
│   ├── OrionDesk.UI/           # 表现层 (WPF)
│   │   ├── Windows/
│   │   │   ├── BaseWidgetWindow.cs   # 组件基类
│   │   │   ├── ClockWidget.xaml      # 时钟组件（含天气）
│   │   │   ├── MonitorWidget.xaml    # 系统监控
│   │   │   ├── LauncherWidget.xaml   # 启动器（含桌面图标隐藏）
│   │   │   ├── StickyNoteWidget.xaml # 便签
│   │   │   ├── FolderWidget.xaml     # 文件夹映射
│   │   │   └── SettingsWindow.xaml   # 设置页面
│   │   └── MainWindow.xaml           # 托盘管理
│   │
│   ├── OrionDesk.BLL/          # 业务逻辑层
│   │   ├── Models/                   # 数据模型
│   │   └── Services/                 # 业务服务
│   │
│   └── OrionDesk.DAL/          # 数据访问层
│       ├── ConfigRepository.cs       # JSON 读写（原子写入+备份）
│       └── DataPath.cs               # 路径管理
│
└── .skills/                    # 项目状态管理
```

## 已实现组件

| 组件 | 状态 | 功能 |
|------|------|------|
| 时钟 | ✅ 完成 | 数字/模拟样式、农历、天气+空气质量+预报+预警+指数+天文、锁定 |
| 系统监控 | ✅ 完成 | CPU/内存/硬盘使用率、GB显示、锁定 |
| 启动器 | ✅ 完成 | 拖拽添加、图标显示、重命名、桌面图标智能隐藏、锁定 |
| 便签 | ✅ 完成 | 多颜色、防抖保存、锁定 |
| 文件夹映射 | ✅ 完成 | 树形结构、懒加载、拖入文件夹、锁定 |

## 诊断工具

| 工具 | 状态 | 功能 |
|------|------|------|
| 诊断监控 | ✅ 完成 | 进程内存/GDI/USER/线程/句柄/GC 实时监控、CSV 日志记录、趋势分析 |
