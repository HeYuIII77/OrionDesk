using System.IO;
using System.Windows;

namespace OrionDesk.UI.Windows
{
    /// <summary>
    /// 文档中心设置窗口
    /// </summary>
    public partial class DocSettingsWindow : Window
    {
        public string SelectedPath { get; private set; } = "";
        private readonly string _browseDescription;

        public DocSettingsWindow(string currentPath, string title = "文档中心设置", string pathLabel = "文档目录")
        {
            InitializeComponent();
            Topmost = true;
            Title = title;
            TitleText.Text = title;
            PathLabel.Text = pathLabel;
            _browseDescription = $"选择{pathLabel}";
            PathBox.Text = currentPath ?? "";
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = _browseDescription,
                ShowNewFolderButton = true
            };

            // 如果已有路径，设为初始目录
            if (Directory.Exists(PathBox.Text))
                dialog.InitialDirectory = PathBox.Text;

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                PathBox.Text = dialog.SelectedPath;
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            var path = PathBox.Text.Trim();

            if (string.IsNullOrEmpty(path))
            {
                System.Windows.MessageBox.Show($"请选择{PathLabel.Text}。", "提示", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            if (!Directory.Exists(path))
            {
                System.Windows.MessageBox.Show("目录不存在，请选择有效的目录。", "提示", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            SelectedPath = path;
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
