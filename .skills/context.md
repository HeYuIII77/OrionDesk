# Current Development Context

**Last updated**: 2026-08-12

## Currently Working On

- 稳定性与一致性优化阶段（P0 已完成，P1 部分完成）

## Current Module

- 全局：稳定性修复 + 交互/视觉统一

## Completed This Session

### 拖放功能修复
- ✅ BaseWidgetWindow 添加 Win32 WM_DROPFILES 支持（DragAcceptFiles + WndProc 钩子）
- ✅ 新增 AcceptFileDrop 属性和 OnFileDrop 虚方法
- ✅ LauncherWidget 切换到 Win32 拖放
- ✅ DocWidget 切换到 Win32 拖放（保留 TreeViewItem 内部拖放）
- ✅ FolderWidget 切换到 Win32 拖放
- ✅ GitSyncWidget 切换到 Win32 拖放

### 文档中心组件

### 文档中心组件
- ✅ DocWidget 树形结构浏览（指定根目录、懒加载）
- ✅ 搜索功能（文件名模糊搜索）
- ✅ 拖放归档（从 Windows 拖入 → 移动到目标目录，冲突处理）
- ✅ 右键菜单（新建 Markdown/文本文档/文件夹、打开、重命名、删除、资源管理器、复制路径）
- ✅ DocSettingsWindow 设置窗口
- ✅ MainWindow 托盘菜单集成

### 禁用 Alt+F4
- ✅ BaseWidgetWindow 添加 IsAppClosing + RequestClose
- ✅ OnClosing 拦截 Alt+F4，允许右键关闭和应用退出
- ✅ 全部 10 个组件 Close_Click 改为 RequestClose

### 背景透明度
- ✅ GlassBackground 从 #E61E1E21 (90%) → #991E1E21 (60%)

### P0 稳定性修复
- ✅ _allWidgets 静态 List 加锁（_allWidgetsLock），迭代用 ToArray 快照
- ✅ WeatherService 30 处裸 GetProperty → SafeGetString/SafeGetDouble
- ✅ MainWindow LoadAndRestore 启动失败添加托盘消息提示
- ✅ ConfigRepository 4 处空 catch 添加 Debug.WriteLine

### P1 交互统一（部分）
- ✅ 锁定按钮位置统一到左侧（6 个组件从右侧改到左侧）
- ✅ StickyNoteWidget 锁按钮尺寸统一为 24x24/FontSize=11
- ✅ 日历组件锁按钮与"今天"按钮布局修复（并排 StackPanel）

### 发布
- ✅ dotnet publish 生成 72MB 自包含单文件 exe

## Not Yet Complete

- P1 续：右键菜单结构统一（8 个 XAML）
- P2：视觉统一（引用 App.xaml 资源、标题栏统一）
- P3：代码清理（删除 Class1.cs、Timer 清理）
- P4：全局搜索（Ctrl+Space）
- 长时间运行稳定性验证（24h+）

## Blockers

- 无

## Next Steps

- 继续 P1：右键菜单结构统一
- P2：视觉统一（引用 App.xaml 配色/文本/圆角资源）
- P3：代码清理
- P4：全局搜索设计与实现
