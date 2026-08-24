using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using OrionDesk.BLL.Models;
using OrionDesk.BLL.Services;

namespace OrionDesk.UI.Windows
{
    /// <summary>
    /// 组件窗口基类
    /// 处理桌面层级、拖拽移动、悬浮效果等通用逻辑
    /// </summary>
    public abstract class BaseWidgetWindow : Window
    {
        #region Win32 API

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindowEx(IntPtr parentHandle, IntPtr childAfter, string? className, string? windowTitle);

        [DllImport("user32.dll")]
        private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern int SendMessageTimeout(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam, uint fuFlags, uint uTimeout, out IntPtr lpdwResult);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern IntPtr GetShellWindow();

        [DllImport("dwmapi.dll")]
        private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref MARGINS pMarInset);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hwnd, int index);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetParent(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern uint RegisterWindowMessage(string lpString);

        [DllImport("shell32.dll")]
        private static extern void DragAcceptFiles(IntPtr hWnd, bool fAccept);

        [DllImport("shell32.dll")]
        private static extern uint DragQueryFile(IntPtr hDrop, uint iFile, System.Text.StringBuilder? lpszFile, uint cch);

        [DllImport("shell32.dll")]
        private static extern void DragFinish(IntPtr hDrop);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct MARGINS
        {
            public int leftWidth;
            public int rightWidth;
            public int topHeight;
            public int bottomHeight;
        }

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private static readonly IntPtr HWND_BOTTOM = new IntPtr(1);

        private const uint WM_SPAWN_WORKERW = 0x052C;
        private const uint WM_DROPFILES = 0x0233;

        // Explorer 重启检测
        private static uint _taskbarCreatedMsg;
        private static readonly List<Action> _explorerRestartCallbacks = new();
        private static bool _hookRegistered = false;

        #endregion

        #region 字段和属性

        // 全局组件注册表（用于吸附对齐）
        private static readonly List<BaseWidgetWindow> _allWidgets = new();
        private static readonly object _allWidgetsLock = new();
        private const int SnapThreshold = 8; // 吸附距离（像素）

        /// <summary>
        /// 注册组件到全局列表（MainWindow 调用）
        /// </summary>
        public static void RegisterWidget(BaseWidgetWindow widget)
        {
            lock (_allWidgetsLock)
            {
                if (!_allWidgets.Contains(widget))
                    _allWidgets.Add(widget);
            }
        }

        /// <summary>
        /// 从全局列表注销组件
        /// </summary>
        public static void UnregisterWidget(BaseWidgetWindow widget)
        {
            lock (_allWidgetsLock)
            {
                _allWidgets.Remove(widget);
            }
        }

        // 组件配置
        protected WidgetConfig _config;
        protected WidgetManager _widgetManager;

        // 窗口状态
        private bool _isMouseOver = false;
        private bool _isDragging = false;
        private System.Windows.Point _dragStartPoint;
        private double _normalOpacity;
        private double _hoverOpacity;

        // 锁定状态
        private bool _isLocked = false;
        public bool IsLocked
        {
            get => _isLocked;
            set
            {
                _isLocked = value;
                _config.Settings["isLocked"] = value;
                UpdateLockState();
                SavePosition();
            }
        }

        // 桌面窗口句柄
        private IntPtr _workerWHandle = IntPtr.Zero;
        private IntPtr _desktopHandle = IntPtr.Zero;

        // 防抖定时器
        private DispatcherTimer? _saveTimer;

        // WorkerW 有效性检查定时器
        private DispatcherTimer? _workerWCheckTimer;

        // 调整大小吸附防重入
        private bool _isResizing = false;

        // 应用退出标志（MainWindow 关闭前设置，允许组件关闭）
        internal static bool IsAppClosing { get; set; } = false;

        // 单个组件主动关闭标志
        private bool _requestingClose = false;

        // Win32 文件拖放（不受窗口 z-order 限制）
        /// <summary>
        /// 子类设为 true 以启用 Win32 WM_DROPFILES 拖放支持。
        /// 解决 WorkerW 子窗口无法接收 OLE DragDrop 的问题。
        /// </summary>
        protected bool AcceptFileDrop { get; set; } = false;

        /// <summary>
        /// 子类重写此方法接收文件拖放。参数为拖入的文件路径列表。
        /// </summary>
        protected virtual void OnFileDrop(string[] files) { }

        #endregion

        #region 构造函数

        protected BaseWidgetWindow(WidgetConfig config, WidgetManager widgetManager)
        {
            _config = config;
            _widgetManager = widgetManager;

            // 注册 Explorer 重启检测（只需注册一次）
            if (!_hookRegistered)
            {
                _taskbarCreatedMsg = RegisterWindowMessage("TaskbarCreated");
                _hookRegistered = true;
            }

            // 设置窗口属性
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = System.Windows.Media.Brushes.Transparent;
            ShowInTaskbar = false;
            ResizeMode = ResizeMode.CanResizeWithGrip;
            Topmost = false; // 我们自己控制层级

            // 不使用 DropShadowEffect（会导致窗口视觉偏移）
            // 阴影由各组件 XAML 中的 Border 自行处理

            // 初始位置和大小
            Left = config.Position.X;
            Top = config.Position.Y;
            Width = config.Position.Width;
            Height = config.Position.Height;

            // 透明度
            _normalOpacity = config.NormalOpacity;
            _hoverOpacity = config.HoverOpacity;
            Opacity = _normalOpacity;

            // 事件处理
            Loaded += OnWindowLoaded;
            LocationChanged += OnLocationChanged;
            SizeChanged += OnSizeChanged;
            MouseEnter += OnMouseEnter;
            MouseLeave += OnMouseLeave;
            MouseLeftButtonDown += OnMouseLeftButtonDown;
            MouseLeftButtonUp += OnMouseLeftButtonUp;
            MouseMove += OnMouseMove;

            // 防抖保存定时器
            _saveTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _saveTimer.Tick += (s, e) =>
            {
                _saveTimer.Stop();
                SavePosition();
            };

            // WorkerW 有效性检查定时器（每 30 秒检查一次）
            _workerWCheckTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(30)
            };
            _workerWCheckTimer.Tick += (s, e) =>
            {
                bool needRestore = false;
                var hwnd = new WindowInteropHelper(this).Handle;

                // 检查1：WPF 层面是否可见（Visibility != Hidden 且 Opacity > 0）
                if (Visibility != Visibility.Visible || Opacity <= 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[WorkerW 检查] WPF 窗口不可见 (Vis={Visibility}, Opacity={Opacity})，尝试恢复");
                    needRestore = true;
                }
                // 检查2：Win32 层面是否可见
                else if (hwnd != IntPtr.Zero && !IsWindowVisible(hwnd))
                {
                    System.Diagnostics.Debug.WriteLine("[WorkerW 检查] Win32 窗口不可见，尝试恢复");
                    needRestore = true;
                }
                // 检查3：WorkerW 句柄是否有效
                else if (_workerWHandle == IntPtr.Zero)
                {
                    needRestore = true;
                }
                else if (!IsWindow(_workerWHandle))
                {
                    System.Diagnostics.Debug.WriteLine("[WorkerW 检查] 句柄已失效，重新设置桌面层级");
                    _workerWHandle = IntPtr.Zero;
                    needRestore = true;
                }
                // 检查4：父子关系是否还在
                else if (hwnd != IntPtr.Zero)
                {
                    var parent = GetParent(hwnd);
                    if (parent != _workerWHandle)
                    {
                        System.Diagnostics.Debug.WriteLine($"[WorkerW 检查] 父子关系断开 (parent={parent}, expected={_workerWHandle})，重新设置桌面层级");
                        needRestore = true;
                    }
                }

                if (needRestore)
                {
                    // 确保 WPF 层面可见
                    Visibility = Visibility.Visible;
                    if (Opacity <= 0) Opacity = _normalOpacity;
                    SetDesktopLevel();
                }
            };
            _workerWCheckTimer.Start();
        }

        #endregion

        #region 窗口生命周期

        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // 隐藏任务栏图标
                HideFromTaskbar();

                // 注册消息钩子（文件拖放 + Explorer 重启检测）
                var hwnd = new WindowInteropHelper(this).Handle;
                var source = HwndSource.FromHwnd(hwnd);
                source?.AddHook(WndProc);

                // 注册 Win32 文件拖放
                if (AcceptFileDrop)
                {
                    DragAcceptFiles(hwnd, true);
                }

                // 延迟设置桌面层级，确保窗口已完全初始化
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        SetDesktopLevel();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[桌面层级] 延迟设置失败: {ex.Message}");
                    }
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[桌面层级] 初始化失败: {ex.Message}");
            }
        }

        /// <summary>
        /// Win32 消息钩子，处理 WM_DROPFILES 和 Explorer 重启
        /// </summary>
        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            // Explorer 重启检测
            if (_taskbarCreatedMsg != 0 && (uint)msg == _taskbarCreatedMsg)
            {
                System.Diagnostics.Debug.WriteLine("[Explorer 重启] 检测到 Explorer 重启，刷新桌面层级");
                // WorkerW 句柄已失效，重置后重新设置桌面层级
                _workerWHandle = IntPtr.Zero;
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        SetDesktopLevel();
                        Show(); // 确保窗口可见
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[Explorer 重启] 刷新失败: {ex.Message}");
                    }
                }), System.Windows.Threading.DispatcherPriority.Loaded);
                return IntPtr.Zero;
            }

            // 文件拖放处理
            if ((uint)msg == WM_DROPFILES && AcceptFileDrop)
            {
                var hDrop = wParam;
                var fileCount = DragQueryFile(hDrop, 0xFFFFFFFF, null, 0);
                var files = new List<string>();

                for (uint i = 0; i < fileCount; i++)
                {
                    var sb = new System.Text.StringBuilder(260);
                    DragQueryFile(hDrop, i, sb, (uint)sb.Capacity);
                    files.Add(sb.ToString());
                }

                DragFinish(hDrop);

                if (files.Count > 0)
                {
                    OnFileDrop(files.ToArray());
                }

                handled = true;
            }

            return IntPtr.Zero;
        }


        /// <summary>
        /// 隐藏任务栏图标
        /// </summary>
        private void HideFromTaskbar()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            var extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TOOLWINDOW);
        }

        /// <summary>
        /// 设置窗口到桌面层级（在图标之上、应用程序之下）
        /// </summary>
        private void SetDesktopLevel()
        {
            try
            {
                // 验证 WorkerW 句柄是否仍然有效（Explorer 重启后会失效）
                if (_workerWHandle != IntPtr.Zero && !IsWindow(_workerWHandle))
                {
                    System.Diagnostics.Debug.WriteLine("[桌面层级] WorkerW 句柄已失效，重新查找");
                    _workerWHandle = IntPtr.Zero;
                }

                // 找到桌面WorkerW窗口（首次或句柄失效时查找）
                if (_workerWHandle == IntPtr.Zero)
                {
                    FindDesktopWindows();
                }

                if (_workerWHandle != IntPtr.Zero)
                {
                    var hwnd = new WindowInteropHelper(this).Handle;
                    // 先记录当前 WPF 逻辑坐标（屏幕坐标，DIP）
                    var targetLeft = Left;
                    var targetTop = Top;
                    // SetParent 会导致 Windows 自动调整位置
                    SetParent(hwnd, _workerWHandle);
                    Topmost = false;
                    // 用 Win32 直接设定精确的屏幕像素坐标，防止 SetParent 导致偏移
                    var source = PresentationSource.FromVisual(this);
                    double dpiX = 1.0, dpiY = 1.0;
                    if (source?.CompositionTarget != null)
                    {
                        dpiX = source.CompositionTarget.TransformToDevice.M11;
                        dpiY = source.CompositionTarget.TransformToDevice.M22;
                    }
                    int pixelX = (int)(targetLeft * dpiX);
                    int pixelY = (int)(targetTop * dpiY);
                    SetWindowPos(hwnd, HWND_BOTTOM, pixelX, pixelY, 0, 0,
                        SWP_NOACTIVATE | SWP_NOSIZE | SWP_SHOWWINDOW);
                    // 同步 WPF 属性
                    Left = targetLeft;
                    Top = targetTop;
                    System.Diagnostics.Debug.WriteLine($"[桌面层级] WorkerW 挂载 pos=({targetLeft},{targetTop}) px=({pixelX},{pixelY}) dpi=({dpiX},{dpiY})");
                }
                else
                {
                    // WorkerW未找到（显示器关闭/Explorer 挂起），保持默认层级
                    // 等下一次定时器检查时会重新尝试挂载到 WorkerW
                    Topmost = false;
                    System.Diagnostics.Debug.WriteLine("[桌面层级] WorkerW未找到，保持默认层级，等待重试");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"设置桌面层级失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 查找桌面窗口
        /// </summary>
        private void FindDesktopWindows()
        {
            // 方法：通过发送消息创建WorkerW窗口
            var progman = FindWindow("Progman", "Program Manager");
            if (progman == IntPtr.Zero) return;

            // 发送消息让Progman创建WorkerW窗口
            SendMessageTimeout(progman, WM_SPAWN_WORKERW, IntPtr.Zero, IntPtr.Zero, 0, 1000, out _);

            // 枚举所有窗口，找到WorkerW
            EnumWindows((hWnd, lParam) =>
            {
                var shellWnd = FindWindowEx(hWnd, IntPtr.Zero, "SHELLDLL_DefView", null);
                if (shellWnd != IntPtr.Zero)
                {
                    // 找到包含SHELLDLL_DefView的窗口，其下一个兄弟窗口就是WorkerW
                    var workerW = FindWindowEx(IntPtr.Zero, hWnd, "WorkerW", null);
                    if (workerW != IntPtr.Zero)
                    {
                        _workerWHandle = workerW;
                        return false; // 停止枚举
                    }
                }
                return true; // 继续枚举
            }, IntPtr.Zero);
        }

        #endregion

        #region 鼠标交互

        private void OnMouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            _isMouseOver = true;
            AnimateOpacity(_hoverOpacity);
        }

        private void OnMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            var position = System.Windows.Input.Mouse.GetPosition(this);
            if (position.X < 0 || position.Y < 0 || position.X > ActualWidth || position.Y > ActualHeight)
            {
                _isMouseOver = false;
                AnimateOpacity(_normalOpacity);
            }
        }

        private void OnMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // 锁定时不允许拖拽
            if (_isLocked) return;

            if (e.ClickCount == 1)
            {
                _isDragging = true;
                _dragStartPoint = e.GetPosition(this);
                CaptureMouse();
                e.Handled = true;
            }
        }

        private void OnMouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                ReleaseMouseCapture();
                e.Handled = true;
            }
        }

        private void OnMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_isDragging && e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
            {
                var currentPoint = e.GetPosition(this);
                var offset = currentPoint - _dragStartPoint;

                var newLeft = Left + offset.X;
                var newTop = Top + offset.Y;

                // 吸附对齐
                var snap = CalculateSnap(newLeft, newTop);
                Left = snap.X;
                Top = snap.Y;

                // 触发防抖保存
                _saveTimer?.Stop();
                _saveTimer?.Start();

                e.Handled = true;
            }
        }

        /// <summary>
        /// 计算吸附位置
        /// </summary>
        private (double X, double Y) CalculateSnap(double left, double top)
        {
            double snapX = left, snapY = top;
            double bestDx = SnapThreshold, bestDy = SnapThreshold;

            var myRight = left + Width;
            var myBottom = top + Height;
            var myCenterX = left + Width / 2;
            var myCenterY = top + Height / 2;

            BaseWidgetWindow[] snapshot;
            lock (_allWidgetsLock) { snapshot = _allWidgets.ToArray(); }
            foreach (var other in snapshot)
            {
                if (other == this || !other.IsVisible) continue;

                var oLeft = other.Left;
                var oTop = other.Top;
                var oRight = oLeft + other.Width;
                var oBottom = oTop + other.Height;
                var oCenterX = oLeft + other.Width / 2;
                var oCenterY = oTop + other.Height / 2;

                // 水平吸附：我的左/右/中 对齐 其他左/右/中
                TrySnap(left, oLeft, ref snapX, ref bestDx, 0);
                TrySnap(left, oRight, ref snapX, ref bestDx, 0);
                TrySnap(myRight, oLeft, ref snapX, ref bestDx, -Width);
                TrySnap(myRight, oRight, ref snapX, ref bestDx, -Width);
                TrySnap(myCenterX, oCenterX, ref snapX, ref bestDx, -Width / 2);

                // 垂直吸附：我的上/下/中 对齐 其他上/下/中
                TrySnap(top, oTop, ref snapY, ref bestDy, 0);
                TrySnap(top, oBottom, ref snapY, ref bestDy, 0);
                TrySnap(myBottom, oTop, ref snapY, ref bestDy, -Height);
                TrySnap(myBottom, oBottom, ref snapY, ref bestDy, -Height);
                TrySnap(myCenterY, oCenterY, ref snapY, ref bestDy, -Height / 2);
            }

            return (snapX, snapY);
        }

        /// <summary>
        /// 尝试吸附到目标位置
        /// </summary>
        private static void TrySnap(double myEdge, double otherEdge, ref double snapPos, ref double bestDist, double offset)
        {
            var dist = Math.Abs(myEdge - otherEdge);
            if (dist < bestDist)
            {
                bestDist = dist;
                snapPos = otherEdge + offset;
            }
        }

        #endregion

        #region 位置和大小保存

        private void OnLocationChanged(object? sender, EventArgs e)
        {
            if (!_isDragging)
            {
                // 非拖拽导致的位置变化（如窗口吸附），也保存
                _saveTimer?.Stop();
                _saveTimer?.Start();
            }
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            // 防重入（吸附调整大小会再次触发 SizeChanged）
            if (_isResizing) return;

            // 调整大小时，右/下边缘自动吸附到其他组件
            if (ResizeMode == ResizeMode.CanResizeWithGrip && e.PreviousSize.Width != e.NewSize.Width || e.PreviousSize.Height != e.NewSize.Height)
            {
                _isResizing = true;
                try
                {
                    var (newWidth, newHeight) = CalculateResizeSnap(e.NewSize.Width, e.NewSize.Height);
                    if (Math.Abs(newWidth - e.NewSize.Width) > 0.5 || Math.Abs(newHeight - e.NewSize.Height) > 0.5)
                    {
                        Width = newWidth;
                        Height = newHeight;
                    }
                }
                finally
                {
                    _isResizing = false;
                }
            }

            SavePosition();
        }

        /// <summary>
        /// 计算调整大小时的吸附值（右/下边缘对齐）
        /// </summary>
        private (double Width, double Height) CalculateResizeSnap(double newWidth, double newHeight)
        {
            var right = Left + newWidth;
            var bottom = Top + newHeight;
            var bestDw = (double)SnapThreshold;
            var bestDh = (double)SnapThreshold;
            var snapW = newWidth;
            var snapH = newHeight;

            BaseWidgetWindow[] snapshot2;
            lock (_allWidgetsLock) { snapshot2 = _allWidgets.ToArray(); }
            foreach (var other in snapshot2)
            {
                if (other == this || !other.IsVisible) continue;

                var oLeft = other.Left;
                var oTop = other.Top;
                var oRight = oLeft + other.Width;
                var oBottom = oTop + other.Height;

                // 右边缘对齐：我的右 → 其他左/右
                TryResizeSnap(right, oLeft, newWidth, ref snapW, ref bestDw);
                TryResizeSnap(right, oRight, newWidth, ref snapW, ref bestDw);

                // 下边缘对齐：我的下 → 其他上/下
                TryResizeSnap(bottom, oTop, newHeight, ref snapH, ref bestDh);
                TryResizeSnap(bottom, oBottom, newHeight, ref snapH, ref bestDh);
            }

            // 吸附后不能小于最小尺寸
            snapW = Math.Max(snapW, MinWidth > 0 ? MinWidth : 50);
            snapH = Math.Max(snapH, MinHeight > 0 ? MinHeight : 50);
            return (snapW, snapH);
        }

        /// <summary>
        /// 尝试调整大小吸附
        /// </summary>
        private static void TryResizeSnap(double myEdge, double otherEdge, double currentSize, ref double snapSize, ref double bestDist)
        {
            var dist = Math.Abs(myEdge - otherEdge);
            if (dist < bestDist)
            {
                bestDist = dist;
                // 调整宽度/高度使右/下边缘对齐到目标
                snapSize = currentSize + (otherEdge - myEdge);
            }
        }

        /// <summary>
        /// 保存位置到配置
        /// </summary>
        protected virtual void SavePosition()
        {
            // 恢复期间不保存，防止覆盖不完整配置
            if (_widgetManager.IsRestoring) return;

            _config.Position.X = Left;
            _config.Position.Y = Top;
            _config.Position.Width = Width;
            _config.Position.Height = Height;

            try
            {
                _widgetManager.Save();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存位置失败: {ex.Message}");
            }
        }

        #endregion

        #region 透明度动画

        /// <summary>
        /// 平滑过渡透明度
        /// </summary>
        private void AnimateOpacity(double targetOpacity)
        {
            var animation = new System.Windows.Media.Animation.DoubleAnimation
            {
                To = targetOpacity,
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseInOut }
            };
            BeginAnimation(OpacityProperty, animation);
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 更新配置中的透明度设置
        /// </summary>
        public void UpdateOpacitySettings(double normalOpacity, double hoverOpacity)
        {
            _normalOpacity = normalOpacity;
            _hoverOpacity = hoverOpacity;
            _config.NormalOpacity = normalOpacity;
            _config.HoverOpacity = hoverOpacity;

            // 立即应用当前状态的透明度
            Opacity = _isMouseOver ? _hoverOpacity : _normalOpacity;
        }

        /// <summary>
        /// 显示或隐藏组件
        /// </summary>
        public void SetVisible(bool visible)
        {
            if (visible)
            {
                Show();
                // 重新挂到 WorkerW 并放到最底层
                SetDesktopLevel();
            }
            else
            {
                Hide();
            }
        }

        /// <summary>
        /// 切换锁定状态
        /// </summary>
        public void ToggleLock()
        {
            IsLocked = !IsLocked;
        }

        /// <summary>
        /// 更新锁定状态
        /// </summary>
        private void UpdateLockState()
        {
            if (_isLocked)
            {
                // 锁定时禁用调整大小
                ResizeMode = ResizeMode.NoResize;
            }
            else
            {
                // 解锁时允许调整大小
                ResizeMode = ResizeMode.CanResizeWithGrip;
            }
        }

        /// <summary>
        /// 加载锁定状态
        /// </summary>
        protected void LoadLockState()
        {
            if (_config.Settings.TryGetValue("isLocked", out var locked))
            {
                _isLocked = ToBool(locked);
                UpdateLockState();
            }
        }

        /// <summary>
        /// 安全类型转换（兼容 JsonElement 和原始类型）
        /// </summary>
        protected static bool ToBool(object? value, bool defaultValue = false)
        {
            if (value is System.Text.Json.JsonElement je)
                return je.ValueKind == System.Text.Json.JsonValueKind.True;
            try { return Convert.ToBoolean(value); }
            catch { return defaultValue; }
        }

        protected static int ToInt(object? value, int defaultValue = 0)
        {
            if (value is System.Text.Json.JsonElement je && je.ValueKind == System.Text.Json.JsonValueKind.Number)
                return je.GetInt32();
            try { return Convert.ToInt32(value); }
            catch { return defaultValue; }
        }

        protected static double ToDouble(object? value, double defaultValue = 0)
        {
            if (value is System.Text.Json.JsonElement je && je.ValueKind == System.Text.Json.JsonValueKind.Number)
                return je.GetDouble();
            try { return Convert.ToDouble(value); }
            catch { return defaultValue; }
        }

        protected static string ToStr(object? value, string defaultValue = "")
        {
            if (value is System.Text.Json.JsonElement je && je.ValueKind == System.Text.Json.JsonValueKind.String)
                return je.GetString() ?? defaultValue;
            return value?.ToString() ?? defaultValue;
        }

        #endregion

        #region 关闭控制

        /// <summary>
        /// 程序主动关闭组件（右键菜单"关闭组件"调用）
        /// </summary>
        public void RequestClose()
        {
            _requestingClose = true;
            Close();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // 允许关闭：应用退出 或 组件自身调用 RequestClose
            if (!IsAppClosing && !_requestingClose)
            {
                e.Cancel = true; // 拦截 Alt+F4
                return;
            }
            base.OnClosing(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            _saveTimer?.Stop();
            _saveTimer = null;
            _workerWCheckTimer?.Stop();
            _workerWCheckTimer = null;
            base.OnClosed(e);
        }

        #endregion
    }
}
