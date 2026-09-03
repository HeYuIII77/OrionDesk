using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using OrionDesk.BLL.Models;
using OrionDesk.BLL.Services;

namespace OrionDesk.UI.Windows
{
    /// <summary>
    /// CMD 启动器组件 - 大图标，点击启动 CMD 并自动执行命令
    /// </summary>
    public partial class CmdLauncherWidget : BaseWidgetWindow
    {
        private string _displayName = "AI";
        private string _icon = "🖥";
        private string _command = "claude";
        private string _workDir = @"C:\WINDOWS\system32";

        public CmdLauncherWidget(WidgetConfig config, WidgetManager widgetManager)
            : base(config, widgetManager)
        {
            InitializeComponent();
            LoadSettings();
            LoadLockState();
        }

        #region 配置

        private void LoadSettings()
        {
            if (_config.Settings.TryGetValue("displayName", out var n))
                _displayName = n?.ToString() ?? "AI";
            if (_config.Settings.TryGetValue("icon", out var ic))
                _icon = ic?.ToString() ?? "🖥";
            if (_config.Settings.TryGetValue("command", out var c))
                _command = c?.ToString() ?? "claude";
            if (_config.Settings.TryGetValue("workDir", out var w))
                _workDir = w?.ToString() ?? @"C:\WINDOWS\system32";
            UpdateDisplay();
        }

        private void SaveSettings()
        {
            if (_widgetManager.IsRestoring) return;
            _config.Settings["displayName"] = _displayName;
            _config.Settings["icon"] = _icon;
            _config.Settings["command"] = _command;
            _config.Settings["workDir"] = _workDir;
            _widgetManager.Save();
        }

        private void LoadLockState()
        {
            if (_config.Settings.TryGetValue("isLocked", out var val))
                IsLocked = ToBool(val);
            UpdateLockButton();
        }

        private void UpdateDisplay()
        {
            Title = _displayName;
            TitleText.Text = $"⚡ {_displayName}";
            IconText.Text = string.IsNullOrWhiteSpace(_icon) ? "🖥" : _icon;
            LaunchButton.ToolTip = string.IsNullOrWhiteSpace(_command)
                ? "右键 → 设置"
                : "点击启动";
        }

        #endregion

        #region 事件

        private void LaunchButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_command))
            {
                System.Windows.MessageBox.Show("请右键组件 → 设置", "提示",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 多行命令用 && 连接（每行一条命令）
            var lines = _command.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrEmpty(l))
                .ToArray();
            if (lines.Length == 0)
            {
                System.Windows.MessageBox.Show("请右键组件 → 设置", "提示",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var cmdArgs = string.Join(" && ", lines);

            try
            {
                var workDir = string.IsNullOrWhiteSpace(_workDir) ? @"C:\WINDOWS\system32" : _workDir;
                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/k {cmdArgs}",
                    WorkingDirectory = workDir,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CmdLauncher] 启动失败: {ex.Message}");
                System.Windows.MessageBox.Show($"启动失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SetCommand_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new CmdLauncherSettingsWindow(_displayName, _icon, _command, _workDir);
            dialog.Owner = this;
            if (dialog.ShowDialog() == true)
            {
                _displayName = dialog.ResultName ?? _displayName;
                _icon = dialog.ResultIcon ?? _icon;
                _command = dialog.ResultCommand ?? _command;
                _workDir = dialog.ResultWorkDir ?? _workDir;
                SaveSettings();
                UpdateDisplay();
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

        #endregion
    }
}
