using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using OrionDesk.BLL.Services;

using Media = System.Windows.Media;

namespace OrionDesk.UI.Windows
{
    /// <summary>
    /// 诊断监控窗口 - 展示进程级指标（内存/GDI/USER/线程/句柄/GC）
    /// </summary>
    public partial class DiagnosticsWindow : Window
    {
        private readonly DiagnosticsService _diagnostics;

        public DiagnosticsWindow(DiagnosticsService diagnostics)
        {
            InitializeComponent();
            _diagnostics = diagnostics;

            // 订阅快照事件
            _diagnostics.OnSnapshot += OnNewSnapshot;

            // 加载已有历史数据
            Loaded += (s, e) =>
            {
                RefreshCurrentSnapshot();
                RefreshHistoryList();
            };

            // 关闭时取消订阅
            Closed += (s, e) => _diagnostics.OnSnapshot -= OnNewSnapshot;
        }

        /// <summary>
        /// 新快照到达时刷新 UI
        /// </summary>
        private void OnNewSnapshot(DiagnosticsService.DiagnosticsSnapshot snapshot)
        {
            Dispatcher.BeginInvoke(() =>
            {
                RefreshCurrentSnapshot();
                AddHistoryRow(snapshot);
                HistoryCountText.Text = $"共 {_diagnostics.History.Count} 条";
            });
        }

        /// <summary>
        /// 刷新当前快照面板
        /// </summary>
        private void RefreshCurrentSnapshot()
        {
            var s = _diagnostics.LatestSnapshot;
            if (s == null)
            {
                TimestampText.Text = "暂无数据";
                return;
            }

            TimestampText.Text = s.Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
            WorkingSetText.Text = $"{s.WorkingSetMB} MB";
            PrivateMemoryText.Text = $"{s.PrivateMemoryMB} MB";
            ManagedHeapText.Text = $"{s.ManagedHeapMB} MB";
            GdiHandlesText.Text = s.GdiHandles.ToString();
            UserHandlesText.Text = s.UserHandles.ToString();
            HandleCountText.Text = s.HandleCount.ToString();
            ThreadCountText.Text = s.ThreadCount.ToString();
            Gen0Text.Text = s.Gen0Collections.ToString();
            Gen1Text.Text = s.Gen1Collections.ToString();
            Gen2Text.Text = s.Gen2Collections.ToString();

            // 根据 GDI 句柄数量高亮警告
            GdiHandlesText.Foreground = s.GdiHandles > 500
                ? new SolidColorBrush(Media.Color.FromRgb(0xFF, 0x6B, 0x6B))
                : Media.Brushes.White;
            UserHandlesText.Foreground = s.UserHandles > 300
                ? new SolidColorBrush(Media.Color.FromRgb(0xFF, 0x6B, 0x6B))
                : Media.Brushes.White;
        }

        /// <summary>
        /// 刷新完整历史列表
        /// </summary>
        private void RefreshHistoryList()
        {
            HistoryRows.Children.Clear();
            foreach (var s in _diagnostics.History)
                AddHistoryRow(s);
            HistoryCountText.Text = $"共 {_diagnostics.History.Count} 条";
        }

        /// <summary>
        /// 添加一行历史数据
        /// </summary>
        private void AddHistoryRow(DiagnosticsService.DiagnosticsSnapshot s)
        {
            var row = new Grid { Margin = new Thickness(0, 0, 0, 2) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });

            // 交替行背景
            if (HistoryRows.Children.Count % 2 == 0)
                row.Background = new SolidColorBrush(Media.Color.FromArgb(0x0A, 0xFF, 0xFF, 0xFF));

            var fontSize = 11.0;
            var foreground = new SolidColorBrush(Media.Color.FromRgb(0xCC, 0xCC, 0xCC));

            // GDI 超过 500 时标红
            var gdiForeground = s.GdiHandles > 500
                ? new SolidColorBrush(Media.Color.FromRgb(0xFF, 0x6B, 0x6B))
                : foreground;
            var userForeground = s.UserHandles > 300
                ? new SolidColorBrush(Media.Color.FromRgb(0xFF, 0x6B, 0x6B))
                : foreground;

            AddCell(row, 0, s.Timestamp.ToString("HH:mm"), fontSize, foreground);
            AddCell(row, 1, s.WorkingSetMB.ToString(), fontSize, foreground);
            AddCell(row, 2, s.ManagedHeapMB.ToString(), fontSize, foreground);
            AddCell(row, 3, s.GdiHandles.ToString(), fontSize, gdiForeground);
            AddCell(row, 4, s.UserHandles.ToString(), fontSize, userForeground);
            AddCell(row, 5, s.HandleCount.ToString(), fontSize, foreground);
            AddCell(row, 6, s.ThreadCount.ToString(), fontSize, foreground);

            HistoryRows.Children.Add(row);
        }

        /// <summary>
        /// 在指定列添加文本单元格
        /// </summary>
        private static void AddCell(Grid row, int column, string text, double fontSize, Media.Brush foreground)
        {
            var tb = new TextBlock
            {
                Text = text,
                FontSize = fontSize,
                Foreground = foreground
            };
            Grid.SetColumn(tb, column);
            row.Children.Add(tb);
        }

        #region 按钮事件

        /// <summary>
        /// 手动采集一次
        /// </summary>
        private void TakeSnapshot_Click(object sender, RoutedEventArgs e)
        {
            _diagnostics.TakeSnapshot();
        }

        /// <summary>
        /// 打开日志目录
        /// </summary>
        private void OpenLogDir_Click(object sender, RoutedEventArgs e)
        {
            var logDir = _diagnostics.GetLogDirectory();
            try
            {
                DAL.DataPath.EnsureDirectoriesExist();
                Process.Start("explorer.exe", logDir);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"无法打开日志目录:\n{logDir}\n\n{ex.Message}",
                    "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion
    }
}
