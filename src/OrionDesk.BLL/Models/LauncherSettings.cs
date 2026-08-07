namespace OrionDesk.BLL.Models
{
    /// <summary>
    /// 启动器组件设置
    /// </summary>
    public class LauncherSettings
    {
        /// <summary>
        /// 应用列表
        /// </summary>
        public List<LauncherItem> Items { get; set; } = new List<LauncherItem>();

        /// <summary>
        /// 图标大小（像素）
        /// </summary>
        public int IconSize { get; set; } = 48;

        /// <summary>
        /// 是否显示名称
        /// </summary>
        public bool ShowName { get; set; } = true;

        /// <summary>
        /// 视图模式：Icons（图标平铺）或 List（列表详情）
        /// </summary>
        public string ViewMode { get; set; } = "Icons";
    }
}
