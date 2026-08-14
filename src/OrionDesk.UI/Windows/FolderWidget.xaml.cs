using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using OrionDesk.BLL.Models;
using OrionDesk.BLL.Services;

namespace OrionDesk.UI.Windows
{
    /// <summary>
    /// 文件夹映射组件（树形结构）
    /// 拖入文件夹路径，展开查看子目录
    /// </summary>
    public partial class FolderWidget : BaseWidgetWindow
    {
        private string _folderPath = "";
        private bool _isSearching;

        public FolderWidget(WidgetConfig config, WidgetManager widgetManager)
            : base(config, widgetManager)
        {
            AcceptFileDrop = true; // 启用 Win32 文件拖放（绕过 WorkerW z-order 限制）
            InitializeComponent();

            // 直接初始化
            LoadLockState();
            LoadFolder();
            UpdateLockButton();
        }

        /// <summary>
        /// 加载保存的文件夹路径
        /// </summary>
        private void LoadFolder()
        {
            if (_config.Settings.TryGetValue("folderPath", out var path))
            {
                var p = path.ToString();
                if (!string.IsNullOrEmpty(p) && Directory.Exists(p))
                {
                    _folderPath = p;
                    LoadTree();
                }
            }
        }

        /// <summary>
        /// 保存文件夹路径
        /// </summary>
        private void SaveFolder()
        {
            _config.Settings["folderPath"] = _folderPath;
            if (!_widgetManager.IsRestoring)
            {
                try { _widgetManager.Save(); }
                catch { }
            }
        }

        /// <summary>
        /// 加载树形结构
        /// </summary>
        private void LoadTree()
        {
            FolderTree.Items.Clear();

            if (string.IsNullOrEmpty(_folderPath) || !Directory.Exists(_folderPath))
            {
                FolderPathText.Text = "拖入文件夹路径开始";
                return;
            }

            FolderPathText.Text = Path.GetFileName(_folderPath);
            FolderPathText.ToolTip = _folderPath;

            try
            {
                var rootNode = CreateDirectoryNode(new DirectoryInfo(_folderPath));
                FolderTree.Items.Add(rootNode);
                // 先加入树再展开，否则 Expanded 事件触发时节点不在视觉树中，懒加载不生效
                rootNode.IsExpanded = true;
            }
            catch (Exception ex)
            {
                FolderPathText.Text = "读取失败";
                Debug.WriteLine($"读取文件夹失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 创建目录节点（只加载一层子项）
        /// </summary>
        private TreeViewItem CreateDirectoryNode(DirectoryInfo dirInfo)
        {
            var node = new TreeViewItem
            {
                Header = "📁 " + dirInfo.Name,
                Tag = dirInfo.FullName,
                FontWeight = FontWeights.Normal,
                Foreground = System.Windows.Media.Brushes.White
            };

            // 添加占位子节点（用于显示展开箭头）
            try
            {
                if (dirInfo.GetDirectories().Length > 0 || dirInfo.GetFiles().Length > 0)
                {
                    node.Items.Add(new TreeViewItem { Visibility = Visibility.Collapsed }); // 占位
                }
            }
            catch { }

            return node;
        }

        /// <summary>
        /// 创建文件节点（仅显示文件名.扩展名）
        /// </summary>
        private TreeViewItem CreateFileNode(FileInfo fileInfo)
        {
            return new TreeViewItem
            {
                Header = $"📄 {fileInfo.Name}",
                Tag = fileInfo.FullName,
                FontWeight = FontWeights.Normal,
                Foreground = System.Windows.Media.Brushes.White
            };
        }

        /// <summary>
        /// 展开节点时加载子项
        /// </summary>
        private void FolderTree_Expanded(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is TreeViewItem node && node.Tag is string path)
            {
                // 移除占位节点
                if (node.Items.Count == 1 && node.Items[0] is TreeViewItem placeholder && placeholder.Visibility == Visibility.Collapsed)
                {
                    node.Items.Clear();
                }
                else if (node.Items.Count > 0)
                {
                    return; // 已经加载过了
                }

                try
                {
                    var dirInfo = new DirectoryInfo(path);

                    // 添加子文件夹
                    foreach (var subDir in dirInfo.GetDirectories())
                    {
                        try
                        {
                            node.Items.Add(CreateDirectoryNode(subDir));
                        }
                        catch { }
                    }

                    // 添加文件
                    foreach (var file in dirInfo.GetFiles())
                    {
                        try
                        {
                            node.Items.Add(CreateFileNode(file));
                        }
                        catch { }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"展开目录失败: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 双击文件打开
        /// </summary>
        private void FolderTree_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (FolderTree.SelectedItem is TreeViewItem node && node.Tag is string path)
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = path,
                        UseShellExecute = true
                    });
                }
                catch { }
            }
        }

        /// <summary>
        /// Win32 文件拖放下事件（绕过 WorkerW z-order 限制）
        /// </summary>
        protected override void OnFileDrop(string[] files)
        {
            if (files.Length > 0 && Directory.Exists(files[0]))
            {
                _folderPath = files[0];
                LoadTree();
                SaveFolder();
            }
        }

        #region 搜索

        private void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            var query = SearchBox.Text.Trim();
            SearchPlaceholder.Visibility = string.IsNullOrEmpty(query)
                ? Visibility.Visible
                : Visibility.Collapsed;

            if (string.IsNullOrEmpty(query))
            {
                _isSearching = false;
                LoadTree();
                return;
            }

            _isSearching = true;
            FolderTree.Items.Clear();

            if (string.IsNullOrEmpty(_folderPath) || !Directory.Exists(_folderPath))
                return;

            try
            {
                SearchDirectory(new DirectoryInfo(_folderPath), query, "");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"搜索失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 递归搜索目录，匹配文件名
        /// </summary>
        private void SearchDirectory(DirectoryInfo dir, string query, string relativePath)
        {
            try
            {
                // 搜索文件
                foreach (var file in dir.GetFiles())
                {
                    if (file.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                    {
                        var display = string.IsNullOrEmpty(relativePath)
                            ? $"📄 {file.Name}"
                            : $"📄 {file.Name}    ({relativePath})";
                        var node = new TreeViewItem
                        {
                            Header = display,
                            Tag = file.FullName,
                            Foreground = System.Windows.Media.Brushes.White
                        };
                        FolderTree.Items.Add(node);
                    }
                }

                // 递归子目录
                foreach (var subDir in dir.GetDirectories())
                {
                    try
                    {
                        var subRelative = string.IsNullOrEmpty(relativePath)
                            ? subDir.Name
                            : $"{relativePath}/{subDir.Name}";
                        SearchDirectory(subDir, query, subRelative);
                    }
                    catch { }
                }
            }
            catch { }
        }

        private void SearchBox_GotFocus(object sender, RoutedEventArgs e)
        {
            SearchPlaceholder.Visibility = Visibility.Collapsed;
        }

        private void SearchBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(SearchBox.Text))
                SearchPlaceholder.Visibility = Visibility.Visible;
        }

        #endregion

        #region 右键菜单

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new DocSettingsWindow(_folderPath, "文件夹映射设置", "文件夹路径");
            dialog.Owner = Window.GetWindow(this);
            if (dialog.ShowDialog() == true)
            {
                _folderPath = dialog.SelectedPath;
                LoadTree();
                SaveFolder();
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            if (_isSearching) return; // 搜索中不响应刷新
            if (!string.IsNullOrEmpty(_folderPath) && Directory.Exists(_folderPath))
                LoadTree();
        }

        private void Close_Click(object sender, RoutedEventArgs e) => RequestClose();

        private void Lock_Click(object sender, RoutedEventArgs e)
        {
            ToggleLock();
            UpdateLockButton();
        }

        private void UpdateLockButton()
        {
            LockButton.Content = IsLocked ? "🔒" : "🔓";
            LockButton.ToolTip = IsLocked ? "解锁" : "锁定";
            LockMenuItem.IsChecked = IsLocked;
            LockMenuItem.Header = IsLocked ? "解锁" : "锁定";
        }

        #endregion
    }
}
