# Current Development Context

**Last updated**: 2026-08-25

## Currently Working On

- 桌面层级稳定性（Win+D、z-order、WorkerW 查找）

## Current Module

- BaseWidgetWindow（组件窗口基类）

## Completed This Session

### Win+D "显示桌面" 拦截
- ✅ WndProc 拦截 WM_WINDOWPOSCHANGING，检测 SWP_HIDEWINDOW 时立即取消隐藏
- ✅ 组件不会因 Win+D 消失

### FindDesktopWindows 多层回退
- ✅ 方法1：EnumWindows 枚举（原始逻辑）
- ✅ 方法2：从 Progman 遍历兄弟窗口找 WorkerW
- ✅ 方法3：FindWindow("WorkerW", null) 直接查找
- ✅ 方法4：回退到 Progman（至少能用）
- ✅ 解决 "WorkerW未找到" 导致组件不显示的问题

### 组件层级锁定
- ✅ WM_ACTIVATE 拦截：点击组件后立刻 SetWindowPos(HWND_BOTTOM) 推回底层
- ✅ 组件始终在应用程序下面，点击不会提升层级
- ✅ 不使用 WS_EX_NOACTIVATE（会阻止子控件接收事件）

### 对话框窗口 Topmost
- ✅ 所有 9 个对话框窗口设置 Topmost = true
- ✅ EventListWindow、EventEditWindow、WeatherDetailWindow、CmdLauncherSettingsWindow、DocSettingsWindow、QuickToolEditWindow、InputDialog、SettingsWindow、DiagnosticsWindow

### 配置清除
- ✅ 用户要求清除 config.json，重新开始

## Not Yet Complete

- P1 续：右键菜单结构统一
- P2：视觉统一
- P3：代码清理

## Blockers

- None

## Next Steps

- 验证 Win+D 拦截、z-order 稳定性、对话框层级
- 继续 P1/P2/P3 优化
