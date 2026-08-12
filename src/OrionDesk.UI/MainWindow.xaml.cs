using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Threading;
using OrionDesk.BLL.Models;
using OrionDesk.BLL.Services;
using OrionDesk.UI.Windows;

// 天气服务（全局共享，带缓存）

namespace OrionDesk.UI
{
    /// <summary>
    /// 主窗口 - 负责托盘图标和组件生命周期管理
    /// </summary>
    public partial class MainWindow : Window
    {
        private NotifyIcon? _trayIcon;
        private ToolStripMenuItem? _startupItem;
        private readonly WidgetManager _widgetManager;
        private readonly WeatherService _weatherService;
        private readonly DiagnosticsService _diagnosticsService;
        private readonly List<BaseWidgetWindow> _activeWidgets = new List<BaseWidgetWindow>();
        private DiagnosticsWindow? _diagnosticsWindow;
        private bool _isExiting = false;
        private bool _isClosingAll = false;

        public MainWindow()
        {
            InitializeComponent();

            _widgetManager = new WidgetManager();
            _weatherService = new WeatherService();
            _diagnosticsService = new DiagnosticsService(intervalMinutes: 5);

            // 初始化托盘图标
            InitializeTrayIcon();

            // 关闭时保存配置
            Closing += (s, e) =>
            {
                // 无论用户退出还是系统关机，都标记为批量关闭
                // 防止组件的 Closed 事件删除配置
                _isClosingAll = true;

                // 保存当前完整配置
                _widgetManager.Save();

                if (!_isExiting)
                {
                    // 非用户主动退出（系统关机等），隐藏窗口
                    e.Cancel = true;
                    Hide();
                }
                else
                {
                    // 用户主动退出，关闭所有组件
                    BaseWidgetWindow.IsAppClosing = true;
                    foreach (var widget in _activeWidgets.ToList())
                        widget.RequestClose();

                    if (_trayIcon != null)
                    {
                        _trayIcon.Visible = false;
                        _trayIcon.Dispose();
                    }
                }
            };


            // 同步加载配置并恢复组件
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    LoadAndRestore();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[启动] 严重异常: {ex}");
                    ShowTrayMessage("组件恢复失败，请检查配置文件");
                }
            }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }

        /// <summary>
        /// 加载配置并恢复组件（同步，防止关机时配置不完整）
        /// </summary>
        private void LoadAndRestore()
        {
            try
            {
                _widgetManager.Load();
                Log($"[启动] 加载完成，组件数={_widgetManager.Settings.Widgets.Count}");

                // 配置加载完成后，同步 UI 状态和注册表
                _startupItem.Checked = _widgetManager.Settings.StartWithWindows;
                SetStartup(_widgetManager.Settings.StartWithWindows);

                // 恢复期间禁止保存，防止组件初始化触发的 SavePosition 覆盖配置
                _widgetManager.IsRestoring = true;
                try
                {
                    RestoreWidgets();
                    Log($"[启动] 恢复完成，活动组件数={_activeWidgets.Count}，配置组件数={_widgetManager.Settings.Widgets.Count}");
                }
                finally
                {
                    _widgetManager.IsRestoring = false;
                    // 恢复完成后统一保存一次（确保配置完整）
                    _widgetManager.Save();
                }

                // 启动诊断服务（组件恢复完成后）
                _diagnosticsService.Start();
                Log("[启动] 诊断服务已启动");
            }
            catch (Exception ex)
            {
                _widgetManager.IsRestoring = false;
                System.Diagnostics.Debug.WriteLine($"[启动] 加载异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 初始化系统托盘图标
        /// </summary>
        private void InitializeTrayIcon()
        {
            _trayIcon = new NotifyIcon
            {
                Icon = GetAppIcon(),
                Visible = true,
                Text = "OrionDesk - 桌面小组件"
            };

            // 创建右键菜单
            var contextMenu = new ContextMenuStrip();

            // 添加组件子菜单
            var addMenu = new ToolStripMenuItem("添加组件");
            addMenu.DropDownItems.Add("时钟", null, (s, e) => AddWidget("clock"));
            addMenu.DropDownItems.Add("系统监控", null, (s, e) => AddWidget("monitor"));
            addMenu.DropDownItems.Add("启动器", null, (s, e) => AddWidget("launcher"));
            addMenu.DropDownItems.Add("便签", null, (s, e) => AddWidget("note"));
            addMenu.DropDownItems.Add("文件夹映射", null, (s, e) => AddWidget("folder"));
            addMenu.DropDownItems.Add("Git 同步监控", null, (s, e) => AddWidget("gitsync"));
            addMenu.DropDownItems.Add("快捷工具", null, (s, e) => AddWidget("quicktools"));
            addMenu.DropDownItems.Add("日历事项", null, (s, e) => AddWidget("calendar"));
            addMenu.DropDownItems.Add("CMD 启动器", null, (s, e) => AddWidget("cmdlauncher"));
            addMenu.DropDownItems.Add("文档中心", null, (s, e) => AddWidget("doc"));
            contextMenu.Items.Add(addMenu);

            contextMenu.Items.Add(new ToolStripSeparator());

            // 显示/隐藏所有组件
            contextMenu.Items.Add("显示所有组件", null, (s, e) => ShowAllWidgets());
            contextMenu.Items.Add("隐藏所有组件", null, (s, e) => HideAllWidgets());

            contextMenu.Items.Add(new ToolStripSeparator());

            // 设置
            contextMenu.Items.Add("设置", null, (s, e) => OpenSettings());

            // 诊断
            contextMenu.Items.Add("诊断", null, (s, e) => OpenDiagnostics());

            contextMenu.Items.Add(new ToolStripSeparator());

            // 开机启动
            _startupItem = new ToolStripMenuItem("开机启动");
            _startupItem.Click += (s, e) =>
            {
                _widgetManager.Settings.StartWithWindows = !_widgetManager.Settings.StartWithWindows;
                _startupItem.Checked = _widgetManager.Settings.StartWithWindows;
                SetStartup(_widgetManager.Settings.StartWithWindows);
                _widgetManager.Save();
            };
            _startupItem.Checked = _widgetManager.Settings.StartWithWindows;
            contextMenu.Items.Add(_startupItem);

            contextMenu.Items.Add(new ToolStripSeparator());

            // 退出
            contextMenu.Items.Add("退出", null, (s, e) =>
            {
                _isExiting = true;
                System.Windows.Application.Current.Shutdown();
            });

            _trayIcon.ContextMenuStrip = contextMenu;

            // 双击托盘图标显示/隐藏所有组件
            _trayIcon.DoubleClick += (s, e) =>
            {
                if (_widgetManager.Settings.ShowAllWidgets)
                    HideAllWidgets();
                else
                    ShowAllWidgets();
            };
        }

        /// <summary>
        /// 获取应用图标
        /// </summary>
        private System.Drawing.Icon GetAppIcon()
        {
            try
            {
                // 从当前 exe 中提取图标（csproj 中 ApplicationIcon 嵌入的）
                var exePath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                {
                    return System.Drawing.Icon.ExtractAssociatedIcon(exePath) ?? System.Drawing.SystemIcons.Application;
                }
            }
            catch
            {
                // 提取失败，回退到系统图标
            }

            return System.Drawing.SystemIcons.Application;
        }

        /// <summary>
        /// 添加组件
        /// </summary>
        private void AddWidget(string type)
        {
            // 各组件默认尺寸
            var (width, height) = type switch
            {
                "clock" => (220, 120),
                "monitor" => (260, 200),
                "launcher" => (300, 120),
                "note" => (250, 200),
                "folder" => (380, 350),
                "gitsync" => (280, 300),
                "quicktools" => (240, 320),
                "calendar" => (280, 360),
                "cmdlauncher" => (160, 160),
                "doc" => (300, 400),
                _ => (200, 100)
            };

            // 在屏幕中央偏左上位置创建
            var screen = Screen.PrimaryScreen?.WorkingArea ?? new System.Drawing.Rectangle(0, 0, 1920, 1080);
            var x = screen.Width / 2 - width / 2;
            var y = screen.Height / 2 - height / 2;

            var config = _widgetManager.AddWidget(type, x, y, width, height);
            CreateWidgetWindow(config);

            // 同步保存，确保配置落盘
            _widgetManager.Save();
        }

        /// <summary>
        /// 创建组件窗口
        /// </summary>
        private BaseWidgetWindow? CreateWidgetWindow(WidgetConfig config)
        {
            BaseWidgetWindow? widget = config.Type switch
            {
                "clock" => new ClockWidget(config, _widgetManager, _weatherService),
                "monitor" => new MonitorWidget(config, _widgetManager),
                "launcher" => new LauncherWidget(config, _widgetManager),
                "note" => new StickyNoteWidget(config, _widgetManager),
                "folder" => new FolderWidget(config, _widgetManager),
                "gitsync" => new GitSyncWidget(config, _widgetManager),
                "quicktools" => new QuickToolsWidget(config, _widgetManager),
                "calendar" => new CalendarWidget(config, _widgetManager),
                "cmdlauncher" => new CmdLauncherWidget(config, _widgetManager),
                "doc" => new DocWidget(config, _widgetManager),
                _ => null
            };

            if (widget != null)
            {
                _activeWidgets.Add(widget);
                BaseWidgetWindow.RegisterWidget(widget);
                widget.Closed += (s, e) =>
                {
                    _activeWidgets.Remove(widget);
                    BaseWidgetWindow.UnregisterWidget(widget);
                    Log($"[Closed] {config.Type} id={config.Id[..8]} isClosingAll={_isClosingAll}");
                    // 只有用户手动关闭单个组件时才删除配置
                    // 程序退出时批量关闭不应删除，否则下次启动无法恢复
                    if (!_isClosingAll)
                    {
                        _widgetManager.RemoveWidget(config.Id);
                        Log($"[Closed] 已从配置移除，剩余组件数={_widgetManager.Settings.Widgets.Count}");
                        _widgetManager.Save();
                    }
                };
                widget.Show();
            }

            return widget;
        }

        /// <summary>
        /// 恢复之前保存的组件
        /// </summary>
        private void RestoreWidgets()
        {
            var widgets = _widgetManager.GetAllWidgets();
            System.Diagnostics.Debug.WriteLine($"[恢复] 共 {widgets.Count} 个组件");
            foreach (var config in widgets)
            {
                try
                {
                    var widget = CreateWidgetWindow(config);
                    System.Diagnostics.Debug.WriteLine($"[恢复] {config.Type} id={config.Id[..8]} {(widget != null ? "成功" : "跳过")}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[恢复] {config.Type} 失败: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 显示所有组件
        /// </summary>
        private void ShowAllWidgets()
        {
            _widgetManager.Settings.ShowAllWidgets = true;
            foreach (var widget in _activeWidgets)
            {
                widget.SetVisible(true);
            }
            _widgetManager.Save();
        }

        /// <summary>
        /// 隐藏所有组件
        /// </summary>
        private void HideAllWidgets()
        {
            _widgetManager.Settings.ShowAllWidgets = false;
            foreach (var widget in _activeWidgets)
            {
                widget.SetVisible(false);
            }
            _widgetManager.Save();
        }

        /// <summary>
        /// 打开诊断窗口
        /// </summary>
        private void OpenDiagnostics()
        {
            if (_diagnosticsWindow == null)
            {
                _diagnosticsWindow = new DiagnosticsWindow(_diagnosticsService);
                _diagnosticsWindow.Closed += (s, e) => _diagnosticsWindow = null;
                _diagnosticsWindow.Show();
            }
            else
            {
                _diagnosticsWindow.Activate();
            }
        }

        /// <summary>
        /// 打开设置窗口
        /// </summary>
        private void OpenSettings()
        {
            var settingsWindow = new SettingsWindow(
                _widgetManager.Settings.Weather,
                _weatherService,
                _widgetManager.Settings.GitSyncRefreshMinutes);
            settingsWindow.Owner = null;
            settingsWindow.ShowDialog();

            // 保存配置
            try
            {
                _widgetManager.Settings.GitSyncRefreshMinutes = settingsWindow.GitSyncRefreshMinutes;
                _widgetManager.Save();
                // 清除天气缓存，立刻刷新所有时钟组件的天气
                _weatherService.ClearCache();
                foreach (var widget in _activeWidgets)
                {
                    if (widget is ClockWidget clock)
                        clock.RefreshWeather();
                }
                ShowTrayMessage("设置已保存");
            }
            catch (Exception ex)
            {
                ShowTrayMessage("保存失败: " + ex.Message);
            }
        }

        /// <summary>
        /// 设置开机启动
        /// </summary>
        private void SetStartup(bool enable)
        {
            try
            {
                var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);

                if (key != null)
                {
                    if (enable)
                    {
                        // Environment.ProcessPath 在 .NET 6+ 可靠获取 EXE 路径
                        var exePath = Environment.ProcessPath ?? "";
                        if (string.IsNullOrEmpty(exePath))
                        {
                            System.Diagnostics.Debug.WriteLine("[开机启动] 无法获取 EXE 路径");
                            return;
                        }
                        key.SetValue("OrionDesk", $"\"{exePath}\"");
                        System.Diagnostics.Debug.WriteLine($"[开机启动] 已设置: {exePath}");
                    }
                    else
                    {
                        key.DeleteValue("OrionDesk", false);
                        System.Diagnostics.Debug.WriteLine("[开机启动] 已移除");
                    }
                    key.Close();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"设置开机启动失败: {ex.Message}");
                ShowTrayMessage("设置开机启动失败");
            }
        }

        /// <summary>
        /// 显示托盘消息
        /// </summary>
        private void ShowTrayMessage(string message)
        {
            _trayIcon?.ShowBalloonTip(3000, "OrionDesk", message, ToolTipIcon.Info);
        }


        protected override void OnClosed(EventArgs e)
        {
            _diagnosticsService?.Dispose();
            _weatherService?.Dispose();
            _trayIcon?.Dispose();
            base.OnClosed(e);
        }

        private static void Log(string msg)
        {
            try
            {
                var logDir = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "OrionDesk");
                System.IO.Directory.CreateDirectory(logDir);
                System.IO.File.AppendAllText(
                    System.IO.Path.Combine(logDir, "startup.log"),
                    $"{DateTime.Now:HH:mm:ss.fff} {msg}\n");
            }
            catch { }
        }
    }
}
