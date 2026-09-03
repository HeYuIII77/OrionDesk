using System.Linq;
using System.Windows;

namespace OrionDesk.UI.Windows
{
    /// <summary>
    /// CMD 启动器设置对话框
    /// </summary>
    public partial class CmdLauncherSettingsWindow : Window
    {
        public string? ResultName { get; private set; }
        public string? ResultIcon { get; private set; }
        public string? ResultCommand { get; private set; }
        public string? ResultWorkDir { get; private set; }

        public CmdLauncherSettingsWindow(string name, string icon, string command, string workDir)
        {
            InitializeComponent();
            Topmost = true;
            NameBox.Text = name;
            IconBox.Text = icon;
            CommandBox.Text = command;
            WorkDirBox.Text = workDir;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NameBox.Text))
            {
                System.Windows.MessageBox.Show("请输入显示名称", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                NameBox.Focus();
                return;
            }
            // 检查是否至少有一条非空命令
            var hasCommand = CommandBox.Text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
                .Any(l => !string.IsNullOrWhiteSpace(l));
            if (!hasCommand)
            {
                System.Windows.MessageBox.Show("请输入至少一条命令", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                CommandBox.Focus();
                return;
            }

            ResultName = NameBox.Text.Trim();
            ResultIcon = IconBox.Text.Trim();
            ResultCommand = CommandBox.Text.Trim();
            ResultWorkDir = WorkDirBox.Text.Trim();
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
