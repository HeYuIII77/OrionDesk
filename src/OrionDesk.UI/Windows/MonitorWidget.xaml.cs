using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using OrionDesk.BLL.Models;
using OrionDesk.BLL.Services;

namespace OrionDesk.UI.Windows
{
    /// <summary>
    /// 系统监控组件
    /// 显示CPU、内存、磁盘使用率
    /// </summary>
    public partial class MonitorWidget : BaseWidgetWindow
    {
        private readonly DispatcherTimer _timer;
        private readonly SystemMonitorService _monitorService;
        private readonly MonitorSettings _settings;
        private readonly Dictionary<string, System.Windows.Controls.ProgressBar> _diskBars = new Dictionary<string, System.Windows.Controls.ProgressBar>();
        private readonly Dictionary<string, TextBlock> _diskValues = new Dictionary<string, TextBlock>();

        public MonitorWidget(WidgetConfig config, WidgetManager widgetManager)
            : base(config, widgetManager)
        {
            InitializeComponent();

            _monitorService = new SystemMonitorService();
            _settings = LoadSettings(config);

            // 初始化定时器
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(_settings.RefreshInterval)
            };
            _timer.Tick += Timer_Tick;

            // 直接初始化
            LoadLockState();
            UpdateLockButton();
            InitializeDiskDrives();
            UpdateMonitor();
            _timer.Start();
        }

        /// <summary>
        /// 加载监控设置
        /// </summary>
        private MonitorSettings LoadSettings(WidgetConfig config)
        {
            var settings = new MonitorSettings();

            if (config.Settings.TryGetValue("showCpu", out var showCpu))
                settings.ShowCpu = ToBool(showCpu, true);

            if (config.Settings.TryGetValue("showMemory", out var showMemory))
                settings.ShowMemory = ToBool(showMemory, true);

            if (config.Settings.TryGetValue("refreshInterval", out var interval))
                settings.RefreshInterval = ToInt(interval, 3);

            // 加载磁盘列表
            if (config.Settings.TryGetValue("drives", out var drivesObj) && drivesObj is System.Text.Json.JsonElement drivesElement)
            {
                foreach (var drive in drivesElement.EnumerateArray())
                {
                    settings.Drives.Add(drive.GetString() ?? "");
                }
            }

            return settings;
        }

        /// <summary>
        /// 初始化磁盘显示
        /// </summary>
        private void InitializeDiskDrives()
        {
            // 获取所有磁盘
            var drives = _monitorService.GetDriveUsage();

            // 如果配置中有指定磁盘，使用配置的；否则使用所有磁盘
            var drivesToShow = _settings.Drives.Count > 0
                ? drives.FindAll(d => _settings.Drives.Contains(d.Letter))
                : drives;

            foreach (var drive in drivesToShow)
            {
                AddDiskUI(drive.Letter);
            }
        }

        /// <summary>
        /// 添加磁盘UI元素
        /// </summary>
        private void AddDiskUI(string driveLetter)
        {
            var stackPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };

            // 标题行
            var grid = new Grid();
            var title = new TextBlock
            {
                Text = driveLetter,
                Style = (Style)FindResource("TitleStyle")
            };
            var value = new TextBlock
            {
                Text = "0%",
                Style = (Style)FindResource("ValueStyle"),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right
            };
            grid.Children.Add(title);
            grid.Children.Add(value);

            // 进度条
            var progressBar = new System.Windows.Controls.ProgressBar
            {
                Style = (Style)FindResource("MonitorProgressBar"),
                Value = 0,
                Maximum = 100,
                Margin = new Thickness(0, 4, 0, 0),
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(76, 175, 80)) // 绿色
            };

            stackPanel.Children.Add(grid);
            stackPanel.Children.Add(progressBar);

            DiskPanel.Children.Add(stackPanel);

            // 保存引用
            _diskBars[driveLetter] = progressBar;
            _diskValues[driveLetter] = value;
        }

        /// <summary>
        /// 定时器更新
        /// </summary>
        private void Timer_Tick(object? sender, EventArgs e)
        {
            UpdateMonitor();
        }

        /// <summary>
        /// 更新监控数据
        /// </summary>
        private void UpdateMonitor()
        {
            try
            {
                // 更新CPU
                if (_settings.ShowCpu)
                {
                    var cpuUsage = _monitorService.GetCpuUsage();
                    CpuBar.Value = cpuUsage;
                    CpuValue.Text = $"{cpuUsage:F1}%";

                    // 根据使用率变色
                    CpuBar.Foreground = GetUsageColor(cpuUsage);
                }

                // 更新内存（显示使用量/总量 GB）
                if (_settings.ShowMemory)
                {
                    var (memUsed, memTotal, memPercentage) = _monitorService.GetMemoryUsage();
                    MemoryBar.Value = memPercentage;
                    MemoryValue.Text = $"{FormatBytes(memUsed)}/{FormatBytes(memTotal)} ({memPercentage:F1}%)";
                }

                // 更新磁盘（显示使用量/总量 GB）
                var drives = _monitorService.GetDriveUsage();
                foreach (var drive in drives)
                {
                    if (_diskBars.TryGetValue(drive.Letter, out var bar))
                    {
                        bar.Value = drive.Percentage;
                        _diskValues[drive.Letter].Text = $"{FormatBytes(drive.UsedSpace)}/{FormatBytes(drive.TotalSize)} ({drive.Percentage:F1}%)";
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新监控数据失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 格式化字节数为可读字符串
        /// </summary>
        private string FormatBytes(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int i = 0;
            double dblBytes = bytes;

            while (dblBytes >= 1024 && i < suffixes.Length - 1)
            {
                dblBytes /= 1024;
                i++;
            }

            return $"{dblBytes:F1} {suffixes[i]}";
        }

        /// <summary>
        /// 根据使用率获取颜色
        /// </summary>
        private System.Windows.Media.Brush GetUsageColor(float percentage)
        {
            if (percentage < 60)
                return new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 120, 212)); // 蓝色
            if (percentage < 80)
                return new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 185, 0)); // 黄色
            return new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 67, 67));     // 红色
        }

        #region 右键菜单事件

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            UpdateMonitor();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            RequestClose();
        }

        private void Lock_Click(object sender, RoutedEventArgs e)
        {
            ToggleLock();
            UpdateLockButton();
        }

        /// <summary>
        /// 更新锁定按钮显示
        /// </summary>
        private void UpdateLockButton()
        {
            LockButton.Content = IsLocked ? "🔒" : "🔓";
            LockButton.ToolTip = IsLocked ? "解锁" : "锁定";
            LockMenuItem.IsChecked = IsLocked;
            LockMenuItem.Header = IsLocked ? "解锁" : "锁定";
        }

        #endregion

        protected override void OnClosed(EventArgs e)
        {
            _timer?.Stop();
            _monitorService?.Dispose();
            base.OnClosed(e);
        }
    }
}
