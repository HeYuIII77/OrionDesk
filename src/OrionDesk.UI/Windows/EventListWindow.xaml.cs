using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using OrionDesk.BLL.Models;
using Orientation = System.Windows.Controls.Orientation;
using TextBlock = System.Windows.Controls.TextBlock;
using Button = System.Windows.Controls.Button;
using StackPanel = System.Windows.Controls.StackPanel;
using Border = System.Windows.Controls.Border;
using Color = System.Windows.Media.Color;
using Colors = System.Windows.Media.Colors;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;
using Brushes = System.Windows.Media.Brushes;

namespace OrionDesk.UI.Windows
{
    /// <summary>
    /// 事项列表窗口 - 显示某天的所有事项，支持编辑/删除/新增
    /// </summary>
    public partial class EventListWindow : Window
    {
        /// <summary>操作结果：编辑的事项</summary>
        public CalendarEvent? EditResult { get; private set; }

        /// <summary>操作结果：删除的事项 ID</summary>
        public string? DeleteResult { get; private set; }

        /// <summary>操作结果：是否要新增</summary>
        public bool WantAdd { get; private set; }

        private readonly DateTime _date;
        private readonly List<CalendarEvent> _events;

        public EventListWindow(DateTime date, List<CalendarEvent> events)
        {
            InitializeComponent();
            Topmost = true;
            _date = date;
            _events = events;
            TitleText.Text = date.ToString("yyyy年M月d日 dddd");
            BuildEventList();
        }

        private void BuildEventList()
        {
            EventPanel.Children.Clear();

            if (_events.Count == 0)
            {
                EventPanel.Children.Add(new TextBlock
                {
                    Text = "暂无事项",
                    Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88)),
                    FontSize = 13,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    Margin = new Thickness(0, 30, 0, 0)
                });
                return;
            }

            foreach (var evt in _events)
            {
                var itemBorder = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(0x1A, 0xFF, 0xFF, 0xFF)),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(10, 8, 10, 8),
                    Margin = new Thickness(0, 0, 0, 6)
                };

                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                // 颜色条
                var colorBar = new Border
                {
                    Background = new SolidColorBrush(GetTypeColor(evt.Type)),
                    CornerRadius = new CornerRadius(2),
                    Width = 4,
                    VerticalAlignment = VerticalAlignment.Stretch
                };
                Grid.SetColumn(colorBar, 0);
                grid.Children.Add(colorBar);

                // 事项信息
                var infoPanel = new StackPanel { Margin = new Thickness(8, 0, 0, 0) };
                infoPanel.Children.Add(new TextBlock
                {
                    Text = evt.Title,
                    Foreground = Brushes.White,
                    FontSize = 13,
                    FontWeight = FontWeights.SemiBold
                });

                var detailParts = new List<string>();
                if (!evt.IsAllDay)
                    detailParts.Add(evt.Start.ToString("HH:mm"));
                detailParts.Add(CalendarEvent.GetTypeName(evt.Type));
                if (evt.Repeat != EventRepeat.None)
                    detailParts.Add(CalendarEvent.GetRepeatName(evt.Repeat));

                infoPanel.Children.Add(new TextBlock
                {
                    Text = string.Join(" · ", detailParts),
                    Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88)),
                    FontSize = 11,
                    Margin = new Thickness(0, 2, 0, 0)
                });
                Grid.SetColumn(infoPanel, 1);
                grid.Children.Add(infoPanel);

                // 操作按钮
                var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

                var editBtn = new Button
                {
                    Content = "✏",
                    Style = (Style)FindResource("LockButtonStyle"),
                    ToolTip = "编辑",
                    Tag = evt
                };
                editBtn.Click += (s, e) => { EditResult = evt; DialogResult = true; };
                btnPanel.Children.Add(editBtn);

                var deleteBtn = new Button
                {
                    Content = "🗑",
                    Style = (Style)FindResource("LockButtonStyle"),
                    ToolTip = "删除",
                    Tag = evt
                };
                deleteBtn.Click += (s, e) =>
                {
                    var result = System.Windows.MessageBox.Show(
                        $"确定删除 \"{evt.Title}\"？",
                        "删除事项", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (result == MessageBoxResult.Yes)
                    {
                        DeleteResult = evt.Id;
                        DialogResult = true;
                    }
                };
                btnPanel.Children.Add(deleteBtn);

                Grid.SetColumn(btnPanel, 2);
                grid.Children.Add(btnPanel);

                itemBorder.Child = grid;
                EventPanel.Children.Add(itemBorder);
            }
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            WantAdd = true;
            DialogResult = true;
        }

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
    }
}
