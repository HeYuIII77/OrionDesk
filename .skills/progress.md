# Development Progress

## 2026-08-06

### Completed
- 项目骨架搭建（三层架构：UI/BLL/DAL）
- 时钟组件（数字/模拟样式、农历节气、锁定功能）
- 系统监控组件（CPU/内存/硬盘使用率、GB显示、锁定功能）
- 启动器组件（拖拽添加、图标显示、重命名、多个实例、锁定功能）
- 便签组件（多颜色、防抖保存、锁定功能）
- 托盘图标管理系统
- 单例模式实现
- 桌面层级控制（WorkerW 窗口技术）
- 内存监控修复（使用 Win32 API GlobalMemoryStatusEx）
- 图标清晰度优化（32x32 大图标 + 高质量渲染）

### Added
- LunarCalendarService - 农历和节气计算服务
- BaseWidgetWindow - 组件基类（桌面层级、拖拽、锁定）
- WidgetManager - 组件管理器
- SystemMonitorService - 系统监控服务
- ConfigRepository - JSON 配置仓库

### Modified
- 时钟组件 - 添加农历节气显示
- 系统监控组件 - 修复内存显示、GB格式、锁按钮位置
- 启动器组件 - 添加重命名、多个实例支持、图标优化
- 所有组件 - 添加锁定功能

### 下午 Session

### Completed
- 天气 API 集成（和风天气 QWeather，IP 定位 + 实时天气 + 设置页面）
- 组件持久化修复（SaveSync 同步保存 + .bak 备份机制 + 原子写入）
- 桌面图标智能隐藏（OrionDesk 启动时隐藏名单图标，退出时恢复）
- 文件夹映射组件（TreeView 树形结构、懒加载、拖入文件夹路径）
- 所有组件 JsonElement 类型兼容（ToBool/ToInt/ToDouble/ToStr）
- 所有组件 OnClosed null 检查
- 隐藏窗口初始化修复（Loaded → 构造函数直接初始化）
- HttpClient gzip 解压支持
- WorkerW fallback（Topmost 降级）
- LauncherItem.ShortcutName 支持桌面图标恢复

### Added
- WeatherSettings - 天气设置模型（ApiKey、ApiHost、RefreshMinutes）
- WeatherService - 天气服务（IP 定位 via ip-api.com + QWeather 实时天气）
- SettingsWindow - 设置窗口（托盘菜单 → 设置）
- FolderWidget - 文件夹映射组件（TreeView 树形结构）

### Modified
- AppSettings - 添加 WeatherSettings 属性（永不为 null）
- MainWindow - 添加 WeatherService、设置菜单、SaveSync 同步保存
- ClockWidget - 集成天气显示、构造函数直接初始化
- BaseWidgetWindow - LoadLockState JsonElement 兼容、ToBool/ToInt 等辅助方法、WorkerW fallback
- LauncherWidget - 桌面图标智能隐藏/恢复、ShortcutName 存储
- LauncherItem - 添加 ShortcutName 属性
- StickyNoteWidget - OnClosed null 检查
- MonitorWidget - OnClosed null 检查
- ConfigRepository - SaveSync 同步保存、原子写入（tmp+rename+bak）、备份恢复

### 晚间 Session

### Completed
- 组件桌面层级统一（移除 Topmost 降级，固定 WorkerW + HWND_BOTTOM）
- 组件大小持久化（移除 XAML 硬编码 Width/Height，基类从配置读取）
- 文件夹映射树节点文字颜色修复（CreateDirectoryNode/CreateFileNode 设白色）
- 开机启动修复（Environment.ProcessPath 替代 Assembly.Location，路径加引号）
- 配置持久化全面重构（同步写入、IsRestoring 保护、.gold 黄金备份）
- Windows 关机不丢失组件（Closing 事件设 _isClosingAll，防止 Closed 删除配置）

### Modified
- BaseWidgetWindow - 移除 Topmost 降级、移除鼠标悬浮层级变化、SavePosition 改同步、IsRestoring 检查
- ConfigRepository - 全面重写：Load/Save/CreateGoldSnapshot，.gold 首次创建永不覆盖，.bak 仅主文件有效时覆盖
- WidgetManager - 简化为 Load/Save，添加 IsRestoring 标志
- MainWindow - 同步恢复、Closing 统一处理关机/退出、移除 SaveAndCleanupAsync
- ClockWidget.xaml / MonitorWidget.xaml / LauncherWidget.xaml / StickyNoteWidget.xaml / FolderWidget.xaml - 移除硬编码 Width/Height
- FolderWidget.xaml.cs - CreateDirectoryNode/CreateFileNode 添加 Foreground=White
- ClockWidget.xaml.cs / StickyNoteWidget.xaml.cs / LauncherWidget.xaml.cs / FolderWidget.xaml.cs - Save 改同步 + IsRestoring 检查

### 天气城市选择

### Completed
- 天气设置支持手动选择城市（VPN 场景下 IP 定位不准）
- 内置中国 130+ 主要城市数据库（省会 + 地级市 + 特别行政区）
- 设置页面添加城市搜索 UI（输入框 + 搜索按钮 + 结果列表 + 清除按钮）
- WeatherSettings 新增 CityName/CityLat/CityLon
- WeatherService 新增 SearchCityAsync（本地模糊匹配）
- GetWeatherAsync 优先使用配置城市，未配置时回退 IP 定位
- 设置保存后自动清除天气缓存
- 注：和风天气 GeoAPI 被安全限制（403），外部地理编码 API 不可用，改用内置数据库

### Modified
- WeatherSettings - 添加 CityName、CityLat、CityLon 属性
- WeatherService - 添加 SearchCityAsync（内置城市数据库模糊匹配）、GetWeatherAsync 支持城市参数
- SettingsWindow - 添加城市搜索 UI、WeatherService 依赖注入
- ClockWidget - UpdateWeather 传入城市配置、添加 RefreshWeather 公开方法
- MainWindow - SettingsWindow 构造函数更新、保存后清除天气缓存并立刻刷新所有时钟组件

### 天气立刻刷新

### Completed
- 设置保存后立刻刷新天气（不需要等定时器）
- ClockWidget 添加 RefreshWeather 公开方法（停止定时器→刷新→重启定时器）
- MainWindow 遍历 _activeWidgets 调用所有 ClockWidget.RefreshWeather()

## 2026-08-07

### Completed
- 启动器快捷方式名称修复（优先使用 .lnk 快捷方式名称，不再用目标 exe 文件名）
- 启动器列表视图（类似资源管理器，每行：16x16 图标 + 名称 + 路径）
- 启动器图标/列表视图切换（右键菜单，持久化 ViewMode）
- 启动器滚动条支持（ScrollViewer 包裹，内容超长自动显示）
- 移除节气功能（SolarTerms 数组、GetCurrentSolarTerm 方法、时钟组件节气显示）
- 自定义 logo.ico（嵌入 exe 图标，托盘图标从 exe 提取）

### 稳定性改进
- 全局异常处理（DispatcherUnhandledException、AppDomain.UnhandledException、TaskScheduler.UnobservedTaskException）
- GDI 句柄泄漏修复（LauncherWidget ExtractIcon 统一处理 bitmap/hBitmap 释放）
- COM 对象泄漏修复（LauncherWidget AddApplication finally 释放）
- WeatherService 退出时释放
- ConfigRepository 保存加 lock 防并发 + 文件操作重试 3 次
- StickyNoteWidget 移除冲突拖拽逻辑，复用 BaseWidgetWindow
- LauncherWidget RefreshView 空引用保护

### UI 美化
- App.xaml 集中配色系统（BgColor/SurfaceColor/BorderColor/AccentColor 等）
- App.xaml 统一 GlassBackground、ButtonStyle、SecondaryButtonStyle、InputStyle、LockButtonStyle
- App.xaml 统一 ProgressBarStyle（渐变色：绿→黄→红）
- App.xaml 添加 BooleanToVisibilityConverter（BoolToVis）
- BaseWidgetWindow 添加 DropShadowEffect 投影效果
- ClockWidget 优化（Segoe UI Light 字体、增加刻度线、更柔和配色）
- MonitorWidget 进度条改用渐变色样式
- FolderWidget TreeView 自定义展开/折叠箭头（▶/▼）、选中/悬停高亮
- LauncherWidget 使用统一 LockButtonStyle
- SettingsWindow 分组标题、分隔线、聚焦高亮输入框
- StickyNoteWidget 圆角统一、按钮透明度优化

### 吸附对齐
- BaseWidgetWindow 添加静态组件注册表（_allWidgets）
- 拖拽时自动吸附到其他组件的边缘（左/右/上/下/中，8px 阈值）
- MainWindow 创建/关闭组件时注册/注销

### Modified
- LauncherWidget.xaml - 添加 ScrollViewer、ListItemButtonStyle、视图切换菜单
- LauncherWidget.xaml.cs - 新增 RefreshView/AddAppListItem/GetSmallIcon/LaunchItem/DeleteItem，视图模式切换逻辑
- LauncherSettings - 添加 ViewMode 属性
- LunarCalendarService - 移除 SolarTerms 数组和 GetCurrentSolarTerm 方法
- ClockWidget.xaml.cs - UpdateLunarInfo 移除节气调用
- OrionDesk.UI.csproj - 添加 ApplicationIcon 指向 logo.ico
- MainWindow.xaml.cs - GetAppIcon 从 exe 提取嵌入图标、RegisterWidget/UnregisterWidget
- App.xaml - 配色系统、统一样式、BoolToVis 转换器
- BaseWidgetWindow.cs - 吸附对齐逻辑（CalculateSnap/TrySnap）

### 天气功能扩展

### Completed
- 天气 API 全面集成（5 个新 API：空气质量、3天预报、天气预警、生活指数、日出日落）
- WeatherService 扩展（新增 GetAirNowAsync/GetForecast3dAsync/GetWarningNowAsync/GetIndices1dAsync/GetSunriseSunsetAsync）
- 智能缓存策略（实时天气跟随刷新间隔、每日数据按日期缓存、预警 15 分钟独立缓存）
- WeatherInfo 扩展（新增 Aqi/AirLevel/AirCategory/Pm2p5/Pm10/Forecast/Warnings/Indices/Sunrise/Sunset）
- 新增数据模型（ForecastDay、WeatherWarning、WeatherIndex）
- WeatherDetailWindow 天气详情弹窗（分组展示：实时天气/空气质量/预警/3天预报/生活指数/天文）
- ClockWidget 天气摘要行优化（城市 天气 温度 空气等级）
- ClockWidget ToolTip 详细信息（悬停显示完整天气数据）
- ClockWidget 右键菜单添加"天气详情"入口

### Modified
- WeatherService.cs - 新增 5 个 API 调用方法、3 组缓存字段、ClearCache 扩展、WeatherInfo 扩展
- ClockWidget.xaml - 添加"天气详情"右键菜单项
- ClockWidget.xaml.cs - UpdateWeather 重写（摘要+ToolTip）、新增 BuildWeatherToolTip/FormatForecastDate/ShowWeatherDetail_Click、_lastWeatherInfo 字段
- WeatherDetailWindow.xaml - 新建天气详情弹窗 XAML
- WeatherDetailWindow.xaml.cs - 新建动态构建天气详情 UI

### 暗色滚动条

### Completed
- App.xaml 新增暗色滚动条样式（DarkScrollBarVertical/Horizontal、DarkScrollViewerStyle）
- 滑块半透明白色圆角条，悬停/拖拽变亮，轨道透明，无箭头按钮
- 应用到 SettingsWindow、WeatherDetailWindow、LauncherWidget、FolderWidget TreeView

### Modified
- App.xaml - 新增 ScrollBarLineButtonStyle、ScrollBarTrackStyle、VerticalThumbStyle、HorizontalThumbStyle、DarkScrollBarVertical、DarkScrollBarHorizontal、DarkScrollViewerStyle
- SettingsWindow.xaml - ScrollViewer 应用 DarkScrollViewerStyle
- WeatherDetailWindow.xaml - ScrollViewer 应用 DarkScrollViewerStyle
- LauncherWidget.xaml - ScrollViewer 应用 DarkScrollViewerStyle
- FolderWidget.xaml - TreeView 样式禁用内置滚动，外层 ScrollViewer 应用 DarkScrollViewerStyle

### 设置窗口布局修复

### Completed
- SettingsWindow 改为 ScrollViewer 包裹内容 + 按钮固定底部
- 窗口改为可调整大小（ResizeMode=CanResizeWithGrip），最小高度 480
- 修复刷新频率和按钮被窗口底部遮挡的问题

### Modified
- SettingsWindow.xaml - Grid 重构为 3 行（标题/ScrollViewer/按钮），移除固定 Height=540

### 文件夹映射简化

### Completed
- 文件仅显示名称.扩展名（移除大小和修改时间）
- 移除不再使用的 FormatSize 方法

### Modified
- FolderWidget.xaml.cs - CreateFileNode 简化 Header、移除 FormatSize 方法

### 便携版发布

### Completed
- OrionDesk.UI.csproj 添加发布配置（PublishSingleFile/SelfContained/win-x64）
- dotnet publish 生成 72MB 自包含单文件 exe（.NET 运行时内置）
- 便携版路径：publish\OrionDesk.UI.exe，双击即用无需安装

### Modified
- OrionDesk.UI.csproj - 添加 PublishSingleFile、SelfContained、RuntimeIdentifier、EnableCompressionInSingleFile 等发布属性
