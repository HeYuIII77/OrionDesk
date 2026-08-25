using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using OrionDesk.BLL.Models;
using OrionDesk.UI.Controls;

namespace OrionDesk.UI.Windows
{
    /// <summary>
    /// 事项编辑对话框 - 添加/编辑日历事项
    /// </summary>
    public partial class EventEditWindow : Window
    {
        /// <summary>保存的事项（对话框返回 true 且非 null 时有效）</summary>
        public CalendarEvent? SavedEvent { get; private set; }

        /// <summary>删除的事项 ID（对话框返回 true 且非 null 时有效）</summary>
        public string? DeletedEventId { get; private set; }

        private readonly CalendarEvent? _editingEvent;

        /// <summary>
        /// 新建模式
        /// </summary>
        public EventEditWindow(DateTime date, List<CalendarEvent> existingEvents)
        {
            InitializeComponent();
            Topmost = true;
            InitComboBoxes();

            DialogTitle.Text = "添加事项";
            DatePicker.SelectedDate = date;
            DeleteButton.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// 编辑模式（点击已有事项时）
        /// </summary>
        public EventEditWindow(CalendarEvent evt) : this(evt.Start, new List<CalendarEvent>())
        {
            _editingEvent = evt;
            DialogTitle.Text = "编辑事项";
            DeleteButton.Visibility = Visibility.Visible;

            // 填充表单
            TitleBox.Text = evt.Title;
            DatePicker.SelectedDate = evt.Start.Date;
            AllDayCheckBox.IsChecked = evt.IsAllDay;

            if (!evt.IsAllDay)
            {
                StartHourBox.Text = evt.Start.Hour.ToString("D2");
                StartMinBox.Text = evt.Start.Minute.ToString("D2");
                if (evt.End.HasValue)
                {
                    EndHourBox.Text = evt.End.Value.Hour.ToString("D2");
                    EndMinBox.Text = evt.End.Value.Minute.ToString("D2");
                }
            }

            // 选中类型和重复
            TypeCombo.SelectedTag = evt.Type.ToString();
            RepeatCombo.SelectedTag = evt.Repeat.ToString();

            NoteBox.Text = evt.Note;
            UpdateTimePanelVisibility();
        }

        /// <summary>
        /// 初始化下拉框选项
        /// </summary>
        private void InitComboBoxes()
        {
            TypeCombo.ItemsSource = new ObservableCollection<DarkComboBoxItem>
            {
                new() { DisplayText = "🔴 工作", Tag = "Work" },
                new() { DisplayText = "🟢 生活", Tag = "Life" },
                new() { DisplayText = "🔵 纪念日", Tag = "Anniversary" },
                new() { DisplayText = "🟡 提醒", Tag = "Reminder" }
            };
            TypeCombo.SelectedTag = "Work";

            RepeatCombo.ItemsSource = new ObservableCollection<DarkComboBoxItem>
            {
                new() { DisplayText = "不重复", Tag = "None" },
                new() { DisplayText = "每天", Tag = "Daily" },
                new() { DisplayText = "每周", Tag = "Weekly" },
                new() { DisplayText = "每月", Tag = "Monthly" },
                new() { DisplayText = "每年", Tag = "Yearly" }
            };
            RepeatCombo.SelectedTag = "None";
        }

        private void AllDayCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            UpdateTimePanelVisibility();
        }

        private void UpdateTimePanelVisibility()
        {
            TimePanel.Visibility = AllDayCheckBox.IsChecked == true
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // 验证
            if (string.IsNullOrWhiteSpace(TitleBox.Text))
            {
                System.Windows.MessageBox.Show("请输入标题", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                TitleBox.Focus();
                return;
            }

            if (DatePicker.SelectedDate == null)
            {
                System.Windows.MessageBox.Show("请选择日期", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var date = DatePicker.SelectedDate.Value;
            var isAllDay = AllDayCheckBox.IsChecked == true;
            var typeStr = TypeCombo.SelectedTag ?? "Work";
            var repeatStr = RepeatCombo.SelectedTag ?? "None";

            DateTime start;
            DateTime? end = null;

            if (isAllDay)
            {
                start = date.Date;
            }
            else
            {
                if (!int.TryParse(StartHourBox.Text, out var sh) || sh < 0 || sh > 23 ||
                    !int.TryParse(StartMinBox.Text, out var sm) || sm < 0 || sm > 59)
                {
                    System.Windows.MessageBox.Show("开始时间格式不正确", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                start = date.Date.AddHours(sh).AddMinutes(sm);

                if (int.TryParse(EndHourBox.Text, out var eh) && eh >= 0 && eh <= 23 &&
                    int.TryParse(EndMinBox.Text, out var em) && em >= 0 && em <= 59)
                {
                    end = date.Date.AddHours(eh).AddMinutes(em);
                }
            }

            // 构建事项
            SavedEvent = _editingEvent != null
                ? new CalendarEvent
                {
                    Id = _editingEvent.Id,
                    Title = TitleBox.Text.Trim(),
                    Start = start,
                    End = end,
                    IsAllDay = isAllDay,
                    Type = Enum.Parse<EventType>(typeStr),
                    Repeat = Enum.Parse<EventRepeat>(repeatStr),
                    Note = NoteBox.Text.Trim()
                }
                : new CalendarEvent
                {
                    Title = TitleBox.Text.Trim(),
                    Start = start,
                    End = end,
                    IsAllDay = isAllDay,
                    Type = Enum.Parse<EventType>(typeStr),
                    Repeat = Enum.Parse<EventRepeat>(repeatStr),
                    Note = NoteBox.Text.Trim()
                };

            DialogResult = true;
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (_editingEvent != null)
            {
                var result = System.Windows.MessageBox.Show(
                    $"确定删除 \"{_editingEvent.Title}\"？",
                    "删除事项", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    DeletedEventId = _editingEvent.Id;
                    DialogResult = true;
                }
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
