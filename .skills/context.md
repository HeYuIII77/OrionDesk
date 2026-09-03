# Current Development Context

**Last updated**: 2026-09-03

## Currently Working On

- 桌面悬浮球功能完善

## Current Module

- DesktopBallWindow（桌面悬浮球）
- BaseWidgetWindow（组件层级控制）

## Completed This Session

### CMD 启动器增强
- ✅ 支持多条命令（每行一条，回车分隔，用 `&&` 连接执行）
- ✅ 命令输入框改为多行（AcceptsReturn + TextWrapping）
- ✅ 设置窗口增加示例提示
- ✅ 移除图标下方命令文字显示

### 桌面悬浮球
- ✅ DesktopBallWindow 独立 Topmost 窗口（不继承 BaseWidgetWindow）
- ✅ 单击拖动 + 双击切换组件层级（置顶/桌面层）
- ✅ 边缘吸附（留40%在外面）
- ✅ 悬停效果（缩放1.15x + 透明度 + 边框变色）
- ✅ 位置持久化（AppSettings DesktopBallX/Y）
- ✅ 启动时自动恢复桌面球
- ✅ 置顶时组件背景变不透明（只改 alpha，保留原色）
- ✅ 置顶时 WM_ACTIVATE 不推回底层（KeepTopmost 标志）
- ✅ 点击其他应用自动退出置顶模式（Application.Deactivated）

### 组件层级控制
- ✅ BaseWidgetWindow 新增 SetTopmost(bool) 公开方法
- ✅ KeepTopmost 标志控制 WM_ACTIVATE 行为
- ✅ _originalBackground 保存/恢复原始背景画刷

### 移除的功能
- ✅ 删除 ShowDesktopWidget（被桌面球替代）
- ✅ 删除文档中心组件（DocWidget，之前已移除）

### 其他
- ✅ 修复 MainWindow 启动时白色窗口闪现（添加 Hide()）
- ✅ README 更新（新增桌面球、CMD 多命令、移除文档中心）
- ✅ 发布到 publish 目录

## Not Yet Complete

- P1 续：右键菜单结构统一
- P2：视觉统一
- P3：代码清理

## Blockers

- None

## Next Steps

- 验证桌面球在多显示器环境下的行为
- 继续 P1/P2/P3 优化
