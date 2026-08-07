using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

using UserControl = System.Windows.Controls.UserControl;

namespace OrionDesk.UI.Controls
{
    /// <summary>
    /// 暗色日期选择控件 - 替代标准 DatePicker
    /// </summary>
    public partial class DarkDatePicker : UserControl
    {
        #region 依赖属性

        public static readonly DependencyProperty SelectedDateProperty =
            DependencyProperty.Register(nameof(SelectedDate), typeof(DateTime?),
                typeof(DarkDatePicker), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedDateChanged));

        public DateTime? SelectedDate
        {
            get => (DateTime?)GetValue(SelectedDateProperty);
            set => SetValue(SelectedDateProperty, value);
        }

        private static void OnSelectedDateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is DarkDatePicker ctrl)
                ctrl.UpdateDisplay();
        }

        #endregion

        public DarkDatePicker()
        {
            InitializeComponent();
            Loaded += (s, e) => UpdateDisplay();

            // 点击外部关闭日历
            AddHandler(Mouse.PreviewMouseDownOutsideCapturedElementEvent, new RoutedEventHandler(OnMouseDownOutside));
        }

        #region 事件处理

        private void CalendarToggle_Click(object sender, RoutedEventArgs e)
        {
            CalendarPopup.IsOpen = !CalendarPopup.IsOpen;
            if (CalendarPopup.IsOpen && SelectedDate.HasValue)
            {
                CalendarControl.SelectedDate = SelectedDate.Value;
                CalendarControl.DisplayDate = SelectedDate.Value;
            }
        }

        private void CalendarControl_SelectedDatesChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (CalendarControl.SelectedDate.HasValue)
            {
                SelectedDate = CalendarControl.SelectedDate.Value;
                UpdateDisplay();
                CalendarPopup.IsOpen = false;
            }
        }

        private void CalendarControl_DisplayDateChanged(object? sender, CalendarDateChangedEventArgs e)
        {
            // 不做额外处理
        }

        private void DateTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            ParseAndApplyDate();
        }

        private void DateTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ParseAndApplyDate();
                CalendarPopup.IsOpen = false;
            }
        }

        private void OnMouseDownOutside(object? sender, RoutedEventArgs e)
        {
            CalendarPopup.IsOpen = false;
        }

        #endregion

        #region 辅助方法

        private void UpdateDisplay()
        {
            DateTextBox.Text = SelectedDate.HasValue
                ? SelectedDate.Value.ToString("yyyy-MM-dd")
                : "";
        }

        private void ParseAndApplyDate()
        {
            if (DateTime.TryParse(DateTextBox.Text, out var date))
            {
                SelectedDate = date;
            }
            else
            {
                // 解析失败，恢复原值
                UpdateDisplay();
            }
        }

        #endregion
    }
}
