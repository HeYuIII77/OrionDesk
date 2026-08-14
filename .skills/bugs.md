# Bug Tracker

## Bug: 组件窗口无法从外部拖入文件（已修复）

**Status**: Resolved — 恢复 WM_DROPFILES 直接注册方式
**Reproduction**: 从资源管理器或桌面拖拽文件到启动器/文件夹映射/Git同步监控组件，拖放无效
**Impact**: 启动器无法通过拖放添加应用
**Root Cause**: commit ba22b13 将 BaseWidgetWindow 的拖放方式从直接注册 WM_DROPFILES 改为 DropOverlayWindow 覆盖窗口代理。但 DropOverlayWindow 返回 HTNOWHERE 导致系统不路由 WM_DROPFILES 消息。
**修复**: 恢复原始实现，在 OnWindowLoaded 中直接注册 WM_DROPFILES（在 SetDesktopLevel 之前），WndProc 处理消息并调用 OnFileDrop。

## Bug: 运行一段时间后组件全部消失（已修复）

**Status**: Resolved — WorkerW 句柄有效性检查 + Explorer 重启检测
**Reproduction**: 运行 OrionDesk 一段时间后，桌面组件全部消失（存在但不显示），退出重进也不恢复
**Impact**: 组件完全不可用
**Root Cause**: Windows Explorer 重启后 WorkerW 窗口被重建，但代码缓存的旧句柄未失效（非 IntPtr.Zero），导致 SetParent 失败
**修复**:
1. 使用 IsWindow API 验证 WorkerW 句柄有效性
2. 注册 TaskbarCreated 消息检测 Explorer 重启
3. 每 30 秒定时检查 WorkerW 句柄
4. Explorer 重启后自动刷新桌面层级

## Bug: 组件位置向右下偏移（已修复）

**Status**: Resolved — 移除窗口级别 DropShadowEffect
**Reproduction**: 打开 OrionDesk 后，所有组件位置比预期偏右下
**Impact**: 组件位置不准确
**Root Cause**: BaseWidgetWindow 的 DropShadowEffect（BlurRadius=10）在 AllowsTransparency=true 窗口上导致视觉边界扩展，造成位置偏移
**修复**: 移除窗口级别的 DropShadowEffect

## Bug: 内存监控显示 0%

**Status**: Resolved — 使用 Win32 API GlobalMemoryStatusEx 获取系统真实内存
**Reproduction**: 添加系统监控组件，内存显示为 0%
**Impact**: 内存使用率无法正确显示
**Resolution**: 修改 SystemMonitorService，使用 Win32 API 替代 GC.GetGCMemoryInfo()

## Bug: 启动器图标模糊

**Status**: Resolved — 使用大图标(32x32) + 高质量渲染
**Reproduction**: 拖拽应用到启动器，图标显示模糊
**Impact**: 用户体验差
**Resolution**: 修改 GetFileIcon 方法，使用 SHGFI_LARGEICON + 高质量位图渲染

## Bug: 系统监控锁按钮与 CPU 使用率重叠

**Status**: Resolved — 锁按钮单独一行，整体往下移动
**Reproduction**: 添加系统监控组件，锁按钮和 CPU 文字重叠
**Impact**: 界面显示异常
**Resolution**: 修改 XAML 布局，锁按钮单独一行

## Bug: 时钟组件启动时 NullReferenceException

**Status**: Resolved — 将 UpdateClockStyle 移到 Loaded 事件
**Reproduction**: 添加时钟组件时崩溃
**Impact**: 无法添加时钟组件
**Resolution**: 在窗口 Loaded 事件中调用 UpdateClockStyle，确保 XAML 控件已初始化

## Bug: 天气 API 返回 403 Invalid Host

**Status**: Resolved — 使用项目专属 API Host（m24d95fgre.re.qweatherapi.com）
**Reproduction**: 调用 devapi.qweather.com 返回 403
**Impact**: 天气功能无法使用
**Resolution**: 和风天气每个项目有专属 API Host，需要在设置中配置

## Bug: 组件持久化失败（退出后配置丢失）

**Status**: Resolved — 改用 SaveSync 同步写入 + 原子替换 + 备份机制
**Reproduction**: 配置天气 API 后退出，重启后配置为空
**Impact**: 所有配置无法持久化
**Resolution**: async void SaveConfigAsync 在退出时未完成写入，改用 SaveSync 同步保存

## Bug: 隐藏窗口 Loaded 事件不触发

**Status**: Resolved — 改为构造函数直接初始化
**Reproduction**: 启动 OrionDesk 后组件时间显示 00:00:00
**Impact**: 组件定时器不启动，天气不更新
**Resolution**: 所有组件从 Loaded 事件改为构造函数中直接初始化

## Bug: JsonElement 类型转换异常

**Status**: Resolved — 添加 ToBool/ToInt/ToDouble/ToStr 辅助方法
**Reproduction**: 启动时组件恢复失败，报 InvalidCastException
**Impact**: 锁定的组件无法加载
**Resolution**: System.Text.Json 反序列化后 Settings 值是 JsonElement，需要特殊处理

## Bug: StickyNoteWidget OnClosed NullReferenceException

**Status**: Resolved — 添加 null 检查
**Reproduction**: 退出程序时便签组件报错
**Impact**: 退出时异常
**Resolution**: _saveTimer?.Stop() 添加 null 条件操作符

## Bug: 组件盖在应用程序上面（层级不统一）

**Status**: Resolved — 移除 Topmost 降级，固定 WorkerW + HWND_BOTTOM
**Reproduction**: 部分组件在应用上面，部分在下面
**Impact**: 组件层级不一致
**Resolution**: 移除所有 Topmost=true 降级，使用 HWND_BOTTOM 固定在桌面图标层

## Bug: 组件大小重启后丢失

**Status**: Resolved — 移除 XAML 硬编码 Width/Height
**Reproduction**: 调整组件大小后重启，恢复默认大小
**Impact**: 用户自定义大小无法保存
**Resolution**: XAML 中的 Width/Height 在 InitializeComponent 时覆盖了配置值，移除后由基类从配置读取

## Bug: 开机启动不生效

**Status**: Resolved — 使用 Environment.ProcessPath
**Reproduction**: 设置开机启动后重启，OrionDesk 不自动启动
**Impact**: 开机启动功能无效
**Resolution**: Assembly.GetExecutingAssembly().Location 在 .NET 10 下返回空字符串，改用 Environment.ProcessPath

## Bug: Windows 关机/重启后组件丢失

**Status**: Resolved — Closing 事件设置 _isClosingAll + 配置持久化重构
**Reproduction**: 关机前有5个组件，重启后只剩部分
**Impact**: 组件配置丢失
**Resolution**: 多个原因：1) 异步保存未完成进程就被杀 2) 恢复期间 SavePosition 覆盖配置 3) Windows 关机时 Closed 事件先于 SessionEnding 触发删除配置。统一改为同步保存 + IsRestoring 保护 + Closing 设 _isClosingAll

## Bug: ProgressBar ControlTemplate 多子元素错误

**Status**: Resolved — 用 Grid 包裹多个 Border
**Reproduction**: 编译时报 MC3089 "ControlTemplate 只能接受一个子级"
**Impact**: 无法编译
**Resolution**: App.xaml 和 MonitorWidget.xaml 的 ProgressBar 模板中，多个 Border 元素用 Grid 包裹

## Bug: 缺少 LockButtonStyle 资源

**Status**: Resolved — 添加到 App.xaml
**Reproduction**: 运行时报 XamlParseException "找不到 LockButtonStyle 资源"
**Impact**: 组件无法启动
**Resolution**: 在 App.xaml 中添加 LockButtonStyle 定义

## Bug: TextBlock 不支持 LetterSpacing 属性

**Status**: Resolved — 移除该属性
**Reproduction**: 编译时报 MC4005 "找不到 LetterSpacing Property"
**Impact**: 无法编译
**Resolution**: WPF TextBlock 没有 LetterSpacing 属性，从 ClockWidget.xaml 移除

## Bug: BoolToVis 资源找不到

**Status**: Resolved — 移到 App.xaml
**Reproduction**: 运行时报 "无法找到名为 BoolToVis 的资源"
**Impact**: FolderWidget 无法启动
**Resolution**: BooleanToVisibilityConverter 从 FolderWidget.xaml 移到 App.xaml（ControlTemplate 内引用需要全局资源）

## Bug: LauncherWidget 图标切列表后内容消失

**Status**: Resolved — 每次切列表都重新设置 ContentScroller.Content
**Reproduction**: 图标模式→切列表→切回图标→再切列表，列表内容为空
**Impact**: 第二次及之后从图标切到列表模式不显示任何内容
**Resolution**: RefreshView 中 `_listPanel` 非 null 时跳过了 `ContentScroller.Content = _listPanel` 赋值，但此时 Content 已被切回 IconPanel。修复为每次切到列表模式都重新赋值

## Bug: FolderWidget 启动后根节点展开但无子内容

**Status**: Resolved — 先 Add 到树再设置 IsExpanded
**Reproduction**: 启动 OrionDesk，文件夹映射组件根节点已展开但子目录不显示，需手动关闭再展开
**Impact**: 启动时无法看到根目录下的文件和文件夹
**Resolution**: IsExpanded=true 在 FolderTree.Items.Add() 之前设置，节点不在视觉树中导致懒加载不生效。调换顺序：先 Add 再 IsExpanded
