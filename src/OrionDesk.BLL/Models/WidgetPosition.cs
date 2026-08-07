namespace OrionDesk.BLL.Models
{
    /// <summary>
    /// 组件位置和大小
    /// </summary>
    public class WidgetPosition
    {
        /// <summary>
        /// X坐标
        /// </summary>
        public double X { get; set; }

        /// <summary>
        /// Y坐标
        /// </summary>
        public double Y { get; set; }

        /// <summary>
        /// 宽度
        /// </summary>
        public double Width { get; set; } = 200;

        /// <summary>
        /// 高度
        /// </summary>
        public double Height { get; set; } = 100;
    }
}
