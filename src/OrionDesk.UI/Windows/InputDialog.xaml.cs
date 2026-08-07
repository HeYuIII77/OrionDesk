using System.Windows;

namespace OrionDesk.UI.Windows
{
    /// <summary>
    /// 通用输入对话框
    /// </summary>
    public partial class InputDialog : Window
    {
        public string? Result { get; private set; }

        public InputDialog(string title, string prompt, string defaultValue = "")
        {
            InitializeComponent();
            TitleText.Text = title;
            PromptText.Text = prompt;
            InputBox.Text = defaultValue;
            InputBox.SelectAll();
            Loaded += (s, e) => InputBox.Focus();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            Result = InputBox.Text;
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
