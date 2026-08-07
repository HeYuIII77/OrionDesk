namespace OrionDesk.BLL.Models
{
    /// <summary>
    /// 应用全局设置
    /// </summary>
    public class AppSettings
    {
        private WeatherSettings _weather = new WeatherSettings();

        /// <summary>
        /// 组件配置列表
        /// </summary>
        public List<WidgetConfig> Widgets { get; set; } = new List<WidgetConfig>();

        /// <summary>
        /// 天气设置（永不为 null）
        /// </summary>
        public WeatherSettings Weather
        {
            get => _weather ??= new WeatherSettings();
            set => _weather = value ?? new WeatherSettings();
        }

        /// <summary>
        /// 是否开机启动
        /// </summary>
        public bool StartWithWindows { get; set; } = false;

        /// <summary>
        /// 是否显示所有组件
        /// </summary>
        public bool ShowAllWidgets { get; set; } = true;

        /// <summary>
        /// 上次保存时间
        /// </summary>
        public DateTime LastSaved { get; set; } = DateTime.Now;
    }
}
