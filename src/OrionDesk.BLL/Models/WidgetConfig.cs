namespace OrionDesk.BLL.Models
{
    /// <summary>
    /// 单个组件的配置
    /// </summary>
    public class WidgetConfig
    {
        /// <summary>
        /// 组件唯一标识
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// 组件类型（clock, monitor, launcher, note, folder）
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// 组件位置和大小
        /// </summary>
        public WidgetPosition Position { get; set; } = new WidgetPosition();

        /// <summary>
        /// 平时透明度（0.0 - 1.0）
        /// </summary>
        public double NormalOpacity { get; set; } = 1.0;

        /// <summary>
        /// 悬浮透明度（0.0 - 1.0）
        /// </summary>
        public double HoverOpacity { get; set; } = 1.0;

        /// <summary>
        /// 是否置顶
        /// </summary>
        public bool Topmost { get; set; } = false;

        /// <summary>
        /// 组件特定设置（JSON对象）
        /// </summary>
        public Dictionary<string, object> Settings { get; set; } = new Dictionary<string, object>();
    }
}
