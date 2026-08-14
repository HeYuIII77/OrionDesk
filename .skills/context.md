# Current Development Context

**Last updated**: 2026-08-14

## Currently Working On

- 组件功能调整 + 拖放限制处理

## Current Module

- 全局：组件整合 + 拖放方案

## Completed This Session

### 文档中心移除
- ✅ 删除 DocWidget.xaml / DocWidget.xaml.cs
- ✅ MainWindow.xaml.cs 移除文档中心 3 处引用（托盘菜单、默认尺寸、工厂分支）
- ✅ 保留 DocSettingsWindow（FolderWidget 在用）

### 文件夹映射添加搜索
- ✅ XAML 添加搜索框（TextBox + placeholder + SearchBoxStyle），Grid 改为 3 行
- ✅ 搜索逻辑：SearchBox_TextChanged 入口、SearchDirectory 递归匹配文件名（含相对路径显示）
- ✅ _isSearching 标志：搜索中禁用刷新，双击搜索结果直接打开文件

### 拖放方案探索（未成功）
- ❌ WM_DROPFILES 直接调用 — WorkerW 子窗口不在前台窗口层级，系统不路由消息
- ❌ DropOverlayWindow 覆盖窗口 — WS_EX_TRANSPARENT 导致拖放命中测试也穿透；去掉后覆盖窗口阻挡桌面交互
- ⚠ 结论：**WorkerW 子窗口无法接收外部应用拖放**，这是 Windows 架构限制
- ✅ 启动器需要改为右键菜单或浏览按钮添加应用

### 时钟组件简化
- ✅ 移除模拟时钟（XAML Canvas + code-behind 6 个方法 + ClockSettings.Style）

### 文件夹映射设置
- ✅ 右键菜单添加"设置"入口，复用 DocSettingsWindow

## Not Yet Complete

- 启动器添加应用方式改造（右键菜单/浏览按钮替代拖放）
- P1 续：右键菜单结构统一
- P2：视觉统一
- P3：代码清理

## Blockers

- 拖放功能受 Windows 架构限制，WorkerW 子窗口无法接收外部拖放

## Next Steps

- 启动器添加"添加应用"右键菜单或浏览按钮
- 清理 DropOverlayWindow / AcceptFileDrop 相关代码（如确认不再需要）
- 继续 P1/P2/P3
