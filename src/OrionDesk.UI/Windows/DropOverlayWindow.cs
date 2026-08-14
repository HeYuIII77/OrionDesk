using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;

namespace OrionDesk.UI.Windows
{
    /// <summary>
    /// 透明覆盖窗口，作为文件拖放代理。
    /// 解决 WorkerW 子窗口无法接收外部拖放的问题：
    /// 覆盖窗口置顶显示，接收 WM_DROPFILES 后转发给目标组件。
    /// </summary>
    internal class DropOverlayWindow : IDisposable
    {
        #region Win32 API

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hwnd, int index);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

        [DllImport("shell32.dll")]
        private static extern void DragAcceptFiles(IntPtr hWnd, bool fAccept);

        [DllImport("shell32.dll")]
        private static extern uint DragQueryFile(IntPtr hDrop, uint iFile, StringBuilder? lpszFile, uint cch);

        [DllImport("shell32.dll")]
        private static extern void DragFinish(IntPtr hDrop);

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const uint SWP_HIDEWINDOW = 0x0080;
        private const uint WM_DROPFILES = 0x0233;

        #endregion

        private readonly Window _overlay;
        private readonly Window _target;
        private HwndSource? _hwndSource;
        private bool _disposed;

        /// <summary>
        /// 文件拖放事件
        /// </summary>
        public event Action<string[]>? FilesDropped;

        public DropOverlayWindow(Window target)
        {
            _target = target;

            // 创建透明覆盖窗口
            _overlay = new Window
            {
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = System.Windows.Media.Brushes.Transparent,
                ShowInTaskbar = false,
                Topmost = true,
                ResizeMode = ResizeMode.NoResize,
                Width = target.Width,
                Height = target.Height,
                Left = target.Left,
                Top = target.Top
            };

            // 监听目标窗口的位置和大小变化
            target.LocationChanged += (s, e) => UpdatePosition();
            target.SizeChanged += (s, e) => UpdatePosition();
            target.IsVisibleChanged += (s, e) => UpdateVisibility();
            target.Closed += (s, e) => Dispose();

            _overlay.Loaded += OnOverlayLoaded;
        }

        /// <summary>
        /// 显示覆盖窗口
        /// </summary>
        public void Show()
        {
            UpdatePosition();
            _overlay.Show();
        }

        /// <summary>
        /// 隐藏覆盖窗口
        /// </summary>
        public void Hide()
        {
            _overlay.Hide();
        }

        /// <summary>
        /// 同步覆盖窗口位置到目标窗口
        /// </summary>
        private void UpdatePosition()
        {
            _overlay.Left = _target.Left;
            _overlay.Top = _target.Top;
            _overlay.Width = _target.Width;
            _overlay.Height = _target.Height;
        }

        /// <summary>
        /// 根据目标窗口可见性同步
        /// </summary>
        private void UpdateVisibility()
        {
            if (_target.IsVisible)
                Show();
            else
                Hide();
        }

        private void OnOverlayLoaded(object sender, RoutedEventArgs e)
        {
            var hwnd = new WindowInteropHelper(_overlay).Handle;

            // 设置窗口样式：工具窗口 + 穿透点击 + 不激活
            var exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_TOOLWINDOW | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE);

            // 注册文件拖放
            DragAcceptFiles(hwnd, true);

            // 注册 WndProc 钩子
            _hwndSource = HwndSource.FromHwnd(hwnd);
            _hwndSource?.AddHook(WndProc);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if ((uint)msg == WM_DROPFILES)
            {
                var hDrop = wParam;
                var fileCount = DragQueryFile(hDrop, 0xFFFFFFFF, null, 0);
                var files = new List<string>();

                for (uint i = 0; i < fileCount; i++)
                {
                    var sb = new StringBuilder(260);
                    DragQueryFile(hDrop, i, sb, (uint)sb.Capacity);
                    files.Add(sb.ToString());
                }

                DragFinish(hDrop);

                if (files.Count > 0)
                {
                    FilesDropped?.Invoke(files.ToArray());
                }

                handled = true;
            }

            return IntPtr.Zero;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _hwndSource?.Dispose();
            try { _overlay.Close(); } catch { }
        }
    }
}
