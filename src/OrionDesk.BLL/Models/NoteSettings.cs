namespace OrionDesk.BLL.Models
{
    /// <summary>
    /// 便签组件设置
    /// </summary>
    public class NoteSettings
    {
        /// <summary>
        /// 便签内容
        /// </summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// 背景颜色（颜色名称或十六进制值）
        /// </summary>
        public string BackgroundColor { get; set; } = "#FFFACD";  // 柠檬绸黄

        /// <summary>
        /// 字体大小
        /// </summary>
        public double FontSize { get; set; } = 14;
    }
}
