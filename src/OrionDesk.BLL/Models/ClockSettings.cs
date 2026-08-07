namespace OrionDesk.BLL.Models
{
    /// <summary>
    /// 时钟组件设置
    /// </summary>
    public class ClockSettings
    {
        /// <summary>
        /// 时钟样式：Digital（数字）或 Analog（模拟）
        /// </summary>
        public string Style { get; set; } = "Digital";

        /// <summary>
        /// 数字时钟格式（如 "HH:mm:ss"）
        /// </summary>
        public string TimeFormat { get; set; } = "HH:mm:ss";

        /// <summary>
        /// 是否显示日期
        /// </summary>
        public bool ShowDate { get; set; } = true;

        /// <summary>
        /// 日期格式（如 "yyyy-MM-dd dddd"）
        /// </summary>
        public string DateFormat { get; set; } = "yyyy-MM-dd dddd";
    }
}
