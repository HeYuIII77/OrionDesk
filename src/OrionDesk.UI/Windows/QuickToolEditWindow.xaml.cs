using System;
using System.Collections.ObjectModel;
using System.Windows;
using OrionDesk.BLL.Models;
using OrionDesk.UI.Controls;

using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace OrionDesk.UI.Windows
{
    /// <summary>
    /// 快捷工具编辑对话框 - 添加/编辑快捷工具
    /// </summary>
    public partial class QuickToolEditWindow : Window
    {
        /// <summary>
        /// 编辑结果（对话框返回 true 时有效）
        /// </summary>
        public QuickToolItem? Result { get; private set; }

        private readonly QuickToolItem? _editingItem;

        /// <summary>
        /// 新建模式
        /// </summary>
        public QuickToolEditWindow()
        {
            InitializeComponent();
            Topmost = true;
            InitTypeCombo();
            DialogTitle.Text = "添加快捷工具";
        }

        /// <summary>
        /// 编辑模式
        /// </summary>
        public QuickToolEditWindow(QuickToolItem item) : this()
        {
            _editingItem = item;
            DialogTitle.Text = "编辑快捷工具";

            // 填充表单
            NameBox.Text = item.Name;
            IconBox.Text = item.Icon;
            PathBox.Text = item.Path;
            ArgsBox.Text = item.Arguments;
            AdminCheckBox.IsChecked = item.RunAsAdmin;

            // 选中对应类型
            TypeCombo.SelectedTag = item.Type.ToString();
        }

        /// <summary>
        /// 初始化类型下拉框
        /// </summary>
        private void InitTypeCombo()
        {
            TypeCombo.ItemsSource = new ObservableCollection<DarkComboBoxItem>
            {
                new() { DisplayText = "应用程序", Tag = "App" },
                new() { DisplayText = "文件夹", Tag = "Folder" },
                new() { DisplayText = "URL", Tag = "Url" },
                new() { DisplayText = "Shell 命令", Tag = "Shell" }
            };
            TypeCombo.SelectedTag = "App";

            // 监听选项变更
            TypeCombo.SelectionChanged += (s, e) => UpdateTypeVisibility();
            UpdateTypeVisibility();
        }

        /// <summary>
        /// 根据选中类型显示/隐藏相关字段
        /// </summary>
        private void UpdateTypeVisibility()
        {
            if (PathLabel == null) return;

            var selectedType = TypeCombo.SelectedTag ?? "App";

            switch (selectedType)
            {
                case "App":
                    PathLabel.Text = "程序路径";
                    ArgsLabel.Visibility = Visibility.Visible;
                    ArgsBox.Visibility = Visibility.Visible;
                    BrowseButton.Visibility = Visibility.Visible;
                    AdminCheckBox.Visibility = Visibility.Visible;
                    break;

                case "Folder":
                    PathLabel.Text = "文件夹路径";
                    ArgsLabel.Visibility = Visibility.Collapsed;
                    ArgsBox.Visibility = Visibility.Collapsed;
                    BrowseButton.Visibility = Visibility.Visible;
                    AdminCheckBox.Visibility = Visibility.Collapsed;
                    break;

                case "Url":
                    PathLabel.Text = "URL 地址";
                    ArgsLabel.Visibility = Visibility.Collapsed;
                    ArgsBox.Visibility = Visibility.Collapsed;
                    BrowseButton.Visibility = Visibility.Collapsed;
                    AdminCheckBox.Visibility = Visibility.Collapsed;
                    break;

                case "Shell":
                    PathLabel.Text = "Shell 命令";
                    ArgsLabel.Visibility = Visibility.Collapsed;
                    ArgsBox.Visibility = Visibility.Collapsed;
                    BrowseButton.Visibility = Visibility.Collapsed;
                    AdminCheckBox.Visibility = Visibility.Collapsed;
                    break;
            }
        }

        /// <summary>
        /// 浏览按钮
        /// </summary>
        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedType = TypeCombo.SelectedTag ?? "App";

            if (selectedType == "Folder")
            {
                var dialog = new System.Windows.Forms.FolderBrowserDialog();
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    PathBox.Text = dialog.SelectedPath;
            }
            else
            {
                var dialog = new OpenFileDialog
                {
                    Title = "选择程序",
                    Filter = "可执行文件 (*.exe;*.msc)|*.exe;*.msc|所有文件 (*.*)|*.*"
                };
                if (dialog.ShowDialog() == true)
                    PathBox.Text = dialog.FileName;
            }
        }

        /// <summary>
        /// 确定按钮
        /// </summary>
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            // 验证
            if (string.IsNullOrWhiteSpace(NameBox.Text))
            {
                System.Windows.MessageBox.Show("请输入名称", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                NameBox.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(PathBox.Text))
            {
                System.Windows.MessageBox.Show("请输入路径", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                PathBox.Focus();
                return;
            }

            // 构建结果
            var selectedType = TypeCombo.SelectedTag ?? "App";
            var toolType = selectedType switch
            {
                "App" => QuickToolType.App,
                "Folder" => QuickToolType.Folder,
                "Url" => QuickToolType.Url,
                "Shell" => QuickToolType.Shell,
                _ => QuickToolType.App
            };

            // 编辑模式：保留原 ID 和 IsPreset 标记
            Result = _editingItem != null
                ? new QuickToolItem
                {
                    Id = _editingItem.Id,
                    IsPreset = _editingItem.IsPreset,
                    Category = _editingItem.Category,
                    Name = NameBox.Text.Trim(),
                    Icon = IconBox.Text.Trim(),
                    Path = PathBox.Text.Trim(),
                    Arguments = ArgsBox.Text.Trim(),
                    Type = toolType,
                    RunAsAdmin = AdminCheckBox.IsChecked == true
                }
                : new QuickToolItem
                {
                    Name = NameBox.Text.Trim(),
                    Icon = IconBox.Text.Trim(),
                    Path = PathBox.Text.Trim(),
                    Arguments = ArgsBox.Text.Trim(),
                    Type = toolType,
                    RunAsAdmin = AdminCheckBox.IsChecked == true,
                    Category = "custom"
                };

            DialogResult = true;
        }

        /// <summary>
        /// 取消按钮
        /// </summary>
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
