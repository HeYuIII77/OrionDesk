# Design Decisions

## 2026-08-06

### Decision: 使用 WorkerW 窗口技术实现桌面层级

**Reason**: 需要将组件放在桌面图标之上、应用程序之下，WorkerW 是实现这一效果的标准方法

**Impact**: 所有组件窗口都需要继承 BaseWidgetWindow，通过 Win32 API 控制层级

**Status**: Active

### Decision: 使用 JSON 存储配置

**Reason**: 简单直接，适合配置类数据，无需额外依赖

**Impact**: 配置文件存储在 %LocalAppData%\OrionDesk\config.json

**Status**: Active

### Decision: 使用 PerformanceCounter 获取系统监控数据

**Reason**: .NET 原生 API，无需额外依赖，功能够用

**Impact**: 需要添加 System.Diagnostics.PerformanceCounter NuGet 包

**Status**: Active

### Decision: 实现单例模式

**Reason**: 避免多个实例同时运行造成冲突

**Impact**: 使用 Mutex 实现，启动时检查是否已有实例运行

**Status**: Active

### Decision: 组件支持锁定功能

**Reason**: 防止误操作移动或调整组件大小

**Impact**: 所有组件右上角添加锁定按钮，锁定后无法拖拽和调整大小

**Status**: Active

## 2026-08-06 下午

### Decision: 使用和风天气 QWeather API 集成天气

**Reason**: 用户提供 API Key，免费版每天 5000 次调用

**Impact**: 时钟组件显示天气，需要设置页面配置 API Key 和 API Host

**Status**: Active

### Decision: 使用 ip-api.com 进行 IP 定位

**Reason**: 和风天气 GeoAPI 对桌面应用有安全限制（403），ip-api.com 免费无需 Key

**Impact**: 通过 IP 获取坐标，再用坐标查询天气

**Status**: Active

### Decision: 配置保存使用同步写入（SaveSync）

**Reason**: 异步保存在程序退出时可能未完成，导致配置丢失

**Impact**: 关键保存点使用同步写入 + 原子替换（tmp+rename+bak）

**Status**: Active

### Decision: 桌面图标智能隐藏策略

**Reason**: 启动器中的应用在 OrionDesk 运行时隐藏桌面图标，退出时恢复

**Impact**: 启动时隐藏名单图标，退出时全部恢复，使用备份文件夹存储

**Status**: Active

### Decision: 组件初始化从 Loaded 改为构造函数直接调用

**Reason**: 隐藏窗口的 Loaded 事件不触发，导致组件不初始化

**Impact**: 所有组件在构造函数中直接初始化

**Status**: Active

### Decision: System.Text.Json JsonElement 类型兼容

**Reason**: 反序列化后 Settings 值是 JsonElement，Convert.ToXxx 无法转换

**Impact**: 添加 ToBool/ToInt/ToDouble/ToStr 辅助方法

**Status**: Active

## 2026-08-06 晚间

### Decision: 组件固定在桌面图标层，不随鼠标悬浮提升

**Reason**: 用户需求，简化交互逻辑，避免 z-order 切换导致的 bug

**Impact**: 移除 OnMouseEnter/OnMouseLeave 的 SetWindowPos 调用，只保留透明度动画

**Status**: Active

### Decision: 配置保存全部改为同步

**Reason**: 异步保存在进程退出/关机时可能未完成，导致配置丢失

**Impact**: 所有 SaveAsync 替换为 Save，移除 Task.Run 包装

**Status**: Active

### Decision: .gold 黄金备份（首次加载创建，永不覆盖）

**Reason**: .bak 每次保存都覆盖，如果保存时数据已损坏，.bak 也被覆盖为坏数据

**Impact**: .gold 在首次加载成功时创建，作为最后的恢复手段；.bak 仅在主文件有效时才覆盖

**Status**: Active

### Decision: IsRestoring 标志防止恢复期间保存

**Reason**: 组件初始化时 SizeChanged 等事件会触发 SavePosition，在所有组件恢复完之前保存会覆盖为不完整配置

**Impact**: WidgetManager.IsRestoring=true 期间所有 Save 调用被跳过，恢复完成后统一保存一次

**Status**: Active

### Decision: Closing 事件统一处理退出和关机

**Reason**: Windows 关机时 Closed 事件先于 SessionEnding 触发，_isClosingAll 还是 false 导致组件删除配置

**Impact**: 在 Closing 事件中设 _isClosingAll=true 并保存配置，后续 Closed 事件不再删除配置

**Status**: Active

### Decision: 设置保存后立刻刷新天气

**Reason**: 用户体验：保存城市设置后应该立刻看到新城市的天气，而不是等下一个定时器周期

**Impact**: ClockWidget 添加 RefreshWeather 公开方法，MainWindow 保存后遍历所有活动组件调用刷新

**Status**: Active

### Decision: 天气支持手动选择城市（内置城市数据库）

**Reason**: VPN 导致 IP 定位不准确，用户需要手动指定天气城市；和风天气 GeoAPI 被安全限制（403），外部地理编码 API 访问受限，改用内置城市数据库

**Impact**: 内置中国 130+ 主要城市坐标，设置页面输入城市名模糊搜索，配置文件存储 CityName/CityLat/CityLon，WeatherService 优先使用配置城市，未配置时回退到 IP 定位

**Status**: Active

## 2026-08-07

### Decision: 移除节气功能

**Reason**: 节气日期边界计算不准（简化日期范围算法误差），修复复杂度高，用户选择直接移除

**Impact**: LunarCalendarService 移除 SolarTerms 数组和 GetCurrentSolarTerm 方法，时钟组件只显示农历

**Status**: Active

### Decision: App.xaml 集中管理全局样式

**Reason**: 各组件各自定义 GlassBackground 等样式，值略有不同，维护困难

**Impact**: 配色系统、按钮样式、输入框样式、进度条样式、锁定按钮样式统一在 App.xaml 定义

**Status**: Active

### Decision: 组件添加投影效果（DropShadowEffect）

**Reason**: 组件平铺在桌面上缺乏层次感，需要视觉深度

**Impact**: BaseWidgetWindow 构造函数添加 DropShadowEffect（黑色、模糊 10px、透明度 25%）

**Status**: Active

### Decision: 拖拽吸附对齐（8px 阈值）

**Reason**: 用户需要精确排列多个组件（竖排/横排对齐）

**Impact**: BaseWidgetWindow 添加静态组件注册表和吸附计算逻辑，支持左/右/上/下/中 10 种对齐方式

**Status**: Active

### Decision: 天气数据分层展示（摘要+ToolTip+详情弹窗）

**Reason**: 时钟组件空间有限，不能塞太多文字；分层展示兼顾信息密度和界面简洁

**Impact**: 主界面一行摘要（城市 天气 温度 空气等级），鼠标悬停 ToolTip 显示详细信息，右键菜单打开 WeatherDetailWindow 完整展示

**Status**: Active

### Decision: 天气 API 智能缓存策略

**Reason**: 不同数据更新频率不同，统一刷新浪费 API 调用次数

**Impact**: 实时天气+空气质量跟随 RefreshMinutes（默认 30 分钟），预报/天文/指数每天缓存一次，预警 15 分钟独立刷新

**Status**: Active

### Decision: 暗色滚动条统一样式

**Reason**: 默认白色滚动条在深色背景上突兀，需要与整体 UI 风格一致

**Impact**: App.xaml 定义 DarkScrollViewerStyle，半透明白色圆角滑块+透明轨道，应用到所有 ScrollViewer 和 TreeView

**Status**: Active

### Decision: 便携版发布（自包含单文件）

**Reason**: 简化分发，用户无需安装 .NET 运行时，双击 exe 即可使用

**Impact**: dotnet publish 生成 72MB 单文件 exe，包含 .NET 10 运行时 + WPF 框架

**Status**: Active

### Decision: Git 同步监控使用 Shell 调用 git CLI

**Reason**: 用户已安装 git（使用 Gitee 必备），零依赖，行为可预测；LibGit2Sharp 对 .NET 10 兼容不确定且增加 70MB+ 包体积

**Impact**: 通过 Process.Start 调用 git 命令，需要 git 在 PATH 中

**Status**: Active

### Decision: Git 仓库自动扫描模式

**Reason**: 用户 D:\Project\C# 下有多个 git 项目，手动添加太麻烦

**Impact**: 扫描指定目录下所有子目录，自动发现含 .git 的仓库；非 git 目录也显示但标记为"无仓库"

**Status**: Active

### Decision: Git 刷新频率设为全局配置

**Reason**: 用户要求在设置页面统一配置，而非每个组件单独设置

**Impact**: AppSettings 添加 GitSyncRefreshMinutes，SettingsWindow 添加对应输入框，GitSyncWidget 从全局设置读取

**Status**: Active

### Decision: ISC 许可证

**Reason**: 用户指定 ISC 许可证，版权归属"星月拾貳"，中英双语

**Impact**: README.md 更新许可证信息，新建 LICENSE 文件

**Status**: Active

### Decision: 快捷工具默认预置 + 可自由编辑删除

**Reason**: 用户要求默认提供常用工具，但所有工具（包括预置的）都可以自由编辑和删除

**Impact**: 首次加载写入 16 个预置工具，移除 IsPreset 限制，编辑/删除对所有工具生效

**Status**: Active

### Decision: SVN 支持集成到现有 GitSyncWidget

**Reason**: 用户公司同时使用 Git 和 SVN，需要在同一组件中统一展示

**Impact**: GitSyncService 扩展 SVN 发现和检查逻辑，新增 VcsType 枚举，UI 显示 [Git]/[SVN] 标签

**Status**: Active

### Decision: 日历事项使用 JSON 存储

**Reason**: 与现有组件一致，无需引入 SQLite 依赖

**Impact**: 事项存储在 WidgetConfig.Settings["events"] JSON 数组中

**Status**: Active

### Decision: 自定义 DarkComboBox 和 DarkDatePicker 控件

**Reason**: WPF 标准 ComboBox/DatePicker 的弹出层无法用样式覆盖暗色主题，白底白字问题无法解决

**Impact**: 创建 Controls/DarkComboBox.xaml 和 DarkDatePicker.xaml，替换所有下拉框和日期选择器

**Status**: Active

### Decision: CMD 启动器独立组件

**Reason**: 用户需要桌面大图标一键启动 CMD 并自动执行命令（如 claude），可自定义图标/名称/命令/起始目录

**Impact**: 新增 CmdLauncherWidget 组件，灰色圆角背景 + 白色图标，右键设置

**Status**: Active

## 2026-08-12

### Decision: 禁用 Alt+F4 关闭组件

**Reason**: 用户不希望误按 Alt+F4 关闭桌面组件，组件应仅通过托盘菜单或右键菜单关闭

**Impact**: BaseWidgetWindow 重写 OnClosing，添加 IsAppClosing（应用退出）和 RequestClose（单组件关闭）两个通道，全部 10 个组件的 Close_Click 改为 RequestClose

**Status**: Active

### Decision: 文档中心组件使用目录结构即数据源

**Reason**: 不引入数据库，根目录路径存 WidgetConfig.Settings["rootPath"]，TreeView 懒加载展示文件夹和文件

**Impact**: 新增 DocWidget + DocSettingsWindow，支持树形浏览、搜索、拖入归档（移动）、新建/重命名/删除

**Status**: Active

### Decision: 锁定按钮统一到左侧

**Reason**: 多数组件锁按钮位置不统一（6 个右侧、4 个左侧），统一到左侧可与右侧的设置按钮分离

**Impact**: Calendar/CmdLauncher/Launcher/QuickTools/Monitor/Clock 6 个组件从右侧改到左侧

**Status**: Active

### Decision: WeatherService 使用 SafeGetString/SafeGetDouble 辅助方法

**Reason**: 30 处裸 GetProperty() 调用在 API 返回格式变化时会抛 KeyNotFoundException，导致天气永远不更新

**Impact**: 添加 SafeGetString/SafeGetDouble 私有静态方法（TryGetProperty + 默认值），替换所有裸 GetProperty

**Status**: Active

### Decision: _allWidgets 静态列表加锁

**Reason**: RegisterWidget/UnregisterWidget 与 CalculateSnap/CalculateResizeSnap 迭代并发，组件拖拽时关闭另一个组件会抛 InvalidOperationException

**Impact**: 添加 _allWidgetsLock 对象，Register/Unregister 加锁，迭代用 ToArray 快照

**Status**: Active

## 2026-08-14

### Decision: 恢复 WM_DROPFILES 直接注册方式

**Reason**: DropOverlayWindow 覆盖窗口方案（返回 HTNOWHERE）导致系统不路由 WM_DROPFILES 消息，拖放功能失效

**Impact**: BaseWidgetWindow 恢复原始 WndProc 处理，在 OnWindowLoaded 中直接注册 WM_DROPFILES（在 SetDesktopLevel 之前）

**Status**: Active

### Decision: WorkerW 句柄有效性检查 + Explorer 重启检测

**Reason**: Windows Explorer 重启后 WorkerW 窗口被重建，缓存的旧句柄未失效（非 IntPtr.Zero），导致 SetParent 失败，组件不可见

**Impact**: 添加 IsWindow API 验证 + TaskbarCreated 消息检测 + 30 秒定时检查，三重保障 WorkerW 句柄有效性

**Status**: Active

### Decision: 移除窗口级别 DropShadowEffect

**Reason**: DropShadowEffect 的 BlurRadius=10 在 AllowsTransparency=true 窗口上导致视觉边界扩展，造成组件位置向右下偏移

**Impact**: 移除 BaseWidgetWindow 的 Effect 属性设置，组件不再有投影效果

**Status**: Active
