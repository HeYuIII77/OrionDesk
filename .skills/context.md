# Current Development Context

**Last updated**: 2026-08-07

## Currently Working On

- OrionDesk 桌面小组件工具 — 多个新组件和功能已完成

## Current Module

- 无正在进行的模块

## Completed This Session

### 诊断监控
- ✅ DiagnosticsService 诊断服务（进程级指标采集：内存/GDI/USER/线程/句柄/GC）
- ✅ DiagnosticsWindow 诊断窗口（实时快照 + 历史趋势 + CSV 日志）
- ✅ Win32 API GetGuiResources 采集 GDI/USER 对象数
- ✅ MainWindow 托盘菜单集成"诊断"入口

### 快捷工具组件
- ✅ QuickToolItem 模型 + QuickToolType 枚举（App/Folder/Url/Shell）
- ✅ QuickToolsWidget 图标网格组件（64×64 按钮、WrapPanel 自动换行）
- ✅ 16 个预置工具（系统 12 + 开发 4），可自由编辑/删除/新增
- ✅ 管理员权限启动（Verb="runas" + UAC 取消静默处理）
- ✅ QuickToolEditWindow 编辑对话框（名称/图标/类型/路径/参数/管理员）
- ✅ 修复 NullReferenceException（UpdateTypeVisibility 控件未就绪）
- ✅ 移除 IsPreset 限制，所有工具可编辑删除

### SVN 支持
- ✅ VcsType 枚举（None/Git/Svn）
- ✅ SVN 仓库发现（.svn 目录检测）
- ✅ RunSvnAsync（svn CLI 调用、UTF-8 + 超时 + Kill）
- ✅ CheckSvnRepoAsync（svn info + svn status → 状态映射）
- ✅ GitSyncWidget 显示 [Git]/[SVN] 标签、拖拽支持 .svn

### 日历事项组件
- ✅ CalendarEvent 模型（Title/Start/End/IsAllDay/Type/Repeat/Note）
- ✅ CalendarWidget 月视图（6×7 网格、今日高亮、事项颜色标记）
- ✅ 重复事项（不重复/每天/每周/每月/每年）
- ✅ 倒计时区域（组件底部，最近 5 个倒计时/正计时）
- ✅ EventEditWindow 编辑对话框
- ✅ EventListWindow 事项列表弹窗（编辑/删除/新增）

### CMD 启动器组件
- ✅ CmdLauncherWidget 大图标组件（灰色背景 + 白色图标）
- ✅ 点击启动 CMD 并自动执行命令
- ✅ CmdLauncherSettingsWindow（设置名称/图标/命令/起始目录）
- ✅ 右键 → 设置，可自定义图标 emoji

### 自定义暗色控件
- ✅ DarkComboBox 自定义暗色下拉框（Popup + ListBox，替代标准 ComboBox）
- ✅ DarkDatePicker 自定义暗色日期选择器（TextBox + Calendar 弹出层）
- ✅ InputDialog 通用输入对话框

### UI 修复
- ✅ 文件夹映射子目录缩进（ItemsPresenter Margin=16,0,0,0）
- ✅ 快捷工具锁图标切换修复（添加 UpdateLockButton 调用）
- ✅ 日历编辑对话框布局优化（DatePicker/ComboBox 暗色化）
- ✅ 时间输入框宽度调整（48px → 60px）
- ✅ CMD 设置窗口标题、滚动条、图标颜色修复

### 其他
- ✅ 调整大小吸附（右/下边缘自动对齐其他组件）
- ✅ 发布便携版（72MB 自包含单文件）
- ✅ README.md 全面更新

## Not Yet Complete

- 长时间运行稳定性验证（24h+ 内存/GDI 句柄监控）

## Blockers

- 无

## Next Steps

- 长时间运行监控（内存、GDI 句柄实际测试）
- 快捷工具第二阶段：网络诊断工具、数据库入口、全局快捷键
- 日历组件后续：周视图/日视图、与天气/快捷工具/Git 联动
