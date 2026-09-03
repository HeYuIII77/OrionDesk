using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Forms = System.Windows.Forms;
using Input = System.Windows.Input;

namespace OrionDesk.UI.Windows
{
    /// <summary>
    /// 桌面悬浮球 - 双击切换组件层级（置顶/桌面层）
    /// </summary>
    public partial class DesktopBallWindow : Window
    {
        #region 字段

        private bool _isDragging;
        private System.Windows.Point _dragStart;
        private bool _hasMoved;
        private bool _widgetsOnTop;
        private readonly Action<bool>? _toggleTopmost;
        private readonly Action<double, double>? _onPositionChanged;

        private const double DragThreshold = 5.0;
        private const double SnapThreshold = 20.0;

        #endregion

        #region 构造函数

        private static readonly System.Windows.Media.Color BorderNormal = System.Windows.Media.Color.FromRgb(0, 120, 212);
        private static readonly System.Windows.Media.Color BorderHover = System.Windows.Media.Color.FromRgb(30, 144, 255);

        public DesktopBallWindow(double initX = -1, double initY = -1,
            Action<bool>? toggleTopmost = null,
            Action<double, double>? onPositionChanged = null)
        {
            InitializeComponent();
            _toggleTopmost = toggleTopmost;
            _onPositionChanged = onPositionChanged;

            BallBorder.BorderBrush = new SolidColorBrush(BorderNormal);

            var workArea = SystemParameters.WorkArea;
            if (initX >= 0 && initY >= 0)
            {
                Left = initX;
                Top = initY;
            }
            else
            {
                Left = workArea.Right - Width - 20;
                Top = workArea.Bottom - Height - 20;
            }

            // 失去焦点时自动退出置顶模式
            System.Windows.Application.Current.Deactivated += OnAppDeactivated;
            Closed += (s, e) => System.Windows.Application.Current.Deactivated -= OnAppDeactivated;
        }

        private void OnAppDeactivated(object? sender, EventArgs e)
        {
            if (_widgetsOnTop)
            {
                _widgetsOnTop = false;
                _toggleTopmost?.Invoke(false);
            }
        }

        #endregion

        #region 切换层级

        private void DoToggleDesktop()
        {
            _widgetsOnTop = !_widgetsOnTop;
            _toggleTopmost?.Invoke(_widgetsOnTop);
        }

        #endregion

        #region 边缘吸附

        private void SnapToEdge()
        {
            var ballCenter = new System.Drawing.Point(
                (int)(Left + Width / 2),
                (int)(Top + Height / 2));
            var screen = Forms.Screen.FromPoint(ballCenter);
            var workArea = screen.WorkingArea;

            var targetLeft = Left;
            var targetTop = Top;

            if (Left < workArea.Left + SnapThreshold)
                targetLeft = workArea.Left - Width * 0.6;
            else if (Left + Width > workArea.Right - SnapThreshold)
                targetLeft = workArea.Right - Width * 0.4;

            if (Top < workArea.Top + SnapThreshold)
                targetTop = workArea.Top - Height * 0.6;
            else if (Top + Height > workArea.Bottom - SnapThreshold)
                targetTop = workArea.Bottom - Height * 0.4;

            Left = targetLeft;
            Top = targetTop;
            _onPositionChanged?.Invoke(Left, Top);
        }

        #endregion

        #region 悬停效果

        private void Window_MouseEnter(object sender, Input.MouseEventArgs e)
        {
            AnimateScale(1.15);
            AnimateOpacity(1.0);
            AnimateBorderColor(BorderHover);
        }

        private void Window_MouseLeave(object sender, Input.MouseEventArgs e)
        {
            if (!_isDragging)
            {
                AnimateScale(1.0);
                AnimateOpacity(0.75);
                AnimateBorderColor(BorderNormal);
            }
        }

        private void AnimateScale(double to)
        {
            var anim = new DoubleAnimation(to, TimeSpan.FromMilliseconds(150));
            BallScale.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
            BallScale.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
        }

        private void AnimateOpacity(double to)
        {
            var anim = new DoubleAnimation(to, TimeSpan.FromMilliseconds(150));
            BeginAnimation(OpacityProperty, anim);
        }

        private void AnimateBorderColor(System.Windows.Media.Color to)
        {
            if (BallBorder.BorderBrush is SolidColorBrush brush)
            {
                var anim = new ColorAnimation(to, TimeSpan.FromMilliseconds(150));
                brush.BeginAnimation(SolidColorBrush.ColorProperty, anim);
            }
        }

        #endregion

        #region 拖拽

        private void Window_MouseLeftButtonDown(object sender, Input.MouseButtonEventArgs e)
        {
            _isDragging = true;
            _hasMoved = false;
            _dragStart = e.GetPosition(this);
            CaptureMouse();
            e.Handled = true;
        }

        private void Window_MouseMove(object sender, Input.MouseEventArgs e)
        {
            if (!_isDragging) return;

            var pos = e.GetPosition(this);
            var dx = pos.X - _dragStart.X;
            var dy = pos.Y - _dragStart.Y;

            if (Math.Abs(dx) > DragThreshold || Math.Abs(dy) > DragThreshold)
                _hasMoved = true;

            if (_hasMoved)
            {
                var newLeft = Left + dx;
                var newTop = Top + dy;

                var screen = Forms.Screen.FromPoint(new System.Drawing.Point(
                    (int)(newLeft + Width / 2),
                    (int)(newTop + Height / 2)));
                var workArea = screen.WorkingArea;
                var hideMax = Width * 0.6;

                newLeft = Math.Max(workArea.Left - hideMax, Math.Min(newLeft, workArea.Right - Width + hideMax));
                newTop = Math.Max(workArea.Top - hideMax, Math.Min(newTop, workArea.Bottom - Height + hideMax));

                Left = newLeft;
                Top = newTop;
            }
        }

        private void Window_MouseLeftButtonUp(object sender, Input.MouseButtonEventArgs e)
        {
            if (!_isDragging) return;

            _isDragging = false;
            ReleaseMouseCapture();

            if (_hasMoved)
            {
                SnapToEdge();
                AnimateScale(1.0);
                AnimateOpacity(0.75);
                AnimateBorderColor(BorderNormal);
                _onPositionChanged?.Invoke(Left, Top);
            }

            e.Handled = true;
        }

        private void Window_MouseDoubleClick(object sender, Input.MouseButtonEventArgs e)
        {
            DoToggleDesktop();
            e.Handled = true;
        }

        #endregion

        #region 关闭

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        #endregion
    }
}
