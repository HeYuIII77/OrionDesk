using System;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using OrionDesk.BLL.Models;
using OrionDesk.BLL.Services;

namespace OrionDesk.UI.Windows
{
    /// <summary>
    /// 时钟组件
    /// 支持数字和模拟两种样式
    /// </summary>
    public partial class ClockWidget : BaseWidgetWindow
    {
        private readonly DispatcherTimer _timer;
        private readonly DispatcherTimer _weatherTimer;
        private readonly ClockSettings _settings;
        private readonly LunarCalendarService _lunarService;
        private readonly WeatherService _weatherService;
        private bool _isDigital = true;
        private bool _showLunar = true;
        private bool _showWeather = true;
        private WeatherInfo? _lastWeatherInfo;

        public ClockWidget(WidgetConfig config, WidgetManager widgetManager, WeatherService weatherService)
            : base(config, widgetManager)
        {
            InitializeComponent();

            _weatherService = weatherService;

            // 加载设置
            _settings = LoadSettings(config);

            // 初始化农历服务
            _lunarService = new LunarCalendarService();

            // 初始化时钟定时器（每秒）
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _timer.Tick += Timer_Tick;

            // 初始化天气定时器（按配置间隔）
            _weatherTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMinutes(_widgetManager.Settings.Weather.RefreshMinutes)
            };
            _weatherTimer.Tick += WeatherTimer_Tick;

            // 窗口大小调整事件
            SizeChanged += ClockWidget_SizeChanged;

            // 直接初始化（InitializeComponent 后控件已可用）
            LoadLockState();
            UpdateClockStyle();
            UpdateClockMenuState();
            UpdateLockButton();
            UpdateClock();
            _timer.Start();

            // 立即请求一次天气，然后启动定时器
            UpdateWeather();
            _weatherTimer.Start();
        }

        /// <summary>
        /// 加载时钟设置
        /// </summary>
        private ClockSettings LoadSettings(WidgetConfig config)
        {
            var settings = new ClockSettings();

            if (config.Settings.TryGetValue("style", out var style))
                settings.Style = style.ToString() ?? "Digital";

            if (config.Settings.TryGetValue("timeFormat", out var timeFormat))
                settings.TimeFormat = timeFormat.ToString() ?? "HH:mm:ss";

            if (config.Settings.TryGetValue("showDate", out var showDate))
                settings.ShowDate = ToBool(showDate, true);

            if (config.Settings.TryGetValue("dateFormat", out var dateFormat))
                settings.DateFormat = dateFormat.ToString() ?? "yyyy-MM-dd dddd";

            // 应用初始样式（不在这里调用 UpdateClockStyle，等窗口加载完成后调用）
            _isDigital = settings.Style == "Digital";

            return settings;
        }

        /// <summary>
        /// 保存时钟设置
        /// </summary>
        private void SaveSettings()
        {
            _config.Settings["style"] = _isDigital ? "Digital" : "Analog";
            _config.Settings["timeFormat"] = _settings.TimeFormat;
            _config.Settings["showDate"] = _settings.ShowDate;
            _config.Settings["dateFormat"] = _settings.DateFormat;

            if (!_widgetManager.IsRestoring)
            {
                try { _widgetManager.Save(); }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"保存时钟设置失败: {ex.Message}"); }
            }
        }

        /// <summary>
        /// 定时器更新
        /// </summary>
        private void Timer_Tick(object? sender, EventArgs e)
        {
            UpdateClock();
        }

        /// <summary>
        /// 天气定时器更新
        /// </summary>
        private void WeatherTimer_Tick(object? sender, EventArgs e)
        {
            UpdateWeather();
        }

        /// <summary>
        /// 更新时钟显示
        /// </summary>
        private void UpdateClock()
        {
            var now = DateTime.Now;

            if (_isDigital)
            {
                // 数字时钟
                TimeText.Text = now.ToString(_settings.TimeFormat);
            }
            else
            {
                // 模拟时钟
                UpdateAnalogClock(now);
            }

            // 日期和农历在两种模式下都更新
            DateText.Text = now.ToString(_settings.DateFormat);

            if (_showLunar)
            {
                UpdateLunarInfo(now);
            }
        }

        /// <summary>
        /// 立即刷新天气（外部调用，如设置变更后）
        /// </summary>
        public void RefreshWeather()
        {
            _weatherTimer.Stop();
            UpdateWeather();
            _weatherTimer.Start();
        }

        /// <summary>
        /// 更新天气显示（摘要行 + ToolTip 详细信息）
        /// </summary>
        private async void UpdateWeather()
        {
            if (!_showWeather)
            {
                WeatherText.Text = "";
                WeatherText.ToolTip = null;
                return;
            }

            var apiKey = _widgetManager.Settings.Weather.ApiKey;
            var apiHost = _widgetManager.Settings.Weather.ApiHost;
            System.Diagnostics.Debug.WriteLine($"[天气] ApiKey={apiKey}, ApiHost={apiHost}");

            if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(apiHost))
            {
                WeatherText.Text = "请在设置中配置天气 API";
                WeatherText.ToolTip = null;
                return;
            }

            try
            {
                var weather = await _weatherService.GetWeatherAsync(
                    apiKey,
                    apiHost,
                    _widgetManager.Settings.Weather.RefreshMinutes,
                    _widgetManager.Settings.Weather.CityLat,
                    _widgetManager.Settings.Weather.CityLon,
                    _widgetManager.Settings.Weather.CityName);

                if (weather != null && !string.IsNullOrEmpty(weather.Temperature))
                {
                    // 摘要行：城市 天气 温度 空气等级
                    var summary = $"{weather.CityName} {weather.WeatherText} {weather.Temperature}°C";
                    if (!string.IsNullOrEmpty(weather.AirCategory))
                        summary += $" {weather.AirCategory}";
                    WeatherText.Text = summary;

                    // ToolTip：详细天气信息
                    WeatherText.ToolTip = BuildWeatherToolTip(weather);

                    // 缓存最新天气数据供详情弹窗使用
                    _lastWeatherInfo = weather;

                    System.Diagnostics.Debug.WriteLine($"[天气] 更新成功: {summary}");
                }
                else
                {
                    WeatherText.Text = "天气获取失败";
                    WeatherText.ToolTip = null;
                    System.Diagnostics.Debug.WriteLine("[天气] GetWeatherAsync 返回 null 或温度为空");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[天气] 异常: {ex.Message}");
                WeatherText.Text = "天气获取失败";
                WeatherText.ToolTip = null;
            }
        }

        /// <summary>
        /// 构建天气 ToolTip 内容（悬停时显示详细信息）
        /// </summary>
        private string BuildWeatherToolTip(WeatherInfo w)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"📍 {w.CityName}");
            sb.AppendLine($"🌡 {w.WeatherText} {w.Temperature}°C（体感 {w.FeelsLike}°C）");
            sb.AppendLine($"💧 湿度 {w.Humidity}%  🌬 {w.WindDir}");

            if (!string.IsNullOrEmpty(w.Aqi))
                sb.AppendLine($"🌿 空气 {w.AirCategory}（AQI {w.Aqi} PM2.5 {w.Pm2p5}）");

            if (!string.IsNullOrEmpty(w.Sunrise))
                sb.AppendLine($"🌅 日出 {w.Sunrise}  🌙 日落 {w.Sunset}");

            if (w.Forecast.Count > 0)
            {
                sb.AppendLine("─── 未来天气 ───");
                foreach (var day in w.Forecast)
                {
                    var label = FormatForecastDate(day.Date);
                    sb.AppendLine($"  {label} {day.TextDay} {day.TempMin}°~{day.TempMax}°");
                }
            }

            if (w.Warnings.Count > 0)
            {
                sb.AppendLine("─── 预警 ───");
                foreach (var warn in w.Warnings)
                    sb.AppendLine($"⚠ {warn.Title}");
            }

            if (w.Indices.Count > 0)
            {
                sb.AppendLine("─── 生活指数 ───");
                foreach (var idx in w.Indices)
                    sb.AppendLine($"  {idx.Name}: {idx.Category}");
            }

            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// 格式化预报日期（今天/明天/后天/MM-dd）
        /// </summary>
        private static string FormatForecastDate(string dateStr)
        {
            if (DateTime.TryParse(dateStr, out var date))
            {
                var today = DateTime.Today;
                if (date == today) return "今天";
                if (date == today.AddDays(1)) return "明天";
                if (date == today.AddDays(2)) return "后天";
                return date.ToString("MM/dd");
            }
            return dateStr;
        }

        /// <summary>
        /// 更新农历信息
        /// </summary>
        private void UpdateLunarInfo(DateTime date)
        {
            try
            {
                var lunarDate = _lunarService.GetLunarDate(date);
                var lunarText = $"{lunarDate.StemBranchYear}年 {lunarDate.MonthName}{lunarDate.DayName}";
                LunarText.Text = lunarText;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取农历信息失败: {ex.Message}");
                LunarText.Text = "";
            }
        }

        /// <summary>
        /// 更新模拟时钟指针
        /// </summary>
        private void UpdateAnalogClock(DateTime time)
        {
            // 时针：每小时30度，每分钟0.5度
            double hourAngle = (time.Hour % 12) * 30 + time.Minute * 0.5;
            RotateHourHand(hourAngle);

            // 分针：每分钟6度
            double minuteAngle = time.Minute * 6 + time.Second * 0.1;
            RotateMinuteHand(minuteAngle);

            // 秒针：每秒6度
            double secondAngle = time.Second * 6;
            RotateSecondHand(secondAngle);
        }

        private void RotateHourHand(double angle)
        {
            var transform = new System.Windows.Media.RotateTransform(angle, 50, 50);
            HourHand.RenderTransform = transform;
        }

        private void RotateMinuteHand(double angle)
        {
            var transform = new System.Windows.Media.RotateTransform(angle, 50, 50);
            MinuteHand.RenderTransform = transform;
        }

        private void RotateSecondHand(double angle)
        {
            var transform = new System.Windows.Media.RotateTransform(angle, 50, 50);
            SecondHand.RenderTransform = transform;
        }

        /// <summary>
        /// 更新时钟样式（数字/模拟）
        /// </summary>
        private void UpdateClockStyle()
        {
            if (_isDigital)
            {
                TimeText.Visibility = Visibility.Visible;
                AnalogClock.Visibility = Visibility.Collapsed;
            }
            else
            {
                TimeText.Visibility = Visibility.Collapsed;
                AnalogClock.Visibility = Visibility.Visible;
            }

            // 日期和农历在两种模式下都显示
            DateText.Visibility = _settings.ShowDate ? Visibility.Visible : Visibility.Collapsed;
            LunarText.Visibility = _showLunar ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// 窗口大小改变时调整模拟时钟大小
        /// </summary>
        private void ClockWidget_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!_isDigital)
            {
                // 根据窗口大小调整模拟时钟
                var size = Math.Min(ActualWidth, ActualHeight) - 40;
                if (size > 0)
                {
                    AnalogClock.Width = size;
                    AnalogClock.Height = size;
                }
            }
        }

        #region 右键菜单事件

        private void DigitalClock_Click(object sender, RoutedEventArgs e)
        {
            _isDigital = true;
            _settings.Style = "Digital";
            UpdateClockStyle();
            UpdateClockMenuState();
            SaveSettings();
        }

        private void AnalogClock_Click(object sender, RoutedEventArgs e)
        {
            _isDigital = false;
            _settings.Style = "Analog";
            UpdateClockStyle();
            UpdateClockMenuState();
            SaveSettings();
        }

        /// <summary>
        /// 更新时钟样式菜单的勾选状态
        /// </summary>
        private void UpdateClockMenuState()
        {
            DigitalClockMenuItem.IsChecked = _isDigital;
            AnalogClockMenuItem.IsChecked = !_isDigital;
        }

        private void ToggleDate_Click(object sender, RoutedEventArgs e)
        {
            _settings.ShowDate = !_settings.ShowDate;
            DateText.Visibility = _settings.ShowDate ? Visibility.Visible : Visibility.Collapsed;
            SaveSettings();
        }

        private void ToggleLunar_Click(object sender, RoutedEventArgs e)
        {
            _showLunar = !_showLunar;
            LunarText.Visibility = _showLunar ? Visibility.Visible : Visibility.Collapsed;
            if (_showLunar)
            {
                UpdateLunarInfo(DateTime.Now);
            }
        }

        private void ToggleWeather_Click(object sender, RoutedEventArgs e)
        {
            _showWeather = !_showWeather;
            WeatherText.Visibility = _showWeather ? Visibility.Visible : Visibility.Collapsed;
            if (_showWeather)
            {
                UpdateWeather();
            }
        }

        private void ShowWeatherDetail_Click(object sender, RoutedEventArgs e)
        {
            if (_lastWeatherInfo == null) return;
            var detailWindow = new WeatherDetailWindow(_lastWeatherInfo);
            detailWindow.Show();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            RequestClose();
        }

        private void Lock_Click(object sender, RoutedEventArgs e)
        {
            ToggleLock();
            UpdateLockButton();
        }

        /// <summary>
        /// 更新锁定按钮显示
        /// </summary>
        private void UpdateLockButton()
        {
            LockButton.Content = IsLocked ? "🔒" : "🔓";
            LockButton.ToolTip = IsLocked ? "解锁" : "锁定";
            LockMenuItem.IsChecked = IsLocked;
            LockMenuItem.Header = IsLocked ? "解锁" : "锁定";
        }

        #endregion

        protected override void OnClosed(EventArgs e)
        {
            _timer?.Stop();
            _weatherTimer?.Stop();
            base.OnClosed(e);
        }
    }
}
