using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using OrionDesk.BLL.Models;
using OrionDesk.BLL.Services;

namespace OrionDesk.UI.Windows
{
    /// <summary>
    /// 启动器组件
    /// 拖拽应用快捷方式添加，点击启动
    /// </summary>
    public partial class LauncherWidget : BaseWidgetWindow
    {
        private readonly LauncherSettings _settings;
        private readonly List<LauncherItem> _items = new List<LauncherItem>();
        private readonly HashSet<string> _hiddenIcons = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private StackPanel? _listPanel;

        // Win32 API for extracting icons
        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        private const uint SHGFI_ICON = 0x100;
        private const uint SHGFI_SMALLICON = 0x1;
        private const uint SHGFI_LARGEICON = 0x0;
        private const uint SHGFI_USEFILEATTRIBUTES = 0x10;

        public LauncherWidget(WidgetConfig config, WidgetManager widgetManager)
            : base(config, widgetManager)
        {
            AcceptFileDrop = true; // 启用 Win32 文件拖放（绕过 WorkerW z-order 限制）
            InitializeComponent();

            _settings = LoadSettings(config);

            // 直接初始化
            LoadLockState();
            LoadTitle();
            RestoreItems();
            UpdateHintVisibility();
            UpdateLockButton();

            // OrionDesk 启动时，隐藏名单中的桌面图标
            HideAllDesktopIcons();
        }

        /// <summary>
        /// 加载启动器设置
        /// </summary>
        private LauncherSettings LoadSettings(WidgetConfig config)
        {
            var settings = new LauncherSettings();

            if (config.Settings.TryGetValue("iconSize", out var iconSize))
                settings.IconSize = Convert.ToInt32(iconSize);

            if (config.Settings.TryGetValue("showName", out var showName))
                settings.ShowName = ToBool(showName);

            if (config.Settings.TryGetValue("viewMode", out var viewMode))
                settings.ViewMode = viewMode.ToString() ?? "Icons";

            // 加载应用列表
            if (config.Settings.TryGetValue("items", out var itemsObj) && itemsObj is System.Text.Json.JsonElement itemsElement)
            {
                foreach (var item in itemsElement.EnumerateArray())
                {
                    var launcherItem = new LauncherItem();
                    if (item.TryGetProperty("name", out var name))
                        launcherItem.Name = name.GetString() ?? "";
                    if (item.TryGetProperty("path", out var path))
                        launcherItem.Path = path.GetString() ?? "";
                    if (item.TryGetProperty("iconPath", out var iconPath))
                        launcherItem.IconPath = iconPath.GetString();
                    if (item.TryGetProperty("arguments", out var args))
                        launcherItem.Arguments = args.GetString();
                    if (item.TryGetProperty("shortcutName", out var sn))
                        launcherItem.ShortcutName = sn.GetString();

                    if (!string.IsNullOrEmpty(launcherItem.Path))
                        settings.Items.Add(launcherItem);
                }
            }

            return settings;
        }

        /// <summary>
        /// 保存启动器设置
        /// </summary>
        private void SaveSettings()
        {
            _config.Settings["items"] = _items.Select(i => new
            {
                name = i.Name,
                path = i.Path,
                iconPath = i.IconPath,
                arguments = i.Arguments,
                shortcutName = i.ShortcutName
            }).ToArray();

            _config.Settings["viewMode"] = _settings.ViewMode;

            if (!_widgetManager.IsRestoring)
            {
                try { _widgetManager.Save(); }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"保存启动器设置失败: {ex.Message}"); }
            }
        }

        /// <summary>
        /// 恢复已保存的应用列表
        /// </summary>
        private void RestoreItems()
        {
            foreach (var item in _settings.Items)
            {
                _items.Add(item);
            }
            RefreshView();
        }

        /// <summary>
        /// Win32 文件拖放下事件（绕过 WorkerW z-order 限制）
        /// </summary>
        protected override void OnFileDrop(string[] files)
        {
            if (IsLocked) return;

            foreach (var file in files)
            {
                // 只接受 .exe 和 .lnk 文件
                if (file.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
                    file.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
                {
                    AddApplication(file);
                }
            }
        }

        /// <summary>
        /// 添加应用程序
        /// </summary>
        private void AddApplication(string filePath)
        {
            // 解析快捷方式
            var actualPath = filePath;
            var name = Path.GetFileNameWithoutExtension(filePath);

            if (filePath.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
            {
                // 保留快捷方式自身的名称（桌面上显示的友好名称）
                var shortcutName = name;

                // 使用 Shell COM 解析快捷方式，获取目标路径
                object? shell = null, shortcut = null;
                try
                {
                    var shellType = Type.GetTypeFromProgID("WScript.Shell");
                    if (shellType != null)
                    {
                        shell = Activator.CreateInstance(shellType);
                        shortcut = shellType.InvokeMember("CreateShortcut",
                            System.Reflection.BindingFlags.InvokeMethod, null, shell, new object[] { filePath });

                        var targetPathProp = shortcut.GetType().InvokeMember("TargetPath",
                            System.Reflection.BindingFlags.GetProperty, null, shortcut, null);

                        if (targetPathProp != null && !string.IsNullOrEmpty(targetPathProp.ToString()))
                        {
                            actualPath = targetPathProp.ToString()!;
                        }
                    }
                }
                catch
                {
                    // 解析失败，使用原文件名
                }
                finally
                {
                    if (shortcut != null) try { Marshal.ReleaseComObject(shortcut); } catch { }
                    if (shell != null) try { Marshal.ReleaseComObject(shell); } catch { }
                }

                // 优先使用快捷方式名称，为空时回退到 exe 文件名
                name = !string.IsNullOrEmpty(shortcutName) ? shortcutName : Path.GetFileNameWithoutExtension(actualPath);
            }

            // 检查是否已存在
            if (_items.Any(i => i.Path.Equals(actualPath, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            var item = new LauncherItem
            {
                Name = name,
                Path = actualPath,
                // 如果是从桌面拖入的快捷方式，记录原始文件名用于恢复
                ShortcutName = filePath.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) && IsOnDesktop(filePath)
                    ? Path.GetFileNameWithoutExtension(filePath)
                    : null
            };

            _items.Add(item);
            SaveSettings();
            RefreshView();
            UpdateHintVisibility();

            // 如果是从桌面拖入的快捷方式，立即隐藏桌面图标
            if (!string.IsNullOrEmpty(item.ShortcutName))
            {
                HideDesktopIcon(item.ShortcutName);
                _hiddenIcons.Add(item.ShortcutName);
            }
        }

        /// <summary>
        /// 判断文件是否在桌面上
        /// </summary>
        private static bool IsOnDesktop(string filePath)
        {
            var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            var publicDesktop = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
            var fileDir = Path.GetDirectoryName(filePath);

            return string.Equals(fileDir, desktopPath, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(fileDir, publicDesktop, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// OrionDesk 启动时，隐藏名单中所有应用的桌面图标
        /// </summary>
        private void HideAllDesktopIcons()
        {
            foreach (var item in _items)
            {
                if (!string.IsNullOrEmpty(item.ShortcutName))
                {
                    HideDesktopIcon(item.ShortcutName);
                    _hiddenIcons.Add(item.ShortcutName);
                }
            }
        }

        /// <summary>
        /// 隐藏桌面图标（将快捷方式移到隐藏备份文件夹）
        /// </summary>
        private void HideDesktopIcon(string shortcutName)
        {
            try
            {
                var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                var publicDesktop = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
                var backupDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "OrionDesk", "hidden_icons");
                Directory.CreateDirectory(backupDir);

                // 在桌面和公共桌面查找快捷方式
                foreach (var desktop in new[] { desktopPath, publicDesktop })
                {
                    var lnkPath = Path.Combine(desktop, shortcutName + ".lnk");
                    if (File.Exists(lnkPath))
                    {
                        var backupPath = Path.Combine(backupDir, shortcutName + ".lnk");
                        if (File.Exists(backupPath))
                            File.Delete(backupPath);
                        File.Move(lnkPath, backupPath);
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"隐藏桌面图标失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 恢复桌面图标（从备份文件夹移回桌面）
        /// </summary>
        public static void RestoreDesktopIcon(string shortcutName)
        {
            try
            {
                var backupDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "OrionDesk", "hidden_icons");
                var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

                var backupPath = Path.Combine(backupDir, shortcutName + ".lnk");
                if (File.Exists(backupPath))
                {
                    var destPath = Path.Combine(desktopPath, shortcutName + ".lnk");
                    File.Move(backupPath, destPath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"恢复桌面图标失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 刷新视图（根据当前视图模式重建 UI）
        /// </summary>
        private void RefreshView()
        {
            // 防止 InitializeComponent 之前调用
            if (IconPanel == null || ContentScroller == null) return;

            // 清空所有面板
            IconPanel.Children.Clear();
            if (_listPanel != null)
                _listPanel.Children.Clear();

            // 根据视图模式重建
            if (_settings.ViewMode == "List")
            {
                // 确保列表面板存在
                if (_listPanel == null)
                {
                    _listPanel = new StackPanel();
                }
                // 每次切换都要重新设置 Content（上次可能被切回 IconPanel）
                ContentScroller.Content = _listPanel;

                foreach (var item in _items)
                {
                    AddAppListItem(item);
                }

                // 更新菜单勾选状态
                ViewModeIconsItem.IsChecked = false;
                ViewModeListItem.IsChecked = true;
            }
            else
            {
                // 图标模式：确保 ScrollViewer 的内容是 IconPanel
                ContentScroller.Content = IconPanel;

                foreach (var item in _items)
                {
                    AddAppIconItem(item);
                }

                // 更新菜单勾选状态
                ViewModeIconsItem.IsChecked = true;
                ViewModeListItem.IsChecked = false;
            }
        }

        /// <summary>
        /// 添加应用按钮到图标面板
        /// </summary>
        private void AddAppIconItem(LauncherItem item)
        {
            var button = new System.Windows.Controls.Button
            {
                Style = (Style)FindResource("AppButtonStyle"),
                Tag = item,
                ToolTip = item.Name,
                Width = _settings.IconSize + 16,
                Height = _settings.ShowName ? _settings.IconSize + 30 : _settings.IconSize + 10
            };

            // 创建内容
            var stackPanel = new System.Windows.Controls.StackPanel
            {
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center
            };

            // 图标
            var icon = new System.Windows.Controls.Image
            {
                Width = _settings.IconSize,
                Height = _settings.IconSize,
                Source = GetFileIcon(item.Path),
                Margin = new Thickness(0, 0, 0, _settings.ShowName ? 4 : 0)
            };
            stackPanel.Children.Add(icon);

            // 名称
            if (_settings.ShowName)
            {
                var nameText = new System.Windows.Controls.TextBlock
                {
                    Text = item.Name,
                    Foreground = System.Windows.Media.Brushes.White,
                    FontSize = 11,
                    FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
                    TextAlignment = System.Windows.TextAlignment.Center,
                    TextTrimming = System.Windows.TextTrimming.CharacterEllipsis,
                    MaxWidth = _settings.IconSize + 10
                };
                stackPanel.Children.Add(nameText);
            }

            button.Content = stackPanel;

            // 点击事件
            button.Click += (s, e) => LaunchItem(item);

            // 右键菜单 - 删除
            var contextMenu = new System.Windows.Controls.ContextMenu();
            var deleteItem = new System.Windows.Controls.MenuItem { Header = "删除" };
            deleteItem.Click += (s, e) => DeleteItem(item);
            contextMenu.Items.Add(deleteItem);
            button.ContextMenu = contextMenu;

            IconPanel.Children.Add(button);
        }

        /// <summary>
        /// 添加应用行到列表面板
        /// </summary>
        private void AddAppListItem(LauncherItem item)
        {
            var button = new System.Windows.Controls.Button
            {
                Style = (Style)FindResource("ListItemButtonStyle"),
                Tag = item,
                ToolTip = item.Path,
                HorizontalContentAlignment = System.Windows.HorizontalAlignment.Stretch
            };

            // 创建行内容：图标 + 名称 + 路径
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });  // 图标
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });  // 名称
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });  // 路径

            // 小图标
            var icon = new System.Windows.Controls.Image
            {
                Width = 16,
                Height = 16,
                Source = GetSmallIcon(item.Path),
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(icon, 0);
            grid.Children.Add(icon);

            // 名称
            var nameText = new System.Windows.Controls.TextBlock
            {
                Text = item.Name,
                Foreground = System.Windows.Media.Brushes.White,
                FontSize = 12,
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = System.Windows.TextTrimming.CharacterEllipsis
            };
            Grid.SetColumn(nameText, 1);
            grid.Children.Add(nameText);

            // 路径
            var pathText = new System.Windows.Controls.TextBlock
            {
                Text = item.Path,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x99, 0xFF, 0xFF, 0xFF)),
                FontSize = 11,
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = System.Windows.TextTrimming.CharacterEllipsis,
                Margin = new Thickness(8, 0, 0, 0)
            };
            Grid.SetColumn(pathText, 2);
            grid.Children.Add(pathText);

            button.Content = grid;

            // 点击事件
            button.Click += (s, e) => LaunchItem(item);

            // 右键菜单 - 删除
            var contextMenu = new System.Windows.Controls.ContextMenu();
            var deleteItem = new System.Windows.Controls.MenuItem { Header = "删除" };
            deleteItem.Click += (s, e) => DeleteItem(item);
            contextMenu.Items.Add(deleteItem);
            button.ContextMenu = contextMenu;

            _listPanel?.Children.Add(button);
        }

        /// <summary>
        /// 启动应用
        /// </summary>
        private void LaunchItem(LauncherItem item)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = item.Path,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"无法启动应用: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 删除应用项
        /// </summary>
        private void DeleteItem(LauncherItem item)
        {
            // 恢复桌面图标
            if (!string.IsNullOrEmpty(item.ShortcutName))
                RestoreDesktopIcon(item.ShortcutName);
            _items.Remove(item);
            SaveSettings();
            RefreshView();
            UpdateHintVisibility();
        }

        /// <summary>
        /// 获取小图标（16x16，用于列表视图）
        /// </summary>
        private ImageSource? GetSmallIcon(string filePath)
        {
            return ExtractIcon(filePath, SHGFI_SMALLICON);
        }

        /// <summary>
        /// 获取文件图标（使用大图标32x32）
        /// </summary>
        private ImageSource? GetFileIcon(string filePath)
        {
            return ExtractIcon(filePath, SHGFI_LARGEICON);
        }

        /// <summary>
        /// 提取文件图标（统一处理 GDI 资源释放）
        /// </summary>
        private ImageSource? ExtractIcon(string filePath, uint sizeFlag)
        {
            try
            {
                if (!File.Exists(filePath))
                    return null;

                var shinfo = new SHFILEINFO();
                SHGetFileInfo(filePath, 0, ref shinfo, (uint)Marshal.SizeOf(shinfo), SHGFI_ICON | sizeFlag);

                if (shinfo.hIcon != IntPtr.Zero)
                {
                    var icon = System.Drawing.Icon.FromHandle(shinfo.hIcon);
                    var bitmap = new System.Drawing.Bitmap(icon.Width, icon.Height);
                    using (var g = System.Drawing.Graphics.FromImage(bitmap))
                    {
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        g.DrawIcon(icon, 0, 0);
                    }
                    DestroyIcon(shinfo.hIcon);

                    var hBitmap = bitmap.GetHbitmap();
                    try
                    {
                        var bitmapSource = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                            hBitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                        bitmapSource.Freeze();
                        return bitmapSource;
                    }
                    finally
                    {
                        DeleteObject(hBitmap);  // 释放 GDI 句柄
                        bitmap.Dispose();       // 释放 Bitmap
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取图标失败: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// 更新提示文本可见性
        /// </summary>
        private void UpdateHintVisibility()
        {
            HintText.Visibility = _items.Count == 0 ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        }

        /// <summary>
        /// 加载标题
        /// </summary>
        private void LoadTitle()
        {
            if (_config.Settings.TryGetValue("title", out var title))
            {
                var titleStr = title.ToString() ?? "启动器";
                TitleText.Text = titleStr;
                TitleTextBox.Text = titleStr;
            }
        }

        /// <summary>
        /// 保存标题
        /// </summary>
        private void SaveTitle(string title)
        {
            _config.Settings["title"] = title;
            if (!_widgetManager.IsRestoring)
            {
                try { _widgetManager.Save(); }
                catch { }
            }
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

        #region 右键菜单事件

        private void ViewModeIcons_Click(object sender, RoutedEventArgs e)
        {
            _settings.ViewMode = "Icons";
            SaveSettings();
            RefreshView();
        }

        private void ViewModeList_Click(object sender, RoutedEventArgs e)
        {
            _settings.ViewMode = "List";
            SaveSettings();
            RefreshView();
        }

        private void ClearAll_Click(object sender, RoutedEventArgs e)
        {
            if (System.Windows.MessageBox.Show("确定要清除所有应用吗？", "确认", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                _items.Clear();
                IconPanel.Children.Clear();
                _listPanel?.Children.Clear();
                SaveSettings();
                UpdateHintVisibility();
            }
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

        private void Rename_Click(object sender, RoutedEventArgs e)
        {
            // 显示文本框，隐藏标题
            TitleText.Visibility = System.Windows.Visibility.Collapsed;
            TitleTextBox.Visibility = System.Windows.Visibility.Visible;
            TitleTextBox.Focus();
            TitleTextBox.SelectAll();
        }

        private void TitleText_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // 双击进入编辑模式
            if (e.ClickCount == 2)
            {
                Rename_Click(sender, e);
            }
        }

        private void TitleTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                FinishRename();
            }
            else if (e.Key == System.Windows.Input.Key.Escape)
            {
                CancelRename();
            }
        }

        private void TitleTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            FinishRename();
        }

        private void FinishRename()
        {
            var newTitle = TitleTextBox.Text.Trim();
            if (string.IsNullOrEmpty(newTitle))
                newTitle = "启动器";

            TitleText.Text = newTitle;
            TitleText.Visibility = System.Windows.Visibility.Visible;
            TitleTextBox.Visibility = System.Windows.Visibility.Collapsed;

            SaveTitle(newTitle);
        }

        private void CancelRename()
        {
            TitleTextBox.Text = TitleText.Text;
            TitleText.Visibility = System.Windows.Visibility.Visible;
            TitleTextBox.Visibility = System.Windows.Visibility.Collapsed;
        }

        #endregion

        protected override void OnClosed(EventArgs e)
        {
            // OrionDesk 退出时，恢复所有隐藏的桌面图标
            foreach (var shortcutName in _hiddenIcons.ToList())
            {
                RestoreDesktopIcon(shortcutName);
            }
            _hiddenIcons.Clear();

            base.OnClosed(e);
        }
    }
}
