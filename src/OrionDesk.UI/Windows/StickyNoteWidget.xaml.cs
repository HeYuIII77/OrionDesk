using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using OrionDesk.BLL.Models;
using OrionDesk.BLL.Services;

namespace OrionDesk.UI.Windows
{
    /// <summary>
    /// 便签组件
    /// 支持多颜色、防抖保存
    /// </summary>
    public partial class StickyNoteWidget : BaseWidgetWindow
    {
        private readonly NoteSettings _settings;
        private readonly DispatcherTimer _saveTimer;

        // 可用颜色
        private static readonly string[] Colors = new[]
        {
            "#FFFACD", // 黄色
            "#FFFFB6C1", // 粉色
            "#FFADD8E6", // 蓝色
            "#FF90EE90"  // 绿色
        };

        public StickyNoteWidget(WidgetConfig config, WidgetManager widgetManager)
            : base(config, widgetManager)
        {
            InitializeComponent();

            _settings = LoadSettings(config);

            // 防抖保存定时器（500ms）
            _saveTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _saveTimer.Tick += (s, e) =>
            {
                _saveTimer.Stop();
                SaveContent();
            };

            // 直接初始化
            LoadLockState();
            ApplySettings();
            UpdateLockButton();
        }

        /// <summary>
        /// 加载便签设置
        /// </summary>
        private NoteSettings LoadSettings(WidgetConfig config)
        {
            var settings = new NoteSettings();

            if (config.Settings.TryGetValue("content", out var content))
                settings.Content = content.ToString() ?? "";

            if (config.Settings.TryGetValue("backgroundColor", out var bgColor))
                settings.BackgroundColor = bgColor.ToString() ?? "#FFFACD";

            if (config.Settings.TryGetValue("fontSize", out var fontSize))
                settings.FontSize = ToDouble(fontSize, 14);

            return settings;
        }

        /// <summary>
        /// 应用设置
        /// </summary>
        private void ApplySettings()
        {
            // 应用背景颜色
            try
            {
                var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(_settings.BackgroundColor);
                NoteBorder.Background = new SolidColorBrush(color);
            }
            catch
            {
                NoteBorder.Background = new SolidColorBrush(System.Windows.Media.Colors.Yellow);
            }

            // 应用内容
            NoteTextBox.Text = _settings.Content;
            NoteTextBox.FontSize = _settings.FontSize;
        }

        /// <summary>
        /// 保存内容
        /// </summary>
        private void SaveContent()
        {
            _config.Settings["content"] = NoteTextBox.Text;
            _config.Settings["backgroundColor"] = _settings.BackgroundColor;
            _config.Settings["fontSize"] = _settings.FontSize;

            if (!_widgetManager.IsRestoring)
            {
                try { _widgetManager.Save(); }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"保存便签内容失败: {ex.Message}"); }
            }
        }

        /// <summary>
        /// 文本变化时触发防抖保存
        /// </summary>
        private void NoteTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            _saveTimer.Stop();
            _saveTimer.Start();
        }

        #region 颜色切换

        private void ColorYellow_Click(object sender, RoutedEventArgs e)
        {
            ChangeColor(Colors[0]);
        }

        private void ColorPink_Click(object sender, RoutedEventArgs e)
        {
            ChangeColor(Colors[1]);
        }

        private void ColorBlue_Click(object sender, RoutedEventArgs e)
        {
            ChangeColor(Colors[2]);
        }

        private void ColorGreen_Click(object sender, RoutedEventArgs e)
        {
            ChangeColor(Colors[3]);
        }

        private void ChangeColor(string colorHex)
        {
            _settings.BackgroundColor = colorHex;
            try
            {
                var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colorHex);
                NoteBorder.Background = new SolidColorBrush(color);

                // 根据背景色调整文字颜色
                var brightness = (color.R * 0.299 + color.G * 0.587 + color.B * 0.114) / 255;
                NoteTextBox.Foreground = brightness > 0.5
                    ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(51, 51, 51))   // 深色文字
                    : new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 255, 255)); // 浅色文字

                SaveContent();
            }
            catch { }
        }

        #endregion

        #region 删除和锁定

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (System.Windows.MessageBox.Show("确定要删除这个便签吗？", "确认",
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                RequestClose();
            }
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
            _saveTimer?.Stop();
            base.OnClosed(e);
        }
    }
}
