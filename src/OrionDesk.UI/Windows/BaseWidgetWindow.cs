using System;
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

        // 调整大小吸附防重入
        private bool _isResizing = false;

        // 应用退出标志（MainWindow 关闭前设置，允许组件关闭）
        internal static bool IsAppClosing { get; set; } = false;

        // 单个组件主动关闭标志
        private bool _requestingClose = false;

        #endregion

        #region 构造函数

        protected BaseWidgetWindow(WidgetConfig config, WidgetManager widgetManager)
        {
            _config = config;
            _widgetManager = widgetManager;

            // 设置窗口属性
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = System.Windows.Media.Brushes.Transparent;
            ShowInTaskbar = false;
            ResizeMode = ResizeMode.CanResizeWithGrip;
            Topmost = false; // 我们自己控制层级

            // 添加投影效果（增加层次感）
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = System.Windows.Media.Colors.Black,
                Direction = 270,
                ShadowDepth = 3,
                Opacity = 0.25,
                BlurRadius = 10
            };

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
        }

        #endregion

        #region 窗口生命周期

        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // 隐藏任务栏图标
                HideFromTaskbar();

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
                // 找到桌面WorkerW窗口（仅首次或句柄失效时查找）
                if (_workerWHandle == IntPtr.Zero)
                {
                    FindDesktopWindows();
                }

                if (_workerWHandle != IntPtr.Zero)
                {
                    var hwnd = new WindowInteropHelper(this).Handle;
                    // 将窗口设置为WorkerW的子窗口，固定在最底层
                    SetParent(hwnd, _workerWHandle);
                    SetWindowPos(hwnd, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOACTIVATE | SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
                    System.Diagnostics.Debug.WriteLine("[桌面层级] 已设置为WorkerW子窗口 (BOTTOM)");
                }
                else
                {
                    // WorkerW未找到，不使用Topmost（会盖住应用程序）
                    System.Diagnostics.Debug.WriteLine("[桌面层级] WorkerW未找到，窗口保持默认层级");
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
            base.OnClosed(e);
        }

        #endregion
    }
}
