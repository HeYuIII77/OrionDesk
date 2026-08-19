using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using OrionDesk.BLL.Models;
using OrionDesk.BLL.Services;

using Button = System.Windows.Controls.Button;
using StackPanel = System.Windows.Controls.StackPanel;
using TextBlock = System.Windows.Controls.TextBlock;
using ComboBoxItem = System.Windows.Controls.ComboBoxItem;
using Orientation = System.Windows.Controls.Orientation;
using Border = System.Windows.Controls.Border;
using Color = System.Windows.Media.Color;
using Colors = System.Windows.Media.Colors;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;
using Brushes = System.Windows.Media.Brushes;

namespace OrionDesk.UI.Windows
{
    /// <summary>
    /// 日历事项组件 - 月视图日历 + 事项管理 + 倒计时
    /// </summary>
    public partial class CalendarWidget : BaseWidgetWindow
    {
        #region 字段

        private List<CalendarEvent> _events = new();
        private DateTime _currentMonth = DateTime.Today;
        private readonly Button[,] _dayButtons = new Button[6, 7];
        private readonly TextBlock[,] _dayTexts = new TextBlock[6, 7];
        private readonly StackPanel[,] _dayDots = new StackPanel[6, 7];
        private DispatcherTimer? _countdownTimer;

        #endregion

        #region 构造函数

        public CalendarWidget(WidgetConfig config, WidgetManager widgetManager)
            : base(config, widgetManager)
        {
            InitializeComponent();
            InitCalendarGrid();
            LoadSettings();
            LoadLockState();
            RenderCalendar();
            RenderCountdowns();

            // 每分钟刷新倒计时
            _countdownTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMinutes(1)
            };
            _countdownTimer.Tick += (s, e) => RenderCountdowns();
            _countdownTimer.Start();
        }

        #endregion

        #region 初始化

        /// <summary>
        /// 初始化 6×7 日期格子
        /// </summary>
        private void InitCalendarGrid()
        {
            for (int row = 0; row < 6; row++)
            {
                for (int col = 0; col < 7; col++)
                {
                    var cellPanel = new StackPanel
                    {
                        VerticalAlignment = System.Windows.VerticalAlignment.Stretch,
                        HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch
                    };

                    // 日期数字
                    var dayText = new TextBlock
                    {
                        FontSize = 11,
                        HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                        Margin = new Thickness(0, 0, 2, 0)
                    };
                    cellPanel.Children.Add(dayText);
                    _dayTexts[row, col] = dayText;

                    // 事项颜色点
                    var dotsPanel = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                        Margin = new Thickness(0, 2, 0, 0)
                    };
                    cellPanel.Children.Add(dotsPanel);
                    _dayDots[row, col] = dotsPanel;

                    // 按钮
                    var button = new Button
                    {
                        Style = (Style)FindResource("DayButtonStyle"),
                        Content = cellPanel,
                        Tag = new DateTime() // 占位，渲染时赋值
                    };
                    button.Click += DayButton_Click;

                    System.Windows.Controls.Grid.SetRow(button, row);
                    System.Windows.Controls.Grid.SetColumn(button, col);
                    CalendarGrid.Children.Add(button);
                    _dayButtons[row, col] = button;
                }
            }
        }

        #endregion

        #region 配置读写

        private void LoadSettings()
        {
            _events.Clear();

            if (_config.Settings.TryGetValue("events", out var raw) && raw is JsonElement je
                && je.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in je.EnumerateArray())
                {
                    var evt = new CalendarEvent();
                    if (item.TryGetProperty("id", out var id)) evt.Id = id.GetString() ?? evt.Id;
                    if (item.TryGetProperty("title", out var t)) evt.Title = t.GetString() ?? "";
                    if (item.TryGetProperty("start", out var s) && DateTime.TryParse(s.GetString(), out var start))
                        evt.Start = start;
                    if (item.TryGetProperty("end", out var e) && e.ValueKind != JsonValueKind.Null
                        && DateTime.TryParse(e.GetString(), out var end))
                        evt.End = end;
                    if (item.TryGetProperty("isAllDay", out var ad)) evt.IsAllDay = ad.ValueKind == JsonValueKind.True;
                    if (item.TryGetProperty("type", out var tp) && Enum.TryParse<EventType>(tp.GetString(), out var eventType))
                        evt.Type = eventType;
                    if (item.TryGetProperty("repeat", out var rp) && Enum.TryParse<EventRepeat>(rp.GetString(), out var repeat))
                        evt.Repeat = repeat;
                    if (item.TryGetProperty("note", out var n)) evt.Note = n.GetString() ?? "";
                    _events.Add(evt);
                }
            }
        }

        private void SaveSettings()
        {
            if (_widgetManager.IsRestoring) return;

            _config.Settings["events"] = _events.Select(evt => new Dictionary<string, object>
            {
                ["id"] = evt.Id,
                ["title"] = evt.Title,
                ["start"] = evt.Start.ToString("yyyy-MM-ddTHH:mm:ss"),
                ["end"] = evt.End?.ToString("yyyy-MM-ddTHH:mm:ss") ?? "",
                ["isAllDay"] = evt.IsAllDay,
                ["type"] = evt.Type.ToString(),
                ["repeat"] = evt.Repeat.ToString(),
                ["note"] = evt.Note
            }).ToArray();

            _widgetManager.Save();
        }

        private void LoadLockState()
        {
            if (_config.Settings.TryGetValue("isLocked", out var val))
                IsLocked = ToBool(val);
            UpdateLockButton();
        }

        #endregion

        #region 日历渲染

        /// <summary>
        /// 渲染月视图
        /// </summary>
        private void RenderCalendar()
        {
            var year = _currentMonth.Year;
            var month = _currentMonth.Month;
            MonthTitle.Text = $"{year}年{month}月";

            var firstDay = new DateTime(year, month, 1);
            var startDayOfWeek = (int)firstDay.DayOfWeek; // 0=周日
            var startDate = firstDay.AddDays(-startDayOfWeek);
            var today = DateTime.Today;

            for (int row = 0; row < 6; row++)
            {
                for (int col = 0; col < 7; col++)
                {
                    var date = startDate.AddDays(row * 7 + col);
                    var isCurrentMonth = date.Month == month;
                    var isToday = date == today;

                    // 日期数字
                    _dayTexts[row, col].Text = date.Day.ToString();
                    _dayTexts[row, col].Foreground = isToday
                        ? new SolidColorBrush(Colors.White)
                        : isCurrentMonth
                            ? new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC))
                            : new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55));

                    // 今日高亮背景
                    _dayButtons[row, col].Background = isToday
                        ? new SolidColorBrush(Color.FromArgb(0x33, 0x00, 0x78, 0xD4))
                        : Brushes.Transparent;

                    // 存储日期到 Tag
                    _dayButtons[row, col].Tag = date;

                    // 事项颜色点
                    _dayDots[row, col].Children.Clear();
                    var dayEvents = GetEventsForDate(date);
                    var showEvents = dayEvents.Take(3).ToList();
                    foreach (var evt in showEvents)
                    {
                        _dayDots[row, col].Children.Add(new Border
                        {
                            Width = 5,
                            Height = 5,
                            CornerRadius = new CornerRadius(2.5),
                            Background = new SolidColorBrush(GetTypeColor(evt.Type)),
                            Margin = new Thickness(1, 0, 1, 0)
                        });
                    }
                    if (dayEvents.Count > 3)
                    {
                        _dayDots[row, col].Children.Add(new TextBlock
                        {
                            Text = $"+{dayEvents.Count - 3}",
                            FontSize = 8,
                            Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88))
                        });
                    }
                }
            }
        }

        /// <summary>
        /// 获取某天的所有事项（含重复展开）
        /// </summary>
        private List<CalendarEvent> GetEventsForDate(DateTime date)
        {
            return _events.Where(e => e.IsOnDate(date)).ToList();
        }

        #endregion

        #region 颜色

        /// <summary>
        /// 获取事项类型对应的颜色
        /// </summary>
        private static Color GetTypeColor(EventType type)
        {
            return type switch
            {
                EventType.Work => Color.FromRgb(0xE7, 0x48, 0x56),
                EventType.Life => Color.FromRgb(0x30, 0xBB, 0x43),
                EventType.Anniversary => Color.FromRgb(0x00, 0x78, 0xD4),
                EventType.Reminder => Color.FromRgb(0xFF, 0xB9, 0x00),
                _ => Colors.White
            };
        }

        #endregion

        #region 倒计时

        /// <summary>
        /// 渲染倒计时区域
        /// </summary>
        private void RenderCountdowns()
        {
            // 保留标题，清除其他
            while (CountdownPanel.Children.Count > 1)
                CountdownPanel.Children.RemoveAt(1);

            var now = DateTime.Now;
            var countdowns = new List<(CalendarEvent evt, bool isPast, TimeSpan span)>();

            foreach (var evt in _events)
            {
                // 跳过今天已过去的全天事项
                if (evt.IsAllDay && evt.Start.Date < now.Date) continue;

                var target = evt.Start;
                var span = target - now;

                if (span.TotalSeconds > 0)
                {
                    // 未来 → 倒计时
                    countdowns.Add((evt, false, span));
                }
                else if (span.TotalDays > -7)
                {
                    // 过去 7 天内 → 正计时
                    countdowns.Add((evt, true, -span));
                }
            }

            // 排序：未来按近→远，过去按远→近
            var sorted = countdowns
                .OrderBy(c => c.isPast ? 1 : 0)
                .ThenBy(c => c.isPast ? -c.span.TotalSeconds : c.span.TotalSeconds)
                .Take(5)
                .ToList();

            if (sorted.Count == 0)
            {
                CountdownPanel.Children.Add(new TextBlock
                {
                    Text = "暂无倒计时事项",
                    Foreground = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66)),
                    FontSize = 11
                });
                return;
            }

            foreach (var (evt, isPast, span) in sorted)
            {
                var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };

                // 图标
                var icon = isPast ? "✅" : "⏳";
                row.Children.Add(new TextBlock
                {
                    Text = icon,
                    FontSize = 11,
                    Margin = new Thickness(0, 0, 4, 0)
                });

                // 标题
                row.Children.Add(new TextBlock
                {
                    Text = evt.Title,
                    Foreground = new SolidColorBrush(GetTypeColor(evt.Type)),
                    FontSize = 11,
                    MaxWidth = 100,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    Margin = new Thickness(0, 0, 8, 0)
                });

                // 倒计时文字
                var countdownText = FormatCountdown(span, isPast);
                row.Children.Add(new TextBlock
                {
                    Text = countdownText,
                    Foreground = new SolidColorBrush(Color.FromRgb(0xAA, 0xCC, 0xFF)),
                    FontSize = 11
                });

                CountdownPanel.Children.Add(row);
            }
        }

        private static string FormatCountdown(TimeSpan span, bool isPast)
        {
            var prefix = isPast ? "已持续" : "还有";
            if (span.TotalDays >= 1)
                return $"{prefix} {(int)span.TotalDays}天 {span.Hours:D2}:{span.Minutes:D2}";
            if (span.TotalHours >= 1)
                return $"{prefix} {span.Hours}小时 {span.Minutes:D2}分";
            return $"{prefix} {span.Minutes}分钟";
        }

        #endregion

        #region 事件处理

        private void PrevMonth_Click(object sender, RoutedEventArgs e)
        {
            _currentMonth = _currentMonth.AddMonths(-1);
            RenderCalendar();
        }

        private void NextMonth_Click(object sender, RoutedEventArgs e)
        {
            _currentMonth = _currentMonth.AddMonths(1);
            RenderCalendar();
        }

        private void Today_Click(object sender, RoutedEventArgs e)
        {
            _currentMonth = DateTime.Today;
            RenderCalendar();
        }

        private void DayButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is DateTime date)
            {
                var existingEvents = GetEventsForDate(date);

                if (existingEvents.Count > 0)
                {
                    // 有事项 → 显示列表，可编辑/删除
                    var listWindow = new EventListWindow(date, existingEvents);
                    listWindow.Owner = this;

                    if (listWindow.ShowDialog() == true)
                    {
                        if (listWindow.DeleteResult != null)
                        {
                            _events.RemoveAll(ev => ev.Id == listWindow.DeleteResult);
                            SaveSettings();
                            RenderCalendar();
                            RenderCountdowns();
                        }
                        else if (listWindow.EditResult != null)
                        {
                            OpenEditWindow(listWindow.EditResult);
                        }
                        else if (listWindow.WantAdd)
                        {
                            OpenEditWindow(date);
                        }
                    }
                }
                else
                {
                    // 无事项 → 直接打开添加窗口
                    OpenEditWindow(date);
                }
            }
        }

        /// <summary>
        /// 打开添加事项窗口
        /// </summary>
        private void OpenEditWindow(DateTime date)
        {
            var editWindow = new EventEditWindow(date, new List<CalendarEvent>());
            editWindow.Owner = this;

            if (editWindow.ShowDialog() == true && editWindow.SavedEvent != null)
            {
                _events.Add(editWindow.SavedEvent);
                SaveSettings();
                RenderCalendar();
                RenderCountdowns();
            }
        }

        /// <summary>
        /// 打开编辑事项窗口
        /// </summary>
        private void OpenEditWindow(CalendarEvent evt)
        {
            var editWindow = new EventEditWindow(evt);
            editWindow.Owner = this;

            if (editWindow.ShowDialog() == true)
            {
                if (editWindow.DeletedEventId != null)
                {
                    _events.RemoveAll(ev => ev.Id == editWindow.DeletedEventId);
                }
                else if (editWindow.SavedEvent != null)
                {
                    var idx = _events.FindIndex(ev => ev.Id == editWindow.SavedEvent.Id);
                    if (idx >= 0)
                        _events[idx] = editWindow.SavedEvent;
                    else
                        _events.Add(editWindow.SavedEvent);
                }
                SaveSettings();
                RenderCalendar();
                RenderCountdowns();
            }
        }

        private void Lock_Click(object sender, RoutedEventArgs e)
        {
            IsLocked = !IsLocked;
            _config.Settings["isLocked"] = IsLocked;
            UpdateLockButton();
            SaveSettings();
        }

        private void UpdateLockButton()
        {
            LockButton.Content = IsLocked ? "🔒" : "🔓";
            LockButton.ToolTip = IsLocked ? "解锁" : "锁定";
            LockMenuItem.IsChecked = IsLocked;
            LockMenuItem.Header = IsLocked ? "解锁" : "锁定";
        }

        private void Close_Click(object sender, RoutedEventArgs e) => RequestClose();

        protected override void OnClosed(EventArgs e)
        {
            _countdownTimer?.Stop();
            _countdownTimer = null;
            base.OnClosed(e);
        }

        #endregion
    }
}
