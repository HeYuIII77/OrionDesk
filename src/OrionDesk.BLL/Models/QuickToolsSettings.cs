namespace OrionDesk.BLL.Models
{
    /// <summary>
    /// 快捷工具组件设置
    /// </summary>
    public class QuickToolsSettings
    {
        /// <summary>
        /// 工具列表
        /// </summary>
        public List<QuickToolItem> Items { get; set; } = new List<QuickToolItem>();

        /// <summary>
        /// 网格列数
        /// </summary>
        public int Columns { get; set; } = 3;
    }
}
