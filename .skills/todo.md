# Todo List

## High

- [ ] P1 续：右键菜单结构统一（功能操作 → 分隔线 → 刷新/设置 → 分隔线 → 锁定 → 分隔线 → 关闭组件）
- [ ] P2：视觉统一（引用 App.xaml 配色资源 TertiaryTextBrush/SecondaryTextBrush 等、引用文本样式、标题栏统一 FontSize=12/Foreground=White/Margin=0,0,0,6）
- [ ] 长时间运行稳定性验证（24h+ 内存/GDI 句柄监控）
- [ ] 验证 Win+D 拦截 + z-order 稳定性 + 对话框层级

## Medium

- [ ] P3：代码清理（删除 2 个 Class1.cs、Timer OnClosed 清理）
- [ ] P4：全局搜索（Ctrl+Space 打开，搜索应用/项目/文档，上下键选择 Enter 打开 Esc 关闭）
- [ ] 快捷工具第二阶段：网络诊断工具（Ping/Tracert/Nslookup/Netstat/远程桌面/SSH）
- [ ] 快捷工具第二阶段：数据库快捷入口（服务状态检测/连接测试）
- [ ] 快捷工具第二阶段：全局快捷键（RegisterHotKey API）
- [ ] 日历组件后续：周视图/日视图
- [ ] 日历组件联动：与天气/快捷工具/Git 组件结合
- [ ] 添加更多组件类型（备忘录等）
- [ ] 添加组件导入导出功能
- [ ] 时钟组件右键菜单勾选状态同步
- [ ] 组件透明度设置 UI

## Low

- [ ] 添加多语言支持
- [ ] 暗色/亮色主题切换
- [ ] 吸附对齐辅助线（拖拽时显示对齐参考线）
- [ ] DatePicker 弹出日历完全暗色化（当前只有输入框暗色）
