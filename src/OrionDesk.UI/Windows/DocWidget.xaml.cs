using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using OrionDesk.BLL.Models;
using OrionDesk.BLL.Services;

namespace OrionDesk.UI.Windows
{
    /// <summary>
    /// 文档中心组件
    /// 指定文档根目录，树形结构展示文件夹和文件
    /// 支持拖入归档、搜索、新建、重命名、删除
    /// </summary>
    public partial class DocWidget : BaseWidgetWindow
    {
        private string _rootPath = "";
        private bool _isSearching;

        // 拖放目标高亮
        private TreeViewItem? _dragTarget;
        private System.Windows.Media.Brush? _dragTargetOriginalBg;

        public DocWidget(WidgetConfig config, WidgetManager widgetManager)
            : base(config, widgetManager)
        {
            AcceptFileDrop = true; // 启用 Win32 文件拖放（绕过 WorkerW z-order 限制）
            InitializeComponent();

            // 注册 TreeViewItem 级别的拖放事件（XAML 中与 TreeView 冲突，改用 AddHandler）
            DocTree.AllowDrop = true; // 内部拖放需要 AllowDrop
            DocTree.AddHandler(TreeViewItem.DragOverEvent, new System.Windows.DragEventHandler(TreeViewItem_DragOver), true);
            DocTree.AddHandler(TreeViewItem.DragLeaveEvent, new System.Windows.DragEventHandler(TreeViewItem_DragLeave), true);
            DocTree.AddHandler(TreeViewItem.DropEvent, new System.Windows.DragEventHandler(TreeViewItem_Drop), true);

            LoadLockState();
            LoadSettings();
            UpdateLockButton();
            UpdateHintVisibility();
        }

        #region 设置

        private void LoadSettings()
        {
            if (_config.Settings.TryGetValue("rootPath", out var path))
            {
                var p = path.ToString();
                if (!string.IsNullOrEmpty(p) && Directory.Exists(p))
                {
                    _rootPath = p;
                    LoadTree();
                }
            }
        }

        private void SaveRootPath()
        {
            _config.Settings["rootPath"] = _rootPath;
            if (!_widgetManager.IsRestoring)
            {
                try { _widgetManager.Save(); }
                catch { }
            }
        }

        #endregion

        #region 树加载

        /// <summary>
        /// 加载整棵树
        /// </summary>
        private void LoadTree()
        {
            DocTree.Items.Clear();

            if (string.IsNullOrEmpty(_rootPath) || !Directory.Exists(_rootPath))
            {
                _rootPath = "";
                UpdateHintVisibility();
                return;
            }

            HintText.Visibility = Visibility.Collapsed;

            try
            {
                var dirInfo = new DirectoryInfo(_rootPath);

                // 先添加子目录
                foreach (var subDir in dirInfo.GetDirectories().OrderBy(d => d.Name))
                {
                    try { DocTree.Items.Add(CreateDirectoryNode(subDir)); }
                    catch { }
                }

                // 再添加文件
                foreach (var file in dirInfo.GetFiles().OrderBy(f => f.Name))
                {
                    try { DocTree.Items.Add(CreateFileNode(file)); }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"加载文档树失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 创建目录节点（带占位子节点实现懒加载）
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

            // 占位子节点（显示展开箭头）
            try
            {
                if (dirInfo.GetDirectories().Length > 0 || dirInfo.GetFiles().Length > 0)
                {
                    node.Items.Add(new TreeViewItem { Visibility = Visibility.Collapsed });
                }
            }
            catch { }

            return node;
        }

        /// <summary>
        /// 创建文件节点
        /// </summary>
        private TreeViewItem CreateFileNode(FileInfo fileInfo)
        {
            return new TreeViewItem
            {
                Header = "📄 " + fileInfo.Name,
                Tag = fileInfo.FullName,
                FontWeight = FontWeights.Normal,
                Foreground = System.Windows.Media.Brushes.White
            };
        }

        /// <summary>
        /// 展开节点时懒加载子项
        /// </summary>
        private void DocTree_Expanded(object sender, RoutedEventArgs e)
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
                    return; // 已加载
                }

                try
                {
                    var dirInfo = new DirectoryInfo(path);

                    // 子目录
                    foreach (var subDir in dirInfo.GetDirectories().OrderBy(d => d.Name))
                    {
                        try { node.Items.Add(CreateDirectoryNode(subDir)); }
                        catch { }
                    }

                    // 文件
                    foreach (var file in dirInfo.GetFiles().OrderBy(f => f.Name))
                    {
                        try { node.Items.Add(CreateFileNode(file)); }
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
        /// 判断节点是否是目录
        /// </summary>
        private static bool IsDirectoryNode(TreeViewItem node)
        {
            return node.Tag is string path && Directory.Exists(path);
        }

        /// <summary>
        /// 递归获取节点的完整路径（用于显示）
        /// </summary>
        private string GetNodeRelativePath(TreeViewItem node)
        {
            var parts = new List<string>();
            var current = node;
            while (current != null)
            {
                if (current.Tag is string path)
                {
                    var name = Path.GetFileName(path);
                    if (!string.IsNullOrEmpty(name))
                        parts.Insert(0, name);
                }
                current = current.Parent as TreeViewItem;
            }
            return string.Join(" / ", parts);
        }

        #endregion

        #region 搜索

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var query = SearchBox.Text.Trim();
            SearchPlaceholder.Visibility = string.IsNullOrEmpty(query) ? Visibility.Visible : Visibility.Collapsed;

            if (string.IsNullOrEmpty(query))
            {
                // 恢复完整树
                _isSearching = false;
                LoadTree();
                return;
            }

            _isSearching = true;
            DocTree.Items.Clear();

            try
            {
                SearchDirectory(new DirectoryInfo(_rootPath), query, "");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"搜索失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 递归搜索目录
        /// </summary>
        private void SearchDirectory(DirectoryInfo dirInfo, string query, string relativePath)
        {
            // 搜索当前目录下的文件
            foreach (var file in dirInfo.GetFiles())
            {
                if (file.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    var node = CreateFileNode(file);
                    if (!string.IsNullOrEmpty(relativePath))
                        node.Header = $"📄 {file.Name}    ({relativePath})";
                    DocTree.Items.Add(node);
                }
            }

            // 递归搜索子目录
            foreach (var subDir in dirInfo.GetDirectories())
            {
                try
                {
                    var subRelative = string.IsNullOrEmpty(relativePath) ? subDir.Name : $"{relativePath} / {subDir.Name}";
                    SearchDirectory(subDir, query, subRelative);
                }
                catch { }
            }
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

        #region 拖放（从 Windows 拖入文件/文件夹 → 移动到目标目录）

        /// <summary>
        /// Win32 文件拖放下事件（绕过 WorkerW z-order 限制）
        /// </summary>
        protected override void OnFileDrop(string[] files)
        {
            // 没有悬停在具体节点上 → 移动到根目录
            if (!string.IsNullOrEmpty(_rootPath))
            {
                MoveFilesToTarget(files, _rootPath);
            }
        }

        /// <summary>
        /// TreeViewItem 级别 DragOver（高亮目标文件夹）
        /// </summary>
        private void TreeViewItem_DragOver(object sender, System.Windows.DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
                return;

            var node = sender as TreeViewItem;
            if (node == null || !IsDirectoryNode(node))
            {
                e.Effects = System.Windows.DragDropEffects.None;
                return;
            }

            // 高亮目标
            if (_dragTarget != node)
            {
                ClearDragHighlight();
                _dragTarget = node;
                _dragTargetOriginalBg = node.Background;
                node.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x44, 0x00, 0x78, 0xD4));
            }

            e.Effects = System.Windows.DragDropEffects.Move;
            e.Handled = true;
        }

        /// <summary>
        /// TreeViewItem DragLeave（取消高亮）
        /// </summary>
        private void TreeViewItem_DragLeave(object sender, System.Windows.DragEventArgs e)
        {
            var node = sender as TreeViewItem;
            if (node == _dragTarget)
            {
                ClearDragHighlight();
            }
        }

        /// <summary>
        /// TreeViewItem Drop（移动到目标文件夹）
        /// </summary>
        private void TreeViewItem_Drop(object sender, System.Windows.DragEventArgs e)
        {
            ClearDragHighlight();

            if (!e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
                return;

            var node = sender as TreeViewItem;
            if (node == null || !IsDirectoryNode(node))
                return;

            var targetDir = node.Tag as string;
            if (string.IsNullOrEmpty(targetDir))
                return;

            var files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
            MoveFilesToTarget(files, targetDir);

            // 展开目标节点显示移入的文件
            node.IsExpanded = true;
            e.Handled = true;
        }

        /// <summary>
        /// 移动文件/文件夹到目标目录
        /// </summary>
        private void MoveFilesToTarget(string[] sources, string targetDir)
        {
            foreach (var source in sources)
            {
                try
                {
                    var name = Path.GetFileName(source);
                    var destPath = Path.Combine(targetDir, name);

                    if (File.Exists(source))
                    {
                        // 文件
                        if (File.Exists(destPath))
                        {
                            var result = System.Windows.MessageBox.Show(
                                $"目标目录已存在同名文件：\n\n{name}\n\n覆盖？",
                                "文件冲突", System.Windows.MessageBoxButton.YesNoCancel, System.Windows.MessageBoxImage.Question);

                            if (result == System.Windows.MessageBoxResult.Yes)
                            {
                                File.Delete(destPath);
                                File.Move(source, destPath);
                            }
                            else if (result == System.Windows.MessageBoxResult.No)
                            {
                                // 重命名：添加 (2) 后缀
                                destPath = GetUniqueFilePath(targetDir, name);
                                File.Move(source, destPath);
                            }
                            // Cancel: 跳过
                        }
                        else
                        {
                            File.Move(source, destPath);
                        }
                    }
                    else if (Directory.Exists(source))
                    {
                        // 文件夹
                        if (Directory.Exists(destPath))
                        {
                            var result = System.Windows.MessageBox.Show(
                                $"目标目录已存在同名文件夹：\n\n{name}\n\n合并？",
                                "文件夹冲突", System.Windows.MessageBoxButton.YesNoCancel, System.Windows.MessageBoxImage.Question);

                            if (result == System.Windows.MessageBoxResult.Yes)
                            {
                                MoveDirectory(source, destPath);
                            }
                            else if (result == System.Windows.MessageBoxResult.No)
                            {
                                destPath = GetUniqueDirPath(targetDir, name);
                                Directory.Move(source, destPath);
                            }
                        }
                        else
                        {
                            Directory.Move(source, destPath);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"移动失败: {ex.Message}", "错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }

            // 刷新树
            LoadTree();
        }

        /// <summary>
        /// 递归合并移动目录
        /// </summary>
        private static void MoveDirectory(string source, string dest)
        {
            if (!Directory.Exists(dest))
                Directory.CreateDirectory(dest);

            foreach (var file in Directory.GetFiles(source))
            {
                var destFile = Path.Combine(dest, Path.GetFileName(file));
                if (File.Exists(destFile))
                    File.Delete(destFile);
                File.Move(file, destFile);
            }

            foreach (var dir in Directory.GetDirectories(source))
            {
                var destSub = Path.Combine(dest, Path.GetFileName(dir));
                MoveDirectory(dir, destSub);
            }

            Directory.Delete(source, true);
        }

        /// <summary>
        /// 获取不重复的文件路径
        /// </summary>
        private static string GetUniqueFilePath(string dir, string fileName)
        {
            var name = Path.GetFileNameWithoutExtension(fileName);
            var ext = Path.GetExtension(fileName);
            var counter = 2;
            string destPath;
            do
            {
                destPath = Path.Combine(dir, $"{name} ({counter}){ext}");
                counter++;
            } while (File.Exists(destPath));
            return destPath;
        }

        /// <summary>
        /// 获取不重复的目录路径
        /// </summary>
        private static string GetUniqueDirPath(string dir, string dirName)
        {
            var counter = 2;
            string destPath;
            do
            {
                destPath = Path.Combine(dir, $"{dirName} ({counter})");
                counter++;
            } while (Directory.Exists(destPath));
            return destPath;
        }

        private void ClearDragHighlight()
        {
            if (_dragTarget != null)
            {
                _dragTarget.Background = _dragTargetOriginalBg ?? System.Windows.Media.Brushes.Transparent;
                _dragTarget = null;
                _dragTargetOriginalBg = null;
            }
        }

        #endregion

        #region 双击打开

        private void DocTree_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (_isSearching)
            {
                // 搜索模式下双击文件 → 打开
                if (DocTree.SelectedItem is TreeViewItem node && node.Tag is string path && File.Exists(path))
                {
                    OpenFile(path);
                }
                return;
            }

            if (DocTree.SelectedItem is TreeViewItem selected && selected.Tag is string selectedPath)
            {
                if (File.Exists(selectedPath))
                {
                    OpenFile(selectedPath);
                }
                // 目录：展开/折叠由 TreeView 自身处理
            }
        }

        private static void OpenFile(string filePath)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = filePath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"无法打开文件: {ex.Message}", "错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        #endregion

        #region 右键菜单（节点级）

        /// <summary>
        /// 右键点击节点时动态生成菜单
        /// </summary>
        protected override void OnPreviewMouseRightButtonDown(MouseButtonEventArgs e)
        {
            base.OnPreviewMouseRightButtonDown(e);

            // 找到点击的 TreeViewItem
            var source = e.OriginalSource as DependencyObject;
            var node = FindParentTreeViewItem(source);

            if (node != null)
            {
                node.IsSelected = true;
                e.Handled = true;

                // 延迟到 MouseRightButtonUp 显示菜单
                node.ContextMenu = IsDirectoryNode(node) ? CreateFolderContextMenu(node) : CreateFileContextMenu(node);
            }
        }

        /// <summary>
        /// 创建文件夹右键菜单
        /// </summary>
        private System.Windows.Controls.ContextMenu CreateFolderContextMenu(TreeViewItem node)
        {
            var menu = new System.Windows.Controls.ContextMenu();

            var newMd = new System.Windows.Controls.MenuItem { Header = "新建 Markdown" };
            newMd.Click += (s, e) => CreateNewFile(node, ".md", "新建 Markdown");
            menu.Items.Add(newMd);

            var newTxt = new System.Windows.Controls.MenuItem { Header = "新建文本文档" };
            newTxt.Click += (s, e) => CreateNewFile(node, ".txt", "新建文本文档");
            menu.Items.Add(newTxt);

            var newFolder = new System.Windows.Controls.MenuItem { Header = "新建文件夹" };
            newFolder.Click += (s, e) => CreateNewFolder(node);
            menu.Items.Add(newFolder);

            menu.Items.Add(new System.Windows.Controls.Separator());

            var openExplorer = new System.Windows.Controls.MenuItem { Header = "在资源管理器中打开" };
            openExplorer.Click += (s, e) => OpenInExplorer(node.Tag as string);
            menu.Items.Add(openExplorer);

            var refresh = new System.Windows.Controls.MenuItem { Header = "刷新" };
            refresh.Click += (s, e) => RefreshNode(node);
            menu.Items.Add(refresh);

            var rename = new System.Windows.Controls.MenuItem { Header = "重命名" };
            rename.Click += (s, e) => RenameItem(node);
            menu.Items.Add(rename);

            menu.Items.Add(new System.Windows.Controls.Separator());

            var delete = new System.Windows.Controls.MenuItem { Header = "删除" };
            delete.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0xFF, 0xFF, 0x6B, 0x6B));
            delete.Click += (s, e) => DeleteItem(node);
            menu.Items.Add(delete);

            return menu;
        }

        /// <summary>
        /// 创建文件右键菜单
        /// </summary>
        private System.Windows.Controls.ContextMenu CreateFileContextMenu(TreeViewItem node)
        {
            var menu = new System.Windows.Controls.ContextMenu();

            var open = new System.Windows.Controls.MenuItem { Header = "打开" };
            open.Click += (s, e) => { if (node.Tag is string p) OpenFile(p); };
            menu.Items.Add(open);

            var openFolder = new System.Windows.Controls.MenuItem { Header = "打开所在文件夹" };
            openFolder.Click += (s, e) => OpenInExplorer(Path.GetDirectoryName(node.Tag as string));
            menu.Items.Add(openFolder);

            var copyPath = new System.Windows.Controls.MenuItem { Header = "复制路径" };
            copyPath.Click += (s, e) => { if (node.Tag is string p) System.Windows.Clipboard.SetText(p); };
            menu.Items.Add(copyPath);

            menu.Items.Add(new System.Windows.Controls.Separator());

            var rename = new System.Windows.Controls.MenuItem { Header = "重命名" };
            rename.Click += (s, e) => RenameItem(node);
            menu.Items.Add(rename);

            menu.Items.Add(new System.Windows.Controls.Separator());

            var delete = new System.Windows.Controls.MenuItem { Header = "删除" };
            delete.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0xFF, 0xFF, 0x6B, 0x6B));
            delete.Click += (s, e) => DeleteItem(node);
            menu.Items.Add(delete);

            return menu;
        }

        /// <summary>
        /// 向上查找 TreeViewItem
        /// </summary>
        private static TreeViewItem? FindParentTreeViewItem(DependencyObject? source)
        {
            while (source != null)
            {
                if (source is TreeViewItem tvi)
                    return tvi;
                source = VisualTreeHelper.GetParent(source);
            }
            return null;
        }

        #endregion

        #region 新建

        /// <summary>
        /// 新建文件
        /// </summary>
        private void CreateNewFile(TreeViewItem parentNode, string extension, string title)
        {
            var dirPath = parentNode.Tag as string;
            if (string.IsNullOrEmpty(dirPath) || !Directory.Exists(dirPath))
                return;

            var dialog = new InputDialog(title, "文件名：", $"未命名{extension}");
            dialog.Owner = Window.GetWindow(this);
            if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.Result))
                return;

            var fileName = dialog.Result.Trim();
            if (!fileName.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                fileName += extension;

            var filePath = Path.Combine(dirPath, fileName);

            try
            {
                if (File.Exists(filePath))
                {
                    System.Windows.MessageBox.Show("同名文件已存在。", "提示", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }

                File.WriteAllText(filePath, "");
                RefreshNode(parentNode);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"创建失败: {ex.Message}", "错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 新建文件夹
        /// </summary>
        private void CreateNewFolder(TreeViewItem parentNode)
        {
            var dirPath = parentNode.Tag as string;
            if (string.IsNullOrEmpty(dirPath) || !Directory.Exists(dirPath))
                return;

            var dialog = new InputDialog("新建文件夹", "文件夹名称：", "新建文件夹");
            dialog.Owner = Window.GetWindow(this);
            if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.Result))
                return;

            var folderName = dialog.Result.Trim();
            var folderPath = Path.Combine(dirPath, folderName);

            try
            {
                if (Directory.Exists(folderPath))
                {
                    System.Windows.MessageBox.Show("同名文件夹已存在。", "提示", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }

                Directory.CreateDirectory(folderPath);
                RefreshNode(parentNode);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"创建失败: {ex.Message}", "错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        #endregion

        #region 重命名

        private void RenameItem(TreeViewItem node)
        {
            var oldPath = node.Tag as string;
            if (string.IsNullOrEmpty(oldPath))
                return;

            var oldName = Path.GetFileName(oldPath);
            var isDir = Directory.Exists(oldPath);

            var dialog = new InputDialog("重命名", "新名称：", oldName);
            dialog.Owner = Window.GetWindow(this);
            if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.Result))
                return;

            var newName = dialog.Result.Trim();
            if (newName == oldName)
                return;

            var parentDir = Path.GetDirectoryName(oldPath);
            if (string.IsNullOrEmpty(parentDir))
                return;

            var newPath = Path.Combine(parentDir, newName);

            try
            {
                if (isDir)
                {
                    if (Directory.Exists(newPath))
                    {
                        System.Windows.MessageBox.Show("同名文件夹已存在。", "提示", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                        return;
                    }
                    Directory.Move(oldPath, newPath);
                }
                else
                {
                    if (File.Exists(newPath))
                    {
                        System.Windows.MessageBox.Show("同名文件已存在。", "提示", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                        return;
                    }
                    File.Move(oldPath, newPath);
                }

                // 刷新父节点
                var parentNode = node.Parent as TreeViewItem;
                if (parentNode != null)
                    RefreshNode(parentNode);
                else
                    LoadTree();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"重命名失败: {ex.Message}", "错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        #endregion

        #region 删除

        private void DeleteItem(TreeViewItem node)
        {
            var path = node.Tag as string;
            if (string.IsNullOrEmpty(path))
                return;

            var name = Path.GetFileName(path);
            var isDir = Directory.Exists(path);

            var type = isDir ? "文件夹" : "文件";
            var result = System.Windows.MessageBox.Show(
                $"确定要删除这个{type}吗？\n\n{name}",
                "确认删除", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);

            if (result != System.Windows.MessageBoxResult.Yes)
                return;

            try
            {
                if (isDir)
                    Directory.Delete(path, true);
                else
                    File.Delete(path);

                // 从树中移除节点
                var parentNode = node.Parent as TreeViewItem;
                if (parentNode != null)
                    parentNode.Items.Remove(node);
                else
                    DocTree.Items.Remove(node);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"删除失败: {ex.Message}", "错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        #endregion

        #region 辅助操作

        private static void OpenInExplorer(string? path)
        {
            if (string.IsNullOrEmpty(path))
                return;

            try
            {
                if (Directory.Exists(path))
                    Process.Start("explorer.exe", path);
                else if (File.Exists(path))
                    Process.Start("explorer.exe", $"/select,\"{path}\"");
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"无法打开资源管理器: {ex.Message}", "错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 刷新节点（重新加载子项）
        /// </summary>
        private void RefreshNode(TreeViewItem node)
        {
            if (node.Tag is string path && Directory.Exists(path))
            {
                node.Items.Clear();
                try
                {
                    var dirInfo = new DirectoryInfo(path);

                    foreach (var subDir in dirInfo.GetDirectories().OrderBy(d => d.Name))
                    {
                        try { node.Items.Add(CreateDirectoryNode(subDir)); }
                        catch { }
                    }

                    foreach (var file in dirInfo.GetFiles().OrderBy(f => f.Name))
                    {
                        try { node.Items.Add(CreateFileNode(file)); }
                        catch { }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"刷新节点失败: {ex.Message}");
                }

                node.IsExpanded = true;
            }
        }

        #endregion

        #region UI 状态

        private void UpdateHintVisibility()
        {
            HintText.Visibility = string.IsNullOrEmpty(_rootPath) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdateLockButton()
        {
            LockButton.Content = IsLocked ? "🔒" : "🔓";
            LockButton.ToolTip = IsLocked ? "解锁" : "锁定";
            LockMenuItem.IsChecked = IsLocked;
            LockMenuItem.Header = IsLocked ? "解锁" : "锁定";
        }

        #endregion

        #region 窗口右键菜单事件

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            if (!_isSearching)
                LoadTree();
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new DocSettingsWindow(_rootPath);
            dialog.Owner = Window.GetWindow(this);
            if (dialog.ShowDialog() == true)
            {
                var newPath = dialog.SelectedPath;
                if (!string.IsNullOrEmpty(newPath) && Directory.Exists(newPath) && newPath != _rootPath)
                {
                    _rootPath = newPath;
                    SaveRootPath();
                    _isSearching = false;
                    SearchBox.Text = "";
                    LoadTree();
                    UpdateHintVisibility();
                }
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e) => RequestClose();

        private void Lock_Click(object sender, RoutedEventArgs e)
        {
            ToggleLock();
            UpdateLockButton();
        }

        #endregion
    }
}
