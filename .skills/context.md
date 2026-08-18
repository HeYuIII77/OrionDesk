# Current Development Context

**Last updated**: 2026-08-14

## Currently Working On

- 稳定性修复（组件消失、位置偏移、拖放失效）

## Current Module

- BaseWidgetWindow（组件窗口基类）

## Completed This Session

### 拖放功能修复
- ✅ 恢复 WM_DROPFILES 直接注册方式（在 SetDesktopLevel 之前注册）
- ✅ 移除 DropOverlayWindow 集成（该方案返回 HTNOWHERE 导致系统不路由 WM_DROPFILES）
- ✅ BaseWidgetWindow 恢复原始 WndProc 处理 WM_DROPFILES 消息
- **Root Cause**: commit ba22b13 将拖放方式从直接注册改为 DropOverlayWindow，但 HTNOWHERE 使系统不路由消息

### 组件消失问题修复
- ✅ WorkerW 句柄有效性检查（IsWindow API 验证）
- ✅ Explorer 重启检测（RegisterWindowMessage "TaskbarCreated"）
- ✅ 定时检查机制（每 30 秒验证 WorkerW 句柄）
- ✅ Explorer 重启后自动刷新桌面层级
- **Root Cause**: Explorer 重启后 WorkerW 窗口被重建，缓存的旧句柄未失效

### 组件位置偏移修复
- ✅ 移除 BaseWidgetWindow 窗口级别的 DropShadowEffect
- **Root Cause**: DropShadowEffect 的 BlurRadius=10 在 AllowsTransparency=true 窗口上导致视觉边界扩展

### 发布
- ✅ dotnet publish 生成 72MB 自包含单文件 exe → publish/OrionDesk.UI.exe

## Not Yet Complete

- P1 续：右键菜单结构统一
- P2：视觉统一
- P3：代码清理

## Blockers

- None

## Next Steps

- 验证修复效果（拖放、组件消失、位置偏移）
- 继续 P1/P2/P3 优化
