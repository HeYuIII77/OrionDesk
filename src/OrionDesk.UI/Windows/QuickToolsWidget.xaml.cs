using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Windows;
using OrionDesk.BLL.Models;
using OrionDesk.BLL.Services;

using Button = System.Windows.Controls.Button;
using ContextMenu = System.Windows.Controls.ContextMenu;
using MenuItem = System.Windows.Controls.MenuItem;
using StackPanel = System.Windows.Controls.StackPanel;
using TextBlock = System.Windows.Controls.TextBlock;
using Media = System.Windows.Media;

namespace OrionDesk.UI.Windows
{
    /// <summary>
    /// 快捷工具组件 - 开发者效率工具面板
    /// </summary>
    public partial class QuickToolsWidget : BaseWidgetWindow
    {
        #region 字段

        private List<QuickToolItem> _items = new();

        #endregion

        #region 构造函数

        public QuickToolsWidget(WidgetConfig config, WidgetManager widgetManager)
            : base(config, widgetManager)
        {
            InitializeComponent();
            LoadSettings();
            LoadLockState();
            RebuildToolPanel();
        }

        #endregion

        #region 配置读写

        /// <summary>
        /// 加载设置
        /// </summary>
        private void LoadSettings()
        {
            _items.Clear();

            // 从配置读取工具列表
            if (_config.Settings.TryGetValue("items", out var raw) && raw is JsonElement je
                && je.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in je.EnumerateArray())
                {
                    var tool = new QuickToolItem();
                    if (item.TryGetProperty("name", out var n)) tool.Name = n.GetString() ?? "";
                    if (item.TryGetProperty("icon", out var ic)) tool.Icon = ic.GetString() ?? "";
                    if (item.TryGetProperty("path", out var p)) tool.Path = p.GetString() ?? "";
                    if (item.TryGetProperty("arguments", out var a)) tool.Arguments = a.GetString() ?? "";
                    if (item.TryGetProperty("type", out var t)) tool.Type = ParseToolType(t.GetString());
                    if (item.TryGetProperty("runAsAdmin", out var ra)) tool.RunAsAdmin = ra.ValueKind == JsonValueKind.True;
                    if (item.TryGetProperty("isPreset", out var ip)) tool.IsPreset = ip.ValueKind == JsonValueKind.True;
                    if (item.TryGetProperty("category", out var c)) tool.Category = c.GetString() ?? "custom";
                    if (item.TryGetProperty("id", out var id)) tool.Id = id.GetString() ?? tool.Id;
                    _items.Add(tool);
                }
            }

            // 首次加载：写入默认工具列表
            if (_items.Count == 0)
            {
                _items = GetPresetTools();
                SaveSettings();
            }
        }

        /// <summary>
        /// 保存设置
        /// </summary>
        private void SaveSettings()
        {
            if (_widgetManager.IsRestoring) return;

            _config.Settings["items"] = _items.Select(item => new Dictionary<string, object>
            {
                ["id"] = item.Id,
                ["name"] = item.Name,
                ["icon"] = item.Icon,
                ["path"] = item.Path,
                ["arguments"] = item.Arguments,
                ["type"] = item.Type.ToString(),
                ["runAsAdmin"] = item.RunAsAdmin,
                ["isPreset"] = item.IsPreset,
                ["category"] = item.Category
            }).ToArray();

            _widgetManager.Save();
        }

        /// <summary>
        /// 加载锁定状态
        /// </summary>
        private new void LoadLockState()
        {
            if (_config.Settings.TryGetValue("isLocked", out var val))
                IsLocked = ToBool(val);
            UpdateLockButton();
        }

        #endregion

        #region UI 构建

        /// <summary>
        /// 重建工具面板
        /// </summary>
        private void RebuildToolPanel()
        {
            ToolPanel.Children.Clear();

            if (_items.Count == 0)
            {
                // 空状态提示
                ToolPanel.Children.Add(new TextBlock
                {
                    Text = "右键 → 添加工具\n或点击左上角 ＋",
                    Foreground = new Media.SolidColorBrush(Media.Color.FromArgb(0x88, 0xFF, 0xFF, 0xFF)),
                    FontSize = 12,
                    TextAlignment = System.Windows.TextAlignment.Center,
                    Margin = new Thickness(0, 40, 0, 0)
                });
                return;
            }

            foreach (var item in _items)
                AddToolButton(item);
        }

        /// <summary>
        /// 添加工具按钮到面板
        /// </summary>
        private void AddToolButton(QuickToolItem item)
        {
            var button = new Button
            {
                Style = (Style)FindResource("ToolButtonStyle"),
                ToolTip = BuildToolTip(item),
                Tag = item
            };

            // 按钮内容：图标 + 名称
            var panel = new StackPanel
            {
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            };
            panel.Children.Add(new TextBlock
            {
                Text = string.IsNullOrEmpty(item.Icon) ? "🔧" : item.Icon,
                FontSize = 24,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 2)
            });
            panel.Children.Add(new TextBlock
            {
                Text = item.Name,
                FontSize = 11,
                Foreground = new Media.SolidColorBrush(Media.Color.FromRgb(0xCC, 0xCC, 0xCC)),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 56
            });
            button.Content = panel;

            // 左键启动
            button.Click += (s, e) => LaunchTool(item);

            // 右键菜单
            var ctx = new ContextMenu();
            var runAsItem = new MenuItem { Header = "以管理员身份运行" };
            runAsItem.Click += (s, e) => LaunchTool(item, forceAdmin: true);
            ctx.Items.Add(runAsItem);

            var editItem = new MenuItem { Header = "编辑" };
            editItem.Click += (s, e) => EditTool(item);
            ctx.Items.Add(editItem);

            var deleteItem = new MenuItem { Header = "删除" };
            deleteItem.Click += (s, e) => DeleteTool(item);
            ctx.Items.Add(deleteItem);

            button.ContextMenu = ctx;
            ToolPanel.Children.Add(button);
        }

        /// <summary>
        /// 构建工具提示
        /// </summary>
        private static string BuildToolTip(QuickToolItem item)
        {
            var tip = item.Name;
            if (!string.IsNullOrEmpty(item.Path))
                tip += $"\n{item.Path}";
            if (item.RunAsAdmin)
                tip += "\n[管理员]";
            return tip;
        }

        #endregion

        #region 启动逻辑

        /// <summary>
        /// 启动工具
        /// </summary>
        private void LaunchTool(QuickToolItem item, bool forceAdmin = false)
        {
            try
            {
                switch (item.Type)
                {
                    case QuickToolType.App:
                        var psi = new ProcessStartInfo
                        {
                            FileName = item.Path,
                            Arguments = item.Arguments,
                            UseShellExecute = true
                        };
                        if (item.RunAsAdmin || forceAdmin)
                            psi.Verb = "runas";
                        Process.Start(psi);
                        break;

                    case QuickToolType.Folder:
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = "explorer.exe",
                            Arguments = item.Path,
                            UseShellExecute = true
                        });
                        break;

                    case QuickToolType.Url:
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = item.Path,
                            UseShellExecute = true
                        });
                        break;

                    case QuickToolType.Shell:
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = "cmd.exe",
                            Arguments = $"/k {item.Path}",
                            UseShellExecute = true
                        });
                        break;
                }
            }
            catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                // 用户取消了 UAC 提示，静默处理
                Debug.WriteLine("[QuickTools] 用户取消管理员权限提升");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[QuickTools] 启动失败: {ex.Message}");
                System.Windows.MessageBox.Show($"启动失败: {item.Name}\n{ex.Message}",
                    "快捷工具", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        #endregion

        #region 添加/编辑/删除

        /// <summary>
        /// 添加工具
        /// </summary>
        private void AddTool_Click(object sender, RoutedEventArgs e)
        {
            var editWindow = new QuickToolEditWindow();
            editWindow.Owner = this;
            if (editWindow.ShowDialog() == true && editWindow.Result != null)
            {
                _items.Add(editWindow.Result);
                AddToolButton(editWindow.Result);
                SaveSettings();
            }
        }

        /// <summary>
        /// 编辑工具
        /// </summary>
        private void EditTool(QuickToolItem item)
        {
            var editWindow = new QuickToolEditWindow(item);
            editWindow.Owner = this;
            if (editWindow.ShowDialog() == true && editWindow.Result != null)
            {
                // 更新原项
                var index = _items.IndexOf(item);
                if (index >= 0)
                    _items[index] = editWindow.Result;
                RebuildToolPanel();
                SaveSettings();
            }
        }

        /// <summary>
        /// 删除工具
        /// </summary>
        private void DeleteTool(QuickToolItem item)
        {
            var result = System.Windows.MessageBox.Show($"确定删除 \"{item.Name}\"？",
                "快捷工具", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                _items.Remove(item);
                RebuildToolPanel();
                SaveSettings();
            }
        }

        #endregion

        #region 锁定

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

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        #endregion

        #region 预置工具

        /// <summary>
        /// 获取预置工具列表
        /// </summary>
        private static List<QuickToolItem> GetPresetTools()
        {
            return new List<QuickToolItem>
            {
                // 系统工具
                new() { Name = "CMD", Icon = "🖥", Path = "cmd.exe", Category = "system" },
                new() { Name = "管理员CMD", Icon = "🖥", Path = "cmd.exe", RunAsAdmin = true, Category = "system" },
                new() { Name = "PowerShell", Icon = "💻", Path = "powershell.exe", Category = "system" },
                new() { Name = "管理员PS", Icon = "💻", Path = "powershell.exe", RunAsAdmin = true, Category = "system" },
                new() { Name = "终端", Icon = "🔧", Path = "wt.exe", Category = "system" },
                new() { Name = "任务管理器", Icon = "📊", Path = "taskmgr.exe", Category = "system" },
                new() { Name = "设备管理器", Icon = "🔌", Path = "devmgmt.msc", Category = "system" },
                new() { Name = "服务管理", Icon = "⚙", Path = "services.msc", Category = "system" },
                new() { Name = "注册表", Icon = "📋", Path = "regedit.exe", Category = "system" },
                new() { Name = "事件查看器", Icon = "📜", Path = "eventvwr.msc", Category = "system" },
                new() { Name = "计算机管理", Icon = "🖥", Path = "compmgmt.msc", Category = "system" },
                new() { Name = "资源监视器", Icon = "📈", Path = "resmon.exe", Category = "system" },

                // 开发工具
                new() { Name = "VS Code", Icon = "🔨", Path = "code", Category = "dev" },
                new() { Name = "VS", Icon = "🔨", Path = "devenv.exe", Category = "dev" },
                new() { Name = "Git Bash", Icon = "🐙", Path = @"C:\Program Files\Git\git-bash.exe", Category = "dev" },
                new() { Name = "Explorer", Icon = "📁", Path = "explorer.exe", Category = "dev" },
            };
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 解析工具类型
        /// </summary>
        private static QuickToolType ParseToolType(string? value)
        {
            return value switch
            {
                "App" => QuickToolType.App,
                "Folder" => QuickToolType.Folder,
                "Url" => QuickToolType.Url,
                "Shell" => QuickToolType.Shell,
                _ => QuickToolType.App
            };
        }

        #endregion
    }
}
